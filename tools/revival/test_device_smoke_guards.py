import subprocess
import unittest
from verify_device_smoke import APP_ID, foreground_is_smoke, require_absent_package, smoke_observation_passed


class DeviceSmokeGuardTests(unittest.TestCase):
    def test_absent_package_can_proceed(self):
        require_absent_package(subprocess.CompletedProcess([], 0, '', ''))

    def test_existing_or_unknown_package_state_is_preserved(self):
        for code, output, error in [(0, 'package:/data/app/test/base.apk', ''),
                                    (1, '', 'device offline'), (2, '', '')]:
            with self.subTest(code=code, output=output), self.assertRaises(ValueError):
                require_absent_package(subprocess.CompletedProcess([], code, output, error))

    def test_only_resumed_isolated_activity_allows_capture(self):
        self.assertTrue(foreground_is_smoke('mResumedActivity: ActivityRecord{abc u0 ' + APP_ID + '/MainActivity t12}'))
        self.assertFalse(foreground_is_smoke('mResumedActivity: ActivityRecord{abc u0 com.android.launcher/Home t12}'))
        self.assertFalse(foreground_is_smoke('mLastPausedActivity: ' + APP_ID + '/MainActivity'))
        self.assertFalse(foreground_is_smoke('mResumedActivity: ' + APP_ID + '.other/MainActivity'))

    def test_complete_observation_and_cleanup_are_required(self):
        valid = dict.fromkeys(['installed', 'launchCommandSucceeded', 'appProcessObserved',
                              'isolatedAppForeground', 'screenshotCaptured', 'unityStartupLogObserved',
                              'ownFreshInstallRemoved'], True)
        valid.update(appLogcatExitCode=0, fatalSignalObserved=False)
        self.assertTrue(smoke_observation_passed(valid))
        for key, value in [('launchCommandSucceeded', False), ('ownFreshInstallRemoved', False),
                           ('appProcessObserved', False), ('isolatedAppForeground', False),
                           ('screenshotCaptured', False), ('appLogcatExitCode', 1),
                           ('fatalSignalObserved', True), ('fatalSignalObserved', None),
                           ('failure', 'inspection failed')]:
            with self.subTest(key=key, value=value):
                self.assertFalse(smoke_observation_passed(dict(valid, **{key: value})))
        self.assertFalse(smoke_observation_passed({'appProcessObserved': True}))


if __name__ == '__main__':
    unittest.main()
