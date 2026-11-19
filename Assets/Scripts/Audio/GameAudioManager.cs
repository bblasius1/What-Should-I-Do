using System.Collections;
using UnityEngine;

/// <summary>Owns music, voice, ambience, and effects playback for the browser game.</summary>
public enum GameMusicTrack { MainMenu, Street, School, Playground }

[DisallowMultipleComponent]
public sealed class GameAudioManager : MonoBehaviour
{
    const string MusicPreference = "wsid.audio.music";
    const string SfxPreference = "wsid.audio.sfx";
    const string VoicePreference = "wsid.audio.voice";
    const string AmbiencePreference = "wsid.audio.ambience";

    public static GameAudioManager Instance { get; private set; }

    public bool IsAudioUnlocked => audioUnlocked;

    AudioSource musicSource;
    AudioSource voiceSource;
    AudioSource sfxSource;
    AudioSource ambienceSource;
    AudioClip activeMusicClip;
    bool activeMusicClipGenerated;
    Coroutine musicRoutine;
    Coroutine voiceRoutine;
    GameMusicTrack pendingTrack = GameMusicTrack.MainMenu;
    GameMusicTrack currentTrack;
    bool hasCurrentTrack;
    bool audioUnlocked;
    bool voiceActive;
    float musicVolume;
    float sfxVolume;
    float voiceVolume;
    float ambienceVolume;
    float fadeMultiplier = 1f;

    public static GameAudioManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var host = new GameObject("Game Audio Manager");
        return host.AddComponent<GameAudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicVolume = PlayerPrefs.GetFloat(MusicPreference, 0.32f);
        sfxVolume = PlayerPrefs.GetFloat(SfxPreference, 0.72f);
        voiceVolume = PlayerPrefs.GetFloat(VoicePreference, 0.95f);
        ambienceVolume = PlayerPrefs.GetFloat(AmbiencePreference, 0.24f);

