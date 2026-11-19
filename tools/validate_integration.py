#!/usr/bin/env python3
"""Static syntax and asset checks for the audio/adaptive learning integration.

This is not a substitute for Unity Editor compilation or WebGL play testing.
"""
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
MUSIC_DIR = ROOT / "Assets/Resources/Audio/Music"
EXPECTED = ["MainMenu", "Street", "School", "Playground"]
FILES = [
    "Assets/Scripts/Audio/GameAudioManager.cs",
    "Assets/Scripts/Audio/ProceduralMusicLibrary.cs",
    "Assets/Scripts/Learning/LearningObjectiveId.cs",
    "Assets/Scripts/Learning/LearningEvent.cs",
    "Assets/Scripts/Learning/LearningProfile.cs",
    "Assets/Scripts/Learning/LearningTracker.cs",
    "Assets/Scripts/Learning/AdaptiveLearningManager.cs",
    "Assets/Scripts/Learning/AdaptiveScenarioConfig.cs",
    "Assets/Scripts/Quiz/CrossingQuiz.cs",
    "Assets/Scripts/Quiz/QuizUi.cs",
    "Assets/Scripts/Quiz/WelcomeMenu.cs",
    "Assets/Scripts/School/SchoolHelpStage.cs",
    "Assets/Scripts/Playground/PlaygroundStage.cs",
    "Assets/Scripts/Localization/GameLanguage.cs",
]

try:
    from tree_sitter import Parser, Language
    import tree_sitter_c_sharp
except ImportError as exc:
    raise SystemExit("Install syntax checker: pip install tree-sitter tree-sitter-c-sharp") from exc

parser = Parser(Language(tree_sitter_c_sharp.language()))
failed = False
for file_name in FILES:
    path = ROOT / file_name
    if not path.is_file():
        print("MISSING C# FILE:", file_name)
        failed = True
        continue
    tree = parser.parse(path.read_bytes())
    if tree.root_node.has_error:
        print("C# SYNTAX ERROR:", file_name)
        failed = True
    else:
        print("C# syntax OK:", file_name)

for name in EXPECTED:
    path = MUSIC_DIR / ("BGM_" + name + ".ogg")
    if not path.is_file():
        print("MISSING MUSIC:", path.relative_to(ROOT))
        failed = True
        continue
    proc = subprocess.run([
        "ffprobe", "-v", "error", "-select_streams", "a:0",
        "-show_entries", "stream=codec_name,channels:format=duration",
        "-of", "json", str(path)
    ], text=True, capture_output=True, check=True)
    metadata = json.loads(proc.stdout)
    duration = float(metadata.get("format", {}).get("duration", 0))
    channels = int(metadata.get("streams", [{}])[0].get("channels", 0))
    if duration < 30 or channels < 1:
        print("INVALID MUSIC:", name, duration, channels)
        failed = True
    else:
        print(f"Music OK: {name}, {duration:.1f}s, {channels} channel(s)")

if failed:
    sys.exit(1)

print("Static integration verification passed. Unity compilation and WebGL play test still required.")
