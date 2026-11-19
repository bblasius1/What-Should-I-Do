# Audio production and integration

The game has four original instrumentals: Main Menu, Street, School and Playground.

- Place your WAV or OGG recordings in `Assets/Resources/Audio/Music/` with names
  `BGM_MainMenu`, `BGM_Street`, `BGM_School`, `BGM_Playground`.
- `GameAudioManager` loads these recordings when available and uses a procedural
  synthesized fallback if they have not been imported.
- The reproducible original soundtrack arrangement is in `tools/generate_music.py`.
  It generates four compressed OGG files for the Unity project.
- The repository's workflow `.github/workflows/build-music.yml` can regenerate
  the soundtracks on the feature branch. This requires GitHub Actions permissions.
- For Unity Editor builds, imported OGG clips will be transcoded according to
  Unity's platform-specific audio compression settings.
- Music starts only after an explicit player click to respect browser autoplay restrictions.
- Audio settings are available from Settings > Audio in the main menu.
- Bilingual voice files can be added at
  `Assets/Resources/Audio/Voice/EN/<Speaker>/<LineId>.wav` and
  `Assets/Resources/Audio/Voice/ZH/<Speaker>/<LineId>.wav`.
- The game keeps working with text-only dialogue when voice clips are missing.
- The Unity client contains no AI API keys.

The initial priority is stability and responsive gameplay. Please test the audio
playback, volume sliders, browser tab focus and Chinese/English voice selection
in an actual Unity 6000.6.8f1 WebGL build.
