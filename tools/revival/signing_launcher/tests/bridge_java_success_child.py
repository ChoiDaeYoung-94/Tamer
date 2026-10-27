"""One valid synthetic JKS->real challenge->receipt->fake producer integration.

Only this fixture module substitutes gate/producer functions, on a fresh private
synthetic root. No actual checkout/config/approved gate is accessed or modified.
"""
import json
import os
from pathlib import Path
import subprocess
import sys
import types

source = Path(__file__).resolve().parent.parent / 'producer_bridge.py'
bridge = types.ModuleType('synthetic_bridge_success')
bridge.__file__ = str(source)
exec(compile(source.read_bytes(), str(source), 'exec'), bridge.__dict__)
pins = [{'path': str(bridge.HELPERS / p), 'sha256': bridge.sha((bridge.HELPERS / p).read_bytes())}
        for p in bridge.SOURCES]
bridge.load_helpers({'pins': pins})


def main():
    java, keytool, classes, evidence = sys.argv[1:5]
    phase = 'fixture_input'
    secret = []
    try:
        bridge.require_owned_job()
        # This producer fixture is itself the native Job's owned child.
        frame = bytearray(sys.stdin.buffer.read(8193))
        if len(frame) > 8192:
            raise ValueError('Synthetic input bound')
        import io
        secret = bridge.passwords(io.BytesIO(frame))
        phase = 'synthetic_key_generation'
        root = Path(evidence) / 'private-valid-fixture'
        from private_ads_evidence import create_private_directory
        create_private_directory(root)
        (root / 'src').mkdir()
        key = root / 'src' / 'AeDeong.keystore'
        cert = root / 'synthetic-upload.der'
        # Environment is explicit on the Keytool child only. No password goes
        # into an argument, file, log, parent environment, or interactive prompt.
        env = {k: v for k, v in os.environ.items() if k not in bridge.SECRET_NAMES}
        env.update(FIXTURE_STORE_PASS=secret[0], FIXTURE_ALIAS_PASS=secret[1])
        command = [keytool, '-genkeypair', '-keystore', str(key), '-storetype', 'JKS',
                   '-alias', 'synthetic-rsa', '-keyalg', 'RSA', '-keysize', '2048', '-validity', '1',
                   '-dname', 'CN=Launcher Synthetic Fixture', '-storepass:env', 'FIXTURE_STORE_PASS',
                   '-keypass:env', 'FIXTURE_ALIAS_PASS', '-noprompt']
        made = subprocess.run(command, cwd=root, env=env, stdin=subprocess.DEVNULL,
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=20,
                              close_fds=True, creationflags=subprocess.CREATE_NO_WINDOW)
        if made.returncode:
            raise ValueError('Synthetic generation failed')
        exported = subprocess.run([keytool, '-exportcert', '-keystore', str(key), '-storetype', 'JKS',
            '-alias', 'synthetic-rsa', '-storepass:env', 'FIXTURE_STORE_PASS', '-file', str(cert)],
            cwd=root, env=env, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL, timeout=10, close_fds=True, creationflags=subprocess.CREATE_NO_WINDOW)
        if exported.returncode:
            raise ValueError('Synthetic certificate export failed')
        env.clear()
        (root / '.revival-local').mkdir()
        (root / '.revival-local' / 'private-ads-build').mkdir()
        run = root / '.revival-local' / 'private-ads-build' / ('f' * 32)
        create_private_directory(run)
        config = run / 'config.synthetic.json'
        config.write_text('{"syntheticFixtureOnly":true}')
        plan_path = run / 'plan.synthetic.json'
        plan_path.write_text('{"syntheticFixtureOnly":true}')
        review = run / 'review.synthetic.json'
        review.write_text('{"syntheticFixtureOnly":true}')
        head = 'a' * 40
        plan_sha = bridge.sha(plan_path.read_bytes())
        plan = {'checkout': str(root), 'run': run.relative_to(root).as_posix(),
                'before': {bridge.producer.KEY: {'sha256': bridge.sha(key.read_bytes())}},
                'config': str(config), 'editorVersion': 'synthetic-only', 'head': head,
                '_reviewedSha256': plan_sha}
        value = {'checkout': str(root), 'certificate': str(cert), 'alias': 'synthetic-rsa',
                 'java': java, 'classDirectory': classes, 'head': head, 'planSha256': plan_sha,
                 'certificateSha256': bridge.sha(cert.read_bytes()), 'plan': str(plan_path),
                 'sourceReview': str(review)}
        calls = {'gate': 0, 'producer': 0}
        def fixture_gate(*args, **kwargs):
            calls['gate'] += 1
            if root.resolve() != (Path(evidence) / 'private-valid-fixture').resolve():
                raise ValueError('Synthetic root mismatch')
            return plan, {}
        def fake_builder(build_root, build_config, build_head, version, *args, **kwargs):
            calls['producer'] += 1
            if (build_root != root or build_config != config or build_head != head or
                    version != 'synthetic-only' or kwargs != {'allow_unity_build': True} or
                    [os.environ.get(name) for name in bridge.SECRET_NAMES] != secret):
                raise ValueError('Synthetic producer binding mismatch')
            # Use the real producer receipt/env consumer contract; Unity itself
            # is never invoked. The synthetic plan is not a production plan.
            receipt = bridge.producer.require_signing(root, plan, run / bridge.SIGNING)
            if (receipt['alias'] != 'synthetic-rsa' or receipt['sourceHead'] != head or
                    receipt['planSha256'] != plan_sha or receipt['certificateSha256'] != value['certificateSha256']):
                raise ValueError('Synthetic exact receipt mismatch')
            return {'actualBuildExecuted': True}  # Fake producer completion only.
        bridge.check = fixture_gate
        bridge.producer.build_once = fake_builder
        phase = 'actual_challenge_receipt_fakeproducer'
        state = {}
        result = bridge.execute(value, 'e' * 32, io.BytesIO(frame), state)
        if (calls != {'gate': 2, 'producer': 1} or result['status'] != 'producer_completed' or
                state.get('phase') != 'completed' or any(name in os.environ for name in bridge.SECRET_NAMES)):
            raise ValueError('Synthetic completed state mismatch')
        return {'passed': True, 'actualChallengeVerified': True, 'exactReceiptVerified': True,
                'fakeProducerCalls': 1, 'actualUnityBuildCalls': 0, 'realApprovedGateCalls': 0,
                'ownedProducerEnvironmentCleared': True, 'binaryVerified': False, 'distributable': False}
    except BaseException as failure:
        return {'passed': False, 'phase': phase, 'exceptionType': type(failure).__name__}
    finally:
        secret.clear()
        if 'frame' in locals():
            frame[:] = b'\0' * len(frame)


if __name__ == '__main__':
    result = main()
    print(json.dumps(result), end='')
    raise SystemExit(0 if result['passed'] else 1)
