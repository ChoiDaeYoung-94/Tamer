"""Owned Python->Java bridge integration with an invalid synthetic JKS only."""
import io
import json
from pathlib import Path
import re
import sys
import types

source = Path(__file__).resolve().parent.parent / 'producer_bridge.py'
bridge = types.ModuleType('synthetic_bridge_java')
bridge.__file__ = str(source)
exec(compile(source.read_bytes(), str(source), 'exec'), bridge.__dict__)
pins = [{'path': str(bridge.HELPERS / p), 'sha256': bridge.sha((bridge.HELPERS / p).read_bytes())}
        for p in bridge.SOURCES]
bridge.load_helpers({'pins': pins})
java, classes, evidence = sys.argv[1:4]
folder = Path(evidence)
(folder / 'src').mkdir()
(folder / 'src' / 'AeDeong.keystore').write_bytes(bytes.fromhex('feedfeed00000002'))
certificate = folder / 'synthetic-certificate.der'
certificate.write_bytes(b'invalid synthetic certificate')
value = {'checkout': str(folder), 'certificate': str(certificate), 'alias': 'synthetic-alias',
         'java': java, 'classDirectory': classes}
plan = {'checkout': str(folder), 'run': 'synthetic-run'}
calls = {'producer': 0, 'receipt': 0}
bridge.check = lambda *args, **kwargs: (plan, {})
def forbidden(*args, **kwargs):
    calls['producer'] += 1
    raise AssertionError('Synthetic builder should be unreachable')
def receipt(*args, **kwargs):
    calls['receipt'] += 1
    raise AssertionError('Synthetic receipt should be unreachable')
bridge.producer.build_once = forbidden
bridge.producer.write_new = receipt
state = {}
kind = ''
try:
    bridge.execute(value, 'e' * 32, sys.stdin.buffer, state)
except bridge.StageFailure as failure:
    kind = failure.kind
if (not re.fullmatch('[A-Za-z0-9_]{1,100}', kind) or calls != {'producer': 0, 'receipt': 0} or
        state.get('phase') != 'challenge' or state.get('producerInvoked') is not False):
    raise SystemExit(2)
print(json.dumps({'passed': True, 'phase': 'challenge', 'exceptionType': kind,
                  'producerCalls': 0, 'receiptCalls': 0, 'parentAndWorkerJobRequired': True}), end='')
