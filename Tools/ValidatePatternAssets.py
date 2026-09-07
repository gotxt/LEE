"""Static checks for the consolidated encounter YAML.

Unity EditMode tests remain authoritative for Unity serialization. These checks
catch broken GUIDs, managed-reference IDs, event types, timings and local calls
without requiring an activated Unity editor.
"""

from pathlib import Path
import math
import re


ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "Assets"
ENCOUNTER = ASSETS / "Resources" / "Patterns" / "CrimsonGolem.asset"


def numeric(block, name):
    match = re.search(r"^\s*" + re.escape(name) + r": ([0-9.eE+-]+)$", block, re.M)
    assert match, f"Missing numeric field {name}"
    value = float(match.group(1))
    assert math.isfinite(value), f"Non-finite {name}"
    return value


guids = {}
for meta in ASSETS.rglob("*.meta"):
    match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8-sig"), re.M)
    if not match:
        continue
    guid = match.group(1)
    assert guid not in guids, f"Duplicate GUID: {meta} / {guids.get(guid)}"
    guids[guid] = Path(str(meta)[:-5])

text = ENCOUNTER.read_text(encoding="utf-8-sig")
assert "  arena:\n" in text and "  libraryPatterns:\n" in text and "  phases:\n" in text
for guid in re.findall(r"guid: ([0-9a-f]{32})", text):
    assert guid in guids and guids[guid].exists(), f"Missing reference: {guid}"

data_text, ref_text = text.split("  references:\n", 1)
ref_blocks = re.split(r"(?=^    - rid: )", ref_text, flags=re.M)
ref_blocks = [block for block in ref_blocks if block.startswith("    - rid: ")]
refs = {int(re.match(r"    - rid: (\d+)", block).group(1)): block for block in ref_blocks}
assert len(refs) == len(ref_blocks), "Duplicate managed-reference ID"

source = "\n".join(path.read_text(encoding="utf-8-sig")
                   for path in (ASSETS / "Scripts" / "Patterns").glob("*.cs"))
for block in refs.values():
    event_type = re.search(r"type: \{class: (\w+),", block).group(1)
    assert re.search(r"\bclass\s+" + event_type + r"\b", source), f"Unknown event: {event_type}"

lines = data_text.splitlines()
starts = []
for index, line in enumerate(lines):
    match = re.match(r"^(  |    )- id: (\S+)$", line)
    if match:
        starts.append((index, len(match.group(1)), match.group(2)))

patterns = {}
for start_index, indent, pattern_id in starts:
    end_index = len(lines)
    for index in range(start_index + 1, len(lines)):
        line = lines[index]
        if not line.strip():
            continue
        leading = len(line) - len(line.lstrip(" "))
        if leading <= indent:
            end_index = index
            break
    block = "\n".join(lines[start_index:end_index])
    name = re.search(r"^\s+name: (.+)$", block, re.M).group(1)
    duration = numeric(block, "minimumDuration")
    assert duration >= 0
    clip_marker = " " * (indent + 2) + "- enabled: "
    clip_blocks = re.split(r"(?=^" + re.escape(clip_marker) + r")", block, flags=re.M)
    clip_blocks = [clip for clip in clip_blocks if clip.startswith(clip_marker)]
    patterns[pattern_id] = {"name": name, "duration": duration, "clips": clip_blocks}

assert len(patterns) == 20, f"Expected 20 inline patterns, found {len(patterns)}"
assert len(patterns) == len(starts), "Duplicate encounter pattern ID"

graph = {pattern_id: [] for pattern_id in patterns}
for pattern_id, pattern in patterns.items():
    for clip in pattern["clips"]:
        start = numeric(clip, "start")
        duration = numeric(clip, "duration")
        assert start >= 0 and duration >= 0
        rid = int(numeric(clip, "rid"))
        assert rid in refs, f"Missing managed reference {rid} in {pattern_id}"
        event = refs[rid]
        if "class: DamageEvent," in event:
            assert duration >= numeric(event, "escapeGrace"), f"Short damage clip in {pattern_id}"
        if "class: CallEncounterPatternEvent," in event:
            child = re.search(r"^\s*patternId: (\S+)$", event, re.M).group(1)
            assert child in patterns, f"Missing local pattern {child}"
            assert duration + 1e-6 >= patterns[child]["duration"], f"Short local call in {pattern_id}"
            graph[pattern_id].append(child)


def visit(pattern_id, path):
    assert pattern_id not in path, "Recursive encounter pattern: " + " -> ".join(path + [pattern_id])
    for child in graph[pattern_id]:
        visit(child, path + [pattern_id])


for pattern_id in patterns:
    visit(pattern_id, [])

print(
    f"Validated one encounter with {len(patterns)} inline patterns and "
    f"{len(refs)} managed events; GUIDs, timings, types and local calls resolve."
)
