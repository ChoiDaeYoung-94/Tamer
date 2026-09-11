import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import backup_private_assets as backup


class PrivateBackupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        self.dest = self.root / 'snapshot'
        self.manifest = self.root / 'manifest.json'
        self.asset = 'Assets/ThirdPartyAssets/example.meta'
        self.data = b'guid: 01234567890123456789012345678901\n'
        path = self.source / self.asset
        path.parent.mkdir(parents=True)
        path.write_bytes(self.data)
        self.entry = dict(path=self.asset, bytes=len(self.data),
                          sha256=hashlib.sha256(self.data).hexdigest(),
                          disposition='private-restore')
        self.write_manifest([self.entry])

    def write_manifest(self, entries):
        self.manifest.write_text(json.dumps(dict(entries=entries)), encoding='utf-8')

    def test_snapshot_is_exact_and_preserves_source_and_guid(self):
        secret = self.source / 'Assets/ThirdParty/PlayFabSharedSettings.asset'
        secret.parent.mkdir(parents=True)
        secret.write_text('private service setting')
        self.write_manifest([self.entry,
                             dict(path=secret.relative_to(self.source).as_posix(),
                                  disposition='local-service-settings-excluded'),
                             dict(path='Assets/ThirdParty/SDK.cs', disposition='git-sdk')])
        receipt = backup.create(self.source, self.dest, self.manifest)
        self.assertEqual(1, receipt['files'])
        self.assertEqual(self.data, (self.dest / self.asset).read_bytes())
        self.assertEqual(self.data, (self.source / self.asset).read_bytes())
        self.assertEqual(receipt, backup.verify(self.dest, self.manifest))
        self.assertFalse((self.dest / 'Assets/ThirdParty').exists())

    def test_source_mismatch_creates_no_destination(self):
        (self.source / self.asset).write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            backup.create(self.source, self.dest, self.manifest)
        self.assertFalse(self.dest.exists())

    def test_existing_destination_is_preserved(self):
        self.dest.mkdir()
        marker = self.dest / 'owner.txt'
        marker.write_text('preserve')
        with self.assertRaises(FileExistsError):
            backup.create(self.source, self.dest, self.manifest)
        self.assertEqual('preserve', marker.read_text())

    def test_path_escape_and_sensitive_material_are_rejected(self):
        for path in ('../escape', '/absolute', 'C:/escape',
                     'Assets/ThirdPartyAssets/../secret',
                     'Assets\\ThirdPartyAssets\\secret',
                     'Assets/ThirdParty/PlayFabSharedSettings.asset',
                     'Assets/ThirdParty/private.keystore', 'Library/cache'):
            with self.subTest(path=path):
                self.write_manifest([dict(self.entry, path=path)])
                with self.assertRaises(ValueError):
                    backup.create(self.source, self.dest, self.manifest)
                self.assertFalse(self.dest.exists())

    def test_nested_destination_and_duplicate_paths_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'non-nested'):
            backup.create(self.source, self.source / 'snapshot', self.manifest)
        self.write_manifest([self.entry, dict(self.entry, path=self.asset.replace('example', 'EXAMPLE'))])
        with self.assertRaises(ValueError):
            backup.create(self.source, self.dest, self.manifest)
        self.assertFalse(self.dest.exists())

    def test_missing_corrupt_and_extra_files_are_not_a_valid_backup(self):
        backup.create(self.source, self.dest, self.manifest)
        asset = self.dest / self.asset
        asset.write_bytes(b'corrupt')
        with self.assertRaises(ValueError):
            backup.verify(self.dest, self.manifest)
        asset.write_bytes(self.data)
        extra = self.dest / 'unexpected.txt'
        extra.write_text('unlisted')
        with self.assertRaises(ValueError):
            backup.verify(self.dest, self.manifest)
        extra.unlink()
        asset.unlink()
        with self.assertRaises(ValueError):
            backup.verify(self.dest, self.manifest)

    def test_absent_receipt_or_different_manifest_is_not_complete(self):
        backup.create(self.source, self.dest, self.manifest)
        data = json.loads(self.manifest.read_text())
        data['source'] = 'different manifest'
        self.manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'receipt'):
            backup.verify(self.dest, self.manifest)
        (self.dest / backup.RECEIPT).unlink()
        with self.assertRaises(FileNotFoundError):
            backup.verify(self.dest, self.manifest)

    def test_manifest_formatting_on_another_host_does_not_invalidate_backup(self):
        receipt = backup.create(self.source, self.dest, self.manifest)
        data = json.loads(self.manifest.read_text())
        self.manifest.write_bytes(json.dumps(data, indent=2).replace('\n', '\r\n').encode('utf-8'))
        self.assertEqual(receipt, backup.verify(self.dest, self.manifest))


if __name__ == '__main__':
    unittest.main()
