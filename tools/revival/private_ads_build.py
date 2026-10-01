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
from windows_owned_files import locked_directories, verified_files

HOOK = 'Assets/Scripts/Editor/RevivalPrivateAdsPreparation'
JOURNAL = '.revival-local/private-ads-build-active.json'
TEMPLATES = ('PrivateAdsContract.cs', 'PrivateAdsSceneInjection.cs', 'PrivateProductionAdsBuild.cs')
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
    return {key: value for key, value in state.items()
            if key not in ('created', 'folderIdentity', 'journalIdentity', 'journalSha256')}


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
    with journal_path.open('xb') as stream:
        if owner is not None:
            owner.update(state)
            state = owner
        info = os.fstat(stream.fileno())
        state['journalIdentity'] = (info.st_dev, info.st_ino)
        raw_journal = json.dumps(journal_view(state), indent=2).encode('utf-8')
        state['journalSha256'] = digest(raw_journal)
        stream.write(raw_journal)
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
    """Handle-bound Windows cleanup. Keep directories for explicit manual recovery.

    Deletion of several files is not atomic. Any failure retains the active journal;
    some verified own files may already have been deleted. Never retry/fallback here.
    """
    expected = {JOURNAL: (state['journalIdentity'], state['journalSha256'])}
    expected.update({path: (ident, state['owned'][path]) for path, ident in state['created'].items()})
    with locked_directories([safe_path(root, state['run'])]):
        with verified_files(root, expected) as handles:
            raw_journal = handles[JOURNAL].bytes()
            saved = json.loads(raw_journal, object_pairs_hook=unique_object)
            if saved != journal_view(state) or saved['checkout'] != str(root):
                raise ValueError('Journal ownership mismatch')
            allowed = set(state['created'])
            folder = safe_path(root, HOOK)
            if folder.exists() and any(p.relative_to(root).as_posix() not in allowed for p in folder.rglob('*')):
                raise ValueError('Unexpected staging contents')
            if any(safe_path(root, path).exists() for path in set(state['owned']) - allowed):
                raise ValueError('Planned path was not created by this invocation')
            # Reserve receipts exclusively before deletion; never overwrite someone
            # else's result. A partial/empty receipt after failure is not success.
            with safe_path(root, state['run'] + '/journal.completed.json').open('xb') as completed:
                with safe_path(root, state['run'] + '/cleanup.json').open('xb') as receipt:
                    for path in state['created']:
                        handles[path].delete()
                        handles[path].close()
                    changed = [path for path, original in state['sources'].items()
                               if not safe_path(root, path).is_file() or
                               digest(safe_path(root, path).read_bytes()) != original['sha256']]
                    changed += list(set(protected_paths(root)) - set(state['sources']))
                    if (changed or git(root, 'rev-parse', 'HEAD') != state['head'] or
                            git(root, 'branch', '--show-current') != state['branch'] or
                            git(root, 'status', '--porcelain')):
                        raise ValueError('Source drift: original journal and snapshots retained')
                    # Keep the active journal while any folder/unknown child remains.
                    # No directory is ever automatically removed or claimed as owned.
                    result = {'cleanupOwnedFiles': True, 'originalsUnchanged': True,
                              'directoryPreserved': folder.exists(), 'manualRecoveryRequired': folder.exists(),
                              'binaryVerified': False, 'distributable': False}
                    completed.write(raw_journal)
                    completed.flush()
                    os.fsync(completed.fileno())
                    receipt.write(json.dumps(result, indent=2).encode('utf-8'))
                    receipt.flush()
                    os.fsync(receipt.fileno())
                    if not folder.exists():
                        handles[JOURNAL].delete()
            return result


def execute(root, config, head, version):
    # The authorized third synthetic contract check passed for its 59 inputs.
    # Unity compatibility, binary verification and retained-directory review remain incomplete.
    raise ValueError('Execution blocked: Unity and lifecycle readiness incomplete')



def verify_hook_receipt(root, state):
    """Bind the disabled callback result to this owned run; never certify a binary."""
    run = safe_path(root, state['run'])
    def reject_constant(_):
        raise ValueError('Invalid receipt constant')
    receipt = json.loads(safe_path(root, state['run'] + '/hook-receipt.json').read_text(encoding='utf-8'),
                         object_pairs_hook=unique_object, parse_constant=reject_constant)
    expected = {'schema': 1, 'runId': state['runId'], 'sourceHead': state['head'],
                'configSha256': state['configSha256'], 'unityVersion': state['editorVersion'],
                'injectedManagers': 1, 'loginScenes': 1, 'preprocessed': True, 'postprocessed': True,
                'buildSceneValueMatched': True, 'compiledEditorGatesDisabled': True,
                'productionContractVerified': False, 'binaryVerified': False, 'distributable': False}
    extra = {'artifactSha256', 'configuredAndroidDefines', 'prospectivePlayerDefinePlanSha256'}
    if not isinstance(receipt, dict) or set(receipt) != set(expected) | extra:
        raise ValueError('Receipt schema rejected')
    if any(type(receipt[key]) is not type(value) or receipt[key] != value
           for key, value in expected.items()):
        raise ValueError('Receipt run or callback state rejected')
    if any(not isinstance(receipt[key], str) or not re.fullmatch('[0-9a-f]{64}', receipt[key])
           for key in ('artifactSha256', 'prospectivePlayerDefinePlanSha256')):
        raise ValueError('Receipt digest rejected')
    defines = receipt['configuredAndroidDefines']
    if not isinstance(defines, str) or any(symbol.startswith('TAMER_') or
            'TEST' in symbol.upper() or 'HARNESS' in symbol.upper() or symbol == 'DEVELOPMENT_BUILD'
            for symbol in defines.split(';')):
        raise ValueError('Receipt configured defines rejected')
    if digest(safe_path(root, Path(state['config']).relative_to(root)).read_bytes()) != state['configSha256']:
        raise ValueError('Receipt configuration changed')
    artifact = safe_path(root, state['run'] + '/disabled-preparation.aab')
    if not artifact.is_file() or artifact.stat().st_size == 0 or digest(artifact.read_bytes()) != receipt['artifactSha256']:
        raise ValueError('Receipt artifact mismatch')
    return {'hookReceiptVerified': True, 'productionContractVerified': False,
            'binaryVerified': False, 'distributable': False}


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
        env.update(TAMER_PRIVATE_ADS_PREPARE='1', TAMER_PRIVATE_ADS_RUN_ID=state['runId'],
                   TAMER_PRIVATE_ADS_SOURCE_HEAD=state['head'], TAMER_PRIVATE_ADS_CONFIG=str(config),
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
        return verify_hook_receipt(root, state)
    finally:
        if state:
            require_editor_closed(root)
            cleanup = finalize(root, state)
            if cleanup['manualRecoveryRequired']:
                raise ValueError('Owned files cleaned; retained directory/journal require manual review')


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
