"""Run the pure privacy restart fixture without launching Unity or a native SDK."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


def run(output, context_only=False, native_context_only=False):
    from private_ads_evidence import create_private_directory
    output = output.resolve()
    if not output.is_relative_to(ROOT / 'Logs') or output.exists():
        raise ValueError('New evidence directory inside Logs required')
    if not subprocess.check_output(['git', '-C', str(ROOT), 'check-ignore', '--', str(output)]).strip():
        raise ValueError('Ignored output required')
    output.parent.mkdir(parents=True, exist_ok=True)
    create_private_directory(output)
    sources = ['Assets/Scripts/Advertising/AdConsentGate.cs', 'Assets/Scripts/Advertising/LocalAgeChoice.cs',
               'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs',
               'tools/revival/private_ads_release/PrivateAdsPrivacyRestartChecks.cs']
    exe = output / 'privacy-restart-checks.exe'
    refs = EDITOR / 'UnityReferenceAssemblies/unity-4.8-api'
    lines = ['-nologo', '-langversion:9.0', '-target:exe', '-out:"' + str(exe) + '"']
    if native_context_only:
        lines += ['-define:TAMER_PRIVACY_MANAGER_HARNESS,TAMER_AD_TEST_HARNESS,TAMER_UMP_ONLY_HARNESS,TAMER_UMP_PUBLISHER_HARNESS']
    lines += ['-r:"' + str(refs / name) + '"' for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
    lines += ['"' + str(ROOT / source) + '"' for source in sources]
    rsp = output / 'compile.rsp'
    rsp.write_text('\n'.join(lines))
    manifest = {'sourceHead': subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD']).decode().strip(),
                'sourceSha256': {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in sources},
                'unityLaunches': 0, 'nativeSdkCalls': 0, 'contextOnly': context_only, 'nativeContextOnly': native_context_only}
    (output / 'manifest.private.json').write_text(json.dumps(manifest, indent=2))
    with (output / 'compile.private.log').open('xb') as log:
        compiled = subprocess.run([str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
                                   '@' + str(rsp)], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    result = {'compileExit': compiled.returncode, 'checksExit': None, 'runtimePrivacyVerified': False}
    receipt = output / 'result.private.json'
    receipt.write_text(json.dumps(result, indent=2))
    if compiled.returncode: raise ValueError('Compile failed; preserve evidence')
    with (output / 'execution.private.log').open('xb') as log:
        command = [str(exe)] + (['--native-context-only'] if native_context_only else ['--context-only'] if context_only else [])
        result['checksExit'] = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT).returncode
    receipt.write_text(json.dumps(result, indent=2))
    if result['checksExit']: raise ValueError('Pure checks failed; preserve evidence')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--context-only', action='store_true', help='Run only the new privacy context boundaries')
    modes.add_argument('--native-context-only', action='store_true', help='Run only the isolated native test context boundaries')
    args = parser.parse_args()
    try:
        print(json.dumps(run(args.output, args.context_only, args.native_context_only)))
    except Exception:
        parser.exit(1, 'Privacy restart checks stopped; preserve evidence.\n')
