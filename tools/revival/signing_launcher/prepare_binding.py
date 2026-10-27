"""Prepare two private launcher metadata files; never claim, sign or build.

Only an existing exact approved plan/source review and explicitly supplied
independent review facts are consumed. No approval or recovery record is made.
Run with the selected standalone Python: -I -S -B -X pycache_prefix=<plan>.
"""
import argparse
from contextlib import ExitStack, redirect_stderr, redirect_stdout
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import sys
import types

LIMIT = 8 * 1024 * 1024
BINDING_NAME = 'launcher-binding.private.json'
MANIFEST_NAME = 'launcher-sources.private.json'
HELPERS = ('private_ads_producer.py', 'private_ads_build.py', 'private_ads_approved.py',
           'private_ads_evidence.py', 'validate_private_ads_preparation.py', 'windows_owned_files.py')
OWN_SOURCES = ('OwnedChild.cs', 'SigningLauncher.cs', 'OperatingKeyPreflight.java',
               'producer_bridge.py', 'Invoke-SigningLauncher.ps1', 'EditorOwnership.ps1')
PATH_FIELDS = ('checkout', 'plan', 'sourceReview', 'certificate', 'git', 'powershell',
               'java', 'classDirectory', 'outputDirectory', 'currentUploadCertificateEvidence',
               'existingKeyOwnershipEvidence')
HASH_FIELDS = ('planSha256', 'sourceReviewSha256', 'certificateSha256',
               'currentUploadCertificateEvidenceSha256', 'existingKeyOwnershipEvidenceSha256')
REQUEST_FIELDS = {*PATH_FIELDS, *HASH_FIELDS, 'head', 'branch', 'alias', 'deadlineUtc',
                  'minimumStartBudgetSeconds', 'producerTimeoutSeconds',
                  'currentUploadCertificateReviewed', 'existingKeyOwnershipReviewed'}
BINDING_FIELDS = {'schema', 'checkout', 'head', 'branch', 'plan', 'planSha256', 'sourceReview',
                  'sourceReviewSha256', 'certificate', 'certificateSha256', 'alias',
                  'currentUploadCertificateReviewed', 'existingKeyOwnershipReviewed', 'python',
                  'git', 'powershell', 'java', 'classDirectory', 'bridge', 'deadlineUtc',
                  'minimumStartBudgetSeconds', 'producerTimeoutSeconds', 'runtimeFiles',
                  'absentRuntimeFiles', 'pins'}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def strict_json(raw):
    def unique(items):
        result = {}
        for name, value in items:
            if name in result:
                raise ValueError('DuplicateProperty')
            result[name] = value
        return result
    def reject(_):
        raise ValueError('InvalidNumber')
    if not 1 <= len(raw) <= LIMIT:
        raise ValueError('MetadataSize')
    return json.loads(raw.decode('utf-8', errors='strict'), object_pairs_hook=unique,
                      parse_constant=reject)


def encoded(value):
    raw = json.dumps(value, ensure_ascii=True, sort_keys=True, separators=(',', ':'),
                     allow_nan=False).encode('utf-8')
    strict_json(raw)
    return raw


def local_path(value):
    """Syntactic validation before any IO, including device/ADS aliases."""
    if (type(value) is not str or not re.match(r'^[A-Za-z]:[\\/]', value) or
            '\0' in value or any(p in ('.', '..') for p in re.split(r'[\\/]', value)) or
            any(c in value[2:] for c in ':*?<>|"') or
            any(p.endswith((' ', '.')) for p in re.split(r'[\\/]', value)[1:])):
        raise ValueError('LocalAbsolutePath')
    for part in re.split(r'[\\/]', value)[1:]:
        if re.fullmatch(r'(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?', part):
            raise ValueError('DevicePath')
    return Path(value)


def canonical(value, *, directory=False):
    path = local_path(value)
    for part in (*reversed(path.parents), path):
        info = part.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
            raise ValueError('ReparsePath')
        if part != path and not stat.S_ISDIR(info.st_mode):
            raise ValueError('AncestorType')
    info = path.stat()
    if (stat.S_ISDIR(info.st_mode) != directory or
            (not directory and (not stat.S_ISREG(info.st_mode) or info.st_nlink != 1))):
        raise ValueError('RegularPath')
    resolved = path.resolve(strict=True)
    if str(path) != str(resolved):
        raise ValueError('NoncanonicalPath')
    return resolved


