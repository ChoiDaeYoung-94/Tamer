"""Entry-specific synthetic snapshots; never use real settings, keys or builds."""
from contextlib import redirect_stderr, redirect_stdout
import hashlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import audit_operating_build_inputs as audit
import test_operating_build_inputs as fixtures


SETTINGS = (b'%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\n'
            b'PlayerSettings:\n  bundleVersion: 1.0.5\n  AndroidBundleVersionCode: 27\n'
            b'  AndroidKeystoreName: SYNTHETIC_PRIVATE_KEY_PATH\n'
            b'  AndroidKeyaliasName: SYNTHETIC_PRIVATE_ALIAS\n'
            b'  unrelatedSecret: SYNTHETIC_SETTINGS_SECRET\n')


class ApprovedVersionPlanTests(unittest.TestCase):
    def plan(self, raw=SETTINGS, maximum=26):
        return audit.approved_adult_release_plan(raw, maximum)

    def test_current_version_is_used_without_legacy_increment(self):
        plan = self.plan()
        self.assertEqual(plan['versionName'], '1.0.5')
        self.assertEqual(plan['candidateVersionCode'], 27)
        self.assertFalse(plan['incrementsVersionCode'])
        self.assertTrue(plan['versionExceedsPublishedMaximum'])
        self.assertFalse(plan['plannedOutputPathKnown'])
        self.assertIsNone(plan['plannedAabOutputAbsent'])
        self.assertNotIn('fileName', plan)

    def test_same_or_lower_current_code_never_exceeds_observed_maximum(self):
        for code in (1, 25, 26):
            with self.subTest(code=code):
                plan = self.plan(SETTINGS.replace(b'Code: 27', f'Code: {code}'.encode()))
                self.assertEqual(plan['candidateVersionCode'], code)
                self.assertFalse(plan['versionExceedsPublishedMaximum'])
        self.assertFalse(self.plan(maximum=27)['versionExceedsPublishedMaximum'])

    def test_android_code_boundaries_and_invalid_maximum_fail_closed(self):
        for code in (1, audit.ANDROID_MAX):
            with self.subTest(code=code):
                self.assertEqual(self.plan(SETTINGS.replace(b'Code: 27', f'Code: {code}'.encode()), 0)
                                 ['candidateVersionCode'], code)
        for code in (0, -1, audit.ANDROID_MAX + 1, audit.INT_MAX, audit.INT_MAX + 1):
            with self.subTest(code=code), self.assertRaises(audit.PreflightError):
                self.plan(SETTINGS.replace(b'Code: 27', f'Code: {code}'.encode()))
        for maximum in (-1, audit.ANDROID_MAX + 1, True, '26'):
            with self.subTest(maximum=maximum), self.assertRaises(audit.PreflightError):
                self.plan(maximum=maximum)

    def test_plain_and_quoted_version_names_preserve_actual_string(self):
        for scalar, expected in [(b'1.0.5', '1.0.5'), (b'"1.0.5"', '1.0.5'),
                                 (b"'release ''one'''", "release 'one'"),
                                 (b'"release \\u0031 # beta"', 'release 1 # beta')]:
            with self.subTest(scalar=scalar):
                self.assertEqual(self.plan(SETTINGS.replace(b'1.0.5', scalar))['versionName'], expected)

    def test_bom_and_supported_line_endings_have_the_same_version(self):
        expected = self.plan()
        for newline in (b'\n', b'\r\n', b'\r'):
            with self.subTest(newline=newline):
                self.assertEqual(self.plan(b'\xef\xbb\xbf' + SETTINGS.replace(b'\n', newline)), expected)

    def test_yaml_mapping_requires_separator_after_colon(self):
        for field, value in ((b'bundleVersion', b'1.0.5'), (b'AndroidBundleVersionCode', b'27')):
            original = field + b': ' + value
            with self.subTest(field=field), self.assertRaises(audit.PreflightError):
                self.plan(SETTINGS.replace(original, field + b':' + value))
            for separator in (b' ', b'\t', b' \t'):
                with self.subTest(field=field, separator=separator):
                    self.assertEqual(self.plan(SETTINGS.replace(original, field + b':' + separator + value)),
                                     self.plan())
        for value in (b'"1.0.5"', b"'1.0.5'"):
            with self.subTest(quoted=value), self.assertRaises(audit.PreflightError):
                self.plan(SETTINGS.replace(b'bundleVersion: 1.0.5', b'bundleVersion:' + value))

    def test_multiline_scalar_continuations_reject_without_shortening(self):
        for field in (b'bundleVersion: 1.0.5', b'AndroidBundleVersionCode: 27'):
            for continuation in (b'    beta', b'  beta', b'\tbeta', b'    beta: value',
                                 b'  beta:continued', b'  - beta', b'\n    beta',
                                 b'  # comment\n\n    beta', b'    # comment\n    beta'):
                with self.subTest(field=field, continuation=continuation), self.assertRaises(audit.PreflightError):
                    self.plan(SETTINGS.replace(field + b'\n', field + b'\n' + continuation + b'\n'))
        for quoted in (b'"1.0.5"', b"'1.0.5'"):
            with self.subTest(quoted=quoted), self.assertRaises(audit.PreflightError):
                self.plan(SETTINGS.replace(b'1.0.5\n', quoted + b'\n    beta\n'))

    def test_blank_comment_and_next_peer_fields_preserve_single_line_values(self):
        expected = self.plan()
        for field in (b'bundleVersion: 1.0.5', b'AndroidBundleVersionCode: 27'):
            for layout in (b'\n', b' \t\n', b'  # comment\n', b'    # comment\n\n',
                           b'# root comment\n  # field comment\n\n'):
                with self.subTest(field=field, layout=layout):
                    self.assertEqual(self.plan(SETTINGS.replace(field + b'\n', field + b'\n' + layout)),
                                     expected)
        # Either selected scalar may be last, including a final line without LF.
        for last in (b'  bundleVersion: 1.0.5\n', b'  AndroidBundleVersionCode: 27\n'):
            raw = SETTINGS.replace(last, b'') + last.rstrip(b'\n')
            with self.subTest(last=last):
                self.assertEqual(self.plan(raw), expected)

    def test_missing_duplicate_misindented_and_quoted_keys_reject(self):
        invalid = [SETTINGS.replace(b'  bundleVersion: 1.0.5\n', b''),
                   SETTINGS.replace(b'  AndroidBundleVersionCode: 27\n', b''),
                   SETTINGS + b'  bundleVersion: 1.0.5\n',
                   SETTINGS + b'  AndroidBundleVersionCode: 27\n',
                   SETTINGS + b'  bundleVersion : 1.0.5\n',
                   SETTINGS + b'  "bundleVersion": 1.0.5\n',
                   SETTINGS + b'  "bundle\\u0056ersion": 1.0.5\n',
                   SETTINGS + b"  'AndroidBundleVersionCode': 27\n",
                   SETTINGS + b'  ? bundleVersion\n  : 1.0.5\n',
                   SETTINGS + b'  &name bundleVersion: 1.0.5\n',
                   SETTINGS.replace(b'  bundleVersion:', b'    bundleVersion:'),
                   SETTINGS.replace(b'  AndroidBundleVersionCode:', b'\tAndroidBundleVersionCode:'),
                   SETTINGS.replace(b'PlayerSettings:', b'OtherSettings:'),
                   SETTINGS + b'PlayerSettings:\n',
                   SETTINGS + b'"PlayerSettings":\n',
                   SETTINGS.replace(b'  AndroidBundleVersionCode:', b'Other:\n  AndroidBundleVersionCode:')]
        for index, raw in enumerate(invalid):
            with self.subTest(case=index), self.assertRaises(audit.PreflightError):
                self.plan(raw)

    def test_ambiguous_or_noncanonical_yaml_scalars_reject(self):
        invalid_versions = [b'', b' ', b'null', b'~', b'true', b'FALSE', b'*alias', b'&a 1.0.5',
                            b'!!str 1.0.5', b'|', b'>', b'{v: 1}', b'[1, 2]', b'1.0.5 # comment',
                            b'version: value', b'@version', b'`version', b'%version', b'- value',
                            b'"unterminated', b"'unterminated", b'"1.0.5" # comment',
                            b'" padded "', b'"\\u0000"', b'"\\u0085"', b'"\\u2028"', b'"\\ud800"',
                            '\u00a01.0.5'.encode(), '1.0.5\u00a0'.encode()]
        invalid_codes = [b'', b' ', b'"27"', b"'27'", b'+27', b'027', b'27.0', b'2.7e1',
                         b'0x1b', b'27 # comment', b'true', b'null', b'|', b'*code', b'&c 27',
                         b'[27]', b'{code: 27}', b'9999999999999999999999', '２７'.encode()]
        for field, values in [(b'1.0.5', invalid_versions), (b'Code: 27', [b'Code: ' + v for v in invalid_codes])]:
            for index, value in enumerate(values):
                with self.subTest(field=field, case=index), self.assertRaises(audit.PreflightError):
                    self.plan(SETTINGS.replace(field, value))

    def test_invalid_encoding_and_non_yaml_line_separators_reject(self):
        for raw in (b'\xff', SETTINGS.decode().encode('utf-16'), SETTINGS.decode().encode('utf-32')):
            with self.subTest(encoding=raw[:4]), self.assertRaises(audit.PreflightError):
                self.plan(raw)
        for separator in ('\v', '\f', '\x85', '\x00', '\x7f', '\u2028', '\u2029'):
            with self.subTest(separator=repr(separator)), self.assertRaises(audit.PreflightError):
                self.plan(SETTINGS.replace(b'\n', separator.encode()))

    def test_each_entry_uses_only_its_own_normalized_public_source_pins(self):
        for entry, pins in audit.ENTRY_SOURCE_PINS.items():
            for newline in ('\n', '\r\n', '\r'):
                blobs = {relative: ('\ufeff' + (fixtures.ROOT / relative).read_bytes().decode('utf-8-sig')
                         .replace('\r\n', '\n').replace('\r', '\n').replace('\n', newline)).encode()
                         for relative in pins}
                with self.subTest(entry=entry, newline=repr(newline)):
                    audit.source_contract(blobs, entry)


