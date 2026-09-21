using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace AD
{
    /// <summary>Missing cloud fields use schema defaults, never an old device snapshot.</summary>
    public static class PlayerDataSyncPolicy
    {
        public static bool CanBindAccount(string owner, string account) => !string.IsNullOrEmpty(account)
            && (string.IsNullOrEmpty(owner) || string.Equals(owner, account, StringComparison.Ordinal));

        public static Dictionary<string, string> Merge(Dictionary<string, string> defaults,
            Dictionary<string, string> local, Dictionary<string, string> server, Dictionary<string, string> pending)
        {
            var result = new Dictionary<string, string>(defaults);
            foreach (var entry in server) result[entry.Key] = entry.Value;
            foreach (var entry in pending) result[entry.Key] = entry.Value;
            local.TryGetValue("GooglePlay", out var localEntitlements);
            server.TryGetValue("GooglePlay", out var serverEntitlements);
            pending.TryGetValue("GooglePlay", out var pendingEntitlements);
            result["GooglePlay"] = UnionEntitlements(UnionEntitlements(serverEntitlements, localEntitlements), pendingEntitlements);
            ValidateGameplayValues(result);
            return result;
        }

        private static void ValidateGameplayValues(Dictionary<string, string> values)
        {
            // Match the existing Player/Creature readers; preserve values instead of guessing repairs.
            if (values.TryGetValue("Gold", out var gold) && !int.TryParse(gold, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                throw new InvalidDataException("Invalid saved gold; recovery is required.");
            foreach (var key in new[] { "Power", "AttackSpeed", "MoveSpeed" })
                if (values.TryGetValue(key, out var value) &&
                    (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || float.IsNaN(number) || float.IsInfinity(number)))
                    throw new InvalidDataException("Invalid saved stat; recovery is required.");
        }

        public static string UnionEntitlements(string first, string second)
        {
            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in new[] { first, second })
            {
                if (string.IsNullOrEmpty(source)) continue;
                foreach (var token in source.Split(','))
                {
                    var value = token.Trim();
                    if (value.Length > 0 && value != "null" && seen.Add(value)) values.Add(value);
                }
            }
            return string.Join(",", values);
        }

        public static void ValidateAllyMonsters(Dictionary<string, string> values, IEnumerable<string> knownNames)
        {
            if (!values.TryGetValue("AllyMonsters", out var allies) || string.IsNullOrEmpty(allies) || allies == "null") return;
            var known = new HashSet<string>(knownNames ?? Array.Empty<string>(), StringComparer.Ordinal);
            // Match Player.SettingAllyMonster's empty-entry handling without changing saved tokens.
            foreach (var name in allies.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (!known.Contains(name)) throw new InvalidDataException("Unknown saved ally; recovery is required.");
        }
    }

    public sealed class PlayerDataChanges
    {
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>();
        private readonly Dictionary<string, long> _revisions = new Dictionary<string, long>();
        private long _revision;
        public PlayerDataChanges Clone()
        {
            var copy = new PlayerDataChanges { _revision = _revision };
            foreach (var entry in _pending) copy._pending.Add(entry.Key, entry.Value);
            foreach (var entry in _revisions) copy._revisions.Add(entry.Key, entry.Value);
            return copy;
        }

        public string Serialize(string owner)
        {
            if (string.IsNullOrEmpty(owner)) throw new InvalidDataException("Pending journal requires an account owner.");
            var entries = new Dictionary<string, object>();
            foreach (var entry in _pending)
                entries.Add(entry.Key, new Dictionary<string, object>
                {
                    { "value", entry.Value },
                    { "revision", _revisions[entry.Key].ToString(CultureInfo.InvariantCulture) }
                });
            return Utility.SerializeToJson(new Dictionary<string, object>
            {
                { "version", "1" }, { "owner", owner },
                { "revision", _revision.ToString(CultureInfo.InvariantCulture) }, { "pending", entries }
            });
        }

        public static PlayerDataChanges Deserialize(string json, string owner, Dictionary<string, string> local)
        {
            var root = Utility.DeserializeFromJson(json) as Dictionary<string, object>;
            if (root == null || root.Count != 4 || !root.TryGetValue("version", out var version) || !(version is string v) || v != "1"
                || string.IsNullOrEmpty(owner) || !root.TryGetValue("owner", out var storedOwner) || !(storedOwner is string o) || o != owner
                || !root.TryGetValue("revision", out var revision) || !TryRevision(revision, out var counter)
                || !root.TryGetValue("pending", out var pending) || !(pending is Dictionary<string, object> entries))
                throw new InvalidDataException("Invalid pending journal; original file preserved.");
            var result = new PlayerDataChanges { _revision = counter };
            var used = new HashSet<long>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Key == "__TamerAccountOwner" || entry.Key == "__TamerPendingJournal"
                    || !(entry.Value is Dictionary<string, object> record) || record.Count != 2
                    || !record.TryGetValue("value", out var value) || !(value is string text)
                    || !record.TryGetValue("revision", out var entryRevision) || !TryRevision(entryRevision, out var number)
                    || number == 0 || number > counter || !used.Add(number)
                    || !local.TryGetValue(entry.Key, out var saved)
                    || (entry.Key == "GooglePlay" ? PlayerDataSyncPolicy.UnionEntitlements(saved, text) != saved : saved != text))
                    throw new InvalidDataException("Invalid pending journal entry; original file preserved.");
                result._pending.Add(entry.Key, text);
                result._revisions.Add(entry.Key, number);
            }
            return result;
        }

        private static bool TryRevision(object value, out long revision)
        {
            revision = 0;
            return value is string text && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out revision)
                && revision >= 0 && revision < long.MaxValue;
        }

        public void Track(string key, string value)
        {
            if (_revision >= long.MaxValue - 1) throw new InvalidDataException("Pending journal revision exhausted.");
            _pending[key] = value;
            _revisions[key] = ++_revision;
        }
        public Dictionary<string, string> Snapshot() => new Dictionary<string, string>(_pending);
        public Dictionary<string, long> SnapshotRevisions() => new Dictionary<string, long>(_revisions);
        public void Clear()
        {
            _pending.Clear();
            _revisions.Clear();
        }
        public void Acknowledge(Dictionary<string, string> submitted, Dictionary<string, long> revisions)
        {
            foreach (var entry in submitted)
                if (_pending.TryGetValue(entry.Key, out var current) && current == entry.Value
                    && revisions.TryGetValue(entry.Key, out var submittedRevision)
                    && _revisions.TryGetValue(entry.Key, out var currentRevision) && currentRevision == submittedRevision)
                {
                    _pending.Remove(entry.Key);
                    _revisions.Remove(entry.Key);
                }
        }
    }
}