def deadline(value):
    if type(value) is not str or not re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?Z', value):
        raise ValueError('UtcDeadline')
    return datetime.fromisoformat(value[:-1] + '+00:00')


def budget(request, now):
    if now.tzinfo is None or (deadline(request['deadlineUtc']) - now).total_seconds() < request['minimumStartBudgetSeconds']:
        raise TimeoutError('StartBudget')


def validate_request(request, now):
    if type(request) is not dict or set(request) != REQUEST_FIELDS:
        raise ValueError('RequestFields')
    for name in PATH_FIELDS:
        local_path(request[name])
    for name in HASH_FIELDS:
        if type(request[name]) is not str or not re.fullmatch('[0-9a-f]{64}', request[name]):
            raise ValueError('ReviewedSha256')
    if (type(request['head']) is not str or not re.fullmatch('[0-9a-f]{40}', request['head']) or
            type(request['branch']) is not str or not request['branch'] or '\0' in request['branch'] or
            type(request['alias']) is not str or not request['alias'] or '\0' in request['alias'] or
            len(request['alias'].encode('utf-8', errors='strict')) > 4096):
        raise ValueError('ExactSourceAlias')
    if any(request[name] is not True for name in ('currentUploadCertificateReviewed', 'existingKeyOwnershipReviewed')):
        raise ValueError('IndependentReviewRequired')
    minimum, timeout = request['minimumStartBudgetSeconds'], request['producerTimeoutSeconds']
    if type(minimum) is not int or not 600 <= minimum <= 2147483647 or type(timeout) is not int or not 1 <= timeout <= 86400:
        raise ValueError('BudgetRange')
    budget(request, now)


class NativeInfo(ctypes.Structure):
    _fields_ = [(name, wintypes.DWORD) for name in (
        'attributes', 'creationLow', 'creationHigh', 'accessLow', 'accessHigh', 'writeLow',
        'writeHigh', 'volume', 'sizeHigh', 'sizeLow', 'links', 'indexHigh', 'indexLow')]


