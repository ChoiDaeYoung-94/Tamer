"""Offline Windows Editor ownership check. Never connects to Unity/Pipeline.

Only the fixed local metadata PowerShell query is launched. Descriptor contents,
command lines, paths and exceptions must never be included in the public result.
"""
import argparse
from contextlib import contextmanager
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

from windows_owned_files import LockedFile, locked_directories


DESCRIPTOR = Path('Library/Pipeline/.unity-pipeline-port')
MAX_DESCRIPTOR_BYTES = 65536
FIELDS = {'pid', 'port', 'projectPath', 'mode', 'unityVersion', 'startedAt', 'lastHeartbeat'}

# No user-controlled script, network request, socket, Unity CLI, mutation or kill.
# Command lines remain in captured memory; only allowlisted numeric/status fields
# can reach the result. A second process read brackets the listener observation.
SNAPSHOT_SCRIPT = r"""
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
function Read-UnityProcesses {
    @(Get-CimInstance -ClassName Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop |
      ForEach-Object {
        $created = $null
        if ($null -ne $_.CreationDate) { $created = $_.CreationDate.ToUniversalTime().ToString('o') }
        $version = $null
        if ($_.ExecutablePath) {
            $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($_.ExecutablePath).ProductVersion
        }
        [pscustomobject]@{ pid = $_.ProcessId; name = $_.Name; executable = $_.ExecutablePath;
                          commandLine = $_.CommandLine; createdAt = $created; version = $version }
      })
}
$before = @(Read-UnityProcesses)
$listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop | ForEach-Object {
    [pscustomobject]@{ pid = $_.OwningProcess; port = $_.LocalPort; address = $_.LocalAddress }
})
$after = @(Read-UnityProcesses)
[pscustomobject]@{ before = $before; listeners = $listeners; after = $after } |
    ConvertTo-Json -Depth 5 -Compress
"""


class Rejected(Exception):
    """Fixed, non-sensitive reason code only."""


def reject(code):
    raise Rejected(code)


def positive_int(value, maximum=0xffffffff):
    return type(value) is int and 0 < value <= maximum


def utc_time(value):
    if not isinstance(value, str):
        reject('timestamp_metadata_invalid')
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
        if parsed.utcoffset() is None:
            reject('timestamp_metadata_invalid')
        return parsed.astimezone(timezone.utc)
    except (ValueError, OverflowError):
        reject('timestamp_metadata_invalid')


def local_absolute(value):
    if not isinstance(value, str) or not value or '\x00' in value:
        reject('project_path_invalid')
    path = Path(value)
    # No UNC/device paths, relative drives, traversal or remote descriptor reads.
    if (not path.is_absolute() or value.startswith(('\\\\', '//')) or
            '..' in path.parts or ':' in str(path)[2:]):
        reject('project_path_invalid')
    return path


def canonical_project(value):
    path = local_absolute(value).resolve(strict=True)
    if not path.is_dir():
        reject('project_path_invalid')
    return path


def project_root(value):
    root = canonical_project(value)
    if (not (root / 'Assets').is_dir() or
            not (root / 'Packages/manifest.json').is_file() or
            not (root / 'ProjectSettings/ProjectVersion.txt').is_file()):
        reject('unity_project_markers_missing')
    return root


def same_project(value, root):
    return os.path.normcase(str(canonical_project(value))) == os.path.normcase(str(root))


def project_version(root):
    path = root / 'ProjectSettings/ProjectVersion.txt'
    if not path.resolve(strict=True).is_relative_to(root):
        reject('project_version_invalid')
    with path.open('r', encoding='utf-8') as stream:
        raw = stream.read(4097)
    if len(raw) > 4096:
        reject('project_version_invalid')
    versions = re.findall(r'^m_EditorVersion: (\S+)\s*$', raw, re.MULTILINE)
    revisions = re.findall(r'^m_EditorVersionWithRevision: (\S+) \(([0-9a-f]{12})\)\s*$', raw, re.MULTILINE)
    if len(versions) != 1 or len(revisions) != 1 or revisions[0][0] != versions[0]:
        reject('project_version_invalid')
    return versions[0], '{}_{}'.format(*revisions[0])


def windows_argv(command_line):
    """Use Windows quoting rules, including backslashes before closing quotes."""
    if sys.platform != 'win32' or not isinstance(command_line, str) or not command_line:
        reject('process_command_line_unavailable')
    shell = ctypes.WinDLL('shell32', use_last_error=True)
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    shell.CommandLineToArgvW.argtypes = [wintypes.LPCWSTR, ctypes.POINTER(ctypes.c_int)]
    shell.CommandLineToArgvW.restype = ctypes.POINTER(wintypes.LPWSTR)
    kernel.LocalFree.argtypes = [wintypes.HLOCAL]
    kernel.LocalFree.restype = wintypes.HLOCAL
    count = ctypes.c_int()
    argv = shell.CommandLineToArgvW(command_line, ctypes.byref(count))
    if not argv:
        reject('process_command_line_unavailable')
    try:
        return [argv[i] for i in range(count.value)]
    finally:
        kernel.LocalFree(ctypes.cast(argv, wintypes.HLOCAL))


