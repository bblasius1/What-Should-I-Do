# What Should I Do?

**[Play now in your browser](https://bblasius1.github.io/What-Should-I-Do/)**

Read the project report as a [PDF](docs/what-should-i-do-project-report.pdf) or [PowerPoint](docs/what-should-i-do-project-report.pptx).

A short 3D game with three everyday challenges for children. Open the link on a computer, wait for the game to load, then select **Start**. No installation or GitHub account is required.

1. **Cross Safely** — Follow the sidewalk, answer the crossing questions, and reach the other side using the zebra crossing.
2. **Get Help at the School Gate** — Find a teacher and explain what happened so they can help.
3. **Make Room for Everyone** — Invite a classmate into the circle and make sure everyone gets a turn.

## Controls and progress

- Move with **WASD**, the **arrow keys**, or a gamepad’s left stick or D-pad.
- Use **Q** and **E** to look around. Press **F** or the gamepad’s **X** button to interact when prompted.
- Answer with the **mouse** or **1**, **2**, and **3**. On a gamepad, highlight an answer and press the bottom face button. A wrong answer gives you a hint and lets you try again.
- Open **Settings** to switch between **English** and **简体中文**. Open **Levels** to replay finished levels and see your best star ratings.

## Build from source

Open the project with **Unity 6000.6.8f1**. Open `Assets/Scenes/SampleScene.unity` and press Play. Choose **Boy** or **Girl** on the welcome screen and select **Start**.

To make a standalone player, open **File → Build Profiles**, choose a Windows or Linux desktop profile with 64-bit architecture, then build. The project already lists the playable scene. Install the matching Unity desktop build module if it is missing.

## Music, voices, and adaptive learning

This branch adds four original looping background tracks: **Main Menu**, **Street**,
**School**, and **Playground**. The files live in
`Assets/Resources/Audio/Music/BGM_*.ogg`. Music switches with the current level
and starts only after the player begins playing, respecting browser autoplay
restrictions. Open **Settings → Audio** for music, voice, sound effects, and
ambience volume controls. When a voice clip plays, the music automatically ducks
so dialogue is easier to hear.

The project supports future prerecorded English and Simplified Chinese voice
clips under `Assets/Resources/Audio/Voice/EN` and `ZH`. No voice recordings are
bundled yet; the game remains fully playable with translated on-screen dialogue.

The new `Assets/Scripts/Learning` system records objective completion and
misunderstandings, then shows authored contextual hints after repeated mistakes.
It does not require an internet connection or an LLM API. Existing three-star
progress remains intact.

**Regenerate music:** Install Python 3, NumPy and FFmpeg, then run
`python tools/generate_music.py` from the repository root. The GitHub Actions
workflow can also regenerate music automatically on this feature branch.

**Validation:** The `tools/validate_integration.py` script checks C# syntax
and confirms all four recorded soundtracks are valid audio. This is not a Unity
compile test. Open the project in Unity 6000.6.8f1, test all three levels,
confirm browser audio starts after clicking Start, and perform a production
WebGL build before merging or publishing the game.

See [audio production instructions](Assets/Audio/README.md).