class WindowsMetadata:
    """Identity, Owner|Group|Access SDDL and streaming SHA on one held handle."""
    def __init__(self):
        if sys.platform != 'win32':
            raise OSError('WindowsRequired')
        self.kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        self.adv = ctypes.WinDLL('advapi32', use_last_error=True)
        self.kernel.GetFileInformationByHandle.argtypes = [wintypes.HANDLE, ctypes.POINTER(NativeInfo)]
        self.kernel.GetFileInformationByHandle.restype = wintypes.BOOL
        self.kernel.LocalFree.argtypes = [ctypes.c_void_p]
        self.adv.GetSecurityInfo.argtypes = [wintypes.HANDLE, ctypes.c_int, wintypes.DWORD,
            ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_void_p)]
        self.adv.GetSecurityInfo.restype = wintypes.DWORD
        self.adv.ConvertSecurityDescriptorToStringSecurityDescriptorW.argtypes = [ctypes.c_void_p,
            wintypes.DWORD, wintypes.DWORD, ctypes.POINTER(wintypes.LPWSTR), ctypes.c_void_p]
        self.adv.ConvertSecurityDescriptorToStringSecurityDescriptorW.restype = wintypes.BOOL

    def info(self, handle):
        value = NativeInfo()
        if not self.kernel.GetFileInformationByHandle(handle, ctypes.byref(value)):
            raise OSError('FileInformation')
        return value

    def sddl(self, handle):
        descriptor, text = ctypes.c_void_p(), wintypes.LPWSTR()
        if self.adv.GetSecurityInfo(handle, 1, 7, None, None, None, None, ctypes.byref(descriptor)):
            raise OSError('SecurityInformation')
        try:
            if not self.adv.ConvertSecurityDescriptorToStringSecurityDescriptorW(descriptor, 1, 7, ctypes.byref(text), None):
                raise OSError('SecurityDescriptor')
            return text.value
        finally:
            if text:
                self.kernel.LocalFree(text)
            self.kernel.LocalFree(descriptor)

    def require_private(self, handle):
        # Bootstrap must inspect the output ACL before reading approval inputs.
        # After source-only loading the existing require_private is also used.
        self.kernel.GetCurrentProcess.restype = wintypes.HANDLE
        self.kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        self.adv.OpenProcessToken.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.POINTER(wintypes.HANDLE)]
        self.adv.GetTokenInformation.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p,
                                               wintypes.DWORD, ctypes.POINTER(wintypes.DWORD)]
        self.adv.ConvertSidToStringSidW.argtypes = [ctypes.c_void_p, ctypes.POINTER(wintypes.LPWSTR)]
        token = wintypes.HANDLE()
        if not self.adv.OpenProcessToken(self.kernel.GetCurrentProcess(), 8, ctypes.byref(token)):
            raise OSError('OwnerToken')
        text = wintypes.LPWSTR()
        try:
            needed = wintypes.DWORD()
            self.adv.GetTokenInformation(token, 1, None, 0, ctypes.byref(needed))
            data = ctypes.create_string_buffer(needed.value)
            if not self.adv.GetTokenInformation(token, 1, data, len(data), ctypes.byref(needed)):
                raise OSError('OwnerInformation')
            if not self.adv.ConvertSidToStringSidW(ctypes.cast(data, ctypes.POINTER(ctypes.c_void_p))[0], ctypes.byref(text)):
                raise OSError('OwnerSid')
            dacl = self.sddl(handle).split('D:', 1)[1]
            prefix = dacl.split('(', 1)[0]
            aces = re.findall(r'\(A;([^;]*);FA;;;([^;)]+)\)', dacl)
            if (prefix not in ('P', 'PAI', 'AI', '') or len(aces) != 2 or
                    {sid for _, sid in aces} != {'SY', text.value} or
                    prefix + ''.join('(A;' + flags + ';FA;;;' + sid + ')' for flags, sid in aces) != dacl or
                    any(re.sub(r'OI|CI|ID', '', flags) for flags, _ in aces)):
                raise ValueError('PrivateAcl')
        finally:
            if text:
                self.kernel.LocalFree(text)
            self.kernel.CloseHandle(token)

    def read(self, path, locked):
        import msvcrt
        canonical(str(path))
        handle = msvcrt.get_osfhandle(locked.stream.fileno())
        def identity():
            info = self.info(handle)
            if info.links != 1 or info.attributes & 0x410:
                raise ValueError('RegularHandle')
            return ':'.join(format(getattr(info, name), '08x') for name in
                ('volume', 'indexHigh', 'indexLow', 'sizeHigh', 'sizeLow', 'writeHigh', 'writeLow'))
        before, acl = identity(), self.sddl(handle)
        digest = hashlib.sha256()
        locked.stream.seek(0)
        while True:
            block = locked.stream.read(1024 * 1024)
            if not block:
                break
            digest.update(block)
        canonical(str(path))
        if identity() != before or self.sddl(handle) != acl:
            raise ValueError('MetadataDrift')
        return {'path': str(path), 'sha256': digest.hexdigest(), 'identity': before, 'sddl': acl}


class BootstrapFile:
    """Minimal read lock until the reviewed existing LockedFile can be loaded."""
    def __init__(self, path, directory=False):
        import msvcrt
        canonical(str(path), directory=directory)
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
            ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
        kernel.CreateFileW.restype = wintypes.HANDLE
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        handle = kernel.CreateFileW(str(path), 0x20080 if directory else 0x80000000,
                                   1, None, 3, 0x02200000, None)
        if handle == ctypes.c_void_p(-1).value:
            raise OSError('BootstrapLock')
        try:
            descriptor = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
        except BaseException:
            kernel.CloseHandle(handle)
            raise
        self.descriptor, self.stream = descriptor, None
        try:
            info = os.fstat(descriptor)
            if (getattr(info, 'st_file_attributes', 0) & 0x400 or
                    stat.S_ISDIR(info.st_mode) != directory or (not directory and info.st_nlink != 1)):
                raise ValueError('BootstrapHandle')
            if not directory:
                self.stream = os.fdopen(descriptor, 'rb')
        except BaseException:
            os.close(descriptor)
            raise

    def __enter__(self):
        return self

    def __exit__(self, *_):
        if self.stream:
            self.stream.close()
        else:
            os.close(self.descriptor)


