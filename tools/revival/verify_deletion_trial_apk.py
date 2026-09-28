"""Verify only the isolated online deletion-trial APK; never accept an older receipt APK."""
import hashlib
import json
import re
import subprocess
from pathlib import Path


def verify(apk, android, application_id='com.AeDeong.MonsterTamer.deletiontrial'):
    if application_id not in ('com.AeDeong.MonsterTamer.deletiontrial',
                               'com.AeDeong.MonsterTamer.deletiontrial.online'):
        raise ValueError('Unapproved trial application ID')
    build = android / 'SDK/build-tools/36.0.0'
    def aapt(*args):
        return subprocess.check_output([str(build/'aapt2.exe'), 'dump', *args, str(apk)], text=True, encoding='utf-8')
    badging = aapt('badging')
    manifest = aapt('xmltree', '--file', 'AndroidManifest.xml')
    signing = subprocess.check_output([str(android/'OpenJDK/bin/java.exe'), '-jar', str(build/'lib/apksigner.jar'),
        'verify', '--print-certs', str(apk)], text=True, encoding='utf-8')
    for required in ("name='" + application_id + "'", "versionCode='26'", "versionName='1.0.5'",
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
    return {'applicationId':application_id,'sha256':hashlib.sha256(apk.read_bytes()).hexdigest(),
        'debugSigning':True,'arm64':True,'network':True,'billing':False,'adsProvider':False,'backup':False}


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--online', action='store_true')
    args = parser.parse_args()
    root=Path(__file__).resolve().parents[2]
    name = 'deletion-online-trial' if args.online else 'deletion-trial'
    identity = 'com.AeDeong.MonsterTamer.deletiontrial' + ('.online' if args.online else '')
    result=verify(root/('Build/revival/Tamer-' + name + '.apk'),Path(
        'C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer'),identity)
    (root/('Logs/revival/' + name + '-apk-verification.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))
