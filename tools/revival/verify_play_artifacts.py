"""Read-only original AAB / Play APK identity and native payload verification.

No upload-key certificate is inferred from the APK application signer or stamp.
All expectations are explicit; a successful report is static evidence only.
"""

import argparse
from contextlib import ExitStack
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import zipfile
import zlib

from verify_native_alignment import ABI_ELF, inspect_apk
from windows_owned_files import LockedFile, locked_directories


class VerificationError(ValueError):
    """A guard failure with a fixed diagnostic safe to show in the CLI."""


def sha256(value):
    if not isinstance(value, str) or not re.fullmatch(r'[a-fA-F0-9]{64}', value):
        raise VerificationError('Expected exactly 64 hexadecimal SHA-256 characters')
    return value.lower()


def positive_integer(value):
    if not re.fullmatch(r'[1-9][0-9]*', str(value)):
        raise VerificationError('Expected a positive decimal integer')
    return int(value)


def parse_signers(output, expected):
    """Accept complete, unique app signer records; keep the stamp separate."""
    expected = [sha256(item) for item in expected]
    if not expected or len(expected) != len(set(expected)):
        raise VerificationError('Expected app signer set must be nonempty and unique')
    app, stamps, mentioned = {}, [], set()
    stamp_mentioned = False
    records, signer_counts = set(), []
    for line in output.splitlines():
        app_line = re.fullmatch(r'Signer #([1-9][0-9]*) certificate SHA-256 digest: ([a-fA-F0-9]{64})', line)
        stamp_line = re.fullmatch(r'Source Stamp Signer certificate SHA-256 digest: ([a-fA-F0-9]{64})', line)
        if line.startswith(('Signer #', 'Source Stamp Signer')):
            record_key = line.split(': ', 1)[0]
            if record_key in records:
                raise VerificationError('Duplicate signer output record')
            records.add(record_key)
        if line.startswith('Number of signers:'):
            count = re.fullmatch(r'Number of signers: ([1-9][0-9]*)', line)
            if not count:
                raise VerificationError('Malformed signer count')
            signer_counts.append(int(count[1]))
        if line.startswith('Signer #'):
            mention = re.match(r'Signer #([1-9][0-9]*) ', line)
            if not mention:
                raise VerificationError('Malformed app signer number')
            mentioned.add(int(mention[1]))
        stamp_mentioned |= line.startswith('Source Stamp Signer')
        if app_line:
            index = int(app_line[1])
            if index in app:
                raise VerificationError('Duplicate app signer index')
            app[index] = sha256(app_line[2])
        elif stamp_line:
            stamps.append(sha256(stamp_line[1]))
        elif 'certificate SHA-256 digest:' in line:
            raise VerificationError('Unrecognized certificate SHA-256 record')
    if (not app or set(app) != mentioned or sorted(app) != list(range(1, len(app) + 1))
            or len(set(app.values())) != len(app)):
        raise VerificationError('Missing, duplicate or ambiguous application signers')
    if len(stamps) > 1 or (stamp_mentioned and not stamps):
        raise VerificationError('Missing or ambiguous source stamp digest')
    if signer_counts and signer_counts != [len(app)]:
        raise VerificationError('Duplicate or inconsistent signer count')
    if set(app.values()) != set(expected):
        raise VerificationError('APK application signer set differs from explicit expectation')
    return dict(applicationSignerCertificateSha256=sorted(app.values()),
                sourceStampCertificateSha256=stamps, sourceStampIsAppSigner=False)


def parse_badging(output):
    """Parse the first package record and exact standalone SDK/debug/ABI records."""
    lines = output.splitlines()
    if not lines or not lines[0].startswith('package: '):
        raise VerificationError('First badging line must be the package metadata record')
    if sum(line.lstrip().startswith('package:') for line in lines) != 1:
        raise VerificationError('Duplicate package metadata record')
    fields, remaining = {}, lines[0][len('package: '):]
    while remaining:
        match = re.match(r"([A-Za-z][A-Za-z0-9_-]*)='([^'\r\n]*)'(?: +|$)", remaining)
        if not match or match[1] in fields:
            raise VerificationError('Malformed or duplicate package metadata field')
        fields[match[1]] = match[2]
        remaining = remaining[match.end():]
    if not {'name', 'versionCode', 'versionName'} <= fields.keys():
        raise VerificationError('Missing package identity fields')

    def record(labels, pattern):
        selected = [line for line in lines[1:] if any(
            line.lstrip().startswith(label) for label in labels)]
        if len(selected) != 1 or not re.fullmatch(pattern, selected[0]):
            raise VerificationError('Missing, duplicate or malformed ' + labels[0])
        return re.fullmatch(pattern, selected[0])

    minimum = record(('sdkVersion:', 'minSdkVersion:'), r"(?:sdkVersion|minSdkVersion):'([1-9][0-9]*)'")[1]
    target = record(('targetSdkVersion:',), r"targetSdkVersion:'([1-9][0-9]*)'")[1]
    native = record(('native-code:',), r"native-code:(?: '[A-Za-z0-9_-]+')+")[0]
    abis = re.findall(r"'([^']+)'", native)
    if len(abis) != len(set(abis)):
        raise VerificationError('Duplicate ABI record')
    debug = [line for line in lines[1:] if line.lstrip().startswith('application-debuggable')]
    if debug not in ([], ['application-debuggable']):
        raise VerificationError('Malformed or duplicate debuggable marker')
    return dict(applicationId=fields['name'], versionCode=positive_integer(fields['versionCode']),
                versionName=fields['versionName'], minSdk=int(minimum), targetSdk=int(target),
                debuggable=bool(debug), abis=sorted(abis))


