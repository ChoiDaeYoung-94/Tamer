"""One focused Windows temporary-file check for the new writable handle mode."""
import hashlib
import os
from pathlib import Path
import tempfile
import unittest
from windows_owned_files import LockedFile, verified_files

class HandleRestoreTest(unittest.TestCase):
    def test_same_handle_restore_and_replacement_rejection(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); path = root / 'owned'
            path.write_bytes(b'after')
            info = path.stat(); identity = (info.st_dev, info.st_ino)
            digest = hashlib.sha256(b'after').hexdigest()
            with verified_files(root, {'owned': (identity, digest)}, writable=True) as handles:
                with self.assertRaises(OSError):
                    with path.open('wb'): pass
                candidate = root / 'candidate'; candidate.write_bytes(b'replacement')
                with self.assertRaises(OSError): os.replace(candidate, path)
                handles['owned'].restore_bytes(b'before', hashlib.sha256(b'before').hexdigest())
            self.assertEqual(path.read_bytes(), b'before')
            other = root / 'other'; other.write_bytes(b'unchanged')
            other_info = other.stat()
            with self.assertRaises(ValueError):
                with verified_files(root, {
                    'owned': (identity, hashlib.sha256(b'before').hexdigest()),
                    'other': ((other_info.st_dev, other_info.st_ino), '0' * 64)}, writable=True) as handles:
                    handles['owned'].restore_bytes(b'must-not-write', hashlib.sha256(b'must-not-write').hexdigest())
            self.assertEqual(path.read_bytes(), b'before')
            self.assertEqual(other.read_bytes(), b'unchanged')
            replacement = root / 'replacement'; replacement.write_bytes(b'before')
            os.replace(replacement, path)
            with self.assertRaises(ValueError):
                with verified_files(root, {'owned': (identity, hashlib.sha256(b'before').hexdigest())}, writable=True): pass
            self.assertEqual(path.read_bytes(), b'before')
            with LockedFile(path, deletable=False) as held:
                self.assertEqual(path.read_bytes(), held.bytes())
                with self.assertRaises(OSError):
                    with path.open('wb'): pass
                with self.assertRaises(ValueError): held.delete()

if __name__ == '__main__': unittest.main()
