"""Changed-scope tests only. All config/key/receipt data is synthetic."""
import argparse
from contextlib import ExitStack, nullcontext
import hashlib
import importlib.util
import io
import json
import marshal
import os
from pathlib import Path
import struct
import sys
import types
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('launcher_bridge_tested', SOURCE / 'producer_bridge.py')
bridge = types.ModuleType(spec.name)
bridge.__file__ = str(SOURCE / 'producer_bridge.py')
exec(compile(Path(bridge.__file__).read_bytes(), bridge.__file__, 'exec'), bridge.__dict__)


def framed(first='합성-store\n🔑', second='합성-key\r\né'):
    result = bytearray(struct.pack('>I', 0x544C5031))
    for value in (first, second):
        raw = value.encode('utf-16-be', errors='surrogatepass')
        result.extend(struct.pack('>I', len(raw) // 2))
        result.extend(raw)
    return result


class BridgeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        pins = [{'path': str(bridge.HELPERS / name),
                 'sha256': bridge.sha((bridge.HELPERS / name).read_bytes())} for name in bridge.SOURCES]
        bridge.load_helpers({'pins': pins})

    def setUp(self):
        self.folder = EVIDENCE / hashlib.sha256(self.id().encode()).hexdigest()[:8]
        self.folder.mkdir()
        self.root = self.folder
        self.run = self.root / '.revival-local' / 'private-ads-build' / ('1' * 32)
        self.run.mkdir(parents=True)
        self.plan_path = self.run / 'plan.private.json'
        self.plan_path.write_text('{}')
        self.source = self.run / 'source.private.json'
        self.source.write_text('{}')
        self.plan = {'head': 'a' * 40, 'branch': 'codex/synthetic', 'mode': 'approved_adult_release',
                     'checkout': str(self.root), 'run': self.run.relative_to(self.root).as_posix(),
                     'config': str(self.run / 'config.private.json'), 'editorVersion': 'synthetic',
                     'before': {bridge.producer.KEY: {'sha256': 'b' * 64}}}
        self.value = {'checkout': str(self.root), 'head': 'a' * 40, 'branch': 'codex/synthetic',
                      'plan': str(self.plan_path), 'planSha256': 'c' * 64,
                      'sourceReview': str(self.source), 'sourceReviewSha256': bridge.sha(self.source.read_bytes()),
                      'python': sys.executable, 'runtimeFiles': [], 'alias': 'synthetic-alias',
                      'certificateSha256': 'd' * 64}

    def test_framing_unicode_partial_NUL_surrogate_and_trailing(self):
        self.assertEqual(bridge.passwords(io.BytesIO(framed())), ['합성-store\n🔑', '합성-key\r\né'])
        for value in (framed()[:-1], framed() + b'x', framed('bad\0value'), framed('bad\ud800')):
            with self.assertRaises((ValueError, UnicodeDecodeError)):
                bridge.passwords(io.BytesIO(value))

    def test_source_loader_ignores_forged_unchecked_pyc(self):
        # A syntactically valid unchecked-hash cache would run its hostile code
        # under a normal import. The exact-source loader never consults it.
        fake = self.folder / 'helpers'
        fake.mkdir()
        pins = []
        for name in bridge.SOURCES:
            path = fake / name
            path.write_bytes(b'SOURCE_ONLY = True\n')
            pins.append({'path': str(path), 'sha256': bridge.sha(path.read_bytes())})
        cache = fake / '__pycache__'
        cache.mkdir()
        hostile = compile('raise RuntimeError("forged cache executed")', str(fake / 'windows_owned_files.py'), 'exec')
        bytecode = importlib.util.MAGIC_NUMBER + struct.pack('<I', 1) + b'12345678' + marshal.dumps(hostile)
        (cache / ('windows_owned_files.' + sys.implementation.cache_tag + '.pyc')).write_bytes(bytecode)
        saved = {name[:-3]: sys.modules[name[:-3]] for name in bridge.SOURCES}
        try:
            with patch.object(bridge, 'HELPERS', fake):
                # Last module's missing expected exports is acceptable here:
                # all six exact source modules were executed, no hostile cache.
                with self.assertRaises(AttributeError):
                    bridge.load_helpers({'pins': pins})
                self.assertTrue(sys.modules['windows_owned_files'].SOURCE_ONLY)
        finally:
            sys.modules.update(saved)
            bridge.base = saved['private_ads_build']
            bridge.producer = saved['private_ads_producer']
            bridge.require_private = saved['private_ads_evidence'].require_private

    def test_python_diagnostics_are_discarded_and_failure_fields_are_preserved(self):
        captured = io.StringIO()
        def rejected(*args, **kwargs):
            print('synthetic-sensitive-value')
            print('synthetic-sensitive-value', file=sys.stderr)
            raise bridge.StageFailure('SyntheticContractFailure')
        with ExitStack() as stack:
            stack.enter_context(patch.object(sys, 'argv', ['bridge', 'inspect', '--binding', str(self.plan_path),
                '--binding-sha256', 'a' * 64, '--attempt', 'e' * 32]))
            stack.enter_context(patch.object(bridge, 'binding', return_value=self.value))
            stack.enter_context(patch.object(bridge, 'load_helpers'))
            stack.enter_context(patch.object(bridge, 'require_private'))
            stack.enter_context(patch.object(bridge, 'configure_tools'))
            stack.enter_context(patch.object(bridge, 'check', side_effect=rejected))
            stack.enter_context(patch.object(sys, 'stdout', captured))
            self.assertEqual(bridge.main(), 1)
        self.assertNotIn('synthetic-sensitive-value', captured.getvalue())
        result = json.loads(captured.getvalue())
        self.assertEqual(result['phase'], 'preflight')
        self.assertEqual(result['exceptionType'], 'SyntheticContractFailure')
        self.assertFalse(result['producerInvoked'])
        self.assertFalse(result['actualBuildExecuted'])

    def gate_patches(self, stack):
        stack.enter_context(patch.object(bridge.sys, 'pycache_prefix', str(self.plan_path)))
        stack.enter_context(patch.object(bridge, 'runtime_files', return_value=set()))
        stack.enter_context(patch.object(bridge.producer, 'load_plan', return_value=self.plan))
        stack.enter_context(patch.object(sys.modules['private_ads_approved'], 'preflight_approved'))
        stack.enter_context(patch.object(bridge.base, 'git', return_value=self.value['branch']))
        stack.enter_context(patch.object(bridge.producer, 'require_unchanged'))

    def test_stale_plan_head_source_and_config_stop_before_claim(self):
        # Exact plan byte rejection uses the real loader, not a mirrored hash check.
        with self.assertRaises(ValueError):
            bridge.producer.load_plan(self.root, self.plan_path, '0' * 64)
        with ExitStack() as stack:
            self.gate_patches(stack)
            self.plan['head'] = '0' * 40
            with self.assertRaises(ValueError):
                bridge.check(self.value, 'e' * 32, claim=True)
            self.plan['head'] = self.value['head']
            self.value['sourceReviewSha256'] = '0' * 64
            with self.assertRaises(ValueError):
                bridge.check(self.value, 'e' * 32, claim=True)
            self.value['sourceReviewSha256'] = bridge.sha(self.source.read_bytes())
            sys.modules['private_ads_approved'].preflight_approved.side_effect = ValueError('Unapproved false/null gate')
            with self.assertRaises(ValueError):
                bridge.check(self.value, 'e' * 32, claim=True)
        self.assertFalse((self.run / bridge.CLAIM).exists())
        self.assertFalse((self.run / bridge.SIGNING).exists())

    def execution_patches(self, stack):
        stack.enter_context(patch.object(bridge, 'require_owned_job'))
        checked = stack.enter_context(patch.object(bridge, 'check', return_value=(self.plan, {})))
        challenge = stack.enter_context(patch.object(bridge, 'challenge'))
        build = stack.enter_context(patch.object(bridge.producer, 'build_once', return_value={'actualBuildExecuted': True}))
        return checked, challenge, build

    def test_challenge_failure_or_context_change_creates_no_receipt_or_producer(self):
        for change in ('challenge', 'context'):
            with ExitStack() as stack:
                checked, challenge, build = self.execution_patches(stack)
                if change == 'challenge':
                    challenge.side_effect = ValueError('synthetic-sensitive-value')
                else:
                    checked.side_effect = [(self.plan, {}), ValueError('Context changed')]
                with self.assertRaises(ValueError):
                    bridge.execute(self.value, 'e' * 32, io.BytesIO(framed()))
                self.assertEqual(build.call_count, 0)
                self.assertFalse((self.run / bridge.SIGNING).exists())
                self.assertFalse(any(name in os.environ for name in bridge.SECRET_NAMES))

    def test_receipt_follows_challenge_and_exact_plan_then_child_environment_only(self):
        order = []
        class Locked:
            def __enter__(self): return self
            def __exit__(self, *args): pass
            def verify(self, *args): order.append('receipt_pinned')
        def write(path, raw):
            order.append('receipt')
            self.assertEqual(order[:2], ['challenge', 'rechecked'])
            with path.open('xb') as output: output.write(raw)
        with ExitStack() as stack:
            checked, challenge, build = self.execution_patches(stack)
            checked.side_effect = lambda *args: (order.append('rechecked') or (self.plan, {})) if order else (self.plan, {})
            challenge.side_effect = lambda *args: order.append('challenge')
            stack.enter_context(patch.object(bridge.producer, 'locked_directories', return_value=nullcontext()))
            stack.enter_context(patch.object(bridge, 'require_private'))
            stack.enter_context(patch.object(bridge.producer, 'write_new', side_effect=write))
            stack.enter_context(patch.object(bridge.producer, 'LockedFile', return_value=Locked()))
            def invoked(*args, **kwargs):
                self.assertEqual([os.environ[name] for name in bridge.SECRET_NAMES], ['합성-store\n🔑', '합성-key\r\né'])
                return {'actualBuildExecuted': True}
            build.side_effect = invoked
            result = bridge.execute(self.value, 'e' * 32, io.BytesIO(framed()))
        self.assertEqual(result['status'], 'producer_completed')
        self.assertFalse(result['binaryVerified'])
        self.assertFalse(result['distributable'])
        receipt = json.loads((self.run / bridge.SIGNING).read_bytes())
        self.assertEqual(receipt['sourceHead'], self.value['head'])
        self.assertEqual(receipt['planSha256'], self.value['planSha256'])
        self.assertEqual(receipt['keystore'], bridge.producer.KEY)
        self.assertFalse(any(name in os.environ for name in bridge.SECRET_NAMES))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    args = parser.parse_args()
    EVIDENCE = args.evidence
    if not EVIDENCE.is_absolute() or not EVIDENCE.is_dir():
        raise ValueError('Existing isolated evidence directory required')
    # Main interpreter has -I -S -B; tests never invoke bridge.main or real build.
    unittest.main(argv=[sys.argv[0]], verbosity=2)