def exact_paths(paths):
    if type(paths) not in (list, set, tuple) or not paths:
        raise ValueError('ExactPathSet')
    result, aliases = [], set()
    for value in paths:
        local_path(value)
        if value.casefold() in aliases:
            raise ValueError('PathAlias')
        aliases.add(value.casefold())
        result.append(value)
    return sorted(result)


def startup_files(python):
    directory = Path(python).parent
    candidates = {str(directory / (Path(python).stem + '._pth')),
                  str(directory / 'pyvenv.cfg'), str(directory.parent / 'pyvenv.cfg')}
    candidates.update(str(path.with_suffix('._pth')) for path in directory.glob('python*.dll') if path.is_file())
    return exact_paths(candidates)


def validate_installed_tools(plan):
    tools = plan['installedTools']
    editor = Path('C:/Program Files/Unity/Hub/Editor') / plan['editorVersion'] / 'Editor/Unity.exe'
    data = editor.parent / 'Data'
    required = {editor, data / 'Managed/UnityEditor.dll', data / 'netcorerun/netcorerun.exe',
                data / 'DotNetSdkRoslyn/csc.dll', data / 'il2cpp/build/deploy/il2cpp.exe',
                data / 'PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe',
                data / 'PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe'}
    if (type(tools) is not dict or set(tools) != {'editorExecutable', 'manifestSha256', 'files'} or
            tools['editorExecutable'] != str(editor) or type(tools['files']) is not dict or
            type(tools['manifestSha256']) is not str or not re.fullmatch('[0-9a-f]{64}', tools['manifestSha256'])):
        raise ValueError('InstalledToolsBinding')
    paths = exact_paths(list(tools['files']))
    if not required.issubset({Path(path) for path in paths}):
        raise ValueError('InstalledToolsIncomplete')
    for path, item in tools['files'].items():
        if (not Path(path).is_relative_to(editor.parent) or type(item) is not dict or
                set(item) != {'identity', 'sha256'} or type(item['identity']) is not list or
                len(item['identity']) != 2 or any(type(x) is not int for x in item['identity']) or
                type(item['sha256']) is not str or not re.fullmatch('[0-9a-f]{64}', item['sha256'])):
            raise ValueError('InstalledToolEntry')


def assemble(request, plan, bridge, runtime, absent, pins):
    """Pure, deterministic assembly; collection and all gates belong to Collector."""
    runtime, absent = exact_paths(runtime), exact_paths(absent)
    value = {name: request[name] for name in BINDING_FIELDS & request.keys()}
    value.update(schema=1, python=str(Path(sys.executable)),
        bridge=str(Path(request['checkout']) / 'tools/revival/signing_launcher/producer_bridge.py'),
        runtimeFiles=runtime, absentRuntimeFiles=absent, pins=pins)
    if set(value) != BINDING_FIELDS or type(value['schema']) is not int:
        raise ValueError('BindingFields')
    required = bridge.immutable_pin_paths(Path(request['checkout']), plan, value)
    supplied = exact_paths([pin['path'] for pin in pins])
    if set(supplied) != required:
        raise ValueError('ImmutableScope')
    by_path = {}
    for pin in pins:
        if (set(pin) != {'path', 'sha256', 'identity', 'sddl'} or
                type(pin['sha256']) is not str or not re.fullmatch('[0-9a-f]{64}', pin['sha256']) or
                type(pin['identity']) is not str or not re.fullmatch('[0-9a-f]{8}(?::[0-9a-f]{8}){6}', pin['identity']) or
                type(pin['sddl']) is not str or not pin['sddl']):
            raise ValueError('PinFields')
        by_path[pin['path']] = pin
    value['pins'] = [by_path[path] for path in supplied]
    manifest = {}
    for name in OWN_SOURCES:
        relative = 'tools/revival/signing_launcher/' + name
        digest = by_path[str(Path(request['checkout']) / relative)]['sha256']
        if digest != plan['before'][relative]['sha256']:
            raise ValueError('SourceDrift')
        manifest[name] = digest
    return encoded(value), encoded(manifest)


