#if UNITY_EDITOR || TAMER_DELETION_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AD
{
    public partial class DataManager
    {
        public void InitializeDeletionTrial()
        {
            if (Application.identifier != RevivalDeletionTrialHarness.ApplicationId || !Debug.isDebugBuild)
                throw new InvalidOperationException("Isolated deletion trial required.");
            _server = Managers.ServerM;
            _playerDataPath = Path.Combine(Application.persistentDataPath, "DeletionTrialPlayer.json");
            _defaults = new Dictionary<string, string>();
            LocalPlayerData = new Dictionary<string, string>();
            if (File.Exists(_playerDataPath))
            {
                LocalPlayerData = ParseData(File.ReadAllText(_playerDataPath));
                if (!LocalPlayerData.TryGetValue(OwnerKey, out _localOwner)) throw new InvalidDataException();
            }
            _initialized = true; // No gameplay, resource loading, periodic writes, ads or IAP initialization.
        }

        public void BindDeletionTrial(string account)
        {
            BeginAccountSession(account); // Includes the production pending-journal guard.
            if (DeletionInProgress) return;
            if (!File.Exists(_playerDataPath))
            {
                LocalPlayerData = new Dictionary<string, string> { [OwnerKey] = account, ["TrialMarker"] = "disposable" };
                WriteAtomically(_playerDataPath, Utility.SerializeToJson(LocalPlayerData));
            }
            _localOwner = account;
            IsServerDataReady = true;
        }

        public bool DeletionTrialLocalExists => File.Exists(_playerDataPath);

        // Only synthetic files below this debug app's private scenario directory are selected.
        public string BindOfflineDeletionTrial(string phase, string account)
        {
            if (Application.identifier != RevivalDeletionTrialHarness.ApplicationId || !Debug.isDebugBuild ||
                (phase != "accepted" && phase != "unknown") || account != "synthetic-" + phase ||
                !string.IsNullOrEmpty(PlayFab.PlayFabSettings.staticPlayer.ClientSessionTicket))
                throw new InvalidOperationException("Offline isolated scenario required.");
            string directory = Path.Combine(Application.persistentDataPath, "OfflineDeletionChecks", phase);
            Directory.CreateDirectory(directory);
            _playerDataPath = Path.Combine(directory, "PlayerData.json");
            _localOwner = "";
            if (File.Exists(_playerDataPath))
            {
                var stored = ParseData(File.ReadAllText(_playerDataPath));
                if (!stored.TryGetValue(OwnerKey, out _localOwner)) throw new InvalidDataException();
            }
            BindDeletionTrial(account);
            return directory;
        }

        public string BindOfflineTrialInventory(string account)
        {
            if (Application.identifier != RevivalDeletionTrialHarness.ApplicationId || !Debug.isDebugBuild ||
                account != "synthetic-accepted" || PlayFabId != account || !IsServerDataReady)
                throw new InvalidOperationException();
            var inventory = new PlayerInventoryStore(_playerDataPath + ".inventory", new[] { "Bat" },
                new Dictionary<string, string> { { "SimpleSword", "Sword" } });
            inventory.BindSession(account, _inventorySession);
            return inventory.PathFor(account);
        }
    }
}
#endif
