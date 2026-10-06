"""Verify the local, non-debug IAP test AAB; never upload or install it."""
import argparse
import hashlib
import json
import re
import stat
import subprocess
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET
from install_bundletool import DEST, SHA256, VERSION
from verify_native_alignment import inspect_elf

ROOT = Path(__file__).resolve().parents[2]
ANDROID = "{http://schemas.android.com/apk/res/android}"

def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def version_code(value):
    if not re.fullmatch(r"[0-9]+", str(value)) or not 0 < int(value) <= 2100000000:
        raise argparse.ArgumentTypeError("Expected a positive Android version code within limits")
    return int(value)

def signing_certificate(directory):
    if directory is None:
        return ROOT / ".revival-local/iap-signing/test-upload.der"
    folder = Path(directory)
    if not folder.is_absolute():
        raise ValueError("Explicit signing directory must be absolute")
    def regular(path, mode):
        info = path.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT or not mode(info.st_mode):
            raise ValueError("Signing inputs and ancestors must be regular paths without links")
    for ancestor in (folder, *folder.parents):
        regular(ancestor, stat.S_ISDIR)
    inputs = {name: folder / name for name in ("test-upload.jks", "password.dpapi", "test-upload.der", "test-only.json")}
    for path in inputs.values():
        regular(path, stat.S_ISREG)
    metadata = json.loads(inputs["test-only.json"].read_text(encoding="utf-8-sig"))
    if not isinstance(metadata, dict) or metadata.get("applicationId") != "com.AeDeong.MonsterTamer.iaptest" or metadata.get("alias") != "tamer-iap-test-upload":
        raise ValueError("Dedicated IAP test signing marker required")
    for field, name in (("keySha256", "test-upload.jks"), ("certificateSha256", "test-upload.der")):
        expected = metadata.get(field)
        if not isinstance(expected, str) or not re.fullmatch(r"[a-fA-F0-9]{64}", expected) or digest(inputs[name]).lower() != expected.lower():
            raise ValueError("Signing input hash does not match the marker")
    return inputs["test-upload.der"]

def validate_manifest(xml, expected_version_code=26):
    root = ET.fromstring(xml)
    app, sdk = root.find("application"), root.find("uses-sdk")
    if root.get("package") != "com.AeDeong.MonsterTamer.iaptest" or app is None or sdk is None:
        raise ValueError("Dedicated IAP test application required")
    if root.get(ANDROID + "versionCode") != str(version_code(expected_version_code)):
        raise ValueError("Test bundle version code does not match the explicit expectation")
    if app.get(ANDROID + "testOnly", "false") != "false" or app.get(ANDROID + "debuggable", "false") != "false" or sdk.get(ANDROID + "targetSdkVersion") != "36" or sdk.get(ANDROID + "minSdkVersion") != "25":
        raise ValueError("Non-debug target36 test bundle required")
    permissions = {p.get(ANDROID + "name") for p in root.findall("uses-permission")}
    if not {"android.permission.INTERNET", "com.android.vending.BILLING"} <= permissions:
        raise ValueError("Network and billing permissions required")
    if "com.google.android.gms.permission.AD_ID" in permissions:
        raise ValueError("GMS advertising ID permission must be absent")
    if any(p.get(ANDROID + "name") == "com.google.android.gms.ads.MobileAdsInitProvider" for p in app.findall("provider")):
        raise ValueError("Ads initialization provider must be absent")
    return permissions

def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--signing-directory", help="Existing absolute signing folder; never copied or changed")
    parser.add_argument("--expected-version-code", type=version_code, default=26)
    args = parser.parse_args(argv)
    cert = signing_certificate(args.signing_directory)
    if digest(DEST) != SHA256:
        raise ValueError("Pinned bundletool changed")
    aab = ROOT / "Build/revival/Tamer-iap-test.aab"
    java = Path("C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe")
    def run(*args):
        return subprocess.check_output([str(java), *map(str,args)], text=True, encoding="utf-8", stderr=subprocess.PIPE)
    run("-jar", DEST, "validate", "--bundle=" + str(aab))
    xml = run("-jar", DEST, "dump", "manifest", "--bundle=" + str(aab), "--module=base")
    permissions = validate_manifest(xml, args.expected_version_code)
    verified = json.loads(run(ROOT / "tools/revival/VerifyAabSignature.java", aab,
                              "--release-cert-sha256", digest(cert)))
    with zipfile.ZipFile(aab) as archive:
        names = [p for p in archive.namelist() if p.startswith("base/lib/") and p.endswith(".so")]
        abis = sorted({p.split("/")[2] for p in names})
        native = [{"file": p, "elf": inspect_elf(archive.read(p))} for p in names]
    if abis != ["arm64-v8a"]:
        raise ValueError("ARM64 only required")
    result = dict(sha256=digest(aab), bytes=aab.stat().st_size, applicationId="com.AeDeong.MonsterTamer.iaptest",
                  versionCode=args.expected_version_code, debuggable=False, minSdk=25, targetSdk=36, abi=abis, bundletool=VERSION, dedicatedTestSignatureVerified=True,
                  verifiedPayloadEntries=verified["verifiedPayloadEntries"], internetPermission=True, billingPermission=True,
                  gmsAdIdPermission=False, mobileAdsInitProvider=False,
                  adServicesPermissionsRemain=any("ACCESS_ADSERVICES" in p for p in permissions), uploaded=False,
                  nativeElfCount=len(native), elfLoadChecksPassed=all(not p["elf"]["errors"] for p in native),
                  elfRelroChecksPassed=all(p["elf"]["relroChecksPassed"] for p in native),
                  deliveredApkZipAlignmentVerified=False)
    (ROOT / "Logs/revival/iap-bundle-native.json").write_text(json.dumps(native,indent=2)+"\n",encoding="utf-8")
    (ROOT / "Logs/revival/iap-bundle-verification.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(result))
    # Preserve both reports even on LOAD failure; RELRO remains a separate check.
    return 0 if result["elfLoadChecksPassed"] else 1

if __name__ == "__main__":
    raise SystemExit(main())
