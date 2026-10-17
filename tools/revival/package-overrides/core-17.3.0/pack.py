"""Reproduce or verify the pinned, local Core package; never installs it."""
import argparse
import difflib
import gzip
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import stat
import tarfile
import zlib

PACKAGE = 'com.unity.render-pipelines.core'
VERSION = '17.3.0'
FINGERPRINT = '3f80e92e91c2611f1415e75e9b791f6aeb165f26'
UPSTREAM_TREE = 'f81b66a6949b5fb7b56ce0ede1a355a7554eea382f88dc55789068ee8a13ec2f'
FSR = 'Runtime/PostProcessing/Shaders/FSRCommon.hlsl'
UPSTREAM_FSR = '395727f6d8f0fbd9d84003f9fcf85f40bc25bb1ed60e534c40d2b301df96bb49'
OLD = b'#if REAL_IS_HALF && !defined(SHADER_API_D3D11)\n'
NEW = b'#if REAL_IS_HALF && !defined(SHADER_API_D3D11) && !defined(SHADER_API_GLES3)\n'
ARCHIVE = PACKAGE + '-17.3.0-tamer-gles3-fsr.tgz'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def inventory(files):
    return ''.join(f'{name}\t{len(data)}\t{sha(data)}\n'
                   for name, data in sorted(files.items())).encode('utf-8')


def safe_path(name):
    path = PurePosixPath(name)
    if (not name or path.is_absolute() or '\\' in name or ':' in name
            or any(c in name for c in '\x00\r\n\t')
            or any(part in ('', '.', '..') for part in name.split('/'))):
        raise ValueError('Unsafe package member path')


def snapshot(source):
    files = {}
    # Reject links and Windows junctions before descending into each directory.
    def visit(directory):
        for item in directory.iterdir():
            info = item.lstat()
            if (stat.S_ISLNK(info.st_mode)
                    or getattr(info, 'st_file_attributes', 0) & 0x400):
                raise ValueError('Source contains a link or reparse point')
            if item.is_dir():
                visit(item)
            elif stat.S_ISREG(info.st_mode):
                name = item.relative_to(source).as_posix()
                safe_path(name)
                files[name] = item.read_bytes()
            else:
                raise ValueError('Source contains a non-regular file')
    root_info = source.lstat()
    if (stat.S_ISLNK(root_info.st_mode)
            or getattr(root_info, 'st_file_attributes', 0) & 0x400):
        raise ValueError('Source root must not be a link or reparse point')
    visit(source)
    if len(files) != 2997 or sum(map(len, files.values())) != 57585783:
        raise ValueError('Unexpected upstream file count or byte count')
    if sha(inventory(files)) != UPSTREAM_TREE or sha(files[FSR]) != UPSTREAM_FSR:
        raise ValueError('Upstream hash mismatch; refusing to pack')
    return files


