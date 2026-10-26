"""Read-only legacy AAB input snapshot; never builds, repairs, signs or approves."""
import argparse
from contextlib import ExitStack
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import stat
from urllib.parse import unquote_plus

from windows_owned_files import LockedFile, locked_directories


INT_MAX = 2_147_483_647
ANDROID_MAX = 2_100_000_000
BUILD_SCRIPT = 'Assets/Scripts/Editor/BuildScript.cs'
OUTPUT_PLAN = 'Assets/Scripts/Editor/LegacyBuildOutputPlan.cs'
BUILD_INFO = 'BuildInfo/buildinfo.txt'
FINISH = 'BuildInfo/finishversionsetting.txt'
PLAYFAB = 'Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset'
PGS_TEXT = 'ProjectSettings/GooglePlayGameSettings.txt'
PGS_ASSET = 'Assets/GooglePlayGames/Resources/PlayGamesSettings.asset'
INPUTS = (BUILD_SCRIPT, OUTPUT_PLAN, BUILD_INFO, FINISH, PLAYFAB, PGS_TEXT, PGS_ASSET)
# Pin reviewed semantics, not OS newline representation. Any C# change requires
# reviewing this calculator and updating the pins, rather than guessing behavior.
SOURCE_PINS = {
    BUILD_SCRIPT: 'f1d74cc0913ccf268413c5dbcf8ce10ac07121c4b33fff1dffa93db8cd17612b',
    OUTPUT_PLAN: '47ffe454ef1517fb033c6f0f4e53fba1817cbf5709a81fc418f6bbec3388137b',
}
UNVERIFIED = [
    'Published maximum is an operator-supplied observation, not a Console lookup.',
    'Source release gates, approval and credentials/signing are separate.',
    'Unity import, Resources identity, OAuth/server mapping and account continuity are unverified.',
    'Actual signed AAB, certificate, manifest, 16KB/device and store delivery checks are separate.',
    'Snapshot locks end before this command returns; this is not a reservation for a later Unity build.',
]


class PreflightError(ValueError):
    """Only fixed, non-sensitive issue codes may reach a report or the console."""


def absolute_local(value):
    path = Path(value)
    if (not path.is_absolute() or '..' in path.parts or str(path).startswith('\\\\')
            or any(part.endswith((' ', '.')) or ':' in part
                   for part in path.parts[1:])):
        raise PreflightError('absolute_local_path_required')
    return path


def attributes(path):
    try:
        return path.lstat()
    except FileNotFoundError:
        return None


def regular(info):
    return (info is not None and stat.S_ISREG(info.st_mode)
            and not getattr(info, 'st_file_attributes', 0) & 0x400
            and info.st_nlink == 1)


def require_absent(path):
    if attributes(path) is not None:
        raise PreflightError('output_exists_or_is_link')


def existing_parent(path):
    """Find a lockable ancestor without resolving a missing build output path."""
    parent = path.parent
    while attributes(parent) is None:
        if parent == parent.parent:
            raise PreflightError('output_parent_missing')
        parent = parent.parent
    return parent


def require_directory_chain(directory):
    # Inspect the supplied spelling before any resolver can hide a reparse point.
    for parent in (*reversed(directory.parents), directory):
        info = attributes(parent)
        if info is not None and (not stat.S_ISDIR(info.st_mode)
                or getattr(info, 'st_file_attributes', 0) & 0x400
                or stat.S_ISLNK(info.st_mode)):
            raise PreflightError('output_ancestor_not_regular_directory')


def require_output_ancestors(path):
    require_directory_chain(path.parent)


def canonical_directory(path):
    # Existing directories only: Windows realpath uses the final OS path and
    # expands DOS short names / drive aliases. Never use it to skip the original
    # ancestor checks; callers repeat those checks under directory locks.
    return Path(os.path.realpath(path, strict=True))


def validate_report_path(root, output):
    if output == root:
        raise PreflightError('report_cannot_be_project_root')
    if output in [root / relative for relative in INPUTS]:
        raise PreflightError('report_collides_with_input')
    # Reports may live under checkout/Logs or in a separate private directory.
    # Do not stage a new report into the project's source/settings/build inputs.
    if output.is_relative_to(root) and output.relative_to(root).parts[0].casefold() != 'logs':
        raise PreflightError('report_inside_project_requires_logs_directory')
    if not output.name.endswith('.private.json'):
        raise PreflightError('private_report_filename_required')
    require_output_ancestors(output)
    require_directory_chain(root)
    require_absent(output)
    if attributes(output.parent) is None:
        raise PreflightError('report_parent_must_already_exist')
    canonical_root = canonical_directory(root)
    canonical_output = canonical_directory(output.parent) / output.name
    if canonical_output.is_relative_to(canonical_root):
        parts = canonical_output.relative_to(canonical_root).parts
        if not parts or parts[0].casefold() != 'logs':
            raise PreflightError('report_inside_project_requires_logs_directory')


