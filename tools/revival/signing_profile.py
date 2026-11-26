"""Closed signing-profile metadata contract; no key, password, SDK or file access.

Callers still hold the existing regular-file/identity/SHA/private-ACL locks. This
module cannot grant review or activation approval and never manufactures evidence.
"""
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import PureWindowsPath, PurePosixPath
import re


EXTERNAL = 'external_pkcs12_upload'
PROFILE_FIELDS = {'schema', 'profile', 'storeType', 'keystore', 'allowedRoot',
                  'keystoreSha256', 'alias', 'certificateSha256',
                  'playAppSigningCertificateSha256',
                  'activationEvidence', 'activationEvidenceSha256'}
ACTIVATION_FIELDS = {'schema', 'applicationId', 'status', 'certificateSha256',
                     'playAppSigningCertificateSha256', 'observedUtc', 'validFromUtc',
                     'source'}


def reject(message):
    raise ValueError(message)


def digest(value):
    if type(value) is not str or re.fullmatch('[0-9a-f]{64}', value) is None:
        reject('Exact lowercase SHA-256 required')
    return value


def local_path(value):
    if type(value) is not str or not value or '\0' in value:
        reject('Canonical local drive path required')
    value=value.replace('\\', '/')
    if re.fullmatch('[A-Za-z]:/[^:]+', value) is None:
        reject('Canonical local drive path required')
    parts=value[3:].split('/')
    if any(not part or part in {'.', '..'} or part[-1:] in {' ', '.'} or
           any(ord(c)<32 or c in '<>"|?*' for c in part) for part in parts):
        reject('Ambiguous path component')
    if any(re.fullmatch(r'(?i)(?:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\..*)?', part)
           for part in parts):
        reject('Reserved device path')
    return PureWindowsPath(value)


def utc(value):
    if type(value) is not str:
        reject('UTC timestamp required')
    try:
        result=datetime.fromisoformat(value)
    except ValueError:
        reject('Invalid UTC timestamp')
    if result.tzinfo is None or result.utcoffset() != timedelta(0):
        reject('Explicit UTC offset required')
    return result


@dataclass(frozen=True)
class Selection:
    profile: str
    store_type: str
    keystore: str
    keystore_sha256: str
    alias: str | None
    certificate_sha256: str | None
    play_signer_sha256: str | None
    worker_magic: int
    external: bool


def select(plan):
    if type(plan) is not dict or type(plan.get('schema')) is not int:
        reject('Plan schema required')
    if plan['schema'] == 2:
        if 'signingProfile' in plan:
            reject('Legacy plan must not carry an external profile')
        value=plan.get('checkout')
        if type(value) is not str:
            reject('Legacy checkout required')
        root=local_path(value) if re.match('[A-Za-z]:',value) else PurePosixPath(value)
        if not root.is_absolute():
            reject('Legacy absolute checkout required')
        try:
            key_hash=digest(plan['before']['src/AeDeong.keystore']['sha256'])
        except (KeyError, TypeError):
            reject('Legacy key inventory required')
        return Selection('legacy_jks', 'JKS', (root/'src/AeDeong.keystore').as_posix(),
                         key_hash, None, None, None, 0x544B5031, False)
    if plan['schema'] != 3:
        reject('Unsupported signing plan schema')
    root=local_path(plan.get('checkout'))
    value=plan.get('signingProfile')
    if (type(value) is not dict or set(value) != PROFILE_FIELDS or
            type(value['schema']) is not int or value['schema'] != 1 or
            value['profile'] != EXTERNAL or value['storeType'] != 'PKCS12'):
        reject('Closed PKCS12 profile required')
    key=local_path(value['keystore']);allowed=local_path(value['allowedRoot'])
    if key.is_relative_to(root) or allowed.is_relative_to(root) or root.is_relative_to(allowed):
        reject('Signing key must remain outside the checkout')
    if key.parent != allowed or len(allowed.parts) < 3 or key.suffix.lower() not in {'.p12', '.pfx'}:
        reject('Exact private key directory and PKCS12 file required')
    alias=value['alias']
    if type(alias) is not str or re.fullmatch('[a-z0-9][a-z0-9._-]{0,127}', alias) is None:
        reject('Exact lowercase PKCS12 alias required')
    local_path(value['activationEvidence']);digest(value['activationEvidenceSha256'])
    play_signer=digest(value['playAppSigningCertificateSha256'])
    if play_signer == digest(value['certificateSha256']):
        reject('Upload and Play signing certificates must remain distinct')
    return Selection(EXTERNAL, 'PKCS12', key.as_posix(), digest(value['keystoreSha256']),
                     alias, digest(value['certificateSha256']), play_signer, 0x544B5032, True)


def require_activation(evidence, selection, *, expected_play_signer, now):
    """A scheduled reset is never equivalent to a fresh active certificate."""
    if not selection.external:
        reject('External upload profile required')
    if (type(evidence) is not dict or set(evidence) != ACTIVATION_FIELDS or
            type(evidence['schema']) is not int or evidence['schema'] != 1 or
            evidence['applicationId'] != 'com.AeDeong.MonsterTamer' or
            evidence['status'] != 'active' or evidence['source'] not in
            {'user_current_console', 'direct_current_console'}):
        reject('Fresh current Console activation evidence required')
    if (digest(expected_play_signer) != selection.play_signer_sha256 or
            digest(evidence['certificateSha256']) != selection.certificate_sha256 or
            digest(evidence['playAppSigningCertificateSha256']) != digest(expected_play_signer) or
            selection.certificate_sha256 == expected_play_signer):
        reject('Upload certificate or unchanged Play signer mismatch')
    if not isinstance(now, datetime) or now.tzinfo is None or now.utcoffset() != timedelta(0):
        reject('Current UTC time required')
    observed=utc(evidence['observedUtc']);valid=utc(evidence['validFromUtc'])
    if not timedelta(0) <= now-observed <= timedelta(hours=24) or valid > observed or valid > now:
        reject('Activation is future, stale or unobserved')
    return True
