"""One synthetic Windows metadata parity; no full launcher or real inputs."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import types


def load(path, name):
    module = types.ModuleType(name)
    module.__file__ = str(path)
    exec(compile(path.read_bytes(), str(path), 'exec', dont_inherit=True), module.__dict__)
    return module


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--powershell', type=Path, required=True)
    args = parser.parse_args()
    source = Path(__file__).resolve().parent.parent
    prep = load(source / 'prepare_binding.py', 'native_test_preparer')
    owned = load(source.parent / 'windows_owned_files.py', 'native_test_owned')
    prep.canonical(str(args.evidence), directory=True)
    prep.canonical(str(args.powershell))
    synthetic = args.evidence / 'synthetic-regular.bin'
    with synthetic.open('xb') as stream:
        stream.write(b'SYNTHETIC REGULAR FILE FOR HANDLE METADATA PARITY\n')
    child_env = {key: value for key, value in os.environ.items() if not key.upper().startswith(
        ('TAMER_', 'PYTHON', 'DOTNET_', 'COMPLUS_')) and key.upper() not in
        ('JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS', 'CLASSPATH')}
    with owned.locked_directories([synthetic.parent]):
        with owned.LockedFile(synthetic, deletable=False) as locked:
            actual = prep.WindowsMetadata().read(synthetic, locked)
            result = subprocess.run([str(args.powershell), '-NoProfile', '-NonInteractive', '-File',
                str(source / 'tests/Test-Metadata.ps1'), '-SyntheticFile', str(synthetic),
                '-LauncherSource', str(source / 'SigningLauncher.cs')], env=child_env,
                stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=60)
            if result.returncode or result.stderr or len(result.stdout) > 65536:
                raise AssertionError('NativeParityChildFailed')
            expected = prep.strict_json(result.stdout.strip())
            if set(expected) != {'identity', 'sddl', 'sha256'} or expected != {key: actual[key] for key in expected}:
                raise AssertionError('NativeMetadataParityMismatch')
            # Incompatible write opens and existing hardlinks fail closed. All
            # mutations below are synthetic fixtures preserved in evidence.
            try:
                with synthetic.open('wb'):
                    pass
            except PermissionError:
                pass
            else:
                raise AssertionError('SharedWriteAccepted')
    hardlink = args.evidence / 'synthetic-hardlink.bin'
    os.link(synthetic, hardlink)
    try:
        prep.canonical(str(hardlink))
    except ValueError:
        pass
    else:
        raise AssertionError('HardlinkAccepted')
    print(json.dumps({'nativeParityCases': 1, 'identityEqual': True, 'sddlEqual': True,
        'sha256Equal': True, 'sharedWriteRejected': True, 'hardlinkRejected': True,
        'launcherExecuted': False, 'aclWrites': 0, 'environmentWrites': 0}, separators=(',', ':')))


if __name__ == '__main__':
    main()
