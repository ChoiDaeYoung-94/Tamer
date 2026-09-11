"""Strict staging configuration. Errors contain field names, never values."""
from dataclasses import dataclass, field
import json
from pathlib import Path
import re
from urllib.parse import urlsplit


class ConfigurationError(ValueError):
    pass


@dataclass(frozen=True, repr=False)
class Settings:
    title: str
    package: str
    accounts: tuple
    origin: str
    port: int
    database: Path
    secret_file: Path = field(repr=False)
    google_file: Path = field(repr=False)

    @classmethod
    def from_env(cls, env):
        def required(name):
            value = env.get('TAMER_RECEIPT_' + name, '')
            if not value or value != value.strip():
                raise ConfigurationError('invalid_' + name.lower())
            return value
        if required('ENV') != 'staging':
            raise ConfigurationError('staging_only')
        title, production = required('TITLE_ID'), required('PRODUCTION_TITLE_ID')
        if not all(re.fullmatch(r'[A-Za-z0-9]{3,32}', t) for t in (title, production)) or title.lower() == production.lower():
            raise ConfigurationError('separate_test_title_required')
        package = required('PACKAGE')
        if not re.fullmatch(r'[a-zA-Z][\w]*(?:\.[a-zA-Z][\w]*)+\.iaptest', package):
            raise ConfigurationError('separate_iaptest_package_required')
        try:
            accounts = json.loads(required('TEST_ACCOUNTS'))
            if (not isinstance(accounts, list) or not 1 <= len(accounts) <= 20 or
                    any(not isinstance(a, str) or not re.fullmatch(r'[A-Za-z0-9_-]{3,64}', a) for a in accounts) or
                    len(set(accounts)) != len(accounts)):
                raise ValueError()
        except (ValueError, TypeError):
            raise ConfigurationError('invalid_test_accounts') from None
        origin = required('PUBLIC_ORIGIN')
        try:
            parsed = urlsplit(origin)
            if parsed.port is not None and not 1 <= parsed.port <= 65535:
                raise ValueError()
        except ValueError:
            raise ConfigurationError('invalid_public_origin') from None
        if (parsed.scheme != 'https' or not parsed.hostname or parsed.username or parsed.password or
                parsed.path not in ('', '/') or parsed.query or parsed.fragment):
            raise ConfigurationError('invalid_public_origin')
        try:
            port = int(required('PORT'))
            if not 1024 <= port <= 65535: raise ValueError()
        except ValueError:
            raise ConfigurationError('invalid_port') from None
        if env.get('TAMER_RECEIPT_BIND', '127.0.0.1') != '127.0.0.1':
            raise ConfigurationError('loopback_bind_required')
        paths = []
        for name in ('DATA_DIR', 'PLAYFAB_SECRET_FILE', 'GOOGLE_CREDENTIALS_FILE'):
            path = Path(required(name))
            if not path.is_absolute(): raise ConfigurationError('absolute_' + name.lower() + '_required')
            if name == 'DATA_DIR':
                if not path.is_dir(): raise ConfigurationError('invalid_data_dir')
            elif not path.is_file():
                raise ConfigurationError('invalid_' + name.lower())
            paths.append(path.resolve())
        return cls(title, package, tuple(accounts), origin.rstrip('/'), port,
                   paths[0] / 'receipts.sqlite', paths[1], paths[2])
