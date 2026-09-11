"""Explicit version-1 migration and consistent SQLite backup; never overwrite."""
from contextlib import closing
import os
from pathlib import Path
import sqlite3


def connect_existing(path, readonly=False):
    return sqlite3.connect(Path(path).resolve().as_uri() + ('?mode=ro' if readonly else '?mode=rw'),
                           uri=True, timeout=5)


def check_schema(db):
    columns = [(r[1], r[2], r[3], r[5]) for r in db.execute('PRAGMA table_info(grants)')]
    expected = [('token_hash', 'TEXT', 0, 1), ('account', 'TEXT', 1, 0),
                ('product', 'TEXT', 1, 0), ('package', 'TEXT', 1, 0)]
    if columns != expected:
        raise ValueError('unsupported_database_schema')


def migrate(path):
    with closing(sqlite3.connect(path, timeout=5)) as db, db:
        db.execute('BEGIN IMMEDIATE')
        version = db.execute('PRAGMA user_version').fetchone()[0]
        if version not in (0, 1): raise ValueError('unsupported_database_version')
        db.execute('CREATE TABLE IF NOT EXISTS grants (token_hash TEXT PRIMARY KEY, '
                   'account TEXT NOT NULL, product TEXT NOT NULL, package TEXT NOT NULL)')
        check_schema(db)
        db.execute('PRAGMA user_version=1')


def ready(path):
    try:
        with closing(connect_existing(path)) as db:
            if db.execute('PRAGMA user_version').fetchone()[0] != 1: return False
            check_schema(db)
            if db.execute('PRAGMA quick_check').fetchone()[0] != 'ok': return False
            db.execute('BEGIN IMMEDIATE')
            db.rollback()  # Check writer access without creating grants.
        return True
    except (OSError, sqlite3.Error, ValueError):
        return False


def backup(source, destination):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    if source == destination: raise ValueError('distinct_backup_required')
    with closing(connect_existing(source, readonly=True)) as src:
        check_schema(src)
        if src.execute('PRAGMA user_version').fetchone()[0] != 1:
            raise ValueError('migration_required')
        fd = os.open(destination, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        os.close(fd)
        try:
            with closing(sqlite3.connect(destination)) as dst:
                src.backup(dst)
                if dst.execute('PRAGMA quick_check').fetchone()[0] != 'ok':
                    raise ValueError('backup_integrity_failed')
        except Exception:
            destination.unlink()  # Only the file exclusively created above.
            raise
