"""Reviewed launcher bridge. Default is inspection; run requires an owned stdin session.

No approval is inferred from a challenge. The existing producer remains responsible
for source/config/tool/inventory checks and its journal/actual delta. This module
never restores files or promotes an artifact to distributable.
"""
import argparse
from contextlib import ExitStack, redirect_stderr, redirect_stdout
import hashlib
import io
import json
import os
from pathlib import Path
import re
import struct
import sys
import threading
import time
import types
import stat

# -I -S excludes cwd, PYTHONPATH and site code. Repository helpers are compiled
# directly from exact pinned .py bytes; no repository .pyc is ever considered.
HELPERS = Path(__file__).resolve().parent.parent
producer = base = require_private = None

SECRET_NAMES = ('TAMER_KEYSTORE_PASS', 'TAMER_KEYALIAS_PASS')
SOURCES = ('private_ads_producer.py', 'private_ads_build.py', 'private_ads_approved.py',
           'private_ads_evidence.py', 'validate_private_ads_preparation.py',
           'windows_owned_files.py')
OWN_SOURCES = ('OwnedChild.cs', 'SigningLauncher.cs', 'OperatingKeyPreflight.java',
               'producer_bridge.py', 'Invoke-SigningLauncher.ps1', 'EditorOwnership.ps1')
CLAIM = 'launcher-attempt.private.json'
SIGNING = 'launcher-signing.private.json'
RESULT = 'launcher-result.private.json'
COLLISIONS = ('launch-once.private.json', 'hook-receipt.json', 'actual-delta.private.json',
              'settings-restoration.json', 'invocation.private.json', SIGNING, RESULT)


class StageFailure(Exception):
    def __init__(self, kind):
        super().__init__()
        self.kind = kind


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def regular(path):
    """Existing local path with no reparse ancestors or hardlink aliases."""
    path = Path(path)
    if not path.is_absolute() or path.drive.startswith('\\\\') or '..' in path.parts:
        raise ValueError('Local absolute path required')
    for part in (path, *path.parents):
        info = part.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
            raise ValueError('Linked path rejected')
    if path.stat().st_nlink != 1 or not path.is_file():
        raise ValueError('Single regular file required')
    return path


