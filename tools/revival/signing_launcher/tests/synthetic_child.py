"""Isolated Windows child fixture. No real key/config/Editor/UI/build access."""
import ctypes
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import time


def marker(path):
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.GetCurrentProcess.restype = ctypes.c_void_p
    kernel.IsProcessInJob.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)]
    inside = ctypes.c_int()
    if not kernel.IsProcessInJob(kernel.GetCurrentProcess(), None, ctypes.byref(inside)):
        raise OSError('Job query failed')
    Path(path).write_text(json.dumps({'pid': os.getpid(), 'job': bool(inside.value)}))


def spawn(mode, path):
    subprocess.Popen([sys.executable, '-I', '-S', '-B', __file__, mode, path], close_fds=False)


def exact(stream, size):
    raw = bytearray()
    while len(raw) < size:
        block = stream.read(size - len(raw))
        if not block:
            raise ValueError('Partial synthetic input')
        raw.extend(block)
    return raw


def main():
    mode, path = sys.argv[1:3]
    marker(path)
    if mode in ('sentinel', 'leaf'):
        time.sleep(60)
    elif mode == 'middle':
        spawn('leaf', path + '.leaf')
        time.sleep(60)
    elif mode in ('tree', 'tree-exit'):
        spawn('middle', path + '.middle')
        if mode == 'tree':
            time.sleep(60)
        else:
            time.sleep(.5)
            print('{}', end='')
    elif mode == 'read':
        stream = sys.stdin.buffer
        if struct.unpack('>I', exact(stream, 4))[0] != 0x544C5031:
            raise ValueError('Synthetic framing mismatch')
        secret = []
        for _ in range(2):
            count = struct.unpack('>I', exact(stream, 4))[0]
            secret.append(exact(stream, count * 2).decode('utf-16-be'))
        if stream.read(1):
            raise ValueError('Synthetic trailing input')
        os.environ['TAMER_KEYSTORE_PASS'], os.environ['TAMER_KEYALIAS_PASS'] = secret
        matched = secret == ['합성-store\n🔑', '합성-key\r\né']
        Path(path + '.received').write_text('matched' if matched else 'mismatch')
        print(json.dumps({'matched': matched}), end='')
        return 0 if matched else 4
    elif mode == 'leak':
        sys.stdout.write('synthetic-sensitive-value')
        sys.stderr.write('synthetic-sensitive-value')
        return 7
    elif mode == 'partial':
        sys.stdout.write('{"matched":')
    elif mode == 'overflow':
        sys.stdout.write('X' * 32768)
        sys.stdout.flush()
        time.sleep(60)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
