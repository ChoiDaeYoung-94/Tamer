import hashlib
import json
import tempfile
import unittest
import subprocess
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


class TrackedSdkManifestTests(unittest.TestCase):
    def test_every_git_sdk_manifest_entry_is_tracked(self):
        root = Path(__file__).resolve().parents[2]
        tracked = set(subprocess.check_output(
            ['git', '-C', str(root), 'ls-files', '--cached'], text=True,
            encoding='utf-8').splitlines())
        entries = json.loads((root / 'docs/revival/assets-manifest.json').read_text(
            encoding='utf-8'))['entries']
        missing = [entry['path'] for entry in entries
                   if entry['disposition'] == 'git-sdk' and entry['path'] not in tracked]
        self.assertEqual([], missing, 'git-sdk entries must survive a clean clone')


class ReviewedSdkMetaTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / 'clone'
        self.source = self.base / 'archive'
        self.root.mkdir()
        self.source.mkdir()
        self.manifest = self.base / 'manifest.json'
        self.ledger = self.base / 'migrations.json'
        self.entries, self.rows = [], []
        for number in range(7):
            path = f'Assets/ThirdParty/SDK/icon{number}.png.meta'
            guid = f'{number:032x}'
            old = f'guid: {guid}\nTextureImporter:\n  serializedVersion: 3\n'.encode()
            new = old.replace(b': 3', b': 4')
            dest = self.root / path
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(new)
            info = lambda data: dict(sha256=hashlib.sha256(data).hexdigest(), bytes=len(data), guid=guid)
            self.entries.append(dict(path=path, disposition='git-sdk', **info(old)))
            self.rows.append(dict(path=path, old=info(old), new=info(new)))
        self.git('init', '-q')
        self.git('add', '.')
        self.git('-c', 'user.name=fixture', '-c', 'user.email=fixture@example.invalid',
                 'commit', '-qm', 'synthetic migration')
        self.commit = self.git('rev-parse', 'HEAD').decode().strip()
        for row in self.rows:
            row['migrationCommit'] = self.commit
        self.write_metadata()
        for key, value in [('ROOT', self.root), ('MANIFEST', self.manifest),
                           ('SDK_META_MIGRATIONS', self.ledger)]:
            context = patch.object(restore, key, value)
            context.start()
            self.addCleanup(context.stop)

    def git(self, *arguments):
        return subprocess.check_output(['git', '-C', str(self.root), *arguments], stderr=subprocess.PIPE)

    def write_metadata(self):
        self.manifest.write_text(json.dumps(dict(entries=self.entries)))
        self.ledger.write_text(json.dumps(dict(schema=1, entries=self.rows)))

    def test_seven_registered_clean_migrations_preserve_bytes_and_inventory(self):
        originals = {(self.root/e['path']): (self.root/e['path']).read_bytes() for e in self.entries}
        manifest = self.manifest.read_bytes()
        restore.restore(self.source, verify=True)
        self.assertEqual(manifest, self.manifest.read_bytes())
        for path, data in originals.items():
            self.assertEqual(data, path.read_bytes())

    def test_unregistered_and_modified_migrations_are_rejected(self):
        dest = self.root / self.entries[0]['path']
        saved = dest.read_bytes()
        with self.subTest('unregistered'):
            removed = self.rows.pop(0)
            self.write_metadata()
            with self.assertRaisesRegex(ValueError, 'preserving'):
                restore.restore(self.source, verify=True)
        with self.subTest('dirty'):
            self.rows.insert(0, removed)
            self.write_metadata()
            dest.write_bytes(saved + b'user edit')
            with self.assertRaisesRegex(ValueError, 'preserving'):
                restore.restore(self.source, verify=True)
            self.assertEqual(saved + b'user edit', dest.read_bytes())

    def test_staged_content_cannot_replace_head_or_registered_migration(self):
        dest = self.root / self.entries[0]['path']
        original = dest.read_bytes()
        dest.write_bytes(original + b'staged edit')
        self.git('add', self.entries[0]['path'])
        dest.write_bytes(original)
        with self.assertRaisesRegex(ValueError, 'preserving'):
            restore.restore(self.source, verify=True)
        self.assertEqual(original, dest.read_bytes())

    def test_changed_guid_and_unavailable_migration_commit_are_rejected(self):
        self.rows[0]['new']['guid'] = 'f' * 32
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, 'preserving'):
            restore.restore(self.source, verify=True)
        self.rows[0]['new']['guid'] = self.entries[0]['guid']
        self.rows[0]['migrationCommit'] = 'a' * 40
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, 'preserving'):
            restore.restore(self.source, verify=True)

    def test_private_conflict_still_aborts_without_overwrite_or_copy(self):
        self.entries.extend([
            dict(path='Assets/private.txt', disposition='private-restore', sha256=hashlib.sha256(b'original').hexdigest()),
            dict(path='Assets/missing.txt', disposition='private-restore', sha256=hashlib.sha256(b'pending').hexdigest())])
        private = self.root / 'Assets/private.txt'
        private.write_bytes(b'user private edit')
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, 'preserving'):
            restore.restore(self.source)
        self.assertEqual(b'user private edit', private.read_bytes())
        self.assertFalse((self.root / 'Assets/missing.txt').exists())


if __name__ == '__main__':
    unittest.main()
