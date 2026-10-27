"""Synthetic-only input guards; no Unity, account, signing key or real settings."""
from contextlib import contextmanager, redirect_stdout, redirect_stderr
from datetime import datetime, timedelta, timezone
import ctypes
from ctypes import wintypes
import hashlib
import io
import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch

import audit_operating_build_inputs as audit


ROOT = Path(__file__).resolve().parents[2]
EPOCH = datetime(2023, 1, 21, tzinfo=timezone(timedelta(hours=9)))
NOW = EPOCH + timedelta(days=8)
TITLE = 'ABC123'
PGS = b'proj.AppId=123456789\nand.ClientId=synthetic-client.apps.example.invalid\nandroid.SetupDone=true\n'
ASSET = b'MonoBehaviour:\n  mAppId: 123456789\n  mWebClientId: synthetic-client.apps.example.invalid\n'


@contextmanager
def fixture_junction(path, target):
    """Pure Python Win32 fixture setup, without a shell or a build tool."""
    path.mkdir()
    substitute = ('\\??\\' + str(target)).encode('utf-16-le')
    printed = str(target).encode('utf-16-le')
    names = substitute + b'\x00\x00' + printed + b'\x00\x00'
    payload = struct.pack('<IHHHHHH', 0xA0000003, 8 + len(names), 0,
                          0, len(substitute), len(substitute) + 2, len(printed)) + names
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                  wintypes.LPVOID, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    kernel.CreateFileW.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.DeviceIoControl.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPVOID,
                                      wintypes.DWORD, wintypes.LPVOID, wintypes.DWORD,
                                      ctypes.POINTER(wintypes.DWORD), wintypes.LPVOID]
    handle = kernel.CreateFileW(str(path), 0x40000000, 0, None, 3, 0x02200000, None)
    if handle == ctypes.c_void_p(-1).value:
        os.rmdir(path)
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        try:
            returned = wintypes.DWORD()
            buffer = ctypes.create_string_buffer(payload)
            if not kernel.DeviceIoControl(handle, 0x000900A4, buffer, len(payload), None, 0,
                                          ctypes.byref(returned), None):
                raise ctypes.WinError(ctypes.get_last_error())
        finally:
            kernel.CloseHandle(handle)
    except BaseException:
        os.rmdir(path)
        raise
    try:
        yield
    finally:
        # Only this newly created fixture junction; no recursive removal.
        os.rmdir(path)


class VersionPlanTests(unittest.TestCase):
    def test_zero_seed_is_code_one_even_when_project_settings_would_be_26(self):
        plan = audit.legacy_aab_plan(b'0,0,0', False, NOW, 26)
        self.assertEqual(plan['candidateVersionCode'], 1)
        self.assertFalse(plan['versionExceedsPublishedMaximum'])
        self.assertEqual(plan['versionName'], '1.0.1')
        self.assertEqual(plan['fileName'], '1.0.1.0.aab')

    def test_seed_26_is_conditional_code_27_and_same_week_increments_build(self):
        plan = audit.legacy_aab_plan(b'1,2,26', False, NOW, 26)
        self.assertEqual(plan['candidateVersionCode'], 27)
        self.assertEqual(plan['fileName'], '1.0.1.3.aab')
        self.assertTrue(plan['versionExceedsPublishedMaximum'])
        self.assertFalse(audit.legacy_aab_plan(b'1,2,26', False, NOW, 27)
                         ['versionExceedsPublishedMaximum'])

    def test_boundary_uses_one_supplied_local_timestamp_and_resets_build(self):
        before = audit.legacy_aab_plan(b'1,5,26', False,
                                      EPOCH + timedelta(days=14, microseconds=-1), 26)
        at = audit.legacy_aab_plan(b'1,5,26', False, EPOCH + timedelta(days=14), 26)
        self.assertEqual(before['fileName'], '1.0.1.6.aab')
        self.assertEqual(at['fileName'], '1.0.2.0.aab')
        self.assertEqual(at['versionName'], '1.0.2')
        self.assertEqual(at['candidateVersionCode'], 27)
        with self.assertRaises(audit.PreflightError):
            audit.legacy_aab_plan(b'0,0,26', False, EPOCH - timedelta(microseconds=1), 26)
        with self.assertRaises(audit.PreflightError):
            audit.legacy_aab_plan(b'0,0,26', False, NOW.replace(tzinfo=None), 26)

    def test_finish_reuses_original_numeric_spelling_without_increment(self):
        denied = audit.legacy_aab_plan(b'01,02,027\r\n', True, NOW, 26)
        self.assertFalse(denied['finishReuseConditionSatisfied'])
        plan = audit.legacy_aab_plan(b'01,02,027\r\n', True, NOW, 26, True)
        self.assertEqual(plan['candidateVersionCode'], 27)
        self.assertEqual(plan['versionName'], '1.0.01')
        self.assertEqual(plan['fileName'], '1.0.01.02.aab')
        self.assertTrue(plan['finishReuseConditionSatisfied'])
        self.assertFalse(audit.legacy_aab_plan(b'1,2,26', True, NOW, 26, True)
                         ['versionExceedsPublishedMaximum'])
        self.assertFalse(audit.legacy_aab_plan(b'1,2,26', False, NOW, 26, True)
                         ['finishReuseConditionSatisfied'])

    def test_malformed_negative_or_overflow_values_fail_closed(self):
        invalid = [b'', b'0,0', b'0,0,26,27', b'-1,0,26', b'0,-1,26', b'0,0,-1',
                   b'+1,0,26', b'0, 0,26', b'0,0,26\n\n', b'0,0,26\x00', b'0,0,2e1',
                   '0,0,２６'.encode(), b'0,0,2147483648', b'2147483648,0,26',
                   b'1,2147483647,26', b'0,0,2100000000', b'0,0,2100000001']
        for raw in invalid:
            with self.subTest(raw=raw), self.assertRaises(audit.PreflightError):
                audit.legacy_aab_plan(raw, False, NOW, 26)
        self.assertEqual(audit.legacy_aab_plan(b'0,0,2099999999', False, NOW, 26)
                         ['candidateVersionCode'], audit.ANDROID_MAX)
        with self.assertRaises(audit.PreflightError):
            audit.legacy_aab_plan(b'0,0,0', True, NOW, 0, True)


