"""Small synthetic fixtures only; no real runtime/key/config/approval inputs."""
import argparse
from contextlib import ExitStack
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import sys
import types
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parent.parent
NOW = datetime(2030, 1, 1, tzinfo=timezone.utc)


def load(path, name):
    module = types.ModuleType(name)
    module.__file__ = str(path)
    exec(compile(path.read_bytes(), str(path), 'exec', dont_inherit=True), module.__dict__)
    return module


PREP = load(SOURCE / 'prepare_binding.py', 'tested_preparer')
BRIDGE = load(SOURCE / 'producer_bridge.py', 'tested_bridge')
BRIDGE.load_helpers({'pins': [{'path': str(BRIDGE.HELPERS / name),
    'sha256': PREP.sha((BRIDGE.HELPERS / name).read_bytes())} for name in BRIDGE.SOURCES]})


class Fixture:
    def __init__(self, folder):
        self.folder, self.root = folder, folder / 'checkout'
        self.root.mkdir(parents=True)
        files = ['tools/revival/' + name for name in PREP.HELPERS]
        files += ['tools/revival/signing_launcher/' + name for name in (*PREP.OWN_SOURCES, 'prepare_binding.py')]
        files += ['tools/revival/private_ads_build/' + name for name in BRIDGE.base.TEMPLATES]
        for relative in files:
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(SOURCE.parent.parent.parent / relative, path)
        self.prep = load(self.root / 'tools/revival/signing_launcher/prepare_binding.py', 'fixture_preparer')
        self.run = self.root / '.revival-local/private-ads-build' / ('1' * 32)
        self.run.mkdir(parents=True)
        self.out = folder / 'output'
        self.out.mkdir()
        self.config = self.root / '.revival-local/config.private.json'
        contract = {'schema': 1, 'mode': 'adult_only_release', 'packageId': 'com.example.synthetic',
            'androidAppId': 'ca-app-pub-1234567890123456~1234567890',
            'productionRewardedAdUnit': 'ca-app-pub-1234567890123456/1234567890',
            'countryCodes': 'KR', 'sourceHead': 'a' * 40}
        approved = sys.modules['private_ads_approved']
        contract.update({name: 'b' * 64 for name in approved.REFERENCES})
        contract.update({name: True for name in approved.TRUE_FLAGS})
        contract.update({name: False for name in approved.FALSE_FLAGS})
        self.config.write_bytes(PREP.encoded(contract))
        self.write('Assets/Scripts/Advertising/AdRequestPolicy.cs', 'public const bool ProductionAdsEnabled = true;\n')
        self.write('Assets/Scripts/Advertising/AgeTreatmentPolicy.cs', '\n'.join([
            'public static bool RegionalConsentReviewed => true;',
            'private const bool AdultConsentReviewed = true;',
            'public static bool PrivacySdkEnvironmentReviewed => false;',
            *('private const bool ' + cohort + 'ConsentReviewed = false;' for cohort in ('Under13', 'From13To15', 'From16To17'))]) + '\n')
        literals = [PREP.sha(self.config.read_bytes()), contract['sourceHead'], *(contract[k] for k in approved.REFERENCES)]
        self.write('Assets/Scripts/Advertising/PrivateAdsReleaseContract.cs',
            'private static readonly Binding ApprovedRelease = new Binding(' + ','.join('"' + p + '"' for p in literals) + ');\n')
        self.write('Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset', 'adMobAndroidAppId: ' + contract['androidAppId'])
        self.write('Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml',
            '<manifest xmlns:android="http://schemas.android.com/apk/res/android"><application><meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="' + contract['androidAppId'] + '"/></application></manifest>')
        self.write('ProjectSettings/ProjectSettings.asset', '  applicationIdentifier:\n    Android: ' + contract['packageId'] + '\n')
        self.write('ProjectSettings/ProjectVersion.txt', 'm_EditorVersion: synthetic\n')
        (self.root / 'Packages').mkdir()
        self.key = self.write('src/AeDeong.keystore', 'SYNTHETIC KEY BYTES ONLY')
        self.cert = folder / 'synthetic.der'
        self.cert.write_bytes(b'SYNTHETIC CERT BYTES ONLY')
        self.evidence = folder / 'existing-review.txt'
        self.evidence.write_bytes(b'SYNTHETIC EXISTING REVIEW FACTS')
        self.runtime = folder / 'runtime'
        self.runtime.mkdir()
        self.python = self.runtime / 'python.exe'
        self.python.write_bytes(b'SYNTHETIC PYTHON')
        (self.runtime / 'python312.dll').write_bytes(b'SYNTHETIC DLL')
        (self.runtime / 'Lib').mkdir()
        (self.runtime / 'Lib/synthetic.py').write_bytes(b'# synthetic runtime\n')
        self.tools = folder / 'tools'
        self.tools.mkdir()
        for name in ('git.exe', 'pwsh.exe', 'java.exe', 'Unity.exe'):
            (self.tools / name).write_bytes(b'SYNTHETIC TOOL ' + name.encode())
        self.classes = folder / 'worker'
        self.classes.mkdir()
        (self.classes / 'OperatingKeyPreflight.class').write_bytes(b'SYNTHETIC CLASS')
        before = BRIDGE.producer.inventory(self.root, self.config)
        for index, (relative, entry) in enumerate(before.items()):
            if relative == BRIDGE.producer.KEY:
                continue
            backup = self.run / ('before-' + str(index) + '.snapshot')
            backup.write_bytes((self.root / relative).read_bytes())
            entry['backup'] = backup.relative_to(self.root).as_posix()
        hook = BRIDGE.base.HOOK + '_' + '1' * 32
        staged = {hook + '.meta': b'SYNTHETIC META', BRIDGE.producer.RESOURCE: self.config.read_bytes(),
                  BRIDGE.producer.RESOURCE + '.meta': b'SYNTHETIC RESOURCE META'}
        for name in BRIDGE.base.TEMPLATES:
            staged[hook + '/' + name] = (SOURCE.parent / 'private_ads_build' / name).read_bytes()
            staged[hook + '/' + name + '.meta'] = b'SYNTHETIC TEMPLATE META'
        owned = {}
        for index, (relative, raw) in enumerate(staged.items()):
            payload = self.run / ('stage-' + str(index) + '.snapshot')
            payload.write_bytes(raw)
            owned[relative] = {'sha256': PREP.sha(raw), 'payload': payload.relative_to(self.root).as_posix(),
                               'absentBefore': True, 'writer': 'producer'}
        editor = self.tools / 'Unity.exe'
        self.plan = {'schema': 2, 'receiptModeVersion': 1, 'mode': 'approved_adult_release',
            'checkout': str(self.root), 'head': 'a' * 40, 'branch': 'codex/synthetic',
            'editorVersion': 'synthetic', 'runId': '1' * 32, 'run': self.run.relative_to(self.root).as_posix(),
            'hook': hook, 'config': str(self.config), 'configSha256': PREP.sha(self.config.read_bytes()),
            'resourceSha256': PREP.sha(self.config.read_bytes()), 'before': before, 'owned': owned,
            'installedTools': {'editorExecutable': str(editor), 'manifestSha256': 'e' * 64,
                'files': {str(editor): {'identity': list(BRIDGE.base.identity(editor)), 'sha256': PREP.sha(editor.read_bytes())}}},
            'producerPrepared': False, 'binaryVerified': False, 'distributable': False}
        self.plan_path, self.review_path = self.run / 'plan.private.json', self.run / 'source-review.private.json'
        self.review = {'schema': 1, 'sourceHead': self.plan['head'],
            'sourceSha256': {p: entry['sha256'] for p, entry in before.items() if p.startswith('tools/revival/')},
            'minimumChecksPassed': True, 'pureCompilationPassed': True, 'independentSourceReviewPassed': True}
        self.plan_path.write_bytes(PREP.encoded(self.plan))
        self.review_path.write_bytes(PREP.encoded(self.review))
        self.request = {'checkout': str(self.root), 'head': self.plan['head'], 'branch': self.plan['branch'],
            'plan': str(self.plan_path), 'planSha256': PREP.sha(self.plan_path.read_bytes()),
            'sourceReview': str(self.review_path), 'sourceReviewSha256': PREP.sha(self.review_path.read_bytes()),
            'certificate': str(self.cert), 'certificateSha256': PREP.sha(self.cert.read_bytes()), 'alias': '합성-alias',
            'git': str(self.tools / 'git.exe'), 'powershell': str(self.tools / 'pwsh.exe'), 'java': str(self.tools / 'java.exe'),
            'classDirectory': str(self.classes), 'outputDirectory': str(self.out),
            'deadlineUtc': (NOW + timedelta(hours=1)).isoformat().replace('+00:00', 'Z'),
            'minimumStartBudgetSeconds': 600, 'producerTimeoutSeconds': 86400,
            'currentUploadCertificateReviewed': True, 'existingKeyOwnershipReviewed': True,
            'currentUploadCertificateEvidence': str(self.evidence), 'existingKeyOwnershipEvidence': str(self.evidence),
            'currentUploadCertificateEvidenceSha256': PREP.sha(self.evidence.read_bytes()),
            'existingKeyOwnershipEvidenceSha256': PREP.sha(self.evidence.read_bytes())}
        self.prep.sys = types.SimpleNamespace(executable=str(self.python), base_prefix=str(self.runtime),
            platform='win32', flags=types.SimpleNamespace(isolated=1, no_site=1, dont_write_bytecode=1),
            pycache_prefix=str(self.plan_path), modules=sys.modules)
        self.events, self.corrupt, self.phase_hook = [], None, None
        fixture, module = self, self.prep
        class FakeReader:
            def read(self, path, locked):
                fixture.events.append(('read', str(path)))
                info = os.fstat(locked.stream.fileno())
                result = {'path': str(path), 'sha256': PREP.sha(locked.bytes()),
                    'identity': ':'.join(format(x & 0xffffffff, '08x') for x in (info.st_dev, 0, info.st_ino, 0, info.st_size, 0, info.st_mtime_ns)),
                    'sddl': 'O:SYG:SYD:P(A;;FA;;;SY)'}
                if fixture.corrupt:
                    fixture.corrupt(result)
                return result
        class Probes:
            def git(self, root, *args):
                fixture.events.append(('git', args))
                if args == ('rev-parse', '--show-toplevel'): return str(root)
                if args == ('rev-parse', 'HEAD'): return fixture.request['head']
                if args == ('branch', '--show-current'): return fixture.request['branch']
                if args[0] == 'check-ignore': return 'ignored'
                if args == ('status', '--porcelain'): return ''
                raise AssertionError('Unexpected probe')
            def closed(self, root):
                fixture.events.append(('closed', str(root)))
        class FakeCollector(module.Collector):
            def __init__(self, request, now):
                super().__init__(request, now)
                self.reader = FakeReader()
                fixture.last_collector = self
            def bootstrap_private(self):
                fixture.events.append(('private-output-before-input', None))
            def bootstrap(self):
                super().bootstrap()
                def forbidden(*args, **kwargs): raise AssertionError('ExecutionForbidden')
                for name in ('prepare_plan', 'create_private_directory', 'build_once'):
                    setattr(self.producer, name, forbidden)
                for name in ('check', 'execute', 'challenge', 'require_owned_job'):
                    setattr(self.bridge, name, forbidden)
            def private(self, path):
                fixture.events.append(('private', str(path)))
            def probes(self): return Probes()
            def installed(self):
                self.producer.pin_installed_tools(self.stack, self.plan['installedTools'])
            def runtime(self):
                fixture.events.append(('runtime', None))
                self.bridge.sys = types.SimpleNamespace(base_prefix=str(fixture.runtime), executable=str(fixture.python))
                return module.exact_paths(self.bridge.runtime_files())
            def collect(self):
                if fixture.phase_hook: fixture.phase_hook(self, 'collect')
                return super().collect()
            def recheck(self):
                if fixture.phase_hook: fixture.phase_hook(self, 'recheck')
                return super().recheck()
            def save(self, name, raw):
                if fixture.phase_hook: fixture.phase_hook(self, name)
                return super().save(name, raw)
        self.collector = FakeCollector

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')
        return path

    def rewrite_plan(self):
        self.plan_path.write_bytes(PREP.encoded(self.plan))
        self.request['planSha256'] = PREP.sha(self.plan_path.read_bytes())

    def rewrite_review(self):
        self.review_path.write_bytes(PREP.encoded(self.review))
        self.request['sourceReviewSha256'] = PREP.sha(self.review_path.read_bytes())

    def prepare(self):
        # Any accidental process, claim, challenge or producer execution fails.
        with patch('subprocess.Popen', side_effect=AssertionError('ProcessForbidden')):
            return self.prep.prepare_binding(self.request, collector=self.collector, now_utc=lambda: NOW)


