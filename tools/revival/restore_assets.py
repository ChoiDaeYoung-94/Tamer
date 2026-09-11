"""Restore licensed local assets without overwriting changes; never copy caches or secrets.

Inventory creation is a maintainer operation. Normal clones use the checked-in inventory.
"""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'docs/revival/assets-manifest.json'
ROOTS = ('Assets/ThirdParty', 'Assets/ThirdPartyAssets', 'Assets/Tests')
SETTINGS = 'Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset'
PRIVATE_CONFIG_NAMES = {'PlayFabSharedSettings.asset', 'PlayFabEditorPrefsSO.asset'}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def contained(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError('Path escapes project: ' + relative)
    return path


def inventory(source):
    entries = []
    for folder in ROOTS:
        paths = sorted((source / folder).rglob('*')) + [source / (folder + '.meta')]
        for path in paths:
            if not path.is_file():
                continue
            rel = path.relative_to(source).as_posix()
            sdk = rel.startswith(('Assets/ThirdParty/PlayFabSDK/', 'Assets/ThirdParty/PlayFabEditorExtensions/'))
            entry = dict(path=rel, bytes=path.stat().st_size,
                         sha256=digest(path), disposition='git-sdk' if sdk else 'private-restore',
                         license='Apache-2.0' if sdk else 'unverified-do-not-redistribute',
                         version='2.138.220621' if 'PlayFabSDK/' in rel else 'local-original-unverified')
            if '/UI/Fonts/' in rel:
                entry.update(disposition='private-restore', license='font-license-unverified-do-not-redistribute')
            if path.suffix == '.meta':
                match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(encoding='utf-8-sig'), re.M)
                if match:
                    entry['guid'] = match[1]
            if path.name in PRIVATE_CONFIG_NAMES:
                # No original configuration values or hashes in the public manifest.
                entry = dict(path=rel, disposition='local-service-settings-excluded', license='private')
            entries.append(entry)
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(dict(schema=1, source='owner-provided Tamer original',
        sdkSources={'PlayFabSDK':'https://github.com/PlayFab/UnitySDK',
                    'PlayFabEditorExtensions':'https://github.com/PlayFab/UnityEditorExtensions'},
        entries=entries), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def restore(source, verify=False):
    entries = json.loads(MANIFEST.read_text(encoding='utf-8'))['entries']
    copied = checked = 0
    # Validate all sources and existing destinations before the first copy.
    pending = []
    for entry in entries:
        if entry['disposition'] == 'local-service-settings-excluded':
            if not verify:
                dest = contained(ROOT, entry['path'])
                template = ROOT / 'tools/revival/templates' / (dest.name + '.txt')
                if not dest.exists():
                    pending.append((template, dest, digest(template)))
            continue
        relative = entry['path']
        dest = contained(ROOT, relative)
        if dest.is_file():
            if digest(dest) != entry['sha256']:
                raise ValueError('Existing file differs; preserving it: ' + relative)
            checked += 1
            continue
        if verify:
            raise ValueError('Missing restored file: ' + relative)
        if entry['disposition'] == 'git-sdk':
            raise ValueError('Missing tracked SDK file; restore from Git, not the legacy asset archive: ' + relative)
        src = contained(source, relative)
        if not src.is_file() or digest(src) != entry['sha256']:
            raise ValueError('Source missing or hash mismatch: ' + relative)
        pending.append((src, dest, entry['sha256']))
    for src, dest, expected in pending:
        dest.parent.mkdir(parents=True, exist_ok=True)
        # Exclusive create protects a file appearing after preflight.
        with src.open('rb') as inp, dest.open('xb') as out:
            shutil.copyfileobj(inp, out)
        if digest(dest) != expected:
            raise ValueError('Copy hash mismatch: ' + str(dest))
        copied += 1
    print(json.dumps(dict(verified=checked, copied=copied, serviceSettings='excluded')))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path)
    parser.add_argument('--inventory', action='store_true')
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    if (args.inventory or not args.verify) and not args.source:
        parser.error('--source is required to restore or inventory')
    if args.source and args.source.resolve() == ROOT:
        parser.error('Source must be a separate original/archive directory')
    if args.inventory:
        inventory(args.source.resolve())
    restore(args.source.resolve() if args.source else ROOT, args.verify)