def binding(path, expected_sha):
    path = regular(path)
    raw = path.read_bytes()
    if not re.fullmatch('[0-9a-f]{64}', expected_sha) or sha(raw) != expected_sha:
        raise ValueError('Exact launcher review required')
    def unique(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError('Duplicate binding field')
            result[key] = value
        return result
    def reject(_):
        raise ValueError('Invalid binding number')
    value = json.loads(raw, object_pairs_hook=unique, parse_constant=reject)
    required = {'schema', 'checkout', 'head', 'branch', 'plan', 'planSha256', 'sourceReview',
                'sourceReviewSha256', 'certificate', 'certificateSha256', 'alias',
                'currentUploadCertificateReviewed', 'existingKeyOwnershipReviewed',
                'python', 'git', 'powershell', 'java', 'classDirectory', 'bridge',
                'deadlineUtc', 'minimumStartBudgetSeconds', 'producerTimeoutSeconds',
                'runtimeFiles', 'absentRuntimeFiles', 'pins'}
    if set(value) != required or value['schema'] != 1:
        raise ValueError('Launcher binding schema rejected')
    if value['currentUploadCertificateReviewed'] is not True or value['existingKeyOwnershipReviewed'] is not True:
        raise ValueError('Independent certificate/ownership review required')
    if (not re.fullmatch('[0-9a-f]{40}', value['head']) or not value['alias'] or '\0' in value['alias'] or
            len(value['alias'].encode('utf-8')) > 4096):
        raise ValueError('Exact source and alias required')
    if Path(value['bridge']) != Path(__file__).resolve():
        raise ValueError('Bridge path mismatch')
    return value


def load_helpers(value):
    """Source-only loader, also ignoring valid-looking unchecked-hash caches."""
    global producer, base, require_private
    pins = {str(Path(p['path'])): p for p in value['pins']}
    for name in ('windows_owned_files', 'private_ads_evidence', 'validate_private_ads_preparation',
                 'private_ads_build', 'private_ads_approved', 'private_ads_producer'):
        path = regular(HELPERS / (name + '.py'))
        raw = path.read_bytes()
        if str(path) not in pins or sha(raw) != pins[str(path)]['sha256']:
            raise ValueError('Exact helper source required before import')
        module = types.ModuleType(name)
        module.__file__ = str(path)
        module.__package__ = ''
        sys.modules[name] = module
        exec(compile(raw, str(path), 'exec', dont_inherit=True), module.__dict__)
    producer = sys.modules['private_ads_producer']
    base = sys.modules['private_ads_build']
    require_private = sys.modules['private_ads_evidence'].require_private


def configure_tools(value):
    """Existing producer uses git/powershell names. Resolve to reviewed exact files.

    Python/JVM injection variables were already removed by the native launcher.
    This child-only patch avoids PATH fallback for the producer's OS probes.
    """
    git = str(regular(value['git']))
    powershell = str(regular(value['powershell']))
    def exact_git(root, *args):
        return base.subprocess.check_output([git, '-C', str(root), *args],
            env={k: v for k, v in os.environ.items() if k not in SECRET_NAMES},
            stderr=base.subprocess.DEVNULL).decode().strip()

    def exact_closed(root):
        result = base.subprocess.run([powershell, '-NoProfile', '-NonInteractive', '-File',
                                      str(Path(__file__).parent / 'EditorOwnership.ps1'), '-Checkout', str(root)],
                                     env={k: v for k, v in os.environ.items() if k not in SECRET_NAMES},
                                     stdout=base.subprocess.DEVNULL, stderr=base.subprocess.DEVNULL)
        if result.returncode:
            raise ValueError('Owner Editor state is not closed')

    base.git = exact_git
    base.require_editor_closed = exact_closed


def runtime_files():
    """-I -S startup excludes site/user code; pin the stdlib/native runtime set.

    Windows system libraries remain the OS trust boundary. No host-wide library
    enumeration or process command line is returned.
    """
    prefix = Path(sys.base_prefix)
    paths = {Path(sys.executable)}
    for folder in ('Lib', 'DLLs'):
        top = prefix / folder
        if top.exists():
            paths.update(p for p in top.rglob('*') if p.is_file() and
                         p.suffix.lower() in ('.py', '.pyd', '.dll', '.zip') and
                         'site-packages' not in p.parts and '__pycache__' not in p.parts)
    paths.update(p for p in prefix.glob('*') if p.is_file() and
                 p.suffix.lower() in ('.dll', '.zip', '.pyd'))
    candidates = {prefix / 'pyvenv.cfg', prefix.parent / 'pyvenv.cfg',
                  prefix / (Path(sys.executable).stem + '._pth')}
    candidates.update(p.with_suffix('._pth') for p in prefix.glob('python*.dll'))
    paths.update(p for p in candidates if p.is_file())
    return {str(p) for p in paths}


def immutable_pin_paths(root, plan, value):
    """Exact persistent read scope shared by metadata preparation and the gate."""
    immutable = {str(root / p) for p in plan['before']
                 if not p.startswith(('Assets/', 'ProjectSettings/', 'Packages/'))}
    return immutable | {str(Path(value['plan'])), str(Path(value['sourceReview'])),
        str(Path(plan['config'])), str(root / 'src/AeDeong.keystore'), str(Path(value['certificate'])),
        value['python'], value['git'], value['powershell'], value['java'],
        str(Path(value['classDirectory']) / 'OperatingKeyPreflight.class'),
        *(str(HELPERS / p) for p in SOURCES),
        *(str(Path(__file__).parent / p) for p in OWN_SOURCES),
        *plan['installedTools']['files'], *value['runtimeFiles']}


def check(value, attempt, *, claim=False):
    if (sys.flags.isolated != 1 or sys.flags.no_site != 1 or sys.flags.dont_write_bytecode != 1 or
            Path(sys.executable) != Path(value['python']) or sys.pycache_prefix is None or
            Path(sys.pycache_prefix) != Path(value['plan']) or not Path(sys.pycache_prefix).is_file() or
            set(value['runtimeFiles']) != runtime_files()):
        raise ValueError('Exact isolated Python runtime required')
    if not re.fullmatch('[0-9a-f]{32}', attempt):
        raise ValueError('Fresh attempt identity required')
    root = Path(value['checkout'])
    plan_path = regular(value['plan'])
    plan = producer.load_plan(root, plan_path, value['planSha256'])
    # This launcher connects only the approved adult AAB entry. Legacy external
    # key and privacy-only APK contracts require separate reviewed launchers.
    if plan['mode'] != 'approved_adult_release' or plan['head'] != value['head'] or plan['branch'] != value['branch']:
        raise ValueError('Approved adult exact plan required')
    config = Path(plan['config'])
    from private_ads_approved import preflight_approved
    preflight_approved(root, config, value['head'], plan['editorVersion'])
    if base.git(root, 'branch', '--show-current') != value['branch']:
        raise ValueError('Branch changed')
    producer.require_unchanged(root, plan)
    source = regular(value['sourceReview'])
    if sha(source.read_bytes()) != value['sourceReviewSha256']:
        raise ValueError('Source review changed')
    producer.require_source_review(root, plan, source)
    if producer.KEY not in plan['before']:
        raise ValueError('Original release key missing from exact plan')
    key = regular(root / producer.KEY)  # Never challenge an external copy.
    if sha(key.read_bytes()) != plan['before'][producer.KEY]['sha256']:
        raise ValueError('Original key changed')
    cert = regular(value['certificate'])
    if sha(cert.read_bytes()) != value['certificateSha256']:
        raise ValueError('Current upload certificate changed')
    run = base.safe_path(root, plan['run'])
    require_private(run)
    if any((run / name).exists() for name in (*COLLISIONS, producer.artifact_name(plan['mode']))):
        raise ValueError('Run already attempted or output exists')
    marker = {'schema': 1, 'attemptId': attempt, 'sourceHead': value['head'],
              'planSha256': value['planSha256']}
    with ExitStack() as stack:
        producer.pin_installed_tools(stack, plan['installedTools'])
    # Assets/ProjectSettings/Packages are deliberately not persistent locks:
    # producer stages and restores them. It repeats inventory checks itself.
    required = immutable_pin_paths(root, plan, value)
    supplied = {p['path']: p for p in value['pins']}
    if len(supplied) != len(value['pins']) or set(supplied) != required:
        raise ValueError('Exact immutable pin scope required')
    # C# also verifies identity, ACL and reparse/hardlinks while retaining handles.
    for path, pin in supplied.items():
        if set(pin) != {'path', 'sha256', 'identity', 'sddl'} or sha(regular(path).read_bytes()) != pin['sha256']:
            raise ValueError('Reviewed pin changed')
    if claim:
        with producer.locked_directories([run]):
            require_private(run)
            producer.write_new(run / CLAIM, producer.encoded(marker))
    if producer.read_json(regular(run / CLAIM)) != marker:
        raise ValueError('Attempt ownership changed')
    return plan, {'schema': 1, 'status': 'ready', 'attemptId': attempt,
                  'sourceHead': value['head'], 'planSha256': value['planSha256']}


def read_exact(stream, count):
    parts = bytearray()
    try:
        while len(parts) < count:
            block = stream.read(count - len(parts))
            if not block:
                raise ValueError('Partial owned input')
            parts.extend(block)
        return parts
    except BaseException:
        parts[:] = b'\0' * len(parts)
        raise


def integer(stream):
    data = read_exact(stream, 4)
    try:
        return struct.unpack('>I', data)[0]
    finally:
        data[:] = b'\0' * len(data)


def passwords(stream):
    """Length-delimited UTF-16BE; NUL and unpaired surrogates cannot enter env."""
    raw = []
    text = []
    try:
        if integer(stream) != 0x544C5031:
            raise ValueError('Owned input framing required')
        for _ in SECRET_NAMES:
            count = integer(stream)
            if not 1 <= count <= 1024:
                raise ValueError('Password length rejected')
            item = read_exact(stream, count * 2)
            raw.append(item)
            decoded = item.decode('utf-16-be', errors='strict')
            if '\0' in decoded:
                raise ValueError('NUL is not an environment value')
            text.append(decoded)
        if stream.read(1):
            raise ValueError('Trailing owned input')
        return text
    finally:
        for item in raw:
            item[:] = b'\0' * len(item)


def challenge(value, secret):
    """One actual Java challenge inside the native owner's non-breakaway Job.

    Direct bridge run cannot mint an existing-key receipt from a mere password
    frame. Java receives binary stdin only, before producer env is populated.
    """
    command = [value['java'], '-XX:-UsePerfData', '-XX:+DisableAttachMechanism',
               '-XX:ErrorFile=NUL', '-XX:-CreateCoredumpOnCrash', '-cp',
               value['classDirectory'], 'OperatingKeyPreflight']
    frame = bytearray(struct.pack('>I', 0x544B5031))
    captured = [bytearray(), bytearray()]
    failure = []
    done = [False, False, False]
    child = None
    def field(raw):
        frame.extend(struct.pack('>I', len(raw)))
        frame.extend(raw)
    def drain(index, stream):
        try:
            while True:
                block = stream.read(1024)
                if not block:
                    break
                if len(captured[index]) + len(block) > 4096:
                    raise ValueError('Worker output bound')
                captured[index].extend(block)
        except BaseException:
            failure.append(True)
        finally:
            done[index] = True
    def write():
        try:
            child.stdin.write(frame)
            child.stdin.close()
        except BaseException:
            failure.append(True)
        finally:
            done[2] = True
    try:
        for path, limit in ((Path(value['checkout']) / producer.KEY, 16 * 1024 * 1024),
                            (Path(value['certificate']), 1024 * 1024)):
            if not 1 <= path.stat().st_size <= limit:
                raise ValueError('Challenge input bound')
            raw = bytearray(path.read_bytes())
            try:
                field(raw)
            finally:
                raw[:] = b'\0' * len(raw)
        field(value['alias'].encode('utf-8'))
        for text in secret:
            raw = bytearray(text, 'utf-16-be')
            try:
                frame.extend(struct.pack('>I', len(raw) // 2))
                frame.extend(raw)
            finally:
                raw[:] = b'\0' * len(raw)
        # No CREATE_BREAKAWAY_FROM_JOB, shell, environment secret, or broad
        # inheritable handles. Windows propagates the launcher's Job membership.
        child = base.subprocess.Popen(command, cwd=value['classDirectory'],
            stdin=base.subprocess.PIPE, stdout=base.subprocess.PIPE, stderr=base.subprocess.PIPE,
            close_fds=True, creationflags=base.subprocess.CREATE_NO_WINDOW)
        import ctypes
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.IsProcessInJob.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)]
        inside = ctypes.c_int()
        if not kernel.IsProcessInJob(int(child._handle), None, ctypes.byref(inside)) or not inside.value:
            raise StageFailure('WorkerJobMembershipRejected')
        for target, arguments in ((drain, (0, child.stdout)), (drain, (1, child.stderr)), (write, ())):
            threading.Thread(target=target, args=arguments, daemon=True).start()
        deadline = time.monotonic() + 30
        while child.poll() is None or not all(done):
            if failure or time.monotonic() >= deadline:
                raise ValueError('Worker deadline or IO failure')
            time.sleep(.01)
        if captured[1]:
            raise StageFailure('WorkerStderrRejected')
        if child.returncode == 0 and captured[0] == b'{"status":"verified","exceptionType":""}':
            return
        try:
            result = json.loads(captured[0])
            if (child.returncode == 1 and set(result) == {'status', 'exceptionType'} and
                    result['status'] == 'failed' and re.fullmatch('[A-Za-z0-9_]{1,100}', result['exceptionType'])):
                raise StageFailure(result['exceptionType'])
        except StageFailure:
            raise
        except (ValueError, TypeError):
            pass
        raise StageFailure('WorkerProtocolRejected')
    finally:
        if child is not None and child.poll() is None:
            child.kill()  # Retained direct child only; native Job owns descendants.
            child.wait(timeout=5)
        frame[:] = b'\0' * len(frame)
        for raw in captured:
            raw[:] = b'\0' * len(raw)


def require_owned_job():
    """Run is valid only inside a non-breakaway KILL_ON_JOB_CLOSE Job."""
    import ctypes
    from ctypes import wintypes
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.GetCurrentProcess.restype = wintypes.HANDLE
    kernel.IsProcessInJob.argtypes = [wintypes.HANDLE, wintypes.HANDLE, ctypes.POINTER(wintypes.BOOL)]
    present = wintypes.BOOL()
    if not kernel.IsProcessInJob(kernel.GetCurrentProcess(), None, ctypes.byref(present)) or not present.value:
        raise ValueError('Native owner Job required')
    class Basic(ctypes.Structure):
        _fields_ = [('processTime', ctypes.c_int64), ('jobTime', ctypes.c_int64),
                    ('flags', wintypes.DWORD), ('minWorking', ctypes.c_size_t),
                    ('maxWorking', ctypes.c_size_t), ('activeLimit', wintypes.DWORD),
                    ('affinity', ctypes.c_size_t), ('priority', wintypes.DWORD),
                    ('scheduling', wintypes.DWORD)]
    limits = Basic()
    kernel.QueryInformationJobObject.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p,
                                                wintypes.DWORD, ctypes.c_void_p]
    if (not kernel.QueryInformationJobObject(None, 2, ctypes.byref(limits), ctypes.sizeof(limits), None) or
            not limits.flags & 0x2000 or limits.flags & (0x800 | 0x1000)):
        raise ValueError('Non-breakaway kill-on-close Job required')


