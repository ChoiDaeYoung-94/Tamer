"""Disabled candidate producer. No automatic build retry or external recovery.

Preparation writes private evidence only. Review receipts are externally supplied
and must bind exact bytes; creating a plan never promotes producer readiness.
"""
import argparse
from contextlib import ExitStack
import json
import os
from pathlib import Path
import subprocess
import uuid

import private_ads_build as base
from validate_private_ads_preparation import read_contract, unique_object
from windows_owned_files import LockedFile, locked_directories, verified_files
from private_ads_evidence import create_private_directory, require_private

RESOURCE = 'Assets/Resources/RevivalPrivateAdsRelease.bytes'
CLI = 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe'
KEY = 'src/AeDeong.keystore'


def installed_tool_inventory(manifest_path, version):
    """Explicit installed Editor/compiler/Android tool manifest, not a version label."""
    manifest = read_json(manifest_path)
    if set(manifest) != {'schema', 'editorVersion', 'editorExecutable', 'files'} or manifest['schema'] != 1 or manifest['editorVersion'] != version:
        raise ValueError('Installed tool manifest rejected')
    expected_editor = Path('C:/Program Files/Unity/Hub/Editor') / version / 'Editor/Unity.exe'
    if Path(manifest['editorExecutable']) != expected_editor or not isinstance(manifest['files'], list):
        raise ValueError('CLI version selection must bind its installed Hub Editor')
    data = expected_editor.parent / 'Data'
    required = {expected_editor, data / 'Managed/UnityEditor.dll',
                data / 'netcorerun/netcorerun.exe', data / 'DotNetSdkRoslyn/csc.dll',
                data / 'il2cpp/build/deploy/il2cpp.exe',
                data / 'PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe',
                data / 'PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe'}
    files = {Path(p) for p in manifest['files']}
    if len(files) != len(manifest['files']) or not required.issubset(files):
        raise ValueError('Required installed tool binaries missing')
    result = {}
    for path in sorted(files):
        if not path.is_absolute() or not path.is_relative_to(expected_editor.parent):
            raise ValueError('Installed tool path rejected')
        # Reject linked ancestors before opening the same bound read handle.
        base.safe_path(expected_editor.parent, path.relative_to(expected_editor.parent))
        with LockedFile(path, deletable=False) as handle:
            result[str(path)] = {'identity': list(handle.identity), 'sha256': base.digest(handle.bytes())}
    return {'editorExecutable': str(expected_editor), 'manifestSha256': base.digest(manifest_path.read_bytes()), 'files': result}


def pin_installed_tools(stack, tools):
    if not tools or tools['editorExecutable'] not in tools['files']:
        raise ValueError('Actual installed Editor binding required')
    for path, expected in tools['files'].items():
        handle = stack.enter_context(LockedFile(Path(path), deletable=False))
        handle.verify(expected['identity'], expected['sha256'])


def encoded(value):
    return json.dumps(value, ensure_ascii=True, sort_keys=True, indent=2).encode('utf-8')


def write_new(path, raw):
    # Asset staging uses its existing project ACL; all private evidence lives in
    # the atomically secured run directory. Never write sensitive bytes elsewhere.
    if '.revival-local' in path.parts and path.name != Path(base.JOURNAL).name:
        require_private(path.parent)
    with path.open('xb') as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())


def read_json(path):
    def reject(_):
        raise ValueError('Invalid JSON constant')
    return json.loads(path.read_bytes(), object_pairs_hook=unique_object, parse_constant=reject)


