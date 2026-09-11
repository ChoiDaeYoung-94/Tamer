"""Exercise the Java verifier with generated keys and tiny synthetic payloads only."""
import json
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path

JDK = Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin')
VERIFIER = Path(__file__).with_name('VerifyAabSignature.java')


@unittest.skipUnless((JDK / 'java.exe').exists(), 'Pinned Unity JDK is required for signature integration tests')
class BundleSignatureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory()
        cls.folder = Path(cls.temp.name)
        cls.signed = cls.folder / 'signed.jar'
        key = cls.folder / 'test.keystore'
        with zipfile.ZipFile(cls.signed, 'w') as archive:
            archive.writestr('payload.txt', b'synthetic signed payload')
        subprocess.run([str(JDK / 'keytool.exe'), '-genkeypair', '-keystore', str(key), '-storepass', 'android',
                        '-keypass', 'android', '-alias', 'test', '-dname', 'CN=Android Debug,O=Android,C=US',
                        '-keyalg', 'RSA', '-validity', '2'], check=True, capture_output=True)
        subprocess.run([str(JDK / 'jarsigner.exe'), '-keystore', str(key), '-storepass', 'android',
                        str(cls.signed), 'test'], check=True, capture_output=True)

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def verify(self, path):
        return subprocess.run([str(JDK / 'java.exe'), str(VERIFIER), str(path)], capture_output=True, text=True)

    def test_signed_payload_is_verified(self):
        result = self.verify(self.signed)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)['verifiedPayloadEntries'], 1)

    def test_modified_signed_payload_is_rejected(self):
        path = self.folder / 'tampered.jar'
        with zipfile.ZipFile(self.signed) as source, zipfile.ZipFile(path, 'w') as target:
            for entry in source.infolist():
                target.writestr(entry, b'changed payload' if entry.filename == 'payload.txt' else source.read(entry))
        self.assertNotEqual(self.verify(path).returncode, 0)

    def test_added_unsigned_payload_is_rejected(self):
        path = self.folder / 'unsigned-extra.jar'
        shutil.copyfile(self.signed, path)
        with zipfile.ZipFile(path, 'a') as archive:
            archive.writestr('extra.txt', b'unsigned synthetic payload')
        self.assertNotEqual(self.verify(path).returncode, 0)


if __name__ == '__main__':
    unittest.main()
