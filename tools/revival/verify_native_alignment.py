"""Check APK native ELF/ZIP alignment without extracting or modifying the APK.

This is static evidence, not proof of 16 KB device execution or store acceptance.
Compressed libraries require extraction; their ZIP data offset is not a mmap
alignment requirement. Manifest extraction settings and AAB splits are not checked.
--strict-relro additionally requires RELRO presence and 16 KB end alignment per
the Android guide. A failed end-modulo check alone does not prove a runtime crash.

References:
https://developer.android.com/guide/practices/page-sizes
https://developer.android.com/tools/zipalign
https://refspecs.linuxfoundation.org/elf/gabi4+/ch5.pheader.html
https://android.googlesource.com/platform/bionic/+/android16-qpr2-release/linker/linker_phdr_16kib_compat.cpp
"""

import argparse
import hashlib
import json
import struct
import zipfile
import zlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PAGE_SIZE = 16384
PT_LOAD = 1
PT_GNU_RELRO = 0x6474E552
# ELF class, machine: Android native ABIs (all are little endian).
ABI_ELF = {'arm64-v8a': (64, 183), 'armeabi-v7a': (32, 40),
           'x86_64': (64, 62), 'x86': (32, 3)}


def inspect_elf(data):
    """Return every LOAD segment; reject truncated or unsupported ELF headers."""
    if len(data) < 16 or data[:4] != b'\x7fELF':
        raise ValueError('Not an ELF file')
    elf_class, encoding, version = data[4:7]
    if elf_class not in (1, 2) or encoding not in (1, 2) or version != 1:
        raise ValueError('Unsupported ELF class, byte order or version')
    endian = '<' if encoding == 1 else '>'
    header = struct.Struct(endian + ('HHIIIIIHHHHHH' if elf_class == 1 else 'HHIQQQIHHHHHH'))
    if len(data) < 16 + header.size:
        raise ValueError('Truncated ELF header')
    (elf_type, machine, elf_version, _, phoff, _, _, ehsize, phentsize,
     phnum, _, _, _) = header.unpack_from(data, 16)
    if elf_type != 3 or elf_version != 1:
        raise ValueError('Expected an ELF shared object (ET_DYN), version 1')
    if ehsize < 16 + header.size or ehsize > len(data):
        raise ValueError('Invalid ELF header size')
    program = struct.Struct(endian + ('IIIIIIII' if elf_class == 1 else 'IIQQQQQQ'))
    if phnum == 0xFFFF:
        raise ValueError('Extended ELF program header numbering is unsupported')
    if not phnum or phoff < ehsize or phentsize < program.size:
        raise ValueError('Missing or invalid ELF program header table')
    if phoff + phentsize * phnum > len(data):
        raise ValueError('Truncated ELF program header table')
    result = dict(bits=32 if elf_class == 1 else 64,
                  byteOrder='little' if encoding == 1 else 'big', machine=machine,
                  loadSegments=[], relroSegments=[], errors=[])
    for index in range(phnum):
        fields = program.unpack_from(data, phoff + index * phentsize)
        if elf_class == 1:
            kind, offset, vaddr, _, filesz, memsz, _, align = fields
        else:
            kind, _, offset, vaddr, _, filesz, memsz, align = fields
        if kind == PT_GNU_RELRO:
            result['relroSegments'].append(dict(index=index, virtualAddress=vaddr,
                memorySize=memsz, endAddress=vaddr + memsz,
                endAligned16KB=(vaddr + memsz) % PAGE_SIZE == 0))
        if kind != PT_LOAD:
            continue
        errors = []
        valid_alignment = align >= PAGE_SIZE and align & (align - 1) == 0
        congruent = align > 0 and offset % align == vaddr % align
        if not valid_alignment:
            errors.append('p_align must be a power of two at least 16384')
        if not congruent:
            errors.append('p_offset and p_vaddr are not congruent modulo p_align')
        if offset % PAGE_SIZE != vaddr % PAGE_SIZE:
            errors.append('p_offset and p_vaddr are not congruent modulo 16384')
        if filesz > memsz:
            errors.append('p_filesz exceeds p_memsz')
        if offset > len(data) or filesz > len(data) - offset:
            errors.append('PT_LOAD file range extends beyond ELF data')
        result['loadSegments'].append(dict(index=index, offset=offset,
            virtualAddress=vaddr, fileSize=filesz, memorySize=memsz,
            alignment=align, congruent=congruent, passed=not errors, errors=errors))
        result['errors'].extend('PT_LOAD {}: {}'.format(index, error) for error in errors)
    if not result['loadSegments']:
        result['errors'].append('ELF has no PT_LOAD segments')
    result['passed'] = not result['errors']
    # Preserve the guide's simple end-address test separately from LOAD checks.
    # Bionic also considers layout: a whole LOAD covered by RELRO does not have
    # a writable tail that end rounding could accidentally make read-only.
    result['relroErrors'] = []
    for relro in result['relroSegments']:
        relro['endRemainder16KB'] = relro['endAddress'] % PAGE_SIZE
        relro['matchesWholeLoadSegment'] = any(
            segment['virtualAddress'] == relro['virtualAddress'] and
            segment['memorySize'] == relro['memorySize']
            for segment in result['loadSegments'])
        if not relro['endAligned16KB']:
            result['relroErrors'].append('PT_GNU_RELRO {}: end address fails guide modulo-16384 check'
                                         .format(relro['index']))
    if not result['relroSegments']:
        result['relroErrors'].append('No PT_GNU_RELRO segment; RELRO protection is not evidenced')
    result['relroChecksPassed'] = not result['relroErrors']
    return result