def inventory(root, config):
    """All purchased assets/meta, settings, packages, producer tools, key and artifacts."""
    paths = set()
    for folder in ('Assets', 'ProjectSettings', 'Packages', 'tools/revival'):
        for path in base.safe_path(root, folder).rglob('*'):
            if path.is_symlink():
                raise ValueError('Linked inventory rejected')
            if path.is_file() and '__pycache__' not in path.parts:
                paths.add(path.relative_to(root).as_posix())
    for folder in ('Build', 'Builds'):
        for path in base.safe_path(root, folder).rglob('*'):
            if path.is_file() and path.suffix.lower() in ('.apk', '.aab'):
                paths.add(path.relative_to(root).as_posix())
    paths.add(config.relative_to(root).as_posix())
    for path in (KEY, CLI):
        if base.safe_path(root, path).is_file():
            paths.add(path)
    result = {}
    for path in sorted(paths):
        file = base.safe_path(root, path)
        before = base.identity(file)
        raw = file.read_bytes()
        if base.identity(file) != before:
            raise ValueError('Inventory identity changed')
        result[path] = {'identity': list(before), 'sha256': base.digest(raw)}
    return result


def resource_bytes(config, root):
    raw = config.read_bytes()
    contract = read_contract(raw, root)
    return encoded({'schema': 1, 'mode': 'disabled_candidate',
                    'androidAppId': contract['androidAppId'],
                    'productionRewardedAdUnit': contract['productionRewardedAdUnit'],
                    'productionActivationApproved': False, 'regionalReviewApproved': False,
                    'adultConsentReviewed': False, 'configSha256': base.digest(raw)})


def prepare_plan(root, config, head, version, *, branch, preflight_result, tool_manifest=None):
    """No subprocess or Assets write; caller supplies the read-only preflight result."""
    if preflight_result.get('disabledPreparationValid') is not True:
        raise ValueError('Read-only preflight required')
    if base.digest(config.read_bytes()) != preflight_result.get('configSha256'):
        raise ValueError('Configuration changed')
    if base.safe_path(root, base.JOURNAL).exists():
        raise ValueError('Active recovery required')
    run_id = uuid.uuid4().hex
    run_rel = '.revival-local/private-ads-build/' + run_id
    run = base.safe_path(root, run_rel)
    run.parent.mkdir(parents=True, exist_ok=True)
    create_private_directory(run)
    hook = base.HOOK + '_' + run_id
    staged = {hook + '.meta': base.meta(True)}
    for name in base.TEMPLATES:
        staged[hook + '/' + name] = (Path(__file__).parent / 'private_ads_build' / name).read_bytes()
        staged[hook + '/' + name + '.meta'] = base.meta()
    staged[RESOURCE] = resource_bytes(config, root)
    staged[RESOURCE + '.meta'] = ('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex +
        '\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n'
        '  assetBundleName: \n  assetBundleVariant: \n').encode()
    for path in staged:
        if base.safe_path(root, path).exists():
            raise ValueError('Staging collision')
    before = inventory(root, config)
    for index, (path, entry) in enumerate(before.items()):
        if path in (KEY, CLI):
            # Immutable and never recovery targets. Do not duplicate the release key.
            continue
        backup = run / ('before-' + str(index) + '.snapshot')
        raw = base.safe_path(root, path).read_bytes()
        if base.digest(raw) != entry['sha256']:
            raise ValueError('Snapshot drift')
        write_new(backup, raw)
        entry['backup'] = backup.relative_to(root).as_posix()
    owned = {}
    for index, (path, raw) in enumerate(staged.items()):
        payload = run / ('stage-' + str(index) + '.snapshot')
        write_new(payload, raw)
        owned[path] = {'sha256': base.digest(raw), 'payload': payload.relative_to(root).as_posix(),
                       'absentBefore': True, 'writer': 'producer'}
    tools = installed_tool_inventory(tool_manifest, version) if tool_manifest is not None else None
    plan = {'schema': 2, 'mode': 'disabled_candidate', 'checkout': str(root), 'head': head,
            'branch': branch, 'editorVersion': version, 'runId': run_id, 'run': run_rel,
            'hook': hook, 'config': str(config), 'configSha256': preflight_result['configSha256'],
            'resourceSha256': owned[RESOURCE]['sha256'], 'before': before, 'owned': owned, 'installedTools': tools,
            'producerPrepared': False, 'binaryVerified': False, 'distributable': False}
    file = run / 'plan.private.json'
    write_new(file, encoded(plan))
    return {'plan': str(file), 'planSha256': base.digest(file.read_bytes()),
            'inventoryContractValid': True, 'producerPrepared': False,
            'actualBuildExecuted': False, 'binaryVerified': False, 'distributable': False}