def native_payloads(archive, prefix):
    """Read each .so once, rejecting duplicate ZIP paths before dict conversion."""
    seen, result = set(), {}
    for info in archive.infolist():
        if info.filename in seen:
            raise VerificationError('Duplicate ZIP entry')
        seen.add(info.filename)
        if not info.filename.startswith(prefix) or not info.filename.endswith('.so'):
            continue
        relative = info.filename[len(prefix):]
        parts = relative.split('/')
        if (len(parts) != 2 or any(part in ('', '.', '..') for part in parts)
                or '\\' in relative or '\x00' in info.orig_filename):
            raise VerificationError('Malformed native payload path')
        result['lib/' + relative] = hashlib.sha256(archive.read(info)).hexdigest()
    if not result:
        raise VerificationError('No native payloads found')
    return result


def absolute_path(value, label):
    path = Path(value)
    if not path.is_absolute():
        raise VerificationError(label + ' must be an explicit absolute path')
    return path.resolve()


def run_tool(command):
    result = subprocess.run([str(part) for part in command], capture_output=True,
                            text=True, encoding='utf-8', check=False)
    if result.returncode != 0:
        # Do not leak raw tool output, paths, certificate subjects or command lines.
        raise VerificationError('SDK inspection tool failed with exit ' + str(result.returncode))
    return result.stdout