        musicSource = CreateSource("Music", true);
        voiceSource = CreateSource("Voice", false);
        sfxSource = CreateSource("SFX", false);
        ambienceSource = CreateSource("Ambience", true);
    }

    AudioSource CreateSource(string sourceName, bool loop)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        return source;
    }

    void Update()
    {
        if (musicSource == null)
            return;

        float duck = voiceActive ? 0.28f : 1f;
        float target = musicVolume * duck * fadeMultiplier;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 2.4f);
        voiceSource.volume = voiceVolume;
        sfxSource.volume = sfxVolume;
        ambienceSource.volume = ambienceVolume;
    }

    /// <summary>Call this from a real menu click to satisfy browser audio autoplay policies.</summary>
    public void UnlockAudio()
    {
        if (audioUnlocked)
            return;

        audioUnlocked = true;
        // The first Play must happen synchronously inside the click/key interaction.
        // Waiting for a coroutine can lose the browser's user-activation window.
        LoadAndStartTrack(pendingTrack);
        if (ambienceSource.clip != null && !ambienceSource.isPlaying)
            ambienceSource.Play();
    }

    public void PlayMusic(GameMusicTrack track)
    {
        pendingTrack = track;
        if (!audioUnlocked || musicSource == null)
            return;

        if (hasCurrentTrack && currentTrack == track && musicSource.isPlaying)
            return;

        if (musicRoutine != null)
            StopCoroutine(musicRoutine);

        musicRoutine = StartCoroutine(SwitchMusic(track));
    }

    IEnumerator SwitchMusic(GameMusicTrack track)
    {
        fadeMultiplier = 0f;
        yield return new WaitForSecondsRealtime(0.28f);

        LoadAndStartTrack(track);
        fadeMultiplier = 1f;
        musicRoutine = null;
    }

    void LoadAndStartTrack(GameMusicTrack track)
    {
        musicSource.Stop();
        musicSource.clip = null;
        if (activeMusicClip != null && activeMusicClipGenerated)
            Destroy(activeMusicClip);

        activeMusicClip = Resources.Load<AudioClip>("Audio/Music/BGM_" + track);
        activeMusicClipGenerated = activeMusicClip == null;
        if (activeMusicClipGenerated)
            activeMusicClip = ProceduralMusicLibrary.CreateClip(track);
        musicSource.clip = activeMusicClip;
        musicSource.loop = true;
        musicSource.volume = musicVolume * (voiceActive ? 0.28f : 1f);
        musicSource.Play();

        currentTrack = track;
        hasCurrentTrack = true;
    }

    public void PlayAmbience(string resourceName)
    {
        if (ambienceSource == null || string.IsNullOrWhiteSpace(resourceName))
            return;

        AudioClip clip = Resources.Load<AudioClip>("Audio/Ambience/" + resourceName);
        if (clip == null)
            return;

        if (ambienceSource.clip == clip && ambienceSource.isPlaying)
            return;

        ambienceSource.clip = clip;
        ambienceSource.loop = true;
        if (audioUnlocked)
            ambienceSource.Play();
    }

    public void PlaySfx(string resourceName)
    {
        if (!audioUnlocked || sfxSource == null || string.IsNullOrWhiteSpace(resourceName))
            return;

        AudioClip clip = Resources.Load<AudioClip>("Audio/SFX/" + resourceName);
        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Looks for a prerecorded voice clip under Resources/Audio/Voice/{EN|ZH}/{speaker}/{lineId}.
    /// Missing clips are intentionally ignored so text dialogue always remains usable.
    /// </summary>
    public bool PlayVoiceLine(string speaker, string lineId)
    {
        if (!audioUnlocked || voiceSource == null || string.IsNullOrWhiteSpace(speaker) ||
            string.IsNullOrWhiteSpace(lineId))
            return false;

        string language = GameLanguage.Current == GameLanguageOption.SimplifiedChinese ? "ZH" : "EN";
        AudioClip clip = Resources.Load<AudioClip>($"Audio/Voice/{language}/{speaker}/{lineId}");
        if (clip == null)
            return false;

        StopVoice();
        voiceSource.clip = clip;
        voiceSource.Play();
        voiceActive = true;
        voiceRoutine = StartCoroutine(ReleaseVoiceAfter(clip.length));
        return true;
    }

    public bool PlayDialogueFor(string heading, string prompt)
    {
        if (heading == "Student by the bench")
            return PlayVoiceLine("Student", "L2_STUDENT_002");

        if (heading == "Classmate")
            return PlayVoiceLine("Classmate", "L2_CLASSMATE_002");

        if (heading == "Teacher on duty")
        {
            if (prompt == "I can help. Can you show me who needs me?")
                return PlayVoiceLine("Teacher", "L2_TEACHER_002");
            if (prompt == "What happened to the student?")
                return PlayVoiceLine("Teacher", "L2_TEACHER_003");
            if (prompt == "Where did they fall?")
                return PlayVoiceLine("Teacher", "L2_TEACHER_004");
        }

        return false;
    }

    IEnumerator ReleaseVoiceAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        voiceActive = false;
        voiceRoutine = null;
    }

    public void StopVoice()
    {
        if (voiceRoutine != null)
        {
            StopCoroutine(voiceRoutine);
            voiceRoutine = null;
        }

        if (voiceSource != null)
            voiceSource.Stop();

        voiceActive = false;
    }

    public static float GetMusicVolume() =>
        Instance != null ? Instance.musicVolume : PlayerPrefs.GetFloat(MusicPreference, 0.32f);

    public static float GetSfxVolume() =>
        Instance != null ? Instance.sfxVolume : PlayerPrefs.GetFloat(SfxPreference, 0.72f);

    public static void SetMusicVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicPreference, normalized);
        if (Instance != null)
            Instance.musicVolume = normalized;
        PlayerPrefs.Save();
    }

    public static void SetSfxVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxPreference, normalized);
        if (Instance != null)
            Instance.sfxVolume = normalized;
        PlayerPrefs.Save();
    }

    public static void SetVoiceVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VoicePreference, normalized);
        if (Instance != null)
            Instance.voiceVolume = normalized;
        PlayerPrefs.Save();
    }

    public static void SetAmbienceVolume(float value)
    {
        float normalized = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(AmbiencePreference, normalized);
        if (Instance != null)
            Instance.ambienceVolume = normalized;
        PlayerPrefs.Save();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (activeMusicClip != null && activeMusicClipGenerated)
            Destroy(activeMusicClip);
    }
}
