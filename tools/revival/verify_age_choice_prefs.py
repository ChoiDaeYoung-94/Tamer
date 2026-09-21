"""Read-only inspection of the observed Unity Android PlayerPrefs XML encoding."""
import argparse
import json
import re
import urllib.parse
import xml.etree.ElementTree as ET
from pathlib import Path

KEY = 'Tamer.Privacy.AgeChoice'
CHOICES = ('under13', '13to15', '16to17', '18plus', 'declined')


def verify(xml_bytes, expected):
    if expected not in CHOICES:
        raise ValueError('Unsupported expected choice')
    root = ET.fromstring(xml_bytes)
    matches = [node for node in root if node.attrib.get('name') == KEY]
    if root.tag != 'map' or len(matches) != 1 or matches[0].tag != 'string':
        raise ValueError('Expected exactly one age-choice string')
    raw = matches[0].text or ''
    if re.search(r'%(?![0-9a-fA-F]{2})', raw):
        raise ValueError('Malformed percent escape')
    # Decode once; never unquote repeatedly or change the file being inspected.
    decoded = urllib.parse.unquote(raw, encoding='utf-8', errors='strict')
    if decoded != '1|' + expected:
        raise ValueError('Age choice does not match the expected version and interval')
    return {'key': KEY, 'raw': raw, 'decoded': decoded, 'matched': True}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('xml', type=Path)
    parser.add_argument('--expected', choices=CHOICES, required=True)
    args = parser.parse_args()
    print(json.dumps(verify(args.xml.read_bytes(), args.expected)))
