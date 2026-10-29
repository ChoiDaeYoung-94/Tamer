"""Read-only facelift guard: preserve existing objects, gameplay fields and UI events."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_PATHS = [
    'Assets/Prefabs/UI/LoginCanvas.prefab',
    'Assets/Resources/Prefabs/MainAndGameUICanvas/Canvas.prefab',
    'Assets/Prefabs/Manager/Manager.prefab',
    'Assets/Prefabs/Main/ShopMan.prefab',
    'Assets/Resources/Prefabs/JoyStick/Canvas.prefab',
    'Assets/Prefabs/UI/NextSceneCanvas.prefab',
    'Assets/Scenes/SetCharacter.unity',
]

def git_bytes(base, path):
    return subprocess.check_output(['git', 'show', f'{base}:{path}'], cwd=ROOT)

def documents(text):
    return {match[1]: match[2] for match in re.findall(
        r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',
        text.replace('\r\n', '\n'), re.M | re.S)}

def events(document):
    return re.findall(r'^  (?:m_On\w+|on\w+):\n(?:    .*\n|\n)*', document, re.M)

def ui_objects(docs):
    def owner(body):
        match = re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}', body, re.M)
        return match[1] if match else None
    canvas_owners = {owner(body) for body in docs.values() if body.startswith('Canvas:\n')}
    transforms = {key: (owner(body), re.search(r'^  m_Father: \{fileID: (-?\d+)\}', body, re.M))
                  for key, body in docs.items() if body.startswith(('RectTransform:\n', 'Transform:\n'))}
    selected = {key for key, (go, _) in transforms.items() if go in canvas_owners}
    while True:
        expanded = selected | {key for key, (_, father) in transforms.items() if father and father[1] in selected}
        if expanded == selected:
            break
        selected = expanded
    game_objects = {transforms[key][0] for key in selected}
    return game_objects | {key for key, body in docs.items() if owner(body) in game_objects}

def verify(base, paths):
    scripts = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', base,
                                       'Assets/Scripts'], cwd=ROOT, text=True).splitlines()
    gameplay_guids = set()
    for path in scripts:
        if path.endswith('.cs.meta'):
            match = re.search(rb'^guid: ([0-9a-f]{32})', git_bytes(base, path), re.M)
            if match:
                gameplay_guids.add(match[1].decode())
    reports = []
    for path in paths:
        before = documents(git_bytes(base, path).decode('utf-8-sig'))
        after = documents((ROOT / path).read_text(encoding='utf-8-sig'))
        if not before or not before.keys() <= after.keys():
            raise ValueError(f'Existing serialized object was removed: {path}')
        event_count = gameplay_count = 0
        visual_objects = ui_objects(before)
        if not visual_objects:
            raise ValueError(f'No existing Canvas in facelift source: {path}')
        new_non_ui = (after.keys() - before.keys()) - ui_objects(after)
        if new_non_ui:
            raise ValueError(f'Unexpected new non-UI objects: {path}, count {len(new_non_ui)}')
        for file_id, original in before.items():
            current = after[file_id]
            if file_id not in visual_objects and original != current:
                raise ValueError(f'Non-UI serialized object changed: {path}, object {file_id}')
            old_events = events(original)
            if old_events != events(current):
                raise ValueError(f'UI event changed: {path}, object {file_id}')
            event_count += len(old_events)
            script = re.search(r'^  m_Script: .*guid: ([0-9a-f]{32})', original, re.M)
            if script and script[1] in gameplay_guids:
                def fields(value):
                    return value.split('  m_EditorClassIdentifier:', 1)[1].split('\n', 1)[1]
                if fields(original) != fields(current):
                    raise ValueError(f'Gameplay/serialized reference changed: {path}, object {file_id}')
                gameplay_count += 1
            old_active = re.search(r'^  m_IsActive: (\d)$', original, re.M)
            if old_active and old_active[0] not in current:
                raise ValueError(f'Existing active state changed: {path}, object {file_id}')
        meta = (ROOT / (path + '.meta')).read_text(encoding='utf-8-sig')
        if meta != git_bytes(base, path + '.meta').decode('utf-8-sig').replace('\r\n', '\n'):
            raise ValueError(f'Existing asset metadata changed: {path}')
        reports.append(dict(path=path, existingObjects=len(before), eventFields=event_count,
                            gameplayComponents=gameplay_count, metadataPreserved=True,
                            sha256=hashlib.sha256((ROOT / path).read_bytes()).hexdigest()))
    return dict(base=base, passed=True, files=reports)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', required=True)
    parser.add_argument('paths', nargs='*')
    args = parser.parse_args()
    print(json.dumps(verify(args.base, args.paths or DEFAULT_PATHS), indent=2))
