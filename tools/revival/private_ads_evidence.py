"""Windows owner+SYSTEM evidence directories, secured before writing private bytes."""
import ctypes
from ctypes import wintypes
from pathlib import Path
import re
import sys


def apis():
    if sys.platform != 'win32':
        raise OSError('Private evidence requires Windows ACLs')
    adv = ctypes.WinDLL('advapi32', use_last_error=True)
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.GetCurrentProcess.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    adv.OpenProcessToken.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.POINTER(wintypes.HANDLE)]
    adv.GetTokenInformation.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p,
                                      wintypes.DWORD, ctypes.POINTER(wintypes.DWORD)]
    adv.ConvertSidToStringSidW.argtypes = [ctypes.c_void_p, ctypes.POINTER(wintypes.LPWSTR)]
    adv.ConvertStringSecurityDescriptorToSecurityDescriptorW.argtypes = [wintypes.LPCWSTR,
        wintypes.DWORD, ctypes.POINTER(ctypes.c_void_p), ctypes.c_void_p]
    adv.GetNamedSecurityInfoW.argtypes = [wintypes.LPWSTR, ctypes.c_int, wintypes.DWORD,
        ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p,
        ctypes.POINTER(ctypes.c_void_p)]
    adv.GetNamedSecurityInfoW.restype = wintypes.DWORD
    adv.ConvertSecurityDescriptorToStringSecurityDescriptorW.argtypes = [ctypes.c_void_p,
        wintypes.DWORD, wintypes.DWORD, ctypes.POINTER(wintypes.LPWSTR), ctypes.c_void_p]
    return adv, kernel


def owner_sid():
    adv, kernel = apis()
    token = wintypes.HANDLE()
    if not adv.OpenProcessToken(kernel.GetCurrentProcess(), 8, ctypes.byref(token)):
        raise OSError('Cannot inspect evidence owner')
    try:
        needed = wintypes.DWORD()
        adv.GetTokenInformation(token, 1, None, 0, ctypes.byref(needed))
        data = ctypes.create_string_buffer(needed.value)
        if not adv.GetTokenInformation(token, 1, data, len(data), ctypes.byref(needed)):
            raise OSError('Cannot inspect owner SID')
        sid = ctypes.cast(data, ctypes.POINTER(ctypes.c_void_p))[0]
        text = wintypes.LPWSTR()
        if not adv.ConvertSidToStringSidW(sid, ctypes.byref(text)):
            raise OSError('Cannot convert owner SID')
        try:
            return text.value
        finally:
            kernel.LocalFree(text)
    finally:
        kernel.CloseHandle(token)


def require_private(path):
    """Accept only two full-access allow ACEs: current owner and SYSTEM."""
    adv, kernel = apis()
    sd = ctypes.c_void_p()
    if adv.GetNamedSecurityInfoW(str(path), 1, 4, None, None, None, None, ctypes.byref(sd)):
        raise OSError('Cannot inspect evidence ACL')
    text = wintypes.LPWSTR()
    try:
        if not adv.ConvertSecurityDescriptorToStringSecurityDescriptorW(sd, 1, 4, ctypes.byref(text), None):
            raise OSError('Cannot inspect evidence DACL')
        value = text.value
        prefix = value.split('(', 1)[0]
        aces = re.findall(r'\(A;([^;]*);FA;;;([^;)]+)\)', value)
        expected_sids = {'SY', owner_sid()}
        if (prefix not in ('D:P', 'D:PAI', 'D:AI', 'D:') or len(aces) != 2 or
                {sid for flags, sid in aces} != expected_sids or
                prefix + ''.join('(A;' + flags + ';FA;;;' + sid + ')' for flags, sid in aces) != value or
                any(re.sub(r'OI|CI|ID', '', flags) for flags, sid in aces)):
            raise ValueError('Evidence ACL is not owner+SYSTEM only')
    finally:
        if text:
            kernel.LocalFree(text)
        kernel.LocalFree(sd)


def create_private_directory(path):
    """Apply a protected inheritable DACL atomically with directory creation."""
    path = Path(path)
    adv, kernel = apis()
    sd = ctypes.c_void_p()
    sddl = 'D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;' + owner_sid() + ')'
    if not adv.ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, ctypes.byref(sd), None):
        raise OSError('Cannot create private ACL')
    class SecurityAttributes(ctypes.Structure):
        _fields_ = [('nLength', wintypes.DWORD), ('lpSecurityDescriptor', ctypes.c_void_p),
                    ('bInheritHandle', wintypes.BOOL)]
    attrs = SecurityAttributes(ctypes.sizeof(SecurityAttributes), sd, False)
    kernel.CreateDirectoryW.argtypes = [wintypes.LPCWSTR, ctypes.POINTER(SecurityAttributes)]
    try:
        if not kernel.CreateDirectoryW(str(path), ctypes.byref(attrs)):
            raise OSError(ctypes.get_last_error(), 'Cannot create exclusive private directory')
    finally:
        kernel.LocalFree(sd)
    require_private(path)