@unittest.skipUnless(sys.platform == 'win32', 'Real Windows guarded handle checks required')
class ApprovedGuardedSnapshotTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='tamer-entry-inputs-')
        self.addCleanup(temporary.cleanup)
        self.base = Path(temporary.name)
        self.root = self.base / 'project'
        self.private = self.base / 'private-reports'
        self.private.mkdir()
        self.counter = 0
        for relative in audit.APPROVED_SOURCE_PINS:
            self.write(relative, (fixtures.ROOT / relative).read_bytes())
        self.write(audit.PROJECT_SETTINGS, SETTINGS)
        self.write(audit.PLAYFAB, b'MonoBehaviour:\n  TitleId: ABC123\n'
                   b'  DeveloperSecretKey: SYNTHETIC_PLAYFAB_SECRET\n')
        self.write(audit.PGS_TEXT, fixtures.PGS)
        self.write(audit.PGS_ASSET, fixtures.ASSET)
        self.write('src/AeDeong.keystore', b'SYNTHETIC_KEY_DO_NOT_READ')
        self.write('.revival-local/approval.private.json', b'SYNTHETIC_APPROVAL_DO_NOT_READ')
        self.write('.revival-local/config.private.json', b'SYNTHETIC_CONFIG_DO_NOT_READ')

    def write(self, relative, raw):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)

    def output(self):
        self.counter += 1
        return self.private / f'report-{self.counter}.private.json'

    def run_audit(self, **kwargs):
        return audit.audit_project(self.root, self.output(), fixtures.TITLE, 26,
                                   now=fixtures.NOW, entry=audit.APPROVED_ENTRY, **kwargs)

    def before(self):
        return {p.relative_to(self.root).as_posix(): p.read_bytes()
                for p in self.root.rglob('*') if p.is_file()}

    def test_selected_allowlist_reads_no_legacy_inputs_and_preserves_tree(self):
        before = self.before()
        opened = []
        original = audit.LockedFile
        def opening(path, **kwargs):
            opened.append(Path(path))
            return original(path, **kwargs)
        original_attributes = audit.attributes
        def attributes(path):
            self.assertNotIn(Path(path), [self.root / audit.BUILD_INFO, self.root / audit.FINISH])
            return original_attributes(path)
        with patch.object(audit, 'LockedFile', side_effect=opening), \
                patch.object(audit, 'attributes', side_effect=attributes):
            result = self.run_audit()
        self.assertTrue(result['inputChecksPassed'])
        self.assertEqual(result['entry'], audit.APPROVED_ENTRY)
        self.assertEqual(result['buildEntryPoint'], 'PrivateProductionAdsBuild.BuildApprovedAdultRelease')
        self.assertTrue(result['projectSettingsVersionCodeUsed'])
        self.assertIsNone(result['legacyAabPlan'])
        self.assertNotIn('localEpoch', result['calculation'])
        self.assertEqual(opened, [self.root / p for p in audit.APPROVED_INPUTS])
        self.assertEqual(set(result['inputSnapshotsPrivate']), set(audit.APPROVED_INPUTS))
        self.assertFalse((self.root / 'BuildInfo').exists())
        self.assertEqual(self.before(), before)

    def test_legacy_markers_seed_and_existing_legacy_outputs_are_irrelevant(self):
        self.write(audit.BUILD_INFO, b'SYNTHETIC_INVALID_LEGACY_SEED')
        (self.root / audit.FINISH).mkdir()
        self.write('Build/AOS/1.0.1.0.aab', b'SYNTHETIC_EXISTING_LEGACY_OUTPUT')
        before = self.before()
        with patch.object(audit, 'existing_parent') as lookup:
            result = self.run_audit()
        lookup.assert_not_called()
        self.assertTrue(result['inputChecksPassed'])
        self.assertNotIn('plannedAabOutputAbsent', result['checks'])
        self.assertIsNone(result['approvedAdultReleasePlan']['plannedAabOutputAbsent'])
        self.assertEqual(self.before(), before)

    def test_equal_published_maximum_fails_without_increment_or_repair(self):
        self.write(audit.PROJECT_SETTINGS, SETTINGS.replace(b'Code: 27', b'Code: 26'))
        before = self.before()
        result = self.run_audit()
        self.assertFalse(result['inputChecksPassed'])
        self.assertEqual(result['approvedAdultReleasePlan']['candidateVersionCode'], 26)
        self.assertEqual(result['issues'], ['versionExceedsPublishedMaximum'])
        self.assertTrue(result['projectSettingsVersionCodeUsed'])
        self.assertEqual(self.before(), before)

    def test_invalid_version_and_title_both_fail_with_fixed_codes(self):
        self.write(audit.PROJECT_SETTINGS, SETTINGS + b'  AndroidBundleVersionCode: 99\n')
        self.write(audit.PLAYFAB, b'MonoBehaviour:\n  TitleId: \n')
        before = self.before()
        result = self.run_audit()
        self.assertFalse(result['inputChecksPassed'])
        self.assertIn('playFabTitleMatchesExpected', result['issues'])
        self.assertIn('project_settings_version_fields_missing_duplicate_or_malformed', result['issues'])
        self.assertIsNone(result['approvedAdultReleasePlan'])
        self.assertFalse(result['projectSettingsVersionCodeUsed'])
        self.assertEqual(self.before(), before)

    def test_pgs_missing_mismatch_and_duplicate_fields_still_fail(self):
        for raw in (fixtures.PGS.replace(b'android.SetupDone=true\n', b''),
                    fixtures.PGS.replace(b'123456789', b'987654321'),
                    fixtures.PGS + b'proj.AppId=123456789\n'):
            self.write(audit.PGS_TEXT, raw)
            before = self.before()
            with self.subTest(raw=raw):
                result = self.run_audit()
                self.assertFalse(result['checks']['pgsSingleConfiguredValuesMatch'])
                self.assertEqual(self.before(), before)

    def test_each_missing_selected_input_fails_before_report_write(self):
        for relative in audit.APPROVED_INPUTS:
            file = self.root / relative
            saved = file.read_bytes()
            file.unlink()
            output = self.output()
            with self.subTest(relative=relative), self.assertRaises(OSError):
                audit.audit_project(self.root, output, fixtures.TITLE, 26, entry=audit.APPROVED_ENTRY)
            self.assertFalse(output.exists())
            file.write_bytes(saved)

    def test_each_changed_approved_source_blocks_version_calculation(self):
        for relative in audit.APPROVED_SOURCE_PINS:
            file = self.root / relative
            saved = file.read_bytes()
            file.write_bytes(saved + b'\n// SYNTHETIC_SOURCE_CHANGE\n')
            before = self.before()
            with self.subTest(relative=relative):
                result = self.run_audit()
                self.assertFalse(result['checks']['approvedSourceContractMatches'])
                self.assertIn('approved_source_contract_changed', result['issues'])
                self.assertIsNone(result['approvedAdultReleasePlan'])
                self.assertFalse(result['projectSettingsVersionCodeUsed'])
                self.assertEqual(self.before(), before)
            file.write_bytes(saved)

    def test_real_windows_snapshot_handles_block_selected_input_writes_and_renames(self):
        before = self.before()
        original = audit.playfab_title_matches
        attempted = []
        def probe(raw, expected):
            for relative in audit.APPROVED_INPUTS:
                file = self.root / relative
                with self.subTest(relative=relative), self.assertRaises(OSError):
                    file.write_bytes(b'SYNTHETIC_BLOCKED_WRITE')
                with self.subTest(rename=relative), self.assertRaises(OSError):
                    file.rename(file.with_name(file.name + '.moved'))
            attempted.append(True)
            return original(raw, expected)
        with patch.object(audit, 'playfab_title_matches', side_effect=probe):
            self.assertTrue(self.run_audit()['inputChecksPassed'])
        self.assertEqual(attempted, [True])
        self.assertEqual(self.before(), before)
        self.write(audit.PROJECT_SETTINGS, SETTINGS + b'  laterOwner: true\n')
        self.assertTrue((self.root / audit.PROJECT_SETTINGS).read_bytes().endswith(b'laterOwner: true\n'))

    def test_preexisting_project_settings_writer_fails_closed(self):
        before = self.before()
        output = self.output()
        with (self.root / audit.PROJECT_SETTINGS).open('r+b'), self.assertRaises(OSError):
            audit.audit_project(self.root, output, fixtures.TITLE, 26, entry=audit.APPROVED_ENTRY)
        self.assertFalse(output.exists())
        self.assertEqual(self.before(), before)

    def test_project_settings_hardlink_is_rejected_without_mutation(self):
        os.link(self.root / audit.PROJECT_SETTINGS, self.base / 'alias.asset')
        before = self.before()
        with self.assertRaisesRegex(audit.PreflightError, 'input_link_or_identity_alias_rejected'):
            self.run_audit()
        self.assertEqual(self.before(), before)

    def test_selected_project_settings_parent_junction_is_rejected(self):
        path = self.root / 'ProjectSettings'
        target = self.base / 'settings-target'
        path.rename(target)
        with fixtures.fixture_junction(path, target), self.assertRaises((OSError, ValueError)):
            self.run_audit()
        target.rename(path)
        self.assertEqual((path / 'ProjectSettings.asset').read_bytes(), SETTINGS)

    def test_private_report_and_stdout_exclude_unrelated_settings_values(self):
        before = self.before()
        output = self.output()
        stdout = io.StringIO()
        args = ['--project-path', str(self.root), '--entry', audit.APPROVED_ENTRY,
                '--expected-playfab-title', fixtures.TITLE, '--published-max-code', '26',
                '--output', str(output)]
        with redirect_stdout(stdout):
            self.assertEqual(audit.main(args), 0)
        result = json.loads(output.read_text(encoding='utf-8'))
        self.assertFalse(result['releaseReady'])
        self.assertEqual(json.loads(stdout.getvalue()), {
            'inputChecksPassed': True, 'releaseReady': False, 'issues': []})
        for forbidden in ('SYNTHETIC_PRIVATE_KEY_PATH', 'SYNTHETIC_PRIVATE_ALIAS',
                          'SYNTHETIC_SETTINGS_SECRET', 'SYNTHETIC_PLAYFAB_SECRET',
                          'SYNTHETIC_KEY_DO_NOT_READ', 'SYNTHETIC_APPROVAL_DO_NOT_READ',
                          'SYNTHETIC_CONFIG_DO_NOT_READ', '123456789', 'synthetic-client', str(self.root),
                          hashlib.sha256(b'SYNTHETIC_KEY_DO_NOT_READ').hexdigest()):
            self.assertNotIn(forbidden, output.read_text(encoding='utf-8'))
            self.assertNotIn(forbidden, stdout.getvalue())
        for forbidden in ('sha256', 'identity', '1.0.5', str(output)):
            self.assertNotIn(forbidden, stdout.getvalue())
        self.assertEqual(self.before(), before)

    def test_report_collision_and_legacy_option_misuse_fail_before_open(self):
        for output, allow in ((self.root / audit.PROJECT_SETTINGS, False),
                              (self.root / audit.APPROVED_SCRIPT, False),
                              (self.output(), True)):
            with self.subTest(output=output, allow=allow), patch.object(audit, 'LockedFile') as opened:
                with self.assertRaises(audit.PreflightError):
                    audit.audit_project(self.root, output, fixtures.TITLE, 26,
                                        entry=audit.APPROVED_ENTRY, allow_finish_reuse=allow)
                opened.assert_not_called()


