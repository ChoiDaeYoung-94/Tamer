"""Restore an existing five-role test-signing backup; never generate a key or build.

Default invocation performs no input reads. Actual restoration requires both
approval switches and a separately reviewed plan SHA. GPG plaintext is carried
only in private subprocess pipes and memory, never printed or saved as a ZIP.

Reviewed plan contract (JSON, duplicate properties rejected):
 schema = tamer-iap-v2-restore-plan-1; purpose = existing-test-app-upload-key-recovery;
 applicationId/alias = the dedicated test constants below;
 ciphertext, receipt, certificateEvidence = {path: absolute local path, sha256};
 binding = {authority: reviewed-google-play-test-upload-certificate,
            certificateSha256: DER digest, generationMarkerSha256: original metadata digest};
 destination = {parent: current user's existing owner-only TamerPrivateBackups,
                name: iap-v2-restore- followed by 32 lowercase hex digits};
 sources = exact SOURCE_NAMES -> reviewed hashes;
 tools = python/powershell/gpg/gpgconf/pinentry -> {path, sha256}.
 Certificate evidence is a separately reviewed normalized JSON observation with
 schema tamer-iap-upload-certificate-observation-1, applicationId, certificateType
 upload, certificateSha256 and observedAtUtc (ISO UTC ending in Z). The reviewer
 confirms its provenance/freshness; this program never queries Google.

Execution: --execute-once --approve-secret-restoration --plan ABSOLUTE_PLAN
 --plan-sha256 REVIEWED_PLAN_HASH. No passwords are command-line parameters.
Failure preserves the restricted destination, agent home and a per-ciphertext
attempt marker. Never delete/rename those markers to bypass a stopped attempt.
NewKey, old-Windows DPAPI, original marker/receipt mutation, SDK/build/upload and
private-key use verification are outside this entry point's scope.
"""
import argparse
import base64
from contextlib import ExitStack
import ctypes
from ctypes import wintypes
import hashlib
from datetime import datetime
import io
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import time
import zipfile

import windows_owned_files as owned

APP = "com.AeDeong.MonsterTamer.iaptest"
ALIAS = "tamer-iap-test-upload"
PURPOSE = "existing-test-app-upload-key-recovery"
ROLES = frozenset(("keystore", "certificate-der", "certificate-pem",
                   "recovery-metadata", "signing-secret"))
ROLE_LIMIT = 16 * 1024 * 1024
PLAIN_LIMIT = 64 * 1024 * 1024
SOURCE_NAMES = frozenset(("restore_iap_signing_v2.py", "Restore-IapSigningV2.ps1",
                          "windows_owned_files.py"))
OUTPUT_NAMES = {"keystore": "test-upload.jks", "certificate-der": "test-upload.der",
                "certificate-pem": "test-upload.pem", "recovery-metadata": "test-only.json"}


def fail():
    # Fixed message only: never include native output, paths, JSON or secret values.
    raise ValueError("V2 signing recovery input or ownership check failed")


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def hash_value(value):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{64}", value):
        fail()
    return value


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            fail()
        result[key] = value
    return result


def json_object(raw, limit):
    if not 0 < len(raw) <= limit:
        fail()
    result = json.loads(bytes(raw).decode("utf-8-sig"), object_pairs_hook=unique_object)
    if not isinstance(result, dict):
        fail()
    return result


def local_path(value):
    if not isinstance(value, str):
        fail()
    path = Path(value)
    if not path.is_absolute() or ".." in path.parts or str(path).startswith("\\\\"):
        fail()
    # Reject alternate data streams on Windows, including non-file drive syntax.
    if os.name == "nt" and ":" in str(path)[len(path.drive):]:
        fail()
    return path


def regular(path, directory=False):
    for parent in (*reversed(path.parents), path):
        info = parent.lstat()
        is_directory = parent != path or directory
        if (stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400
                or not (stat.S_ISDIR(info.st_mode) if is_directory else stat.S_ISREG(info.st_mode))
                or (not is_directory and info.st_nlink != 1)):
            fail()


