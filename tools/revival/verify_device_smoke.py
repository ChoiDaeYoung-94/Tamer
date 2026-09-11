"""Observe an approved smoke artifact on a USB device without changing existing apps.

Requires TAMER_ADB_SERIAL in the local environment (never written to evidence).
No network/settings changes, account flows, ad requests, purchase or global log
clearing. Refuses an existing package. Removes only its own newly installed test
package, and only if its installed package paths still match the captured paths.
Screenshots are taken only while the isolated app is the resumed activity.
"""
import argparse
import json
import os
import re
import subprocess
import tempfile
import time
import zipfile
from pathlib import Path

from verify_emulator_runtime import validate_provenance
from verify_bundle import APP_ID, ROOT


def require_absent_package(result):
    if result.returncode not in (0, 1) or result.stderr.strip():
        raise ValueError('Cannot establish package absence; existing apps preserved')
    if result.stdout.strip():
        raise ValueError('Isolated package already exists; it will not be overwritten or removed')


def foreground_is_smoke(text):
    return any(('mResumedActivity:' in line or 'topResumedActivity=' in line)
               and re.search(r'\b' + re.escape(APP_ID) + r'/[^\s}]+', line)
               for line in text.splitlines())


def smoke_observation_passed(result):
    return (all(result.get(key) is True for key in [
        'installed', 'launchCommandSucceeded', 'appProcessObserved',
        'isolatedAppForeground', 'screenshotCaptured', 'unityStartupLogObserved',
        'ownFreshInstallRemoved'])
        and result.get('appLogcatExitCode') == 0
        and result.get('fatalSignalObserved') is False
        and not result.get('failure'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--apk', type=Path)
    group.add_argument('--apks', type=Path)
    parser.add_argument('--expected-sha256', required=True)
    parser.add_argument('--provenance', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--observe-seconds', type=int, default=30)
    args = parser.parse_args()
    serial = os.environ.get('TAMER_ADB_SERIAL', '')
    if not serial or serial.startswith('emulator-'):
        parser.error('Set private TAMER_ADB_SERIAL for the authorized physical device')
    artifact = args.apk or args.apks
    artifact_hash = validate_provenance(artifact, args.expected_sha256,
        json.loads(args.provenance.read_text(encoding='utf-8-sig')))
    if args.output.exists():
        parser.error('Choose a new private evidence directory')
    args.output.mkdir(parents=True)
    adb = ROOT / 'tools/.local/android-sdk/platform-tools/adb.exe'
    player = Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer')
    build_tools = player / 'SDK/build-tools/36.0.0'

    def run(command, timeout=30):
        return subprocess.run([str(value) for value in command], capture_output=True, text=True,
            encoding='utf-8', errors='replace', timeout=timeout)

    def device(*command, timeout=30):
        return run([adb, '-s', serial, *command], timeout=timeout)

    result = dict(schema=1, applicationId=APP_ID, artifactSha256=artifact_hash,
        installed=False, appProcessObserved=False, runtime16KBVerified=False,
        existingAppsModified=False, networkSettingsChanged=False, globalLogsCleared=False,
        scope='Newly installed isolated RevivalSmoke only')
    own_paths = None
    try:
        state = device('get-state')
        if state.returncode or state.stdout.strip() != 'device':
            raise ValueError('USB device is unavailable or unauthorized')
        require_absent_package(device('shell', 'pm', 'path', APP_ID))
        for key in ['ro.product.model', 'ro.build.version.release', 'ro.build.version.sdk',
                    'ro.product.cpu.abilist', 'ro.dalvik.vm.native.bridge',
                    'bionic.linker.16kb.app_compat.enabled', 'pm.16kb.app_compat.disabled']:
            reply = device('shell', 'getprop', key)
            result[key] = reply.stdout.strip() if reply.returncode == 0 else None
        reply = device('shell', 'getconf', 'PAGE_SIZE')
        if reply.returncode or reply.stdout.strip() not in ('4096', '16384', '65536'):
            raise ValueError('Could not observe PAGE_SIZE')
        result['pageSize'] = int(reply.stdout.strip())
        with tempfile.TemporaryDirectory(dir=args.output) as temp:
            paths = [args.apk] if args.apk else []
            if args.apks:
                with zipfile.ZipFile(args.apks) as archive:
                    for index, entry in enumerate(archive.infolist()):
                        if entry.filename.endswith('.apk'):
                            path = Path(temp) / f'split-{index}.apk'
                            path.write_bytes(archive.read(entry))
                            paths.append(path)
            if not paths:
                raise ValueError('No APKs in artifact')
            signers = set()
            for path in paths:
                badging = run([build_tools / 'aapt2.exe', 'dump', 'badging', path])
                if badging.returncode or f"name='{APP_ID}'" not in badging.stdout or "versionCode='26'" not in badging.stdout:
                    raise ValueError('Artifact package identity mismatch')
                signature = run([player / 'OpenJDK/bin/java.exe', '-jar', build_tools / 'lib/apksigner.jar', 'verify', '--print-certs', path])
                match = re.search(r'Signer #1 certificate SHA-256 digest:\s*([0-9a-f]+)', signature.stdout)
                if signature.returncode or 'CN=Android Debug' not in signature.stdout or not match:
                    raise ValueError('Artifact debug signature verification failed')
                signers.add(match.group(1))
            if len(signers) != 1:
                raise ValueError('Split signers disagree')
            result['signerSha256'] = signers.pop()
            # No -r: never replace a package that appeared after the absence check.
            install = device('install-multiple', '-t', *paths, timeout=180)
            if install.returncode or 'Success' not in install.stdout:
                raise ValueError('Installation failed; existing apps were not replaced')
            result['installed'] = True
            captured = device('shell', 'pm', 'path', APP_ID)
            if captured.returncode or not captured.stdout.startswith('package:'):
                raise ValueError('Cannot capture installed package ownership')
            own_paths = captured.stdout.strip()
            resolved = device('shell', 'cmd', 'package', 'resolve-activity', '--brief', APP_ID)
            component = next((line for line in resolved.stdout.splitlines() if line.startswith(APP_ID + '/')), None)
            if component is None:
                raise ValueError('No isolated launch activity')
            launch = device('shell', 'am', 'start', '-W', '-n', component)
            result['launchCommandSucceeded'] = launch.returncode == 0 and 'Status: ok' in launch.stdout
            time.sleep(max(1, min(args.observe_seconds, 120)))
            pid = device('shell', 'pidof', APP_ID).stdout.strip()
            result['appProcessObserved'] = bool(re.fullmatch(r'\d+', pid))
            if result['appProcessObserved']:
                logs = device('logcat', '-d', '--pid=' + pid)
                result['appLogcatExitCode'] = logs.returncode
                (args.output / 'app-logcat.txt').write_text(logs.stdout, encoding='utf-8')
                result['unityStartupLogObserved'] = logs.returncode == 0 and 'Unity' in logs.stdout
                result['fatalSignalObserved'] = (
                    bool(re.search(r'Fatal signal|FATAL EXCEPTION', logs.stdout)) if logs.returncode == 0 else None)
                maps = device('shell', 'run-as', APP_ID, 'cat', '/proc/' + pid + '/maps')
                result['processMapsExitCode'] = maps.returncode
                (args.output / 'process-maps.txt').write_text(maps.stdout, encoding='utf-8')
                result['processMapsReadable'] = maps.returncode == 0
                result['unityNativeLibrariesMapped'] = all(name in maps.stdout for name in ['libunity.so', 'libil2cpp.so'])
                result['nativeTranslationMapped'] = 'ndk_translation' in maps.stdout or 'houdini' in maps.stdout
            resumed = device('shell', 'dumpsys', 'activity', 'activities')
            result['foregroundQueryExitCode'] = resumed.returncode
            result['resumedMarkerObserved'] = any(
                'mResumedActivity:' in line or 'topResumedActivity=' in line
                for line in resumed.stdout.splitlines())
            result['isolatedActivityMentioned'] = APP_ID + '/' in resumed.stdout
            result['systemPermissionActivityResumed'] = any(
                ('mResumedActivity:' in line or 'topResumedActivity=' in line)
                and 'permissioncontroller/' in line
                for line in resumed.stdout.splitlines())
            result['isolatedAppForeground'] = resumed.returncode == 0 and foreground_is_smoke(resumed.stdout)
            if result['isolatedAppForeground']:
                with (args.output / 'screen.png').open('wb') as stream:
                    capture = subprocess.run([str(adb), '-s', serial, 'exec-out', 'screencap', '-p'],
                        stdout=stream, stderr=subprocess.PIPE, timeout=30)
                    result['screenshotCaptured'] = capture.returncode == 0
            # Actual native ABI/linker mode and visible smoke need separate review.
    except (ValueError, subprocess.TimeoutExpired) as error:
        result['failure'] = type(error).__name__ if isinstance(error, subprocess.TimeoutExpired) else str(error)
    finally:
        if own_paths is not None:
            try:
                current = device('shell', 'pm', 'path', APP_ID)
                if current.returncode == 0 and current.stdout.strip() == own_paths:
                    cleanup = device('uninstall', APP_ID, timeout=60)
                    result['ownFreshInstallRemoved'] = cleanup.returncode == 0 and 'Success' in cleanup.stdout
                else:
                    result['cleanupRefusedChangedPackage'] = True
            except subprocess.TimeoutExpired:
                result['ownFreshInstallRemoved'] = False
        result['smokeObservationPassed'] = smoke_observation_passed(result)
        result['visualReviewCompleted'] = False
        (args.output / 'device-smoke.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
    return 0 if result['smokeObservationPassed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
