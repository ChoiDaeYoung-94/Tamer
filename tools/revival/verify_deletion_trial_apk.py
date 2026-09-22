"""Verify only the isolated online deletion-trial APK; never accept an older receipt APK."""
import hashlib
import json
import re
import subprocess
from pathlib import Path


def verify(apk, android):
    build = android / 'SDK/build-tools/36.0.0'
    def aapt(*args):
        return subprocess.check_output([str(build/'aapt2.exe'), 'dump', *args, str(apk)], text=True, encoding='utf-8')
    badging = aapt('badging')
    manifest = aapt('xmltree', '--file', 'AndroidManifest.xml')
    signing = subprocess.check_output([str(android/'OpenJDK/bin/java.exe'), '-jar', str(build/'lib/apksigner.jar'),
        'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
    for required in ("name='com.AeDeong.MonsterTamer.deletiontrial'", "versionCode='26'", "versionName='1.0.5'",
                     "targetSdkVersion:'36'", 'application-debuggable', "native-code: 'arm64-v8a'"):
        if required not in badging: raise ValueError('Isolated deletion trial metadata mismatch')
    if not re.search(r"(?:minS|s)dkVersion:'24'", badging) or 'CN=Android Debug' not in signing:
        raise ValueError('Expected min24 and debug signing')
    for required in ('android.permission.INTERNET','android.permission.ACCESS_NETWORK_STATE'):
        if required not in manifest: raise ValueError('Network permission missing')
    for forbidden in ('com.android.vending.BILLING','com.google.android.gms.permission.AD_ID',
                      'com.google.android.gms.ads.MobileAdsInitProvider'):
        if forbidden in manifest: raise ValueError('Forbidden billing/ads initialization')
    for attribute in ('allowBackup','fullBackupContent'):
        if not re.search(r'android:'+attribute+r'[^\n]*=(?:false|\(type 0x12\)0x0)\s*(?:\n|$)',manifest):
            raise ValueError('Backup enabled')
    rules = aapt('xmltree','--file','res/xml/tamer_playerrestore_rules.xml')
    if rules.count('E: exclude') != 18 or 'E: cloud-backup' not in rules or 'E: device-transfer' not in rules:
        raise ValueError('Backup exclusions incomplete')
    return {'applicationId':'com.AeDeong.MonsterTamer.deletiontrial','sha256':hashlib.sha256(apk.read_bytes()).hexdigest(),
        'debugSigning':True,'arm64':True,'network':True,'billing':False,'adsProvider':False,'backup':False}


if __name__ == '__main__':
    root=Path(__file__).resolve().parents[2]
    result=verify(root/'Build/revival/Tamer-deletion-trial.apk',Path(
        'C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer'))
    (root/'Logs/revival/deletion-trial-apk-verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))