def validate_plan(plan):
    if (plan.get("schema") != "tamer-iap-v2-restore-plan-1"
            or plan.get("applicationId") != APP or plan.get("alias") != ALIAS
            or plan.get("purpose") != PURPOSE):
        fail()
    for name in ("ciphertext", "receipt", "certificateEvidence"):
        item = plan.get(name)
        if not isinstance(item, dict) or set(item) != {"path", "sha256"}:
            fail()
        local_path(item["path"])
        hash_value(item["sha256"])
    binding = plan.get("binding")
    if (not isinstance(binding, dict) or set(binding) != {"authority", "certificateSha256", "generationMarkerSha256"}
            or binding["authority"] != "reviewed-google-play-test-upload-certificate"):
        fail()
    hash_value(binding["certificateSha256"])
    hash_value(binding["generationMarkerSha256"])
    destination = plan.get("destination")
    if (not isinstance(destination, dict) or set(destination) != {"parent", "name"}
            or not re.fullmatch(r"iap-v2-restore-[0-9a-f]{32}", destination["name"])):
        fail()
    local_path(destination["parent"])
    if local_path(destination["parent"]) != Path.home() / "TamerPrivateBackups":
        fail()
    if set(plan.get("sources", {})) != SOURCE_NAMES or set(plan.get("tools", {})) != {"python", "powershell", "gpg", "gpgconf", "pinentry"}:
        fail()
    for value in plan["sources"].values():
        hash_value(value)
    for tool in plan["tools"].values():
        if not isinstance(tool, dict) or set(tool) != {"path", "sha256"}:
            fail()
        local_path(tool["path"])
        hash_value(tool["sha256"])
    return plan


def validate_receipt(raw, plan):
    receipt = json_object(raw, 16384)
    if (receipt.get("verified") is not True or receipt.get("backupContentVerified") is not True
            or receipt.get("signingSecretIncluded") is not True
            or not isinstance(receipt.get("roles"), list)
            or len(receipt["roles"]) != 5 or set(receipt["roles"]) != ROLES
            or receipt.get("encryptedSha256") != plan["ciphertext"]["sha256"]
            or receipt.get("generatedInputMarkerSha256") != plan["binding"]["generationMarkerSha256"]):
        fail()


def validate_certificate_evidence(raw, binding):
    """A pinned, normalized observation; never queries Google or infers readiness."""
    evidence = json_object(raw, 16384)
    if (evidence.get("schema") != "tamer-iap-upload-certificate-observation-1"
            or evidence.get("applicationId") != APP or evidence.get("certificateType") != "upload"
            or evidence.get("certificateSha256") != binding["certificateSha256"]
            or not isinstance(evidence.get("observedAtUtc"), str)
            or not evidence["observedAtUtc"].endswith("Z")):
        fail()
    datetime.fromisoformat(evidence["observedAtUtc"].replace("Z", "+00:00"))
    # The reviewer decides freshness. A timestamp/certificate display is not an
    # assertion that Google activated a key or will accept a signed bundle.


def clear(data):
    if isinstance(data, bytearray):
        data[:] = b"\0" * len(data)


def bounded_bytes(handle, limit):
    info = os.fstat(handle.stream.fileno())
    if not 0 < info.st_size <= limit:
        fail()
    return handle.bytes()


def bounded_zip_entry(archive, name, limit):
    data = bytearray()
    try:
        with archive.open(name) as stream:
            while True:
                part = stream.read(min(65536, limit + 1 - len(data)))
                if not part:
                    return data
                data.extend(part)
                if len(data) > limit:
                    fail()
    except Exception:
        clear(data)
        raise


