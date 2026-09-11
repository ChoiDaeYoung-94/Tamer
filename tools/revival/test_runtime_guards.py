import hashlib
import subprocess
import tempfile
import unittest
from pathlib import Path

from verify_emulator_runtime import APP_ID, require_offline_results, validate_provenance


class RuntimeGuardTests(unittest.TestCase):
    def test_only_observed_loopback_is_offline(self):
        require_offline_results([subprocess.CompletedProcess([], 0, ''), subprocess.CompletedProcess([], 0, ''),
                                subprocess.CompletedProcess([], 0, '1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536')])

    def test_failed_or_unknown_network_state_is_rejected(self):
        for code, output in [(1, ''), (0, ''), (0, 'unexpected output'),
                             (0, '1: lo: <UP>\n2: wlan0: <UP,LOWER_UP> mtu 1500')]:
            with self.subTest(code=code, output=output), self.assertRaises(ValueError):
                require_offline_results([subprocess.CompletedProcess([], code, output)])

    def test_failed_disable_command_blocks_even_with_loopback(self):
        with self.assertRaises(ValueError):
            require_offline_results([subprocess.CompletedProcess([], 1, ''),
                                    subprocess.CompletedProcess([], 0, '1: lo: <LOOPBACK,UP>')])

    def test_provenance_binds_hash_identity_and_first_scene(self):
        with tempfile.TemporaryDirectory() as temp:
            artifact = Path(temp) / 'fixture.bin'
            artifact.write_bytes(b'synthetic artifact')
            digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
            valid = dict(artifactSha256=digest, applicationId=APP_ID,
                         entryScene='Assets/Tests/Scenes/RevivalSmoke.unity', buildCodeCommit='synthetic-test')
            self.assertEqual(validate_provenance(artifact, digest, valid), digest)
            for field, value in [('artifactSha256', '0' * 64), ('applicationId', 'production'),
                                 ('entryScene', 'Assets/Scenes/Login.unity'), ('buildCodeCommit', '')]:
                invalid = dict(valid, **{field: value})
                with self.subTest(field=field), self.assertRaises(ValueError):
                    validate_provenance(artifact, digest, invalid)
            with self.assertRaises(ValueError):
                validate_provenance(artifact, '0' * 64, valid)


if __name__ == '__main__':
    unittest.main()