class PrepareTests(unittest.TestCase):
    def setUp(self):
        self.fixture = Fixture(EVIDENCE / hashlib.sha256(self.id().encode()).hexdigest()[:12])

    def reject(self, error=ValueError):
        f = self.fixture
        with self.assertRaises(error): f.prepare()
        self.assertFalse((f.out / PREP.BINDING_NAME).exists())
        self.assertFalse((f.out / PREP.MANIFEST_NAME).exists())

    def test_synthetic_success_exact_binding_manifest_scope_and_no_execution(self):
        f = self.fixture
        result = f.prepare()
        self.assertTrue(result['metadataPrepared'])
        for name in ('actualBuildExecuted', 'binaryVerified', 'distributable'): self.assertFalse(result[name])
        binding_raw = (f.out / PREP.BINDING_NAME).read_bytes()
        value = f.last_collector.bridge.binding(f.out / PREP.BINDING_NAME, PREP.sha(binding_raw))
        self.assertEqual(set(value), PREP.BINDING_FIELDS)
        self.assertEqual(type(value['schema']), int)
        self.assertEqual(set(json.loads((f.out / PREP.MANIFEST_NAME).read_bytes())), set(PREP.OWN_SOURCES))
        self.assertEqual({pin['path'] for pin in value['pins']},
            f.last_collector.bridge.immutable_pin_paths(f.root, f.plan, value))
        self.assertFalse(any('existing-review.txt' in p['path'] for p in value['pins']))
        self.assertFalse(any(name in p['path'] for p in value['pins'] for name in ('Assets\\', 'ProjectSettings\\', 'Packages\\')))
        self.assertFalse(any((f.run / name).exists() for name in (BRIDGE.CLAIM, BRIDGE.SIGNING, BRIDGE.RESULT)))
        self.assertEqual(set(p.name for p in f.out.iterdir()), {PREP.BINDING_NAME, PREP.MANIFEST_NAME})

    def test_request_review_bool_is_not_inferred_from_evidence(self):
        for name in ('currentUploadCertificateReviewed', 'existingKeyOwnershipReviewed'):
            for value in (False, None, 1, 'true'):
                with self.subTest(name=name, value=value):
                    request = dict(self.fixture.request, **{name: value})
                    with self.assertRaises(ValueError): PREP.validate_request(request, NOW)
        self.assertEqual(self.fixture.events, [])

    def test_request_strict_types_alias_and_budgets_before_io(self):
        f = self.fixture
        for name, value in [('minimumStartBudgetSeconds', True), ('minimumStartBudgetSeconds', 599),
            ('producerTimeoutSeconds', True), ('producerTimeoutSeconds', 0), ('producerTimeoutSeconds', 86401),
            ('alias', ''), ('alias', 'bad\0alias'), ('alias', '\ud800'), ('alias', 'é' * 2049),
            ('head', 'A' * 40), ('certificateSha256', 'F' * 64), ('deadlineUtc', '2030-01-01')]:
            with self.subTest(name=name, value=repr(value)):
                with self.assertRaises((ValueError, UnicodeError)): PREP.validate_request(dict(f.request, **{name: value}), NOW)
        self.assertEqual(f.events, [])

    def test_expired_deadline_rejects_before_io(self):
        self.fixture.request['deadlineUtc'] = NOW.isoformat().replace('+00:00', 'Z')
        self.reject(TimeoutError)
        self.assertEqual(self.fixture.events, [])

    def test_missing_independent_evidence_rejects(self):
        self.fixture.request['existingKeyOwnershipEvidence'] = str(self.fixture.folder / 'missing.txt')
        self.reject(FileNotFoundError)

    def test_changed_independent_evidence_rejects_before_runtime_or_key(self):
        self.fixture.evidence.write_bytes(b'CHANGED')
        self.reject()
        self.assertFalse(any(event[0] == 'runtime' or event == ('read', str(self.fixture.key)) for event in self.fixture.events))

    def test_real_plan_loader_rejects_wrong_review_sha(self):
        f = self.fixture
        with self.assertRaises(ValueError): BRIDGE.producer.load_plan(f.root, f.plan_path, '0' * 64)
        f.request['planSha256'] = '0' * 64
        self.reject()

    def test_plan_head_branch_and_mode_must_match(self):
        f = self.fixture
        for name, value in (('head', '0' * 40), ('branch', 'codex/other'), ('mode', 'disabled_candidate')):
            old = f.plan[name]
            f.plan[name] = value
            f.rewrite_plan()
            self.reject()
            f.plan[name] = old
            f.rewrite_plan()

    def test_source_review_missing_false_and_old_scope_rejects(self):
        f = self.fixture
        for name in ('minimumChecksPassed', 'pureCompilationPassed', 'independentSourceReviewPassed'):
            f.review[name] = False
            f.rewrite_review()
            self.reject()
            f.review[name] = True
        f.review['sourceSha256'].pop('tools/revival/private_ads_producer.py')
        f.rewrite_review()
        self.reject()

    def test_review_schema_must_be_integer_one_and_plan_integer_two(self):
        f = self.fixture
        for value in (True, '1', 2):
            f.review['schema'] = value
            f.rewrite_review()
            self.reject()
        f.review['schema'] = 1
        f.rewrite_review()
        f.plan['schema'] = True
        f.rewrite_plan()
        self.reject()

    def test_real_source_review_rejects_extra_or_incomplete_source_scope(self):
        f = self.fixture
        f.review['sourceSha256']['tools/revival/unreviewed.py'] = '0' * 64
        f.rewrite_review()
        self.reject()

    def test_actual_false_advertising_gate_stops_before_inventory_key_runtime_output(self):
        f = self.fixture
        f.write('Assets/Scripts/Advertising/AdRequestPolicy.cs', 'public const bool ProductionAdsEnabled = false;\n')
        original = Path.read_bytes
        def guarded(path):
            if path == f.key: raise AssertionError('KeyReadBeforeApproval')
            return original(path)
        with patch.object(Path, 'read_bytes', guarded): self.reject()
        self.assertFalse(any(event[0] == 'runtime' or event == ('read', str(f.key)) for event in f.events))

    def test_actual_null_approved_binding_stops_before_runtime_key_output(self):
        f = self.fixture
        f.write('Assets/Scripts/Advertising/PrivateAdsReleaseContract.cs', 'private static readonly Binding ApprovedRelease = null;\n')
        original = Path.read_bytes
        def guarded(path):
            if path == f.key: raise AssertionError('KeyReadBeforeApproval')
            return original(path)
        with patch.object(Path, 'read_bytes', guarded): self.reject()
        self.assertFalse(any(event[0] == 'runtime' or event == ('read', str(f.key)) for event in f.events))

    def test_current_certificate_and_original_key_sha_reject_drift(self):
        f = self.fixture
        f.cert.write_bytes(b'CHANGED SYNTHETIC CERT')
        self.reject()
        f.cert.write_bytes(b'SYNTHETIC CERT BYTES ONLY')
        f.key.write_bytes(b'CHANGED SYNTHETIC KEY')
        self.reject()

    def test_absent_startup_files_and_directories_reject(self):
        f = self.fixture
        (f.runtime / 'python._pth').mkdir()
        self.reject()

    def test_absent_python_dll_redirect_rejects(self):
        (self.fixture.runtime / 'python312._pth').write_bytes(b'SYNTHETIC REDIRECT')
        self.reject()

    def test_parent_pyvenv_redirect_rejects(self):
        (self.fixture.folder / 'pyvenv.cfg').write_bytes(b'SYNTHETIC REDIRECT')
        self.reject()

    def test_runtime_scope_changes_before_publication_reject(self):
        f = self.fixture
        def hook(collector, phase):
            if phase == 'recheck': (f.runtime / 'Lib/new.py').write_bytes(b'# new synthetic source')
        f.phase_hook = hook
        self.reject()

    def test_pin_drift_during_collection_rejects(self):
        f = self.fixture
        counts = {}
        def corrupt(pin):
            counts[pin['path']] = counts.get(pin['path'], 0) + 1
            if pin['path'] == str(f.tools / 'git.exe') and counts[pin['path']] > 1: pin['identity'] = 'ffffffff:' * 6 + 'ffffffff'
        f.corrupt = corrupt
        self.reject()

    def test_source_sha_drift_rejects(self):
        f = self.fixture
        def corrupt(pin):
            if pin['path'].endswith('OwnedChild.cs'): pin['sha256'] = '0' * 64
        f.corrupt = corrupt
        self.reject()

    def test_claim_and_all_existing_attempt_outputs_reject_without_replacement(self):
        f = self.fixture
        original_folder = f.folder
        for index, name in enumerate((BRIDGE.CLAIM, *BRIDGE.COLLISIONS, BRIDGE.producer.artifact_name(f.plan['mode']))):
            path = f.run / name
            path.write_bytes(b'EXISTING SYNTHETIC MARKER')
            self.reject()
            self.assertEqual(path.read_bytes(), b'EXISTING SYNTHETIC MARKER')
            # Each case gets a separate fresh fixture; preserve all evidence.
            f = Fixture(original_folder.parent / (original_folder.name + '-next-' + str(index)))
            self.fixture = f

    def test_existing_output_is_preserved(self):
        f = self.fixture
        path = f.out / PREP.BINDING_NAME
        path.write_bytes(b'EXISTING SYNTHETIC OUTPUT')
        with self.assertRaises(FileExistsError): f.prepare()
        self.assertEqual(path.read_bytes(), b'EXISTING SYNTHETIC OUTPUT')
        self.assertFalse((f.out / PREP.MANIFEST_NAME).exists())

    def test_second_create_new_failure_preserves_first_output(self):
        f = self.fixture
        def hook(collector, phase):
            if phase == PREP.MANIFEST_NAME: (f.out / PREP.MANIFEST_NAME).write_bytes(b'RACING SYNTHETIC OUTPUT')
        f.phase_hook = hook
        with self.assertRaises(FileExistsError): f.prepare()
        self.assertTrue((f.out / PREP.BINDING_NAME).exists())
        self.assertEqual((f.out / PREP.MANIFEST_NAME).read_bytes(), b'RACING SYNTHETIC OUTPUT')

    def test_deadline_after_first_output_preserves_partial(self):
        f = self.fixture
        def hook(collector, phase):
            if phase == PREP.MANIFEST_NAME: collector.now = lambda: NOW + timedelta(hours=1)
        f.phase_hook = hook
        with self.assertRaises(TimeoutError): f.prepare()
        self.assertTrue((f.out / PREP.BINDING_NAME).exists())
        self.assertFalse((f.out / PREP.MANIFEST_NAME).exists())

    def test_output_acl_rejection_happens_before_plan_read(self):
        f = self.fixture
        class RejectAcl(f.collector):
            def bootstrap_private(self): raise ValueError('SyntheticAclRejected')
        f.collector = RejectAcl
        self.reject()
        self.assertEqual(f.events, [])

    def test_forward_slash_output_uses_the_locked_bootstrap_directory(self):
        f = self.fixture
        f.request['outputDirectory'] = f.out.as_posix()
        class ForwardSlashCollector(f.collector):
            def bootstrap_private(self):
                original = self.reader
                self.reader = f.prep.WindowsMetadata()
                try:
                    with patch.object(f.prep.WindowsMetadata, 'require_private',
                            side_effect=lambda handle: f.events.append(('locked-output-acl', handle))):
                        return f.prep.Collector.bootstrap_private(self)
                finally:
                    self.reader = original
        f.collector = ForwardSlashCollector
        self.assertTrue(f.prepare()['metadataPrepared'])
        self.assertEqual(len([event for event in f.events if event[0] == 'locked-output-acl']), 1)
        self.assertEqual(set(path.name for path in f.out.iterdir()), {PREP.BINDING_NAME, PREP.MANIFEST_NAME})

    def test_real_native_output_acl_rejects_broad_synthetic_directory(self):
        f = self.fixture
        class RealAcl(f.collector):
            def bootstrap_private(self):
                reader, self.reader = self.reader, f.prep.WindowsMetadata()
                try: super(f.collector, self).bootstrap_private()
                finally: self.reader = reader
        # Invoke the concrete bootstrap ACL method directly, preserving the
        # fake collection boundary for everything after this expected refusal.
        def rejected(self):
            import msvcrt
            handle = self.directory_handles[self.request['outputDirectory']]
            f.prep.WindowsMetadata().require_private(msvcrt.get_osfhandle(handle.descriptor))
        RealAcl.bootstrap_private = rejected
        f.collector = RealAcl
        self.reject()
        self.assertEqual(f.events, [])

    def test_exact_pin_scope_rejects_missing_extra_duplicate_and_case_alias(self):
        f = self.fixture
        f.prepare()
        value = PREP.strict_json((f.out / PREP.BINDING_NAME).read_bytes())
        pins = value['pins']
        for changed in (pins[:-1], pins + [dict(pins[0], path=str(f.folder / 'extra.bin'))],
                        pins + [dict(pins[0])], pins + [dict(pins[0], path=pins[0]['path'].swapcase())]):
            with self.subTest(count=len(changed)):
                with self.assertRaises(ValueError):
                    f.prep.assemble(f.request, f.plan, f.last_collector.bridge,
                        value['runtimeFiles'], value['absentRuntimeFiles'], changed)

    def test_runtime_enumeration_exact_small_tree_and_startup_candidates(self):
        f = self.fixture
        (f.runtime / 'DLLs').mkdir()
        (f.runtime / 'DLLs/synthetic.pyd').write_bytes(b'SYNTHETIC EXTENSION')
        (f.runtime / 'archive.zip').write_bytes(b'SYNTHETIC ZIP')
        (f.runtime / 'Lib/site-packages').mkdir()
        (f.runtime / 'Lib/site-packages/ignored.py').write_bytes(b'# ignored')
        (f.runtime / 'Lib/__pycache__').mkdir()
        (f.runtime / 'Lib/__pycache__/ignored.py').write_bytes(b'# ignored')
        (f.runtime / 'Lib/ignored.txt').write_bytes(b'ignored')
        bridge = load(f.root / 'tools/revival/signing_launcher/producer_bridge.py', 'runtime_fixture_bridge')
        bridge.sys = types.SimpleNamespace(base_prefix=str(f.runtime), executable=str(f.python))
        expected = {str(f.python), str(f.runtime / 'python312.dll'), str(f.runtime / 'archive.zip'),
                    str(f.runtime / 'Lib/synthetic.py'), str(f.runtime / 'DLLs/synthetic.pyd')}
        self.assertEqual(bridge.runtime_files(), expected)
        self.assertEqual(set(PREP.startup_files(str(f.python))), {str(f.runtime / 'python._pth'),
            str(f.runtime / 'python312._pth'), str(f.runtime / 'pyvenv.cfg'), str(f.folder / 'pyvenv.cfg')})

    def test_runtime_scope_drift_during_save_is_incomplete_with_outputs_preserved(self):
        f = self.fixture
        def hook(collector, phase):
            if phase == PREP.MANIFEST_NAME: (f.runtime / 'Lib/new.py').write_bytes(b'# synthetic new file')
        f.phase_hook = hook
        with self.assertRaises(ValueError): f.prepare()
        self.assertTrue((f.out / PREP.BINDING_NAME).exists())
        self.assertTrue((f.out / PREP.MANIFEST_NAME).exists())

    def test_synthetic_reparse_and_noncanonical_case_reject(self):
        f = self.fixture
        original = Path.lstat
        def linked(path):
            if path == f.key.parent:
                return types.SimpleNamespace(st_mode=0o040000, st_file_attributes=0x400)
            return original(path)
        with patch.object(Path, 'lstat', linked):
            with self.assertRaises(ValueError): PREP.canonical(str(f.key))
        with self.assertRaises(ValueError): PREP.canonical(str(f.key.with_name('aedeong.keystore')))

    def test_all_preparer_environment_writes_are_trapped(self):
        f = self.fixture
        before = dict(os.environ)
        class Guard(dict):
            def __setitem__(self, *args): raise AssertionError('EnvironmentWriteForbidden')
            def __delitem__(self, *args): raise AssertionError('EnvironmentWriteForbidden')
            def pop(self, *args): raise AssertionError('EnvironmentWriteForbidden')
            def update(self, *args, **kwargs): raise AssertionError('EnvironmentWriteForbidden')
            def clear(self): raise AssertionError('EnvironmentWriteForbidden')
            def setdefault(self, *args): raise AssertionError('EnvironmentWriteForbidden')
        with patch.object(os, 'environ', Guard(before)):
            self.assertTrue(f.prepare()['metadataPrepared'])
        self.assertEqual(dict(os.environ), before)


