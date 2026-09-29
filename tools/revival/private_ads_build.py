"""Private disabled AAB preparation. Default is read-only; execution needs --execute.

No signing/settings edits, installs, device operations, or distribution. A surviving
journal blocks the next run; recovery is deliberately manual and evidence-preserving.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import uuid

from validate_private_ads_preparation import audit, unique_object

HOOK = 'Assets/Scripts/Editor/RevivalPrivateAdsPreparation'
JOURNAL = '.revival-local/private-ads-build-active.json'
TEMPLATES = ('PrivateAdsContract.cs', 'PrivateProductionAdsBuild.cs')
PROTECTED = (
    'Assets/Scenes/Login.unity', 'Assets/Scenes/Login.unity.meta',
    'Assets/Prefabs/Manager/Manager.prefab', 'Assets/Prefabs/Manager/Manager.prefab.meta',
    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
    'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml',
)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def identity(path):
    info = path.stat()
    return (info.st_dev, info.st_ino)


def journal_view(state):
    # Runtime-created identities are not reconstructed from a surviving journal.
    return {key: value for key, value in state.items() if key not in ('created', 'folderIdentity')}


def safe_path(root, relative):
    """Reject traversal, symlinks and Windows junctions for every existing ancestor."""
    rel = Path(relative)
    if rel.is_absolute() or '..' in rel.parts:
        raise ValueError('Unsafe owned path')
    path = root / rel
    for part in (path, *path.parents):
        if part.exists() or part.is_symlink():
            info = part.lstat()
            if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
                raise ValueError('Linked path rejected')
    if not path.resolve().is_relative_to(root):
        raise ValueError('Owned path escaped checkout')
    return path


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.DEVNULL).decode().strip()


def require_editor_closed(root):
    if sys.platform != 'win32':
        raise ValueError('Editor ownership check requires Windows')
    # Process command lines are inspected locally; never returned or logged.
    script = r'''$wanted = [IO.Path]::GetFullPath($env:TAMER_CHECKOUT_CHECK).TrimEnd('\')
    $processes = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction Stop)
    if (@($processes | Where-Object { -not $_.CommandLine }).Count -gt 0) { exit 3 }
    $found = @($processes | Where-Object {
      $_.CommandLine -match '(?i)(?:^|\s)-projectPath\s+(?:"([^"]+)"|(\S+))' -and
      [IO.Path]::GetFullPath(($Matches[1] + $Matches[2])).TrimEnd('\') -eq $wanted
    }); if ($found.Count -gt 0) { exit 2 }'''
    result = subprocess.run(['powershell', '-NoProfile', '-NonInteractive', '-Command',
                             "$ErrorActionPreference='Stop'; " + script],
                            env=dict(os.environ, TAMER_CHECKOUT_CHECK=str(root)),
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if result.returncode:
        raise ValueError('Owner Editor state is not closed')


def protected_paths(root):
    paths = set(PROTECTED)
    for folder in ('ProjectSettings', 'Packages'):
        paths.update(p.relative_to(root).as_posix() for p in (root / folder).rglob('*') if p.is_file())
    for pattern in ('*.cs', '*.unity', '*.prefab', '*.asmdef', '*.rsp'):
        paths.update(p.relative_to(root).as_posix() for p in (root / 'Assets').rglob(pattern)
                     if not p.relative_to(root).as_posix().startswith(HOOK + '/'))
    return sorted(paths)


def preflight(root, config, expected_head, editor_version):
    safe_path(root, JOURNAL)
    if (root / JOURNAL).exists() or (root / HOOK).exists() or (root / (HOOK + '.meta')).exists():
        raise ValueError('Stale journal/hook: manual recovery required')
    if git(root, 'rev-parse', '--show-toplevel').replace('\\', '/').lower() != root.as_posix().lower():
        raise ValueError('Checkout root mismatch')
    if git(root, 'rev-parse', 'HEAD') != expected_head or git(root, 'status', '--porcelain'):
        raise ValueError('Expected clean HEAD required')
    version = (root / 'ProjectSettings/ProjectVersion.txt').read_text()
    if not re.search(r'^m_EditorVersion: ' + re.escape(editor_version) + r'\s*$', version, re.M):
        raise ValueError('Explicit Editor version must match project')
    if not config.resolve().is_relative_to(root / '.revival-local') and not config.resolve().is_relative_to(root / 'Logs'):
        raise ValueError('Configuration must be private and local to owner checkout')
    safe_path(root, config.relative_to(root))
    if not git(root, 'check-ignore', '--', str(config)):
        raise ValueError('Configuration must be ignored')
    require_editor_closed(root)
    return audit(root, config)


def meta(folder=False):
    header = 'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n'
    return (header + ('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if folder else
            'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
            '  executionOrder: 0\n  icon: {instanceID: 0}\n') +
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode()


def require_staged_tree(root, state):
    if git(root, 'rev-parse', 'HEAD') != state['head'] or git(root, 'branch', '--show-current') != state['branch']:
        raise ValueError('Checkout identity changed')
    if git(root, 'diff', '--name-only', 'HEAD'):
        raise ValueError('Tracked files changed before launch')
    untracked = set(filter(None, git(root, 'ls-files', '--others', '--exclude-standard', '-z').split('\0')))
    if untracked - set(state['owned']):
        raise ValueError('Unowned files appeared before launch')
    for path, expected in state['owned'].items():
        if digest(safe_path(root, path).read_bytes()) != expected:
            raise ValueError('Staged hooks changed before launch')
    for path, original in state['sources'].items():
        if digest(safe_path(root, path).read_bytes()) != original['sha256']:
            raise ValueError('Source changed before launch')
    if digest(Path(state['config']).read_bytes()) != state['configSha256']:
        raise ValueError('Configuration changed before launch')


def stage(root, config, preflight_result, expected_head, editor_version, owner=None):
    """Journal precedes any Assets write. Caller always invokes finalize in finally."""
    journal_path = safe_path(root, JOURNAL)
    if journal_path.exists() or safe_path(root, HOOK).exists() or safe_path(root, HOOK + '.meta').exists():
        raise ValueError('Existing owned staging state')
    run_id = uuid.uuid4().hex
    run = safe_path(root, '.revival-local/private-ads-build/' + run_id)
    run.mkdir(parents=True, exist_ok=False)
    files = {HOOK + '.meta': meta(True)}
    for name in TEMPLATES:
        files[HOOK + '/' + name] = (Path(__file__).parent / 'private_ads_build' / name).read_bytes()
        files[HOOK + '/' + name + '.meta'] = meta()
    sources = {}
    for index, path in enumerate(protected_paths(root)):
        raw = safe_path(root, path).read_bytes()
        backup = run / ('source-' + str(index) + '.snapshot')
        backup.write_bytes(raw)
        sources[path] = {'sha256': digest(raw), 'backup': backup.relative_to(root).as_posix()}
    state = {'schema': 1, 'checkout': str(root), 'runId': run_id, 'head': expected_head,
             'editorVersion': editor_version, 'branch': git(root, 'branch', '--show-current'),
             'run': run.relative_to(root).as_posix(), 'config': str(config),
             'configSha256': preflight_result['configSha256'], 'sources': sources,
             'owned': {path: digest(raw) for path, raw in files.items()},
             'created': {}, 'folderIdentity': None}
    # Exclusive creation also rejects a concurrent wrapper. Orphan snapshots are private,
    # harmless, and preserved if the process dies before journal creation.
    with journal_path.open('x', encoding='utf-8') as stream:
        if owner is not None:
            owner.update(state)
            state = owner
        json.dump(journal_view(state), stream, indent=2)
        stream.flush()
        os.fsync(stream.fileno())
    safe_path(root, HOOK).mkdir(exist_ok=False)
    state['folderIdentity'] = identity(safe_path(root, HOOK))
    for path, raw in files.items():
        with safe_path(root, path).open('xb') as stream:
            info = os.fstat(stream.fileno())
            state['created'][path] = (info.st_dev, info.st_ino)
            stream.write(raw)
    if digest(config.read_bytes()) != state['configSha256']:
        raise ValueError('Configuration changed while staging')
    return state


def finalize(root, state):
    """Remove only this invocation's byte-identical files. Never restore unknown edits."""
    journal = safe_path(root, JOURNAL)
    saved = json.loads(journal.read_bytes(), object_pairs_hook=unique_object)
    if saved != journal_view(state) or saved['checkout'] != str(root):
        raise ValueError('Journal ownership mismatch; preserve for manual recovery')
    # Validate everything before removing anything. Missing owned files are allowed:
    # staging can have failed before creating them, or cleanup can have been interrupted.
    allowed = set(state['created'])
    folder = safe_path(root, HOOK)
    if folder.exists() and identity(folder) != state['folderIdentity']:
        raise ValueError('Folder was not exclusively created by this invocation')
    if folder.exists() and any(p.relative_to(root).as_posix() not in allowed for p in folder.rglob('*')):
        raise ValueError('Unexpected staging contents; preserve for manual recovery')
    if any(safe_path(root, path).exists() for path in set(state['owned']) - allowed):
        raise ValueError('Planned path was not created by this invocation')
    for path, expected in state['owned'].items():
        file = safe_path(root, path)
        if file.exists() and (identity(file) != state['created'].get(path) or
                              not file.is_file() or digest(file.read_bytes()) != expected):
            raise ValueError('Owned file changed; preserve for manual recovery')
    for path in reversed(list(state['created'])):
        file = safe_path(root, path)
        if file.exists():
            # Recheck immediately; this still does not provide an OS transaction.
            if identity(file) != state['created'][path] or digest(file.read_bytes()) != state['owned'][path]:
                raise ValueError('Owned file changed immediately before removal')
            file.unlink()
    if folder.exists():
        folder.rmdir()  # Empty only; no recursive deletion.
    changed = [path for path, original in state['sources'].items()
               if not safe_path(root, path).is_file() or digest(safe_path(root, path).read_bytes()) != original['sha256']]
    changed += list(set(protected_paths(root)) - set(state['sources']))
    result = {'cleanupOwnedFiles': True, 'originalsUnchanged': not changed,
              'changedSourcePaths': sorted(set(changed)), 'binaryVerified': False, 'distributable': False}
    safe_path(root, state['run'] + '/cleanup.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    if (changed or git(root, 'rev-parse', 'HEAD') != state['head'] or
            git(root, 'branch', '--show-current') != state['branch'] or git(root, 'status', '--porcelain')):
        raise ValueError('Source drift: snapshots/journal retained; manual comparison required')
    safe_path(root, state['run'] + '/journal.completed.json').write_bytes(journal.read_bytes())
    journal.unlink()
    return result


