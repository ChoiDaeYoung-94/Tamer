"""Read-only preflight for disabled production-ad preparation; never builds or injects."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate configuration key')
        result[key] = value
    return result


def read_contract(raw, checkout):
    config = json.loads(raw, object_pairs_hook=unique_object)
    if not isinstance(config, dict):
        raise ValueError('Configuration must be an object')
    for key, expected in [('consoleInventoryConfirmed', True),
                          ('productionActivationApproved', False),
                          ('regionalReviewApproved', False)]:
        if config.get(key) is not expected:
            raise ValueError('Required boolean contract failed: ' + key)
    for key in ('checkout', 'androidAppId', 'productionRewardedAdUnit'):
        if not isinstance(config.get(key), str) or not config[key]:
            raise ValueError('Required string contract failed: ' + key)
    if not Path(config['checkout']).is_absolute() or Path(config['checkout']).resolve() != checkout.resolve():
        raise ValueError('Checkout mismatch')
    app, unit = config['androidAppId'], config['productionRewardedAdUnit']
    if (not re.fullmatch(r'ca-app-pub-[0-9]{16}~[0-9]{10}', app)
            or not re.fullmatch(r'ca-app-pub-[0-9]{16}/[0-9]{10}', unit)
            or app.startswith('ca-app-pub-3940256099942544~')
            or app.split('~')[0] != unit.split('/')[0]):
        raise ValueError('Publisher inventory mismatch')
    return config


def require_disabled_declaration(source, name, declaration):
    # Conservative source contract, not a C# compiler. Mask comments and strings
    # before matching so examples cannot stand in for an active declaration.
    token = re.compile(r'//[^\r\n]*|/\*[\s\S]*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
    code = token.sub(lambda m: re.sub(r'[^\r\n]', ' ', m.group()), source)
    if re.search(r'/\*|\*/|["\']', code):
        raise ValueError('Unsupported or incomplete source token')
    declarations = list(re.finditer(r'\bbool\s+' + re.escape(name) + r'\s*(?:=>|=)', code))
    if len(declarations) != 1:
        raise ValueError('Ambiguous gate declaration')
    matches = list(re.finditer(r'^\s*' + re.escape(declaration) + r'\s*$', code, re.M))
    if len(matches) != 1:
        raise ValueError('Gate must be an unconditional disabled declaration')
    depth = 0
    for directive in re.finditer(r'^\s*#\s*(\w+)', code[:matches[0].end()], re.M):
        kind = directive.group(1)
        if kind == 'if':
            depth += 1
        elif kind == 'endif':
            depth -= 1
            if depth < 0:
                raise ValueError('Unbalanced preprocessor scope')
        elif kind in ('else', 'elif') and depth == 0:
            raise ValueError('Unbalanced preprocessor branch')
        elif kind not in ('else', 'elif', 'region', 'endregion'):
            raise ValueError('Unsupported preprocessor contract')
    if depth:
        raise ValueError('Conditional gate declaration')


def audit(checkout, config_path):
    root = checkout.resolve()
    raw = config_path.read_bytes()
    config = read_contract(raw, root)
    paths = ['Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
             'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml',
             'Assets/Scripts/Advertising/AdRequestPolicy.cs',
             'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs']
    data = {path: (root / path).read_bytes() for path in paths}
    settings = re.findall(r'^\s*adMobAndroidAppId:\s*(\S+)\s*$', data[paths[0]].decode('utf-8-sig'), re.M)
    ns = '{http://schemas.android.com/apk/res/android}'
    manifest = ET.fromstring(data[paths[1]])
    ids = [n.get(ns + 'value') for n in manifest.iter('meta-data')
           if n.get(ns + 'name') == 'com.google.android.gms.ads.APPLICATION_ID']
    if settings != [config['androidAppId']] or ids != settings:
        raise ValueError('Source publisher identity mismatch')
    require_disabled_declaration(data[paths[2]].decode('utf-8-sig'), 'ProductionAdsEnabled',
                                 'public const bool ProductionAdsEnabled = false;')
    age = data[paths[3]].decode('utf-8-sig')
    require_disabled_declaration(age, 'RegionalConsentReviewed',
                                 'public static bool RegionalConsentReviewed => false;')
    for cohort in ('Under13', 'From13To15', 'From16To17', 'Adult'):
        require_disabled_declaration(age, cohort + 'ConsentReviewed',
                                     'private const bool ' + cohort + 'ConsentReviewed = false;')
    # Digest detects changes only when a caller checks it again; it is not a lock.
    return {'scope': 'read-only source/config preflight; no injection/build/binary verification',
            'configSha256': hashlib.sha256(raw).hexdigest(),
            'sourceSha256': {path: hashlib.sha256(value).hexdigest() for path, value in data.items()},
            'disabledPreparationValid': True, 'binaryVerified': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkout', type=Path, required=True)
    parser.add_argument('--config', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = audit(args.checkout, args.config)
    except Exception:
        # Parser/path exceptions can contain private data; do not echo them.
        parser.exit(1, 'Private ads preparation rejected; inspect configuration locally.\n')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
