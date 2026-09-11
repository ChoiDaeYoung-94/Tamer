"""python -m server.receipts {check,migrate,backup,restore,serve}."""
import argparse
import json
import os
from pathlib import Path

from .config import Settings
from . import database
from .runtime import build, make_server, safe_logging


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['check', 'migrate', 'backup', 'restore', 'serve'])
    parser.add_argument('--destination', type=Path)
    parser.add_argument('--source', type=Path)
    args = parser.parse_args()
    safe_logging()
    try:
        settings = Settings.from_env(os.environ)
        if args.command == 'check':
            print(json.dumps({'configurationValid': True, 'databaseReady': database.ready(settings.database),
                              'upstreamVerified': False}))
        elif args.command == 'migrate': database.migrate(settings.database)
        elif args.command in ('backup', 'restore'):
            if args.destination is None or not args.destination.is_absolute():
                raise ValueError('absolute_destination_required')
            source = settings.database if args.command == 'backup' else args.source
            if source is None or not source.is_absolute(): raise ValueError('absolute_source_required')
            database.backup(source, args.destination)
        else:
            server = make_server(build(settings), settings.port)
            try: server.run()
            finally: server.close()
        return 0
    except KeyboardInterrupt:
        return 0
    except Exception:
        print('{"success":false,"code":"receipt_configuration_or_operation_failed"}')
        return 1


if __name__ == '__main__': raise SystemExit(main())