def execute(root, config, head, version):
    # The authorized third synthetic contract check passed for its 59 inputs.
    # Unity compatibility, binary verification and OS cleanup races remain unresolved.
    raise ValueError('Execution blocked: Unity and lifecycle readiness incomplete')


def _execute_after_contract_review(root, config, head, version):
    """Unreachable draft; not a supported API until contract verification is cleared."""
    pre = preflight(root, config, head, version)
    state = {}
    try:
        stage(root, config, pre, head, version, state)
        run = safe_path(root, state['run'])
        env = dict(os.environ)
        for key in list(env):
            if key.startswith('TAMER_PRIVATE_ADS_'):
                del env[key]
        env.update(TAMER_PRIVATE_ADS_PREPARE='1', TAMER_PRIVATE_ADS_CONFIG=str(config),
                   TAMER_PRIVATE_ADS_SHA256=state['configSha256'],
                   TAMER_PRIVATE_ADS_OUTPUT=str(run / 'disabled-preparation.aab'),
                   TAMER_PRIVATE_ADS_RECEIPT=str(run / 'hook-receipt.json'))
        cli = root / 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe'
        command = [str(cli), 'build', str(root), '--target', 'Android', '--execute-method',
                   'PrivateProductionAdsBuild.Build', '--output-path', env['TAMER_PRIVATE_ADS_OUTPUT'],
                   '--editor-version', version, '--log-file', str(run / 'editor.private.log'),
                   '--no-tail', '--non-interactive', '--allow-dirty-build']
        # Dirty allowance is solely for journal-owned hooks. The entire original tree
        # was clean at preflight; Editor must still be closed before launch.
        require_editor_closed(root)
        require_staged_tree(root, state)
        with (run / 'cli.private.log').open('wb') as log:
            result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT)
        if result.returncode or not (run / 'hook-receipt.json').is_file():
            raise ValueError('Preparation did not complete')
        return {'hookReceiptPresent': True, 'binaryVerified': False, 'distributable': False}
    finally:
        if state:
            require_editor_closed(root)
            finalize(root, state)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkout', type=Path, required=True)
    parser.add_argument('--config', type=Path, required=True)
    parser.add_argument('--expected-head', required=True)
    parser.add_argument('--editor-version', required=True)
    parser.add_argument('--execute', action='store_true')
    args = parser.parse_args()
    try:
        if not args.checkout.is_absolute() or not args.config.is_absolute():
            raise ValueError('Explicit absolute paths required')
        # Reject linked checkout before resolving it away.
        safe_path(args.checkout, '.')
        root, config = args.checkout.resolve(), args.config.absolute()
        result = (execute(root, config, args.expected_head, args.editor_version) if args.execute else
                  preflight(root, config, args.expected_head, args.editor_version))
        print(json.dumps(result, indent=2))
    except Exception:
        # Neither exception text nor subprocess output may expose private IDs.
        parser.exit(1, 'Private preparation stopped. Inspect the owner checkout locally; '
                    'if .revival-local/private-ads-build-active.json exists, preserve it and '
                    'compare its snapshots/owned hashes before manual recovery.\n')


if __name__ == '__main__':
    main()
