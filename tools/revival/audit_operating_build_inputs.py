"""Read-only entry-specific AAB inputs; never builds, repairs, signs or approves."""
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
# Keep the original allowlist and pins as the legacy API's defaults.
INPUTS = (BUILD_SCRIPT, OUTPUT_PLAN, BUILD_INFO, FINISH, PLAYFAB, PGS_TEXT, PGS_ASSET)
LEGACY_ENTRY = 'legacy_aab'
APPROVED_ENTRY = 'approved_adult_release'
ENTRIES = (LEGACY_ENTRY, APPROVED_ENTRY)
APPROVED_SCRIPT = 'tools/revival/private_ads_build/PrivateProductionAdsBuild.cs'
PRODUCER_SCRIPT = 'tools/revival/private_ads_producer.py'
PROJECT_SETTINGS = 'ProjectSettings/ProjectSettings.asset'
APPROVED_INPUTS = (APPROVED_SCRIPT, PRODUCER_SCRIPT, PROJECT_SETTINGS, PLAYFAB, PGS_TEXT, PGS_ASSET)
ENTRY_INPUTS = {LEGACY_ENTRY: INPUTS, APPROVED_ENTRY: APPROVED_INPUTS}
# Pin reviewed semantics, not OS newline representation. Any source change requires
# reviewing this calculator and updating the pins, rather than guessing behavior.
SOURCE_PINS = {
    BUILD_SCRIPT: 'f1d74cc0913ccf268413c5dbcf8ce10ac07121c4b33fff1dffa93db8cd17612b',
    OUTPUT_PLAN: '47ffe454ef1517fb033c6f0f4e53fba1817cbf5709a81fc418f6bbec3388137b',
}
APPROVED_SOURCE_PINS = {
    APPROVED_SCRIPT: 'b57976b1ee9b2d3ef5cf1615b10e85f9548d693a4d44f750d1bacd5d89aab5e0',
    PRODUCER_SCRIPT: '163f189f9f7251bc49caa470844e12063b4bb2f29bf8fca8ab63e9b32aa2f68b',
}
ENTRY_SOURCE_PINS = {LEGACY_ENTRY: SOURCE_PINS, APPROVED_ENTRY: APPROVED_SOURCE_PINS}
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
    if output in [root / relative for inputs in ENTRY_INPUTS.values() for relative in inputs]:
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


def source_contract(blobs, entry=LEGACY_ENTRY):
    issue = 'legacy_source_contract_changed' if entry == LEGACY_ENTRY else 'approved_source_contract_changed'
    for relative, pin in ENTRY_SOURCE_PINS[entry].items():
        normalized = decode(blobs[relative]).replace('\r\n', '\n').replace('\r', '\n')
        if hashlib.sha256(normalized.encode('utf-8')).hexdigest() != pin:
            raise PreflightError(issue)
    if entry != LEGACY_ENTRY:
        return
    source = decode(blobs[BUILD_SCRIPT])
    expected = {'VERSION': '1.0.', 'DAY_CALCULATEVERSION': '01/21/2023 00:00:00',
                'BUILDINFO_PATH': BUILD_INFO, 'BUILDINFO_FINISHVERSIONSETTING': FINISH,
                'AOS_BUILD_PATH': 'Build/AOS'}
    for key, value in expected.items():
        found = re.findall(r'private const string ' + key + r' = "([^"\r\n]*)";', source)
        if found != [value]:
            raise PreflightError('legacy_source_contract_changed')


def single_line_mapping(text, name, issue):
    """Select one plain Unity mapping key at indent two, never shorten its scalar.

    This is a narrow lexical guard, not a YAML parser or Unity import. Outside
    the selected line only key/layout shape is checked; other values are never
    extracted or decoded. The match preserves the separator and raw value.
    """
    text = text.replace('\r\n', '\n').replace('\r', '\n')
    # Unsupported keys at this peer level could hide a duplicate, including an
    # escaped quoted spelling or an alias. Do not interpret them to guess a key.
    # Deeper content may belong to a non-target value, so leave it uninterpreted.
    if re.search(r'^  (?:[?&*!]|["\'][^\n]*:)', text, re.M):
        raise PreflightError(issue)
    key = re.escape(name)
    occurrences = re.findall(r'^[ \t]*' + key + r'[ \t]*:[^\n]*$', text, re.M)
    matches = list(re.finditer(r'^  ' + key + r':(?P<separator>[ \t]+)(?P<value>[^\n]*)$', text, re.M))
    if len(occurrences) != 1 or len(matches) != 1:
        raise PreflightError(issue)
    selected = matches[0]
    # Comments and blank lines do not end a plain multiline scalar. Its next
    # significant line must be an ordinary peer mapping, or there must be EOF.
    for following in text[selected.end():].split('\n'):
        if not following.strip(' \t') or following.lstrip(' \t').startswith('#'):
            continue
        if not re.match(r'^  [A-Za-z_][A-Za-z0-9_]*:(?:[ \t]|$)', following):
            raise PreflightError(issue)
        break
    return selected