def decode(raw):
    try:
        return raw.decode('utf-8-sig')
    except UnicodeError:
        raise PreflightError('invalid_input_encoding') from None


def source_contract(blobs):
    for relative, pin in SOURCE_PINS.items():
        normalized = decode(blobs[relative]).replace('\r\n', '\n')
        if hashlib.sha256(normalized.encode('utf-8')).hexdigest() != pin:
            raise PreflightError('legacy_source_contract_changed')
    source = decode(blobs[BUILD_SCRIPT])
    expected = {'VERSION': '1.0.', 'DAY_CALCULATEVERSION': '01/21/2023 00:00:00',
                'BUILDINFO_PATH': BUILD_INFO, 'BUILDINFO_FINISHVERSIONSETTING': FINISH,
                'AOS_BUILD_PATH': 'Build/AOS'}
    for key, value in expected.items():
        found = re.findall(r'private const string ' + key + r' = "([^"\r\n]*)";', source)
        if found != [value]:
            raise PreflightError('legacy_source_contract_changed')


def playfab_title_matches(raw, expected):
    """Extract only TitleId. Other serialized fields are never interpreted."""
    text = decode(raw)
    field_lines = re.findall(r'^[ \t]*TitleId[ \t]*:[^\r\n]*\r?$', text, re.M)
    titles = re.findall(r'^  TitleId: ([0-9A-Fa-f]+)\r?$', text, re.M)
    return len(field_lines) == 1 and len(titles) == 1 and titles[0] == expected


def yaml_string(text, name):
    fields = re.findall(r'^[ \t]*' + re.escape(name) + r'[ \t]*:[^\r\n]*\r?$', text, re.M)
    values = re.findall(r'^  ' + re.escape(name) + r':[ \t]*([^\r\n]*)\r?$', text, re.M)
    if len(fields) != 1 or len(values) != 1:
        raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
    value = values[0].strip()
    if value.startswith('"'):
        try:
            value = json.loads(value)
        except ValueError:
            raise PreflightError('pgs_fields_missing_duplicate_or_malformed') from None
    elif value.startswith("'"):
        if not value.endswith("'") or re.search(r"(?<!')'(?!')", value[1:-1]):
            raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
        value = value[1:-1].replace("''", "'")
    elif any(char in value for char in '#{}[]!&*|>\"\''):
        raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
    if not isinstance(value, str) or not value.strip() or any(ord(c) < 32 for c in value):
        raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
    return value


def pgs_matches(settings_raw, asset_raw):
    allowlist = {'proj.AppId', 'and.ClientId', 'android.SetupDone'}
    values = {}
    text = decode(settings_raw)
    # StreamReader.ReadLine recognizes CRLF, CR and LF, not splitlines()'s VT,
    # FF, NEL or Unicode separators. Accept only ASCII layout whitespace, rather
    # than normalizing Unicode whitespace into an allowlisted field key.
    if any((ord(c) < 32 and c not in '\t\r\n') or 0x7f <= ord(c) <= 0x9f
           or (c.isspace() and c not in ' \t\r\n') for c in text):
        raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
    for line in re.split(r'\r\n|\r|\n', text):
        if not line.strip(' \t'):
            break  # The plugin does not read any later fields.
        key, separator, value = line.strip(' \t').partition('=')
        key = key.strip(' \t')
        if key not in allowlist:
            continue
        if not separator or key in values:
            raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
        value = value.strip(' \t')
        if re.search(r'%(?![0-9A-Fa-f]{2})', value):
            raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
        try:
            values[key] = unquote_plus(value, encoding='utf-8', errors='strict')
        except UnicodeError:
            raise PreflightError('pgs_fields_missing_duplicate_or_malformed') from None
        if (not values[key].strip() or any(ord(c) < 32 or 0x7f <= ord(c) <= 0x9f
                                          or c in '\u2028\u2029' for c in values[key])):
            raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
    if set(values) != allowlist or values['android.SetupDone'] != 'true':
        raise PreflightError('pgs_fields_missing_duplicate_or_malformed')
    asset = decode(asset_raw)
    return (values['proj.AppId'] == yaml_string(asset, 'mAppId')
            and values['and.ClientId'] == yaml_string(asset, 'mWebClientId'))


