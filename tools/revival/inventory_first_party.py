"""List tracked project C# for the file-by-file recovery audit (no asset contents)."""
import argparse
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def inventory():
    tracked = subprocess.check_output(
        ['git', 'ls-files', '*.cs'], cwd=ROOT, text=True).splitlines()
    rows = []
    excluded = {}
    for name in tracked:
        if not (name.startswith(('Assets/Scripts/', 'Assets/Tests/')) or name == 'Assets/GPGSIds.cs'):
            prefix = '/'.join(name.split('/')[:2])
            excluded[prefix] = excluded.get(prefix, 0) + 1
            continue
        source = (ROOT / name).read_text(encoding='utf-8-sig')
        rows.append({
            'path': name,
            'kind': 'tests' if name.startswith('Assets/Tests/') else
                    'generated-platform-ids' if name == 'Assets/GPGSIds.cs' else
                    'editor' if name.startswith('Assets/Scripts/Editor/') else 'runtime',
            'lines': len(source.splitlines()),
            'declaredTypes': re.findall(r'\b(?:class|struct|interface|enum)\s+(\w+)', source),
            'managerReferences': sorted(set(re.findall(r'\bManagers\.(\w+)', source))),
            'singletonReferences': sorted(set(re.findall(r'\b(\w+)\.Instance\b', source))),
            'metaPresent': (ROOT / (name + '.meta')).is_file(),
        })
    return {'schema': 1, 'sourceCommit': subprocess.check_output(
        ['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'scope': 'Tracked Assets/Scripts, Assets/Tests, and generated Assets/GPGSIds.cs; vendor SDK/assets excluded',
        'lexicalReferencesOnly': True, 'files': rows, 'excludedByRoot': excluded}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = inventory()
    if args.output:
        args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'files': len(result['files']), 'missingMeta': sum(
        not row['metaPresent'] for row in result['files']), 'excludedByRoot': result['excludedByRoot']}))