class ReadOnlyProbes:
    def __init__(self, request, base, now):
        self.request, self.base, self.now = request, base, now

    def environment(self):
        # A new child block only; process/user/system environments are untouched.
        result = {key: value for key, value in os.environ.items() if not key.upper().startswith(
            ('TAMER_', 'PYTHON', 'DOTNET_', 'COMPLUS_')) and key.upper() not in
            ('JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS', 'CLASSPATH')}
        result.update(GIT_OPTIONAL_LOCKS='0', GIT_TERMINAL_PROMPT='0')
        return result

    def timeout(self):
        budget(self.request, self.now())
        return min(120, (deadline(self.request['deadlineUtc']) - self.now()).total_seconds())

    def git(self, root, *args):
        return self.base.subprocess.check_output([self.request['git'], '-c', 'core.fsmonitor=false',
            '-c', 'core.untrackedCache=false', '-C', str(root), *args],
            env=self.environment(), timeout=self.timeout(), stderr=self.base.subprocess.DEVNULL).decode().strip()

    def closed(self, root):
        result = self.base.subprocess.run([self.request['powershell'], '-NoProfile', '-NonInteractive',
            '-File', str(root / 'tools/revival/signing_launcher/EditorOwnership.ps1'), '-Checkout', str(root)],
            env=self.environment(), timeout=self.timeout(), stdout=self.base.subprocess.DEVNULL,
            stderr=self.base.subprocess.DEVNULL)
        if result.returncode:
            raise ValueError('OwnerEditorNotClosed')