def legacy_aab_plan(raw, finished, now, published_max, allow_finish_reuse=False):
    """Reviewed nonnegative subset of LegacyBuildOutputPlan.Capture, AAB only."""
    if type(published_max) is not int or not 0 <= published_max <= ANDROID_MAX:
        raise PreflightError('published_maximum_out_of_range')
    found = re.fullmatch(r'([0-9]+),([0-9]+),([0-9]+)(?:\r?\n)?', decode(raw))
    if found is None or any(len(value) > 10 for value in found.groups()):
        raise PreflightError('buildinfo_requires_three_nonnegative_int32_values')
    original = found.groups()
    previous_week, previous_build, previous_code = map(int, original)
    if max(previous_week, previous_build, previous_code) > INT_MAX:
        raise PreflightError('buildinfo_int32_overflow')
    if previous_code > ANDROID_MAX:
        raise PreflightError('android_version_code_out_of_range')
    if now.tzinfo is None or now.utcoffset() is None:
        raise PreflightError('aware_local_planning_time_required')
    local = now.replace(tzinfo=None)
    epoch = datetime(2023, 1, 21)
    if local < epoch:
        raise PreflightError('planning_time_before_epoch')
    current_week = (local - epoch).days // 7
    if finished:
        week_text, build_text = original[:2]
        code = previous_code
    else:
        build = previous_build + 1 if previous_week == current_week else 0
        code = previous_code + 1
        if build > INT_MAX or code > INT_MAX:
            raise PreflightError('next_version_int32_overflow')
        week_text, build_text = str(current_week), str(build)
    if not 1 <= code <= ANDROID_MAX:
        raise PreflightError('android_version_code_out_of_range')
    return {'previousWeek': previous_week, 'previousBuild': previous_build,
            'previousVersionCode': previous_code, 'currentCalculatedWeek': current_week,
            'reusesVersion': finished, 'finishReuseAcknowledged': allow_finish_reuse,
            'finishReuseConditionSatisfied': finished == allow_finish_reuse,
            'candidateVersionCode': code, 'publishedMaximumObserved': published_max,
            'versionExceedsPublishedMaximum': code > published_max,
            'versionName': '1.0.' + week_text,
            'fileName': '1.0.' + week_text + '.' + build_text + '.aab'}


def capture_local_time():
    # Capture once. Calculations, filename, report and boundary tests use this value.
    return datetime.now().astimezone()


def validate_arguments(expected_title, published_max, source_head):
    if not re.fullmatch(r'[0-9A-Fa-f]+', expected_title):
        raise PreflightError('expected_playfab_title_requires_hex_identifier')
    if type(published_max) is not int or not 0 <= published_max <= ANDROID_MAX:
        raise PreflightError('published_maximum_out_of_range')
    if source_head is not None and not re.fullmatch('[0-9a-f]{40}', source_head):
        raise PreflightError('source_head_requires_full_lowercase_sha')