def make_archive(files):
    buffer = io.BytesIO()
    with gzip.GzipFile(fileobj=buffer, mode='wb', filename='', mtime=0,
                       compresslevel=9) as compressed:
        with tarfile.open(fileobj=compressed, mode='w|', format=tarfile.PAX_FORMAT) as tar:
            for name, data in sorted(files.items()):
                info = tarfile.TarInfo('package/' + name)
                info.size, info.mode = len(data), 0o644
                info.uid = info.gid = info.mtime = 0
                info.uname = info.gname = ''
                tar.addfile(info, io.BytesIO(data))
    result = buffer.getvalue()
    with tarfile.open(fileobj=io.BytesIO(result), mode='r:gz') as tar:
        members = tar.getmembers()
        if len(members) != len(files) or len({m.name for m in members}) != len(files):
            raise ValueError('Duplicate or missing archive entries')
        for member in members:
            safe_path(member.name)
            if (not member.isfile() or not member.name.startswith('package/')
                    or member.uid or member.gid or member.mtime
                    or member.uname or member.gname or member.mode != 0o644
                    or set(member.pax_headers) - {'path'}
                    or member.pax_headers.get('path', member.name) != member.name):
                raise ValueError('Unsafe or unexpected archive metadata')
            if tar.extractfile(member).read() != files[member.name[len('package/'):]]:
                raise ValueError('Archive content mismatch')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--verify', action='store_true', help='Compare outputs without writing')
    args = parser.parse_args()
    source = args.source.absolute()
    destination = Path(__file__).resolve().parent
    if source == destination or source in destination.parents:
        raise ValueError('Output must not be within the upstream source')
    upstream = snapshot(source)
    manifest = json.loads(upstream['package.json'])
    if (manifest['name'], manifest['version'], manifest['unity'], manifest['_fingerprint']) != (
            PACKAGE, VERSION, '6000.3', FINGERPRINT):
        raise ValueError('Unexpected package identity')
    if upstream[FSR].count(OLD) != 1:
        raise ValueError('Expected exactly one original FSR condition')
    patched = dict(upstream)
    patched[FSR] = upstream[FSR].replace(OLD, NEW)
    fingerprint_line = ('  },\n  "_fingerprint": "' + FINGERPRINT + '"\n').encode()
    if upstream['package.json'].count(fingerprint_line) != 1:
        raise ValueError('Unexpected fingerprint serialization')
    patched['package.json'] = upstream['package.json'].replace(fingerprint_line, b'  }\n')
    expected_manifest = dict(manifest)
    del expected_manifest['_fingerprint']
    if json.loads(patched['package.json']) != expected_manifest:
        raise ValueError('Unexpected package metadata change')
    changed = sorted(name for name in upstream if upstream[name] != patched[name])
    if changed != sorted([FSR, 'package.json']):
        raise ValueError('Unexpected changed file set')
    patch = ''.join(difflib.unified_diff(
        upstream[FSR].decode().splitlines(keepends=True),
        patched[FSR].decode().splitlines(keepends=True),
        fromfile='a/' + FSR, tofile='b/' + FSR)).encode('utf-8')
    archive = make_archive(patched)
    provenance = {
        'schemaVersion': 1,
        'status': 'candidate-not-installed-not-shader-validated',
        'package': PACKAGE, 'version': VERSION,
        'unityEditor': '6000.3.25f1', 'unityEditorRevision': 'e1dba0a9aba4',
        'source': 'installed builtin Core package cache; pinned by full file inventory',
        'upstreamFingerprint': FINGERPRINT,
        'upstreamFiles': len(upstream), 'upstreamBytes': sum(map(len, upstream.values())),
        'upstreamInventorySha256': sha(inventory(upstream)),
        'patchedInventorySha256': sha(inventory(patched)),
        'archive': ARCHIVE, 'archiveBytes': len(archive), 'archiveSha256': sha(archive),
        'patchSha256': sha(patch),
        'changes': {name: {'beforeSha256': sha(upstream[name]),
                           'afterSha256': sha(patched[name])} for name in changed},
        'metadataChange': 'Remove generated _fingerprint; preserve name/version/unity/dependencies',
        'preserved': 'All other file bytes, including every .meta/GUID, sample, notice and C# file',
        'packing': 'sorted package/ regular files; mode 0644; uid/gid/mtime 0; empty owner names; gzip level 9, mtime 0, no filename; PAX path headers only for long names',
        'zlibVersion': zlib.ZLIB_VERSION,
    }
    outputs = {ARCHIVE: archive, 'fsr-gles3-fp32.patch': patch,
               'upstream-files.sha256.tsv': inventory(upstream),
               'patched-files.sha256.tsv': inventory(patched),
               'provenance.json': (json.dumps(provenance, indent=2) + '\n').encode('utf-8')}
    # Check every destination before writing; never overwrite different existing data.
    for name, data in outputs.items():
        target = destination / name
        if target.is_symlink():
            raise ValueError('Output must not be a symlink')
        if target.exists() and target.read_bytes() != data:
            raise ValueError('Existing output differs: ' + name)
        if args.verify and not target.exists():
            raise ValueError('Missing output: ' + name)
    if not args.verify:
        for name, data in outputs.items():
            target = destination / name
            if not target.exists():
                with target.open('xb') as stream:
                    stream.write(data)
    print(json.dumps({'mode': 'verify' if args.verify else 'pack',
                      'files': len(patched), 'changed': changed,
                      'archiveBytes': len(archive), 'archiveSha256': sha(archive)}))


if __name__ == '__main__':
    main()
