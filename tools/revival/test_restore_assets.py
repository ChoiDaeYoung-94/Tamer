import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import restore_assets as restore


class RestoreTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / 'clone'
        self.source = self.base / 'source'
        self.root.mkdir()
        self.source.mkdir()
        self.manifest = self.base / 'manifest.json'
        self.files = {'Assets/a.meta': b'guid: 01234567890123456789012345678901\n', 'Assets/b.txt': b'original'}
        entries = []
        for name, data in self.files.items():
            file = self.source / name
            file.parent.mkdir(exist_ok=True, parents=True)
            file.write_bytes(data)
            entries.append(dict(path=name, sha256=hashlib.sha256(data).hexdigest(), disposition='private-restore'))
        self.manifest.write_text(json.dumps(dict(entries=entries)))
        for key, value in [('ROOT', self.root), ('MANIFEST', self.manifest)]:
            context = patch.object(restore, key, value)
            context.start()
            self.addCleanup(context.stop)

    def test_restore_is_idempotent_and_preserves_source_and_guid(self):
        restore.restore(self.source)
        restore.restore(self.source)
        restore.restore(self.source, verify=True)
        for name, data in self.files.items():
            self.assertEqual(data, (self.root / name).read_bytes())
            self.assertEqual(data, (self.source / name).read_bytes())

    def test_conflict_aborts_before_any_copy(self):
        dest = self.root / 'Assets/b.txt'
        dest.parent.mkdir()
        dest.write_bytes(b'user edit')
        with self.assertRaisesRegex(ValueError, 'preserving'):
            restore.restore(self.source)
        self.assertFalse((self.root / 'Assets/a.meta').exists())
        self.assertEqual(dest.read_bytes(), b'user edit')

    def test_path_escape_is_rejected(self):
        self.manifest.write_text(json.dumps(dict(entries=[dict(path='../escape', disposition='private-restore')])) )
        with self.assertRaisesRegex(ValueError, 'escapes'):
            restore.restore(self.source)

    def test_missing_upgraded_sdk_never_falls_back_to_legacy_archive(self):
        entries = json.loads(self.manifest.read_text())['entries']
        entries[1]['disposition'] = 'git-sdk'
        self.manifest.write_text(json.dumps(dict(entries=entries)))
        with self.assertRaisesRegex(ValueError, 'restore from Git'):
            restore.restore(self.source)
        self.assertFalse((self.root / 'Assets/a.meta').exists())
        self.assertFalse((self.root / 'Assets/b.txt').exists())


if __name__ == '__main__':
    unittest.main()
