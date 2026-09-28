"""Verify an isolated sample/control APK, including its actual merged AdMob App ID."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def expected_app_id(variant: str) -> str:
    if variant != 'ump-publisher':
        return 'ca-app-pub-3940256099942544~3347511713'
    def unique_object(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Ambiguous private publisher configuration')
            result[key] = value
        return result
    config = json.loads((ROOT / 'Logs/revival/production-ads-prepared/production-ads.private.json').read_text(encoding='utf-8'), object_pairs_hook=unique_object)
    app_id = config.get('androidAppId', '')
    if (Path(config.get('checkout', '')).resolve() != ROOT.resolve()
            or not re.fullmatch(r'ca-app-pub-[0-9]{16}~[0-9]{10}', app_id)
            or app_id.startswith('ca-app-pub-3940256099942544~')
            or config.get('productionActivationApproved') is not False
            or config.get('regionalReviewApproved') is not False):
        raise ValueError('Private publisher configuration mismatch')
    return app_id


def verify(apk: Path, android_player: Path, variant: str) -> dict:
    expected_id = expected_app_id(variant)
    development = variant != 'control'
    build_tools = android_player / 'SDK/build-tools/36.0.0'
    aapt = str(build_tools / 'aapt2.exe')
    badging = subprocess.check_output([aapt, 'dump', 'badging', str(apk)], text=True, encoding='utf-8')
    manifest = subprocess.check_output(
        [aapt, 'dump', 'xmltree', '--file', 'AndroidManifest.xml', str(apk)], text=True, encoding='utf-8')
    signing = subprocess.check_output(
        [str(android_player / 'OpenJDK/bin/java.exe'), '-jar', str(build_tools / 'lib/apksigner.jar'),
         'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
    identity = 'com.AeDeong.MonsterTamer.revival.' + {
        'sample': 'ads', 'control': 'adscontrol', 'ump-sample': 'ump', 'ump-publisher': 'umppublisher'}[variant]
    for expected in [f"name='{identity}'", "versionCode='26'", "versionName='1.0.5'", "targetSdkVersion:'36'"]:
        if expected not in badging:
            raise ValueError('Harness metadata mismatch: ' + expected)
    if not re.search(r"(?:minS|s)dkVersion:'25'", badging):
        raise ValueError('Harness minSdk must be 25')
    abi = next(line for line in badging.splitlines() if line.startswith('native-code:'))
    if re.findall(r"'([^']+)'", abi) != ['arm64-v8a']:
        raise ValueError('Harness must remain ARM64 only')
    if ('application-debuggable' in badging) != development:
        raise ValueError('Harness development/release variant mismatch')
    if 'CN=Android Debug' not in signing:
        raise ValueError('Harness requires debug signing')
    entries = re.split(r'(?m)^\s*E: ', manifest)
    app_id_entries = [entry for entry in entries if 'com.google.android.gms.ads.APPLICATION_ID' in entry]
    if len(app_id_entries) != 1 or set(re.findall(r'ca-app-pub-\d+~\d+', app_id_entries[0])) != {expected_id}:
        raise ValueError('Merged manifest must contain exactly the selected AdMob App ID')
    if set(re.findall(r'ca-app-pub-\d+~\d+', manifest)) != {expected_id}:
        raise ValueError('Unexpected AdMob App ID in merged manifest')
    with apk.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(variant=variant, bytes=apk.stat().st_size, sha256=digest, applicationId=identity,
                minSdk=25, targetSdk=36, versionCode=26, version='1.0.5', abi=['arm64-v8a'],
                debuggable=development, debugSignatureVerified=True, sampleAppIdVerified=variant != 'ump-publisher',
                publisherAppIdVerified=variant == 'ump-publisher',
                mobileAdsInitProviderPresent='com.google.android.gms.ads.MobileAdsInitProvider' in manifest,
                adIdPermissionPresent='com.google.android.gms.permission.AD_ID' in manifest)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--variant', choices=['sample', 'control', 'ump-sample', 'ump-publisher'], required=True)
    parser.add_argument('--android-player', type=Path, default=Path(
        'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    result = verify(root / f'Build/revival/Tamer-ads-{args.variant}.apk', args.android_player, args.variant)
    output = root / f'Logs/revival/ads-{args.variant}-verification.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
