#if UNITY_EDITOR || TAMER_IAP_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AD
{
    public partial class DataManager
    {
        // A recovery boot never activates the original account, timers, gameplay or store writers.
        internal bool CanPrepareIapRecovery => _initialized && !_shutdown && !ReceiptOnly &&
            !IsServerDataReady && string.IsNullOrEmpty(PlayFabId) && !DeletionInProgress && !_deletionSignedOut &&
            !EquipmentBlocksWrites && !_allyPurchaseWriting && !_allyPurchaseReloadRequired &&
            _equipmentRecord == null && _server != null && !_server.IsInProgress &&
            (string.IsNullOrEmpty(_localOwner) || !AD.Privacy.DeletionRecoveryGuard.IsPending(_localOwner));

        internal Dictionary<string, string> IapRecoveryDefaults() => new Dictionary<string, string>(_defaults);

        // Diagnostic reads never bind a session, grant an entitlement or repair a file.
        internal static bool TryReadIapNoAdsFile(string path, string expectedOwner, out bool present)
        {
            present = false;
            if (string.IsNullOrWhiteSpace(expectedOwner) || string.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
                return false;
            try
            {
                RevivalIapRecoveryProfile.CheckAncestors(Path.GetDirectoryName(path));
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length <= 0 || stream.Length > 1024 * 1024) return false;
                    using (var reader = new StreamReader(stream))
                    {
                        var stored = ParseData(reader.ReadToEnd());
                        if (!stored.TryGetValue(OwnerKey, out var owner) || owner != expectedOwner) return false;
                        stored.TryGetValue("GooglePlay", out var values);
                        present = HasIapNoAds(values);
                        return true;
                    }
                }
            }
            catch (Exception) { return false; } // Do not display paths, account IDs or raw JSON.
        }

        private static bool HasIapNoAds(string values) => !string.IsNullOrEmpty(values) &&
            values.Split(',').Any(value => value.Trim() == "ProductNoAds");

        internal string ReadIapNoAdsObservation()
        {
            if (!_initialized || _shutdown || ReceiptOnly || !IsServerDataReady || DeletionInProgress ||
                _deletionSignedOut || LocalPlayerData == null || string.IsNullOrEmpty(_localOwner) ||
                _localOwner != PlayFabId) return "No Ads local: unavailable (authenticate first)";
            if (!TryReadIapNoAdsFile(_playerDataPath, _localOwner, out var saved))
                return "No Ads local: unavailable (read failed; files preserved)";
            LocalPlayerData.TryGetValue("GooglePlay", out var current);
            return "No Ads local: memory=" + (HasIapNoAds(current) ? "present" : "absent") +
                "; disk=" + (saved ? "present" : "absent") + "; owner=matched";
        }

        internal void InitializeIapRecovery(string path, ServerManager server, string persistentRoot)
        {
            if (_initialized || _shutdown) throw new InvalidOperationException("Fresh recovery manager required.");
            _playerDataPath = path;
            _server = server ?? throw new ArgumentNullException(nameof(server));
            RevivalIapRecoveryProfile.CheckAncestors(Path.GetDirectoryName(path));
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked original progress; preserve and stop.");
            _defaults = ParseData(Resources.Load<TextAsset>("Data/PlayerData").text);
            LoadStoredData(); // Keep owner/journal/equipment parsing and preservation guards.
            if (File.Exists(path) && (string.IsNullOrWhiteSpace(_localOwner) || _localOwner == "null"))
                throw new InvalidDataException("Ownerless original progress; preserve and stop.");
            MonsterData = Utility.DeserializeFromJson(Resources.Load<TextAsset>("Data/MonstersData").text) as Dictionary<string, object>;
            ItemData = Utility.DeserializeFromJson(Resources.Load<TextAsset>("Data/ItemsData").text) as Dictionary<string, object>;
            RevivalIapRecoveryProfile.CheckNoDeletionRecovery(persistentRoot);
            string inventoryDirectory = path + ".inventory";
            RevivalIapRecoveryProfile.CheckAncestors(inventoryDirectory);
            if (Directory.Exists(inventoryDirectory))
            {
                if (string.IsNullOrWhiteSpace(_localOwner)) throw new InvalidDataException("Unbound original inventory; preserved.");
                EnsureInventoryStore();
                string expectedPath = _inventoryStore.PathFor(_localOwner);
                if (Directory.GetFileSystemEntries(inventoryDirectory).Any(entry => entry != expectedPath))
                    throw new InvalidDataException("Unexpected original inventory files; preserved.");
                if (File.Exists(expectedPath) && (File.GetAttributes(expectedPath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked original inventory; preserved.");
                _inventoryStore.ReadEnvelope(_localOwner); // Read-only; no BindSession/Load writes.
            }
            _initialized = true;
            // No periodic writer, game service initialization, or remote UserData transport.
        }
    }
}
#endif