class SettingsTests(unittest.TestCase):
    def test_only_single_exact_title_is_accepted(self):
        valid = b'MonoBehaviour:\n  TitleId: ABC123\n  DeveloperSecretKey: SYNTHETIC_ONLY\n'
        self.assertTrue(audit.playfab_title_matches(valid, TITLE))
        for raw in [valid.replace(b'ABC123', b''), valid.replace(b'ABC123', b'DEF456'),
                    valid.replace(b'ABC123', b'abc123'), valid.replace(b'ABC123', b'ABC123 '),
                    valid + b'  TitleId: ABC123\n', valid + b'  TitleId : ABC123\n',
                    valid.replace(b'ABC123', b'not-hex'), b'\xff']:
            with self.subTest(raw=raw):
                try:
                    self.assertFalse(audit.playfab_title_matches(raw, TITLE))
                except audit.PreflightError:
                    pass

    def test_pgs_valid_single_values_and_serialized_strings_match(self):
        self.assertTrue(audit.pgs_matches(PGS, ASSET))
        self.assertTrue(audit.pgs_matches(PGS.replace(b'123456789', b'%31%32%33%34%35%36%37%38%39'),
                                         ASSET.replace(b'123456789', b"'123456789'")))
        self.assertTrue(audit.pgs_matches(PGS, ASSET.replace(b'123456789', b'"123456789"')))

    def test_pgs_reader_accepts_only_crlf_cr_lf_boundaries_and_ascii_trim(self):
        for newline in [b'\r\n', b'\r', b'\n']:
            settings = b'\xef\xbb\xbf' + PGS.replace(b'\n', newline)
            settings = settings.replace(b'proj.AppId=', b' \tproj.AppId \t= \t')
            with self.subTest(newline=newline):
                self.assertTrue(audit.pgs_matches(settings, ASSET))

    def test_pgs_non_reader_separators_and_controls_reject_before_splitting(self):
        for separator in ['\v', '\f', '\x85', '\u2028', '\u2029', '\x1c', '\x00', '\x7f']:
            settings = PGS.decode().replace('\n', separator).encode()
            with self.subTest(separator=repr(separator)), self.assertRaises(audit.PreflightError):
                audit.pgs_matches(settings, ASSET)
        for escaped in [b'%0B', b'%0C', b'%C2%85', b'%E2%80%A8', b'%E2%80%A9', b'%7F']:
            with self.subTest(escaped=escaped), self.assertRaises(audit.PreflightError):
                audit.pgs_matches(PGS.replace(b'123456789', escaped), ASSET)

    def test_pgs_first_blank_line_stops_reading_later_fields(self):
        for blank in [b'\n', b' \t\r\n', b'\r']:
            with self.subTest(blank=blank), self.assertRaises(audit.PreflightError):
                audit.pgs_matches(blank + PGS, ASSET)
            # A duplicate after the stop has no effect in the actual reader.
            self.assertTrue(audit.pgs_matches(PGS + blank + b'proj.AppId=ignored\n', ASSET))

    def test_pgs_exact_keys_and_narrow_utf8_encoding_reject_malformed_inputs(self):
        for wrong_key in [b'proj.appId', b'proj.AppId.extra', b'proj. AppId', b'proj.\tAppId']:
            with self.subTest(key=wrong_key), self.assertRaises(audit.PreflightError):
                audit.pgs_matches(PGS.replace(b'proj.AppId', wrong_key), ASSET)
        for whitespace in ['\u00a0', '\u2007', '\u202f']:
            # Do not silently trim an extra key that the plugin would recognize.
            extra = ('proj.AppId' + whitespace + '=other\n').encode()
            with self.subTest(whitespace=repr(whitespace)), self.assertRaises(audit.PreflightError):
                audit.pgs_matches(PGS + extra, ASSET)
        for raw in [b'\xff', PGS.decode().encode('utf-16'), PGS.decode().encode('utf-32')]:
            with self.subTest(encoding=raw[:4]), self.assertRaises(audit.PreflightError):
                audit.pgs_matches(raw, ASSET)

    def test_pgs_mismatch_missing_duplicates_blank_and_bad_encoding_reject(self):
        cases = [(PGS, ASSET.replace(b'123456789', b'987654321')),
                 (PGS, ASSET.replace(b'synthetic-client', b'other-client')),
                 (PGS.replace(b'android.SetupDone=true\n', b''), ASSET),
                 (PGS.replace(b'=true', b'=false'), ASSET),
                 (PGS.replace(b'=true', b'=True'), ASSET),
                 (PGS + b'proj.AppId=123456789\n', ASSET),
                 (PGS + b'and.ClientId=synthetic-client.apps.example.invalid\n', ASSET),
                 (PGS + b'android.SetupDone=true\n', ASSET),
                 (PGS.replace(b'123456789', b'%20'), ASSET),
                 (PGS.replace(b'123456789', b'%XY'), ASSET),
                 (PGS.replace(b'123456789', b'%FF'), ASSET),
                 (PGS.replace(b'and.ClientId', b'\nand.ClientId'), ASSET),
                 (PGS, ASSET.replace(b'mAppId:', b'mOther:')),
                 (PGS, ASSET.replace(b'mAppId: ', b'mAppId:')),
                 (PGS, ASSET.replace(b'mWebClientId: ', b'mWebClientId:')),
                 (PGS, ASSET.replace(b'mAppId: 123456789', b'mAppId:"123456789"')),
                 (PGS, ASSET + b'  mAppId: 123456789\n'),
                 (PGS, ASSET + b'  mWebClientId: other-client\n'),
                 (PGS, ASSET.replace(b'123456789', b' ')),
                 (PGS, ASSET.replace(b'123456789', b'123 # comment'))]
        for settings, asset in cases:
            with self.subTest(settings=settings, asset=asset):
                try:
                    self.assertFalse(audit.pgs_matches(settings, asset))
                except audit.PreflightError:
                    pass


