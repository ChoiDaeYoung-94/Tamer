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
