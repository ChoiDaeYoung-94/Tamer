import io
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

import prepare_encrypted_key_backup as backup


class EncryptedBackupTests(unittest.TestCase):
    def test_real_gpg_dummy_stream_roundtrip_and_tamper(self):
        if not backup.GPG.is_file():
            self.fail('Required existing GPG is unavailable')
        # Public synthetic test password, never a production credential.
        with tempfile.TemporaryDirectory() as directory:
            home = Path(directory)
            command = [str(backup.GPG), '--no-options', '--homedir', backup.gpg_path(home),
                       '--batch', '--yes', '--no-symkey-cache', '--pinentry-mode', 'loopback',
                       '--passphrase', 'PUBLIC-SYNTHETIC-TEST-ONLY']
            contents = {'keystore': b'dummy-key', 'certificate': b'dummy-public-cert',
                        'recovery-note': b'dummy-recovery-note'}
            try:
                encrypted = subprocess.run(command + ['--symmetric', '--cipher-algo', 'AES256'],
                                           input=backup.pack(contents), capture_output=True, check=True).stdout
                plain = subprocess.run(command + ['--decrypt'], input=encrypted,
                                       capture_output=True, check=True).stdout
                backup.verify_plaintext(plain, contents)
                changed = dict(contents, keystore=b'changed-key')
                with self.assertRaises(ValueError):
                    backup.verify_plaintext(plain, changed)
                broken = bytearray(encrypted)
                broken[-8] ^= 1
                result = subprocess.run(command + ['--decrypt'], input=broken, capture_output=True)
                self.assertNotEqual(result.returncode, 0)
                self.assertFalse(any(home.glob('*.zip')))
            finally:
                subprocess.run([str(backup.GPG.parent / 'gpgconf.exe'), '--homedir', backup.gpg_path(home),
                                '--kill', 'gpg-agent'], capture_output=True)

    def test_exact_inventory_and_source_mutation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            inputs = {}
            for role in ('keystore', 'certificate', 'recovery-note'):
                inputs[role] = root / role
                inputs[role].write_bytes(role.encode())
            contents, snapshots = backup.collect(inputs)
            backup.unchanged(snapshots)
            inputs['keystore'].write_bytes(b'changed')
            with self.assertRaises(ValueError):
                backup.unchanged(snapshots)
            with self.assertRaises(ValueError):
                backup.collect(dict(inputs, certificate=inputs['keystore']))
            stream = io.BytesIO(backup.pack(contents))
            with zipfile.ZipFile(stream, 'a') as archive:
                archive.writestr('unexpected', b'extra')
            with self.assertRaises(ValueError):
                backup.verify_plaintext(stream.getvalue(), contents)


if __name__ == '__main__':
    unittest.main()
