import io
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
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


class BackupFailureFlowTests(unittest.TestCase):
    def test_production_flow_cancellation_cleanup_replacement_and_success(self):
        for failure in ('acl', 'pinentry', 'cleanup', 'replacement', None):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                target = root / 'owned'
                target.mkdir()
                inputs = {}
                for role in ('keystore', 'certificate', 'recovery-note'):
                    inputs[role] = root / role
                    inputs[role].write_bytes(role.encode())
                contents, _ = backup.collect(inputs)
                encrypted = target / 'recovery.zip.gpg'

                def owner(_):
                    if failure == 'acl':
                        raise ValueError('Synthetic ACL rejection')
                    return target

                def gpg(home, arguments, payload=None):
                    if failure == 'pinentry':
                        raise ValueError('Synthetic cancellation')
                    if '--symmetric' in arguments:
                        encrypted.write_bytes(b'encrypted dummy')
                        return b''
                    self.assertEqual(payload, b'encrypted dummy')
                    return backup.pack(contents)

                def cleanup(*args, **kwargs):
                    if failure == 'cleanup':
                        raise subprocess.CalledProcessError(1, 'synthetic-cleanup')
                    if failure == 'replacement':
                        encrypted.write_bytes(b'replacement')
                    return subprocess.CompletedProcess([], 0)

                with patch.object(backup, 'owner_directory', side_effect=owner), \
                     patch.object(backup, 'gpg_run', side_effect=gpg) as run, \
                     patch.object(backup.subprocess, 'run', side_effect=cleanup):
                    if failure:
                        with self.assertRaises((ValueError, subprocess.CalledProcessError)):
                            backup.create_backup(inputs, root)
                        self.assertFalse((target / 'verified-receipt.json').exists())
                        if failure == 'acl':
                            run.assert_not_called()
                    else:
                        self.assertEqual(backup.create_backup(inputs, root), encrypted)
                        receipt = __import__('json').loads((target / 'verified-receipt.json').read_text())
                        self.assertTrue(receipt['verified'])
                        self.assertEqual(receipt['encryptedSha256'], backup.digest(encrypted.read_bytes()))

    def test_receipt_write_failure_does_not_publish_partial_success(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            with patch.object(backup.json, 'dump', side_effect=OSError('Synthetic disk failure')):
                with self.assertRaises(OSError):
                    backup.publish_receipt(target, {'verified': True})
            self.assertFalse((target / 'verified-receipt.json').exists())
            self.assertFalse((target / 'verified-receipt.pending').exists())


if __name__ == '__main__':
    unittest.main()
