"""Changed producer behavior only: synthetic files, no Unity/SDK/signing calls."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import private_ads_build as base
import private_ads_producer as producer


class ProducerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.config = self.root / '.revival-local/config.json'
        self.config.parent.mkdir()
        # Synthetic inventory, never used to issue an ad request.
        self.config.write_text(json.dumps({'checkout': str(self.root),
            'androidAppId': 'ca-app-pub-1111111111111111~2222222222',
            'productionRewardedAdUnit': 'ca-app-pub-1111111111111111/3333333333',
            'consoleInventoryConfirmed': True, 'productionActivationApproved': False,
            'regionalReviewApproved': False}))
        for p in ('Assets/Purchased/a.bin', 'Assets/Purchased/a.bin.meta',
                  'ProjectSettings/settings.asset', 'Packages/packages-lock.json',
                  producer.CLI, producer.KEY, 'Build/old.apk', 'tools/revival/helper.py'):
            file = self.root / p
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b'synthetic original')
        self.head = 'a' * 40

    def prepare(self):
        result = producer.prepare_plan(self.root, self.config, self.head, 'synthetic', branch='synthetic',
            preflight_result={'disabledPreparationValid': True, 'configSha256': base.digest(self.config.read_bytes())})
        plan_path = Path(result['plan'])
        return plan_path, result['planSha256'], producer.load_plan(self.root, plan_path, result['planSha256'])

    def stage_synthetic(self, plan):
        staged = {}
        for p, entry in plan['owned'].items():
            target = self.root / p
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((self.root / entry['payload']).read_bytes())
            staged[p] = {'identity': list(base.identity(target)), 'sha256': entry['sha256']}
        run = self.root / plan['run']
        producer.write_new(run / 'staged-owned.private.json', producer.encoded(staged))
        return run

    def review_recovery(self, plan, sha, delta, late=None):
        run = self.root / plan['run']
        producer.write_new(self.root / base.JOURNAL, producer.encoded({'run': plan['run'], 'planSha256': sha}))
        review = {'schema': 1, 'runId': plan['runId'], 'planSha256': sha,
            'deltaSha256': base.digest((run / 'actual-delta.private.json').read_bytes()),
            'restore': sorted(set(delta['changed']) & set(plan['before'])),
            'delete': sorted(set(delta['changed']) - set(plan['before'])), 'lateWriters': late or {}}
        file = run / 'recovery.private.json'
        producer.write_new(file, producer.encoded(review))
        return file, base.digest(file.read_bytes())

    def test_prepare_full_snapshots_without_subprocess_assets_or_secrets(self):
        before = {p.relative_to(self.root).as_posix(): p.read_bytes() for p in (self.root / 'Assets').rglob('*') if p.is_file()}
        with patch.object(producer.subprocess, 'run', side_effect=AssertionError('No subprocess')):
            path, sha, plan = self.prepare()
        self.assertFalse(plan['producerPrepared'])
        self.assertFalse(plan['binaryVerified'])
        self.assertIn('Assets/Purchased/a.bin.meta', plan['before'])
        self.assertIn(producer.KEY, plan['before'])
        self.assertIn('Build/old.apk', plan['before'])
        self.assertEqual(before, {p.relative_to(self.root).as_posix(): p.read_bytes() for p in (self.root / 'Assets').rglob('*') if p.is_file()})
        resource = (self.root / plan['owned'][producer.RESOURCE]['payload']).read_bytes()
        self.assertNotIn(str(self.root).encode(), resource)
        self.assertNotIn(b'password', path.read_bytes().lower())
        self.assertFalse(json.loads(resource)['adultConsentReviewed'])

    def test_changed_approval_rejects_resource_preparation(self):
        contract = json.loads(self.config.read_bytes())
        contract['productionActivationApproved'] = True
        self.config.write_text(json.dumps(contract))
        with self.assertRaises(ValueError):
            self.prepare()
        self.assertFalse((self.root / producer.RESOURCE).exists())

    def test_stale_config_tool_source_identity_rejected(self):
        for p in (producer.CLI, 'Assets/Purchased/a.bin', 'tools/revival/helper.py'):
            with self.subTest(path=p):
                path, sha, plan = self.prepare()
                file = self.root / p
                file.write_bytes(b'changed')
                with self.assertRaises(ValueError):
                    producer.require_unchanged(self.root, plan)
                file.write_bytes(b'synthetic original')

    def test_staging_collision_and_stale_active_journal_rejected(self):
        (self.root / producer.RESOURCE).parent.mkdir(parents=True)
        (self.root / producer.RESOURCE).write_bytes(b'other owner')
        with self.assertRaises(ValueError):
            self.prepare()
        self.assertEqual((self.root / producer.RESOURCE).read_bytes(), b'other owner')

    def test_missing_launch_permission_and_review_block_before_preflight(self):
        with patch.object(base, 'preflight', side_effect=AssertionError('No preflight')):
            with self.assertRaises(ValueError):
                base.execute(self.root, self.config, self.head, 'synthetic')
            with self.assertRaises(ValueError):
                producer.build_once(self.root, self.config, self.head, 'synthetic', None, None, None, None)
        self.assertFalse((self.root / base.JOURNAL).exists())

    def test_missing_signing_does_not_launch_or_write_marker(self):
        path, sha, plan = self.prepare()
        with patch.object(base, 'preflight'), patch.object(base, 'git', return_value='synthetic'), \
             patch.object(producer, 'require_source_review'), patch.object(producer.subprocess, 'run', side_effect=AssertionError('No launch')):
            with self.assertRaises((ValueError, TypeError)):
                producer.build_once(self.root, self.config, self.head, 'synthetic', path, sha,
                                    path, None, allow_unity_build=True)
        self.assertFalse((self.root / plan['run'] / 'launch-once.private.json').exists())

    def test_plan_review_digest_and_resource_mismatch_rejected(self):
        path, sha, plan = self.prepare()
        with self.assertRaises(ValueError):
            producer.load_plan(self.root, path, '0' * 64)
        plan['resourceSha256'] = '0' * 64
        path.write_bytes(producer.encoded(plan))
        with self.assertRaises(ValueError):
            producer.load_plan(self.root, path, base.digest(path.read_bytes()))

    def test_observation_archives_without_restore_or_delete(self):
        path, sha, plan = self.prepare()
        self.stage_synthetic(plan)
        original = self.root / 'ProjectSettings/settings.asset'
        original.write_bytes(b'actual changed')
        generated = self.root / 'Assets/generated.meta'
        generated.write_bytes(b'actual generated')
        delta = producer.observe(self.root, plan)
        self.assertEqual(original.read_bytes(), b'actual changed')
        self.assertEqual(generated.read_bytes(), b'actual generated')
        self.assertNotIn('archive', delta['after']['Assets/generated.meta'])
        self.assertEqual((self.root / delta['changed']['Assets/generated.meta']['archive']).read_bytes(), b'actual generated')
        self.assertFalse(delta['binaryVerified'])

    def test_unknown_late_writer_recovery_preserves_all_bytes(self):
        path, sha, plan = self.prepare()
        self.stage_synthetic(plan)
        generated = self.root / 'Assets/generated.meta'
        generated.write_bytes(b'unknown writer')
        delta = producer.observe(self.root, plan)
        review, review_sha = self.review_recovery(plan, sha, delta)
        with patch.object(base, 'require_editor_closed'), patch.object(base, 'git', side_effect=lambda r, *a: self.head if a == ('rev-parse', 'HEAD') else 'synthetic'):
            with self.assertRaises(ValueError):
                producer.recover_reviewed(self.root, path, sha, review, review_sha)
        self.assertEqual(generated.read_bytes(), b'unknown writer')
        self.assertTrue((self.root / base.JOURNAL).exists())

    def test_replaced_identity_recovery_preserves_replacement(self):
        path, sha, plan = self.prepare()
        self.stage_synthetic(plan)
        delta = producer.observe(self.root, plan)
        review, review_sha = self.review_recovery(plan, sha, delta)
        target = self.root / producer.RESOURCE
        target.rename(target.with_suffix('.saved'))
        target.write_bytes(b'replacement')
        with patch.object(base, 'require_editor_closed'), patch.object(base, 'git', side_effect=lambda r, *a: self.head if a == ('rev-parse', 'HEAD') else 'synthetic'):
            with self.assertRaises(ValueError):
                producer.recover_reviewed(self.root, path, sha, review, review_sha)
        self.assertEqual(target.read_bytes(), b'replacement')

    def test_reviewed_recovery_archives_generated_and_preserves_empty_dirs(self):
        path, sha, plan = self.prepare()
        run = self.stage_synthetic(plan)
        (self.root / 'ProjectSettings/settings.asset').write_bytes(b'actual changed')
        generated = self.root / 'Assets/generated.meta'
        generated.write_bytes(b'Unity generated')
        (run / 'disabled-preparation.aab').write_bytes(b'candidate preserved')
        delta = producer.observe(self.root, plan)
        review, review_sha = self.review_recovery(plan, sha, delta, {'Assets/generated.meta': 'Unity'})
        def git(root, *args):
            return self.head if args == ('rev-parse', 'HEAD') else ('synthetic' if args == ('branch', '--show-current') else '')
        with patch.object(base, 'require_editor_closed'), patch.object(base, 'git', side_effect=git):
            result = producer.recover_reviewed(self.root, path, sha, review, review_sha)
        self.assertTrue(result['externalRecoveryComplete'])
        self.assertFalse(result['distributable'])
        self.assertFalse((self.root / base.JOURNAL).exists())
        self.assertTrue((self.root / plan['hook']).is_dir())
        self.assertTrue((run / 'journal.completed.json').is_file())
        self.assertEqual((self.root / 'ProjectSettings/settings.asset').read_bytes(), b'synthetic original')
        self.assertEqual((run / 'disabled-preparation.aab').read_bytes(), b'candidate preserved')
        self.assertEqual((self.root / delta['changed']['Assets/generated.meta']['archive']).read_bytes(), b'Unity generated')
        self.assertEqual((self.root / 'Build/old.apk').read_bytes(), b'synthetic original')

    def test_conditional_single_invocation_retains_staging_and_never_certifies_binary(self):
        path, sha, plan = self.prepare()
        # Synthetic tool binding only; pinning is mocked and no Editor runs.
        fake_editor = str(self.root / producer.CLI)
        plan['installedTools'] = {'editorExecutable': fake_editor, 'files': {
            fake_editor: plan['before'][producer.CLI]}, 'manifestSha256': 'e' * 64}
        path.write_bytes(producer.encoded(plan))
        sha = base.digest(path.read_bytes())
        def fake_run(command, *, env, stdout, stderr):
            run = self.root / plan['run']
            (run / 'disabled-preparation.aab').write_bytes(b'synthetic callback artifact')
            receipt = {'schema': 1, 'runId': plan['runId'], 'sourceHead': self.head,
                'configSha256': plan['configSha256'], 'resourceSha256': plan['resourceSha256'],
                'unityVersion': 'synthetic', 'injectedManagers': 1, 'loginScenes': 1,
                'preprocessed': True, 'postprocessed': True, 'buildSceneValueMatched': True,
                'compiledEditorGatesDisabled': True, 'productionContractVerified': False,
                'binaryVerified': False, 'distributable': False, 'configuredAndroidDefines': '',
                'prospectivePlayerDefinePlanSha256': 'b' * 64,
                'artifactSha256': base.digest((run / 'disabled-preparation.aab').read_bytes())}
            producer.write_new(run / 'hook-receipt.json', producer.encoded(receipt))
            producer.write_new(run / 'settings-restoration.json', producer.encoded(
                {'schema': 1, 'runId': plan['runId'], 'scopedSettingsRestored': True}))
            return type('Result', (), {'returncode': 0})()
        with patch.object(base, 'preflight'), patch.object(base, 'require_editor_closed'), \
             patch.object(base, 'git', side_effect=lambda r, *a: 'synthetic' if a == ('branch', '--show-current') else ''), \
             patch.object(producer, 'require_source_review'), \
             patch.object(producer, 'pin_installed_tools'), \
             patch.object(producer, 'require_signing', return_value={'keystoreSha256': plan['before'][producer.KEY]['sha256'], 'alias': 'synthetic'}), \
             patch.object(producer.subprocess, 'run', side_effect=fake_run) as launch:
            result = producer.build_once(self.root, self.config, self.head, 'synthetic', path, sha,
                                         path, path, allow_unity_build=True)
        self.assertEqual(launch.call_count, 1)
        self.assertTrue(result['hookReceiptVerified'])
        self.assertFalse(result['binaryVerified'])
        self.assertFalse(result['distributable'])
        self.assertTrue((self.root / base.JOURNAL).is_file())
        self.assertTrue((self.root / producer.RESOURCE).is_file())
        self.assertTrue((self.root / plan['run'] / 'actual-delta.private.json').is_file())
        receipt_path = self.root / plan['run'] / 'hook-receipt.json'
        receipt = producer.read_json(receipt_path)
        receipt['resourceSha256'] = '0' * 64
        receipt_path.write_bytes(producer.encoded(receipt))
        with self.assertRaises(ValueError):
            base.verify_hook_receipt(self.root, plan)

    def test_new_helper_path_is_never_deleted_by_recovery(self):
        path, sha, plan = self.prepare()
        self.stage_synthetic(plan)
        helper = self.root / 'tools/revival/unowned.py'
        helper.write_bytes(b'preserved helper')
        delta = producer.observe(self.root, plan)
        review, review_sha = self.review_recovery(plan, sha, delta, {'tools/revival/unowned.py': 'Unity'})
        with patch.object(base, 'require_editor_closed'), patch.object(base, 'git', side_effect=lambda r, *a: self.head if a == ('rev-parse', 'HEAD') else 'synthetic'):
            with self.assertRaises(ValueError):
                producer.recover_reviewed(self.root, path, sha, review, review_sha)
        self.assertEqual(helper.read_bytes(), b'preserved helper')

    def test_private_acl_and_no_release_key_copy(self):
        from private_ads_evidence import require_private
        path, sha, plan = self.prepare()
        require_private(path.parent)
        require_private(path)
        require_private(self.root / plan['owned'][producer.RESOURCE]['payload'])
        self.assertNotIn('backup', plan['before'][producer.KEY])
        self.assertNotIn('backup', plan['before'][producer.CLI])
        public = self.root / 'public-evidence'
        public.mkdir()
        with self.assertRaises(ValueError):
            require_private(public)

    def test_missing_installed_editor_binding_rejects_before_marker(self):
        path, sha, plan = self.prepare()
        with patch.object(base, 'preflight'), patch.object(base, 'git', return_value='synthetic'), \
             patch.object(producer, 'require_source_review'), patch.object(producer, 'require_signing', return_value={}), \
             patch.object(producer.subprocess, 'run', side_effect=AssertionError('No launch')):
            with self.assertRaises(ValueError):
                producer.build_once(self.root, self.config, self.head, 'synthetic', path, sha,
                                    path, path, allow_unity_build=True)
        self.assertFalse((self.root / plan['run'] / 'launch-once.private.json').exists())

    def test_installed_binary_identity_hash_pin_rejects_changed_tool(self):
        from contextlib import ExitStack
        file = self.root / producer.CLI
        expected = {'identity': list(base.identity(file)), 'sha256': base.digest(file.read_bytes())}
        tools = {'editorExecutable': str(file), 'files': {str(file): expected}}
        file.write_bytes(b'changed executable')
        with ExitStack() as stack, self.assertRaises(ValueError):
            producer.pin_installed_tools(stack, tools)


if __name__ == '__main__':
    unittest.main()