class SingleLineSettingsTests(unittest.TestCase):
    title = b'MonoBehaviour:\n  TitleId: ABC123\n  DeveloperSecretKey: SYNTHETIC_ONLY\n'

    def scalar_cases(self):
        return [('title', self.title, b'TitleId', b'ABC123'),
                ('pgs', ASSET, b'mAppId', b'123456789'),
                ('pgs', ASSET, b'mWebClientId', b'synthetic-client.apps.example.invalid')]

    def matches(self, kind, raw):
        return (audit.playfab_title_matches(raw, TITLE) if kind == 'title'
                else audit.pgs_matches(PGS, raw))

    def assert_rejected(self, kind, raw):
        try:
            self.assertFalse(self.matches(kind, raw))
        except audit.PreflightError:
            pass

    def test_each_selected_scalar_rejects_multiline_continuations(self):
        for kind, raw, name, value in self.scalar_cases():
            for spelling in (value, b'"' + value + b'"', b"'" + value + b"'"):
                selected = b'  ' + name + b': ' + spelling
                base = raw.replace(b'  ' + name + b': ' + value, selected)
                for continuation in (b'    beta', b'  beta', b'\tbeta', b'    beta: value',
                                     b'  beta:continued', b'\n    beta', b'  # comment\n\n    beta',
                                     b'    # comment\n  beta'):
                    with self.subTest(kind=kind, name=name, spelling=spelling, continuation=continuation):
                        self.assert_rejected(kind, base.replace(selected + b'\n',
                                                               selected + b'\n' + continuation + b'\n'))
            # YAML quoted scalars can span lines too; never decode just line one.
            for quote in (b'"', b"'"):
                multiline = quote + value + b'\n    beta' + quote
                with self.subTest(kind=kind, name=name, multiline_quote=quote):
                    self.assert_rejected(kind, raw.replace(name + b': ' + value, name + b': ' + multiline))

    def test_each_selected_scalar_rejects_ambiguous_and_misindented_keys(self):
        for kind, raw, name, value in self.scalar_cases():
            escaped = name[:-1] + ('\\u%04x' % name[-1]).encode()
            for duplicate in (b"  '" + name + b"': " + value,
                              b'  "' + name + b'": ' + value,
                              b'  "' + escaped + b'": ' + value,
                              b'  ? ' + name + b'\n  : ' + value,
                              b'  &selected ' + name + b': ' + value,
                              b'  !!str ' + name + b': ' + value,
                              b'  *selected: ' + value):
                with self.subTest(kind=kind, name=name, duplicate=duplicate):
                    self.assert_rejected(kind, raw + duplicate + b'\n')
            canonical = b'  ' + name + b': ' + value
            for malformed in (b'    ' + name + b': ' + value, b'\t' + name + b': ' + value,
                              b'  ' + name + b' : ' + value, b'  ' + name + b':' + value):
                with self.subTest(kind=kind, name=name, malformed=malformed):
                    self.assert_rejected(kind, raw.replace(canonical, malformed))

    def test_public_template_peer_layout_and_unrelated_values_are_compatible(self):
        # Public template only. This never opens the local PlayFab/PGS assets.
        template = (ROOT / 'tools/revival/templates/PlayFabSharedSettings.asset.txt').read_bytes()
        template = template.replace(b'\r\n', b'\n').replace(b'\r', b'\n')
        self.assertFalse(audit.playfab_title_matches(template, TITLE))
        synthetic_title = template.replace(b'  TitleId:\n', b'  TitleId: ABC123\n')
        self.assertTrue(audit.playfab_title_matches(synthetic_title, TITLE))
        synthetic_pgs = (template.split(b'MonoBehaviour:\n', 1)[0] + b'MonoBehaviour:\n'
                         b'  m_ObjectHideFlags: 0\n'
                         b'  m_Script: {fileID: 11500000, guid: 00000000000000000000000000000000, type: 3}\n'
                         b'  m_Name: PlayGamesSettings\n  m_EditorClassIdentifier:\n'
                         + ASSET.split(b'MonoBehaviour:\n', 1)[1] + b'  mNearbyServiceId:\n')
        cases = [('title', synthetic_title, b'TitleId', b'ABC123'),
                 ('pgs', synthetic_pgs, b'mAppId', b'123456789'),
                 ('pgs', synthetic_pgs, b'mWebClientId', b'synthetic-client.apps.example.invalid')]
        for kind, raw, name, value in cases:
            selected = b'  ' + name + b': ' + value + b'\n'
            for layout in (b'\n', b' \t\n', b'  # comment\n', b'    # comment\n\n',
                           b'# root comment\n  # peer comment\n\n'):
                with self.subTest(kind=kind, name=name, layout=layout):
                    self.assertTrue(self.matches(kind, raw.replace(selected, selected + layout)))
            at_eof = raw.replace(selected, b'') + selected.rstrip(b'\n')
            self.assertTrue(self.matches(kind, at_eof))
            for newline in (b'\n', b'\r\n', b'\r'):
                with self.subTest(kind=kind, name=name, newline=newline):
                    self.assertTrue(self.matches(kind, raw.replace(b'\n', newline)))
            # A non-target value is not parsed, even when it has nested literal
            # text that resembles quoted/complex keys or a private credential.
            uninterpreted = (raw + b'  unrelated: !SyntheticSecret |\n'
                             b'    "SYNTHETIC_PRIVATE_LITERAL": not-a-field\n'
                             b'    ? SYNTHETIC_PRIVATE_LITERAL\n')
            self.assertTrue(self.matches(kind, uninterpreted))


class ReportCanonicalPathTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='tamer-report-path-')
        self.addCleanup(temporary.cleanup)
        self.base = Path(temporary.name)
        self.project = self.base / 'long-project'
        self.alias = self.base / 'alternate-spelling'
        for root in [self.project, self.alias]:
            for name in ['Assets', 'Logs']:
                (root / name).mkdir(parents=True)

    def canonical_alias(self, path):
        # A logical resolver regression, not evidence of an OS-created alias.
        if path.is_relative_to(self.alias):
            return self.project / path.relative_to(self.alias)
        return path

    def test_mixed_canonical_root_and_output_aliases_cannot_bypass_logs(self):
        for root, parent in [(self.alias, self.project / 'Assets'),
                             (self.project, self.alias / 'Assets')]:
            output = parent / 'report.private.json'
            with self.subTest(root=root), patch.object(audit, 'canonical_directory',
                                                       side_effect=self.canonical_alias):
                with self.assertRaisesRegex(audit.PreflightError,
                                            'report_inside_project_requires_logs_directory'):
                    audit.validate_report_path(root, output)
                self.assertFalse(output.exists())

    def test_canonical_alias_logs_and_separate_directory_are_allowed(self):
        for root, parent in [(self.alias, self.project / 'Logs'),
                             (self.project, self.alias / 'Logs'), (self.project, self.base)]:
            with self.subTest(root=root, parent=parent), patch.object(audit, 'canonical_directory',
                                                                     side_effect=self.canonical_alias):
                audit.validate_report_path(root, parent / 'report.private.json')

    def test_canonical_resolution_failure_is_fail_closed(self):
        output = self.base / 'report.private.json'
        with patch.object(audit, 'canonical_directory', side_effect=OSError('synthetic resolver failure')):
            with self.assertRaises(OSError):
                audit.validate_report_path(self.project, output)
        self.assertFalse(output.exists())


