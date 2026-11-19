from argparse import ArgumentParser
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
from pathlib import Path
from struct import pack, unpack
import json

from PIL import Image

parser = ArgumentParser(description="Resize embedded glTF textures for GitHub Pages.")
parser.add_argument("--project-root", type=Path, default=Path.cwd())
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
PROJECT = args.project_root.resolve()
OPTIMIZED = args.output.resolve()

def pad(data, byte):
    return data + byte * (-len(data) % 4)

def optimize(path):
    raw = path.read_bytes()
    magic, version, length = unpack("<4sII", raw[:12])
    if magic != b"glTF" or version != 2 or length != len(raw):
        raise ValueError(f"Unexpected glTF header: {path}")
    json_size, json_kind = unpack("<I4s", raw[12:20])
    json_end = 20 + json_size
    doc = json.loads(raw[20:json_end])
    bin_size, bin_kind = unpack("<I4s", raw[json_end:json_end+8])
    data = raw[json_end+8:]
    if json_kind != b"JSON" or bin_kind != b"BIN\0" or bin_size != len(data):
        raise ValueError(f"Unexpected glTF chunks: {path}")
    if len(doc.get("buffers", [])) != 1 or "uri" in doc["buffers"][0]:
        raise ValueError(f"Unexpected buffer layout: {path}")
    if "EXT_meshopt_compression" in doc.get("extensionsUsed", []):
        raise ValueError(f"Raw offsets require special handling: {path}")

    cap = 1024 if path.parent.name == "Avatars" else 512
    views = doc["bufferViews"]
    replacements = {}
    for image in doc.get("images", []):
        if "bufferView" not in image:
            continue
        index = image["bufferView"]
        view = views[index]
        start = view.get("byteOffset", 0)
        with Image.open(BytesIO(data[start:start+view["byteLength"]])) as source:
            if max(source.size) <= cap:
                continue
            source.thumbnail((cap, cap), Image.Resampling.LANCZOS)
            target = BytesIO()
            source.save(target, format="PNG")
        image["mimeType"] = "image/png"
        replacements[index] = target.getvalue()
    if not replacements:
        return 0

    output = bytearray()
    cursor = 0
    for index, view in sorted(enumerate(views), key=lambda item: item[1].get("byteOffset", 0)):
        start = view.get("byteOffset", 0)
        end = start + view["byteLength"]
        if view["buffer"] != 0 or start < cursor or end > len(data):
            raise ValueError(f"Overlapping or invalid buffer view: {path}")
        output += data[cursor:start]
        view["byteOffset"] = len(output)
        if index in replacements:
            value = replacements[index]
            output += value + b"\0" * ((end - start - len(value)) % 4)
            view["byteLength"] = len(value)
        else:
            output += data[start:end]
        cursor = end
    output += data[cursor:]
    output = pad(output, b"\0")
    doc["buffers"][0]["byteLength"] = len(output)
    json_chunk = pad(json.dumps(doc, separators=(",", ":"), ensure_ascii=False).encode("utf-8"), b" ")
    destination = OPTIMIZED / path.relative_to(PROJECT)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(
        pack("<4sII", b"glTF", 2, 28 + len(json_chunk) + len(output))
        + pack("<I4s", len(json_chunk), b"JSON") + json_chunk
        + pack("<I4s", len(output), b"BIN\0") + output
    )
    return len(replacements)

files = sorted((PROJECT / "Assets").rglob("*.glb"))
with ThreadPoolExecutor(max_workers=4) as pool:
    results = list(pool.map(optimize, files))
print(f"Resized {sum(results)} embedded textures across {sum(bool(count) for count in results)} browser build assets.")

