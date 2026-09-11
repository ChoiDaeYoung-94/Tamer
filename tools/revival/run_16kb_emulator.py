"""Probe this project's isolated 16KB AVD with a bounded software-emulation boot.

Never installs a driver, changes Windows features, or controls another AVD/device.
On successful boot it leaves this AVD running for explicit follow-up validation;
on timeout/failure it stops only the emulator it launched. Raw logs stay local.
"""
import argparse
import json
import os
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
AVD = 'Tamer_16KB_API36'
SERIAL = 'emulator-5580'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--timeout', type=int, default=600)
    parser.add_argument('--gles-only', action='store_true', help='Diagnostic fallback: SwiftShader, Vulkan disabled, kernel log')
    args = parser.parse_args()
    if not 1 <= args.timeout <= 3600:
        parser.error('timeout must be between 1 and 3600 seconds')
    sdk = ROOT / 'tools/.local/android-sdk'
    emulator = sdk / 'emulator/emulator.exe'
    adb = sdk / 'platform-tools/adb.exe'
    logs = ROOT / 'Logs/revival'
    logs.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    env['ANDROID_USER_HOME'] = str(ROOT / 'tools/.local/android-user')
    env['ANDROID_AVD_HOME'] = str(ROOT / 'tools/.local/android-avd')
    env['ANDROID_HOME'] = str(sdk)

    def call(command, timeout=20):
        result = subprocess.run([str(value) for value in command], capture_output=True, text=True,
                                encoding='utf-8', errors='replace', timeout=timeout, env=env)
        return result.returncode, result.stdout.strip()

    _, devices = call([adb, 'devices'])
    if SERIAL in devices:
        raise ValueError('Reserved emulator port is occupied; existing device preserved')
    acceleration = call([emulator, '-accel-check'])
    command = [str(emulator), '-avd', AVD, '-port', '5580', '-no-window', '-no-audio',
               '-no-snapshot', '-no-boot-anim', '-gpu', 'software', '-accel', 'off',
               '-memory', '2048', '-cores', '2']
    if args.gles_only:
        command[command.index('software')] = 'swiftshader'
        command.extend(['-feature', '-Vulkan', '-show-kernel'])
    started = time.monotonic()
    result = dict(schema=1, avd=AVD, serial=SERIAL, requestedImage='system-images;android-36;google_apis_ps16k;x86_64',
                  accelerationCheck=acceleration, accelerationRequested='off', bootCompleted=False,
                  runtime16KBVerified=False, timeoutSeconds=args.timeout)
    result['glesOnlyDiagnostic'] = args.gles_only
    with (logs / 'emulator-16kb.log').open('w', encoding='utf-8') as log:
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, env=env,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        result['launcherPid'] = process.pid
        try:
            while time.monotonic() - started < args.timeout:
                if process.poll() is not None:
                    result['launcherExitCode'] = process.returncode
                    result['failure'] = 'Emulator exited before boot completed; inspect private emulator log'
                    break
                try:
                    _, boot = call([adb, '-s', SERIAL, 'shell', 'getprop', 'sys.boot_completed'], timeout=10)
                except subprocess.TimeoutExpired:
                    boot = ''
                if boot == '1':
                    result['bootCompleted'] = True
                    _, result['pageSize'] = call([adb, '-s', SERIAL, 'shell', 'getconf', 'PAGE_SIZE'])
                    properties = {}
                    for key in ['ro.build.version.release', 'ro.build.version.sdk', 'ro.product.cpu.abilist',
                                'ro.dalvik.vm.native.bridge', 'ro.build.fingerprint', 'ro.boot.hardware.cpu.pagesize']:
                        _, properties[key] = call([adb, '-s', SERIAL, 'shell', 'getprop', key])
                    result['properties'] = properties
                    result['emulatorLeftRunning'] = True
                    # Boot/PAGE_SIZE are environment evidence, not an app execution verdict.
                    break
                time.sleep(5)
            if not result['bootCompleted']:
                result.setdefault('failure', 'Boot timeout')
                if process.poll() is None:
                    try:
                        call([adb, '-s', SERIAL, 'emu', 'kill'], timeout=10)
                    except subprocess.TimeoutExpired:
                        result['consoleShutdownTimedOut'] = True
                    try:
                        process.wait(timeout=15)
                    except subprocess.TimeoutExpired:
                        pass
                result['emulatorLeftRunning'] = False
        finally:
            if not result['bootCompleted'] and process.poll() is None:
                # The launcher owns a QEMU child. Terminating just the launcher
                # can orphan it; terminate only this newly launched process tree.
                if os.name == 'nt':
                    subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
                                   capture_output=True, timeout=20)
                else:
                    process.terminate()
                process.wait(timeout=20)
                result['ownedProcessTreeStopped'] = True
            result['elapsedSeconds'] = round(time.monotonic() - started, 2)
            (logs / 'emulator-16kb-probe.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
    return 0 if result['bootCompleted'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
