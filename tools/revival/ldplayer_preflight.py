"""Read-only LDPlayer target/ADB checks; never install, launch, or change settings."""
import argparse
import hashlib
import json
import subprocess
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Instance:
    index: int
    name: str
    started: bool

    @property
    def expected_serial(self):
        # LDPlayer's indexed console must independently confirm this mapping.
        return f'emulator-{5554 + 2 * self.index}'


def parse_instances(output):
    instances = []
    for line in output.splitlines():
        if not line.strip():
            continue
        values = line.strip().split(',')
        if len(values) not in (7, 10):
            raise ValueError('Unsupported LDPlayer list2 format; no target selected.')
        try:
            numbers = [int(value) for value in (values[0], *values[2:])]
        except ValueError:
            raise ValueError('Non-numeric LDPlayer instance metadata.') from None
        # list2 uses -1 for absent host/VM PIDs on stopped instances.
        # Preserve every row for duplicate/target checks; never substitute or start it.
        if numbers[3] not in (0, 1) or any(
                value < 0 and not (numbers[3] == 0 and column in (4, 5) and value == -1)
                for column, value in enumerate(numbers)):
            raise ValueError('Invalid LDPlayer instance metadata.')
        instances.append(Instance(numbers[0], values[1], numbers[3] == 1))
    if len({instance.index for instance in instances}) != len(instances):
        raise ValueError('Duplicate LDPlayer indexes; no target selected.')
    return instances


def select_instance(instances, name):
    matches = [instance for instance in instances if instance.name == name]
    if len(matches) != 1:
        raise ValueError('Expected exactly one instance with the exact requested name.')
    return matches[0]


def parse_devices(output):
    devices = {}
    for line in output.splitlines():
        fields = line.split()
        if len(fields) >= 2 and fields[1] in ('device', 'offline', 'unauthorized'):
            if fields[0] in devices:
                raise ValueError('Duplicate ADB transport identifiers.')
            devices[fields[0]] = fields[1]
    return devices


def execute(argv):
    result = subprocess.run(argv, capture_output=True, timeout=20, check=False)
    # Never expose raw tool errors: they may include other devices or local data.
    for encoding in ('utf-8-sig', 'cp949', 'gb18030'):
        try:
            output = result.stdout.decode(encoding)
            break
        except UnicodeDecodeError:
            continue
    else:
        raise ValueError('Tool output encoding could not be decoded.')
    if result.returncode != 0:
        raise ValueError(f'Read-only tool command failed (exit {result.returncode}).')
    return output.strip()


def inspect(directory, name='Tamer', runner=execute):
    console = str(directory / 'ldconsole.exe')
    adb = str(directory / 'adb.exe')
    instance = select_instance(parse_instances(runner([console, 'list2'])), name)
    devices = parse_devices(runner([adb, 'devices']))
    result = {
        'instance': {'name': instance.name, 'index': instance.index,
                     'androidStarted': instance.started},
        'targetAdbState': devices.get(instance.expected_serial, 'absent'),
        'otherTransportCount': len(devices) - int(instance.expected_serial in devices),
        'transportReady': False,
        'gameRuntimeVerified': False,
        'installedApkVerified': False,
        'settingsChanged': False,
        'limitations': ['Transport readiness is not APK/ARM64/16KB/device validation.'],
    }
    if not instance.started:
        result['blocker'] = 'Requested instance is not Android-started; no launch performed.'
        return result
    if result['targetAdbState'] != 'device':
        result['blocker'] = 'Requested instance has no ready ADB transport; settings left unchanged.'
        return result
    prefix = [console, 'adb', '--index', str(instance.index), '--command']
    serial = runner([*prefix, 'get-serialno'])
    if serial != instance.expected_serial:
        result['blocker'] = 'Indexed console transport did not match the requested instance.'
        return result
    # These guest queries only run after BOTH enumeration and indexed confirmation.
    queries = {
        'androidVersion': 'shell getprop ro.build.version.release',
        'androidApi': 'shell getprop ro.build.version.sdk',
        'abiList': 'shell getprop ro.product.cpu.abilist',
        'pageSizeBytes': 'shell getconf PAGESIZE',
    }
    result['guest'] = {key: runner([*prefix, command]) for key, command in queries.items()}
    guest = result['guest']
    if (not guest['androidVersion'] or not guest['androidApi'].isdigit()
            or not guest['abiList'] or not guest['pageSizeBytes'].isdigit()):
        raise ValueError('Guest metadata incomplete; readiness is not confirmed.')
    result['transportReady'] = True
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ldplayer-dir', type=Path, required=True)
    parser.add_argument('--name', default='Tamer', help='Exact existing instance name')
    parser.add_argument('--apk', type=Path, help='Optional local file fingerprint only; no install')
    args = parser.parse_args()
    try:
        directory = args.ldplayer_dir.resolve(strict=True)
        if not all((directory / file).is_file() for file in ('ldconsole.exe', 'adb.exe')):
            raise ValueError('ldconsole.exe and adb.exe must exist in the selected directory.')
        artifact = None
        if args.apk:
            if not args.apk.is_file():
                raise ValueError('Local APK not found; no installation or guest query performed.')
            with args.apk.open('rb') as stream:
                sha256 = hashlib.file_digest(stream, 'sha256').hexdigest()
            artifact = {'bytes': args.apk.stat().st_size, 'sha256': sha256}
        result = inspect(directory, args.name)
        if artifact:
            result['localArtifact'] = artifact
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result['transportReady'] else 2
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        # Do not include tool stderr, guest output, or private paths in this report.
        message = str(error) if isinstance(error, ValueError) else type(error).__name__
        print(json.dumps({'transportReady': False, 'error': message}, ensure_ascii=False))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
