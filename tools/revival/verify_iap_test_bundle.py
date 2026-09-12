"""Verify the local, non-debug IAP test AAB; never upload or install it."""
import hashlib
import json
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

def validate_manifest(xml):
    root = ET.fromstring(xml)
    app, sdk = root.find("application"), root.find("uses-sdk")
    if root.get("package") != "com.AeDeong.MonsterTamer.iaptest" or app is None or sdk is None:
        raise ValueError("Dedicated IAP test application required")
    if app.get(ANDROID + "testOnly", "false") != "false" or app.get(ANDROID + "debuggable", "false") != "false" or sdk.get(ANDROID + "targetSdkVersion") != "36" or sdk.get(ANDROID + "minSdkVersion") != "24":
        raise ValueError("Non-debug target36 test bundle required")
    permissions = {p.get(ANDROID + "name") for p in root.findall("uses-permission")}
    if not {"android.permission.INTERNET", "com.android.vending.BILLING"} <= permissions:
        raise ValueError("Network and billing permissions required")
    if "com.google.android.gms.permission.AD_ID" in permissions:
        raise ValueError("GMS advertising ID permission must be absent")
    if any(p.get(ANDROID + "name") == "com.google.android.gms.ads.MobileAdsInitProvider" for p in app.findall("provider")):
        raise ValueError("Ads initialization provider must be absent")
    return permissions

def main():
    if digest(DEST) != SHA256:
        raise ValueError("Pinned bundletool changed")
    aab = ROOT / "Build/revival/Tamer-iap-test.aab"
    cert = ROOT / ".revival-local/iap-signing/test-upload.der"
    java = Path("C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe")
    def run(*args):
        return subprocess.check_output([str(java), *map(str,args)], text=True, encoding="utf-8", stderr=subprocess.PIPE)
    run("-jar", DEST, "validate", "--bundle=" + str(aab))
    xml = run("-jar", DEST, "dump", "manifest", "--bundle=" + str(aab), "--module=base")
    permissions = validate_manifest(xml)
    verified = json.loads(run(ROOT / "tools/revival/VerifyAabSignature.java", aab,
                              "--release-cert-sha256", digest(cert)))
    with zipfile.ZipFile(aab) as archive:
        names = [p for p in archive.namelist() if p.startswith("base/lib/") and p.endswith(".so")]
        abis = sorted({p.split("/")[2] for p in names})
        native = [{"file": p, "elf": inspect_elf(archive.read(p))} for p in names]
    if abis != ["arm64-v8a"]:
        raise ValueError("ARM64 only required")
    result = dict(sha256=digest(aab), bytes=aab.stat().st_size, applicationId="com.AeDeong.MonsterTamer.iaptest",
                  debuggable=False, minSdk=24, targetSdk=36, abi=abis, bundletool=VERSION, dedicatedTestSignatureVerified=True,
                  verifiedPayloadEntries=verified["verifiedPayloadEntries"], internetPermission=True, billingPermission=True,
                  gmsAdIdPermission=False, mobileAdsInitProvider=False,
                  adServicesPermissionsRemain=any("ACCESS_ADSERVICES" in p for p in permissions), uploaded=False,
                  nativeElfCount=len(native), elfLoadChecksPassed=all(not p["elf"]["errors"] for p in native),
                  elfRelroChecksPassed=all(p["elf"]["relroChecksPassed"] for p in native),
                  deliveredApkZipAlignmentVerified=False)
    (ROOT / "Logs/revival/iap-bundle-native.json").write_text(json.dumps(native,indent=2)+"\n",encoding="utf-8")
    (ROOT / "Logs/revival/iap-bundle-verification.json").write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(result))

if __name__ == "__main__":
    main()
