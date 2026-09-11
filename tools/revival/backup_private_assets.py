"""Create or verify a local-only snapshot of the manifest's private restore files.

No SDK, service settings, signing material, caches or unlisted files are copied.
A snapshot is complete only when every hash matches and its receipt is written.
This does not encrypt data or replace an independently stored backup.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil

import restore_assets as restore

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'docs/revival/assets-manifest.json'
RECEIPT = 'private-assets-receipt.json'


def private_entries(manifest):
    entries = []
    seen = set()
    for entry in json.loads(manifest.read_text(encoding='utf-8'))['entries']:
        if entry['disposition'] != 'private-restore':
            continue
        relative = entry['path']
        path = PurePosixPath(relative)
        if (path.is_absolute() or '..' in path.parts or '\\' in relative
                or ':' in relative or path.as_posix() != relative):
            raise ValueError('Invalid private asset path: ' + relative)
        if not any(relative == folder + '.meta' or relative.startswith(folder + '/')
                   for folder in restore.ROOTS):
            raise ValueError('Outside private asset roots: ' + relative)
        if path.name in restore.PRIVATE_CONFIG_NAMES or path.suffix.lower() in {
                '.keystore', '.jks', '.p12', '.pfx', '.pem', '.key'}:
            raise ValueError('Sensitive material cannot enter asset snapshot: ' + relative)
        folded = relative.casefold()
        if folded in seen:
            raise ValueError('Duplicate private asset path: ' + relative)
        seen.add(folded)
        if not re.fullmatch('[0-9a-f]{64}', entry['sha256']):
            raise ValueError('Invalid SHA-256: ' + relative)
        if type(entry['bytes']) is not int or entry['bytes'] < 0:
            raise ValueError('Invalid size: ' + relative)
        entries.append(entry)
    if not entries:
        raise ValueError('Manifest contains no private assets')
    return entries


def check_files(root, entries):
    for entry in entries:
        path = restore.contained(root, entry['path'])
        if (not path.is_file() or path.stat().st_size != entry['bytes']
                or restore.digest(path) != entry['sha256']):
            raise ValueError('Private asset missing or hash mismatch: ' + entry['path'])


def expected_receipt(manifest, entries):
    # Git may check out JSON with CRLF or LF on different recovery hosts.
    canonical = json.dumps(json.loads(manifest.read_text(encoding='utf-8')),
                           sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode('utf-8')
    return dict(schema=1, manifestCanonicalSha256=hashlib.sha256(canonical).hexdigest(),
                files=len(entries), bytes=sum(entry['bytes'] for entry in entries),
                serviceSettings='excluded', signingMaterial='excluded', sdk='restore-from-git')


def verify(destination, manifest=MANIFEST):
    entries = private_entries(manifest)
    destination = destination.resolve()
    expected = expected_receipt(manifest, entries)
    if json.loads((destination / RECEIPT).read_text(encoding='utf-8')) != expected:
        raise ValueError('Snapshot receipt does not match the current manifest')
    expected_paths = {entry['path'] for entry in entries} | {RECEIPT}
    actual_paths = {path.relative_to(destination).as_posix()
                    for path in destination.rglob('*') if path.is_file()}
    if actual_paths != expected_paths:
        raise ValueError('Snapshot has missing or unlisted files')
    check_files(destination, entries)
    return expected


def create(source, destination, manifest=MANIFEST):
    source = source.resolve()
    destination = destination.resolve()
    if source.is_relative_to(destination) or destination.is_relative_to(source):
        raise ValueError('Source and snapshot must be separate, non-nested directories')
    entries = private_entries(manifest)
    if destination.exists():
        raise FileExistsError('Preserving existing snapshot destination')
    check_files(source, entries)  # Validate the entire source before creating anything.
    destination.mkdir(parents=True, exist_ok=False)
    for entry in entries:
        src = restore.contained(source, entry['path'])
        dest = restore.contained(destination, entry['path'])
        dest.parent.mkdir(parents=True, exist_ok=True)
        with src.open('rb') as inp, dest.open('xb') as out:
            shutil.copyfileobj(inp, out)
    check_files(destination, entries)
    receipt = expected_receipt(manifest, entries)
    with (destination / RECEIPT).open('x', encoding='utf-8') as out:
        out.write(json.dumps(receipt, indent=2) + '\n')
    # Re-read both receipt and the exact file set before reporting success.
    return verify(destination, manifest)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path)
    parser.add_argument('--destination', required=True, type=Path)
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    if args.verify and args.source:
        parser.error('--verify does not accept --source')
    if not args.verify and not args.source:
        parser.error('--source is required when creating a snapshot')
    result = verify(args.destination) if args.verify else create(args.source, args.destination)
    print(json.dumps(result))