def load_plan(root, path, reviewed_sha):
    path = base.safe_path(root, Path(path).relative_to(root))
    if not path.is_relative_to(root / '.revival-local') or base.digest(path.read_bytes()) != reviewed_sha:
        raise ValueError('Exact private plan review required')
    plan = read_json(path)
    if (plan.get('schema') != 2 or plan.get('mode') != 'disabled_candidate' or
            plan.get('checkout') != str(root) or plan.get('producerPrepared') is not False or
            plan.get('binaryVerified') is not False or plan.get('distributable') is not False):
        raise ValueError('Candidate plan rejected')
    if base.safe_path(root, plan['run']) != path.parent:
        raise ValueError('Run path mismatch')
    if (not base.re.fullmatch('[0-9a-f]{32}', plan['runId']) or
            plan['run'] != '.revival-local/private-ads-build/' + plan['runId'] or
            plan['hook'] != base.HOOK + '_' + plan['runId']):
        raise ValueError('Owned run identity rejected')
    expected_owned = {plan['hook'] + '.meta', RESOURCE, RESOURCE + '.meta'}
    for name in base.TEMPLATES:
        expected_owned.update((plan['hook'] + '/' + name, plan['hook'] + '/' + name + '.meta'))
    if set(plan['owned']) != expected_owned:
        raise ValueError('Exact producer staging scope required')
    if plan['resourceSha256'] != base.digest(resource_bytes(Path(plan['config']), root)):
        raise ValueError('Disabled resource contract mismatch')
    for p, entry in plan['owned'].items():
        payload = base.safe_path(root, entry['payload'])
        if (not payload.is_relative_to(path.parent) or entry['absentBefore'] is not True or
                entry['writer'] != 'producer' or base.digest(payload.read_bytes()) != entry['sha256']):
            raise ValueError('Owned payload binding rejected')
        if p == RESOURCE and entry['sha256'] != plan['resourceSha256']:
            raise ValueError('Resource payload mismatch')
        if p.endswith('.cs') and payload.read_bytes() != (Path(__file__).parent / 'private_ads_build' / Path(p).name).read_bytes():
            raise ValueError('Template source mismatch')
    return plan


def require_unchanged(root, plan):
    now = inventory(root, Path(plan['config']))
    expected = {path: {key: item[key] for key in ('identity', 'sha256')}
                for path, item in plan['before'].items()}
    if now != expected:
        raise ValueError('Protected inventory changed')
    for entry in plan['before'].values():
        if 'backup' in entry and base.digest(base.safe_path(root, entry['backup']).read_bytes()) != entry['sha256']:
            raise ValueError('Immutable original snapshot mismatch')
    for path, item in plan['owned'].items():
        if base.safe_path(root, path).exists() or base.digest(
                base.safe_path(root, item['payload']).read_bytes()) != item['sha256']:
            raise ValueError('Staging collision or payload mismatch')


def require_source_review(root, plan, review):
    review = read_json(base.safe_path(root, Path(review).relative_to(root)))
    sources = {p: e['sha256'] for p, e in plan['before'].items() if p.startswith('tools/revival/')}
    if (set(review) != {'schema', 'sourceHead', 'sourceSha256', 'minimumChecksPassed',
                       'pureCompilationPassed', 'independentSourceReviewPassed'} or
            review['schema'] != 1 or review['sourceHead'] != plan['head'] or
            review['sourceSha256'] != sources or any(review[k] is not True for k in
            ('minimumChecksPassed', 'pureCompilationPassed', 'independentSourceReviewPassed'))):
        raise ValueError('Implemented exact source review required')


