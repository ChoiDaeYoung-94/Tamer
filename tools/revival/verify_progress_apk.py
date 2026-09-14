"""Read-only metadata and manifest checks for the separate progress probe APK."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
android = Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer')
sdk = android / 'SDK/build-tools/36.0.0'
apk = root / 'Build/revival/Tamer-progress.apk'

def aapt(*args):
    return subprocess.check_output([str(sdk / 'aapt2.exe'), *args], text=True, encoding='utf-8')

badging = aapt('dump', 'badging', str(apk))
manifest = aapt('dump', 'xmltree', str(apk), '--file', 'AndroidManifest.xml')
for expected in ["name='com.AeDeong.MonsterTamer.revival.progress'", "versionCode='26'",
                 "versionName='1.0.5'", "targetSdkVersion:'36'", 'application-debuggable']:
    if expected not in badging:
        raise ValueError('Unexpected probe metadata: ' + expected)
if not re.search(r"(?:minS|s)dkVersion:'24'", badging):
    raise ValueError('Expected min SDK 24')
native = next(line for line in badging.splitlines() if line.startswith('native-code:'))
if re.findall(r"'([^']+)'", native) != ['arm64-v8a']:
    raise ValueError('Expected ARM64 only')
for forbidden in ['com.android.vending.BILLING', 'com.google.android.gms.permission.AD_ID',
                  'com.google.android.gms.ads.MobileAdsInitProvider']:
    if forbidden in manifest:
        raise ValueError('Forbidden merged manifest entry: ' + forbidden)
if 'android.permission.INTERNET' not in manifest:
    raise ValueError('Explicit test network capability missing')
signing = subprocess.check_output([str(android / 'OpenJDK/bin/java.exe'), '-jar',
    str(sdk / 'lib/apksigner.jar'), 'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
if 'CN=Android Debug' not in signing:
    raise ValueError('Expected isolated development debug signature')
with apk.open('rb') as stream:
    digest = hashlib.file_digest(stream, 'sha256').hexdigest()
result = {'apk': 'Build/revival/Tamer-progress.apk', 'sha256': digest, 'bytes': apk.stat().st_size,
          'applicationId': 'com.AeDeong.MonsterTamer.revival.progress', 'versionCode': 26,
          'minSdk': 24, 'targetSdk': 36, 'abi': ['arm64-v8a'], 'debuggable': True,
          'debugSignatureVerified': True, 'billingPermission': False, 'adIdPermission': False,
          'adsInitProvider': False, 'internet': True, 'deviceExecutionVerified': False}
(root / 'Logs/revival/progress-apk-verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