def verify_artifacts(*, original_aab, play_apk, expected_aab_sha256,
                     expected_package, expected_version_code, expected_version_name,
                     expected_min_sdk, expected_target_sdk, expected_debuggable,
                     expected_abis, expected_app_cert_sha256, android_player,
                     build_tools_version='36.0.0'):
    """Return a report only after every guard passes. Inputs are never written.

    Windows handle locks keep path-based SDK/native reads bound to hashed inputs.
    SDK tools must be trusted local files. This API does not publish a report.
    """
    aab = absolute_path(original_aab, 'original AAB')
    apk = absolute_path(play_apk, 'Play APK')
    android = absolute_path(android_player, 'AndroidPlayer')
    if aab == apk or aab.suffix.lower() != '.aab' or apk.suffix.lower() != '.apk':
        raise VerificationError('Distinct original .aab and Play .apk inputs required')
    expected_hash = sha256(expected_aab_sha256)
    expected_certs = [sha256(item) for item in expected_app_cert_sha256]
    if not expected_certs or len(expected_certs) != len(set(expected_certs)):
        raise VerificationError('Expected application certificates must be nonempty and unique')
    abis = list(expected_abis)
    if not abis or len(abis) != len(set(abis)) or any(abi not in ABI_ELF for abi in abis):
        raise VerificationError('Expected ABIs must be known, nonempty and unique')
    expected = dict(applicationId=expected_package,
                    versionCode=positive_integer(expected_version_code), versionName=expected_version_name,
                    minSdk=positive_integer(expected_min_sdk), targetSdk=positive_integer(expected_target_sdk),
                    debuggable=expected_debuggable, abis=sorted(abis))
    if (not isinstance(expected_debuggable, bool) or not expected_package or not expected_version_name
            or expected['minSdk'] > expected['targetSdk']):
        raise VerificationError('Explicit valid package, version and SDK/debug expectations required')
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', build_tools_version):
        raise VerificationError('Expected explicit numeric build-tools version')
    tools = android / 'SDK/build-tools' / build_tools_version
    aapt = tools / 'aapt2.exe'
    java = android / 'OpenJDK/bin/java.exe'
    signer = tools / 'lib/apksigner.jar'
    if not all(path.is_file() for path in (aapt, java, signer)):
        raise VerificationError('AndroidPlayer inspection tools are missing')
    with locked_directories([aab.parent, apk.parent]), ExitStack() as stack:
        held_aab = stack.enter_context(LockedFile(aab, deletable=False))
        held_apk = stack.enter_context(LockedFile(apk, deletable=False))

        def digest(held):
            held.stream.seek(0)
            return hashlib.file_digest(held.stream, 'sha256').hexdigest()

        aab_hash, apk_hash = digest(held_aab), digest(held_apk)
        if aab_hash != expected_hash:
            raise VerificationError('Original AAB SHA-256 differs from explicit expectation')
        metadata = parse_badging(run_tool([aapt, 'dump', 'badging', apk]))
        if metadata != expected:
            raise VerificationError('APK metadata differs from explicit expectation')
        signing = parse_signers(run_tool([java, '-jar', signer, 'verify', '--print-certs', apk]), expected_certs)
        with zipfile.ZipFile(held_aab.stream) as original, zipfile.ZipFile(held_apk.stream) as delivered:
            aab_libs = native_payloads(original, 'base/lib/')
            apk_libs = native_payloads(delivered, 'lib/')
        if aab_libs != apk_libs:
            raise VerificationError('Entire native payload set/content differs from original base/lib')
        native = inspect_apk(apk, expected_abis=abis, strict_relro=False)
        if native['sha256'] != apk_hash or not native['loadZipChecksPassed']:
            raise VerificationError('APK native LOAD/ZIP inspection failed or hash differs')
        inspected = {entry['path']: entry['sha256'] for entry in native['libraries']}
        if inspected != apk_libs or native['nativeLibraryCount'] != len(apk_libs):
            raise VerificationError('Native inspection inventory differs from compared payloads')
        if digest(held_aab) != aab_hash or digest(held_apk) != apk_hash:
            raise VerificationError('Input content changed during inspection')
        return dict(schemaVersion=1, checkedAtUtc=datetime.now(timezone.utc).isoformat(),
                    staticArtifactChecksPassed=True, originalAab=str(aab), originalAabSha256=aab_hash,
                    playApk=str(apk), playApkSha256=apk_hash, androidPlayer=str(android),
                    buildToolsVersion=build_tools_version, originalAabMatchesExpectedSha256=True,
                    **metadata, **signing, apkSignatureVerified=True,
                    nativeMatchesOriginalBaseLib=True, nativePayloadSha256=apk_libs,
                    nativeInspection=native, loadZipChecksPassed=True,
                    relroChecksPassed=native['relroChecksPassed'], strictRelroExecuted=False,
                    aabUploadSignatureVerified=False, installed=False,
                    deviceSpecificSplitVerified=False, runtime16KBVerified=False,
                    policyComplianceVerified=False, releaseReady=False,
                    limitations=[
                        'Checks only explicitly supplied identity expectations; not current release criteria.',
                        'APK app signers and source stamp are separate; AAB upload signature is not checked.',
                        'RELRO is separate diagnostic evidence, never a strict RELRO rerun or runtime verdict.',
                        'No device execution, purchase, policy review, split APK or store approval is established.',
                        'Native comparison covers APK lib/<abi>/*.so and AAB base/lib/<abi>/*.so only.'])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('original-aab', 'play-apk', 'android-player', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    for name in ('aab-sha256', 'package', 'version-code', 'version-name', 'min-sdk', 'target-sdk'):
        parser.add_argument('--expected-' + name, required=True)
    parser.add_argument('--expected-debuggable', choices=('true', 'false'), required=True)
    parser.add_argument('--expected-abi', action='append', choices=sorted(ABI_ELF), required=True)
    parser.add_argument('--expected-app-cert-sha256', action='append', required=True)
    parser.add_argument('--build-tools-version', default='36.0.0')
    args = parser.parse_args(argv)
    try:
        output = absolute_path(args.output, 'output')
        # An existing parent inherits its owner's privacy controls; never create/replace inputs or reports.
        if args.output.is_symlink() or output.exists() or not output.parent.is_dir():
            raise VerificationError('Output must be a new file in an existing private directory')
        if output in (args.original_aab.resolve(), args.play_apk.resolve()):
            raise VerificationError('Output must differ from inputs')
        report = verify_artifacts(original_aab=args.original_aab, play_apk=args.play_apk,
            expected_aab_sha256=args.expected_aab_sha256, expected_package=args.expected_package,
            expected_version_code=args.expected_version_code, expected_version_name=args.expected_version_name,
            expected_min_sdk=args.expected_min_sdk, expected_target_sdk=args.expected_target_sdk,
            expected_debuggable=args.expected_debuggable == 'true', expected_abis=args.expected_abi,
            expected_app_cert_sha256=args.expected_app_cert_sha256, android_player=args.android_player,
            build_tools_version=args.build_tools_version)
        encoded = json.dumps(report, indent=2, ensure_ascii=True) + '\n'
        with output.open('x', encoding='utf-8') as stream:
            stream.write(encoded)
        print('Static artifact checks passed; local report created. Runtime/release remain unverified.')
        return 0
    except VerificationError as error:
        print('Artifact verification failed: ' + str(error) + '; no success report published.', file=sys.stderr)
        return 1
    except (ValueError, OSError, RuntimeError, UnicodeError, zipfile.BadZipFile, zlib.error) as error:
        # Exceptions from tools/ZIPs are intentionally not echoed (may contain private data).
        print('Artifact verification failed (' + type(error).__name__ + '); no success report published.', file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
