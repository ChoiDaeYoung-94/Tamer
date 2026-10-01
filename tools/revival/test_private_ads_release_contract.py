"""Pure new runtime contract checks, never launches Unity or loads an SDK."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')
SOURCE = 'Assets/Scripts/Advertising/PrivateAdsReleaseContract.cs'
CHECKS = 'tools/revival/private_ads_release/PrivateAdsReleaseContractChecks.cs'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run_checks(output):
    if not output.is_absolute() or not output.is_relative_to(ROOT / 'Logs'):
        raise ValueError('Ignored absolute Logs output required')
    if not subprocess.check_output(['git', '-C', str(ROOT), 'check-ignore', '--', str(output)]).strip():
        raise ValueError('Ignored output required')
    output.mkdir(parents=True, exist_ok=False)
    # Pure fixtures use synthetic inventory only. No actual approval resource is written.
    refs = EDITOR / 'UnityReferenceAssemblies/unity-4.8-api'
    newtonsoft = ROOT / 'Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll'
    executable = output / 'contract-checks.exe'
    sources = [SOURCE, CHECKS, 'Assets/Scripts/Advertising/AdRequestPolicy.cs', 'Assets/Scripts/Advertising/LocalAgeChoice.cs']
    lines = ['-nologo', '-langversion:9.0', '-target:exe', '-define:UNITY_ANDROID', '-out:"' + str(executable) + '"']
    lines += ['-r:"' + str(refs / name) + '"' for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
    lines += ['-r:"' + str(newtonsoft) + '"']
    lines += ['-r:"' + str(refs / 'Facades/netstandard.dll') + '"']
    lines += ['"' + str(ROOT / p) + '"' for p in sources]
    rsp = output / 'checks.rsp'
    rsp.write_text('\n'.join(lines))
    manifest = {'sourceHead': subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD']).decode().strip(),
        'sourceSha256': {p: digest(ROOT / p) for p in sources}, 'responseSha256': digest(rsp),
        'fixture': 'synthetic true approvals in memory only; Unity API stubs',
        'unityBuilds': 0, 'sdkCalls': 0, 'realApprovalRecordsModified': 0}
    (output / 'manifest.private.json').write_text(json.dumps(manifest, indent=2))
    command = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'), '@' + str(rsp)]
    with (output / 'compile.private.log').open('xb') as log:
        compiled = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    if compiled.returncode:
        (output / 'result.private.json').write_text(json.dumps({'compileReturnCode': compiled.returncode, 'executed': False}))
        raise ValueError('Pure checks compilation failed; preserve logs')
    shutil.copyfile(newtonsoft, output / 'Newtonsoft.Json.dll')
    with (output / 'execution.private.log').open('xb') as log:
        executed = subprocess.run([str(EDITOR / 'MonoBleedingEdge/bin/mono.exe'), str(executable)],
                                  cwd=output, stdout=log, stderr=subprocess.STDOUT)
    (output / 'return-codes.private.json').write_text(json.dumps({'compile': compiled.returncode, 'execution': executed.returncode}))
    result = json.loads((output / 'execution.private.log').read_bytes())
    (output / 'result.private.json').write_text(json.dumps(result, indent=2))
    if executed.returncode or result['failed'] or not all(result['checks'].values()):
        raise ValueError('Pure contract checks failed; preserve result')
    return {'pureChecksPassed': len(result['checks']), 'unityBuilds': 0, 'sdkCalls': 0,
            'binaryVerified': False, 'runtimeActivationApproved': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(run_checks(args.output)))
    except Exception:
        parser.exit(1, 'Private release checks stopped; preserve local evidence.\n')


if __name__ == '__main__':
    main()
