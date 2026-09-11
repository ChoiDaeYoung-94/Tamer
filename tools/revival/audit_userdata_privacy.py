"""Offline, aggregate-only audit of complete PlayFab UserData snapshots.

No network, credentials, write payloads, migration or deletion API are supported.
See docs/revival/userdata-private-migration.ko.md for the snapshot contract.
"""
import argparse
import json
import sys
from pathlib import Path

KNOWN_KEYS = frozenset({"Gold", "Power", "AttackSpeed", "MoveSpeed", "Tutorial",
                        "AllyMonsters", "Sex", "GooglePlay", "GoogleAdMob", "NickName"})
MAX_BYTES = 16 * 1024 * 1024


class SnapshotError(ValueError):
    """Messages must never contain input data, keys, identifiers or paths."""


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise SnapshotError("duplicate_json_key")
        result[key] = value
    return result


def _invalid_constant(_):
    raise SnapshotError("invalid_json_number")


def load_snapshot(path):
    try:
        with Path(path).open("rb") as stream:
            raw = stream.read(MAX_BYTES + 1)
        if len(raw) > MAX_BYTES:
            raise SnapshotError("snapshot_too_large")
        result = json.loads(raw.decode("utf-8-sig"), object_pairs_hook=_unique_object,
                            parse_constant=_invalid_constant)
    except SnapshotError:
        raise
    except (OSError, UnicodeError, ValueError, RecursionError):
        raise SnapshotError("unreadable_snapshot") from None
    validate_snapshot(result)
    return result


def _fields(value, required, optional=()):
    if not isinstance(value, dict) or not set(required) <= value.keys() or \
            value.keys() - set(required) - set(optional):
        raise SnapshotError("invalid_snapshot_shape")


def validate_snapshot(snapshot):
    _fields(snapshot, {"schema", "title", "scope", "players"})
    if type(snapshot["schema"]) is not int or snapshot["schema"] != 1 or \
            snapshot["scope"] != "complete-userdata-per-listed-player" or \
            not isinstance(snapshot["title"], str) or not snapshot["title"].strip() or \
            not isinstance(snapshot["players"], list):
        raise SnapshotError("invalid_snapshot_header")
    seen = set()
    for player in snapshot["players"]:
        _fields(player, {"PlayFabId", "DataVersion", "Data"})
        identity, version = player["PlayFabId"], player["DataVersion"]
        if not isinstance(identity, str) or not identity.strip() or identity in seen:
            raise SnapshotError("invalid_or_duplicate_player")
        seen.add(identity)
        if type(version) is not int or not 0 <= version <= 4294967295:
            raise SnapshotError("invalid_data_version")
        if not isinstance(player["Data"], dict):
            raise SnapshotError("invalid_data")
        for key, record in player["Data"].items():
            if not isinstance(key, str) or not key or key != key.strip() or key.startswith("!"):
                raise SnapshotError("invalid_data_key")
            _fields(record, {"Value", "Permission"}, {"LastUpdated"})
            if not isinstance(record["Value"], str) or record["Permission"] not in ("Public", "Private"):
                raise SnapshotError("invalid_data_record")
            if "LastUpdated" in record and not isinstance(record["LastUpdated"], str):
                raise SnapshotError("invalid_data_record")


def audit(snapshot):
    validate_snapshot(snapshot)
    known_public = unknown_public = private = affected = 0
    for player in snapshot["players"]:
        has_public = False
        for key, record in player["Data"].items():
            if record["Permission"] == "Private":
                private += 1
            else:
                has_public = True
                if key in KNOWN_KEYS:
                    known_public += 1
                else:
                    unknown_public += 1
        affected += has_public
    return {"schema": 1, "mode": "offline-audit", "productionWrites": 0,
            "listedPlayers": len(snapshot["players"]), "playersWithPublicKeys": affected,
            "knownPublicKeys": known_public, "unknownPublicKeys": unknown_public,
            "privateKeys": private, "automaticMigrationAuthorized": False,
            "populationCoverageProven": False,
            "requiresUnknownKeyReview": unknown_public > 0}


def compare(before, after, phase):
    """Preflight detects drift; postflight requires exact values and intended permission changes.

    A matching preflight is never a lock/CAS guarantee. A postflight success only
    describes these snapshots, not intervening writes or future old-client writes.
    """
    validate_snapshot(before)
    validate_snapshot(after)
    if phase not in ("preflight", "postflight"):
        raise SnapshotError("invalid_phase")
    if before["title"] != after["title"]:
        raise SnapshotError("title_mismatch")
    old = {p["PlayFabId"]: p for p in before["players"]}
    new = {p["PlayFabId"]: p for p in after["players"]}
    if old.keys() != new.keys():
        raise SnapshotError("player_set_mismatch")
    counts = {"keySetMismatches": 0, "valueMismatches": 0,
              "permissionMismatches": 0, "versionMismatches": 0}
    for identity, left in old.items():
        right = new[identity]
        a, b = left["Data"], right["Data"]
        if a.keys() != b.keys():
            counts["keySetMismatches"] += 1
        candidates = any(k in KNOWN_KEYS and v["Permission"] == "Public" for k, v in a.items())
        old_version, new_version = left["DataVersion"], right["DataVersion"]
        version_ok = new_version == old_version
        if phase == "postflight" and candidates:
            version_ok = new_version > old_version
        if not version_ok:
            counts["versionMismatches"] += 1
        for key in a.keys() & b.keys():
            if a[key]["Value"] != b[key]["Value"]:
                counts["valueMismatches"] += 1
            expected = a[key]["Permission"]
            if phase == "postflight" and key in KNOWN_KEYS and expected == "Public":
                expected = "Private"
            if b[key]["Permission"] != expected:
                counts["permissionMismatches"] += 1
    return {"schema": 1, "mode": "offline-" + phase, "productionWrites": 0,
            "listedPlayers": len(old), "snapshotComparisonPassed": not any(counts.values()),
            "automaticMigrationAuthorized": False, "concurrentWritesExcluded": False, **counts}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", help="Private local JSON snapshot; never commit it")
    parser.add_argument("--compare", metavar="SNAPSHOT")
    parser.add_argument("--phase", choices=("preflight", "postflight"))
    args = parser.parse_args(argv)
    if bool(args.compare) != bool(args.phase):
        parser.error("--compare and --phase must be used together")
    try:
        before = load_snapshot(args.snapshot)
        result = compare(before, load_snapshot(args.compare), args.phase) if args.compare else audit(before)
    except SnapshotError as error:
        print(json.dumps({"error": str(error), "productionWrites": 0}), file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2))
    return 1 if result.get("snapshotComparisonPassed") is False else 0


if __name__ == "__main__":
    sys.exit(main())
