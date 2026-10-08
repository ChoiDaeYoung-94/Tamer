"""IAP source recovery: capture first, restore only a separately reviewed delta.

Never reads signing inputs. Nine originals and two initially absent config paths
are the entire allowed scope. Existing handle guards bind reads/writes to files.
"""
import argparse
import hashlib
import json
import os
import subprocess
from contextlib import contextmanager
from pathlib import Path

from private_ads_build import require_editor_closed, git
from private_ads_evidence import require_private
from windows_owned_files import LockedFile, locked_directories, verified_files

ORIGINALS = (
    'ProjectSettings/ProjectSettings.asset',
    'Assets/Plugins/Android/AndroidManifest.xml',
    'Assets/Plugins/Android/AndroidManifest.xml.meta',
    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset.meta',
    'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml',
    'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml.meta',
    'Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset',
    'Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset.meta',
)
TEMPORARIES = ('Assets/Resources/RevivalIapLocal.json', 'Assets/Resources/RevivalIapLocal.json.meta')
COLLISIONS = (
    'Build/revival/Tamer-iap-test.aab', 'Logs/revival/iap-bundle-build.log',
    'Logs/revival/iap-bundle-verification.json', 'Logs/revival/iap-bundle-native.json',
    'Build/AOSSettingAPK.txt', 'Build/AOSSettingAAB.txt', 'Build/checkedBuilding.txt',
)
SOURCE_FILES = (
    'tools/revival/Run-IapTestBundle.ps1', 'tools/revival/ProjectSettingsSnapshot.ps1',
    'tools/revival/iap_owned_recovery.py', 'tools/revival/windows_owned_files.py',
    'tools/revival/private_ads_build.py', 'tools/revival/private_ads_evidence.py',
    'tools/revival/validate_private_ads_preparation.py',
    'Assets/Scripts/Editor/RevivalIapBuild.cs', 'Assets/Scripts/Editor/RevivalBuild.cs',
)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


@contextmanager
def recovery_scope(root, path, manifest, expected_hash):
    # Keep the checkout and evidence ancestry fixed throughout every action.
    with locked_directories([root, path.parent]):
        require_private(path.parent)
        with LockedFile(path, deletable=False) as handle:
            raw = handle.bytes()
            if digest(raw) != expected_hash or json.loads(raw) != manifest:
                raise ValueError('Approved protection manifest changed')
            if os.path.normcase(str(root)) != os.path.normcase(manifest['projectRoot']):
                raise ValueError('Recovery checkout differs from the protected owner')
            with LockedFile(root, directory=True) as directory:
                if list(directory.identity) != manifest['projectRootIdentity']:
                    raise ValueError('Protected checkout directory was replaced')
            if git(root, 'rev-parse', 'HEAD') != manifest['head']:
                raise ValueError('Approved source HEAD changed')
            with locked_directories([(root / rel).parent for rel in SOURCE_FILES]):
                for relative in SOURCE_FILES:
                    with LockedFile(root / relative, deletable=False) as source:
                        if digest(source.bytes()) != manifest['helperPins'][relative]:
                            raise ValueError('Approved build/recovery source changed')
                yield


def save_new(path, value):
    with path.open('xb') as stream:
        stream.write(json.dumps(value, sort_keys=True, indent=2).encode())


def load_manifest(path, expected_hash):
    require_private(path.parent)
    with locked_directories([path.parent]), LockedFile(path, deletable=False) as handle:
        raw = handle.bytes()
    if digest(raw) != expected_hash:
        raise ValueError('Protection manifest changed')
    manifest = json.loads(raw)
    if manifest.get('schemaVersion') != 2 or manifest.get('mode') != 'reviewed-iap-source-recovery':
        raise ValueError('Final reviewed recovery manifest required; drafts are not executable')
    if set(manifest['helperPins']) != set(SOURCE_FILES):
        raise ValueError('Exact build and recovery source pins required')
    if set(manifest['protection']) != set(ORIGINALS):
        raise ValueError('Protection scope must be exactly nine original files')
    if set(manifest['expectedAbsent']) != set((*TEMPORARIES, *COLLISIONS)):
        raise ValueError('Expected absence scope changed')
    originals = {}
    with locked_directories([path.parent]):
        for relative, entry in manifest['protection'].items():
            name = entry['snapshot']
            if Path(name).name != name:
                raise ValueError('Snapshot must be in the private protection directory')
            with LockedFile(path.parent / name, deletable=False) as handle:
                raw = handle.bytes()
            if digest(raw) != entry['sha256'] or len(raw) != entry['length']:
                raise ValueError('Original backup changed')
            originals[relative] = raw
    return manifest, originals


def _preflight(root, path, manifest):
    require_editor_closed(root)
    for name in ('build-run-once.marker', 'after-build.private.json', 'restoration.private.json'):
        if (path.parent / name).exists():
            raise ValueError('Run/recovery evidence already exists; no retry or overwrite')
    expected = {rel: (entry['identity'], entry['sha256'])
                for rel, entry in manifest['protection'].items()}
    with verified_files(root, expected):
        pass
    with locked_directories([(root / rel).parent for rel in manifest['expectedAbsent']]):
        if any((root / rel).exists() for rel in manifest['expectedAbsent']):
            raise ValueError('Expected output or temporary path is occupied')


def preflight(root, path, manifest, expected_hash, start=False):
    with recovery_scope(root, path, manifest, expected_hash):
        _preflight(root, path, manifest)
        if start:
            with (path.parent / 'build-run-once.marker').open('xb'):
                pass


