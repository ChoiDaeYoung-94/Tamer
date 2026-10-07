"""Read-only approved adult producer conditions. Never writes approval records."""
import hashlib
import json
from pathlib import Path
import re
from xml.etree import ElementTree as ET
from validate_private_ads_preparation import unique_object, require_disabled_declaration

REFERENCES = ('inventorySha256', 'countryContractSha256', 'activationReviewSha256',
              'regionalReviewSha256', 'adultReviewSha256')
TRUE_FLAGS = ('productionActivationApproved', 'regionalReviewApproved', 'adultConsentReviewed')
FALSE_FLAGS = ('under13ConsentReviewed', 'from13To15ConsentReviewed', 'from16To17ConsentReviewed')
FIELDS = {'schema', 'mode', 'packageId', 'androidAppId', 'productionRewardedAdUnit',
          'countryCodes', 'sourceHead', *REFERENCES, *TRUE_FLAGS, *FALSE_FLAGS}


def require_immutable_binding(source, pins, binding_name='ApprovedRelease'):
    if binding_name not in ('ApprovedRelease', 'ApprovedPrivacy'):
        raise ValueError('Binding name rejected')
    # Preserve offsets while masking comments and every quoted value. A declaration
    # printed inside a string can never be mistaken for active source code.
    token = re.compile(r'//[^\r\n]*|/\*[\s\S]*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
    code = token.sub(lambda m: re.sub(r'[^\r\n]', ' ', m.group()), source)
    if re.search(r'/\*|\*/|["\']', code):
        raise ValueError('Unsupported or incomplete binding source token')
    declarations = list(re.finditer(r'\bprivate\s+static\s+readonly\s+Binding\s+' + binding_name + r'\s*=', code))
    if len(declarations) != 1 or len(re.findall(r'\b' + binding_name + r'\s*=', code)) != 1:
        raise ValueError('One immutable binding declaration required')
    declaration = declarations[0]
    end = code.find(';', declaration.end())
    if end < 0 or '#' in code[declaration.start():end]:
        raise ValueError('Incomplete binding declaration')
    pattern = r'private\s+static\s+readonly\s+Binding\s+' + binding_name + r'\s*=\s*new\s+Binding\s*\(\s*' + r'\s*,\s*'.join('"' + re.escape(p) + '"' for p in pins) + r'\s*\)\s*;'
    if not re.fullmatch(pattern, source[declaration.start():end + 1]):
        raise ValueError('Exact literal binding required')
    depth = 0
    for directive in re.finditer(r'^\s*#\s*(\w+)[^\r\n]*', code, re.M):
        if directive.start() <= declaration.start() <= directive.end() or declaration.start() < directive.start() < end:
            raise ValueError('Conditional binding declaration rejected')
        if directive.start() > declaration.start() and depth and directive.start() <= end:
            raise ValueError('Conditional binding rejected')
        kind = directive.group(1)
        if kind == 'if':
            depth += 1
        elif kind == 'endif':
            depth -= 1
            if depth < 0: raise ValueError('Unbalanced source directives')
        elif kind in ('else', 'elif'):
            if depth == 0: raise ValueError('Unbalanced source branch')
        elif kind not in ('region', 'endregion'):
            raise ValueError('Unsupported binding source directive')
        # A declaration inside any conditional is rejected, even a true condition.
        if directive.end() < declaration.start():
            following = code.find('#', directive.end())
            if (following == -1 or following > declaration.start()) and depth:
                raise ValueError('Binding must be unconditional')
    if depth:
        raise ValueError('Unclosed source directives')


PRIVACY_REFERENCES = ('inventorySha256', 'countryContractSha256', 'regionalReviewSha256',
                      'adultReviewSha256', 'privacyEnvironmentReviewSha256')
PRIVACY_TRUE_FLAGS = ('privacySdkEnvironmentApproved', 'regionalReviewApproved', 'adultConsentReviewed')
PRIVACY_FALSE_FLAGS = (*FALSE_FLAGS, 'umpUnderAgeOfConsent', 'productionAdsAuthorized')
PRIVACY_FIELDS = {'schema', 'mode', 'packageId', 'androidAppId', 'sourceHead',
                  *PRIVACY_REFERENCES, *PRIVACY_TRUE_FLAGS, *PRIVACY_FALSE_FLAGS}


def read_approved(raw, *, privacy_only=False):
    def reject(_):
        raise ValueError('Invalid JSON constant')
    fields = PRIVACY_FIELDS if privacy_only else FIELDS
    references = PRIVACY_REFERENCES if privacy_only else REFERENCES
    true_flags = PRIVACY_TRUE_FLAGS if privacy_only else TRUE_FLAGS
    false_flags = PRIVACY_FALSE_FLAGS if privacy_only else FALSE_FLAGS
    mode = 'privacy_only_adult' if privacy_only else 'adult_only_release'
    if not raw or len(raw) > 16384:
        raise ValueError('Approved resource size rejected')
    value = json.loads(raw.decode('utf-8-sig'), object_pairs_hook=unique_object, parse_constant=reject)
    if not isinstance(value, dict) or set(value) != fields or type(value['schema']) is not int or value['schema'] != 1 or value['mode'] != mode:
        raise ValueError('Approved schema rejected')
    if any(value[k] is not True for k in true_flags) or any(value[k] is not False for k in false_flags):
        raise ValueError('Adult approval boundary rejected')
    strings = fields - {'schema', *true_flags, *false_flags}
    if any(type(value[k]) is not str or not value[k] for k in strings):
        raise ValueError('Approved string type rejected')
    if any(not re.fullmatch('[0-9a-f]{64}', value[k]) for k in references) or not re.fullmatch('[0-9a-f]{40}', value['sourceHead']):
        raise ValueError('Approval provenance rejected')
    app = value['androidAppId']
    unit = value.get('productionRewardedAdUnit')
    if (not re.fullmatch(r'ca-app-pub-[0-9]{16}~[0-9]{10}', app) or
            (not privacy_only and not re.fullmatch(r'ca-app-pub-[0-9]{16}/[0-9]{10}', unit)) or
            app.startswith('ca-app-pub-3940256099942544~') or (not privacy_only and app.split('~')[0] != unit.split('/')[0]) or
            not re.fullmatch(r'[a-zA-Z][a-zA-Z0-9_]*(?:\.[a-zA-Z][a-zA-Z0-9_]*)+', value['packageId'])):
        raise ValueError('Approved inventory rejected')
    if privacy_only:
        return value
    countries = value['countryCodes'].split(';')
    if countries != sorted(set(countries)) or any(not re.fullmatch('[A-Z]{2}', c) for c in countries):
        raise ValueError('Approved country declaration rejected')
    return value


def preflight_approved(root, config, head, version, *, privacy_only=False):
    import private_ads_build as base
    # Reuse ownership/clean/private/version checks, without disabled config audit.
    base.preflight(root, config, head, version, audit_contract=False)
    if privacy_only and any(any((root / 'Assets').rglob(name + '.*')) for name in ('RevivalPrivateAdsRelease', 'RevivalPrivatePrivacyRelease')):
        raise ValueError('Approval resource collision')
    raw = config.read_bytes()
    contract = read_approved(raw, privacy_only=privacy_only)
    request = (root / 'Assets/Scripts/Advertising/AdRequestPolicy.cs').read_text(encoding='utf-8-sig')
    age = (root / 'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs').read_text(encoding='utf-8-sig')
    # Conservative source declarations; actual compiled values are checked again
    # by the separate Editor approved entry. No JSON can turn these gates on.
    require_disabled_declaration(request, 'ProductionAdsEnabled', 'public const bool ProductionAdsEnabled = ' + ('false' if privacy_only else 'true') + ';')
    require_disabled_declaration(age, 'RegionalConsentReviewed', 'public static bool RegionalConsentReviewed => true;')
    require_disabled_declaration(age, 'AdultConsentReviewed', 'private const bool AdultConsentReviewed = true;')
    # An adult advertising contract cannot authorize the separate privacy SDK environment.
    require_disabled_declaration(age, 'PrivacySdkEnvironmentReviewed',
                                 'public static bool PrivacySdkEnvironmentReviewed => ' + ('true' if privacy_only else 'false') + ';')
    for cohort in ('Under13', 'From13To15', 'From16To17'):
        require_disabled_declaration(age, cohort + 'ConsentReviewed', 'private const bool ' + cohort + 'ConsentReviewed = false;')
    reader = 'PrivatePrivacyReleaseContract.cs' if privacy_only else 'PrivateAdsReleaseContract.cs'
    source = (root / 'Assets/Scripts/Advertising' / reader).read_text(encoding='utf-8-sig')
    references = PRIVACY_REFERENCES if privacy_only else REFERENCES
    pins = [hashlib.sha256(raw).hexdigest(), contract['sourceHead'], *(contract[k] for k in references)]
    # Deliberately recognize only one literal, source-reviewed immutable binding.
    # A future alternate binding implementation requires a reviewed validator edit.
    require_immutable_binding(source, pins, 'ApprovedPrivacy' if privacy_only else 'ApprovedRelease')
    settings = (root / 'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset').read_text()
    if re.findall(r'^\s*adMobAndroidAppId:\s*(\S+)\s*$', settings, re.M) != [contract['androidAppId']]:
        raise ValueError('Source App ID mismatch')
    manifest = ET.fromstring((root / 'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml').read_bytes())
    android = '{http://schemas.android.com/apk/res/android}'
    apps = [n.get(android + 'value') for n in manifest.iter('meta-data') if n.get(android + 'name') == 'com.google.android.gms.ads.APPLICATION_ID']
    project = (root / 'ProjectSettings/ProjectSettings.asset').read_text()
    if apps != [contract['androidAppId']] or re.findall(r'^\s*Android:\s*(\S+)\s*$', project, re.M) != [contract['packageId']]:
        raise ValueError('Approved package/manifest mismatch')
    return {('approvedPrivacySourceContractValid' if privacy_only else 'approvedSourceContractValid'): True, 'configSha256': hashlib.sha256(raw).hexdigest(),
            'binaryVerified': False, 'distributable': False}
