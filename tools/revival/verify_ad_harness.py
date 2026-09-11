"""Verify an isolated sample/control APK, including its actual merged AdMob App ID."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path


def verify(apk: Path, android_player: Path, variant: str) -> dict:
    build_tools = android_player / 'SDK/build-tools/36.0.0'
    aapt = str(build_tools / 'aapt2.exe')
    badging = subprocess.check_output([aapt, 'dump', 'badging', str(apk)], text=True, encoding='utf-8')
    manifest = subprocess.check_output(
        [aapt, 'dump', 'xmltree', '--file', 'AndroidManifest.xml', str(apk)], text=True, encoding='utf-8')
    signing = subprocess.check_output(
        [str(android_player / 'OpenJDK/bin/java.exe'), '-jar', str(build_tools / 'lib/apksigner.jar'),
         'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
    identity = 'com.AeDeong.MonsterTamer.revival.' + ('ads' if variant == 'sample' else 'adscontrol')
    for expected in [f"name='{identity}'", "versionCode='26'", "versionName='1.0.5'", "targetSdkVersion:'36'"]:
        if expected not in badging:
            raise ValueError('Harness metadata mismatch: ' + expected)
    if not re.search(r"(?:minS|s)dkVersion:'24'", badging):
        raise ValueError('Harness minSdk must be 24')
    abi = next(line for line in badging.splitlines() if line.startswith('native-code:'))
    if re.findall(r"'([^']+)'", abi) != ['arm64-v8a']:
        raise ValueError('Harness must remain ARM64 only')
    if ('application-debuggable' in badging) != (variant == 'sample'):
        raise ValueError('Harness development/release variant mismatch')
    if 'CN=Android Debug' not in signing:
        raise ValueError('Harness requires debug signing')
    entries = re.split(r'(?m)^\s*E: ', manifest)
    app_id_entries = [entry for entry in entries if 'com.google.android.gms.ads.APPLICATION_ID' in entry]
    if len(app_id_entries) != 1 or 'ca-app-pub-3940256099942544~3347511713' not in app_id_entries[0]:
        raise ValueError('Merged manifest must contain exactly the official sample AdMob App ID')
    if re.findall(r'ca-app-pub-\d+~\d+', manifest) and set(re.findall(r'ca-app-pub-\d+~\d+', manifest)) != {'ca-app-pub-3940256099942544~3347511713'}:
        raise ValueError('Unexpected AdMob App ID in merged manifest')
    with apk.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(variant=variant, bytes=apk.stat().st_size, sha256=digest, applicationId=identity,
                minSdk=24, targetSdk=36, versionCode=26, version='1.0.5', abi=['arm64-v8a'],
                debuggable=variant == 'sample', debugSignatureVerified=True, sampleAppIdVerified=True,
                mobileAdsInitProviderPresent='com.google.android.gms.ads.MobileAdsInitProvider' in manifest,
                adIdPermissionPresent='com.google.android.gms.permission.AD_ID' in manifest)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--variant', choices=['sample', 'control'], required=True)
    parser.add_argument('--android-player', type=Path, default=Path(
        'C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    result = verify(root / f'Build/revival/Tamer-ads-{args.variant}.apk', args.android_player, args.variant)
    output = root / f'Logs/revival/ads-{args.variant}-verification.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
