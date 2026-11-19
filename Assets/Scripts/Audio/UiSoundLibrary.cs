using UnityEngine;

/// <summary>Lightweight, original nonverbal feedback sounds for UI and learning decisions.</summary>
internal enum UiSoundCue { Click, Correct, Incorrect, Completed }

internal static class UiSoundLibrary
{
    const int SampleRate = 22050;
    const float TwoPi = Mathf.PI * 2f;

    internal static AudioClip Create(UiSoundCue cue)
    {
        float duration = cue switch
        {
            UiSoundCue.Click => 0.075f,
            UiSoundCue.Correct => 0.28f,
            UiSoundCue.Incorrect => 0.24f,
            _ => 0.72f,
        };

        int length = Mathf.CeilToInt(duration * SampleRate);
        var samples = new float[length];

        switch (cue)
        {
            case UiSoundCue.Click:
                AddTone(samples, 0f, duration, 587.33f, 0.12f);
                break;

            case UiSoundCue.Correct:
                AddTone(samples, 0f, 0.18f, 659.25f, 0.10f);
                AddTone(samples, 0.085f, 0.19f, 783.99f, 0.09f);
                break;

            case UiSoundCue.Incorrect:
                // A gentle response without an alarming or punitive buzzer.
                AddTone(samples, 0f, 0.14f, 392.00f, 0.065f);
                AddTone(samples, 0.11f, 0.13f, 349.23f, 0.050f);
                break;

            case UiSoundCue.Completed:
                AddTone(samples, 0f, 0.29f, 523.25f, 0.09f);
                AddTone(samples, 0.16f, 0.29f, 659.25f, 0.08f);
                AddTone(samples, 0.32f, 0.36f, 783.99f, 0.10f);
                AddTone(samples, 0.40f, 0.30f, 1046.50f, 0.055f);
                break;
        }

        AudioClip clip = AudioClip.Create("UI " + cue, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static void AddTone(float[] samples, float start, float duration, float frequency, float volume)
    {
        int offset = Mathf.FloorToInt(start * SampleRate);
        int count = Mathf.Min(samples.Length - offset, Mathf.FloorToInt(duration * SampleRate));
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float attack = Mathf.Clamp01(t / 0.008f);
            float release = Mathf.Clamp01((duration - t) / Mathf.Min(duration, 0.1f));
            float envelope = attack * release * Mathf.Exp(-t * 3.4f);
            float phase = TwoPi * frequency * t;
            float tone = Mathf.Sin(phase) + 0.15f * Mathf.Sin(2f * phase);
            samples[offset + i] = Mathf.Clamp(samples[offset + i] + tone * envelope * volume, -0.6f, 0.6f);
        }
    }
}
