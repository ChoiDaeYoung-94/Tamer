"""Read-only YAML GUID audit. Package assets require Unity's resolved PackageCache.

Reports unresolved external GUIDs, not null fields, built-ins, or local fileIDs.
"""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
guid_pattern = re.compile(r'guid: ([0-9a-f]{32})')
definition_pattern = re.compile(r'^guid: ([0-9a-f]{32})\s*$', re.MULTILINE)
known = set()
for folder in ('Assets', 'Packages', 'Library/PackageCache'):
    for path in (root / folder).rglob('*.meta'):
        known.update(definition_pattern.findall(path.read_text(encoding='utf-8-sig', errors='replace')))
missing = {}
checked = 0
for folder in ('Assets/Scenes', 'Assets/Prefabs', 'Assets/Resources', 'Assets/Art', 'Assets/Materials', 'Assets/Animations', 'Assets/Settings'):
    for path in (root / folder).rglob('*'):
        if path.suffix.lower() not in {'.unity', '.prefab', '.asset', '.mat', '.controller', '.anim'}:
            continue
        data = path.read_bytes()
        if not data.startswith(b'%YAML'):
            continue
        checked += 1
        for guid in set(guid_pattern.findall(data.decode('utf-8-sig'))):
            if guid not in known and not guid.startswith('0000000000000000'):
                missing.setdefault(guid, []).append(path.relative_to(root).as_posix())
result = dict(checkedFiles=checked, knownGuids=len(known), unresolved=missing)
out = root / 'Logs/revival/guid-audit.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(dict(checkedFiles=checked, unresolvedGuids=len(missing), report=str(out))))
raise SystemExit(1 if missing else 0)
