"""Synthetic producer wiring only; no real settings, signing, Editor or build."""
from contextlib import ExitStack
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import private_ads_approved as approved
import private_ads_build as base
import private_ads_producer as producer
from test_private_ads_approved import fixture as approved_fixture


class LaunchObserved(Exception):
    """Stop the inert subprocess boundary before any actual execution."""


class ApprovedLoginInputTests(unittest.TestCase):
    def fixture(self, directory, mode, settings=b'MonoBehaviour:\n  TitleId: aBc123\n  Other: SYNTHETIC_ONLY\n'):
        root = Path(directory).resolve()
        config = root / '.revival-local/config.json'
        config.parent.mkdir()
        value = approved_fixture()
        if mode == 'privacy_only_adult':
            value = {k: v for k, v in value.items() if k in approved.PRIVACY_FIELDS}
            value.update(mode=mode, privacyEnvironmentReviewSha256='b' * 64)
            value.update({k: True for k in approved.PRIVACY_TRUE_FLAGS})
            value.update({k: False for k in approved.PRIVACY_FALSE_FLAGS})
        elif mode == 'disabled_candidate':
            value = {'checkout': str(root), 'androidAppId': value['androidAppId'],
                     'productionRewardedAdUnit': value['productionRewardedAdUnit'],
                     'consoleInventoryConfirmed': True, 'productionActivationApproved': False,
                     'regionalReviewApproved': False}
        config.write_bytes(producer.encoded(value))
        for relative in ('Assets/Purchased/preserved.bin', 'ProjectSettings/preserved.asset',
                         'Packages/packages-lock.json', 'tools/revival/preserved.py', producer.CLI, producer.KEY):
            file = root / relative
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b'SYNTHETIC_ORIGINAL')
        if settings is not None:
            file = root / producer.PLAYFAB_SETTINGS
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(settings)
        key = {'disabled_candidate': 'disabledPreparationValid',
               'approved_adult_release': 'approvedSourceContractValid',
               'privacy_only_adult': 'approvedPrivacySourceContractValid'}[mode]
        result = producer.prepare_plan(root, config, 'a' * 40, 'synthetic', branch='synthetic', mode=mode,
            preflight_result={key: True, 'configSha256': base.digest(config.read_bytes())})
        path = Path(result['plan'])
        plan = producer.load_plan(root, path, result['planSha256'])
        editor = str(root / producer.CLI)
        plan['installedTools'] = {'editorExecutable': editor, 'files': {editor: plan['before'][producer.CLI]}}
        path.write_bytes(producer.encoded(plan))
        return root, config, path, base.digest(path.read_bytes()), plan

    def invoke(self, fixture, launch):
        root, config, path, sha, plan = fixture
        with ExitStack() as stack:
            for target, name in ((approved, 'preflight_approved'), (base, 'preflight'),
                                 (base, 'require_editor_closed'), (producer, 'require_source_review'),
                                 (producer, 'pin_installed_tools')):
                stack.enter_context(patch.object(target, name))
            stack.enter_context(patch.object(base, 'git', side_effect=lambda r, *args:
                'synthetic' if args == ('branch', '--show-current') else ''))
            stack.enter_context(patch.object(producer, 'require_signing', return_value={
                'keystoreSha256': plan['before'][producer.KEY]['sha256'], 'alias': 'synthetic'}))
            stack.enter_context(patch.object(producer.subprocess, 'run', side_effect=launch))
            producer.build_once(root, config, 'a' * 40, 'synthetic', path, sha, path, path,
                                allow_unity_build=True)

    def protected_bytes(self, fixture):
        root, _, _, _, plan = fixture
        return {relative: (root / relative).read_bytes() for relative in plan['before']}

    def assert_not_staged(self, fixture):
        root, _, _, _, plan = fixture
        self.assertFalse((root / base.JOURNAL).exists())
        self.assertFalse((root / plan['run'] / 'launch-once.private.json').exists())
        self.assertFalse((root / plan['hook']).exists())
        for relative in plan['owned']:
            self.assertFalse((root / relative).exists())

    def test_both_approved_entries_receive_exact_reviewed_title_and_hash(self):
        for mode in ('approved_adult_release', 'privacy_only_adult', 'disabled_candidate'):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as directory:
                fixture = self.fixture(directory, mode, settings=None if mode == 'disabled_candidate' else
                    b'MonoBehaviour:\n  TitleId: aBc123\n  Other: SYNTHETIC_ONLY\n')
                root, _, _, _, plan = fixture
                before = self.protected_bytes(fixture)
                calls = []
                def observe_launch(command, *, env, stdout, stderr):
                    calls.append(command)
                    if mode == 'disabled_candidate':
                        self.assertNotIn('TAMER_PRIVATE_ADS_PLAYFAB_TITLE', env)
                        self.assertNotIn('TAMER_PRIVATE_ADS_PLAYFAB_SETTINGS_SHA256', env)
                    else:
                        self.assertEqual(env['TAMER_PRIVATE_ADS_PLAYFAB_TITLE'], 'aBc123')
                        self.assertEqual(env['TAMER_PRIVATE_ADS_PLAYFAB_SETTINGS_SHA256'],
                                         plan['before'][producer.PLAYFAB_SETTINGS]['sha256'])
                    self.assertEqual(command[command.index('--execute-method') + 1], producer.build_entry(mode))
                    self.assertEqual(env['TAMER_PRIVATE_ADS_OUTPUT'], str(root / plan['run'] / producer.artifact_name(mode)))
                    self.assertTrue((root / plan['run'] / 'launch-once.private.json').exists())
                    self.assertTrue((root / base.JOURNAL).exists())
                    self.assertEqual(self.protected_bytes(fixture), before)
                    raise LaunchObserved()
                with patch.dict(producer.os.environ, {'TAMER_PRIVATE_ADS_PLAYFAB_TITLE': 'STALE',
                        'TAMER_PRIVATE_ADS_PLAYFAB_SETTINGS_SHA256': 'STALE'}), self.assertRaises(LaunchObserved):
                    self.invoke(fixture, observe_launch)
                self.assertEqual(len(calls), 1)
                self.assertEqual(self.protected_bytes(fixture), before)
                self.assertTrue((root / plan['run'] / 'actual-delta.private.json').exists())
                self.assertFalse((root / plan['run'] / producer.artifact_name(mode)).exists())

    def test_missing_changed_and_malformed_title_reject_before_marker_stage_or_launch(self):
        cases = [None, b'  TitleId: \n', b'  TitleId: not-hex\n',
                 b'  TitleId: ABC123\n  TitleId : ABC123\n',
                 b'  TitleId: ABC123\n  "Title\\u0049d": ABC123\n',
                 b'  TitleId: ABC123\n\n  # comment\n    continuation\n',
                 'changed_snapshot', 'missing_snapshot', 'changed_original']
        for mode in ('approved_adult_release', 'privacy_only_adult'):
            for case, settings in enumerate(cases):
                with self.subTest(mode=mode, case=case), tempfile.TemporaryDirectory() as directory:
                    fixture = self.fixture(directory, mode, settings if not isinstance(settings, str) else
                        b'MonoBehaviour:\n  TitleId: ABC123\n')
                    root, _, _, _, plan = fixture
                    if isinstance(settings, str):
                        entry = plan['before'][producer.PLAYFAB_SETTINGS]
                        target = root / (producer.PLAYFAB_SETTINGS if settings == 'changed_original' else entry['backup'])
                        if settings == 'missing_snapshot':
                            target.unlink()
                        else:
                            target.write_bytes(b'  TitleId: FEDCBA\n')
                    before = self.protected_bytes(fixture)
                    calls = []
                    def forbidden_launch(*args, **kwargs):
                        calls.append(True)
                        raise AssertionError('No launch permitted')
                    with self.assertRaises((ValueError, OSError)):
                        self.invoke(fixture, forbidden_launch)
                    self.assertEqual(calls, [])
                    self.assert_not_staged(fixture)
                    self.assertEqual(self.protected_bytes(fixture), before)


if __name__ == '__main__':
    unittest.main()