def require_signing(root, plan, signing):
    """Externally verified existing release key; passwords live only in child env."""
    signing = read_json(base.safe_path(root, Path(signing).relative_to(root)))
    expected = {'schema', 'keystore', 'keystoreSha256', 'alias', 'certificateSha256',
                'existingReleaseKeyVerified', 'sourceHead', 'planSha256'}
    if (set(signing) != expected or signing['schema'] != 1 or signing['keystore'] != KEY or
            signing['existingReleaseKeyVerified'] is not True or signing['sourceHead'] != plan['head'] or
            signing['planSha256'] != plan['_reviewedSha256'] or KEY not in plan['before'] or
            signing['keystoreSha256'] != plan['before'][KEY]['sha256'] or
            not isinstance(signing['alias'], str) or not signing['alias'] or
            not isinstance(signing['certificateSha256'], str) or
            not base.re.fullmatch('[0-9a-f]{64}', signing['certificateSha256']) or
            not os.environ.get('TAMER_KEYSTORE_PASS') or not os.environ.get('TAMER_KEYALIAS_PASS')):
        raise ValueError('Verified existing release signing binding and credentials required')
    return signing


def observe(root, plan):
    """Capture actual bytes before any recovery. Observation authorizes no deletion."""
    run = base.safe_path(root, plan['run'])
    after = inventory(root, Path(plan['config']))
    # Hooks and resources are within Assets, hence included in the full inventory.
    changed = {p: dict(e) for p, e in after.items() if p not in plan['before'] or
               any(e[k] != plan['before'][p][k] for k in ('identity', 'sha256'))}
    for index, (path, entry) in enumerate(changed.items()):
        raw = base.safe_path(root, path).read_bytes()
        if base.digest(raw) != entry['sha256']:
            raise ValueError('Observed bytes changed')
        archive = run / ('after-' + str(index) + '.snapshot')
        write_new(archive, raw)
        entry['archive'] = archive.relative_to(root).as_posix()
    artifacts = {}
    candidate = run / 'disabled-preparation.aab'
    if candidate.exists():
        artifacts[candidate.relative_to(root).as_posix()] = {
            'identity': list(base.identity(candidate)), 'sha256': base.digest(candidate.read_bytes())}
    receipt = {'schema': 2, 'runId': plan['runId'], 'after': after, 'changed': changed, 'artifacts': artifacts,
               'missing': sorted(set(plan['before']) - set(after)),
               'binaryVerified': False, 'distributable': False}
    write_new(run / 'actual-delta.private.json', encoded(receipt))
    return receipt


