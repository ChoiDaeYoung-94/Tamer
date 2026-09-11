"""Marker rejection must precede any Editor invocation or project mutation."""
import pathlib
import subprocess
import tempfile
import unittest

RUNNER = pathlib.Path(__file__).resolve().with_name('Run-Baseline.ps1')


class PreflightTests(unittest.TestCase):
    def test_markers_reject_before_editor_validation_and_preserve_files(self):
        for marker in ('AOSSettingAPK.txt', 'AOSSettingAAB.txt', 'checkedBuilding.txt'):
            with self.subTest(marker=marker), tempfile.TemporaryDirectory() as folder:
                root = pathlib.Path(folder)
                (root / 'Build').mkdir()
                path = root / 'Build' / marker
                path.write_bytes(b'preserve-marker')
                before = {p.relative_to(root): p.read_bytes() for p in root.rglob('*') if p.is_file()}
                result = subprocess.run(
                    ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(RUNNER),
                     '-ProjectPath', str(root), '-EditorPath', str(root / 'never-launch.exe')],
                    capture_output=True, text=True)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('Legacy build marker exists', result.stderr)
                self.assertNotIn('Editor missing', result.stderr)
                self.assertEqual(before, {p.relative_to(root): p.read_bytes() for p in root.rglob('*') if p.is_file()})


if __name__ == '__main__':
    unittest.main()
