"""Verify an isolated debug AAB and generate/sign/check device-targeted split APKs.

No upload, installation, store connection or operational keystore access. Outputs
contain licensed game assets: keep the output directory private. Use a new output
directory on each run to preserve earlier evidence.
"""
import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from install_bundletool import DEST, SHA256, VERSION
from verify_native_alignment import inspect_apk

ROOT = Path(__file__).resolve().parents[2]
ANDROID = '{http://schemas.android.com/apk/res/android}'
APP_ID = 'com.AeDeong.MonsterTamer.revival'


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def validate_manifest(xml):
    manifest = ET.fromstring(xml)
    sdk = manifest.find('uses-sdk')
    app = manifest.find('application')
    if (manifest.get('package') != APP_ID or manifest.get(ANDROID + 'versionCode') != '26'
            or manifest.get(ANDROID + 'versionName') != '1.0.5'
            or sdk is None or sdk.get(ANDROID + 'minSdkVersion') != '24'
            or sdk.get(ANDROID + 'targetSdkVersion') != '36'
            or app is None or app.get(ANDROID + 'debuggable') != 'true'):
        raise ValueError('Expected isolated debug application, unchanged version and min24/target36')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--aab', type=Path, default=ROOT / 'Build/revival/Tamer-development.aab')
    parser.add_argument('--output', type=Path, default=ROOT / 'Build/revival/bundle-check')
    parser.add_argument('--device-spec', type=Path, help='Optional bundletool device JSON; default is synthetic ARM64 API36')
    parser.add_argument('--android-player', type=Path, default=Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer'))
    args = parser.parse_args()
    if not DEST.exists() or digest(DEST) != SHA256:
        raise ValueError('Run install_bundletool.py; pinned bundletool is missing or changed')
    if args.output.exists():
        raise ValueError('Output exists; select a new private directory to preserve earlier evidence')
    args.output.mkdir(parents=True)
    java = args.android_player / 'OpenJDK/bin/java.exe'
    build_tools = args.android_player / 'SDK/build-tools/36.0.0'

    def run(command):
        return subprocess.check_output([str(value) for value in command], stderr=subprocess.STDOUT,
                                       text=True, encoding='utf-8', errors='replace')

    def bundle(*command):
        return run([java, '-Duser.language=en', '-jar', DEST, *command])

    bundle('validate', '--bundle=' + str(args.aab))
    with zipfile.ZipFile(args.aab) as archive:
        bundle_abis = sorted({name.split('/')[2] for name in archive.namelist()
                              if name.startswith('base/lib/') and name.endswith('.so')})
    if bundle_abis != ['arm64-v8a']:
        raise ValueError('AAB must contain ARM64 only')
    manifest = bundle('dump', 'manifest', '--bundle=' + str(args.aab), '--module=base')
    validate_manifest(manifest)
    certificate = run([args.android_player / 'OpenJDK/bin/keytool.exe', '-J-Duser.language=en', '-printcert', '-jarfile', args.aab])
    if 'CN=Android Debug' not in certificate:
        raise ValueError('AAB is not signed with the expected debug identity')
    signature = run([args.android_player / 'OpenJDK/bin/jarsigner.exe', '-J-Duser.language=en', '-verify', args.aab])
    if 'jar verified.' not in signature:
        raise ValueError('AAB signature verification failed')
    if re.search(r'unsigned entries|disabled algorithm', signature, re.I):
        raise ValueError('AAB contains unsigned entries or disabled signature algorithms')
    coverage = json.loads(run([java, ROOT / 'tools/revival/VerifyAabSignature.java', args.aab]))
    config = bundle('dump', 'config', '--bundle=' + str(args.aab))
    (args.output / 'bundle-config.json').write_text(config, encoding='utf-8')
    spec = json.loads(args.device_spec.read_text(encoding='utf-8')) if args.device_spec else dict(
        supportedAbis=['arm64-v8a'], supportedLocales=['en'], screenDensity=420, sdkVersion=36)
    spec_path = args.output / 'device-spec.json'
    spec_path.write_text(json.dumps(spec, indent=2), encoding='utf-8')
    # This newly generated local-only key is unrelated to the production key.
    key = args.output / 'revival-debug.keystore'
    run([args.android_player / 'OpenJDK/bin/keytool.exe', '-genkeypair', '-keystore', key,
         '-storepass', 'android', '-keypass', 'android', '-alias', 'androiddebugkey',
         '-dname', 'CN=Android Debug,O=Android,C=US', '-keyalg', 'RSA', '-validity', '3650'])
    key_certificate = run([args.android_player / 'OpenJDK/bin/keytool.exe', '-J-Duser.language=en',
                           '-list', '-v', '-keystore', key, '-storepass', 'android', '-alias', 'androiddebugkey'])
    key_sha256 = re.search(r'SHA256:\s*([0-9A-Fa-f:]+)', key_certificate).group(1).replace(':', '').lower()
    apks = args.output / 'Tamer-development.apks'
    bundle('build-apks', '--bundle=' + str(args.aab), '--output=' + str(apks),
           '--device-spec=' + str(spec_path), '--ks=' + str(key), '--ks-pass=pass:android',
           '--key-pass=pass:android', '--ks-key-alias=androiddebugkey')
    splits_dir = args.output / 'splits'
    bundle('extract-apks', '--apks=' + str(apks), '--device-spec=' + str(spec_path),
           '--output-dir=' + str(splits_dir))
    splits = []
    native_reports = []
    for path in sorted(splits_dir.glob('*.apk')):
        signed = run([java, '-jar', build_tools / 'lib/apksigner.jar', 'verify', '--print-certs', path])
        if 'CN=Android Debug' not in signed:
            raise ValueError('Split debug signature missing')
        signer_sha256 = re.search(r'Signer #1 certificate SHA-256 digest:\s*([0-9a-f]+)', signed).group(1)
        if signer_sha256 != key_sha256:
            raise ValueError('Split signer differs from this run\'s generated debug key')
        run([build_tools / 'zipalign.exe', '-c', '-P', '16', '4', path])
        with zipfile.ZipFile(path) as archive:
            abis = sorted({name.split('/')[1] for name in archive.namelist() if name.startswith('lib/') and name.endswith('.so')})
        record = dict(file=path.name, bytes=path.stat().st_size, sha256=digest(path), signatureVerified=True, zipalignExitCode=0, abis=abis)
        if abis:
            if abis != ['arm64-v8a']:
                raise ValueError('Split must contain ARM64 only')
            native = inspect_apk(path, ('arm64-v8a',))
            (args.output / (path.stem + '-native.json')).write_text(json.dumps(native, indent=2), encoding='utf-8')
            if not native['loadZipChecksPassed']:
                raise ValueError('Split LOAD/ZIP check failed; inspect private native report')
            record.update(loadZipChecksPassed=True, relroChecksPassed=native['relroChecksPassed'],
                          libraries=native['nativeLibraryCount'])
            native_reports.append(native)
        splits.append(record)
    if not native_reports:
        raise ValueError('No native split found for this native application')
    result = dict(schema=1, bundletoolVersion=VERSION, bundletoolSha256=SHA256,
        aab=dict(bytes=args.aab.stat().st_size, sha256=digest(args.aab), debugSignatureVerified=True),
        aabSignatureCoverage=coverage,
        jarInputStreamOrderingWarning='internal inconsistencies' in signature,
        aabAbis=bundle_abis, splitSignerSha256=key_sha256,
        bundleValidationPassed=True, deviceSpec=spec, deviceSpecIsSynthetic=not bool(args.device_spec),
        configRequests16KB='PAGE_ALIGNMENT_16K' in config, apksSha256=digest(apks), splits=splits,
        runtimeVerified=False, uploaded=False)
    (args.output / 'bundle-verification.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