def playfab_title_matches(raw, expected):
    """Extract only TitleId. Other serialized fields are never interpreted."""
    text = decode(raw)
    try:
        field = single_line_mapping(text, 'TitleId', 'playfab_title_field_malformed')
    except PreflightError:
        return False
    value = field.group('value')
    # Preserve the existing exact, unquoted TitleId spelling and one-space rule.
    return (field.group('separator') == ' ' and re.fullmatch(r'[0-9A-Fa-f]+', value) is not None
            and value == expected)


def yaml_string(text, name, issue='pgs_fields_missing_duplicate_or_malformed'):
    value = single_line_mapping(text, name, issue).group('value').strip(' \t')
    if value.startswith('"'):
        try:
            value = json.loads(value)
        except ValueError:
            raise PreflightError(issue) from None
    elif value.startswith("'"):
        if not value.endswith("'") or re.search(r"(?<!')'(?!')", value[1:-1]):
            raise PreflightError(issue)
        value = value[1:-1].replace("''", "'")
    elif any(char in value for char in '#{}[]!&*|>\"\''):
        raise PreflightError(issue)
    if not isinstance(value, str) or not value.strip() or any(ord(c) < 32 for c in value):
        raise PreflightError(issue)
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


def approved_adult_release_plan(raw, published_max):
    """Read the current Unity version; the reviewed producer does not increment it.

    This is a narrow Unity YAML subset, not a general YAML parser. Never inspect
    serialized signing fields or infer a producer run/output from these values.
    """
    if type(published_max) is not int or not 0 <= published_max <= ANDROID_MAX:
        raise PreflightError('published_maximum_out_of_range')
    issue = 'project_settings_version_fields_missing_duplicate_or_malformed'
    text = decode(raw).replace('\r\n', '\n').replace('\r', '\n')
    if any((ord(c) < 32 and c not in '\t\n') or 0x7f <= ord(c) <= 0x9f
           or c in '\u2028\u2029' for c in text):
        raise PreflightError(issue)
    # Reject duplicate/quoted/misindented keys, including keys outside this root.
    roots = re.findall(r'^[ \t]*(?:PlayerSettings|"PlayerSettings"|\'PlayerSettings\')[ \t]*:[^\n]*$', text, re.M)
    if roots != ['PlayerSettings:']:
        raise PreflightError(issue)
    heading = re.search(r'^PlayerSettings:\n', text, re.M)
    if heading is None:
        raise PreflightError(issue)
    block = text[heading.end():]
    if re.search(r'^[^ \t\n#]', block, re.M):
        raise PreflightError(issue)
    fields = {}
    for name in ('bundleVersion', 'AndroidBundleVersionCode'):
        value = single_line_mapping(text, name, issue)
        if value.start() < heading.end():
            raise PreflightError(issue)
        fields[name] = value.group('value').strip(' \t')
    version = yaml_string(text, 'bundleVersion', issue)
    spelling = fields['bundleVersion']
    if (spelling != spelling.strip() or version != version.strip()
            or any(0x7f <= ord(c) <= 0x9f or 0xd800 <= ord(c) <= 0xdfff
                                         or c in '\u2028\u2029' for c in version)
            or (not spelling.startswith(('"', "'")) and
                (':' in spelling or spelling.startswith(('@', '`', '%', '?', ',', '- '))
                 or spelling.casefold() in ('null', '~', 'true', 'false')))):
        raise PreflightError(issue)
    code_text = fields['AndroidBundleVersionCode']
    if not re.fullmatch(r'0|[1-9][0-9]{0,9}', code_text):
        raise PreflightError(issue)
    code = int(code_text)
    if not 1 <= code <= ANDROID_MAX:
        raise PreflightError('android_version_code_out_of_range')
    return {'versionName': version, 'candidateVersionCode': code,
            'publishedMaximumObserved': published_max,
            'versionExceedsPublishedMaximum': code > published_max,
            'versionSource': PROJECT_SETTINGS, 'incrementsVersionCode': False,
            'outputPathSource': 'reviewed producer plan run + artifact_name(mode)',
            'plannedOutputPathKnown': False, 'plannedAabOutputAbsent': None}


def validate_arguments(expected_title, published_max, source_head, *, entry=LEGACY_ENTRY,
                       allow_finish_reuse=False):
    if entry not in ENTRIES:
        raise PreflightError('unsupported_build_entry')
    if type(allow_finish_reuse) is not bool:
        raise PreflightError('finish_reuse_requires_boolean')
    if entry != LEGACY_ENTRY and allow_finish_reuse:
        raise PreflightError('finish_reuse_option_requires_legacy_entry')
    if not re.fullmatch(r'[0-9A-Fa-f]+', expected_title):
        raise PreflightError('expected_playfab_title_requires_hex_identifier')
    if type(published_max) is not int or not 0 <= published_max <= ANDROID_MAX:
        raise PreflightError('published_maximum_out_of_range')
    if source_head is not None and not re.fullmatch('[0-9a-f]{40}', source_head):
        raise PreflightError('source_head_requires_full_lowercase_sha')


