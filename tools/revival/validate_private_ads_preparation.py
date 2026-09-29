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
    if not re.search(r'^\s*public const bool ProductionAdsEnabled = false;\s*$',
                     data[paths[2]].decode('utf-8-sig'), re.M):
        raise ValueError('Production source gate is not disabled')
    age = data[paths[3]].decode('utf-8-sig')
    if not re.search(r'^\s*public static bool RegionalConsentReviewed => false;\s*$', age, re.M):
        raise ValueError('Regional source gate is not disabled')
    for cohort in ('Under13', 'From13To15', 'From16To17', 'Adult'):
        if not re.search(r'^\s*private const bool ' + cohort + r'ConsentReviewed = false;\s*$', age, re.M):
            raise ValueError('Cohort source gate is not disabled')
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
