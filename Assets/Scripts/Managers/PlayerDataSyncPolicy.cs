using System;
using System.Collections.Generic;
using System.IO;

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
            if (values.TryGetValue("Gold", out var gold) && !int.TryParse(gold, out _))
                throw new InvalidDataException("Invalid saved gold; recovery is required.");
            foreach (var key in new[] { "Power", "AttackSpeed", "MoveSpeed" })
                if (values.TryGetValue(key, out var value) &&
                    (!float.TryParse(value, out var number) || float.IsNaN(number) || float.IsInfinity(number)))
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
        public void Track(string key, string value) => _pending[key] = value;
        public Dictionary<string, string> Snapshot() => new Dictionary<string, string>(_pending);
        public void Clear() => _pending.Clear();
        public void Acknowledge(Dictionary<string, string> submitted)
        {
            foreach (var entry in submitted)
                if (_pending.TryGetValue(entry.Key, out var current) && current == entry.Value)
                    _pending.Remove(entry.Key);
        }
    }
}
