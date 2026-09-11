"""Install the official pinned Windows CLI in this project, without changing global PATH."""
import hashlib
import json
import subprocess
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parents[2]
lock = json.loads((root / 'tools/revival/toolchain.json').read_text())
version = lock['unityCli']
base = 'https://public-cdn.cloud.unity3d.com/hub/prod/cli/' + version + '/'
manifest = json.load(urllib.request.urlopen(base + 'latest.json', timeout=60))
assert manifest['version'] == version, 'Release version mismatch'
binary = manifest['binaries']['win32-x64']
expected = lock['unityCliWindowsX64Sha256']
assert binary['sha256'].lower() == expected, 'Published checksum changed'
dest = root / 'tools/.local/unity-cli' / version / 'unity.exe'
data = dest.read_bytes() if dest.exists() else urllib.request.urlopen(base + binary['filename'], timeout=120).read()
assert hashlib.sha256(data).hexdigest() == expected, 'Binary checksum mismatch'
dest.parent.mkdir(parents=True, exist_ok=True)
if not dest.exists():
    with dest.open('xb') as out:
        out.write(data)
subprocess.run([str(dest), '--version'], check=True, cwd=root)
subprocess.run([str(dest), 'skill', 'install', 'codex', '--local', '--yes', '--non-interactive'], check=True, cwd=root)
print(dest)
