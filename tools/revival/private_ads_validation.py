"""Owned registration/synthetic validation, separate from blocked production execution.

Prepare is read-only for Assets. Run-once stages only declared templates. Recovery
is a separately reviewed step; no automatic deletion/restoration on failure.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import uuid
from private_ads_build import safe_path, require_editor_closed, protected_paths, git, meta
from windows_owned_files import LockedFile, locked_directories, verified_files
from install_bundletool import DEST as BUNDLETOOL, SHA256 as BUNDLETOOL_SHA
from verify_private_ads_synthetic import verify as verify_synthetic

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'Logs/revival/private-ads-validation'
HOOK = 'Assets/Scripts/Editor/RevivalPrivateAdsValidation'
FIXTURE = 'Assets/RevivalPrivateAdsSynthetic'
CLI = ROOT / 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe'
CLI_SHA = 'f2b2571cd5e9d9975be3cab0b7e3931a9f6193c42c2a491f7c30d72861349993'
JDK = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin')
BLOCKED = {'TAMER_KEYSTORE_PASS', 'TAMER_KEYALIAS_PASS', 'TAMER_UPLOAD_KEYSTORE_PATH',
           'TAMER_UPLOAD_KEY_ALIAS', 'TAMER_UPLOAD_CERT_SHA256', 'TAMER_IAP_TEST_KEY_PASSWORD'}
MODES = {
    'registration': ('PrivateAdsContract.cs', 'PrivateAdsSceneInjection.cs', 'PrivateProductionAdsBuild.cs'),
    'synthetic': ('PrivateAdsSceneInjection.cs', 'PrivateAdsSyntheticCallbacks.cs', 'PrivateAdsSyntheticBuild.cs')}

HELPERS = ('tools/revival/private_ads_validation.py', 'tools/revival/private_ads_build.py',
           'tools/revival/windows_owned_files.py', 'tools/revival/install_bundletool.py',
           'tools/revival/validate_private_ads_preparation.py', 'tools/revival/verify_private_ads_synthetic.py',
           'tools/revival/VerifyAabSignature.java')
def source_paths(mode):
    names = set(MODES[mode]) | {'PrivateAdsRegistrationProbe.cs'}
    if mode == 'synthetic': names.add('PrivateAdsSyntheticInvoke.cs')
    return set(HELPERS) | {'tools/revival/private_ads_build/' + name for name in names}
def staged_paths(mode):
    paths = {HOOK + '.meta'}
    for name in MODES[mode]: paths.update({HOOK + '/' + name, HOOK + '/' + name + '.meta'})
    if mode == 'synthetic': paths.add(FIXTURE + '.meta')
    return paths
def snapshot_paths():
    return set(protected_paths(ROOT)) | {'Assets/Settings/Settings/UniversalRP-LowQuality.asset',
                                         'Assets/Settings/Settings/UniversalRP-MediumQuality.asset'}
def validate_manifest(manifest):
    if manifest.get('schema') != 1 or manifest.get('checkout') != str(ROOT) or manifest.get('mode') not in MODES:
        raise ValueError('Fixed manifest schema/checkout/mode required')
    mode = manifest['mode']
    if set(manifest['sources']) != source_paths(mode) or set(manifest['staged']) != staged_paths(mode):
        raise ValueError('Source/staging set changed')
    for name in MODES[mode]:
        if manifest['staged'][HOOK + '/' + name]['sha256'] != manifest['sources']['tools/revival/private_ads_build/' + name]:
            raise ValueError('Staged code does not match reviewed source')
    entry_name = 'PrivateAdsRegistrationProbe.cs' if mode == 'registration' else 'PrivateAdsSyntheticInvoke.cs'
    if manifest['entrySha256'] != manifest['sources']['tools/revival/private_ads_build/' + entry_name]:
        raise ValueError('Entry does not match reviewed source')
    if set(manifest['toolHashes']) != {str(JDK / 'java.exe'), str(JDK / 'keytool.exe')}:
        raise ValueError('Tool set changed')
    for item in list(manifest['staged'].values()) + list(manifest['snapshots'].values()):
        if Path(item['backup']).name != item['backup'] or not item['backup'].startswith(('stage-', 'snapshot-')):
            raise ValueError('Unsafe backup path')

def sha(raw): return hashlib.sha256(raw).hexdigest()
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def create(path, raw):
    with path.open('xb') as stream:
        stream.write(raw); stream.flush(); os.fsync(stream.fileno())
        info = os.fstat(stream.fileno())
        return [info.st_dev, info.st_ino]
def save(path, value): return create(path, json.dumps(value, indent=2).encode('utf-8'))
def child_env():
    return {key: value for key, value in os.environ.items()
            if not key.upper().startswith('TAMER_PRIVATE_ADS_') and key.upper() not in BLOCKED}
def require_clean(head):
    require_editor_closed(ROOT)
    if git(ROOT, 'rev-parse', '--show-toplevel').replace('\\', '/').lower() != ROOT.as_posix().lower():
        raise ValueError('Wrong checkout')
    if git(ROOT, 'rev-parse', 'HEAD') != head or git(ROOT, 'status', '--porcelain'):
        raise ValueError('Clean expected HEAD required')
    if sha(CLI.read_bytes()) != CLI_SHA:
        raise ValueError('CLI changed')
    if '6000.3.25f1 (e1dba0a9aba4)' not in (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text():
        raise ValueError('Unity pin mismatch')

def prepare(mode, head):
    require_clean(head)
    for relative in (HOOK, HOOK + '.meta', FIXTURE, FIXTURE + '.meta',
                     'Assets/Scripts/Editor/RevivalPrivateAdsPreparation', '.revival-local/private-ads-build-active.json'):
        if safe_path(ROOT, relative).exists(): raise ValueError('Existing staging state requires review')
    output = safe_path(ROOT, 'Build/revival/privateads-synthetic.aab')
    if mode == 'synthetic' and output.exists(): raise ValueError('Synthetic output already exists')
    if mode == 'synthetic' and (not BUNDLETOOL.is_file() or sha(BUNDLETOOL.read_bytes()) != BUNDLETOOL_SHA):
        raise ValueError('Pinned bundletool required')
    debug_sha = None
    debug_key = Path.home() / '.android/debug.keystore'
    if mode == 'synthetic':
        with locked_directories([debug_key.parent]), LockedFile(debug_key, deletable=False) as key:
            debug_sha = sha(key.bytes())
    run = BASE / uuid.uuid4().hex; run.mkdir(parents=True)
    files = {HOOK + '.meta': meta(True)}
    sources = {}
    for name in MODES[mode]:
        source = ROOT / 'tools/revival/private_ads_build' / name
        raw = source.read_bytes(); sources[source.relative_to(ROOT).as_posix()] = sha(raw)
        files[HOOK + '/' + name] = raw; files[HOOK + '/' + name + '.meta'] = meta()
    if mode == 'synthetic': files[FIXTURE + '.meta'] = meta(True)
    staged = {}
    for index, (relative, raw) in enumerate(files.items()):
        stored = 'stage-' + str(index); create(run / stored, raw)
        staged[relative] = {'backup': stored, 'sha256': sha(raw)}
    snapshots = {}
    paths = snapshot_paths()
    for index, relative in enumerate(sorted(paths)):
        raw = safe_path(ROOT, relative).read_bytes(); stored = 'snapshot-' + str(index)
        create(run / stored, raw); snapshots[relative] = {'backup': stored, 'sha256': sha(raw)}
    probe = ROOT / 'tools/revival/private_ads_build/PrivateAdsRegistrationProbe.cs'
    sources[probe.relative_to(ROOT).as_posix()] = sha(probe.read_bytes())
    entry_file = probe if mode == 'registration' else ROOT / 'tools/revival/private_ads_build/PrivateAdsSyntheticInvoke.cs'
    sources[entry_file.relative_to(ROOT).as_posix()] = sha(entry_file.read_bytes())
    create(run / 'Entry.cs', entry_file.read_bytes())
    command = [str(CLI), 'run', str(ROOT), '--editor-version', '6000.3.25f1', '--command', 'run_script',
               '--timeout', '1800' if mode == 'synthetic' else '900', '--non-interactive', '--format', 'json', '--',
               '--file', str(run / 'Entry.cs'), '--mode', 'ephemeral', '--entry',
               'PrivateAdsRegistrationProbe.Inspect' if mode == 'registration' else 'PrivateAdsSyntheticInvoke.Run']
    sources = {relative: sha(safe_path(ROOT, relative).read_bytes()) for relative in source_paths(mode)}
    tools = {str(path): sha(path.read_bytes()) for path in (JDK / 'java.exe', JDK / 'keytool.exe')}
    save(run / 'manifest.json', {'schema': 1, 'mode': mode, 'head': head, 'branch': git(ROOT, 'branch', '--show-current'),
         'checkout': str(ROOT), 'sources': sources, 'staged': staged, 'snapshots': snapshots,
         'command': command, 'toolHashes': tools, 'entrySha256': sha(entry_file.read_bytes()), 'runnerSha256': sha(Path(__file__).read_bytes()),
         'debugKeySha256': debug_sha, 'buildExecuted': False, 'productionExecutionEnabled': False})
    return {'prepared': True, 'run': str(run), 'assetsStaged': False}

def run_once(run):
    run = run.resolve()
    if run.parent != BASE.resolve(): raise ValueError('Run outside private evidence root')
    manifest = read(run / 'manifest.json'); validate_manifest(manifest); require_clean(manifest['head'])
    if set(manifest['snapshots']) != snapshot_paths(): raise ValueError('Snapshot set changed')
    if manifest['runnerSha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Runner drift')
    if git(ROOT, 'branch', '--show-current') != manifest['branch']: raise ValueError('Branch drift')
    for relative, expected in manifest['sources'].items():
        if sha(safe_path(ROOT, relative).read_bytes()) != expected: raise ValueError('Source drift')
    for relative, item in manifest['snapshots'].items():
        if sha(safe_path(ROOT, relative).read_bytes()) != item['sha256']: raise ValueError('Snapshot drift')
    if sha((run / 'Entry.cs').read_bytes()) != manifest['entrySha256']: raise ValueError('Entry drift')
    for relative, item in manifest['staged'].items():
        if safe_path(ROOT, relative).exists() or sha((run / item['backup']).read_bytes()) != item['sha256']:
            raise ValueError('Stage collision or bytes changed')
    if manifest['mode'] not in MODES: raise ValueError('Unknown mode')
    expected_command = [str(CLI), 'run', str(ROOT), '--editor-version', '6000.3.25f1', '--command', 'run_script',
        '--timeout', '1800' if manifest['mode'] == 'synthetic' else '900', '--non-interactive', '--format', 'json', '--',
        '--file', str(run / 'Entry.cs'), '--mode', 'ephemeral', '--entry',
        'PrivateAdsRegistrationProbe.Inspect' if manifest['mode'] == 'registration' else 'PrivateAdsSyntheticInvoke.Run']
    if manifest['command'] != expected_command: raise ValueError('Command drift')
    for path, expected in manifest['toolHashes'].items():
        if sha(Path(path).read_bytes()) != expected: raise ValueError('Java tools changed')
    for relative in (HOOK, FIXTURE, FIXTURE + '.meta', 'Assets/Scripts/Editor/RevivalPrivateAdsPreparation',
                     '.revival-local/private-ads-build-active.json'):
        if safe_path(ROOT, relative).exists(): raise ValueError('Stale staging state')
    if manifest['mode'] == 'synthetic' and safe_path(ROOT, 'Build/revival/privateads-synthetic.aab').exists():
        raise ValueError('Output exists')
    # The exclusive journal is the one-attempt marker and precedes every Assets mutation.
    save(run / 'invocation-started.json', {'head': manifest['head'], 'mode': manifest['mode'],
        'manifestSha256': sha((run / 'manifest.json').read_bytes()), 'runnerSha256': sha(Path(__file__).read_bytes())})
    created = {}; result = None; error_type = None; debug_sha = None; invocation_passed = False
    try:
        safe_path(ROOT, HOOK).mkdir(exist_ok=False)
        if manifest['mode'] == 'synthetic':
            safe_path(ROOT, FIXTURE).mkdir(exist_ok=False)
            safe_path(ROOT, 'Build/revival').mkdir(parents=True, exist_ok=True)
            # Export only the existing standard debug key certificate; never generate/use a production key.
            debug_key = Path.home() / '.android/debug.keystore'
            with locked_directories([debug_key.parent]), LockedFile(debug_key, deletable=False) as key:
                if sha(key.bytes()) != manifest['debugKeySha256']: raise ValueError('Reviewed debug key changed')
                with (run / 'debug-certificate.der').open('xb') as out:
                    subprocess.run([str(JDK / 'keytool.exe'), '-exportcert', '-keystore', str(debug_key),
                        '-storepass', 'android', '-alias', 'androiddebugkey'], stdout=out,
                        stderr=subprocess.PIPE, check=True, env=child_env())
            debug_sha = sha((run / 'debug-certificate.der').read_bytes())
            save(run / 'debug-certificate-receipt.json', {'sha256': debug_sha})
        for relative, item in manifest['staged'].items():
            created[relative] = create(safe_path(ROOT, relative), (run / item['backup']).read_bytes())
        save(run / 'created-files.json', created)
        with (run / 'cli.private.json').open('xb') as out, (run / 'editor.private.log').open('xb') as err:
            result = subprocess.run(manifest['command'], cwd=ROOT, env=child_env(), stdout=out, stderr=err)
        response = read(run / 'cli.private.json')
        if result.returncode or not response.get('success') or not response.get('data', {}).get('success') or not response['data']['result'].get('success'):
            raise ValueError('Inner invocation failed')
        if manifest['mode'] == 'synthetic':
            save(run / 'artifact-check.json', verify_synthetic(ROOT / 'Build/revival/privateads-synthetic.aab', run / 'debug-certificate.der', debug_sha))
        invocation_passed = True
    except Exception as error:
        error_type = type(error).__name__
    finally:
        closed = True
        try: require_editor_closed(ROOT)
        except Exception: closed = False
        changes = {}
        for relative, item in manifest['snapshots'].items():
            path = safe_path(ROOT, relative)
            if not path.is_file() or sha(path.read_bytes()) != item['sha256']:
                stored = 'after-' + str(len(changes))
                if path.is_file():
                    raw = path.read_bytes(); create(run / stored, raw)
                    info = path.stat(); changes[relative] = {'backup': stored, 'sha256': sha(raw), 'identity': [info.st_dev, info.st_ino]}
                else: changes[relative] = {'missing': True}
        save(run / 'after.json', {'manifestSha256': sha((run / 'manifest.json').read_bytes()),
             'debugCertificateSha256': debug_sha, 'runnerSha256': sha(Path(__file__).read_bytes()), 'invocationMarkerSha256': sha((run / 'invocation-started.json').read_bytes()), 'changes': changes, 'created': created, 'editorClosed': closed,
             'exitCode': result.returncode if result is not None else None, 'errorType': error_type, 'invocationPassed': invocation_passed,
             'manualRecoveryRequired': True, 'productionExecutionEnabled': False})
    return {'invocations': 1, 'manualRecoveryRequired': True, 'receipt': str(run / 'after.json')}

def recover_reviewed(run, reviewed_after_sha):
    run = run.resolve()
    if run.parent != BASE.resolve(): raise ValueError('Unexpected evidence root')
    manifest = read(run / 'manifest.json'); validate_manifest(manifest); after_path = run / 'after.json'
    if sha(after_path.read_bytes()) != reviewed_after_sha: raise ValueError('Reviewed after receipt drift')
    after = read(after_path); require_editor_closed(ROOT)
    marker = read(run / 'invocation-started.json')
    current_manifest_sha = sha((run / 'manifest.json').read_bytes())
    current_runner_sha = sha(Path(__file__).read_bytes())
    if after['manifestSha256'] != current_manifest_sha or marker['manifestSha256'] != current_manifest_sha or \
       after['runnerSha256'] != current_runner_sha or marker['runnerSha256'] != current_runner_sha or \
       after['invocationMarkerSha256'] != sha((run / 'invocation-started.json').read_bytes()):
        raise ValueError('Reviewed evidence chain changed')
    if set(after['created']) - staged_paths(manifest['mode']): raise ValueError('Created path outside fixed set')
    current_protected = {p for p in snapshot_paths() if not p.startswith(HOOK + '/') and not p.startswith(FIXTURE + '/')}
    if set(manifest['snapshots']) != current_protected: raise ValueError('Protected snapshot set changed')
    if git(ROOT, 'rev-parse', 'HEAD') != manifest['head'] or git(ROOT, 'branch', '--show-current') != manifest['branch']:
        raise ValueError('Checkout changed')
    if git(ROOT, 'diff', '--cached', '--name-only'): raise ValueError('Unexpected staged changes')
    if set(git(ROOT, 'diff', '--name-only').splitlines()) != set(after['changes']):
        raise ValueError('Tracked change set does not match reviewed receipt')
    for relative, expected in manifest['sources'].items():
        if sha(safe_path(ROOT, relative).read_bytes()) != expected: raise ValueError('Reviewed source changed')
    for relative, item in after['changes'].items():
        if item.get('missing') or sha(safe_path(ROOT, relative).read_bytes()) != item['sha256']:
            raise ValueError('Current after bytes changed')
    save(run / 'recovery-started.json', {'reviewedAfterSha256': reviewed_after_sha})
    allowed = {'ProjectSettings/ProjectSettings.asset', 'ProjectSettings/EditorBuildSettings.asset',
               'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
               'Assets/Settings/Settings/UniversalRP-LowQuality.asset', 'Assets/Settings/Settings/UniversalRP-MediumQuality.asset'}
    if set(after['changes']) - allowed: raise ValueError('Change outside recovery allowlist')
    expected = {relative: (item['identity'], item['sha256']) for relative, item in after['changes'].items()}
    snapshots = {}
    for relative in expected:
        item = manifest['snapshots'][relative]; raw = (run / item['backup']).read_bytes()
        if sha(raw) != item['sha256']: raise ValueError('Snapshot bytes changed')
        snapshots[relative] = raw
    # All ancestor and file handles are acquired/identity+hash checked before the first write.
    with verified_files(ROOT, expected, writable=True) as handles:
        for relative, handle in handles.items():
            handle.restore_bytes(snapshots[relative], manifest['snapshots'][relative]['sha256'])
    for relative, item in manifest['snapshots'].items():
        if sha(safe_path(ROOT, relative).read_bytes()) != item['sha256']: raise ValueError('Protected snapshot mismatch')
    expected_files = {relative: (identity, manifest['staged'][relative]['sha256'])
                      for relative, identity in after['created'].items()}
    with verified_files(ROOT, expected_files) as handles:
        for handle in handles.values(): handle.delete()
    result = {'protectedSnapshotsRestored': True, 'ownedStagedFilesRemoved': True,
              'manualRecoveryRequired': True, 'retainedFoldersAndGeneratedFixture': True,
              'productionExecutionEnabled': False}
    save(run / 'recovery-result.json', result)
    return result

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    prepare_parser = sub.add_parser('prepare'); prepare_parser.add_argument('--mode', choices=MODES, required=True)
    prepare_parser.add_argument('--expected-head', required=True)
    run_parser = sub.add_parser('run-once'); run_parser.add_argument('--run', type=Path, required=True)
    recovery = sub.add_parser('recover-reviewed'); recovery.add_argument('--run', type=Path, required=True)
    recovery.add_argument('--reviewed-after-sha256', required=True)
    args = parser.parse_args()
    if args.action == 'prepare': result = prepare(args.mode, args.expected_head)
    elif args.action == 'run-once': result = run_once(args.run)
    else: result = recover_reviewed(args.run, args.reviewed_after_sha256)
    print(json.dumps(result))

if __name__ == '__main__': main()
