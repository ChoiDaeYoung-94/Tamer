"""Windows handle-bound cleanup; no pathname deletion or recursive removal.

Share modes exclude writers/deleters for checked files. Ancestor directory handles
exclude rename/delete while paths are opened. Existing incompatible handles fail
closed. This is not an ACL boundary against administrators or raw volume access.
"""
import ctypes
from ctypes import wintypes
from contextlib import ExitStack, contextmanager
import hashlib
import os
from pathlib import Path
import sys


class FileDispositionInfo(ctypes.Structure):
    _fields_ = [('DeleteFile', ctypes.c_ubyte)]  # Win32 BOOLEAN, not four-byte BOOL.


def _api():
    if sys.platform != 'win32':
        raise OSError('Windows file handles required')
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                  wintypes.LPVOID, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    kernel.CreateFileW.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.CloseHandle.restype = wintypes.BOOL
    kernel.SetFileInformationByHandle.argtypes = [wintypes.HANDLE, ctypes.c_int,
                                                wintypes.LPVOID, wintypes.DWORD]
    kernel.SetFileInformationByHandle.restype = wintypes.BOOL
    return kernel


class LockedFile:
    def __init__(self, path, directory=False):
        import msvcrt
        self.kernel = _api()
        self.stream = None
        self.descriptor = None
        self.directory = directory
        # Directory locks exclude write/delete handles to the directory itself;
        # they do not prevent normal creation of new children.
        access = 0x80 if directory else 0x80000000 | 0x10000  # attributes / read+DELETE
        share = 0x1  # no FILE_SHARE_WRITE or FILE_SHARE_DELETE
        handle = self.kernel.CreateFileW(str(path), access, share, None, 3,
                                         0x02000000 | 0x00200000, None)  # backup + open reparse
        if handle == ctypes.c_void_p(-1).value:
            raise OSError(ctypes.get_last_error(), 'Cannot lock owned path')
        try:
            descriptor = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
        except Exception:
            self.kernel.CloseHandle(handle)
            raise
        self.descriptor = descriptor
        try:
            info = os.fstat(descriptor)
            if getattr(info, 'st_file_attributes', 0) & 0x400:
                raise ValueError('Reparse handle rejected')
            import stat
            if stat.S_ISDIR(info.st_mode) != directory:
                raise ValueError('Unexpected owned handle type')
            self.identity = (info.st_dev, info.st_ino)
            if not directory:
                self.stream = os.fdopen(descriptor, 'rb')
        except Exception:
            self.close()
            raise

    def bytes(self):
        if self.directory:
            raise ValueError('Cannot read directory as file')
        self.stream.seek(0)
        return self.stream.read()

    def verify(self, expected_identity, expected_sha256):
        if self.identity != tuple(expected_identity) or hashlib.sha256(self.bytes()).hexdigest() != expected_sha256:
            raise ValueError('Owned identity or content changed')

    def delete(self):
        """Mark exactly the verified handle for deletion, never reopen its pathname."""
        import msvcrt
        if self.directory:
            raise ValueError('Directory deletion is not supported')
        disposition = FileDispositionInfo(1)
        if not self.kernel.SetFileInformationByHandle(msvcrt.get_osfhandle(self.stream.fileno()),
                                                      4, ctypes.byref(disposition), ctypes.sizeof(disposition)):
            raise OSError(ctypes.get_last_error(), 'Owned handle deletion failed')

    def close(self):
        if self.stream is not None:
            self.stream.close()
            self.stream = None
            self.descriptor = None
        elif self.descriptor is not None:
            os.close(self.descriptor)
            self.descriptor = None

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


@contextmanager
def locked_directories(paths):
    """Lock from volume root down; reject junctions before entering each child."""
    with ExitStack() as stack:
        held = set()
        for supplied in paths:
            path = Path(supplied)
            if not path.is_absolute() or '..' in path.parts or str(path).startswith('\\\\'):
                raise ValueError('Absolute local directory path required')
            for parent in (*reversed(path.parents), path):
                key = str(parent).casefold()
                if key not in held:
                    stack.enter_context(LockedFile(parent, directory=True))
                    held.add(key)
        yield


@contextmanager
def verified_files(root, expected):
    """Acquire and check every target before yielding any handle for removal.

    expected maps relative path -> (identity, sha256). Missing files are rejected:
    an interrupted cleanup preserves its journal for explicit manual recovery.
    """
    root = Path(root)
    targets = {}
    for relative in expected:
        path = Path(relative)
        if path.is_absolute() or '..' in path.parts:
            raise ValueError('Unsafe owned relative path')
        targets[relative] = root / path
    with locked_directories([p.parent for p in targets.values()]):
        with ExitStack() as stack:
            handles = {}
            for relative, path in targets.items():
                handle = stack.enter_context(LockedFile(path))
                handle.verify(*expected[relative])
                handles[relative] = handle
            yield handles