@unittest.skipUnless(sys.platform == 'win32', 'Real Windows guarded handle checks required')
class GuardedSnapshotTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='tamer-operating-inputs-')
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.root = self.base / 'project'
        self.private = self.base / 'private-reports'
        self.private.mkdir()
        self.output = self.private / 'report.private.json'
        for relative in audit.INPUTS:
            (self.root / relative).parent.mkdir(parents=True, exist_ok=True)
        for relative in audit.SOURCE_PINS:
            self.write(relative, (ROOT / relative).read_bytes())  # public C# only
        self.write(audit.BUILD_INFO, b'0,0,26')
        self.write(audit.PLAYFAB, b'MonoBehaviour:\n  TitleId: ABC123\n'
                   b'  DeveloperSecretKey: SYNTHETIC_SECRET_NEVER_REPORT\n')
        self.write(audit.PGS_TEXT, PGS)
        self.write(audit.PGS_ASSET, ASSET)
        # Deliberately incompatible ProjectSettings code: never used as seed.
        self.write('ProjectSettings/ProjectSettings.asset', b'  AndroidBundleVersionCode: 999\n')
        self.write('src/AeDeong.keystore', b'SYNTHETIC_KEY_DO_NOT_READ')
        self.write('src/upload.der', b'SYNTHETIC_DER_DO_NOT_READ')

    def write(self, relative, raw):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)

    def run_audit(self, **kwargs):
        return audit.audit_project(self.root, self.output, TITLE, 26, now=NOW, **kwargs)

    def before(self):
        return {relative: (self.root / relative).read_bytes() for relative in audit.INPUTS
                if (self.root / relative).exists()}

    def assert_preserved(self, before):
        self.assertEqual(before, self.before())

    def test_pass_is_private_snapshot_only_and_preserves_all_inputs(self):
        before = self.before()
        opened = []
        actual = audit.LockedFile
        def observed(path, **kwargs):
            opened.append(Path(path))
            return actual(path, **kwargs)
        with patch.object(audit, 'LockedFile', side_effect=observed):
            result = self.run_audit(source_head='a' * 40)
        self.assertTrue(result['inputChecksPassed'])
        self.assertEqual(result['entry'], audit.LEGACY_ENTRY)
        self.assertEqual(result['buildEntryPoint'], 'Build/AOS/AAB')
        self.assertIsNone(result['approvedAdultReleasePlan'])
        self.assertFalse(result['releaseReady'])
        self.assertFalse(result['projectSettingsVersionCodeUsed'])
        self.assertFalse(result['sourceHeadIndependentlyVerified'])
        self.assertEqual(result['legacyAabPlan']['candidateVersionCode'], 27)
        self.assertEqual(opened, [self.root / p for p in audit.INPUTS if p != audit.FINISH])
        raw = self.output.read_text(encoding='utf-8')
        for forbidden in ['SYNTHETIC_SECRET_NEVER_REPORT', 'DeveloperSecretKey', '123456789',
                          'synthetic-client', str(self.root), 'SYNTHETIC_KEY_DO_NOT_READ',
                          hashlib.sha256(b'SYNTHETIC_KEY_DO_NOT_READ').hexdigest()]:
            self.assertNotIn(forbidden, raw)
        self.assertEqual(json.loads(raw), result)
        self.assert_preserved(before)

    def test_empty_title_and_seed_zero_are_both_reported_without_repair(self):
        self.write(audit.PLAYFAB, b'MonoBehaviour:\n  TitleId: \n')
        self.write(audit.BUILD_INFO, b'0,0,0')
        before = self.before()
        result = self.run_audit()
        self.assertFalse(result['inputChecksPassed'])
        self.assertFalse(result['checks']['playFabTitleMatchesExpected'])
        self.assertFalse(result['checks']['versionExceedsPublishedMaximum'])
        self.assertEqual(result['legacyAabPlan']['candidateVersionCode'], 1)
        self.assert_preserved(before)

    def test_finish_requires_acknowledgment_maximum_and_absent_aab(self):
        self.write(audit.FINISH, b'\xff\x00synthetic marker')
        self.write(audit.BUILD_INFO, b'01,02,027')
        result = self.run_audit()
        self.assertFalse(result['checks']['finishReuseConditionSatisfied'])
        self.output = self.private / 'acknowledged.private.json'
        self.assertTrue(self.run_audit(allow_finish_reuse=True)['inputChecksPassed'])
        self.output = self.private / 'published.private.json'
        result = audit.audit_project(self.root, self.output, TITLE, 27,
                                     now=NOW, allow_finish_reuse=True)
        self.assertFalse(result['checks']['versionExceedsPublishedMaximum'])
        self.write('Build/AOS/1.0.01.02.aab', b'preserve old synthetic AAB')
        self.output = self.private / 'collision.private.json'
        before = self.before()
        result = self.run_audit(allow_finish_reuse=True)
        self.assertFalse(result['checks']['plannedAabOutputAbsent'])
        self.assert_preserved(before)
        self.assertEqual((self.root / 'Build/AOS/1.0.01.02.aab').read_bytes(),
                         b'preserve old synthetic AAB')

    def test_missing_input_and_changed_source_are_rejected(self):
        missing = self.root / audit.PGS_ASSET
        raw = missing.read_bytes()
        missing.unlink()
        with self.assertRaises(OSError):
            self.run_audit()
        self.assertFalse(self.output.exists())
        missing.write_bytes(raw)
        self.write(audit.OUTPUT_PLAN, (self.root / audit.OUTPUT_PLAN).read_bytes() + b'// changed\n')
        result = self.run_audit()
        self.assertFalse(result['checks']['legacySourceContractMatches'])
        self.assertIsNone(result['legacyAabPlan'])

    def test_existing_report_input_collision_and_missing_finish_collision_preserve(self):
        before = self.before()
        for relative in (audit.PLAYFAB, audit.FINISH):
            self.output = self.root / relative
            with self.subTest(relative=relative), self.assertRaises(audit.PreflightError):
                self.run_audit()
        self.output = self.private / 'existing.private.json'
        self.output.write_bytes(b'preserve existing report')
        with self.assertRaises(audit.PreflightError):
            self.run_audit()
        self.assertEqual(self.output.read_bytes(), b'preserve existing report')
        self.assert_preserved(before)

    def test_output_creation_race_uses_exclusive_create_and_does_not_overwrite(self):
        before = self.before()
        original_open = Path.open
        def racing_open(path, mode='r', *args, **kwargs):
            if path == self.output and mode == 'xb':
                with original_open(path, 'xb') as competitor:
                    competitor.write(b'late competing report')
            return original_open(path, mode, *args, **kwargs)
        with patch.object(Path, 'open', racing_open), self.assertRaises(FileExistsError):
            self.run_audit()
        self.assertEqual(self.output.read_bytes(), b'late competing report')
        self.assert_preserved(before)

    def test_hardlinked_input_and_report_alias_reject_without_mutation(self):
        target = self.root / audit.BUILD_INFO
        alias = self.private / 'buildinfo-alias.private.json'
        os.link(target, alias)
        with self.assertRaises(audit.PreflightError):
            self.run_audit()
        self.output = alias
        with self.assertRaises(audit.PreflightError):
            self.run_audit()
        self.assertEqual(target.read_bytes(), b'0,0,26')

    def test_symlink_and_junction_input_ancestors_reject(self):
        target = self.root / audit.PGS_ASSET
        alias_target = self.base / 'synthetic-asset.txt'
        target.rename(alias_target)
        try:
            target.symlink_to(alias_target)
        except OSError:
            target.write_bytes(alias_target.read_bytes())
        else:
            with self.assertRaises((OSError, ValueError)):
                self.run_audit()
            target.unlink()
            target.write_bytes(alias_target.read_bytes())
        # A directory junction does not need Windows developer-mode symlink rights.
        buildinfo = self.root / 'BuildInfo'
        moved = self.base / 'synthetic-buildinfo'
        buildinfo.rename(moved)
        try:
            with fixture_junction(buildinfo, moved):
                with self.assertRaises((OSError, ValueError)):
                    self.run_audit()
        finally:
            moved.rename(buildinfo)
        self.assertEqual(target.read_bytes(), ASSET)

    def test_junction_report_parent_and_planned_aab_parent_reject(self):
        destination = self.base / 'synthetic-output-destination'
        destination.mkdir()
        alias = self.base / 'synthetic-private-alias'
        before = self.before()
        with fixture_junction(alias, destination):
            self.output = alias / 'report.private.json'
            with self.assertRaises((OSError, ValueError)):
                self.run_audit()
        self.assertEqual(list(destination.iterdir()), [])
        self.output = self.private / 'report.private.json'
        build = self.root / 'Build'
        build.mkdir()
        with fixture_junction(build / 'AOS', destination):
            with self.assertRaises((OSError, ValueError)):
                self.run_audit()
        self.assertEqual(list(destination.iterdir()), [])
        self.assertFalse(self.output.exists())
        self.assert_preserved(before)

    def test_original_junction_spelling_rejects_before_canonical_resolution(self):
        alias = self.base / 'synthetic-root-junction'
        with fixture_junction(alias, self.root), patch.object(audit, 'canonical_directory') as resolver:
            with self.assertRaisesRegex(audit.PreflightError, 'output_ancestor_not_regular_directory'):
                audit.validate_report_path(alias, self.output)
            resolver.assert_not_called()
        alias = self.base / 'synthetic-report-junction'
        with fixture_junction(alias, self.private), patch.object(audit, 'canonical_directory') as resolver:
            with self.assertRaisesRegex(audit.PreflightError, 'output_ancestor_not_regular_directory'):
                audit.validate_report_path(self.root, alias / 'report.private.json')
            resolver.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_real_windows_short_names_cannot_bypass_logs_rule(self):
        long_root = self.base / 'SyntheticLongProjectForShortNameCheck'
        self.root.rename(long_root)
        self.root = long_root
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.GetShortPathNameW.argtypes = [wintypes.LPCWSTR, wintypes.LPWSTR, wintypes.DWORD]
        kernel.GetShortPathNameW.restype = wintypes.DWORD
        size = kernel.GetShortPathNameW(str(long_root), None, 0)
        if not size:
            raise ctypes.WinError(ctypes.get_last_error())
        buffer = ctypes.create_unicode_buffer(size)
        length = kernel.GetShortPathNameW(str(long_root), buffer, size)
        if not length or length >= size:
            raise ctypes.WinError(ctypes.get_last_error())
        short_root = Path(buffer.value)
        if short_root == long_root:
            self.skipTest('OS returned no distinct 8.3 alias; volume settings were not changed')
        self.assertEqual(audit.canonical_directory(short_root), audit.canonical_directory(long_root))
        for root, parent in [(short_root, long_root / 'Assets'), (long_root, short_root / 'Assets')]:
            output = parent / 'report.private.json'
            with self.subTest(root=root), patch.object(audit, 'LockedFile') as opening:
                with self.assertRaisesRegex(audit.PreflightError,
                                            'report_inside_project_requires_logs_directory'):
                    audit.audit_project(root, output, TITLE, 26, now=NOW)
                opening.assert_not_called()
                self.assertFalse(output.exists())
        (long_root / 'Logs').mkdir()
        output = long_root / 'Logs' / 'allowed.private.json'
        result = audit.audit_project(short_root, output, TITLE, 26, now=NOW)
        self.assertTrue(result['inputChecksPassed'])
        self.assertFalse(result['releaseReady'])

    def test_final_report_alias_check_holds_root_and_output_parent_locks(self):
        original_locks = audit.locked_directories
        original_resolver = audit.canonical_directory
        batches = []
        @contextmanager
        def recording_locks(paths):
            batches.append(list(paths))
            with original_locks(paths):
                yield
        def late_alias(path):
            if batches and batches[-1] == [self.root, self.output.parent] and path == self.output.parent:
                return self.root / 'Assets'
            return original_resolver(path)
        before = self.before()
        with patch.object(audit, 'locked_directories', side_effect=recording_locks), \
                patch.object(audit, 'canonical_directory', side_effect=late_alias):
            with self.assertRaisesRegex(audit.PreflightError,
                                        'report_inside_project_requires_logs_directory'):
                self.run_audit()
        self.assertEqual(batches[-1], [self.root, self.output.parent])
        self.assertFalse(self.output.exists())
        self.assert_preserved(before)

    def test_report_equal_to_project_root_has_fixed_cli_error_before_open(self):
        args = ['--project-path', str(self.root), '--expected-playfab-title', TITLE,
                '--published-max-code', '26', '--output', str(self.root)]
        stdout = io.StringIO()
        before = self.before()
        with patch.object(audit, 'LockedFile') as opening, redirect_stdout(stdout):
            self.assertEqual(audit.main(args), 2)
        opening.assert_not_called()
        self.assertEqual(json.loads(stdout.getvalue()), {
            'inputChecksPassed': False, 'releaseReady': False,
            'issues': ['report_cannot_be_project_root']})
        self.assertNotIn(str(self.root), stdout.getvalue())
        self.assert_preserved(before)

    def test_finish_appearance_during_read_is_rejected(self):
        original = audit.playfab_title_matches
        def late_marker(raw, expected):
            self.write(audit.FINISH, b'late synthetic marker')
            return original(raw, expected)
        with patch.object(audit, 'playfab_title_matches', side_effect=late_marker), \
                self.assertRaisesRegex(audit.PreflightError, 'finish_marker_appeared'):
            self.run_audit()
        self.assertFalse(self.output.exists())

    def test_snapshot_handles_block_input_writes_then_release_for_later_owner(self):
        before = self.before()
        original = audit.playfab_title_matches
        attempted = []
        def writer_probe(raw, expected):
            with self.assertRaises(OSError):
                (self.root / audit.BUILD_INFO).write_bytes(b'1,2,99')
            with self.assertRaises(OSError):
                (self.root / audit.BUILD_INFO).rename(self.root / 'BuildInfo/moved.txt')
            attempted.append(True)
            return original(raw, expected)
        with patch.object(audit, 'playfab_title_matches', side_effect=writer_probe):
            self.assertTrue(self.run_audit()['inputChecksPassed'])
        self.assertEqual(attempted, [True])
        self.assert_preserved(before)
        self.write(audit.BUILD_INFO, b'1,2,99')
        self.assertEqual((self.root / audit.BUILD_INFO).read_bytes(), b'1,2,99')

    def test_unsafe_report_paths_fail_before_any_input_is_opened(self):
        for output in [Path('relative.private.json'), self.private / 'report.json',
                       self.root / 'Assets/new.private.json',
                       self.private / 'missing' / 'report.private.json']:
            self.output = output
            with self.subTest(output=output), patch.object(audit, 'LockedFile') as opening:
                with self.assertRaises((OSError, ValueError)):
                    self.run_audit()
                opening.assert_not_called()

    def test_io_error_stdout_excludes_exception_paths(self):
        self.output.write_bytes(b'previous private report')
        stdout = io.StringIO()
        args = ['--project-path', str(self.root), '--expected-playfab-title', TITLE,
                '--published-max-code', '26', '--output', str(self.output)]
        with redirect_stdout(stdout):
            self.assertEqual(audit.main(args), 2)
        result = json.loads(stdout.getvalue())
        self.assertFalse(result['inputChecksPassed'])
        self.assertFalse(result['releaseReady'])
        self.assertNotIn(str(self.root), stdout.getvalue())
        self.assertNotIn(str(self.output), stdout.getvalue())
        self.assertEqual(self.output.read_bytes(), b'previous private report')

    def test_cli_captures_time_once_and_stdout_never_dumps_private_report(self):
        args = ['--project-path', str(self.root), '--expected-playfab-title', TITLE,
                '--published-max-code', '26', '--output', str(self.output)]
        stdout = io.StringIO()
        with patch.object(audit, 'capture_local_time', return_value=NOW) as clock, redirect_stdout(stdout):
            self.assertEqual(audit.main(args), 0)
        clock.assert_called_once_with()
        self.assertEqual(json.loads(stdout.getvalue()), {
            'inputChecksPassed': True, 'releaseReady': False, 'issues': []})
        self.assertEqual(json.loads(self.output.read_text())['capturedLocalTime'], NOW.isoformat())


class CommandLineTests(unittest.TestCase):
    def test_required_duplicate_malformed_or_out_of_range_arguments_reject(self):
        args = ['--project-path', 'D:/synthetic/project', '--expected-playfab-title', TITLE,
                '--published-max-code', '26', '--output', 'D:/synthetic/report.private.json']
        for invalid in [[], args + ['--published-max-code', '27'],
                        args + ['--allow-finish-reuse', '--allow-finish-reuse'],
                        [*args[:5], '-1', *args[6:]], [*args[:5], '026', *args[6:]],
                        [*args[:5], '2.6', *args[6:]]]:
            with self.subTest(args=invalid), redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                audit.main(invalid)
        for title, maximum, head in [('not-a-title', 26, None), (TITLE, -1, None),
                                      (TITLE, audit.ANDROID_MAX + 1, None),
                                      (TITLE, True, None), (TITLE, 26, 'short')]:
            with self.subTest(title=title, maximum=maximum), self.assertRaises(audit.PreflightError):
                audit.validate_arguments(title, maximum, head)


if __name__ == '__main__':
    unittest.main()
