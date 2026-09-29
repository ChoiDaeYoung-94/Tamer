"""Changed Windows cleanup paths only; all writes/deletes are in temporary fixtures."""
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import private_ads_build as build
from windows_owned_files import LockedFile, locked_directories, verified_files


@unittest.skipUnless(os.name == 'nt', 'Windows handle semantics required')
class WindowsCleanupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()

    def fixture(self):
        for path in build.PROTECTED:
            file = self.root / path
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b'synthetic original')
        (self.root / 'Assets/Scripts/Editor').mkdir(parents=True)
        config = self.root / 'config.json'
        config.write_bytes(b'{}')
        fake = patch.object(build, 'git', side_effect=lambda root, *args:
                            'head' if args[:2] == ('rev-parse', 'HEAD') else '')
        fake.start()
        self.addCleanup(fake.stop)
        return build.stage(self.root, config, {'configSha256': build.digest(b'{}')}, 'head', 'synthetic')

    def test_locked_handle_excludes_writes_replacement_and_parent_rename(self):
        parent = self.root / 'owned'
        parent.mkdir()
        file = parent / 'hook.cs'
        file.write_bytes(b'owned')
        replacement = self.root / 'other.cs'
        replacement.write_bytes(b'other')
        expected = {'owned/hook.cs': (build.identity(file), build.digest(b'owned'))}
        with verified_files(self.root, expected) as handles:
            with self.assertRaises(OSError):
                file.write_bytes(b'changed')
            with self.assertRaises(OSError):
                os.replace(replacement, file)
            with self.assertRaises(OSError):
                parent.rename(self.root / 'moved')
            self.assertEqual(handles['owned/hook.cs'].bytes(), b'owned')
            handles['owned/hook.cs'].delete()
        self.assertFalse(file.exists())
        self.assertEqual(replacement.read_bytes(), b'other')
        self.assertTrue(parent.exists())

    def test_same_bytes_replacement_rejected_before_any_deletion(self):
        file = self.root / 'owned.cs'
        file.write_bytes(b'owned')
        expected = {'owned.cs': (build.identity(file), build.digest(b'owned'))}
        file.rename(self.root / 'original.cs')  # Keep original inode alive.
        file.write_bytes(b'owned')
        with self.assertRaises(ValueError):
            with verified_files(self.root, expected):
                self.fail('Replaced file must not be accepted')
        self.assertEqual(file.read_bytes(), b'owned')
        self.assertTrue((self.root / 'original.cs').exists())

    def test_finalize_keeps_empty_directory_active_journal_and_originals(self):
        state = self.fixture()
        journal = (self.root / build.JOURNAL).read_bytes()
        result = build.finalize(self.root, state)
        self.assertTrue(result['cleanupOwnedFiles'])
        self.assertTrue(result['manualRecoveryRequired'])
        self.assertTrue((self.root / build.HOOK).is_dir())
        self.assertEqual(list((self.root / build.HOOK).iterdir()), [])
        self.assertEqual((self.root / build.JOURNAL).read_bytes(), journal)
        self.assertEqual((self.root / build.PROTECTED[0]).read_bytes(), b'synthetic original')
        self.assertFalse((self.root / (build.HOOK + '.meta')).exists())

    def test_shared_writer_blocks_cleanup_without_fallback(self):
        state = self.fixture()
        first = next(iter(state['created']))
        with (self.root / first).open('r+b'):
            with self.assertRaises(OSError):
                build.finalize(self.root, state)
        self.assertTrue(all((self.root / path).is_file() for path in state['created']))
        self.assertTrue((self.root / build.JOURNAL).is_file())
        self.assertFalse((self.root / state['run'] / 'cleanup.json').exists())

    def test_partial_delete_failure_preserves_journal_and_releases_handles(self):
        state = self.fixture()
        journal = (self.root / build.JOURNAL).read_bytes()
        original = LockedFile.delete
        calls = []

        def fail_second(handle):
            calls.append(handle.identity)
            if len(calls) == 2:
                raise OSError('synthetic disposition failure')
            original(handle)

        with patch.object(LockedFile, 'delete', fail_second), self.assertRaises(OSError):
            build.finalize(self.root, state)
        paths = list(state['created'])
        self.assertFalse((self.root / paths[0]).exists())
        self.assertTrue(all((self.root / path).exists() for path in paths[1:]))
        self.assertEqual((self.root / build.JOURNAL).read_bytes(), journal)
        # Released locks permit later explicit manual review/edit; no retry here.
        with (self.root / paths[1]).open('r+b') as stream:
            self.assertEqual(stream.read(), (self.root / paths[1]).read_bytes())
        self.assertEqual((self.root / state['run'] / 'cleanup.json').read_bytes(), b'')

    def test_existing_receipt_is_never_overwritten(self):
        state = self.fixture()
        receipt = self.root / state['run'] / 'cleanup.json'
        receipt.write_bytes(b'other result')
        with self.assertRaises(FileExistsError):
            build.finalize(self.root, state)
        self.assertEqual(receipt.read_bytes(), b'other result')
        self.assertTrue(all((self.root / path).exists() for path in state['created']))
        self.assertTrue((self.root / build.JOURNAL).exists())


if __name__ == '__main__':
    unittest.main()
