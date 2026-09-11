"""Verify actual development APK identity, API levels, ABI and signature with SDK tools."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--android-player', type=Path, default=Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer'))
args = parser.parse_args()
apk = root / 'Build/revival/Tamer-development.apk'
tools = args.android_player / 'SDK/build-tools/36.0.0'
badging = subprocess.check_output([str(tools / 'aapt2.exe'), 'dump', 'badging', str(apk)], text=True, encoding='utf-8')
signing = subprocess.check_output([str(args.android_player / 'OpenJDK/bin/java.exe'), '-jar', str(tools / 'lib/apksigner.jar'), 'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
for expected in ["name='com.AeDeong.MonsterTamer.revival'", "versionCode='26'", "versionName='1.0.5'", "targetSdkVersion:'36'", "native-code: 'arm64-v8a'", 'application-debuggable']:
    if expected not in badging:
        raise ValueError('APK verification failed: ' + expected)
if not re.search(r"(?:minS|s)dkVersion:'24'", badging):
    raise ValueError('APK min SDK must be 24')
if 'CN=Android Debug' not in signing:
    raise ValueError('Expected development debug certificate')
result = dict(apk='Build/revival/Tamer-development.apk', bytes=apk.stat().st_size,
    sha256=hashlib.file_digest(apk.open('rb'), 'sha256').hexdigest(), minSdk=24, targetSdk=36,
    abi=['arm64-v8a'], applicationId='com.AeDeong.MonsterTamer.revival',
    version='1.0.5', versionCode=26, debuggable=True, debugSignatureVerified=True)
out = root / 'Logs/revival/apk-verification.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result))