class PureTests(unittest.TestCase):
    def test_duplicate_properties_nonfinite_and_bad_utf8_are_rejected(self):
        for raw in (b'{"schema":1,"schema":1}', b'{"x":NaN}', b'{"x":Infinity}', b'"\xff"'):
            with self.subTest(raw=raw):
                with self.assertRaises((ValueError, UnicodeError)): PREP.strict_json(raw)

    def test_metadata_size_bound(self):
        with self.assertRaises(ValueError): PREP.strict_json(b'')
        with self.assertRaises(ValueError): PREP.encoded({'x': 'x' * PREP.LIMIT})

    def test_path_traversal_unc_ads_devices_and_alias_sets_reject(self):
        for path in ('relative', '//server/share/file', 'C:/a/../b', 'C:/a/./b', 'C:/a:stream', 'C:/CON', 'C:/bad.', 'C:/bad*/x'):
            with self.subTest(path=path):
                with self.assertRaises(ValueError): PREP.local_path(path)
        with self.assertRaises(ValueError): PREP.exact_paths(['C:\\a', 'C:\\A'])
        with self.assertRaises(ValueError): PREP.exact_paths(['C:\\a', 'C:\\a'])

    def test_installed_tools_none_and_incomplete_reject_without_file_reads(self):
        for tools in (None, {}, {'editorExecutable': 'C:\\fake.exe', 'files': {}}):
            with self.subTest(tools=tools):
                with self.assertRaises(ValueError): PREP.validate_installed_tools({'editorVersion': 'synthetic', 'installedTools': tools})

    def test_installed_tools_exact_required_binaries_and_types(self):
        editor = Path('C:/Program Files/Unity/Hub/Editor/synthetic/Editor/Unity.exe')
        data = editor.parent / 'Data'
        paths = [editor, data / 'Managed/UnityEditor.dll', data / 'netcorerun/netcorerun.exe',
            data / 'DotNetSdkRoslyn/csc.dll', data / 'il2cpp/build/deploy/il2cpp.exe',
            data / 'PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe',
            data / 'PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe']
        tools = {'editorExecutable': str(editor), 'manifestSha256': 'a' * 64,
                 'files': {str(path): {'identity': [1, 2], 'sha256': 'b' * 64} for path in paths}}
        plan = {'editorVersion': 'synthetic', 'installedTools': tools}
        PREP.validate_installed_tools(plan)
        removed = tools['files'].pop(str(paths[-1]))
        with self.assertRaises(ValueError): PREP.validate_installed_tools(plan)
        tools['files'][str(paths[-1])] = removed
        tools['files'][str(editor)]['identity'] = [True, 2]
        with self.assertRaises(ValueError): PREP.validate_installed_tools(plan)

    def test_probe_child_environment_removes_injection_without_parent_writes(self):
        before = dict(os.environ)
        probes = PREP.ReadOnlyProbes({}, None, lambda: NOW)
        class Environment:
            def items(self): return [('TAMER_KEYSTORE_PASS', 'synthetic'), ('tamer_keyalias_pass', 'synthetic'),
                ('PYTHONPATH', 'synthetic'), ('JAVA_TOOL_OPTIONS', 'synthetic'), ('PATH', 'synthetic')]
        with patch.object(PREP, 'os', types.SimpleNamespace(environ=Environment())):
            self.assertEqual(probes.environment(), {'PATH': 'synthetic', 'GIT_OPTIONAL_LOCKS': '0', 'GIT_TERMINAL_PROMPT': '0'})
        self.assertEqual(dict(os.environ), before)

    def test_cli_rejects_unknown_or_gate_bypass_without_echoing_values(self):
        captured = io.StringIO()
        with patch.object(PREP.sys, 'stdout', captured):
            self.assertEqual(PREP.main(['--allow-reviewed-build', 'synthetic-sensitive-value']), 1)
        self.assertNotIn('synthetic-sensitive-value', captured.getvalue())
        self.assertFalse(json.loads(captured.getvalue())['metadataPrepared'])

    def test_cli_duplicate_and_legacy_fields_are_rejected(self):
        for argv in (['--alias=a', '--alias=b'], ['--config', 'synthetic'], ['--mode', 'approved_adult_release'],
                     ['--python', 'synthetic'], ['--password', 'synthetic'], ['--claim'], ['--run'], ['--debug']):
            with self.subTest(argv=argv):
                with self.assertRaises(ValueError): PREP.arguments(argv)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    args = parser.parse_args()
    EVIDENCE = args.evidence
    if not EVIDENCE.is_absolute() or not EVIDENCE.is_dir():
        raise ValueError('Existing synthetic evidence directory required')
    unittest.main(argv=[sys.argv[0]], verbosity=2)
