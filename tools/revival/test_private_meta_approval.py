"""Synthetic authorization boundaries; never runs restoration against a real checkout."""
import copy
import hashlib
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import restore_assets as restore


class PrivateMetaApprovalTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        base = Path(self.temp.name)
        self.root, self.source = base / 'checkout', base / 'original'
        self.root.mkdir(); self.source.mkdir()
        subprocess.check_call(['git', '-C', str(self.root), 'init', '-q'])
        (self.root / '.gitignore').write_text('Logs/\n')
        self.guid = 'a' * 32
        self.old = ('guid: ' + self.guid + '\nimport: old\n').encode()
        self.new = ('guid: ' + self.guid + '\nimport: new\n').encode()
        self.path = 'Assets/private.psd.meta'
        self.asset = b'synthetic original artwork'
        for root, data in [(self.root, self.new), (self.source, self.old)]:
            (root / 'Assets').mkdir()
            (root / self.path).write_bytes(data)
            (root / 'Assets/private.psd').write_bytes(self.asset)
        self.private = self.root / 'Logs/revival'
        self.private.mkdir(parents=True)
        (self.private / 'old.meta').write_bytes(self.old)
        (self.private / 'new.meta').write_bytes(self.new)
        (self.root / 'tools/revival').mkdir(parents=True)
        (self.root / 'tools/revival/toolchain.json').write_text('{"unityEditor":"6000.3.25f1"}')
        (self.root / 'ProjectSettings').mkdir()
        (self.root / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.3.25f1\n')
        def info(data, meta=False):
            value = dict(sha256=hashlib.sha256(data).hexdigest(), bytes=len(data))
            if meta: value['guid'] = self.guid
            return value
        self.entries = [dict(path=self.path, disposition='private-restore', **info(self.old, True)),
                        dict(path='Assets/private.psd', disposition='private-restore', **info(self.asset))]
        self.row = dict(path=self.path, old=info(self.old, True), new=info(self.new, True),
                        asset=info(self.asset), preservedOriginal='Logs/revival/old.meta',
                        preservedCurrent='Logs/revival/new.meta')
        self.approval = dict(schema=1, approval='explicit-user-one-private-meta-migration',
                             checkout=str(self.root), source=str(self.source), unity='6000.3.25f1', entries=[self.row])
        self.ledger = self.private / 'approval.json'
        context = patch.object(restore, 'ROOT', self.root)
        context.start(); self.addCleanup(context.stop)

    def load(self, approval=None):
        self.ledger.write_text(json.dumps(approval or self.approval))
        return restore.load_private_meta_approval(self.ledger, self.source, self.entries)

    def test_exact_approval_preserves_all_bytes(self):
        before = {(self.root / self.path): self.new, (self.source / self.path): self.old}
        self.assertEqual(self.row, self.load())
        for file, data in before.items(): self.assertEqual(data, file.read_bytes())

    def test_wrong_scope_version_count_and_record_fail_closed(self):
        for key, value in [('checkout', str(self.source)), ('source', str(self.root)),
                           ('unity', '6000.0.81f1'), ('entries', [self.row, self.row])]:
            with self.subTest(key=key):
                record = copy.deepcopy(self.approval); record[key] = value
                with self.assertRaises(ValueError): self.load(record)
        for key in ['old', 'new', 'asset']:
            with self.subTest(key=key):
                record = copy.deepcopy(self.approval); record['entries'][0][key]['sha256'] = '0' * 64
                with self.assertRaises(ValueError): self.load(record)
        record = copy.deepcopy(self.approval); record['entries'][0]['new']['guid'] = 'b' * 32
        with self.assertRaises(ValueError): self.load(record)

    def test_changed_source_current_asset_and_backup_rejected(self):
        for file in [self.source/self.path, self.root/self.path, self.root/'Assets/private.psd',
                     self.source/'Assets/private.psd', self.private/'old.meta', self.private/'new.meta']:
            with self.subTest(file=file.name):
                before = file.read_bytes(); file.write_bytes(before + b'change')
                with self.assertRaises(ValueError): self.load()
                file.write_bytes(before)

    def test_public_tracked_or_outside_ledger_rejected(self):
        self.load()
        subprocess.check_call(['git', '-C', str(self.root), 'add', '-f', str(self.ledger)])
        with self.assertRaises(ValueError): self.load()
        with self.assertRaises(ValueError): restore.load_private_meta_approval(self.root/'approval.json', self.source, self.entries)

    def test_other_private_conflict_aborts_before_copy(self):
        manifest = self.private/'manifest.json'
        entries = self.entries + [dict(path='Assets/other.meta', disposition='private-restore', sha256='0'*64),
                                  dict(path='Assets/missing', disposition='private-restore', sha256='0'*64)]
        manifest.write_text(json.dumps(dict(entries=entries)))
        (self.root/'Assets/other.meta').write_bytes(b'unapproved')
        self.ledger.write_text(json.dumps(self.approval))
        with patch.object(restore, 'MANIFEST', manifest):
            with self.assertRaisesRegex(ValueError, 'preserving'): restore.restore(self.source, private_meta_approval=self.ledger)
        self.assertFalse((self.root/'Assets/missing').exists())
        self.assertEqual(self.new, (self.root/self.path).read_bytes())


if __name__ == '__main__': unittest.main()
