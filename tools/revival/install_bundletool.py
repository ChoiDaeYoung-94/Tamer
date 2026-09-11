"""Download the pinned official bundletool locally and verify its published digest."""
import hashlib
import urllib.request
from pathlib import Path

VERSION = '1.18.3'
SHA256 = 'a099cfa1543f55593bc2ed16a70a7c67fe54b1747bb7301f37fdfd6d91028e29'
ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'tools/.local/bundletool' / ('bundletool-all-' + VERSION + '.jar')


def install():
    data = DEST.read_bytes() if DEST.exists() else urllib.request.urlopen(
        f'https://github.com/google/bundletool/releases/download/{VERSION}/{DEST.name}', timeout=120).read()
    if hashlib.sha256(data).hexdigest() != SHA256:
        raise ValueError('bundletool checksum mismatch; existing file preserved')
    DEST.parent.mkdir(parents=True, exist_ok=True)
    if not DEST.exists():
        with DEST.open('xb') as stream:
            stream.write(data)
    print(f'bundletool {VERSION}: published SHA-256 verified')


if __name__ == '__main__':
    install()