def audit_project(root, output, expected_title, published_max, *, allow_finish_reuse=False,
                  source_head=None, now=None):
    """Inspect only the seven allowlisted inputs, using a short Windows snapshot lock."""
    validate_arguments(expected_title, published_max, source_head)
    root, output = absolute_local(root), absolute_local(output)
    now = capture_local_time() if now is None else now
    result = {'schema': 1, 'mode': 'operating-build-inputs', 'releaseReady': False,
              'inputChecksPassed': False, 'capturedLocalTime': now.isoformat(),
              'calculation': {'localEpoch': '2023-01-21T00:00:00', 'versionPrefix': '1.0.',
                              'weekRule': 'whole local days since epoch divided by 7',
                              'timeScope': 'one captured local time; not a later Unity reservation'},
              'sourceHeadClaim': source_head, 'sourceHeadIndependentlyVerified': False,
              'checks': {}, 'issues': [], 'inputSnapshotsPrivate': {}, 'legacyAabPlan': None,
              'projectSettingsVersionCodeUsed': False, 'unverified': UNVERIFIED}
    validate_report_path(root, output)
    paths = {relative: root / relative for relative in INPUTS}
    # Every input parent is already expected to exist. No directory creation.
    with locked_directories([p.parent for p in paths.values()] + [output.parent]), ExitStack() as stack:
        validate_report_path(root, output)
        blobs, held, identities = {}, {}, set()
        finished = attributes(paths[FINISH]) is not None
        for relative, path in paths.items():
            if relative == FINISH and not finished:
                result['inputSnapshotsPrivate'][relative] = {'present': False}
                continue
            handle = stack.enter_context(LockedFile(path, deletable=False))
            info = os.fstat(handle.stream.fileno())
            if not regular(info) or handle.identity in identities:
                raise PreflightError('input_link_or_identity_alias_rejected')
            identities.add(handle.identity)
            raw = handle.bytes()
            held[relative], blobs[relative] = handle, raw
            result['inputSnapshotsPrivate'][relative] = {
                'present': True, 'identity': list(handle.identity),
                'sha256': hashlib.sha256(raw).hexdigest()}

        def check(name, operation):
            try:
                value = operation()
                passed = value is not False
                if not passed:
                    result['issues'].append(name)
            except PreflightError as error:
                passed = False
                result['issues'].append(str(error))
            result['checks'][name] = passed
            return passed

        contract_ok = check('legacySourceContractMatches', lambda: source_contract(blobs))
        check('playFabTitleMatchesExpected', lambda: playfab_title_matches(blobs[PLAYFAB], expected_title))
        check('pgsSingleConfiguredValuesMatch', lambda: pgs_matches(blobs[PGS_TEXT], blobs[PGS_ASSET]))
        if contract_ok:
            def calculate():
                result['legacyAabPlan'] = legacy_aab_plan(blobs[BUILD_INFO], finished, now,
                                                          published_max, allow_finish_reuse)
            if check('legacyAabPlanCalculated', calculate):
                plan = result['legacyAabPlan']
                check('finishReuseConditionSatisfied', lambda: plan['finishReuseConditionSatisfied'])
                check('versionExceedsPublishedMaximum', lambda: plan['versionExceedsPublishedMaximum'])
                build_output = root / 'Build/AOS' / plan['fileName']
                with locked_directories([existing_parent(build_output)]):
                    check('plannedAabOutputAbsent', lambda: (
                        require_output_ancestors(build_output), require_absent(build_output)))
        # Verify pathname identity and content while the same file handles remain held.
        for relative, handle in held.items():
            named = paths[relative].stat()
            snapshot = result['inputSnapshotsPrivate'][relative]
            if (not regular(named) or (named.st_dev, named.st_ino) != handle.identity):
                raise PreflightError('input_snapshot_changed')
            handle.verify(snapshot['identity'], snapshot['sha256'])
        if not finished and attributes(paths[FINISH]) is not None:
            raise PreflightError('finish_marker_appeared_during_snapshot')
        result['inputChecksPassed'] = bool(result['checks']) and all(result['checks'].values())
    # Release input locks before writing a report. The output parent is locked
    # again and CREATE_NEW is the final authority, including a late collision.
    with locked_directories([root, output.parent]):
        validate_report_path(root, output)
        encoded = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
        with output.open('xb') as stream:
            created = os.fstat(stream.fileno())
            named = output.stat()
            if (not regular(created) or not regular(named)
                    or (created.st_dev, created.st_ino) != (named.st_dev, named.st_ino)):
                raise PreflightError('report_identity_changed')
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
    return result


class Once(argparse.Action):
    def __call__(self, parser, namespace, values, option_string=None):
        seen = getattr(namespace, '_seen_options', set())
        if self.dest in seen:
            parser.error('duplicate option rejected')
        seen.add(self.dest)
        setattr(namespace, '_seen_options', seen)
        setattr(namespace, self.dest, True if self.nargs == 0 else values)


def published_number(value):
    if not re.fullmatch(r'0|[1-9][0-9]{0,9}', value):
        raise argparse.ArgumentTypeError('canonical nonnegative integer required')
    return int(value)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument('--project-path', required=True, action=Once)
    parser.add_argument('--expected-playfab-title', required=True, action=Once)
    parser.add_argument('--published-max-code', required=True, type=published_number, action=Once)
    parser.add_argument('--output', required=True, action=Once)
    parser.add_argument('--allow-finish-reuse', action=Once, nargs=0, default=False)
    parser.add_argument('--source-head', action=Once)
    args = parser.parse_args(argv)
    try:
        result = audit_project(args.project_path, args.output, args.expected_playfab_title,
                               args.published_max_code, allow_finish_reuse=args.allow_finish_reuse,
                               source_head=args.source_head)
    except (OSError, ValueError) as error:
        code = str(error) if isinstance(error, PreflightError) else 'guarded_input_or_report_io_failed'
        print(json.dumps({'inputChecksPassed': False, 'releaseReady': False, 'issues': [code]}))
        return 2
    # Private snapshots/identities/settings values/paths never go to stdout.
    print(json.dumps({key: result[key] for key in ('inputChecksPassed', 'releaseReady', 'issues')}))
    return 0 if result['inputChecksPassed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
