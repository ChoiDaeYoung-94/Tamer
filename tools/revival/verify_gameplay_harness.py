"""Verify the isolated gameplay APK identity and absence of network/billing permissions."""
import hashlib
import argparse
import json
import re
import subprocess
from pathlib import Path


def verify(apk, android, variant='development'):
    if variant not in ('development', 'photo'):
        raise ValueError('Unknown gameplay variant')
    build_tools = android / 'SDK/build-tools/36.0.0'
    aapt = str(build_tools / 'aapt2.exe')
    badging = subprocess.check_output([aapt, 'dump', 'badging', str(apk)], text=True, encoding='utf-8')
    manifest = subprocess.check_output([aapt, 'dump', 'xmltree', '--file', 'AndroidManifest.xml', str(apk)], text=True, encoding='utf-8')
    signing = subprocess.check_output([str(android / 'OpenJDK/bin/java.exe'), '-jar', str(build_tools / 'lib/apksigner.jar'), 'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
    identity = 'com.AeDeong.MonsterTamer.revival.gameplay'
    for expected in [f"name='{identity}'", "versionCode='26'", "versionName='1.0.5'", "targetSdkVersion:'36'"]:
        if expected not in badging:
            raise ValueError('Gameplay APK metadata mismatch: ' + expected)
    if ('application-debuggable' in badging) != (variant == 'development'):
        raise ValueError('Gameplay development/photo variant mismatch')
    if not re.search(r"(?:minS|s)dkVersion:'24'", badging):
        raise ValueError('Expected min SDK 24')
    abi = next(line for line in badging.splitlines() if line.startswith('native-code:'))
    if re.findall(r"'([^']+)'", abi) != ['arm64-v8a'] or 'CN=Android Debug' not in signing:
        raise ValueError('Expected ARM64 and debug signing')
    blocked = ['android.permission.INTERNET', 'android.permission.ACCESS_NETWORK_STATE',
               'com.android.vending.BILLING', 'com.google.android.gms.permission.AD_ID',
               'com.google.android.gms.ads.MobileAdsInitProvider']
    if any(value in manifest for value in blocked):
        raise ValueError('Offline manifest still contains a forbidden permission/provider')
    with apk.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(variant=variant, applicationId=identity, bytes=apk.stat().st_size, sha256=digest,
                minSdk=24, targetSdk=36, versionCode=26, version='1.0.5', abi=['arm64-v8a'],
                debuggable=variant == 'development', debugSignatureVerified=True, internetPermission=False,
                billingPermission=False, adIdPermission=False, mobileAdsInitProvider=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--variant', choices=['development', 'photo'], default='development')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    suffix = '-photo' if args.variant == 'photo' else ''
    result = verify(root / f'Build/revival/Tamer-gameplay{suffix}.apk', Path(
        'C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer'), args.variant)
    output = root / f'Logs/revival/gameplay{suffix}-apk-verification.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
