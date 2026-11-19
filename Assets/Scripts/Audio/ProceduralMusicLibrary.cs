using UnityEngine;

/// <summary>
/// Creates compact, original background loops at runtime. This keeps the WebGL repository
/// lightweight while preserving the four musical identities from the audio production plan.
/// </summary>
internal static class ProceduralMusicLibrary
{
    const int SampleRate = 22050;
    const int Bars = 24;
    const float TwoPi = Mathf.PI * 2f;

    static readonly int[][] MenuChords =
    {
        new[] { 48, 52, 55, 60 }, new[] { 43, 47, 50, 55 },
        new[] { 45, 48, 52, 57 }, new[] { 41, 45, 48, 53 }
    };

    static readonly int[][] StreetChords =
    {
        new[] { 41, 45, 48, 53 }, new[] { 48, 52, 55, 60 },
        new[] { 43, 47, 50, 55 }, new[] { 45, 48, 52, 57 }
    };

    static readonly int[][] SchoolChords =
    {
        new[] { 48, 52, 55, 60 }, new[] { 45, 48, 52, 57 },
        new[] { 41, 45, 48, 53 }, new[] { 43, 47, 50, 55 }
    };

    static readonly int[][] PlaygroundChords =
    {
        new[] { 43, 47, 50, 55 }, new[] { 50, 54, 57, 62 },
        new[] { 40, 43, 47, 52 }, new[] { 48, 52, 55, 60 }
    };

    static readonly int[] MenuScale = { 60, 62, 64, 65, 67, 69, 71, 72, 74, 76 };
    static readonly int[] StreetScale = { 60, 62, 64, 65, 67, 69, 70, 72, 74, 76 };
    static readonly int[] PlaygroundScale = { 59, 62, 64, 67, 69, 71, 74, 76 };

    static readonly int[] MenuMelody = { 0, 2, 4, 2, 1, 2, 4, 5 };
    static readonly int[] StreetMelody = { 2, 4, 5, 4, 2, 1, 0, 1 };
    static readonly int[] SchoolMelody = { 4, 2, 1, 0, 2, 4, 5, 4 };
    static readonly int[] PlaygroundMelody = { 0, 2, 4, 7, 5, 4, 2, 4 };

