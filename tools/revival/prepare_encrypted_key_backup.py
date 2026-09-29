"""Create an owner-only, verified GPG backup without a plaintext archive on disk.

Passwords are entered by the user in GnuPG pinentry, never accepted by this CLI.
The backup password does not replace the existing keystore password.
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import uuid
import zipfile

GPG = Path('C:/Program Files/Git/usr/bin/gpg.exe')
LIMIT = 16 * 1024 * 1024


def gpg_path(path):
    # Git's MSYS GPG agent rejects ':' in its Unix socket path.
    path = Path(path).absolute()
    if path.drive:
        return '/' + path.drive[0].lower() + path.as_posix()[2:]
    return path.as_posix()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def regular_path(path):
    path = Path(path).absolute()
    for part in (path, *path.parents):
        if part.exists():
            info = part.lstat()
            if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
                raise ValueError('Links and reparse points are not allowed')
    return path


def collect(inputs):
    if set(inputs) != {'keystore', 'certificate', 'recovery-note'}:
        raise ValueError('Exactly three recovery roles are required')
    contents, snapshots = {}, {}
    seen = set()
    for role, supplied in inputs.items():
        path = regular_path(supplied)
        if not path.is_file() or path.stat().st_size > LIMIT:
            raise ValueError('Invalid recovery input')
        identity = (path.stat().st_dev, path.stat().st_ino)
        if identity in seen:
            raise ValueError('Recovery inputs must be distinct')
        seen.add(identity)
        data = path.read_bytes()
        if not data or len(data) > LIMIT:
            raise ValueError('Invalid recovery input size')
        contents[role] = data
        snapshots[role] = (path, digest(data))
    return contents, snapshots


def pack(contents):
    manifest = {role: {'sha256': digest(data), 'bytes': len(data)}
                for role, data in contents.items()}
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, 'w', zipfile.ZIP_STORED) as archive:
        for role, data in contents.items():
            archive.writestr(role, data)
        archive.writestr('manifest.json', json.dumps(manifest, sort_keys=True))
    return stream.getvalue()


def verify_plaintext(plaintext, expected):
    with zipfile.ZipFile(io.BytesIO(plaintext)) as archive:
        names = archive.namelist()
        if len(names) != 4 or set(names) != {*expected, 'manifest.json'}:
            raise ValueError('Recovery archive inventory mismatch')
        for role, original in expected.items():
            info = archive.getinfo(role)
            if info.file_size != len(original) or digest(archive.read(role)) != digest(original):
                raise ValueError('Recovery content mismatch')
        if archive.getinfo('manifest.json').file_size > 4096:
            raise ValueError('Invalid recovery manifest')
        recorded = json.loads(archive.read('manifest.json'))
        actual = {role: {'sha256': digest(data), 'bytes': len(data)}
                  for role, data in expected.items()}
        if recorded != actual:
            raise ValueError('Recovery manifest mismatch')


def unchanged(snapshots):
    for path, before in snapshots.values():
        regular_path(path)
        if digest(path.read_bytes()) != before:
            raise ValueError('Recovery source changed; backup is not verified')


def owner_directory(parent):
    if os.name != 'nt':
        raise ValueError('This entry point requires Windows owner ACLs')
    parent = regular_path(parent)
    owner_root = regular_path(Path.home() / 'TamerPrivateBackups')
    if parent != owner_root:
        raise ValueError('Use the designated owner backup directory')
    # Do not alter ACLs of an existing directory containing unrelated material.
    if not parent.exists():
        parent.mkdir()
    target = parent / ('key-backup-' + uuid.uuid4().hex)
    target.mkdir()
    sid = subprocess.run(['powershell.exe', '-NoProfile', '-Command',
                          '[System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value'],
                         check=True, capture_output=True, text=True).stdout.strip()
    if not re.fullmatch(r'S-1-[0-9-]+', sid):
        raise ValueError('Cannot establish backup owner')
    subprocess.run(['icacls.exe', str(target), '/inheritance:r', '/grant:r',
                    '*' + sid + ':(OI)(CI)F', '*S-1-5-18:(OI)(CI)F'],
                   check=True, capture_output=True)
    return target


def gpg_run(home, arguments, payload=None):
    result = subprocess.run([str(GPG), '--no-options', '--homedir', gpg_path(home),
                             '--no-symkey-cache', '--pinentry-mode', 'ask', *arguments],
                            input=payload, capture_output=True)
    if result.returncode:
        # Neither native stderr nor decrypted data belongs in terminal output.
        raise ValueError('GPG cancelled or failed; no verified backup was produced')
    return result.stdout


def create_backup(inputs, destination):
    contents, snapshots = collect(inputs)
    target = owner_directory(destination)
    home = target / 'gpg-home'
    home.mkdir()
    pinentry = GPG.parent / 'pinentry-w32.exe'
    if not GPG.is_file() or not pinentry.is_file():
        raise ValueError('Existing GPG and Windows pinentry are required')
    (home / 'gpg-agent.conf').write_text(
        'pinentry-program /usr/bin/pinentry-w32.exe\nno-allow-external-cache\n', encoding='utf-8')
    encrypted = target / 'recovery.zip.gpg'
    try:
        print('Enter a NEW backup password in the local GPG window; then enter it again for verification.', flush=True)
        gpg_run(home, ['--cipher-algo', 'AES256', '--symmetric', '--output', gpg_path(encrypted)], pack(contents))
        if not encrypted.is_file() or encrypted.stat().st_size == 0:
            raise ValueError('Missing encrypted output')
        plaintext = gpg_run(home, ['--decrypt', gpg_path(encrypted)])
        verify_plaintext(plaintext, contents)
        unchanged(snapshots)
        receipt = {'verified': True, 'roles': sorted(contents),
                   'encryptedSha256': digest(encrypted.read_bytes()),
                   'sourceUnchanged': True, 'offsiteVerified': False,
                   'keystorePasswordVerified': False}
        with (target / 'verified-receipt.json').open('x', encoding='utf-8') as handle:
            json.dump(receipt, handle, indent=2)
        return encrypted
    finally:
        # Stop only the agent attached to this newly created private home.
        subprocess.run([str(GPG.parent / 'gpgconf.exe'), '--homedir', gpg_path(home),
                        '--kill', 'gpg-agent'], capture_output=True, check=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--keystore', type=Path, required=True)
    parser.add_argument('--certificate', type=Path, required=True)
    parser.add_argument('--recovery-note', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = create_backup({'keystore': args.keystore, 'certificate': args.certificate,
                                'recovery-note': args.recovery_note}, Path.home() / 'TamerPrivateBackups')
    except Exception:
        parser.exit(1, 'Backup incomplete. Preserve sources; do not upload any output without a verified receipt.\n')
    print('Verified encrypted file for your manual Drive upload: ' + str(result))
    print('Keep its password separately. The existing keystore password remains unchanged and unverified.')


if __name__ == '__main__':
    main()
