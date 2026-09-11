import contextlib
import copy
import io
import json
import tempfile
import unittest
from pathlib import Path

import audit_userdata_privacy as privacy


def sample():
    return {"schema": 1, "title": "synthetic-title", "scope": "complete-userdata-per-listed-player",
            "players": [{"PlayFabId": "synthetic-player", "DataVersion": 7, "Data": {
                "Gold": {"Value": "00100", "Permission": "Public"},
                "GooglePlay": {"Value": "ProductNoAds,legacy-token", "Permission": "Public"},
                "NickName": {"Value": "synthetic-nickname", "Permission": "Private"},
                "unknown-sensitive-key": {"Value": "synthetic-secret", "Permission": "Public"}}}]}


class PrivacyAuditTests(unittest.TestCase):
    def test_audit_counts_without_mutating_or_disclosing_data(self):
        before = sample()
        original = copy.deepcopy(before)
        result = privacy.audit(before)
        self.assertEqual((result["knownPublicKeys"], result["unknownPublicKeys"], result["privateKeys"]), (2, 1, 1))
        self.assertFalse(result["automaticMigrationAuthorized"])
        self.assertEqual(before, original)
        output = json.dumps(result)
        for sensitive in ("synthetic", "unknown-sensitive", "Gold", "ProductNoAds", "00100"):
            self.assertNotIn(sensitive, output)

    def test_empty_snapshot_does_not_claim_population_coverage(self):
        data = sample()
        data["players"] = []
        self.assertFalse(privacy.audit(data)["populationCoverageProven"])

    def test_ad_reward_timestamp_is_known_and_preserved(self):
        before = sample()
        before["players"][0]["Data"] = {"GoogleAdMob": {"Value": "synthetic-timestamp", "Permission": "Public"}}
        self.assertEqual(privacy.audit(before)["knownPublicKeys"], 1)
        after = copy.deepcopy(before)
        after["players"][0]["Data"]["GoogleAdMob"]["Permission"] = "Private"
        after["players"][0]["DataVersion"] += 1
        self.assertTrue(privacy.compare(before, after, "postflight")["snapshotComparisonPassed"])
        after["players"][0]["Data"]["GoogleAdMob"]["Value"] = "null"
        self.assertEqual(privacy.compare(before, after, "postflight")["valueMismatches"], 1)

    def test_identical_preflight_does_not_claim_lock(self):
        result = privacy.compare(sample(), sample(), "preflight")
        self.assertTrue(result["snapshotComparisonPassed"])
        self.assertFalse(result["concurrentWritesExcluded"])

    def test_preflight_rejects_version_drift_even_if_values_return_to_original(self):
        after = sample()
        after["players"][0]["DataVersion"] += 2
        self.assertFalse(privacy.compare(sample(), after, "preflight")["snapshotComparisonPassed"])

    def test_postflight_preserves_exact_strings_and_unknown_permissions(self):
        before = sample()
        after = copy.deepcopy(before)
        for key in ("Gold", "GooglePlay"):
            after["players"][0]["Data"][key]["Permission"] = "Private"
        after["players"][0]["DataVersion"] += 1
        self.assertTrue(privacy.compare(before, after, "postflight")["snapshotComparisonPassed"])
        after["players"][0]["Data"]["Gold"]["Value"] = "100"
        self.assertEqual(privacy.compare(before, after, "postflight")["valueMismatches"], 1)

    def test_entitlement_loss_detected(self):
        after = sample()
        after["players"][0]["Data"]["GooglePlay"]["Value"] = ""
        self.assertEqual(privacy.compare(sample(), after, "postflight")["valueMismatches"], 1)

    def test_key_addition_and_removal_detected(self):
        for remove in (True, False):
            after = sample()
            data = after["players"][0]["Data"]
            if remove:
                del data["Gold"]
            else:
                data["new"] = {"Value": "", "Permission": "Private"}
            self.assertEqual(privacy.compare(sample(), after, "postflight")["keySetMismatches"], 1)

    def test_unknown_key_permission_change_detected(self):
        after = sample()
        after["players"][0]["Data"]["unknown-sensitive-key"]["Permission"] = "Private"
        self.assertGreater(privacy.compare(sample(), after, "postflight")["permissionMismatches"], 0)

    def test_postflight_requires_version_advance_for_changed_keys(self):
        after = sample()
        for key in ("Gold", "GooglePlay"):
            after["players"][0]["Data"][key]["Permission"] = "Private"
        self.assertEqual(privacy.compare(sample(), after, "postflight")["versionMismatches"], 1)

    def test_private_noop_postflight(self):
        data = sample()
        for value in data["players"][0]["Data"].values():
            value["Permission"] = "Private"
        self.assertTrue(privacy.compare(data, copy.deepcopy(data), "postflight")["snapshotComparisonPassed"])

    def test_scope_title_and_player_mismatches_rejected(self):
        for field, value in (("scope", "filtered"), ("title", "different"), ("players", [])):
            after = sample()
            after[field] = value
            with self.subTest(field=field), self.assertRaises(privacy.SnapshotError):
                privacy.compare(sample(), after, "preflight")

    def test_invalid_records_rejected(self):
        for value in (None, 100, [], {}):
            data = sample()
            data["players"][0]["Data"]["Gold"]["Value"] = value
            with self.subTest(value=value), self.assertRaises(privacy.SnapshotError):
                privacy.audit(data)
        for permission in (None, "public", "", 0):
            data = sample()
            data["players"][0]["Data"]["Gold"]["Permission"] = permission
            with self.subTest(permission=permission), self.assertRaises(privacy.SnapshotError):
                privacy.audit(data)

    def test_duplicate_players_and_invalid_versions_rejected(self):
        data = sample()
        data["players"].append(copy.deepcopy(data["players"][0]))
        with self.assertRaises(privacy.SnapshotError):
            privacy.audit(data)
        for version in (True, -1, 1.5, 4294967296):
            data = sample()
            data["players"][0]["DataVersion"] = version
            with self.subTest(version=version), self.assertRaises(privacy.SnapshotError):
                privacy.audit(data)

    def test_unknown_wrapper_fields_rejected(self):
        data = sample()
        data["credential"] = "synthetic-secret"
        with self.assertRaises(privacy.SnapshotError):
            privacy.audit(data)

    def test_parser_errors_are_sanitized(self):
        for raw in ('{"sensitive":1,"sensitive":2}', '{"sensitive":NaN}', 'sensitive-invalid-json',
                    '{"sensitive":' + '9' * 5000 + '}'):
            with tempfile.TemporaryDirectory() as folder:
                path = Path(folder) / "private.json"
                path.write_text(raw, encoding="utf-8")
                error = io.StringIO()
                with contextlib.redirect_stderr(error):
                    self.assertEqual(privacy.main([str(path)]), 2)
                self.assertNotIn("sensitive", error.getvalue())
                self.assertNotIn(folder, error.getvalue())

    def test_oversized_and_missing_input_errors_are_sanitized(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "private.json"
            for exists in (False, True):
                if exists:
                    path.write_bytes(b" " * (privacy.MAX_BYTES + 1))
                error = io.StringIO()
                with contextlib.redirect_stderr(error):
                    self.assertEqual(privacy.main([str(path)]), 2)
                self.assertNotIn(folder, error.getvalue())

    def test_cli_comparison_failure_exit_and_input_unchanged(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "before.json"
            other = Path(folder) / "after.json"
            raw = json.dumps(sample())
            path.write_text(raw, encoding="utf-8")
            after = sample()
            after["players"][0]["DataVersion"] += 1
            other.write_text(json.dumps(after), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(privacy.main([str(path), "--compare", str(other), "--phase", "preflight"]), 1)
            self.assertEqual(path.read_text(encoding="utf-8"), raw)


if __name__ == "__main__":
    unittest.main()
