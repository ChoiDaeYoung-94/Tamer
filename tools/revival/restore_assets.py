"""Restore licensed local assets without overwriting changes; never copy caches or secrets.

Inventory creation is a maintainer operation. Normal clones use the checked-in inventory.
"""
import argparse
import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'docs/revival/assets-manifest.json'
ROOTS = ('Assets/ThirdParty', 'Assets/ThirdPartyAssets', 'Assets/Tests')
SETTINGS = 'Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset'
PRIVATE_CONFIG_NAMES = {'PlayFabSharedSettings.asset', 'PlayFabEditorPrefsSO.asset'}
SDK_META_MIGRATIONS = ROOT / 'docs/revival/sdk-meta-migrations.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def contained(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError('Path escapes project: ' + relative)
    return path


def reviewed_sdk_meta(entry, dest):
    """Accept only an explicit migration with unchanged GUID and clean Git provenance."""
    if entry['disposition'] != 'git-sdk' or dest.suffix != '.meta':
        return False
    if not SDK_META_MIGRATIONS.is_file():
        return False
    ledger = json.loads(SDK_META_MIGRATIONS.read_text(encoding='utf-8'))
    if ledger.get('schema') != 1:
        raise ValueError('Unsupported SDK meta migration ledger')
    records = ledger['entries']
    if len({row['path'] for row in records}) != len(records):
        raise ValueError('Duplicate SDK meta migration path')
    row = next((row for row in records if row['path'] == entry['path']), None)
    if row is None or row['old'] != {key: entry.get(key) for key in ('sha256', 'bytes', 'guid')}:
        return False
    data = dest.read_bytes()
    guid = re.search(r'^guid: ([0-9a-f]{32})$', data.decode('utf-8-sig'), re.M)
    if not guid or guid[1] != entry.get('guid') or row['new'] != {
            'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data), 'guid': guid[1]}:
        return False
    commit = row['migrationCommit']
    if not re.fullmatch(r'[0-9a-f]{40}', commit):
        return False
    try:
        # Index/HEAD equality rejects staged edits; raw byte equality rejects dirty,
        # untracked and unresolved paths. Never bless arbitrary current Git content.
        def blob(revision):
            return subprocess.check_output(['git', '-C', str(ROOT), 'show', revision],
                                           stderr=subprocess.PIPE)
        if blob('HEAD:' + entry['path']) != data or blob(':' + entry['path']) != data:
            return False
        if blob(commit + ':' + entry['path']) != data:
            return False
        return subprocess.run(['git', '-C', str(ROOT), 'merge-base', '--is-ancestor', commit, 'HEAD'],
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE).returncode == 0
    except (OSError, subprocess.CalledProcessError):
        return False


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


def load_private_meta_approval(path, source, entries):
    """Explicit local approval for exactly one preserved private importer migration."""
    path = Path(path).resolve()
    private_root = (ROOT / 'Logs/revival').resolve()
    if not path.is_relative_to(private_root):
        raise ValueError('Private approval must stay in ignored local Logs/revival')
    relative = path.relative_to(ROOT.resolve()).as_posix()
    tracked = subprocess.check_output(['git', '-C', str(ROOT), 'ls-files', '--', relative])
    ignored = subprocess.run(['git', '-C', str(ROOT), 'check-ignore', '-q', '--', relative],
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if tracked or ignored.returncode != 0:
        raise ValueError('Private approval must be untracked and ignored')
    approval = json.loads(path.read_text(encoding='utf-8-sig'))
    if approval.get('schema') != 1 or approval.get('approval') != 'explicit-user-one-private-meta-migration':
        raise ValueError('Invalid explicit private approval')
    if Path(approval['checkout']).resolve() != ROOT.resolve() or Path(approval['source']).resolve() != source.resolve():
        raise ValueError('Private approval checkout/source mismatch')
    unity = json.loads((ROOT / 'tools/revival/toolchain.json').read_text(encoding='utf-8'))['unityEditor']
    editor = re.search(r'^m_EditorVersion: (.+)$',
                       (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8'), re.M)
    if not editor or approval.get('unity') != unity or editor[1].strip() != unity:
        raise ValueError('Private approval Unity version mismatch')
    rows = approval.get('entries', [])
    if len(rows) != 1:
        raise ValueError('Private approval requires exactly one meta')
    row = rows[0]
    entry = next((entry for entry in entries if entry['path'] == row['path']), None)
    if not entry or entry['disposition'] != 'private-restore' or not row['path'].endswith('.meta'):
        raise ValueError('Private approval must name a manifest private meta')
    def meta_info(file):
        data = file.read_bytes()
        guid = re.search(r'^guid: ([0-9a-f]{32})$', data.decode('utf-8-sig'), re.M)
        return dict(sha256=hashlib.sha256(data).hexdigest(), bytes=len(data), guid=guid[1] if guid else None)
    original = contained(source, row['path'])
    current = contained(ROOT, row['path'])
    old = {key: entry.get(key) for key in ('sha256', 'bytes', 'guid')}
    if row.get('old') != old or meta_info(original) != old or meta_info(current) != row.get('new'):
        raise ValueError('Private approval old/new meta bytes mismatch')
    if row['new']['guid'] != old['guid'] or not old['guid'] or row['new']['sha256'] == old['sha256']:
        raise ValueError('Private approval GUID or migration mismatch')
    for label, expected in [('preservedOriginal', old), ('preservedCurrent', row['new'])]:
        backup = contained(ROOT, row[label])
        if not backup.is_relative_to(private_root) or meta_info(backup) != expected:
            raise ValueError('Private approval preserved evidence mismatch')
    asset_path = row['path'][:-5]
    asset = next((entry for entry in entries if entry['path'] == asset_path), None)
    if not asset or asset['disposition'] != 'private-restore':
        raise ValueError('Private approval asset provenance missing')
    expected_asset = {key: asset[key] for key in ('sha256', 'bytes')}
    if row.get('asset') != expected_asset:
        raise ValueError('Private approval asset manifest mismatch')
    for root in (ROOT, source):
        file = contained(root, asset_path)
        if digest(file) != asset['sha256'] or file.stat().st_size != asset['bytes']:
            raise ValueError('Private approval asset contents mismatch')
    return row


def restore(source, verify=False, private_meta_approval=None):
    entries = json.loads(MANIFEST.read_text(encoding='utf-8'))['entries']
    approved_private = (load_private_meta_approval(private_meta_approval, source, entries)
                        if private_meta_approval is not None else None)
    copied = checked = migrations = private_migrations = 0
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
                if (approved_private and relative == approved_private['path'] and
                        digest(dest) == approved_private['new']['sha256'] and
                        dest.stat().st_size == approved_private['new']['bytes']):
                    private_migrations += 1
                elif reviewed_sdk_meta(entry, dest):
                    migrations += 1
                else:
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
    print(json.dumps(dict(verified=checked, copied=copied, serviceSettings='excluded',
                          sdkMetaMigrations=migrations, privateMetaMigrations=private_migrations)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path)
    parser.add_argument('--inventory', action='store_true')
    parser.add_argument('--verify', action='store_true')
    parser.add_argument('--private-meta-approval', type=Path,
                        help='Explicit ignored local ledger for one approved private meta migration')
    args = parser.parse_args()
    if args.private_meta_approval and (not args.source or args.inventory):
        parser.error('Private approval requires --source and cannot regenerate inventory')
    if (args.inventory or not args.verify) and not args.source:
        parser.error('--source is required to restore or inventory')
    if args.source and args.source.resolve() == ROOT:
        parser.error('Source must be a separate original/archive directory')
    if args.inventory:
        inventory(args.source.resolve())
    restore(args.source.resolve() if args.source else ROOT, args.verify, args.private_meta_approval)