def build_once(root, config, head, version, plan_path, reviewed_sha, source_review, signing,
               *, allow_unity_build=False):
    if not allow_unity_build:
        raise ValueError('Explicit Unity launch permission required')
    plan = load_plan(root, plan_path, reviewed_sha)
    require_private(base.safe_path(root, plan['run']))
    if plan['head'] != head or plan['editorVersion'] != version or plan['config'] != str(config):
        raise ValueError('Invocation mismatch')
    base.preflight(root, config, head, version)
    if base.git(root, 'branch', '--show-current') != plan['branch']:
        raise ValueError('Branch changed')
    require_unchanged(root, plan)
    bindings = {}
    for path in (Path(plan_path), Path(source_review), Path(signing)):
        path = base.safe_path(root, path.relative_to(root))
        bindings[path] = (base.identity(path), base.digest(path.read_bytes()))
    require_source_review(root, plan, source_review)
    plan['_reviewedSha256'] = reviewed_sha
    sign = require_signing(root, plan, signing)
    with ExitStack() as tool_check:
        pin_installed_tools(tool_check, plan['installedTools'])
    if CLI not in plan['before']:
        raise ValueError('Exact CLI missing from review')
    run = base.safe_path(root, plan['run'])
    for name in ('launch-once.private.json', 'hook-receipt.json', 'disabled-preparation.aab',
                 'actual-delta.private.json', 'settings-restoration.json'):
        if (run / name).exists():
            raise ValueError('Run already attempted or output exists')
    # Marker and journal precede Assets writes; an interrupted partial stage stays visible.
    write_new(run / 'launch-once.private.json', encoded({'runId': plan['runId'], 'planSha256': reviewed_sha,
        'sourceReviewSha256': base.digest(Path(source_review).read_bytes()),
        'signingReceiptSha256': base.digest(Path(signing).read_bytes())}))
    write_new(base.safe_path(root, base.JOURNAL), encoded({'run': plan['run'], 'planSha256': reviewed_sha}))
    try:
        base.safe_path(root, plan['hook']).mkdir(parents=True, exist_ok=False)
        for path, item in plan['owned'].items():
            target = base.safe_path(root, path)
            target.parent.mkdir(parents=True, exist_ok=True)
            write_new(target, base.safe_path(root, item['payload']).read_bytes())
        staged = {p: {'identity': list(base.identity(base.safe_path(root, p))), 'sha256': e['sha256']}
                  for p, e in plan['owned'].items()}
        write_new(run / 'staged-owned.private.json', encoded(staged))
        env = {k: v for k, v in os.environ.items() if not k.startswith('TAMER_PRIVATE_ADS_')}
        env.update(TAMER_PRIVATE_ADS_PREPARE='1', TAMER_PRIVATE_ADS_RUN_ID=plan['runId'],
                   TAMER_PRIVATE_ADS_SOURCE_HEAD=head, TAMER_PRIVATE_ADS_CONFIG=str(config),
                   TAMER_PRIVATE_ADS_SHA256=plan['configSha256'],
                   TAMER_PRIVATE_ADS_OUTPUT=str(run / 'disabled-preparation.aab'),
                   TAMER_PRIVATE_ADS_RECEIPT=str(run / 'hook-receipt.json'),
                   TAMER_PRIVATE_ADS_RESOURCE_SHA256=plan['resourceSha256'],
                   TAMER_PRIVATE_ADS_EDITOR=plan['installedTools']['editorExecutable'],
                   TAMER_PRIVATE_ADS_EDITOR_SHA256=plan['installedTools']['files'][plan['installedTools']['editorExecutable']]['sha256'],
                   TAMER_PRIVATE_ADS_SETTINGS_RECEIPT=str(run / 'settings-restoration.json'),
                   TAMER_PRIVATE_ADS_KEYSTORE=str(base.safe_path(root, KEY)),
                   TAMER_PRIVATE_ADS_KEYSTORE_SHA256=sign['keystoreSha256'],
                   TAMER_PRIVATE_ADS_KEY_ALIAS=sign['alias'])
        # Never let credentials go to CLI output/receipt; C# consumes execution-only env.
        command = [str(base.safe_path(root, CLI)), 'build', str(root), '--target', 'Android',
                   '--execute-method', 'PrivateProductionAdsBuild.Build', '--output-path',
                   env['TAMER_PRIVATE_ADS_OUTPUT'], '--editor-version', version,
                   '--log-file', str(run / 'editor.private.log'), '--no-tail', '--non-interactive',
                   '--allow-dirty-build']
        base.require_editor_closed(root)
        expected = {p: {k: e[k] for k in ('identity', 'sha256')} for p, e in plan['before'].items()}
        expected.update(staged)
        if inventory(root, config) != expected or base.git(root, 'diff', '--name-only', 'HEAD'):
            raise ValueError('Prelaunch protected inventory changed')
        immutable = [p for p in plan['before'] if not p.startswith(('Assets/', 'ProjectSettings/', 'Packages/'))]
        with locked_directories([base.safe_path(root, p).parent for p in immutable]), ExitStack() as stack:
            for path, expected in bindings.items():
                handle = stack.enter_context(LockedFile(path, deletable=False))
                handle.verify(*expected)
            pin_installed_tools(stack, plan['installedTools'])
            for p in immutable:
                handle = stack.enter_context(LockedFile(base.safe_path(root, p), deletable=False))
                handle.verify(plan['before'][p]['identity'], plan['before'][p]['sha256'])
            with (run / 'cli.private.log').open('xb') as log:
                result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT)
        write_new(run / 'invocation.private.json', encoded({'returnCode': result.returncode}))
    finally:
        # No restore, delete, retry or promotion here, even on failed invocation.
        base.require_editor_closed(root)
        observe(root, plan)
    state = dict(plan)
    callback = base.verify_hook_receipt(root, state)
    if result.returncode:
        raise ValueError('Single invocation failed; preserve actual delta')
    restored = read_json(run / 'settings-restoration.json')
    if restored != {'schema': 1, 'runId': plan['runId'], 'scopedSettingsRestored': True}:
        raise ValueError('Scoped settings restoration unverified')
    return dict(callback, actualBuildExecuted=True, externalRecoveryRequired=True)


