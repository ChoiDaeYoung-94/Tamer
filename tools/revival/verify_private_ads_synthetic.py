"""Read-only verification of the synthetic AAB. Never installs, signs or publishes."""
import argparse
import hashlib
import json
import subprocess
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET
from install_bundletool import DEST, SHA256
from windows_owned_files import LockedFile, locked_directories

ROOT = Path(__file__).resolve().parents[2]
JDK = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin')
ANDROID = '{http://schemas.android.com/apk/res/android}'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def _verify_locked(aab, expected_debug_certificate, expected_certificate_sha):
    if aab.resolve() != ROOT / 'Build/revival/privateads-synthetic.aab' or digest(DEST) != SHA256:
        raise ValueError('Fixed synthetic output and pinned bundletool required')
    # Exact expected certificate DER is exported from the reviewed debug keystore before build.
    expected = digest(expected_debug_certificate)
    if expected != expected_certificate_sha: raise ValueError("Reviewed debug certificate changed")
    def run(*args):
        return subprocess.check_output([str(JDK / 'java.exe'), *map(str, args)],
            stderr=subprocess.PIPE, text=True, encoding='utf-8')
    run('-jar', DEST, 'validate', '--bundle=' + str(aab))
    xml = run('-jar', DEST, 'dump', 'manifest', '--bundle=' + str(aab), '--module=base')
    manifest = ET.fromstring(xml)
    app, sdk = manifest.find('application'), manifest.find('uses-sdk')
    if manifest.get('package') != 'com.tamer.revival.privateads.synthetic' or app is None or sdk is None:
        raise ValueError('Synthetic package required')
    if app.get(ANDROID + 'debuggable', 'false') != 'false' or sdk.get(ANDROID + 'minSdkVersion') != '25' or sdk.get(ANDROID + 'targetSdkVersion') != '36':
        raise ValueError('Non-development min25 target36 required')
    metadata = [item.get(ANDROID + 'value') for item in app.findall('meta-data')
                if item.get(ANDROID + 'name') == 'com.google.android.gms.ads.APPLICATION_ID']
    if metadata != ['ca-app-pub-3940256099942544~3347511713']:
        raise ValueError('Only fixed public sample application is allowed')
    signature = json.loads(run(ROOT / 'tools/revival/VerifyAabSignature.java', aab))
    # Existing Java verifier requires Android Debug subject and signed payload coverage.
    if signature['signerSha256'] != expected or not signature['allPayloadEntriesSigned']:
        raise ValueError('Artifact does not match reviewed debug certificate')
    with zipfile.ZipFile(aab) as archive:
        abis = sorted({name.split('/')[2] for name in archive.namelist()
                       if name.startswith('base/lib/') and name.endswith('.so')})
    if abis != ['arm64-v8a']:
        raise ValueError('ARM64 only required')
    return {'artifactSha256': digest(aab), 'debugCertificateMatched': True,
            'verifiedPayloadEntries': signature['verifiedPayloadEntries'],
            'syntheticPackageAndSampleAppMatched': True, 'abi': abis,
            'productionContractVerified': False, 'binaryVerified': False, 'distributable': False}

def verify(aab, expected_debug_certificate, expected_certificate_sha):
    aab, expected_debug_certificate = aab.absolute(), expected_debug_certificate.absolute()
    with locked_directories([aab.parent, expected_debug_certificate.parent]):
        # Read-only handles permit Java readers, but exclude writes and replacement throughout all checks.
        with LockedFile(aab, deletable=False) as artifact, LockedFile(expected_debug_certificate, deletable=False) as certificate:
            if hashlib.sha256(certificate.bytes()).hexdigest() != expected_certificate_sha:
                raise ValueError('Reviewed certificate digest mismatch')
            before = hashlib.sha256(artifact.bytes()).hexdigest()
            result = _verify_locked(aab, expected_debug_certificate, expected_certificate_sha)
            if result['artifactSha256'] != before or hashlib.sha256(artifact.bytes()).hexdigest() != before:
                raise ValueError('Artifact bytes changed')
            return result

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--debug-certificate', type=Path, required=True)
    parser.add_argument('--debug-certificate-sha256', required=True)
    args = parser.parse_args()
    print(json.dumps(verify(ROOT / 'Build/revival/privateads-synthetic.aab', args.debug_certificate, args.debug_certificate_sha256)))

if __name__ == '__main__':
    main()
