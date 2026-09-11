"""Read-only source/AAB preflight. Does not build, sign, upload, or approve a release."""
import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET
from install_bundletool import DEST, SHA256
from verify_native_alignment import inspect_elf

ROOT = Path(__file__).resolve().parents[2]
ANDROID = '{http://schemas.android.com/apk/res/android}'
APP_ID = 'com.AeDeong.MonsterTamer'
UNVERIFIED = ['Play Console active upload certificate and highest published version code',
              'Production account continuity, changed-progress writes, purchase restore and receipt authority',
              'Privacy/deletion endpoint, retention decisions and published declarations',
              'Families operating settings and creative compliance',
              'Actual ARM64 16KB runtime and device-targeted split validation',
              'Internal-track upload and submission approval']
SOURCE_INPUTS = ['ProjectSettings/ProjectSettings.asset', 'ProjectSettings/ProjectVersion.txt',
                 'ProjectSettings/EditorBuildSettings.asset', 'Assets/Resources/IAPProductCatalog.json']


def ensure_output_safe(output, inputs):
    output=Path(output).resolve()
    for source in inputs:
        source=Path(source).resolve()
        if output == source or (output.exists() and source.exists() and output.samefile(source)):
            raise ValueError('Output collides with an input file; original bytes preserved')


def inspect_bundle_libraries(aab):
    libraries=[]
    with zipfile.ZipFile(aab) as archive:
        names=archive.namelist()
        if len(names)!=len(set(names)): raise ValueError('Duplicate AAB ZIP entries')
        for name in names:
            if not name.endswith('.so'): continue
            if not re.fullmatch(r'[^/]+/lib/arm64-v8a/[^/]+\.so',name):
                raise ValueError('Unexpected native library path or ABI')
            result=inspect_elf(archive.read(name))
            if result['machine'] != 183 or result['bits'] != 64 or result['byteOrder'] != 'little':
                raise ValueError('Native ELF does not match little-endian ARM64')
            libraries.append(dict(path=name,loadPassed=result['passed'],relroPassed=result['relroChecksPassed']))
    if not libraries: raise ValueError('No ARM64 native libraries')
    return libraries


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def source_checks(root):
    settings = (root / 'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8-sig')
    def scalar(name):
        match = re.search(r'^  '+re.escape(name)+r':\s*([^\r\n]*)$', settings, re.M)
        return match.group(1).strip() if match else None
    def android_value(name):
        match = re.search(r'^  '+re.escape(name)+r':\r?\n((?:    [^\n]*\n?)*)', settings, re.M)
        value = re.search(r'^    Android:\s*([^\r\n]*)', match.group(1), re.M) if match else None
        return value.group(1).strip() if value else None
    catalog = json.loads((root / 'Assets/Resources/IAPProductCatalog.json').read_text(encoding='utf-8-sig'))
    scenes = re.findall(r'^  - enabled: 1\r?\n    path: (.+)$', (root / 'ProjectSettings/EditorBuildSettings.asset').read_text(), re.M)
    defines = set((android_value('scriptingDefineSymbols') or '').split(';'))
    checks = {
        'unityPinned': 'm_EditorVersion: 6000.0.81f1' in (root/'ProjectSettings/ProjectVersion.txt').read_text(),
        'productionIdPreserved': android_value('applicationIdentifier') == APP_ID,
        'min24': scalar('AndroidMinSdkVersion') == '24',
        'target36': scalar('AndroidTargetSdkVersion') == '36',
        'arm64Only': scalar('AndroidTargetArchitectures') == '2',
        'il2cpp': android_value('scriptingBackend') == '1',
        'noGlobalHarnessDefines': not any(d.startswith('TAMER_') for d in defines),
        'iapAutoInitExplicitlyDisabled': catalog.get('enableCodelessAutoInitialization') is False and catalog.get('enableUnityGamingServicesAutoInitialization') is False,
        'loginFirstScene': bool(scenes) and scenes[0].strip() == 'Assets/Scenes/Login.unity',
        'noLegacyBuildMarkers': not any((root/'Build'/name).exists() for name in ('AOSSettingAPK.txt','AOSSettingAAB.txt','checkedBuilding.txt')),
    }
    return dict(schema=1, mode='source', checks=checks, sourceChecksPassed=all(checks.values()),
                currentVersionCode=scalar('AndroidBundleVersionCode'), releaseReady=False,
                note='Source settings are not the final AAB manifest/signature. No version or signing settings changed.',
                unverified=UNVERIFIED)