def validate_archive(plaintext, binding):
    """Pure validation, suitable for synthetic fixtures; owns returned bytearrays."""
    if not 0 < len(plaintext) <= PLAIN_LIMIT:
        fail()
    contents = {}
    try:
        with zipfile.ZipFile(io.BytesIO(plaintext)) as archive:
            entries = archive.infolist()
            if len(entries) != 6 or {e.filename for e in entries} != ROLES | {"manifest.json"}:
                fail()
            if sum(e.file_size for e in entries) > PLAIN_LIMIT:
                fail()
            for entry in entries:
                mode = entry.external_attr >> 16
                if (entry.is_dir() or entry.flag_bits & 1 or entry.external_attr & (0x10 | 0x400)
                        or stat.S_IFMT(mode) not in (0, stat.S_IFREG)
                        or not 0 < entry.file_size <= (4096 if entry.filename == "manifest.json" else ROLE_LIMIT)):
                    fail()
            manifest_raw = bounded_zip_entry(archive, "manifest.json", 4096)
            try:
                manifest = json_object(manifest_raw, 4096)
            finally:
                clear(manifest_raw)
            if set(manifest) != ROLES:
                fail()
            for role in ROLES:
                record = manifest[role]
                if (not isinstance(record, dict) or set(record) != {"sha256", "bytes"}
                        or type(record["bytes"]) is not int or not 0 < record["bytes"] <= ROLE_LIMIT
                        or record["bytes"] != archive.getinfo(role).file_size):
                    fail()
                hash_value(record["sha256"])
                contents[role] = bounded_zip_entry(archive, role, ROLE_LIMIT)
                if len(contents[role]) != record["bytes"] or sha(contents[role]) != record["sha256"]:
                    fail()
        metadata = json_object(contents["recovery-metadata"], ROLE_LIMIT)
        if (metadata.get("applicationId") != APP or metadata.get("alias") != ALIAS
                or metadata.get("purpose") != PURPOSE
                or sha(contents["recovery-metadata"]) != binding["generationMarkerSha256"]):
            fail()
        for field, role in (("keySha256", "keystore"), ("certificateSha256", "certificate-der"),
                            ("pemSha256", "certificate-pem")):
            if metadata.get(field) != sha(contents[role]):
                fail()
        if sha(contents["certificate-der"]) != binding["certificateSha256"]:
            fail()
        secret = json_object(contents["signing-secret"], 8192)
        if (set(secret) != {"storePassword", "aliasPassword"}
                or not isinstance(secret["storePassword"], str)
                or not 32 <= len(secret["storePassword"]) <= 1024
                or secret["storePassword"] != secret["aliasPassword"]):
            fail()
        return contents
    except Exception:
        for value in contents.values():
            clear(value)
        raise