class Collector:
    """Real IO boundary. Tests replace only this boundary, never CLI gates."""
    def __init__(self, request, now):
        self.request, self.now = request, now
        self.stack = ExitStack()
        self.bootstrap_handles, self.handles, self.metadata = {}, {}, {}
        self.parents = set()
        self.directory_handles = {}
        self.producer_directories = set()
        self.reader = WindowsMetadata()

    def __enter__(self):
        return self

    def __exit__(self, *args):
        return self.stack.__exit__(*args)

    def lock_bootstrap(self, path, *, directory=False):
        path = canonical(str(path), directory=directory)
        for parent in (*reversed(path.parents), *((path,) if directory else ())):
            if str(parent).casefold() not in self.parents:
                self.directory_handles[str(parent)] = self.stack.enter_context(BootstrapFile(parent, directory=True))
                self.parents.add(str(parent).casefold())
        if directory:
            return None
        if str(path) not in self.bootstrap_handles:
            self.bootstrap_handles[str(path)] = self.stack.enter_context(BootstrapFile(path))
        return self.bootstrap_handles[str(path)]

    def snapshot(self, path, expected):
        budget(self.request, self.now())
        handle = self.lock_bootstrap(path)
        handle.stream.seek(0)
        raw = handle.stream.read(LIMIT + 1)
        if not 1 <= len(raw) <= LIMIT or sha(raw) != expected:
            raise ValueError('SnapshotBinding')
        return raw

    def private(self, path):
        self.bridge.require_private(path)

    def bootstrap(self):
        r = self.request
        self.runtime_configuration()
        for name in PATH_FIELDS:
            canonical(r[name], directory=name in ('checkout', 'classDirectory', 'outputDirectory'))
        self.lock_bootstrap(Path(r['outputDirectory']), directory=True)
        self.bootstrap_private()
        self.outputs_absent()
        self.plan_raw = self.snapshot(Path(r['plan']), r['planSha256'])
        self.review_raw = self.snapshot(Path(r['sourceReview']), r['sourceReviewSha256'])
        plan, review = strict_json(self.plan_raw), strict_json(self.review_raw)
        if (type(plan) is not dict or type(plan.get('schema')) is not int or plan['schema'] != 2 or
                plan.get('mode') != 'approved_adult_release' or
                plan.get('checkout') != r['checkout'] or plan.get('head') != r['head'] or plan.get('branch') != r['branch'] or
                type(plan.get('before')) is not dict or type(review) is not dict or
                type(review.get('schema')) is not int or review['schema'] != 1 or
                review.get('sourceHead') != r['head'] or type(review.get('sourceSha256')) is not dict or
                any(review.get(k) is not True for k in
                    ('minimumChecksPassed', 'pureCompilationPassed', 'independentSourceReviewPassed'))):
            raise ValueError('ExistingApprovedReview')
        self.raw_plan, self.review = plan, review
        source_pins = []
        root = Path(r['checkout'])
        # Only fixed public sources execute. Every source is locked and bound to
        # both reviewed snapshots before bridge/helper compile/exec.
        names = ['tools/revival/' + name for name in HELPERS]
        names += ['tools/revival/signing_launcher/' + name for name in (*OWN_SOURCES, 'prepare_binding.py')]
        for relative in names:
            expected = review['sourceSha256'].get(relative)
            if (type(expected) is not str or not re.fullmatch('[0-9a-f]{64}', expected) or
                    plan['before'].get(relative, {}).get('sha256') != expected):
                raise ValueError('ReviewedSourceScope')
            raw = self.snapshot(root / relative, expected)
            source_pins.append({'path': str(root / relative), 'sha256': sha(raw)})
        if root / 'tools/revival/signing_launcher/prepare_binding.py' != Path(__file__).resolve():
            raise ValueError('PreparerCheckout')
        bridge_path = root / 'tools/revival/signing_launcher/producer_bridge.py'
        self.bridge = types.ModuleType('prepared_launcher_bridge')
        self.bridge.__file__ = str(bridge_path)
        exec(compile(self.snapshot(bridge_path, review['sourceSha256']['tools/revival/signing_launcher/producer_bridge.py']),
                     str(bridge_path), 'exec', dont_inherit=True), self.bridge.__dict__)
        self.bridge.load_helpers({'pins': source_pins})
        self.producer, self.base = self.bridge.producer, self.bridge.base
        for path in (Path(r['outputDirectory']), Path(r['plan']).parent):
            self.private(path)
        for name in ('plan', 'sourceReview', 'currentUploadCertificateEvidence', 'existingKeyOwnershipEvidence'):
            self.private(Path(r[name]).parent)
            self.private(Path(r[name]))
        for prefix in ('currentUploadCertificateEvidence', 'existingKeyOwnershipEvidence'):
            self.snapshot(Path(r[prefix]), r[prefix + 'Sha256'])
        for name in ('git', 'powershell'):
            self.pin(Path(r[name]))  # retain the exact probe executable before use
        probes = self.probes()
        self.base.git, self.base.require_editor_closed = probes.git, probes.closed

    def bootstrap_private(self):
        import msvcrt
        handle = self.directory_handles[str(Path(self.request['outputDirectory']))]
        self.reader.require_private(msvcrt.get_osfhandle(handle.descriptor))

    def probes(self):
        return ReadOnlyProbes(self.request, self.base, self.now)

    def approved(self):
        r, root, plan = self.request, Path(self.request['checkout']), self.raw_plan
        config = canonical(plan['config'])
        # This gate precedes inventory/key reads and runtime enumeration/hash.
        sys.modules['private_ads_approved'].preflight_approved(root, config, r['head'], plan['editorVersion'])
        if self.base.git(root, 'branch', '--show-current') != r['branch']:
            raise ValueError('BranchChanged')
        self.plan = self.producer.load_plan(root, Path(r['plan']), r['planSha256'])
        if self.plan != self.raw_plan:
            raise ValueError('PlanDrift')
        self.producer.require_source_review(root, self.plan, Path(r['sourceReview']))
        self.producer.require_unchanged(root, self.plan)
        self.installed()
        if self.producer.KEY not in self.plan['before']:
            raise ValueError('OriginalKeyMissing')
        self.fresh_run()

    def installed(self):
        validate_installed_tools(self.plan)
        self.producer.pin_installed_tools(self.stack, self.plan['installedTools'])

    def outputs_absent(self):
        folder = Path(self.request['outputDirectory'])
        if any(os.path.lexists(folder / name) for name in (BINDING_NAME, MANIFEST_NAME)):
            raise FileExistsError('OutputCollision')

    def fresh_run(self):
        run = self.base.safe_path(Path(self.request['checkout']), self.plan['run'])
        self.hold_directory(run)
        self.private(run)
        if any(os.path.lexists(run / name) for name in
               (self.bridge.CLAIM, *self.bridge.COLLISIONS, self.producer.artifact_name(self.plan['mode']))):
            raise ValueError('RunAlreadyAttempted')

    def hold_directory(self, path):
        key = str(path).casefold()
        if key not in self.producer_directories:
            self.stack.enter_context(self.producer.locked_directories([path]))
            self.producer_directories.add(key)

    def pin(self, path):
        budget(self.request, self.now())
        path = canonical(str(path))
        key = str(path)
        if key not in self.handles:
            self.hold_directory(path.parent)
            self.handles[key] = self.stack.enter_context(self.producer.LockedFile(path, deletable=False))
        value = self.reader.read(path, self.handles[key])
        if key in self.metadata and self.metadata[key] != value:
            raise ValueError('PinDrift')
        self.metadata[key] = value
        return value

    def runtime_configuration(self):
        if (sys.flags.isolated != 1 or sys.flags.no_site != 1 or sys.flags.dont_write_bytecode != 1 or
                sys.pycache_prefix is None or Path(sys.pycache_prefix) != Path(self.request['plan']) or
                Path(sys.executable).parent != Path(sys.base_prefix)):
            raise ValueError('StandaloneIsolatedPython')

    def runtime(self):
        self.runtime_configuration()
        return exact_paths(self.bridge.runtime_files())

    def absent(self):
        result = startup_files(sys.executable)
        for value in result:
            self.hold_directory(Path(value).parent)
            if os.path.lexists(value):
                raise ValueError('RuntimeStartupRedirect')
        return result

    def collect(self):
        r = self.request
        self.runtime_paths, self.absent_paths = self.runtime(), self.absent()
        value = dict(r, python=str(Path(sys.executable)), runtimeFiles=self.runtime_paths)
        required = self.bridge.immutable_pin_paths(Path(r['checkout']), self.plan, value)
        paths = exact_paths(required)
        pins = [self.pin(Path(path)) for path in paths]
        # Match metadata collected from the held handles to existing provenance.
        expected = {str(Path(r['checkout']) / path): item['sha256']
                    for path, item in self.plan['before'].items() if str(Path(r['checkout']) / path) in required}
        expected.update({r['plan']: r['planSha256'], r['sourceReview']: r['sourceReviewSha256'],
                         r['certificate']: r['certificateSha256'], self.plan['config']: self.plan['configSha256']})
        expected.update({path: item['sha256'] for path, item in self.plan['installedTools']['files'].items()})
        if any(self.metadata[path]['sha256'] != digest for path, digest in expected.items()):
            raise ValueError('ExistingPinBinding')
        return assemble(r, self.plan, self.bridge, self.runtime_paths, self.absent_paths, pins)

    def recheck(self):
        budget(self.request, self.now())
        for path, handle in self.bootstrap_handles.items():
            handle.stream.seek(0)
            current = handle.stream.read(LIMIT + 1)
            if path == self.request['plan']:
                expected = self.plan_raw
            elif path == self.request['sourceReview']:
                expected = self.review_raw
            else:
                # Exact source/evidence snapshots remain held, and these
                # digests were verified before any repository code executed.
                expected_sha = self.snapshot_hashes[path]
                if sha(current) != expected_sha:
                    raise ValueError('SnapshotDrift')
                continue
            if current != expected:
                raise ValueError('SnapshotDrift')
        for path in list(self.handles):
            self.pin(Path(path))
        if self.runtime() != self.runtime_paths or self.absent() != self.absent_paths:
            raise ValueError('RuntimeScopeDrift')
        self.producer.require_unchanged(Path(self.request['checkout']), self.plan)
        sys.modules['private_ads_approved'].preflight_approved(Path(self.request['checkout']),
            Path(self.plan['config']), self.request['head'], self.plan['editorVersion'])
        if self.base.git(Path(self.request['checkout']), 'branch', '--show-current') != self.request['branch']:
            raise ValueError('BranchChanged')
        self.fresh_run()
        self.outputs_absent()

    def save(self, name, raw):
        budget(self.request, self.now())
        path = Path(self.request['outputDirectory']) / name
        self.private(path.parent)
        with path.open('xb') as stream:
            if stream.write(raw) != len(raw):
                raise OSError('PartialMetadataWrite')
            stream.flush()
            os.fsync(stream.fileno())
        self.private(path)
        pinned = self.pin(path)
        if pinned['sha256'] != sha(raw):
            raise ValueError('SavedMetadataDrift')
        return pinned['sha256']

    def verify_saved(self):
        budget(self.request, self.now())
        for path in list(self.handles):
            self.pin(Path(path))
        for name in (BINDING_NAME, MANIFEST_NAME):
            self.private(Path(self.request['outputDirectory']) / name)
        self.private(Path(self.request['outputDirectory']))
        if self.runtime() != self.runtime_paths or self.absent() != self.absent_paths:
            raise ValueError('RuntimeScopeDrift')
        self.fresh_run()