def validate_manifest(xml, version_code, published_max):
    if version_code <= published_max or published_max < 0:
        raise ValueError('Candidate version must exceed the explicitly supplied published maximum')
    manifest = ET.fromstring(xml)
    sdk, app = manifest.find('uses-sdk'), manifest.find('application')
    if (manifest.get('package') != APP_ID or manifest.get(ANDROID+'versionCode') != str(version_code)
            or sdk is None or sdk.get(ANDROID+'minSdkVersion') != '24' or sdk.get(ANDROID+'targetSdkVersion') != '36'
            or app is None or app.get(ANDROID+'debuggable', 'false') != 'false'
            or app.get(ANDROID+'testOnly', 'false') != 'false'):
        raise ValueError('Expected production package, reviewed version, min24/target36 and non-debug/non-test application')
    return {'versionCode':version_code,'minSdk':24,'targetSdk':36,'debuggable':False,'testOnly':False}


def candidate_checks(aab, version_code, published_max, certificate, bundletool, java):
    if not re.fullmatch(r'[0-9a-fA-F]{64}', certificate):
        raise ValueError('Expected certificate SHA-256, not a keystore or password')
    if digest(bundletool) != SHA256:
        raise ValueError('Pinned bundletool digest mismatch')
    def run(args):
        return subprocess.check_output([str(x) for x in args], text=True, encoding='utf-8', stderr=subprocess.PIPE, timeout=180)
    run([java,'-jar',bundletool,'validate','--bundle='+str(aab)])
    manifest = run([java,'-jar',bundletool,'dump','manifest','--bundle='+str(aab),'--module=base'])
    metadata = validate_manifest(manifest,version_code,published_max)
    signature=json.loads(run([java,ROOT/'tools/revival/VerifyAabSignature.java',aab,'--release-cert-sha256',certificate]))
    libraries=inspect_bundle_libraries(aab)
    return dict(schema=1,mode='aab',artifactSha256=digest(aab),manifest=metadata,signature=signature,
                libraries=libraries,artifactChecksPassed=all(x['loadPassed'] and x['relroPassed'] for x in libraries),
                releaseReady=False,unverified=UNVERIFIED,
                note='Supplied certificate/version claims still require Console confirmation. Static RELRO failure is not proof of runtime crash.')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'Logs/revival/release-preflight.json')
    commands=parser.add_subparsers(dest='mode',required=True)
    commands.add_parser('source')
    candidate=commands.add_parser('aab')
    candidate.add_argument('--aab',type=Path,required=True)
    candidate.add_argument('--version-code',type=int,required=True)
    candidate.add_argument('--published-max-code',type=int,required=True)
    candidate.add_argument('--upload-cert-sha256',required=True)
    candidate.add_argument('--bundletool',type=Path,default=DEST)
    candidate.add_argument('--java',type=Path,default=Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe'))
    args=parser.parse_args()
    inputs=[ROOT/path for path in SOURCE_INPUTS]+[Path(__file__),ROOT/'tools/revival/VerifyAabSignature.java',
        ROOT/'tools/revival/install_bundletool.py',ROOT/'tools/revival/verify_native_alignment.py']
    if args.mode=='aab': inputs += [args.aab,args.bundletool,args.java]
    ensure_output_safe(args.output,inputs)
    result=source_checks(ROOT) if args.mode=='source' else candidate_checks(args.aab,args.version_code,args.published_max_code,args.upload_cert_sha256,args.bundletool,args.java)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result))
    return 0 if result.get('sourceChecksPassed',result.get('artifactChecksPassed',False)) else 1


if __name__=='__main__': raise SystemExit(main())