def safe_child(executable, arguments, payload, limit, timeout=940):
    """Bound pipe memory; discard stderr and never embed it in an exception."""
    # Reuse bounded concurrent drainage; import has no external side effects.
    import threading
    result = {"out": bytearray(), "overflow": False}
    child = subprocess.Popen([str(executable), *arguments], stdin=subprocess.PIPE,
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                             creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    def drain(stream, keep):
        count = 0
        try:
            while True:
                part = stream.read(8192)
                if not part:
                    break
                count += len(part)
                if count > (limit if keep else 65536):
                    result["overflow"] = True
                if keep and len(result["out"]) + len(part) <= limit:
                    result["out"].extend(part)
                elif keep:
                    result["overflow"] = True
        finally:
            stream.close()
    def feed():
        try:
            child.stdin.write(payload)
            child.stdin.flush()
        except (OSError, BrokenPipeError):
            pass
        finally:
            child.stdin.close()
    threads = [threading.Thread(target=drain, args=(child.stdout, True), daemon=True),
               threading.Thread(target=drain, args=(child.stderr, False), daemon=True),
               threading.Thread(target=feed, daemon=True)]
    try:
        for thread in threads:
            thread.start()
        deadline = time.monotonic() + timeout
        while child.poll() is None:
            if result["overflow"] or time.monotonic() > deadline:
                fail()
            time.sleep(0.05)
        for thread in threads:
            thread.join(5)
        if child.returncode != 0 or result["overflow"] or any(t.is_alive() for t in threads):
            fail()
        return result["out"]
    except Exception:
        if child.poll() is None:
            if os.name == "nt":
                # Only this private child PID and its descendants; no global
                # process-name cleanup or other user's agent is touched.
                killer = Path(os.environ.get("SystemRoot", "C:/Windows")) / "System32/taskkill.exe"
                try:
                    subprocess.run([str(killer), "/PID", str(child.pid), "/T", "/F"],
                                   capture_output=True, timeout=10)
                finally:
                    if child.poll() is None:
                        child.kill()
            else:
                child.kill()
            child.wait(timeout=10)
        for thread in threads:
            thread.join(5)
        clear(result["out"])
        raise ValueError("Private recovery child failed or timed out") from None


def owner_sid(powershell):
    raw = safe_child(powershell, ["-NoProfile", "-NonInteractive", "-Command",
        "[Security.Principal.WindowsIdentity]::GetCurrent().User.Value"], b"", 256, 30)
    sid = bytes(raw).decode("ascii").strip()
    if not re.fullmatch(r"S-1-[0-9-]+", sid):
        fail()
    return sid


def owner_acl(path, sid, powershell):
    script = """$ErrorActionPreference='Stop'
$a=Get-Acl -LiteralPath $env:TAMER_V2_ACL_PATH
if((Get-Item -LiteralPath $env:TAMER_V2_ACL_PATH).PSIsContainer -and !$a.AreAccessRulesProtected){exit 1}
$r=@($a.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]))
if($r.Count -ne 2){exit 2}
foreach($s in @($env:TAMER_V2_OWNER,'S-1-5-18')) {
 $x=@($r|Where-Object {$_.IdentityReference.Value -eq $s})
 if($x.Count -ne 1 -or $x[0].AccessControlType -ne 'Allow' -or $x[0].FileSystemRights -ne [Security.AccessControl.FileSystemRights]::FullControl){exit 3}
}
"""
    # Only non-secret path/SID in an isolated child environment; no global changes.
    env = os.environ.copy()
    env.update(TAMER_V2_ACL_PATH=str(path), TAMER_V2_OWNER=sid)
    result = subprocess.run([str(powershell), "-NoProfile", "-NonInteractive", "-Command", script],
                            env=env, capture_output=True, timeout=30)
    if result.returncode:
        fail()


def create_owner_directory(path, sid):
    """Atomic CREATE_NEW directory ACL; never relax an existing parent's ACL."""
    adv = ctypes.WinDLL("advapi32", use_last_error=True)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    class Attributes(ctypes.Structure):
        _fields_ = [("length", wintypes.DWORD), ("descriptor", wintypes.LPVOID), ("inherit", wintypes.BOOL)]
    descriptor = wintypes.LPVOID()
    adv.ConvertStringSecurityDescriptorToSecurityDescriptorW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, ctypes.POINTER(wintypes.LPVOID), wintypes.LPVOID]
    adv.ConvertStringSecurityDescriptorToSecurityDescriptorW.restype = wintypes.BOOL
    kernel.CreateDirectoryW.argtypes = [wintypes.LPCWSTR, ctypes.POINTER(Attributes)]
    kernel.CreateDirectoryW.restype = wintypes.BOOL
    kernel.LocalFree.argtypes = [wintypes.LPVOID]
    if not adv.ConvertStringSecurityDescriptorToSecurityDescriptorW(
            "D:P(A;OICI;FA;;;" + sid + ")(A;OICI;FA;;;SY)", 1, ctypes.byref(descriptor), None):
        fail()
    try:
        attributes = Attributes(ctypes.sizeof(Attributes), descriptor, False)
        if not kernel.CreateDirectoryW(str(path), ctypes.byref(attributes)):
            fail()
    finally:
        kernel.LocalFree(descriptor)


def write_new(path, raw):
    with path.open("xb") as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())


class NewOwnedFile:
    """CREATE_NEW and publication verification on the same non-shared write handle."""
    def __init__(self, path, raw):
        import msvcrt
        kernel = owned._api()
        handle = kernel.CreateFileW(str(path), 0x80000000 | 0x40000000, 0x1,
                                    None, 1, 0x00200000, None)
        if handle == ctypes.c_void_p(-1).value:
            fail()
        try:
            descriptor = msvcrt.open_osfhandle(handle, os.O_RDWR | os.O_BINARY)
        except Exception:
            kernel.CloseHandle(handle)
            raise
        self.stream = os.fdopen(descriptor, "r+b")
        try:
            info = os.fstat(descriptor)
            if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1 or getattr(info, "st_file_attributes", 0) & 0x400:
                fail()
            self.identity = (info.st_dev, info.st_ino)
            self.stream.write(raw)
            self.stream.flush()
            os.fsync(self.stream.fileno())
            self.stream.seek(0)
            if sha(self.stream.read()) != sha(raw):
                fail()
        except Exception:
            self.stream.close()
            raise

    def bytes(self):
        self.stream.seek(0)
        return self.stream.read()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.stream.close()


