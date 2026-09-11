"""Install and observe an isolated debug APK/APKS only on this task's named AVD.

The artifact must start in RevivalSmoke (no gameplay behaviours). No taps, account
login, store purchase or ad request are performed. App logs/screenshots stay local.
"""
import argparse
import hashlib
import json
import re
import subprocess
import tempfile
import time
import zipfile
from pathlib import Path

from run_16kb_emulator import ROOT, AVD, SERIAL
from verify_bundle import APP_ID


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--apk', type=Path)
    group.add_argument('--apks', type=Path)
    parser.add_argument('--output', type=Path, required=True, help='New private evidence directory')
    parser.add_argument('--observe-seconds', type=int, default=30)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('Use a new evidence directory')
    args.output.mkdir(parents=True)
    adb = ROOT / 'tools/.local/android-sdk/platform-tools/adb.exe'
    player = Path('C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer')
    build_tools = player / 'SDK/build-tools/36.0.0'

    def run(command, timeout=30):
        return subprocess.run([str(value) for value in command], capture_output=True, text=True,
                              encoding='utf-8', errors='replace', timeout=timeout)

    def device(*command, timeout=30):
        return run([adb, '-s', SERIAL, *command], timeout)

    name = device('emu', 'avd', 'name')
    if name.returncode or name.stdout.splitlines()[0] != AVD:
        raise ValueError('Expected task-owned emulator; no other device may be used')
    result = dict(schema=1, avd=AVD, serial=SERIAL, applicationId=APP_ID, installed=False,
                  appProcessObserved=False, runtime16KBVerified=False, scope='Isolated RevivalSmoke only')
    try:
        result['pageSize'] = device('shell', 'getconf', 'PAGE_SIZE').stdout.strip()
        for key in ['ro.product.cpu.abilist', 'ro.dalvik.vm.native.bridge', 'ro.build.version.sdk',
                    'ro.build.fingerprint', 'bionic.linker.16kb.app_compat.enabled', 'pm.16kb.app_compat.disabled']:
            result[key] = device('shell', 'getprop', key).stdout.strip()
        if result['pageSize'] != '16384':
            raise ValueError('Expected observed PAGE_SIZE=16384; artifact was not installed')
        artifact = args.apk or args.apks
        result['artifactSha256'] = hashlib.sha256(artifact.read_bytes()).hexdigest()
        with tempfile.TemporaryDirectory(dir=args.output) as temporary:
            paths = [args.apk] if args.apk else []
            if args.apks:
                with zipfile.ZipFile(args.apks) as archive:
                    for index, entry in enumerate(archive.infolist()):
                        if entry.filename.endswith('.apk'):
                            path = Path(temporary) / f'split-{index}.apk'
                            path.write_bytes(archive.read(entry))
                            paths.append(path)
            if not paths:
                raise ValueError('No APK entries')
            for path in paths:
                badging = run([build_tools / 'aapt2.exe', 'dump', 'badging', path])
                if badging.returncode or f"name='{APP_ID}'" not in badging.stdout or "versionCode='26'" not in badging.stdout:
                    raise ValueError('Unexpected APK identity')
                signed = run([player / 'OpenJDK/bin/java.exe', '-jar', build_tools / 'lib/apksigner.jar', 'verify', '--print-certs', path])
                if signed.returncode or 'CN=Android Debug' not in signed.stdout:
                    raise ValueError('Expected valid debug-signed APK')
            # Offline smoke validation: do not permit SDK startup to use guest networking.
            device('shell', 'svc', 'wifi', 'disable')
            device('shell', 'svc', 'data', 'disable')
            installed = device('install-multiple', '-r', '-t', *paths, timeout=180)
            result['installOutput'] = (installed.stdout + installed.stderr).strip()
            if installed.returncode:
                raise ValueError('Debug APK installation failed')
            result['installed'] = True
            resolved = device('shell', 'cmd', 'package', 'resolve-activity', '--brief', APP_ID).stdout.strip().splitlines()
            component = next((line for line in resolved if line.startswith(APP_ID + '/')), None)
            if component is None:
                raise ValueError('No isolated launch activity')
            launch = device('shell', 'am', 'start', '-W', '-n', component)
            result['launchOutput'] = launch.stdout.strip()
            time.sleep(max(1, min(args.observe_seconds, 120)))
            pid = device('shell', 'pidof', APP_ID).stdout.strip()
            result['appProcessObserved'] = bool(pid)
            if pid and re.fullmatch(r'\d+', pid):
                log = device('logcat', '-d', '--pid=' + pid).stdout
                (args.output / 'app-logcat.txt').write_text(log, encoding='utf-8')
                maps = device('shell', 'run-as', APP_ID, 'cat', '/proc/' + pid + '/maps')
                (args.output / 'process-maps.txt').write_text(maps.stdout, encoding='utf-8')
                result['unityStartupLogObserved'] = 'Unity' in log
                result['nativeBridgeMappingObserved'] = 'ndk_translation' in maps.stdout
                result['processMapsReadable'] = maps.returncode == 0
            with (args.output / 'screen.png').open('wb') as stream:
                subprocess.run([str(adb), '-s', SERIAL, 'exec-out', 'screencap', '-p'], stdout=stream, timeout=30, check=True)
            result['screenshotCaptured'] = True
            # ARM translation and linker compatibility require separate interpretation.
            result['runtime16KBVerified'] = False
            result['interpretationRequired'] = 'Review screenshot, process ABI/translation and linker compatibility; process presence alone is not a native ARM64 verdict'
    except (ValueError, subprocess.TimeoutExpired) as error:
        result['failure'] = str(error)
    finally:
        (args.output / 'runtime-observation.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
    return 0 if result['appProcessObserved'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