def zip_data_offset(stream, info, apk_size):
    """Use local header lengths; the central-directory extra field may differ."""
    stream.seek(info.header_offset)
    header = stream.read(30)
    if len(header) != 30 or header[:4] != b'PK\x03\x04':
        raise ValueError('Missing or truncated ZIP local header')
    fields = struct.unpack('<4s5H3I2H', header)
    if fields[3] != info.compress_type:
        raise ValueError('ZIP local/central compression methods differ')
    if fields[2] != info.flag_bits:
        raise ValueError('ZIP local/central flags differ')
    offset = info.header_offset + 30 + fields[-2] + fields[-1]
    if offset > apk_size or info.compress_size > apk_size - offset:
        raise ValueError('ZIP native data extends beyond APK')
    return offset


def inspect_apk(apk, expected_abis=('arm64-v8a',), strict_relro=False):
    """Collect failures instead of accepting empty or partially inspected APKs."""
    apk = Path(apk).resolve()
    apk_label = apk.relative_to(ROOT).as_posix() if apk.is_relative_to(ROOT) else str(apk)
    report = dict(schemaVersion=2, apk=apk_label, bytes=None, sha256=None,
        pageSize=PAGE_SIZE, expectedAbis=sorted(set(expected_abis)), abis=[],
        arm64Only=False, libraries=[], errors=[], loadZipChecksPassed=False,
        relroChecksPassed=False, relroErrors=[], strictRelro=strict_relro,
        relroCheckScope='RELRO presence and (virtualAddress + memorySize) modulo 16384',
        runtime16KBVerified=False,
        limitations=[
            'loadZipChecksPassed covers LOAD alignment, ZIP native packaging, ABI and inspected structure only.',
            'Compressed native libraries require extraction; manifest extraction settings are not checked.',
            'relroChecksPassed is the separate Android guide GNU_RELRO presence/end-modulo check; false means that additional guide check did not pass.',
            'A nonzero RELRO end remainder alone does not prove a runtime blocker: Bionic treats whole-LOAD RELRO differently from a prefix with a writable tail.',
            'RELRO layout fields are evidence, not a complete linker simulation; no device execution or store approval is established.',
            'AAB configuration, generated split APKs and native libraries outside lib/ are not checked.',
            'APK signing, non-native ZIP entry alignment and runtime page-size assumptions are not checked.'
        ],
        sources=['https://developer.android.com/guide/practices/page-sizes',
                 'https://developer.android.com/tools/zipalign',
                 'https://refspecs.linuxfoundation.org/elf/gabi4+/ch5.pheader.html',
                 'https://android.googlesource.com/platform/bionic/+/android16-qpr2-release/linker/linker_phdr_16kib_compat.cpp'])
    try:
        with apk.open('rb') as stream:
            report['sha256'] = hashlib.file_digest(stream, 'sha256').hexdigest()
            report['bytes'] = stream.seek(0, 2)
            with zipfile.ZipFile(stream) as archive:
                names = set()
                for info in archive.infolist():
                    if not info.filename.startswith('lib/') or not info.filename.endswith('.so'):
                        continue
                    entry = dict(path=info.filename, abi=None, bytes=info.file_size,
                        sha256=None, compressionMethod=info.compress_type,
                        compressed=info.compress_type != zipfile.ZIP_STORED,
                        zipLocalHeaderOffset=info.header_offset, zipDataOffset=None,
                        zipAlignmentRequired=info.compress_type == zipfile.ZIP_STORED,
                        zipAligned16KB=None, elf=None, errors=[], loadZipChecksPassed=False,
                        relroChecksPassed=False, relroErrors=[])
                    report['libraries'].append(entry)
                    parts = info.filename.split('/')
                    if (len(parts) != 3 or any(part in ('', '.', '..') for part in parts)
                            or '\\' in info.filename or '\x00' in info.orig_filename):
                        entry['errors'].append('Expected ZIP path lib/<abi>/<name>.so')
                    else:
                        entry['abi'] = parts[1]
                    if info.filename in names:
                        entry['errors'].append('Duplicate native ZIP path')
                    names.add(info.filename)
                    if info.compress_type not in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED):
                        entry['errors'].append('Unsupported APK native compression method')
                    try:
                        entry['zipDataOffset'] = zip_data_offset(stream, info, report['bytes'])
                        if entry['zipAlignmentRequired']:
                            entry['zipAligned16KB'] = entry['zipDataOffset'] % PAGE_SIZE == 0
                            if not entry['zipAligned16KB']:
                                entry['errors'].append('Uncompressed native ZIP data is not 16 KB aligned')
                        data = archive.read(info)  # Also verifies CRC and local/central names.
                        entry['sha256'] = hashlib.sha256(data).hexdigest()
                        entry['elf'] = inspect_elf(data)
                        entry['errors'].extend(entry['elf']['errors'])
                        expected = ABI_ELF.get(entry['abi'])
                        if expected is None:
                            entry['errors'].append('Unknown Android ABI path')
                        elif ((entry['elf']['bits'], entry['elf']['machine']) != expected
                              or entry['elf']['byteOrder'] != 'little'):
                            entry['errors'].append('ELF class, machine or byte order does not match ABI path')
                    except (ValueError, OSError, RuntimeError, NotImplementedError,
                            zipfile.BadZipFile, zlib.error) as error:
                        entry['errors'].append(str(error))
                    entry['loadZipChecksPassed'] = not entry['errors']
                    if entry['elf'] is None:
                        entry['relroErrors'].append('ELF was not inspected; RELRO cannot be verified')
                    else:
                        entry['relroErrors'] = entry['elf']['relroErrors']
                        entry['relroChecksPassed'] = entry['elf']['relroChecksPassed']
                    report['errors'].extend('{}: {}'.format(info.filename, error)
                                            for error in entry['errors'])
                    report['relroErrors'].extend('{}: {}'.format(info.filename, error)
                                                 for error in entry['relroErrors'])
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        report['errors'].append(str(error))
        report['relroErrors'].append('APK inspection did not complete; RELRO cannot be verified')
    report['abis'] = sorted({entry['abi'] for entry in report['libraries'] if entry['abi']})
    report['arm64Only'] = report['abis'] == ['arm64-v8a']
    if not report['libraries']:
        report['errors'].append('No native libraries found in lib/; native check cannot pass')
        report['relroErrors'].append('No native libraries inspected; RELRO cannot be verified')
    if report['abis'] != report['expectedAbis']:
        report['errors'].append('APK ABIs do not match expected ABIs')
    report['nativeLibraryCount'] = len(report['libraries'])
    report['loadZipChecksPassed'] = not report['errors']
    report['relroChecksPassed'] = not report['relroErrors']
    report['compressedNativeLibraryCount'] = sum(entry['compressed'] for entry in report['libraries'])
    relros = [segment for entry in report['libraries'] if entry['elf'] is not None
              for segment in entry['elf']['relroSegments']]
    report['relroSegmentCount'] = len(relros)
    report['relroEndAlignmentFailureCount'] = sum(not segment['endAligned16KB'] for segment in relros)
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apk', type=Path, default=ROOT / 'Build/revival/Tamer-development.apk')
    parser.add_argument('--output', type=Path, default=ROOT / 'Logs/revival/native-alignment.json')
    parser.add_argument('--expected-abi', action='append', choices=sorted(ABI_ELF),
                        help='Expected ABI; repeat for multiple ABIs. Default: arm64-v8a only.')
    parser.add_argument('--strict-relro', action='store_true',
                        help='Also fail exit status if the separate RELRO guide check fails.')
    args = parser.parse_args(argv)
    if args.apk.resolve() == args.output.resolve():
        parser.error('--output must not overwrite the APK')
    report = inspect_apk(args.apk, args.expected_abi or ('arm64-v8a',), args.strict_relro)
    encoded = json.dumps(report, indent=2, ensure_ascii=True) + '\n'
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(encoded, encoding='utf-8')
    print(encoded, end='')
    passed = report['loadZipChecksPassed'] and (not args.strict_relro or report['relroChecksPassed'])
    return 0 if passed else 1


if __name__ == '__main__':
    raise SystemExit(main())