    internal static AudioClip CreateClip(GameMusicTrack track)
    {
        int bpm = track switch
        {
            GameMusicTrack.MainMenu => 90,
            GameMusicTrack.Street => 92,
            GameMusicTrack.School => 80,
            _ => 110,
        };

        float beat = 60f / bpm;
        float barDuration = beat * 4f;
        int frameCount = Mathf.CeilToInt(Bars * barDuration * SampleRate);
        var samples = new float[frameCount];

        int[][] chords = track switch
        {
            GameMusicTrack.MainMenu => MenuChords,
            GameMusicTrack.Street => StreetChords,
            GameMusicTrack.School => SchoolChords,
            _ => PlaygroundChords,
        };

        int[] scale = track switch
        {
            GameMusicTrack.Street => StreetScale,
            GameMusicTrack.Playground => PlaygroundScale,
            _ => MenuScale,
        };

        int[] melody = track switch
        {
            GameMusicTrack.MainMenu => MenuMelody,
            GameMusicTrack.Street => StreetMelody,
            GameMusicTrack.School => SchoolMelody,
            _ => PlaygroundMelody,
        };

        for (int bar = 0; bar < Bars; bar++)
        {
            float start = bar * barDuration;
            int[] chord = chords[bar % chords.Length];

            AddTone(samples, start, beat * 1.7f, chord[0] - 12, 0.055f, 4);
            AddTone(samples, start + beat * 2f, beat * 1.45f, chord[0] - 12, 0.038f, 4);

            for (int i = 1; i < 4; i++)
                AddTone(samples, start, barDuration * 0.86f, chord[i] + 12, 0.012f, 2);

            if (track == GameMusicTrack.MainMenu)
            {
                int[] pattern = { 0, 2, 1, 2, 0, 2, 1, 2 };
                for (int step = 0; step < pattern.Length; step++)
                    AddTone(samples, start + step * beat * 0.5f, beat * 0.34f,
                        chord[pattern[step]] + 12, 0.032f, 1);
            }
            else if (track == GameMusicTrack.Street)
            {
                int[] pattern = { 0, 1, 2, 1 };
                for (int step = 0; step < pattern.Length; step++)
                    AddTone(samples, start + step * beat, beat * 0.42f,
                        chord[pattern[step]] + 12, 0.025f, 1);
            }
            else if (track == GameMusicTrack.School)
            {
                for (int i = 0; i < 3; i++)
                {
                    AddTone(samples, start, beat * 1.7f, chord[i] + 12, 0.018f, 0);
                    AddTone(samples, start + beat * 2f, beat * 1.55f, chord[i] + 12, 0.015f, 0);
                }
                AddTone(samples, start + beat * 1.5f, beat * 0.45f, chord[2] + 19, 0.012f, 1);
            }
            else
            {
                int[] pattern = { 0, 2, 1, 2, 0, 2, 1, 2 };
                for (int step = 0; step < pattern.Length; step++)
                    AddTone(samples, start + step * beat * 0.5f, beat * 0.28f,
                        chord[pattern[step]] + 12, 0.030f, 5);

                AddPercussion(samples, start, beat, false, 0.045f);
                AddPercussion(samples, start + beat * 2f, beat, false, 0.034f);
                AddPercussion(samples, start + beat, beat, true, 0.009f);
                AddPercussion(samples, start + beat * 3f, beat, true, 0.010f);
            }

            bool leaveSpace = (track == GameMusicTrack.Street || track == GameMusicTrack.School) &&
                bar % 4 == 3;

            if (!leaveSpace)
            {
                float interval = beat * 0.5f;
                int instrument = track == GameMusicTrack.MainMenu ? 1 :
                    track == GameMusicTrack.Playground ? 3 : 0;
                float volume = track == GameMusicTrack.MainMenu ? 0.040f :
                    track == GameMusicTrack.Playground ? 0.036f :
                    track == GameMusicTrack.Street ? 0.026f : 0.022f;

                for (int step = 0; step < melody.Length; step++)
                {
                    if (step == 3 || step == 7)
                        continue;

                    int scaleIndex = melody[step];
                    if (bar % 4 == 2 && step >= 6)
                        scaleIndex = Mathf.Max(0, scaleIndex - 1);

                    int note = scale[scaleIndex] + (track == GameMusicTrack.MainMenu ||
                        track == GameMusicTrack.Playground ? 12 : 0);
                    AddTone(samples, start + step * interval, interval * 0.78f,
                        note, volume, instrument);
                }
            }
        }

        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));

        if (peak > 0.0001f)
        {
            float gain = 0.72f / peak;
            int fadeSamples = Mathf.Min(SampleRate / 12, samples.Length / 8);
            for (int i = 0; i < samples.Length; i++)
            {
                float edge = 1f;
                if (i < fadeSamples)
                    edge = i / (float)fadeSamples;
                else if (i >= samples.Length - fadeSamples)
                    edge = (samples.Length - 1 - i) / (float)fadeSamples;

                samples[i] = Mathf.Clamp(samples[i] * gain * edge, -0.92f, 0.92f);
            }
        }

        AudioClip clip = AudioClip.Create("BGM_" + track, frameCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static void AddTone(float[] buffer, float startSeconds, float duration, int midi, float amplitude, int instrument)
    {
        int start = Mathf.Max(0, Mathf.FloorToInt(startSeconds * SampleRate));
        if (start >= buffer.Length || duration <= 0f)
            return;

        int count = Mathf.Min(buffer.Length - start, Mathf.CeilToInt(duration * SampleRate));
        float frequency = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        float decaySeconds = instrument switch
        {
            0 => 0.62f,
            1 => 0.34f,
            2 => 3.2f,
            3 => 0.27f,
            4 => 0.78f,
            _ => 0.22f,
        };
        float attackSeconds = instrument == 2 ? 0.16f : 0.008f;
        float releaseStart = duration * 0.82f;
        float releaseDuration = Mathf.Max(0.04f, duration - releaseStart);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float attack = Mathf.Clamp01(t / attackSeconds);
            float release = t > releaseStart ? Mathf.Clamp01((duration - t) / releaseDuration) : 1f;
            float envelope = attack * release * Mathf.Exp(-t / decaySeconds);
            float phase = TwoPi * frequency * t;

            float fundamental = Mathf.Sin(phase);
            float second = Mathf.Sin(phase * 2.01f + 0.13f);
            float third = Mathf.Sin(phase * 3.98f + 0.27f);
            float fourth = Mathf.Sin(phase * 6.02f + 0.39f);

            float tone = instrument switch
            {
                0 => fundamental + 0.38f * second + 0.15f * third,
                1 => fundamental + 0.34f * second + 0.12f * third,
                2 => 0.78f * fundamental + 0.20f * second + 0.07f * third,
                3 => fundamental + 0.42f * third + 0.10f * fourth,
                4 => fundamental + 0.16f * second,
                _ => fundamental + 0.30f * second + 0.09f * third,
            };

            buffer[start + i] += tone * envelope * amplitude;
        }
    }

    static void AddPercussion(float[] buffer, float startSeconds, float beat, bool highHat, float amplitude)
    {
        int start = Mathf.Max(0, Mathf.FloorToInt(startSeconds * SampleRate));
        float duration = highHat ? 0.065f : 0.24f;
        int count = Mathf.Min(buffer.Length - start, Mathf.CeilToInt(duration * SampleRate));

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = highHat ? Mathf.Exp(-t * 62f) : Mathf.Exp(-t * 16f);
            float phase = highHat
                ? TwoPi * 6200f * t
                : TwoPi * (82f * t - 42f * t * t);
            buffer[start + i] += Mathf.Sin(phase) * envelope * amplitude;
        }
    }
}
