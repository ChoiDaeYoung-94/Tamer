"""Explicit host configuration. Never include supplied values in errors/repr."""
from dataclasses import dataclass
import json
from pathlib import Path
import re
import tempfile
from urllib.parse import urlsplit

from .deletion import Policy


class ConfigurationError(ValueError):
    pass


@dataclass(frozen=True, repr=False)
class Settings:
    database: Path
    title: str
    secret: str
    origin: str
    policy: Policy
    receipt_ttl: int
    port: int

    @classmethod
    def from_env(cls, env):
        def required(name):
            value = env.get('TAMER_DELETION_' + name, '')
            if (not isinstance(value, str) or not value or value != value.strip()
                    or any(ord(c) < 32 or ord(c) == 127 for c in value)):
                raise ConfigurationError('invalid_' + name.lower())
            return value

        def flag(name):
            value = env.get('TAMER_DELETION_' + name, 'false')
            if value not in ('true', 'false'):
                raise ConfigurationError('invalid_' + name.lower())
            return value == 'true'

        def number(name, minimum, maximum):
            value = required(name)
            if not value.isascii() or not value.isdecimal() or not minimum <= int(value) <= maximum:
                raise ConfigurationError('invalid_' + name.lower())
            return int(value)

        title, secret = required('TITLE_ID'), required('PLAYFAB_SECRET')
        if not re.fullmatch(r'[A-Za-z0-9]{3,32}', title):
            raise ConfigurationError('invalid_title_id')
        if not secret.isascii() or len(secret) > 4096 or any(c.isspace() for c in secret):
            raise ConfigurationError('invalid_playfab_secret')
        origin = required('PUBLIC_ORIGIN')
        try:
            uri = urlsplit(origin)
            if (not origin.isascii() or uri.scheme != 'https' or not uri.hostname
                    or uri.username is not None or uri.password is not None or uri.path != '/'
                    or uri.query or uri.fragment or '%' in uri.netloc or '\\' in origin
                    or any(c.isspace() for c in origin)
                    or (uri.port is not None and not 1 <= uri.port <= 65535)
                    or not re.fullmatch(r'[A-Za-z0-9.-]+', uri.hostname)
                    or any(not re.fullmatch(r'[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?', p)
                           for p in uri.hostname.split('.'))):
                raise ValueError()
        except ValueError:
            raise ConfigurationError('invalid_public_origin') from None
        revision = required('POLICY_REVISION')
        if len(revision) > 128 or required('SCOPE') != 'title':
            raise ConfigurationError('invalid_policy')
        try:
            plan = json.loads(required('RETENTION_PLAN'))
            if (not isinstance(plan, list) or not plan or len(plan) > 64
                    or any(not isinstance(v, str) or not v.strip() or len(v) > 1024 for v in plan)):
                raise ValueError()
        except (ValueError, TypeError):
            raise ConfigurationError('invalid_retention_plan') from None
        policy = Policy(revision, 'title', flag('RETENTION_APPROVED'), flag('REJOIN_APPROVED'),
                        flag('ENABLED'), tuple(plan), flag('SESSION_CONFIRMATION_ENABLED'),
                        flag('GOOGLE_PLAY_GAMES_SESSION_CONFIRMATION_ENABLED'))
        if policy.enabled and (not policy.ready or not policy.session_confirmation_enabled):
            raise ConfigurationError('explicit_policy_approval_required')
        root = Path(required('DATA_DIR'))
        if required('STORAGE_KIND') != 'local-persistent':
            raise ConfigurationError('persistent_local_storage_required')
        try:
            if (not root.is_absolute() or not root.is_dir() or root.is_symlink()
                    or root.resolve() != root.absolute()
                    or root.resolve().is_relative_to(Path(tempfile.gettempdir()).resolve())
                    or any(root.resolve().is_relative_to(Path(p)) for p in ('/tmp', '/var/tmp', '/dev/shm'))):
                raise ValueError()
            database = root / 'deletion.sqlite'
            if database.is_symlink() or (database.exists() and not database.is_file()):
                raise ValueError()
        except (ValueError, OSError):
            raise ConfigurationError('invalid_data_dir') from None
        return cls(database, title, secret, origin, policy,
                   number('RECEIPT_TTL_SECONDS', 1, 30 * 86400), number('PORT', 1024, 65535))
