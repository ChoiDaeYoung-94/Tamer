import json
import os
import subprocess
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import iap_owned_recovery as recovery
from private_ads_evidence import create_private_directory


@unittest.skipUnless(os.name == 'nt', 'Requires actual Windows handle guards')
class IapOwnedRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.private = self.root / 'private'
        create_private_directory(self.private)
        records = {}
        for i, relative in enumerate(recovery.ORIGINALS):
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            raw = ('original-' + str(i)).encode()
            path.write_bytes(raw)
            snapshot = str(i) + '.snapshot'
            (self.private / snapshot).write_bytes(raw)
            info = path.stat()
            records[relative] = {'snapshot': snapshot, 'sha256': recovery.digest(raw),
                                 'length': len(raw), 'identity': [info.st_dev, info.st_ino]}
        for relative in (*recovery.TEMPORARIES, *recovery.COLLISIONS):
            (self.root / relative).parent.mkdir(parents=True, exist_ok=True)
        self.manifest_path = self.private / 'manifest.json'
        pins = {}
        for relative in recovery.SOURCE_FILES:
            source = self.root / relative
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes(b'synthetic-source')
            pins[relative] = recovery.digest(source.read_bytes())
        info = self.root.stat()
        self.manifest_path.write_text(json.dumps({'schemaVersion': 2,
            'mode': 'reviewed-iap-source-recovery', 'head': 'synthetic-head', 'helperPins': pins,
            'projectRoot': str(self.root), 'projectRootIdentity': [info.st_dev, info.st_ino],
            'protection': records,
            'expectedAbsent': list((*recovery.TEMPORARIES, *recovery.COLLISIONS))}))
        self.manifest_hash = recovery.digest(self.manifest_path.read_bytes())
        self.manifest, self.originals = recovery.load_manifest(
            self.manifest_path, recovery.digest(self.manifest_path.read_bytes()))
        self.closed = patch.object(recovery, 'require_editor_closed')
        self.closed.start()
        self.addCleanup(self.closed.stop)
        self.git = patch.object(recovery, 'git', return_value='synthetic-head')
        self.git.start()
        self.addCleanup(self.git.stop)

    def test_wrong_checkout_stops_before_capture(self):
        other = self.root / 'other-checkout'
        other.mkdir()
        with self.assertRaises(ValueError):
            recovery.capture(other, self.manifest_path, self.manifest, self.manifest_hash)
        self.assertFalse((self.private / 'after-build.private.json').exists())

    def test_manifest_change_after_load_stops_before_capture(self):
        self.manifest_path.write_bytes(self.manifest_path.read_bytes() + b' ')
        with self.assertRaises(ValueError):
            recovery.capture(self.root, self.manifest_path, self.manifest, self.manifest_hash)
        self.assertFalse((self.private / 'after-0.snapshot').exists())

    def test_source_change_stops_before_start_marker(self):
        (self.root / recovery.SOURCE_FILES[0]).write_bytes(b'foreign-source')
        with self.assertRaises(ValueError):
            recovery.preflight(self.root, self.manifest_path, self.manifest, self.manifest_hash, start=True)
        self.assertFalse((self.private / 'build-run-once.marker').exists())

    def test_start_marker_is_single_use(self):
        recovery.preflight(self.root, self.manifest_path, self.manifest, self.manifest_hash, start=True)
        self.assertTrue((self.private / 'build-run-once.marker').exists())
        with self.assertRaises(ValueError):
            recovery.preflight(self.root, self.manifest_path, self.manifest, self.manifest_hash, start=True)

    def captured_change(self):
        target = self.root / recovery.ORIGINALS[0]
        replacement = target.with_suffix('.replacement')
        replacement.write_bytes(b'build-output')
        os.replace(replacement, target)
        (self.root / recovery.TEMPORARIES[0]).write_bytes(b'isolated-config')
        recovery.capture(self.root, self.manifest_path, self.manifest, self.manifest_hash)
        delta = self.private / 'after-build.private.json'
        return target, recovery.digest(delta.read_bytes())

    def test_reviewed_normal_replacement_restores_bytes_and_reports_identity(self):
        target, reviewed_hash = self.captured_change()
        writes = []
        original_restore = recovery.LockedFile.restore_bytes
        def record_restore(handle, raw, expected_hash):
            writes.append(handle.identity)
            return original_restore(handle, raw, expected_hash)
        with patch.object(recovery.LockedFile, 'restore_bytes', record_restore):
            recovery.restore(self.root, self.manifest_path, self.manifest, self.originals, reviewed_hash, self.manifest_hash)
        self.assertEqual(writes, [(target.stat().st_dev, target.stat().st_ino)])
        self.assertEqual(target.read_bytes(), self.originals[recovery.ORIGINALS[0]])
        self.assertFalse((self.root / recovery.TEMPORARIES[0]).exists())
        report = json.loads((self.private / 'restoration.private.json').read_bytes())
        self.assertIn(recovery.ORIGINALS[0], report['identityDifferences'])
        self.assertFalse(report['originalIdentitiesFullyRestored'])

    def test_identity_only_replacement_is_verified_without_rewrite(self):
        target = self.root / recovery.ORIGINALS[0]
        replacement = target.with_suffix('.replacement')
        replacement.write_bytes(target.read_bytes())
        os.replace(replacement, target)
        recovery.capture(self.root, self.manifest_path, self.manifest, self.manifest_hash)
        reviewed_hash = recovery.digest((self.private / 'after-build.private.json').read_bytes())
        with patch.object(recovery.LockedFile, 'restore_bytes', side_effect=AssertionError('Unchanged bytes must not be rewritten')):
            recovery.restore(self.root, self.manifest_path, self.manifest, self.originals, reviewed_hash, self.manifest_hash)
        report = json.loads((self.private / 'restoration.private.json').read_bytes())
        self.assertIn(recovery.ORIGINALS[0], report['identityDifferences'])

    def test_junction_ancestor_stops_before_writes(self):
        target, reviewed_hash = self.captured_change()
        parent = (self.root / recovery.ORIGINALS[-1]).parent
        moved = parent.with_name('original-resources')
        parent.rename(moved)
        result = subprocess.run(['cmd', '/c', 'mklink', '/J', str(parent), str(moved)],
                                capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, 'Synthetic directory junction creation failed')
        # Remove only the known fixture junction, before TemporaryDirectory cleanup.
        self.addCleanup(parent.rmdir)
        with self.assertRaises(ValueError):
            recovery.restore(self.root, self.manifest_path, self.manifest, self.originals, reviewed_hash, self.manifest_hash)
        self.assertEqual(target.read_bytes(), b'build-output')
        self.assertEqual((moved / Path(recovery.ORIGINALS[-1]).name).read_bytes(),
                         self.originals[recovery.ORIGINALS[-1]])

    def test_same_bytes_external_replacement_after_review_stops_before_writes(self):
        target, reviewed_hash = self.captured_change()
        other = self.root / recovery.ORIGINALS[1]
        replacement = other.with_suffix('.replacement')
        replacement.write_bytes(other.read_bytes())
        os.replace(replacement, other)
        with self.assertRaises(ValueError):
            recovery.restore(self.root, self.manifest_path, self.manifest, self.originals, reviewed_hash, self.manifest_hash)
        self.assertEqual(target.read_bytes(), b'build-output')
        self.assertTrue((self.root / recovery.TEMPORARIES[0]).exists())

    def test_content_change_after_review_stops_before_writes(self):
        target, reviewed_hash = self.captured_change()
        (self.root / recovery.ORIGINALS[-1]).write_bytes(b'foreign-edit')
        with self.assertRaises(ValueError):
            recovery.restore(self.root, self.manifest_path, self.manifest, self.originals, reviewed_hash, self.manifest_hash)
        self.assertEqual(target.read_bytes(), b'build-output')

    def test_missing_original_stops_before_writes(self):
        target, reviewed_hash = self.captured_change()
        (self.root / recovery.ORIGINALS[-1]).unlink()
        with self.assertRaises(OSError):
            recovery.restore(self.root, self.manifest_path, self.manifest, self.originals, reviewed_hash, self.manifest_hash)
        self.assertEqual(target.read_bytes(), b'build-output')

    def test_reparse_target_stops_before_writes(self):
        target, reviewed_hash = self.captured_change()
        other = self.root / recovery.ORIGINALS[-1]
        external = self.root / 'unrelated'
        external.write_bytes(other.read_bytes())
        other.unlink()
        try:
            other.symlink_to(external)
        except OSError:
            self.skipTest('Creating symlinks is not permitted on this Windows host')
        with self.assertRaises(ValueError):
            recovery.restore(self.root, self.manifest_path, self.manifest, self.originals, reviewed_hash, self.manifest_hash)
        self.assertEqual(target.read_bytes(), b'build-output')
        self.assertEqual(external.read_bytes(), self.originals[recovery.ORIGINALS[-1]])


if __name__ == '__main__':
    unittest.main()