def execute(value, attempt, stream, state=None):
    # Startup has no secret env. Parent sends only after suspended creation,
    # non-breakaway Job assignment and resume. This is the owned producer child.
    state = state if state is not None else {}
    state.update(phase='preflight', producerInvoked=False)
    if any(name in os.environ for name in SECRET_NAMES):
        raise ValueError('Credentials must arrive through owned stdin')
    require_owned_job()
    plan, _ = check(value, attempt)
    state['phase'] = 'input'
    secret = passwords(stream)
    try:
        state['phase'] = 'challenge'
        challenge(value, secret)
        # Recheck actual HEAD/clean/plan/source/config/tool/inventory after the
        # challenge. No signing receipt or producer env exists before this.
        state['phase'] = 'recheck'
        plan, _ = check(value, attempt)
        signing = Path(plan['checkout']) / plan['run'] / SIGNING
        receipt = {'schema': 1, 'keystore': producer.KEY,
                   'keystoreSha256': plan['before'][producer.KEY]['sha256'],
                   'alias': value['alias'], 'certificateSha256': value['certificateSha256'],
                   'existingReleaseKeyVerified': True, 'sourceHead': value['head'],
                   'planSha256': value['planSha256']}
        # A direct run has now performed the same actual challenge. Independent
        # ownership/current certificate/source review were prerequisites.
        state['phase'] = 'receipt'
        with producer.locked_directories([signing.parent]):
            require_private(signing.parent)
            base.safe_path(Path(plan['checkout']), signing.relative_to(plan['checkout']))
            producer.write_new(signing, producer.encoded(receipt))
            regular(signing)
            with producer.LockedFile(signing, deletable=False) as pinned:
                pinned.verify(base.identity(signing), sha(producer.encoded(receipt)))
                for name, text in zip(SECRET_NAMES, secret):
                    os.environ[name] = text
                state.update(phase='producer', producerInvoked=True)
                result = producer.build_once(Path(value['checkout']), Path(plan['config']), value['head'],
                    plan['editorVersion'], Path(value['plan']), value['planSha256'],
                    Path(value['sourceReview']), signing, allow_unity_build=True)
        if result.get('actualBuildExecuted') is not True:
            raise ValueError('Producer completion unverified')
        state['phase'] = 'completed'
        return {'schema': 1, 'status': 'producer_completed', 'exceptionType': '', 'phase': 'completed',
                'producerInvoked': True, 'actualBuildExecuted': True,
                'binaryVerified': False, 'distributable': False, 'externalRecoveryRequired': True}
    finally:
        for name in SECRET_NAMES:
            os.environ.pop(name, None)
        # Python/CLR/OS immutable copies cannot be claimed completely erased.
        secret.clear()


