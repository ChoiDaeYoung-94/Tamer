"""Synthetic ownership boundaries; no Editor, server, listener or HTTP calls."""
from contextlib import contextmanager, redirect_stdout
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import pipeline_connection_preflight as check


@unittest.skipUnless(sys.platform == 'win32', 'Windows command-line and file-handle semantics')
class PipelineOwnershipTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='Tamer ownership ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        (self.root / 'Assets').mkdir()
        (self.root / 'Packages').mkdir()
        (self.root / 'Packages/manifest.json').write_text('{}', encoding='utf-8')
        (self.root / 'ProjectSettings').mkdir()
        (self.root / 'ProjectSettings/ProjectVersion.txt').write_text(
            'm_EditorVersion: 6000.3.25f1\nm_EditorVersionWithRevision: 6000.3.25f1 (e1dba0a9aba4)\n',
            encoding='utf-8')
        self.now = datetime(2026, 10, 20, 8, 0, tzinfo=timezone.utc)
        self.secret = 'SYNTHETIC-PRIVATE-TOKEN-DO-NOT-PRINT'
        self.descriptor = dict(pid=1234, port=7800, projectPath=str(self.root), mode='editor',
                               unityVersion='6000.3.25f1', startedAt='2026-10-20T07:59:00Z',
                               lastHeartbeat='2026-10-20T07:59:58Z', evalToken=self.secret)
        self.process = dict(pid=1234, name='Unity.exe', executable=r'C:\Editor\Unity.exe',
                            commandLine=subprocess.list2cmdline(
                                [r'C:\Editor\Unity.exe', '-projectPath', str(self.root)]),
                            createdAt='2026-10-20T07:58:00Z', version='6000.3.25f1_e1dba0a9aba4')
        self.snapshot = dict(before=[deepcopy(self.process)], after=[deepcopy(self.process)],
                             listeners=[dict(pid=1234, port=7800, address='0.0.0.0')])

    def inspect(self, descriptor=None, snapshot=None, raw=None):
        payload = raw if raw is not None else json.dumps(
            self.descriptor if descriptor is None else descriptor).encode('utf-8')

        @contextmanager
        def reader(_root):
            yield payload, self.now.timestamp() - 2

        return check.preflight(str(self.root), reader=reader,
                               collector=lambda: self.snapshot if snapshot is None else snapshot,
                               now=self.now)

    def assert_rejected(self, result, reason=None):
        self.assertFalse(result['safeToAttemptConnection'])
        self.assertFalse(result['controlConnectionVerified'])
        self.assertNotIn(self.secret, json.dumps(result))
        self.assertNotIn(str(self.root), json.dumps(result))
        if reason is not None:
            self.assertEqual(result['reason'], reason)

    def test_consistent_metadata_is_only_an_offline_permission_to_consider_connection(self):
        result = self.inspect()
        self.assertTrue(result['safeToAttemptConnection'])
        self.assertFalse(result['controlConnectionVerified'])
        self.assertEqual(result['listenerPids'], [1234])
        self.assertNotIn(self.secret, json.dumps(result))
        self.assertNotIn('evalToken', json.dumps(result))
        self.assertNotIn(str(self.root), json.dumps(result))

    def test_two_different_listener_owners_on_one_port_are_rejected(self):
        self.snapshot['listeners'].append(dict(pid=5678, port=7800, address='127.0.0.1'))
        self.assert_rejected(self.inspect(), 'descriptor_port_owner_conflict')

    def test_missing_wrong_or_invalid_listener_cannot_authorize_connection(self):
        for listeners, reason in [([], 'descriptor_port_not_listening'),
                                  ([dict(pid=1234, port=7801, address='127.0.0.1')], 'descriptor_port_not_listening'),
                                  ([dict(pid=5678, port=7800, address='127.0.0.1')], 'descriptor_port_owner_conflict'),
                                  ([dict(pid=True, port=7800, address='127.0.0.1')], 'listener_metadata_invalid')]:
            with self.subTest(listeners=listeners):
                self.assert_rejected(self.inspect(snapshot=dict(self.snapshot, listeners=listeners)), reason)

    def test_windows_quoting_spaces_and_trailing_separator_preserve_exact_project(self):
        args = [r'C:\Program Files\Unity\Unity.exe', '-projectPath', str(self.root) + '\\', '-logFile', '-']
        command = subprocess.list2cmdline(args)
        self.assertEqual(check.windows_argv(command), args)
        for row in self.snapshot['before'] + self.snapshot['after']:
            row['commandLine'] = command
        self.assertTrue(self.inspect()['safeToAttemptConnection'])

    def test_another_project_or_duplicate_target_process_cannot_be_selected(self):
        other = self.root / 'Other'
        other.mkdir()
        changed = deepcopy(self.snapshot)
        for row in changed['before'] + changed['after']:
            row['commandLine'] = subprocess.list2cmdline(['Unity.exe', '-projectPath', str(other)])
        self.assert_rejected(self.inspect(snapshot=changed), 'descriptor_pid_project_mismatch_or_worker')
        changed = deepcopy(self.snapshot)
        for key in ('before', 'after'):
            changed[key].append(dict(self.process, pid=5678))
        self.assert_rejected(self.inspect(snapshot=changed), 'multiple_editors_for_project')

    def test_import_workers_are_excluded_and_cannot_own_the_descriptor(self):
        for marker in (['-assetImportWorker'], ['-name', 'AssetImportWorker0']):
            with self.subTest(marker=marker):
                changed = deepcopy(self.snapshot)
                worker = dict(self.process, pid=5678, commandLine=subprocess.list2cmdline(['Unity.exe'] + marker))
                for key in ('before', 'after'):
                    changed[key].append(deepcopy(worker))
                self.assertTrue(self.inspect(snapshot=changed)['safeToAttemptConnection'])
                for key in ('before', 'after'):
                    changed[key] = [dict(worker, pid=1234)]
                self.assert_rejected(self.inspect(snapshot=changed), 'descriptor_pid_project_mismatch_or_worker')

    def test_unknown_process_command_lines_and_ambiguous_project_flags_fail_closed(self):
        commands = [None, '', 'Unity.exe', 'Unity.exe -projectPath',
                    subprocess.list2cmdline(['Unity.exe', '-projectPath', str(self.root), '-projectPath', str(self.root)])]
        for command in commands:
            with self.subTest(command=command):
                changed = deepcopy(self.snapshot)
                for row in changed['before'] + changed['after']:
                    row['commandLine'] = command
                self.assert_rejected(self.inspect(snapshot=changed))

    def test_stale_reused_or_changing_pid_is_rejected(self):
        self.assert_rejected(self.inspect(snapshot=dict(self.snapshot, before=[], after=[])),
                             'descriptor_pid_not_running_editor')
        changed = deepcopy(self.snapshot)
        for row in changed['before'] + changed['after']:
            row['createdAt'] = '2026-10-20T07:59:30Z'
        self.assert_rejected(self.inspect(snapshot=changed), 'pid_reused_or_timestamp_inconsistent')
        changed['after'][0]['createdAt'] = '2026-10-20T07:59:31Z'
        self.assert_rejected(self.inspect(snapshot=changed), 'process_snapshot_changed')

    def test_wrong_descriptor_version_or_process_revision_is_rejected(self):
        self.assert_rejected(self.inspect(descriptor=dict(self.descriptor, unityVersion='6000.0.81f1')),
                             'descriptor_editor_version_mismatch')
        changed = deepcopy(self.snapshot)
        for row in changed['before'] + changed['after']:
            row['version'] = '6000.3.25f1_deadbeef0000'
        self.assert_rejected(self.inspect(snapshot=changed), 'process_editor_version_mismatch')

    def test_invalid_descriptor_identity_or_auth_metadata_is_rejected_without_disclosure(self):
        other = self.root / 'Other'
        other.mkdir()
        cases = [('pid', True), ('pid', 0), ('port', 7850), ('port', 7900), ('port', '7800'),
                 ('mode', 'player'), ('evalToken', ''), ('evalToken', None), ('projectPath', str(other)),
                 ('startedAt', '2026-10-20T08:01:00Z'), ('lastHeartbeat', '2026-10-20T08:01:00Z')]
        for key, value in cases:
            with self.subTest(key=key, value=value):
                self.assert_rejected(self.inspect(descriptor=dict(self.descriptor, **{key: value})))
        raw = json.dumps(self.descriptor)[:-1] + ',"pid":5678}'
        self.assert_rejected(self.inspect(raw=raw.encode()), 'descriptor_duplicate_field')
        for raw in (b'[]', b'{not-json', b'null'):
            with self.subTest(raw=raw):
                self.assert_rejected(self.inspect(raw=raw))

    def test_invalid_project_and_version_file_do_not_fall_back_to_other_editors(self):
        for path in ('relative', r'\\server\share\Tamer', r'C:relative'):
            self.assert_rejected(check.preflight(path, collector=lambda: self.fail('collector must not run')))
        (self.root / 'ProjectSettings/ProjectVersion.txt').write_text('invalid', encoding='utf-8')
        self.assert_rejected(self.inspect(), 'project_version_invalid')

    def test_metadata_collection_errors_never_leak_command_lines_or_credentials(self):
        @contextmanager
        def reader(_root):
            yield json.dumps(self.descriptor).encode(), self.now.timestamp() - 2
        with patch.object(check.subprocess, 'run', side_effect=OSError(self.secret)):
            self.assert_rejected(check.preflight(str(self.root), reader=reader, now=self.now))
        capture = io.StringIO()
        with redirect_stdout(capture):
            exit_code = check.main(['--unknown', self.secret])
        self.assertEqual(exit_code, 2)
        self.assertNotIn(self.secret, capture.getvalue())
        self.assertEqual(json.loads(capture.getvalue())['reason'], 'arguments_invalid')

    def test_bounded_real_descriptor_read_has_no_write_or_delete_and_preserves_bytes(self):
        target = self.root / check.DESCRIPTOR
        target.parent.mkdir(parents=True)
        raw = json.dumps(self.descriptor).encode()
        target.write_bytes(raw)
        before = hashlib.sha256(target.read_bytes()).hexdigest()
        with check.read_descriptor(self.root) as (actual, _mtime):
            self.assertEqual(actual, raw)
        self.assertEqual(hashlib.sha256(target.read_bytes()).hexdigest(), before)
        self.assertTrue(target.exists())
        target.write_bytes(b'x' * (check.MAX_DESCRIPTOR_BYTES + 1))
        with self.assertRaises(check.Rejected):
            with check.read_descriptor(self.root):
                self.fail('oversized descriptor must not yield')

    def test_snapshot_collector_uses_only_fixed_read_only_os_query_and_hides_stderr(self):
        with patch.object(check.subprocess, 'run', return_value=subprocess.CompletedProcess(
                [], 0, json.dumps(self.snapshot), self.secret)) as run:
            self.assertEqual(check.collect_windows_snapshot(), self.snapshot)
        args, kwargs = run.call_args
        self.assertEqual(Path(args[0][0]).name.casefold(), 'powershell.exe')
        self.assertNotIn('unity.exe', [str(arg).casefold() for arg in args[0]])
        self.assertTrue(kwargs['capture_output'])
        self.assertEqual(kwargs['timeout'], 20)
        with patch.object(check.subprocess, 'run', return_value=subprocess.CompletedProcess([], 1, '', self.secret)):
            with self.assertRaisesRegex(check.Rejected, '^os_snapshot_unavailable$'):
                check.collect_windows_snapshot()


if __name__ == '__main__':
    unittest.main()