def prepare_binding(request, *, collector=None, now_utc=None):
    """Collector injection is a test boundary; the CLI always uses real IO."""
    now = now_utc if now_utc is not None else lambda: datetime.now(timezone.utc)
    validate_request(request, now())
    factory = Collector if collector is None else collector
    with factory(request, now) as collected:
        collected.bootstrap()
        # Save bootstrap digests once; subsequent reads never redefine them.
        collected.snapshot_hashes = {}
        for path, handle in collected.bootstrap_handles.items():
            handle.stream.seek(0)
            collected.snapshot_hashes[path] = sha(handle.stream.read(LIMIT + 1))
        collected.approved()
        binding_raw, manifest_raw = collected.collect()
        collected.recheck()
        binding_sha = collected.save(BINDING_NAME, binding_raw)
        # Existing strict launcher parser consumes the persisted exact bytes.
        collected.bridge.binding(Path(request['outputDirectory']) / BINDING_NAME, binding_sha)
        manifest_sha = collected.save(MANIFEST_NAME, manifest_raw)
        collected.verify_saved()
        budget(request, now())
        return {'schema': 1, 'metadataPrepared': True, 'bindingSha256': binding_sha,
                'sourceManifestSha256': manifest_sha, 'pinCount': len(strict_json(binding_raw)['pins']),
                'runtimeFileCount': len(collected.runtime_paths), 'actualBuildExecuted': False,
                'binaryVerified': False, 'distributable': False}