def recover_reviewed(root, plan_path, plan_sha, recovery_path, recovery_sha):
    """Only an exact reviewed actual delta may authorize external file operations."""
    plan = load_plan(root, plan_path, plan_sha)
    require_private(base.safe_path(root, plan['run']))
    run = base.safe_path(root, plan['run'])
    base.require_editor_closed(root)
    if base.git(root, 'rev-parse', 'HEAD') != plan['head'] or base.git(root, 'branch', '--show-current') != plan['branch']:
        raise ValueError('Source checkout changed')
    raw = base.safe_path(root, Path(recovery_path).relative_to(root)).read_bytes()
    if base.digest(raw) != recovery_sha:
        raise ValueError('Exact recovery review required')
    review = read_json(Path(recovery_path))
    delta_path = run / 'actual-delta.private.json'
    delta = read_json(delta_path)
    if (set(review) != {'schema', 'runId', 'planSha256', 'deltaSha256', 'restore', 'delete', 'lateWriters'} or
            review['schema'] != 1 or review['runId'] != plan['runId'] or review['planSha256'] != plan_sha or
            review['deltaSha256'] != base.digest(delta_path.read_bytes()) or delta['missing']):
        raise ValueError('Recovery binding or missing original rejected')
    current = inventory(root, Path(plan['config']))
    if current != delta['after']:
        raise ValueError('Observed identity/content drift')
    restore = set(review['restore'])
    delete = set(review['delete'])
    if len(restore) != len(review['restore']) or len(delete) != len(review['delete']) or restore & delete:
        raise ValueError('Duplicate recovery scope')
    original_changes = set(delta['changed']) & set(plan['before'])
    new = set(delta['changed']) - set(plan['before'])
    if restore != original_changes or delete != new:
        raise ValueError('Unknown recovery scope')
    # Producer helpers/config/key/old artifacts are immutable, never a recovery target.
    if any(not p.startswith(('Assets/', 'ProjectSettings/', 'Packages/')) for p in restore | delete):
        raise ValueError('Protected helper/signing/artifact drift')
    staged_path = run / 'staged-owned.private.json'
    staged = read_json(staged_path) if staged_path.exists() else {}
    if (new & set(plan['owned'])) - set(staged):
        raise ValueError('Partial staging needs separate producer ownership evidence')
    late = new - set(staged)
    if set(review['lateWriters']) != late or any(review['lateWriters'][p] != 'Unity' for p in late):
        raise ValueError('Late-created exact writer review required')
    if any(p not in plan['owned'] or delta['after'][p] != e for p, e in staged.items()):
        raise ValueError('Staged owner changed')
    originals = {p: base.safe_path(root, plan['before'][p]['backup']).read_bytes() for p in restore}
    for p, raw in originals.items():
        if base.digest(raw) != plan['before'][p]['sha256']:
            raise ValueError('Original snapshot changed')
    for p in restore | delete:
        e = delta['changed'][p]
        if base.digest(base.safe_path(root, e['archive']).read_bytes()) != e['sha256']:
            raise ValueError('Actual byte archive changed')
    targets = {p: (delta['after'][p]['identity'], delta['after'][p]['sha256']) for p in restore | delete}
    journal = base.safe_path(root, base.JOURNAL)
    if read_json(journal) != {'run': plan['run'], 'planSha256': plan_sha}:
        raise ValueError('Active journal mismatch')
    journal_expected = (base.identity(journal), base.digest(journal.read_bytes()))
    # Every target/ancestor acquired before the first write. Directories are preserved.
    with locked_directories([run]), ExitStack() as pins:
        # Prevent unrelated originals, snapshots and old artifacts from changing
        # between the full inventory comparison and journal completion.
        for p, e in delta['after'].items():
            if p not in targets:
                handle = pins.enter_context(LockedFile(base.safe_path(root, p), deletable=False))
                handle.verify(e['identity'], e['sha256'])
        for p in restore | delete:
            entry = delta['changed'][p]
            archive = base.safe_path(root, entry['archive'])
            handle = pins.enter_context(LockedFile(archive, deletable=False))
            handle.verify(base.identity(archive), entry['sha256'])
        for p, e in delta['artifacts'].items():
            handle = pins.enter_context(LockedFile(base.safe_path(root, p), deletable=False))
            handle.verify(e['identity'], e['sha256'])
        with LockedFile(journal) as journal_handle, verified_files(root, targets, writable=True) as handles:
            journal_handle.verify(*journal_expected)
            for p in sorted(restore):
                handles[p].restore_bytes(originals[p], plan['before'][p]['sha256'])
            for p in sorted(delete):
                handles[p].delete()
        after = inventory(root, Path(plan['config']))
        if set(after) != set(plan['before']) or any(after[p]['sha256'] != e['sha256'] for p, e in plan['before'].items()):
            raise ValueError('Recovery content/inventory mismatch')
        if base.git(root, 'status', '--porcelain'):
            raise ValueError('Recovered checkout not clean')
        base.require_editor_closed(root)
        with LockedFile(journal) as handle:
            handle.verify(*journal_expected)
            write_new(run / 'journal.completed.json', handle.bytes())
            write_new(run / 'recovery-receipt.private.json', encoded({'schema': 1,
                'runId': plan['runId'], 'contentInventoryRestored': True, 'cleanClosed': True,
                'directoriesPreserved': True, 'originalFileIdentitiesRestored': False,
                'binaryVerified': False, 'distributable': False}))
            handle.delete()
    return {'externalRecoveryComplete': True, 'binaryVerified': False, 'distributable': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('prepare', 'build-once', 'recover'))
    parser.add_argument('--checkout', type=Path, required=True)
    parser.add_argument('--config', type=Path, required=True)
    parser.add_argument('--expected-head', required=True)
    parser.add_argument('--editor-version', required=True)
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--reviewed-plan-sha256')
    parser.add_argument('--source-review', type=Path)
    parser.add_argument('--tool-manifest', type=Path)
    parser.add_argument('--signing', type=Path)
    parser.add_argument('--allow-unity-build', action='store_true')
    parser.add_argument('--recovery', type=Path)
    parser.add_argument('--reviewed-recovery-sha256')
    args = parser.parse_args()
    try:
        if not args.checkout.is_absolute() or not args.config.is_absolute():
            raise ValueError('Absolute paths required')
        base.safe_path(args.checkout, '.')
        root = args.checkout.resolve()
        if args.action == 'prepare':
            pre = base.preflight(root, args.config, args.expected_head, args.editor_version)
            result = prepare_plan(root, args.config, args.expected_head, args.editor_version,
                                  branch=base.git(root, 'branch', '--show-current'), preflight_result=pre,
                                  tool_manifest=args.tool_manifest)
        elif args.action == 'build-once':
            result = base.execute(root, args.config, args.expected_head, args.editor_version,
                                  plan=args.plan, reviewed_sha256=args.reviewed_plan_sha256,
                                  source_review=args.source_review, signing=args.signing,
                                  allow_unity_build=args.allow_unity_build)
        else:
            result = recover_reviewed(root, args.plan, args.reviewed_plan_sha256,
                                      args.recovery, args.reviewed_recovery_sha256)
        # Preparation paths/hashes are private; callers inspect the ignored receipts.
        print(json.dumps({k: v for k, v in result.items() if k not in ('plan', 'planSha256')}))
    except Exception:
        parser.exit(1, 'Private producer stopped; preserve the active journal and local evidence.\n')


if __name__ == '__main__':
    main()
