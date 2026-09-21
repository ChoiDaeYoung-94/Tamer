"""python -m server.privacy {check,init-db,serve}; configuration is environment-only."""
import argparse
import os
from pathlib import Path
import sys

from .config import Settings
from .runtime import build, check_database, initialize, instance_lock, make_server, safe_logging


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('check', 'init-db', 'serve'))
    args = parser.parse_args(argv)
    safe_logging()
    try:
        pinned = Path(__file__).with_name('.python-version').read_text(encoding='ascii').strip()
        if '.'.join(map(str, sys.version_info[:3])) != pinned:
            raise ValueError('python_version_mismatch')
        settings = Settings.from_env(os.environ)
        with instance_lock(settings.database):
            if args.command == 'init-db':
                initialize(settings)
            elif args.command == 'check':
                check_database(settings.database)
            else:
                server = make_server(build(settings), settings.port)
                try:
                    server.run()
                finally:
                    server.close()
        print('{"success":true,"upstreamVerified":false}')
        return 0
    except KeyboardInterrupt:
        return 0
    except Exception:
        # Never print exception text, environment values, local paths, or credentials.
        print('{"success":false,"code":"deletion_configuration_or_operation_failed"}')
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