class EntryCommandLineTests(unittest.TestCase):
    def test_unknown_empty_and_duplicate_entries_are_cli_errors(self):
        args = ['--project-path', 'D:/synthetic/project', '--expected-playfab-title', fixtures.TITLE,
                '--published-max-code', '26', '--output', 'D:/synthetic/report.private.json']
        for invalid in (['--entry', 'privacy_only_adult'], ['--entry', ''],
                        ['--entry', audit.LEGACY_ENTRY, '--entry', audit.APPROVED_ENTRY],
                        ['--entry', audit.APPROVED_ENTRY, '--entry', audit.APPROVED_ENTRY]):
            with self.subTest(invalid=invalid), redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                audit.main(args + invalid)

    def test_approved_rejects_legacy_reuse_option_with_fixed_error(self):
        args = ['--project-path', 'D:/synthetic/project', '--entry', audit.APPROVED_ENTRY,
                '--expected-playfab-title', fixtures.TITLE, '--published-max-code', '26',
                '--output', 'D:/synthetic/report.private.json', '--allow-finish-reuse']
        stdout = io.StringIO()
        with redirect_stdout(stdout), patch.object(audit, 'LockedFile') as opened:
            self.assertEqual(audit.main(args), 2)
        opened.assert_not_called()
        self.assertEqual(json.loads(stdout.getvalue())['issues'], ['finish_reuse_option_requires_legacy_entry'])

    def test_api_rejects_invalid_entry_and_non_boolean_legacy_flag(self):
        for entry, reuse in (('unknown', False), (audit.APPROVED_ENTRY, True),
                             (audit.LEGACY_ENTRY, 1), (audit.APPROVED_ENTRY, None)):
            with self.subTest(entry=entry, reuse=reuse), self.assertRaises(audit.PreflightError):
                audit.validate_arguments(fixtures.TITLE, 26, None, entry=entry, allow_finish_reuse=reuse)


if __name__ == '__main__':
    unittest.main()