def pinned_cli_executable(executable, root):
    """Exclude only this checkout's exact, hash-verified pinned Unity CLI.

    Windows reports both the CLI and Editor as Unity.exe. Product names,
    command-line flags and a basename alone are not sufficient identity.
    """
    candidate = local_absolute(executable).resolve()
    base = (root / 'tools/.local/unity-cli').resolve()
    if not candidate.is_relative_to(base) or not candidate.is_relative_to(root):
        return False
    config_path = root / 'tools/revival/toolchain.json'
    if not config_path.resolve(strict=True).is_relative_to(root):
        reject('pinned_cli_metadata_invalid')
    with locked_directories([config_path.parent]), LockedFile(config_path, deletable=False) as handle:
        raw = handle.stream.read(16385)
    if len(raw) > 16384:
        reject('pinned_cli_metadata_invalid')
    config = json.loads(raw)
    if not isinstance(config, dict):
        reject('pinned_cli_metadata_invalid')
    version, expected = config.get('unityCli'), config.get('unityCliWindowsX64Sha256')
    if (not isinstance(version, str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,63}', version)
            or not isinstance(expected, str) or not re.fullmatch(r'[a-fA-F0-9]{64}', expected)):
        reject('pinned_cli_metadata_invalid')
    if candidate != (base / version / 'unity.exe').resolve(strict=True):
        return False
    with locked_directories([candidate.parent]), LockedFile(candidate, deletable=False) as handle:
        actual = hashlib.file_digest(handle.stream, 'sha256').hexdigest()
    if actual != expected.lower():
        reject('pinned_cli_digest_mismatch')
    return True


def process_identity(row, root, expected_product_version):
    if not isinstance(row, dict) or not positive_int(row.get('pid')):
        reject('process_metadata_invalid')
    if (row.get('name', '').casefold() != 'unity.exe' or
            not isinstance(row.get('executable'), str) or
            Path(row['executable']).name.casefold() != 'unity.exe'):
        reject('process_executable_unavailable')
    args = windows_argv(row.get('commandLine'))
    if pinned_cli_executable(row['executable'], root):
        return (row['pid'], 'pinned-cli', utc_time(row.get('createdAt')))
    lower = [arg.casefold() for arg in args]
    worker = any(arg in ('-assetimportworker', '-isassetimportworker') for arg in lower)
    worker = worker or any(arg == '-name' and i + 1 < len(lower) and
                           lower[i + 1].startswith('assetimportworker')
                           for i, arg in enumerate(lower))
    if worker:
        return (row['pid'], 'worker', None)
    created = utc_time(row.get('createdAt'))
    indexes = [i for i, arg in enumerate(lower) if arg == '-projectpath']
    if len(indexes) != 1 or indexes[0] + 1 >= len(args):
        reject('process_project_argument_invalid')
    matches = same_project(args[indexes[0] + 1], root)
    if matches and row.get('version') != expected_product_version:
        reject('process_editor_version_mismatch')
    return (row['pid'], matches, created)


def descriptor_pairs(pairs):
    """Drop all unknown fields, including evalToken, during JSON decoding."""
    seen, kept = set(), {}
    for key, value in pairs:
        if key in seen:
            reject('descriptor_duplicate_field')
        seen.add(key)
        if key in FIELDS:
            kept[key] = value
        elif key == 'evalToken':
            kept['_tokenPresent'] = isinstance(value, str) and bool(value.strip())
    return kept


def decode_descriptor(raw, root, mtime, now):
    value = json.loads(raw, object_pairs_hook=descriptor_pairs)
    if not isinstance(value, dict) or not FIELDS.issubset(value):
        reject('descriptor_metadata_missing')
    if not positive_int(value['pid']):
        reject('descriptor_pid_invalid')
    if not positive_int(value['port'], 65535) or not 7800 <= value['port'] <= 7849:
        reject('descriptor_editor_port_out_of_range')
    if value['mode'] not in ('editor', 'batchmode') or not value.get('_tokenPresent'):
        reject('descriptor_metadata_missing')
    if not same_project(value['projectPath'], root):
        reject('descriptor_project_mismatch')
    version, product_version = project_version(root)
    if value['unityVersion'] != version:
        reject('descriptor_editor_version_mismatch')
    started, heartbeat = utc_time(value['startedAt']), utc_time(value['lastHeartbeat'])
    if not started <= heartbeat <= now or started.timestamp() > mtime or mtime > now.timestamp():
        reject('descriptor_timestamp_inconsistent')
    # Heartbeat age is NOT a liveness signal: the package refreshes on requests.
    return {'pid': value['pid'], 'port': value['port'], 'started': started,
            'expectedProductVersion': product_version}


