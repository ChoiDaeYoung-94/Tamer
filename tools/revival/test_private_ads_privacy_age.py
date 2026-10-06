"""Pure age-change privacy ownership checks. No native SDK or Unity launch."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


def run(output):
    if not output.is_absolute() or not output.is_relative_to(ROOT / 'Logs'):
        raise ValueError('Private Logs output required')
    if not subprocess.check_output(['git', '-C', str(ROOT), 'check-ignore', '--', str(output)]).strip():
        raise ValueError('Ignored output required')
    output.mkdir(parents=True, exist_ok=False)
    sources = ['Assets/Scripts/Advertising/AdConsentGate.cs', 'Assets/Scripts/Advertising/LocalAgeChoice.cs',
               'tools/revival/private_ads_release/PrivateAdsPrivacyAgeChecks.cs']
    refs = EDITOR / 'UnityReferenceAssemblies/unity-4.8-api'
    exe = output / 'privacy-age-checks.exe'
    lines = ['-nologo', '-langversion:9.0', '-target:exe', '-out:"' + str(exe) + '"']
    lines += ['-r:"' + str(refs / n) + '"' for n in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
    lines += ['"' + str(ROOT / p) + '"' for p in sources]
    rsp = output / 'compile.rsp'; rsp.write_text('\n'.join(lines))
    manifest = {'sourceHead': subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD']).decode().strip(),
        'sourceSha256': {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in sources},
        'unityBuilds': 0, 'sdkCalls': 0, 'nativeForms': 0}
    (output / 'manifest.private.json').write_text(json.dumps(manifest, indent=2))
    with (output / 'compile.private.log').open('xb') as log:
        compiled = subprocess.run([str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'), '@' + str(rsp)],
                                  cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    (output / 'compile-result.private.json').write_text(json.dumps({'returnCode': compiled.returncode}))
    if compiled.returncode: raise ValueError('Pure compile failed; preserve')
    with (output / 'execution.private.log').open('xb') as log:
        result = subprocess.run([str(EDITOR / 'MonoBleedingEdge/bin/mono.exe'), str(exe)],
                                cwd=output, stdout=log, stderr=subprocess.STDOUT)
    parsed = json.loads((output / 'execution.private.log').read_bytes())
    (output / 'result.private.json').write_text(json.dumps(parsed, indent=2))
    if result.returncode or parsed['failed'] or not all(parsed['checks'].values()):
        raise ValueError('Pure checks failed; preserve')
    return {'purePrivacyAgeChecksPassed': len(parsed['checks']), 'nativeSdkCalls': 0,
            'runtimePrivacyVerified': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try: print(json.dumps(run(args.output)))
    except Exception: parser.exit(1, 'Privacy age checks stopped; preserve local evidence.\n')
