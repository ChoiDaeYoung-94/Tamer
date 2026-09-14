"""Read-only metadata and manifest checks for the offline eight-field DataManager game-save probe APK."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
android = Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer')
sdk = android / 'SDK/build-tools/36.0.0'
apk = root / 'Build/revival/Tamer-gamesave-offline.apk'

def aapt(*args):
    return subprocess.check_output([str(sdk / 'aapt2.exe'), *args], text=True, encoding='utf-8')

badging = aapt('dump', 'badging', str(apk))
manifest = aapt('dump', 'xmltree', str(apk), '--file', 'AndroidManifest.xml')
for expected in ["name='com.AeDeong.MonsterTamer.revival.gamesave'", "versionCode='26'",
                 "versionName='1.0.5'", "targetSdkVersion:'36'", 'application-debuggable']:
    if expected not in badging:
        raise ValueError('Unexpected probe metadata: ' + expected)
if not re.search(r"(?:minS|s)dkVersion:'24'", badging):
    raise ValueError('Expected min SDK 24')
native = next(line for line in badging.splitlines() if line.startswith('native-code:'))
if re.findall(r"'([^']+)'", native) != ['arm64-v8a']:
    raise ValueError('Expected ARM64 only')
for forbidden in ['android.permission.INTERNET', 'android.permission.ACCESS_NETWORK_STATE', 'com.android.vending.BILLING', 'com.google.android.gms.permission.AD_ID',
                  'com.google.android.gms.ads.MobileAdsInitProvider']:
    if forbidden in manifest:
        raise ValueError('Forbidden merged manifest entry: ' + forbidden)
signing = subprocess.check_output([str(android / 'OpenJDK/bin/java.exe'), '-jar',
    str(sdk / 'lib/apksigner.jar'), 'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
if 'CN=Android Debug' not in signing:
    raise ValueError('Expected isolated development debug signature')
with apk.open('rb') as stream:
    digest = hashlib.file_digest(stream, 'sha256').hexdigest()
result = {'apk': 'Build/revival/Tamer-gamesave-offline.apk', 'sha256': digest, 'bytes': apk.stat().st_size,
          'applicationId': 'com.AeDeong.MonsterTamer.revival.gamesave', 'versionCode': 26,
          'minSdk': 24, 'targetSdk': 36, 'abi': ['arm64-v8a'], 'debuggable': True,
          'debugSignatureVerified': True, 'billingPermission': False, 'adIdPermission': False,
          'adsInitProvider': False, 'internet': False, 'deviceExecutionVerified': False}
(root / 'Logs/revival/gamesave-apk-verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
