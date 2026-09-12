import unittest
from verify_iap_test_bundle import validate_manifest

class IapBundleManifestTests(unittest.TestCase):
    xml = """<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.AeDeong.MonsterTamer.iaptest"><uses-sdk android:targetSdkVersion="36"/><uses-permission android:name="android.permission.INTERNET"/><uses-permission android:name="com.android.vending.BILLING"/><application android:debuggable="false"/></manifest>"""
    def test_isolated_release_manifest_is_accepted(self):
        self.assertIn("com.android.vending.BILLING", validate_manifest(self.xml))
    def test_operational_package_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace(".iaptest", ""))
    def test_debuggable_bundle_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace('debuggable="false"', 'debuggable="true"'))
    def test_testonly_bundle_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace('debuggable="false"', 'testOnly="true"'))
    def test_gms_ad_id_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace('</manifest>', '<uses-permission xmlns:android="http://schemas.android.com/apk/res/android" android:name="com.google.android.gms.permission.AD_ID"/></manifest>'))
    def test_missing_billing_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace("com.android.vending.BILLING", "unrelated"))
    def test_ads_provider_is_rejected(self):
        xml = self.xml.replace('<application android:debuggable="false"/>', '<application><provider android:name="com.google.android.gms.ads.MobileAdsInitProvider"/></application>')
        with self.assertRaises(ValueError): validate_manifest(xml)

if __name__ == "__main__": unittest.main()
