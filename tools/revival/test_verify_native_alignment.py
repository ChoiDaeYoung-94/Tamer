"""Synthetic ELF/ZIP fixtures only; no shipped SDK or private assets are embedded."""

import contextlib
import hashlib
import io
import json
import struct
import tempfile
import unittest
import warnings
import zipfile
from pathlib import Path

import verify_native_alignment as verify


def elf_fixture(segments=None, bits=64, machine=None, endian='<'):
    """Construct only ELF headers and zero padding; there is no executable code."""
    if segments is None:
        segments = [dict(offset=0, vaddr=0, align=16384)]
    if machine is None:
        machine = 183 if bits == 64 else 40
    header_size, program_size = (64, 56) if bits == 64 else (52, 32)
    ident = b'\x7fELF' + bytes((2 if bits == 64 else 1, 1 if endian == '<' else 2, 1)) + bytes(9)
    header_format = 'HHIQQQIHHHHHH' if bits == 64 else 'HHIIIIIHHHHHH'
    header = struct.pack(endian + header_format, 3, machine, 1, 0,
                         header_size, 0, 0, header_size, program_size, len(segments), 0, 0, 0)
    data = bytearray(ident + header)
    for segment in segments:
        kind = segment.get('kind', 1)
        offset, vaddr = segment.get('offset', 0), segment.get('vaddr', 0)
        filesz, memsz = segment.get('filesz', 512), segment.get('memsz', 512)
        align = segment.get('align', 16384)
        if bits == 64:
            data.extend(struct.pack(endian + 'IIQQQQQQ', kind, 5, offset, vaddr, 0, filesz, memsz, align))
        else:
            data.extend(struct.pack(endian + 'IIIIIIII', kind, offset, vaddr, 0, filesz, memsz, 5, align))
    return bytes(data).ljust(1024, b'\0')


class NativeAlignmentTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.base = Path(directory.name)
        self.apk = self.base / 'synthetic.apk'

    def make_apk(self, entries=None, compression=zipfile.ZIP_STORED, aligned=True,
                 different_central_extra=False):
        if entries is None:
            entries = [('lib/arm64-v8a/libsynthetic.so', elf_fixture())]
        with warnings.catch_warnings():
            warnings.simplefilter('ignore', UserWarning)  # Deliberate duplicate fixture.
            with zipfile.ZipFile(self.apk, 'w') as archive:
                for name, data in entries:
                    info = zipfile.ZipInfo(name)
                    info.compress_type = compression
                    if aligned and compression == zipfile.ZIP_STORED:
                        start = archive.fp.tell() + 30 + len(name.encode('utf-8'))
                        padding = (-start) % verify.PAGE_SIZE
                        if 0 < padding < 4:
                            padding += verify.PAGE_SIZE
                        if padding:
                            info.extra = struct.pack('<HH', 0xCAFE, padding - 4) + bytes(padding - 4)
                    archive.writestr(info, data)
                    if different_central_extra:
                        info.extra = b''
        return verify.inspect_apk(self.apk)

    def test_aligned_arm64_reports_hashes_all_segments_and_no_runtime_claim(self):
        data = elf_fixture([dict(offset=0, vaddr=0, align=16384),
                            dict(offset=128, vaddr=16384 + 128, align=16384)])
        report = self.make_apk([('lib/arm64-v8a/libsynthetic.so', data)])
        self.assertTrue(report['loadZipChecksPassed'], report['errors'])
        self.assertTrue(report['arm64Only'])
        self.assertFalse(report['runtime16KBVerified'])
        self.assertEqual(hashlib.sha256(self.apk.read_bytes()).hexdigest(), report['sha256'])
        self.assertEqual(self.apk.stat().st_size, report['bytes'])
        entry = report['libraries'][0]
        self.assertEqual(hashlib.sha256(data).hexdigest(), entry['sha256'])
        self.assertEqual(2, len(entry['elf']['loadSegments']))
        self.assertEqual(16384, entry['zipDataOffset'])
        self.assertTrue(entry['zipAligned16KB'])

    def test_checks_later_load_segment_not_only_first(self):
        data = elf_fixture([dict(align=16384), dict(align=4096)])
        report = self.make_apk([('lib/arm64-v8a/libsynthetic.so', data)])
        self.assertFalse(report['loadZipChecksPassed'])
        segments = report['libraries'][0]['elf']['loadSegments']
        self.assertTrue(segments[0]['passed'])
        self.assertFalse(segments[1]['passed'])

    def test_no_alignment_and_non_power_of_two_are_rejected(self):
        for align in (0, 1, 4096, 8192, 24576):
            with self.subTest(align=align):
                result = verify.inspect_elf(elf_fixture([dict(align=align)]))
                self.assertFalse(result['passed'])
                self.assertIn('p_align', result['errors'][0])

    def test_valid_64kb_alignment_is_accepted(self):
        self.assertTrue(verify.inspect_elf(elf_fixture([dict(align=65536)]))['passed'])

    def test_congruence_is_checked_against_p_align_not_just_16kb(self):
        data = elf_fixture([dict(align=65536, offset=128, vaddr=16384 + 128)])
        result = verify.inspect_elf(data)
        self.assertFalse(result['passed'])
        self.assertIn('modulo p_align', ' '.join(result['errors']))

    def test_incongruent_16kb_addresses_are_rejected(self):
        result = verify.inspect_elf(elf_fixture([dict(offset=128, vaddr=256)]))
        self.assertFalse(result['passed'])
        self.assertIn('modulo 16384', ' '.join(result['errors']))

    def test_empty_or_non_load_program_table_does_not_pass(self):
        with self.assertRaisesRegex(ValueError, 'program header table'):
            verify.inspect_elf(elf_fixture([]))
        result = verify.inspect_elf(elf_fixture([dict(kind=4)]))
        self.assertFalse(result['passed'])
        self.assertIn('no PT_LOAD', ' '.join(result['errors']))

    def test_truncated_and_non_elf_inputs_do_not_pass(self):
        for data in (b'', b'not elf', elf_fixture()[:32], elf_fixture()[:100]):
            with self.subTest(size=len(data)), self.assertRaises(ValueError):
                verify.inspect_elf(data)

    def test_extended_program_numbering_fails_explicitly(self):
        data = bytearray(elf_fixture())
        struct.pack_into('<H', data, 56, 65535)
        with self.assertRaisesRegex(ValueError, 'Extended'):
            verify.inspect_elf(data)

    def test_invalid_load_file_ranges_do_not_pass(self):
        for segment in (dict(filesz=513, memsz=512), dict(offset=1020),
                        dict(offset=2048, vaddr=2048, filesz=0, memsz=0)):
            with self.subTest(segment=segment):
                self.assertFalse(verify.inspect_elf(elf_fixture([segment]))['passed'])

    def test_elf32_and_big_endian_header_layouts_are_parsed(self):
        for bits in (32, 64):
            for endian in ('<', '>'):
                with self.subTest(bits=bits, endian=endian):
                    result = verify.inspect_elf(elf_fixture(bits=bits, endian=endian))
                    self.assertTrue(result['passed'])
                    self.assertEqual(bits, result['bits'])
                    self.assertEqual('little' if endian == '<' else 'big', result['byteOrder'])

    def test_uncompressed_unaligned_zip_fails_even_with_valid_elf(self):
        report = self.make_apk(aligned=False)
        self.assertFalse(report['loadZipChecksPassed'])
        self.assertTrue(report['libraries'][0]['elf']['passed'])
        self.assertFalse(report['libraries'][0]['zipAligned16KB'])

    def test_zip_offset_uses_local_extra_not_central_extra(self):
        report = self.make_apk(different_central_extra=True)
        with zipfile.ZipFile(self.apk) as archive:
            self.assertEqual(b'', archive.infolist()[0].extra)
        self.assertTrue(report['loadZipChecksPassed'], report['errors'])
        self.assertEqual(16384, report['libraries'][0]['zipDataOffset'])

    def test_deflated_library_has_no_zip_alignment_requirement_but_checks_elf(self):
        report = self.make_apk(compression=zipfile.ZIP_DEFLATED)
        self.assertTrue(report['loadZipChecksPassed'], report['errors'])
        entry = report['libraries'][0]
        self.assertTrue(entry['compressed'])
        self.assertFalse(entry['zipAlignmentRequired'])
        self.assertIsNone(entry['zipAligned16KB'])
        self.assertNotEqual(0, entry['zipDataOffset'] % 16384)
        bad = self.make_apk([('lib/arm64-v8a/libsynthetic.so', elf_fixture([dict(align=4096)]))],
                            compression=zipfile.ZIP_DEFLATED)
        self.assertFalse(bad['loadZipChecksPassed'])

    def test_abi_path_must_match_elf_architecture_and_byte_order(self):
        for data in (elf_fixture(machine=62), elf_fixture(bits=32), elf_fixture(endian='>')):
            with self.subTest(elf=data[:20]):
                report = self.make_apk([('lib/arm64-v8a/libsynthetic.so', data)])
                self.assertFalse(report['loadZipChecksPassed'])
                self.assertIn('does not match ABI', ' '.join(report['errors']))

    def test_checks_every_abi_and_rejects_extra_abi_by_default(self):
        report = self.make_apk([('lib/arm64-v8a/libsynthetic.so', elf_fixture()),
                                ('lib/x86_64/libsynthetic.so', elf_fixture(machine=62))])
        self.assertEqual(2, report['nativeLibraryCount'])
        self.assertFalse(report['loadZipChecksPassed'])
        self.assertFalse(report['arm64Only'])
        report = verify.inspect_apk(self.apk, ('arm64-v8a', 'x86_64'))
        self.assertTrue(report['loadZipChecksPassed'], report['errors'])

    def test_duplicate_and_malformed_native_paths_do_not_pass(self):
        name = 'lib/arm64-v8a/libsynthetic.so'
        report = self.make_apk([(name, elf_fixture()), (name, elf_fixture())])
        self.assertFalse(report['loadZipChecksPassed'])
        self.assertIn('Duplicate', ' '.join(report['errors']))
        for name in ('lib/../escape.so', 'lib/arm64-v8a/nested/escape.so',
                     'lib//libbad.so', 'lib/unknown/libbad.so'):
            with self.subTest(name=name):
                self.assertFalse(self.make_apk([(name, elf_fixture())])['loadZipChecksPassed'])

    def test_corrupted_library_crc_fails_with_json_evidence(self):
        report = self.make_apk()
        damaged = bytearray(self.apk.read_bytes())
        damaged[report['libraries'][0]['zipDataOffset'] + 1000] ^= 1
        self.apk.write_bytes(damaged)
        report = verify.inspect_apk(self.apk)
        self.assertFalse(report['loadZipChecksPassed'])
        self.assertIn('CRC', ' '.join(report['errors']))
        json.dumps(report)

    def test_missing_invalid_and_empty_archive_fail_closed(self):
        self.assertFalse(verify.inspect_apk(self.apk)['loadZipChecksPassed'])
        self.apk.write_bytes(b'not a zip')
        self.assertFalse(verify.inspect_apk(self.apk)['loadZipChecksPassed'])
        self.assertFalse(self.make_apk([])['loadZipChecksPassed'])

    def test_relro_is_evidence_separate_from_load_zip_verdict(self):
        data = elf_fixture([dict(), dict(kind=verify.PT_GNU_RELRO, vaddr=4096, memsz=4096)])
        report = self.make_apk([('lib/arm64-v8a/libsynthetic.so', data)])
        self.assertTrue(report['loadZipChecksPassed'])
        self.assertFalse(report['relroChecksPassed'])
        self.assertEqual(1, report['relroEndAlignmentFailureCount'])
        self.assertFalse(report['libraries'][0]['elf']['relroSegments'][0]['endAligned16KB'])
        self.assertTrue(any('GNU_RELRO' in note for note in report['limitations']))

    def test_whole_load_relro_layout_does_not_hide_failed_guide_modulo_check(self):
        # Like the observed Unity libraries: RELRO ends with its LOAD, so no
        # writable tail lies within that LOAD. Keep the stricter guide result
        # visible without mislabelling it as a proven runtime crash.
        data = elf_fixture([dict(memsz=4096), dict(kind=verify.PT_GNU_RELRO, memsz=4096)])
        report = self.make_apk([('lib/arm64-v8a/libsynthetic.so', data)])
        relro = report['libraries'][0]['elf']['relroSegments'][0]
        self.assertTrue(report['loadZipChecksPassed'])
        self.assertFalse(report['relroChecksPassed'])
        self.assertTrue(relro['matchesWholeLoadSegment'])
        self.assertEqual(4096, relro['endRemainder16KB'])
        self.assertFalse(report['runtime16KBVerified'])
        self.assertNotIn('staticChecksPassed', report)

    def test_relro_prefix_alignment_is_reported_independently_of_whole_load(self):
        for relro_size, expected in ((16384, True), (4096, False)):
            with self.subTest(relro_size=relro_size):
                data = elf_fixture([dict(memsz=32768),
                                    dict(kind=verify.PT_GNU_RELRO, memsz=relro_size)])
                report = self.make_apk([('lib/arm64-v8a/libsynthetic.so', data)])
                self.assertTrue(report['loadZipChecksPassed'])
                self.assertEqual(expected, report['relroChecksPassed'])
                self.assertFalse(report['libraries'][0]['elf']['relroSegments'][0]['matchesWholeLoadSegment'])

    def test_missing_relro_is_not_a_relro_pass(self):
        report = self.make_apk()
        self.assertTrue(report['loadZipChecksPassed'])
        self.assertFalse(report['relroChecksPassed'])
        self.assertEqual(0, report['relroSegmentCount'])
        self.assertIn('No PT_GNU_RELRO', ' '.join(report['relroErrors']))

    def test_strict_cli_fails_relro_but_default_exit_remains_load_zip_only(self):
        data = elf_fixture([dict(memsz=4096), dict(kind=verify.PT_GNU_RELRO, memsz=4096)])
        self.make_apk([('lib/arm64-v8a/libsynthetic.so', data)])
        output = self.base / 'relro.json'
        arguments = ['--apk', str(self.apk), '--output', str(output)]
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, verify.main(arguments))
            self.assertEqual(1, verify.main(arguments + ['--strict-relro']))
        report = json.loads(output.read_text())
        self.assertTrue(report['strictRelro'])
        self.assertTrue(report['loadZipChecksPassed'])
        self.assertFalse(report['relroChecksPassed'])

        aligned = elf_fixture([dict(memsz=16384), dict(kind=verify.PT_GNU_RELRO, memsz=16384)])
        self.make_apk([('lib/arm64-v8a/libsynthetic.so', aligned)])
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, verify.main(arguments + ['--strict-relro']))
        self.make_apk([('lib/arm64-v8a/libsynthetic.so', aligned)], aligned=False)
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, verify.main(arguments + ['--strict-relro']))
        report = json.loads(output.read_text())
        self.assertTrue(report['relroChecksPassed'])
        self.assertFalse(report['loadZipChecksPassed'])

    def test_cli_writes_machine_readable_evidence_and_never_modifies_apk(self):
        self.make_apk()
        original = self.apk.read_bytes()
        output = self.base / 'evidence' / 'native.json'
        stdout = io.StringIO()
        with contextlib.redirect_stdout(stdout):
            status = verify.main(['--apk', str(self.apk), '--output', str(output)])
        self.assertEqual(0, status)
        self.assertEqual(json.loads(stdout.getvalue()), json.loads(output.read_text()))
        self.assertEqual(original, self.apk.read_bytes())
        self.make_apk(aligned=False)
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, verify.main(['--apk', str(self.apk), '--output', str(output)]))
        self.assertFalse(json.loads(output.read_text())['loadZipChecksPassed'])

    def test_cli_rejects_output_overwriting_apk(self):
        self.make_apk()
        original = self.apk.read_bytes()
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            verify.main(['--apk', str(self.apk), '--output', str(self.apk)])
        self.assertEqual(original, self.apk.read_bytes())


if __name__ == '__main__':
    unittest.main()