class SilentParser(argparse.ArgumentParser):
    def error(self, message):
        raise ValueError('ArgumentsRejected')


class Discard(io.TextIOBase):
    def write(self, value):
        return len(value)


def arguments(argv):
    seen = set()
    for token in argv:
        if token.startswith('--'):
            name = token.split('=', 1)[0]
            if name in seen:
                raise ValueError('DuplicateArgument')
            seen.add(name)
    parser = SilentParser(add_help=False, allow_abbrev=False)
    options = {'head': 'expected-head', 'branch': 'expected-branch'}
    for name in sorted(REQUEST_FIELDS):
        option = options.get(name, re.sub(r'([A-Z])', lambda m: '-' + m[1].lower(), name))
        if name in ('currentUploadCertificateReviewed', 'existingKeyOwnershipReviewed'):
            parser.add_argument('--' + option, dest=name, action='store_true', default=False)
        else:
            parser.add_argument('--' + option, dest=name, required=True,
                                type=int if name in ('minimumStartBudgetSeconds', 'producerTimeoutSeconds') else str)
    # No default true, password, config/mode, claim/run or debug gate bypass flags.
    return vars(parser.parse_args(argv))


def main(argv=None):
    result, code = None, 1
    try:
        with redirect_stdout(Discard()), redirect_stderr(Discard()):
            request = arguments(sys.argv[1:] if argv is None else argv)
            result = prepare_binding(request)
        code = 0
    except BaseException as failure:
        result = {'schema': 1, 'status': 'failed', 'exceptionType': type(failure).__name__,
                  'metadataPrepared': False, 'actualBuildExecuted': False,
                  'binaryVerified': False, 'distributable': False}
    sys.stdout.write(json.dumps(result, separators=(',', ':')))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
