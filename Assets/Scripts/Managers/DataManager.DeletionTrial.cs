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
    }
}
#endif