@contextmanager
def read_descriptor(root):
    """Canonical root aliases allowed; reparse points inside the root rejected.

    Existing handle locks pin every ancestor and the descriptor while collecting
    metadata. The read handle has neither write nor delete access. No fallback.
    """
    path = root / DESCRIPTOR
    with locked_directories([path.parent]):
        with LockedFile(path, deletable=False) as handle:
            size = os.fstat(handle.stream.fileno()).st_size
            if not 0 < size <= MAX_DESCRIPTOR_BYTES:
                reject('descriptor_size_invalid')
            # Bounded read even if an unexpected writer bypasses sharing rules.
            raw = handle.stream.read(MAX_DESCRIPTOR_BYTES + 1)
            if len(raw) != size:
                reject('descriptor_changed')
            yield raw, os.fstat(handle.stream.fileno()).st_mtime


def collect_windows_snapshot():
    if sys.platform != 'win32':
        reject('windows_required')
    # Fixed OS executable, never PATH-resolved Unity/CLI or a supplied command.
    shell = Path(os.environ['SystemRoot']) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
    completed = subprocess.run(
        [str(shell), '-NoLogo', '-NoProfile', '-NonInteractive', '-Command', SNAPSHOT_SCRIPT],
        capture_output=True, encoding='utf-8-sig', errors='strict', timeout=20,
        check=False, creationflags=subprocess.CREATE_NO_WINDOW)
    if completed.returncode:
        reject('os_snapshot_unavailable')
    return json.loads(completed.stdout)


def assess_snapshot(descriptor, snapshot, root, now):
    if (not isinstance(snapshot, dict) or
            any(not isinstance(snapshot.get(key), list) for key in ('before', 'listeners', 'after'))):
        reject('os_snapshot_invalid')
    identities = []
    for key in ('before', 'after'):
        rows = [process_identity(row, root, descriptor['expectedProductVersion']) for row in snapshot[key]]
        if len({row[0] for row in rows}) != len(rows):
            reject('process_metadata_invalid')
        identities.append({row[0]: row[1:] for row in rows})
    if identities[0] != identities[1]:
        reject('process_snapshot_changed')
    processes = identities[0]
    target = processes.get(descriptor['pid'])
    if target is None:
        reject('descriptor_pid_not_running_editor')
    if target[0] is not True:
        reject('descriptor_pid_project_mismatch_or_worker')
    if target[1] > descriptor['started'] or target[1] > now:
        reject('pid_reused_or_timestamp_inconsistent')
    candidates = sorted(pid for pid, identity in processes.items() if identity[0] is True)
    if candidates != [descriptor['pid']]:
        reject('multiple_editors_for_project')
    owners = set()
    for listener in snapshot['listeners']:
        if (not isinstance(listener, dict) or not positive_int(listener.get('port'), 65535) or
                not positive_int(listener.get('pid')) or not isinstance(listener.get('address'), str) or
                not listener['address']):
            reject('listener_metadata_invalid')
        if listener['port'] == descriptor['port']:
            owners.add(listener['pid'])
    if not owners:
        reject('descriptor_port_not_listening')
    if owners != {descriptor['pid']}:
        reject('descriptor_port_owner_conflict')
    return {'descriptorPid': descriptor['pid'], 'port': descriptor['port'],
            'matchingEditorPids': candidates, 'listenerPids': sorted(owners)}


def preflight(project_path, *, collector=collect_windows_snapshot, reader=read_descriptor, now=None):
    result = {'scope': 'offlineOwnershipPreflight', 'safeToAttemptConnection': False,
              'controlConnectionVerified': False, 'reason': 'preflight_unavailable'}
    try:
        now = now or datetime.now(timezone.utc)
        root = project_root(project_path)
        with reader(root) as (raw, mtime):
            descriptor = decode_descriptor(raw, root, mtime, now)
            details = assess_snapshot(descriptor, collector(), root, now)
        result.update(details)
        result.update(safeToAttemptConnection=True, reason='ownership_metadata_consistent')
    except Rejected as error:
        result['reason'] = error.args[0]
    except FileNotFoundError:
        result['reason'] = 'project_or_descriptor_missing'
    except (OSError, ValueError, TypeError, KeyError, AttributeError, OverflowError,
            subprocess.SubprocessError, RuntimeError):
        # Never serialize exception text, raw JSON, command lines or stderr.
        pass
    return result


class SafeParser(argparse.ArgumentParser):
    def error(self, message):
        # argparse's default includes arbitrary user input; keep it out of logs.
        reject('arguments_invalid')


def main(argv=None):
    parser = SafeParser(description='Windows Pipeline offline ownership preflight (no connection).')
    parser.add_argument('--project-path', required=True)
    try:
        args = parser.parse_args(argv)
        result = preflight(args.project_path)
    except Rejected:
        result = {'scope': 'offlineOwnershipPreflight', 'safeToAttemptConnection': False,
                  'controlConnectionVerified': False, 'reason': 'arguments_invalid'}
    print(json.dumps(result, ensure_ascii=True, sort_keys=True))
    return 0 if result['safeToAttemptConnection'] else 2


if __name__ == '__main__':
    sys.exit(main())
