"""Synthetic records/ELF ZIPs and mocked SDK calls only; no real APK/SDK use."""

from contextlib import redirect_stderr, redirect_stdout
import hashlib
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile

from test_verify_native_alignment import elf_fixture
import verify_play_artifacts as verify


APP = 'a' * 64
OTHER = 'b' * 64
STAMP = 'c' * 64
SIGNING = f'Signer #1 certificate SHA-256 digest: {APP}\nSource Stamp Signer certificate SHA-256 digest: {STAMP}\n'
BADGING = """package: name='org.example.synthetic' versionCode='29' versionName='1.2.3' platformBuildVersionName='16' platformBuildVersionCode='36' compileSdkVersion='36' compileSdkVersionCodename='16'
sdkVersion:'25'
targetSdkVersion:'36'
application-label:'application-debuggable name=org.example.unrelated'
native-code: 'arm64-v8a'
"""
LIB = 'lib/arm64-v8a/libsynthetic.so'


class RecordTests(unittest.TestCase):
    def test_app_stamp_and_explicit_multi_signer_sets(self):
        result = verify.parse_signers(SIGNING, [APP.upper()])
        self.assertEqual([APP], result['applicationSignerCertificateSha256'])
        self.assertEqual([STAMP], result['sourceStampCertificateSha256'])
        self.assertFalse(result['sourceStampIsAppSigner'])
        self.assertEqual([], verify.parse_signers(SIGNING.splitlines()[0], [APP])['sourceStampCertificateSha256'])
        self.assertEqual([APP], verify.parse_signers('Number of signers: 1\n' + SIGNING, [APP])['applicationSignerCertificateSha256'])
        multi = SIGNING + f'Signer #2 certificate SHA-256 digest: {OTHER}\n'
        self.assertEqual([APP, OTHER], verify.parse_signers(multi, [OTHER, APP])['applicationSignerCertificateSha256'])
        with self.assertRaises(ValueError):
            verify.parse_signers(multi, [APP])

    def test_stamp_only_matching_stamp_or_other_app_cannot_pass(self):
        for output, expected in ((SIGNING, [STAMP]),
                (f'Source Stamp Signer certificate SHA-256 digest: {APP}', [APP]),
                (SIGNING.replace(APP, OTHER), [APP]), ('', [APP])):
            with self.subTest(output=output), self.assertRaises(ValueError):
                verify.parse_signers(output, expected)

    def test_duplicate_incomplete_and_malformed_signer_records_fail(self):
        invalid = [SIGNING + SIGNING.splitlines()[0], SIGNING + SIGNING.splitlines()[1],
            SIGNING + f'Signer #1 certificate SHA-256 digest: {OTHER}',
            SIGNING + f'Signer #2 certificate SHA-256 digest: {APP}',
            SIGNING + 'Signer #2 certificate DN: CN=Synthetic',
            SIGNING.replace('#1', '#2'), SIGNING.replace('#1', '#0'),
            SIGNING.replace(APP, 'z' * 64), SIGNING.replace(APP, APP + 'a'),
            SIGNING + f'Unknown certificate SHA-256 digest: {APP}',
            SIGNING + f' Signer #2 certificate SHA-256 digest: {OTHER}',
            SIGNING + 'Number of signers: 2\n',
            SIGNING + 'Number of signers: 1\nNumber of signers: 1\n',
            SIGNING + 'Signer #1 certificate DN: CN=One\nSigner #1 certificate DN: CN=Two\n',
            SIGNING.replace(STAMP, 'short')]
        for output in invalid:
            with self.subTest(output=output), self.assertRaises(ValueError):
                verify.parse_signers(output, [APP])
        for expected in ([], [APP, APP.upper()], ['invalid']):
            with self.subTest(expected=expected), self.assertRaises(ValueError):
                verify.parse_signers(SIGNING, expected)

    def test_first_package_fields_do_not_collide_with_compile_codename(self):
        result = verify.parse_badging(BADGING)
        self.assertEqual('org.example.synthetic', result['applicationId'])
        self.assertEqual(29, result['versionCode'])
        self.assertEqual('1.2.3', result['versionName'])
        self.assertFalse(result['debuggable'])
        self.assertEqual(24, verify.parse_badging(BADGING.replace("sdkVersion:'25'", "sdkVersion:'24'"))['minSdk'])
        self.assertTrue(verify.parse_badging(BADGING + 'application-debuggable\n')['debuggable'])

    def test_metadata_duplicates_malformed_and_substrings_cannot_supply_fields(self):
        invalid = [BADGING + BADGING.splitlines()[0],
            BADGING.replace("name='org.example.synthetic'", "name='org.example.synthetic' name='other'"),
            BADGING.replace("name='org.example.synthetic' ", ''),
            BADGING.replace("versionCode='29'", "versionCode='29' versionCode='30'"),
            BADGING.replace("versionCode='29' ", ''),
            BADGING.replace("sdkVersion:'25'", "application-label:'sdkVersion:\'25\''"),
            BADGING + "sdkVersion:'25'\n", BADGING + "minSdkVersion:'25'\n",
            BADGING + "targetSdkVersion:'36'\n", BADGING.replace("targetSdkVersion:'36'", "other-targetSdkVersion:'36'"),
            BADGING + "native-code: 'arm64-v8a'\n",
            BADGING.replace("native-code: 'arm64-v8a'", "native-code: 'arm64-v8a' 'arm64-v8a'"),
            BADGING + 'application-debuggable:true\n',
            BADGING + 'application-debuggable\napplication-debuggable\n',
            'warning\n' + BADGING]
        for output in invalid:
            with self.subTest(output=output), self.assertRaises(ValueError):
                verify.parse_badging(output)


class ArtifactTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.base = Path(directory.name).resolve()
        self.aab, self.apk = self.base / 'synthetic.aab', self.base / 'synthetic.apk'
        self.android = self.base / 'AndroidPlayer'
        for relative in ('SDK/build-tools/36.0.0/aapt2.exe',
                         'SDK/build-tools/36.0.0/lib/apksigner.jar', 'OpenJDK/bin/java.exe'):
            target = self.android / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.touch()  # Never executed: subprocess.run is always mocked.
        self.payload = elf_fixture([dict(), dict(kind=0x6474E552, vaddr=4096, memsz=4096)])
        self.write_zip(self.aab, [('base/' + LIB, self.payload)])
        self.write_zip(self.apk, [(LIB, self.payload)])
        self.options = dict(original_aab=self.aab, play_apk=self.apk,
            expected_aab_sha256=self.digest(self.aab), expected_package='org.example.synthetic',
            expected_version_code=29, expected_version_name='1.2.3', expected_min_sdk=25,
            expected_target_sdk=36, expected_debuggable=False, expected_abis=['arm64-v8a'],
            expected_app_cert_sha256=[APP], android_player=self.android)
        self.calls = []
        self.badging, self.signing = BADGING, SIGNING
        self.sdk_exit = 0
        mock = patch.object(verify.subprocess, 'run', side_effect=self.sdk)
        self.sdk_mock = mock.start()
        self.addCleanup(mock.stop)

    @staticmethod
    def digest(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()

    @staticmethod
    def write_zip(path, entries, compression=zipfile.ZIP_DEFLATED):
        with warnings.catch_warnings():
            warnings.simplefilter('ignore', UserWarning)
            with zipfile.ZipFile(path, 'w', compression=compression) as archive:
                for name, raw in entries:
                    archive.writestr(name, raw)

    def sdk(self, command, **kwargs):
        self.calls.append(command)
        output = self.badging if Path(command[0]).name == 'aapt2.exe' else self.signing
        return subprocess.CompletedProcess(command, self.sdk_exit, stdout=output, stderr='synthetic diagnostic')

    def cli(self, output):
        args = ['--original-aab', str(self.aab), '--play-apk', str(self.apk),
            '--android-player', str(self.android), '--output', str(output)]
        for key in ('aab_sha256', 'package', 'version_code', 'version_name', 'min_sdk', 'target_sdk', 'debuggable'):
            value = self.options['expected_' + key]
            args += ['--expected-' + key.replace('_', '-'), str(value).lower() if isinstance(value, bool) else str(value)]
        args += ['--expected-abi', 'arm64-v8a', '--expected-app-cert-sha256', APP]
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            return verify.main(args)

    def test_static_success_preserves_inputs_and_keeps_relro_runtime_separate(self):
        before = (self.aab.read_bytes(), self.apk.read_bytes())
        output = self.base / 'success.private.json'
        self.assertEqual(0, self.cli(output))
        result = json.loads(output.read_text())
        self.assertTrue(result['staticArtifactChecksPassed'])
        self.assertTrue(result['loadZipChecksPassed'])
        self.assertFalse(result['relroChecksPassed'])
        for field in ('strictRelroExecuted', 'runtime16KBVerified', 'aabUploadSignatureVerified',
                      'installed', 'deviceSpecificSplitVerified', 'policyComplianceVerified', 'releaseReady'):
            self.assertFalse(result[field], field)
        self.assertEqual({LIB: hashlib.sha256(self.payload).hexdigest()}, result['nativePayloadSha256'])
        self.assertEqual(before, (self.aab.read_bytes(), self.apk.read_bytes()))
        self.assertEqual(['-jar', str(self.android / 'SDK/build-tools/36.0.0/lib/apksigner.jar'),
                          'verify', '--print-certs', str(self.apk)], self.calls[1][1:])

    def test_exit_nonzero_even_with_matching_certificate_and_metadata(self):
        self.sdk_exit = 1
        output = self.base / 'failure.json'
        self.assertEqual(1, self.cli(output))
        self.assertFalse(output.exists())
        # Exercise the signer exit guard after a successful aapt call as well.
        def signer_fails(command, **kwargs):
            result = self.sdk(command, **kwargs)
            result.returncode = 0 if Path(command[0]).name == 'aapt2.exe' else 1
            return result
        self.sdk_mock.side_effect = signer_fails
        self.assertEqual(1, self.cli(output))
        self.assertFalse(output.exists())

    def test_wrong_hash_or_metadata_or_signer_yields_no_report(self):
        for key, wrong in (('expected_aab_sha256', '0' * 64), ('expected_package', 'org.example.other'),
                ('expected_version_code', 30), ('expected_version_name', 'different'),
                ('expected_min_sdk', 24), ('expected_target_sdk', 35), ('expected_debuggable', True),
                ('expected_abis', ['x86_64'])):
            old = self.options[key]
            with self.subTest(key=key), self.assertRaises(ValueError):
                verify.verify_artifacts(**dict(self.options, **{key: wrong}))
            self.assertEqual(old, self.options[key])
        self.signing = SIGNING.replace(APP, OTHER)
        output = self.base / 'failure.json'
        self.assertEqual(1, self.cli(output))
        self.assertFalse(output.exists())
        self.options['expected_min_sdk'] = 24
        self.badging = BADGING.replace("sdkVersion:'25'", "sdkVersion:'24'")
        self.signing = SIGNING
        self.assertEqual(24, verify.verify_artifacts(**self.options)['minSdk'])

    def test_native_set_missing_extra_payload_and_duplicates_fail(self):
        other = LIB.replace('libsynthetic', 'libother')
        for entries in ([], [(other, self.payload)], [(LIB, self.payload), (other, self.payload)],
                        [(LIB, self.payload + b'changed')], [(LIB, self.payload), (LIB, self.payload)]):
            with self.subTest(entries=[name for name, _ in entries]):
                self.write_zip(self.apk, entries)
                with self.assertRaises(ValueError):
                    verify.verify_artifacts(**self.options)
        self.write_zip(self.apk, [(LIB, self.payload)])
        self.write_zip(self.aab, [('base/' + LIB, self.payload), ('base/' + other, self.payload)])
        self.options['expected_aab_sha256'] = self.digest(self.aab)
        with self.assertRaises(ValueError):
            verify.verify_artifacts(**self.options)

    def test_bad_load_or_zip_alignment_fails_even_when_payloads_match(self):
        for raw, compression in ((elf_fixture([dict(align=4096)]), zipfile.ZIP_DEFLATED),
                                 (self.payload, zipfile.ZIP_STORED)):
            with self.subTest(compression=compression):
                self.write_zip(self.aab, [('base/' + LIB, raw)])
                self.write_zip(self.apk, [(LIB, raw)], compression)
                self.options['expected_aab_sha256'] = self.digest(self.aab)
                output = self.base / 'failure.json'
                self.assertEqual(1, self.cli(output))
                self.assertFalse(output.exists())

    def test_existing_output_input_alias_and_relative_paths_fail_before_sdk(self):
        existing = self.base / 'existing.json'
        existing.write_bytes(b'preserve')
        for output in (existing, self.aab, self.apk, Path('relative.json')):
            with self.subTest(output=output):
                self.assertEqual(1, self.cli(output))
        self.assertEqual(b'preserve', existing.read_bytes())
        self.sdk_mock.assert_not_called()
        for key in ('original_aab', 'play_apk', 'android_player'):
            with self.subTest(key=key), self.assertRaises(ValueError):
                verify.verify_artifacts(**dict(self.options, **{key: Path('relative')}))


if __name__ == '__main__':
    unittest.main()