class Discard(io.TextIOBase):
    def write(self, value):
        return len(value)


def main():
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument('action', choices=('inspect', 'claim', 'run'))
    parser.add_argument('--binding', type=Path, required=True)
    parser.add_argument('--binding-sha256', required=True)
    parser.add_argument('--attempt', required=True)
    parser.add_argument('--allow-reviewed-build', action='store_true')
    args = parser.parse_args()
    result = None
    code = 1
    state = {'phase': 'binding', 'producerInvoked': False}
    try:
        with redirect_stdout(Discard()), redirect_stderr(Discard()):
            value = binding(args.binding, args.binding_sha256)
            load_helpers(value)
            require_private(args.binding.parent)
            configure_tools(value)
            if args.action == 'run':
                if not args.allow_reviewed_build:
                    raise ValueError('Explicit reviewed build permission required')
                result = execute(value, args.attempt, sys.stdin.buffer, state)
            else:
                state['phase'] = 'preflight'
                _, result = check(value, args.attempt, claim=args.action == 'claim')
        code = 0
    except BaseException as failure:
        kind = failure.kind if isinstance(failure, StageFailure) else type(failure).__name__
        if not re.fullmatch('[A-Za-z0-9_]{1,100}', kind):
            kind = 'Exception'
        result = {'schema': 1, 'status': 'failed', 'exceptionType': kind, 'phase': state['phase'],
                  'producerInvoked': state['producerInvoked'],
                  'actualBuildExecuted': None if state['producerInvoked'] else False,
                  'binaryVerified': False, 'distributable': False,
                  'externalRecoveryRequired': state['producerInvoked']}
    # Strict fixed fields only; native diagnostics, paths and secret text never flow out.
    sys.stdout.write(json.dumps(result, separators=(',', ':')))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