def audit_project(root, output, expected_title, published_max, *, allow_finish_reuse=False,
                  source_head=None, now=None, entry=LEGACY_ENTRY):
    """Inspect only the selected entry's inputs using a short Windows snapshot lock."""
    validate_arguments(expected_title, published_max, source_head, entry=entry,
                       allow_finish_reuse=allow_finish_reuse)
    root, output = absolute_local(root), absolute_local(output)
    now = capture_local_time() if now is None else now
    legacy = entry == LEGACY_ENTRY
    calculation = ({'localEpoch': '2023-01-21T00:00:00', 'versionPrefix': '1.0.',
                    'weekRule': 'whole local days since epoch divided by 7',
                    'timeScope': 'one captured local time; not a later Unity reservation'}
                   if legacy else {'versionSource': PROJECT_SETTINGS,
                                   'versionRule': 'use current bundleVersion and AndroidBundleVersionCode unchanged',
                                   'outputRule': 'producer plan required; no inferred output collision check'})
    result = {'schema': 2, 'mode': 'operating-build-inputs', 'entry': entry, 'releaseReady': False,
              'inputChecksPassed': False, 'capturedLocalTime': now.isoformat(),
              'buildEntryPoint': 'Build/AOS/AAB' if legacy else 'PrivateProductionAdsBuild.BuildApprovedAdultRelease',
              'calculation': calculation,
              'sourceHeadClaim': source_head, 'sourceHeadIndependentlyVerified': False,
              'checks': {}, 'issues': [], 'inputSnapshotsPrivate': {}, 'legacyAabPlan': None,
              'approvedAdultReleasePlan': None,
              'projectSettingsVersionCodeUsed': False, 'unverified': UNVERIFIED}
    if not legacy:
        result['unverified'] = UNVERIFIED + [
            'Approved producer output path/collision requires its exact reviewed private plan; not checked here.',
            'Serialized ProjectSettings versions are not an observation of imported or later PlayerSettings.',
        ]
    validate_report_path(root, output)
    paths = {relative: root / relative for relative in ENTRY_INPUTS[entry]}
    # Every input parent is already expected to exist. No directory creation.
    with locked_directories([p.parent for p in paths.values()] + [output.parent]), ExitStack() as stack:
        validate_report_path(root, output)
        blobs, held, identities = {}, {}, set()
        finished = legacy and attributes(paths[FINISH]) is not None
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

        contract_ok = check('legacySourceContractMatches' if legacy else 'approvedSourceContractMatches',
                            lambda: source_contract(blobs, entry))
        check('playFabTitleMatchesExpected', lambda: playfab_title_matches(blobs[PLAYFAB], expected_title))
        check('pgsSingleConfiguredValuesMatch', lambda: pgs_matches(blobs[PGS_TEXT], blobs[PGS_ASSET]))
        if contract_ok and legacy:
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
        elif contract_ok:
            def calculate_approved():
                result['approvedAdultReleasePlan'] = approved_adult_release_plan(blobs[PROJECT_SETTINGS], published_max)
                result['projectSettingsVersionCodeUsed'] = True
            if check('approvedAdultReleasePlanCalculated', calculate_approved):
                check('versionExceedsPublishedMaximum', lambda:
                      result['approvedAdultReleasePlan']['versionExceedsPublishedMaximum'])
        # Verify pathname identity and content while the same file handles remain held.
        for relative, handle in held.items():
            named = paths[relative].stat()
            snapshot = result['inputSnapshotsPrivate'][relative]
            if (not regular(named) or (named.st_dev, named.st_ino) != handle.identity):
                raise PreflightError('input_snapshot_changed')
            handle.verify(snapshot['identity'], snapshot['sha256'])
        if legacy and not finished and attributes(paths[FINISH]) is not None:
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
    parser.add_argument('--entry', choices=ENTRIES, default=LEGACY_ENTRY, action=Once)
    parser.add_argument('--allow-finish-reuse', action=Once, nargs=0, default=False)
    parser.add_argument('--source-head', action=Once)
    args = parser.parse_args(argv)
    try:
        result = audit_project(args.project_path, args.output, args.expected_playfab_title,
                               args.published_max_code, allow_finish_reuse=args.allow_finish_reuse,
                               source_head=args.source_head, entry=args.entry)
    except (OSError, ValueError) as error:
        code = str(error) if isinstance(error, PreflightError) else 'guarded_input_or_report_io_failed'
        print(json.dumps({'inputChecksPassed': False, 'releaseReady': False, 'issues': [code]}))
        return 2
    # Private snapshots/identities/settings values/paths never go to stdout.
    print(json.dumps({key: result[key] for key in ('inputChecksPassed', 'releaseReady', 'issues')}))
    return 0 if result['inputChecksPassed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
