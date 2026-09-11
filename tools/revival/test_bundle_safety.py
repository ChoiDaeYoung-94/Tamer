import unittest
from verify_bundle import validate_manifest


class BundleSafetyTests(unittest.TestCase):
    manifest = '''<manifest xmlns:android="http://schemas.android.com/apk/res/android"
        package="com.AeDeong.MonsterTamer.revival" android:versionCode="26" android:versionName="1.0.5">
        <uses-sdk android:minSdkVersion="24" android:targetSdkVersion="36"/>
        <application android:debuggable="true"/></manifest>'''

    def test_accepts_isolated_debug_bundle(self):
        validate_manifest(self.manifest)

    def test_rejects_production_and_unexpected_bundle_settings(self):
        for old, new in [('MonsterTamer.revival', 'MonsterTamer'), ('debuggable="true"', 'debuggable="false"'),
                         ('targetSdkVersion="36"', 'targetSdkVersion="35"'), ('versionCode="26"', 'versionCode="27"'),
                         ('minSdkVersion="24"', 'minSdkVersion="23"')]:
            with self.subTest(change=new), self.assertRaises(ValueError):
                validate_manifest(self.manifest.replace(old, new))

    def test_rejects_missing_application(self):
        with self.assertRaises(ValueError):
            validate_manifest(self.manifest.replace('<application android:debuggable="true"/>', ''))


if __name__ == '__main__':
    unittest.main()
