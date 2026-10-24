"""Guard against selecting a different game or treating silent commands as success."""
import unittest
from pathlib import Path
from ldplayer_preflight import inspect, parse_instances, select_instance


LIST = '0,Other,1,2,1,100,101,1600,900,240\n1,Tamer,3,4,1,102,103,1280,720,240'


class LDPlayerPreflightTests(unittest.TestCase):
    def run_inspect(self, devices, serial='emulator-5556', instance_list=LIST, guest=None):
        calls = []
        def runner(argv):
            calls.append(argv)
            if argv[1:] == ['list2']:
                return instance_list
            if argv[1:] == ['devices']:
                return 'List of devices attached\n' + devices
            self.assertEqual(argv[1:5], ['adb', '--index', '1', '--command'])
            command = argv[-1]
            if command == 'get-serialno':
                return serial
            return (guest or {'shell getprop ro.build.version.release': '14',
                             'shell getprop ro.build.version.sdk': '34',
                             'shell getprop ro.product.cpu.abilist': 'x86_64,arm64-v8a',
                             'shell getconf PAGESIZE': '4096'})[command]
        result = inspect(Path('test-tools'), runner=runner)
        return result, calls

    def test_other_instance_transport_never_substitutes_for_tamer(self):
        result, calls = self.run_inspect('emulator-5554\tdevice')
        self.assertFalse(result['transportReady'])
        self.assertEqual(result['targetAdbState'], 'absent')
        self.assertEqual(len(calls), 2)  # No guest query against another game.
        self.assertNotIn('emulator-5554', str(result))

    def test_offline_and_unauthorized_target_never_reaches_guest(self):
        for state in ('offline', 'unauthorized'):
            with self.subTest(state=state):
                result, calls = self.run_inspect(f'emulator-5556\t{state}')
                self.assertFalse(result['transportReady'])
                self.assertEqual(len(calls), 2)

    def test_console_mismatch_or_silent_success_stops_before_guest(self):
        for serial in ('emulator-5554', '', 'adb.exe: device not found'):
            with self.subTest(serial=serial):
                result, calls = self.run_inspect('emulator-5556\tdevice', serial=serial)
                self.assertFalse(result['transportReady'])
                self.assertEqual(len(calls), 3)

    def test_duplicate_exact_name_or_index_is_rejected(self):
        with self.assertRaises(ValueError):
            select_instance(parse_instances(LIST.replace('Other', 'Tamer')), 'Tamer')
        with self.assertRaises(ValueError):
            parse_instances(LIST.replace('1,Tamer', '0,Tamer'))
        with self.assertRaises(ValueError):
            select_instance(parse_instances(LIST), 'tamer')

    def test_stopped_target_is_not_launched(self):
        result, calls = self.run_inspect('emulator-5556\tdevice',
            instance_list=LIST.replace('1,Tamer,3,4,1,', '1,Tamer,3,4,0,'))
        self.assertFalse(result['transportReady'])
        self.assertEqual(len(calls), 2)

    def test_stopped_other_instance_with_absent_pids_does_not_block_ready_tamer(self):
        instances = LIST + '\n2,Stopped,0,0,0,-1,-1,1280,720,240'
        result, calls = self.run_inspect('emulator-5556\tdevice', instance_list=instances)
        self.assertTrue(result['transportReady'])
        self.assertEqual(result['instance']['index'], 1)
        self.assertTrue(all(call[3] == '1' for call in calls[2:]))

    def test_absent_pids_on_stopped_target_never_allow_guest_queries(self):
        for row in ('1,Tamer,0,0,0,-1,-1', '1,Tamer,0,0,0,-1,-1,1280,720,240'):
            with self.subTest(row=row):
                result, calls = self.run_inspect('emulator-5556\tdevice', instance_list=row)
                self.assertFalse(result['transportReady'])
                self.assertFalse(result['instance']['androidStarted'])
                self.assertEqual(len(calls), 2)

    def test_negative_metadata_remains_rejected_outside_stopped_pid_sentinels(self):
        for row in ('1,Tamer,0,0,1,-1,-1', '1,Tamer,0,0,0,-2,-1',
                    '1,Tamer,-1,0,0,-1,-1', '1,Tamer,0,0,0,-1,-1,-1,720,240',
                    '-1,Tamer,0,0,0,-1,-1', '1,Tamer,0,0,2,100,101'):
            with self.subTest(row=row), self.assertRaises(ValueError):
                self.run_inspect('emulator-5556\tdevice', instance_list=row)

    def test_ready_transport_does_not_claim_runtime_or_installed_apk_pass(self):
        result, calls = self.run_inspect('emulator-5554\tdevice\nemulator-5556\tdevice')
        self.assertTrue(result['transportReady'])
        self.assertFalse(result['gameRuntimeVerified'])
        self.assertFalse(result['installedApkVerified'])
        self.assertFalse(result['settingsChanged'])
        self.assertEqual(result['guest']['pageSizeBytes'], '4096')
        self.assertTrue(all('install' not in call and 'launch' not in call for call in calls))

    def test_incomplete_guest_response_never_claims_ready(self):
        with self.assertRaises(ValueError):
            self.run_inspect('emulator-5556\tdevice', guest={
                'shell getprop ro.build.version.release': '',
                'shell getprop ro.build.version.sdk': '',
                'shell getprop ro.product.cpu.abilist': '',
                'shell getconf PAGESIZE': ''})


if __name__ == '__main__':
    unittest.main()