def recover(plan_path, expected_plan_hash):
    """Actual Windows path. Caller must enforce CLI approval first."""
    if os.name != "nt":
        fail()
    hash_value(expected_plan_hash)
    plan_path = local_path(str(plan_path))
    regular(plan_path)
    plaintext = protected = None
    contents = {}
    destination = None
    with ExitStack() as locks:
        locks.enter_context(owned.locked_directories([plan_path.parent]))
        plan_handle = locks.enter_context(owned.LockedFile(plan_path, deletable=False))
        plan_raw = bounded_bytes(plan_handle, 65536)
        if sha(plan_raw) != expected_plan_hash:
            fail()
        plan = validate_plan(json_object(plan_raw, 65536))
        source = Path(__file__).resolve().parent
        paths = {local_path(item["path"]): item["sha256"] for item in plan["tools"].values()}
        paths.update({source / name: digest for name, digest in plan["sources"].items()})
        paths.update({local_path(plan[name]["path"]): plan[name]["sha256"] for name in ("ciphertext", "receipt", "certificateEvidence")})
        parent = local_path(plan["destination"]["parent"])
        locks.enter_context(owned.locked_directories([parent, *[p.parent for p in paths]]))
        checked = {}
        for path, expected in paths.items():
            regular(path)
            handle = locks.enter_context(owned.LockedFile(path, deletable=False))
            limit = 128 * 1024 * 1024
            for name, size in (("ciphertext", PLAIN_LIMIT), ("receipt", 16384), ("certificateEvidence", 16384)):
                if path == local_path(plan[name]["path"]):
                    limit = size
            if sha(bounded_bytes(handle, limit)) != expected:
                fail()
            checked[path] = handle
        if local_path(plan["tools"]["python"]["path"]) != Path(sys.executable).absolute():
            fail()
        validate_receipt(checked[local_path(plan["receipt"]["path"])].bytes(), plan)
        validate_certificate_evidence(checked[local_path(plan["certificateEvidence"]["path"])].bytes(), plan["binding"])
        ps = local_path(plan["tools"]["powershell"]["path"])
        sid = owner_sid(ps)
        owner_acl(parent, sid, ps)
        for name in ("ciphertext", "receipt", "certificateEvidence"):
            owner_acl(local_path(plan[name]["path"]), sid, ps)
        # A rename of the destination cannot silently retry the same ciphertext.
        # A failed attempt remains until a separately reviewed recovery decision.
        write_new(parent / ("iap-v2-restore-" + plan["ciphertext"]["sha256"] + ".attempt.private.json"),
                  json.dumps({"planSha256": expected_plan_hash, "attempt": 1,
                              "automaticRetry": False}).encode())
        destination = parent / plan["destination"]["name"]
        create_owner_directory(destination, sid)
        locks.enter_context(owned.locked_directories([destination]))
        owner_acl(destination, sid, ps)
        write_new(destination / "restore-attempt.private.json", json.dumps({"planSha256": expected_plan_hash,
            "attempt": 1, "automaticRetry": False, "restored": False}).encode())
        # Keep Git/MSYS agent socket paths short; this second fresh owner-only
        # directory is bound to the same reviewed run, never a shared GPG home.
        home = parent / ("g-" + plan["destination"]["name"].rsplit("-", 1)[1])
        create_owner_directory(home, sid)
        locks.enter_context(owned.locked_directories([home]))
        owner_acl(home, sid, ps)
        script = source / "Restore-IapSigningV2.ps1"
        common = ["-NoProfile", "-NonInteractive", "-File", str(script), "-ExecuteApproved"]
        decryption_returned = False
        try:
            decrypt_args = [*common, "-Mode", "Decrypt", "-Gpg", plan["tools"]["gpg"]["path"],
                "-GpgConf", plan["tools"]["gpgconf"]["path"], "-Pinentry", plan["tools"]["pinentry"]["path"],
                "-GpgHome", str(home), "-CipherSha256", plan["ciphertext"]["sha256"]]
            plaintext = safe_child(ps, decrypt_args,
                checked[local_path(plan["ciphertext"]["path"])].bytes(), PLAIN_LIMIT)
            decryption_returned = True  # Worker requires its agent cleanup first.
            contents = validate_archive(plaintext, plan["binding"])
            # DER/PEM X509 equivalence and CurrentUser DPAPI roundtrip happen in this
            # private child. No keystore password or raw ZIP reaches terminal output.
            payload = bytearray(json.dumps({"secret": json_object(contents["signing-secret"], 8192),
                "der": base64.b64encode(contents["certificate-der"]).decode("ascii"),
                "pem": bytes(contents["certificate-pem"]).decode("ascii"),
                "certificateSha256": plan["binding"]["certificateSha256"]}).encode("utf-8"))
            try:
                protected = safe_child(ps, [*common, "-Mode", "Protect"], payload, 32768, 60)
            finally:
                clear(payload)
            result = json_object(protected, 32768)
            if (set(result) != {"dpapi", "certificateMatched", "dpapiRoundtrip"}
                    or result["certificateMatched"] is not True or result["dpapiRoundtrip"] is not True
                    or not isinstance(result["dpapi"], str) or not 64 <= len(result["dpapi"]) <= 16384
                    or len(result["dpapi"]) % 2 or not re.fullmatch(r"[0-9a-fA-F]+", result["dpapi"])):
                fail()
            output_hashes = {}
            publications = {name: contents[role] for role, name in OUTPUT_NAMES.items()}
            publications["password.dpapi"] = result["dpapi"].encode("utf-8")
            for name, raw in publications.items():
                path = destination / name
                handle = locks.enter_context(NewOwnedFile(path, raw))
                owner_acl(path, sid, ps)
                output_hashes[name] = sha(handle.bytes())
                if output_hashes[name] != sha(raw):
                    fail()
            receipt = {"schema": "tamer-iap-v2-restoration-1", "archiveContentVerified": True,
                "planSha256": expected_plan_hash, "encryptedSha256": plan["ciphertext"]["sha256"],
                "reviewedCertificateBindingMatched": True, "certificateEvidenceSha256": plan["certificateEvidence"]["sha256"],
                "currentUserDpapiRoundtripVerified": True, "originalMetadataUnchanged": True,
                "ownedAgentCleanupReported": True,
                "outputSha256": output_hashes, "googleUploadCertificateActive": False, "uploadReady": False,
                "privateKeyUseVerified": False, "plaintextArchiveWritten": False, "secretFileWritten": False}
            write_new(destination / "restoration-receipt.private.json", json.dumps(receipt, indent=2).encode())
            return {"restored": True, "currentUserDpapiRoundtripVerified": True,
                    "privateKeyUseVerified": False, "uploadReady": False}
        except Exception:
            agent_cleanup = decryption_returned
            if not decryption_returned:
                # Even a crashed/terminated PowerShell worker must not leave its
                # dedicated agent unmanaged. This operation never asks a password.
                agent_home = str(home)
                if "/Git/usr/bin/" in plan["tools"]["gpg"]["path"].replace("\\", "/"):
                    agent_home = "/" + home.drive[0].lower() + home.as_posix()[2:]
                try:
                    cleanup = safe_child(plan["tools"]["gpgconf"]["path"],
                        ["--homedir", agent_home, "--kill", "gpg-agent"], b"", 65536, 15)
                    clear(cleanup)
                    agent_cleanup = True
                except Exception:
                    agent_cleanup = False
            write_new(destination / "restoration-failed.private.json", json.dumps({"restored": False,
                "retry": False, "preservePartial": True, "ownedAgentCleanupReported": agent_cleanup}).encode())
            raise ValueError("V2 restoration failed; restricted partial output preserved") from None
        finally:
            for data in (plaintext, protected, *contents.values()):
                clear(data)


class SafeArgumentParser(argparse.ArgumentParser):
    def error(self, _message):
        # Unknown options may contain accidentally supplied secrets. Never echo
        # argv values in argparse's default "unrecognized arguments" diagnostic.
        self.exit(2, "Invalid recovery options; use the reviewed plan and local Pinentry only.\n")


def main(argv=None):
    parser = SafeArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=Path)
    parser.add_argument("--plan-sha256")
    parser.add_argument("--execute-once", action="store_true")
    parser.add_argument("--approve-secret-restoration", action="store_true")
    args = parser.parse_args(argv)
    if not args.execute_once and not args.approve_secret_restoration:
        print('{"preparedOnly":true,"inputRead":false,"executed":false}')
        return 0
    if not args.execute_once or not args.approve_secret_restoration or not args.plan or not args.plan_sha256:
        parser.error("Both explicit approval switches and a reviewed absolute plan/hash are required")
    try:
        result = recover(args.plan, args.plan_sha256)
    except Exception:
        print('{"restored":false,"retry":false,"detailsSuppressed":true}')
        return 1
    print(json.dumps(result))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
