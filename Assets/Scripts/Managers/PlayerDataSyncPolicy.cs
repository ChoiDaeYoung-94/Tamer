using System;
using System.Collections.Generic;

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
            return result;
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