def capture(root, path, manifest, expected_hash):
    with recovery_scope(root, path, manifest, expected_hash):
        _capture(root, path, manifest, expected_hash)


def _capture(root, path, manifest, expected_hash):
    require_editor_closed(root)
    after = {}
    # No restoration here: replaced regular identities are evidence for review.
    with locked_directories([(root / rel).parent for rel in (*ORIGINALS, *TEMPORARIES)]):
        for index, relative in enumerate((*ORIGINALS, *TEMPORARIES)):
            target = root / relative
            if not target.exists():
                if relative in ORIGINALS:
                    raise ValueError('Protected original missing; preserve and inspect')
                after[relative] = {'absent': True}
                continue
            with LockedFile(target, deletable=False) as handle:
                raw = handle.bytes()
                record = {'identity': list(handle.identity), 'sha256': digest(raw), 'length': len(raw)}
            archive = 'after-' + str(index) + '.snapshot'
            with (path.parent / archive).open('xb') as stream:
                stream.write(raw)
            record['archive'] = archive
            if relative in ORIGINALS:
                original = manifest['protection'][relative]
                record['rawChanged'] = record['sha256'] != original['sha256']
                record['identityChanged'] = record['identity'] != original['identity']
            after[relative] = record
    save_new(path.parent / 'after-build.private.json', {
        'manifestSha256': expected_hash, 'projectRoot': manifest['projectRoot'],
        'projectRootIdentity': manifest['projectRootIdentity'], 'after': after,
        'editorClosed': True, 'independentDeltaReviewRequired': True,
        'restored': False, 'releaseReady': False,
    })


def restore(root, path, manifest, originals, reviewed_delta_hash, expected_hash):
    with recovery_scope(root, path, manifest, expected_hash):
        _restore(root, path, manifest, originals, reviewed_delta_hash, expected_hash)


def _restore(root, path, manifest, originals, reviewed_delta_hash, expected_hash):
    require_editor_closed(root)
    delta_path = path.parent / 'after-build.private.json'
    with locked_directories([path.parent]), LockedFile(delta_path, deletable=False) as delta_handle:
        raw = delta_handle.bytes()
        if digest(raw) != reviewed_delta_hash:
            raise ValueError('Reviewed actual delta changed')
        delta = json.loads(raw)
        if (delta['manifestSha256'] != expected_hash or
                delta['projectRoot'] != manifest['projectRoot'] or
                delta['projectRootIdentity'] != manifest['projectRootIdentity']):
            raise ValueError('Actual delta belongs to another protection manifest')
        after = delta['after']
        if set(after) != set((*ORIGINALS, *TEMPORARIES)):
            raise ValueError('Unexpected recovery scope')
        expected = {rel: (entry['identity'], entry['sha256'])
                    for rel, entry in after.items() if not entry.get('absent')}
        absent = [rel for rel, entry in after.items() if entry.get('absent')]
        if any(rel in ORIGINALS for rel in absent):
            raise ValueError('Original missing in reviewed delta')
        with locked_directories([(root / rel).parent for rel in absent]):
            if any((root / rel).exists() for rel in absent):
                raise ValueError('Absent temporary appeared after review')
            # Acquire/check all current identities and bytes before the first write.
            with verified_files(root, expected, writable=True) as handles:
                for relative in ORIGINALS:
                    original_sha = manifest['protection'][relative]['sha256']
                    if after[relative]['sha256'] != original_sha:
                        handles[relative].restore_bytes(originals[relative], original_sha)
                for relative in TEMPORARIES:
                    if relative in handles:
                        handles[relative].delete()
    require_editor_closed(root)
    identity_changes = []
    with locked_directories([(root / rel).parent for rel in ORIGINALS]):
        for relative in ORIGINALS:
            with LockedFile(root / relative, deletable=False) as handle:
                if digest(handle.bytes()) != manifest['protection'][relative]['sha256']:
                    raise ValueError('Restored content mismatch')
                if list(handle.identity) != manifest['protection'][relative]['identity']:
                    identity_changes.append(relative)
    if any((root / rel).exists() for rel in TEMPORARIES):
        raise ValueError('Temporary configuration still exists')
    save_new(path.parent / 'restoration.private.json', {
        'reviewedDeltaSha256': reviewed_delta_hash, 'originalRawRestored': True,
        'temporariesAbsent': True, 'identityDifferences': identity_changes,
        'originalIdentitiesFullyRestored': not identity_changes,
        'editorClosed': True, 'releaseReady': False,
    })


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('preflight', 'start', 'capture', 'restore'))
    parser.add_argument('--project', required=True, type=Path)
    parser.add_argument('--manifest', required=True, type=Path)
    parser.add_argument('--manifest-sha256', required=True)
    parser.add_argument('--reviewed-delta-sha256')
    args = parser.parse_args()
    # Do not resolve away junctions before the ancestor handle guard sees them.
    root = args.project.absolute()
    path = args.manifest.absolute()
    manifest, originals = load_manifest(path, args.manifest_sha256)
    if args.action in ('preflight', 'start'):
        preflight(root, path, manifest, args.manifest_sha256, start=args.action == 'start')
    elif args.action == 'capture':
        capture(root, path, manifest, args.manifest_sha256)
    else:
        if not args.reviewed_delta_sha256:
            parser.error('restore requires an independently reviewed actual delta SHA256')
        restore(root, path, manifest, originals, args.reviewed_delta_sha256, args.manifest_sha256)
    print(json.dumps({'action': args.action, 'completed': True, 'releaseReady': False}))


if __name__ == '__main__':
    main()
