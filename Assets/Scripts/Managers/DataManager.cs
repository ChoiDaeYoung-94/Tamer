using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using PlayFab.ClientModels;
using Cysharp.Threading.Tasks;

namespace AD
{
    /// <summary>Login restores the server snapshot; gameplay uploads only explicitly changed keys.</summary>
    public class DataManager : MonoBehaviour
    {
        public Dictionary<string, UserDataRecord> PlayFabPlayerData;
        public Dictionary<string, string> LocalPlayerData;
        public Dictionary<string, object> MonsterData;
        public Dictionary<string, object> ItemData;
        public string PlayFabId { get; private set; } = string.Empty;
        public bool IsServerDataReady { get; private set; }
        public bool HasKnownAccount => !string.IsNullOrEmpty(_localOwner) || !string.IsNullOrEmpty(PlayFabId);
        // Retained for existing serialized scenes. Conflicts no longer trigger a bulk upload.
        public bool IsConflict;

        private string _playerDataPath = string.Empty;
        private const string OwnerKey = "__TamerAccountOwner";
        private string _localOwner = string.Empty;
        private bool _sessionBackupCreated;
        private Dictionary<string, string> _defaults;
        private readonly PlayerDataChanges _changes = new PlayerDataChanges();
        private CancellationTokenSource _ctsLocalDataUpdate;

        public void InitializeData()
        {
            LoadPlayerData();
            MonsterData = Utility.DeserializeFromJson(Managers.ResourceM.Load<TextAsset>("DataManager", "Data/MonstersData").ToString()) as Dictionary<string, object>;
            ItemData = Utility.DeserializeFromJson(Managers.ResourceM.Load<TextAsset>("DataManager", "Data/ItemsData").ToString()) as Dictionary<string, object>;
            _ctsLocalDataUpdate?.Cancel();
            _ctsLocalDataUpdate?.Dispose();
            _ctsLocalDataUpdate = new CancellationTokenSource();
            PeriodicLocalDataUpdateAsync(_ctsLocalDataUpdate.Token).Forget();
        }

        private void LoadPlayerData()
        {
            _playerDataPath = Path.Combine(Application.persistentDataPath, "PlayerData.json");
            _defaults = ParseData(Managers.ResourceM.Load<TextAsset>("DataManager", "Data/PlayerData").ToString());
            // Never rewrite a legacy or malformed save during initialization.
            LocalPlayerData = File.Exists(_playerDataPath)
                ? ParseData(File.ReadAllText(_playerDataPath))
                : new Dictionary<string, string>(_defaults);
            foreach (var entry in _defaults)
                if (!LocalPlayerData.ContainsKey(entry.Key)) LocalPlayerData.Add(entry.Key, entry.Value);
            _localOwner = LocalPlayerData.TryGetValue(OwnerKey, out var owner) ? owner : string.Empty;
            LocalPlayerData.Remove(OwnerKey);
        }

        private static Dictionary<string, string> ParseData(string json)
        {
            var parsed = Utility.DeserializeFromJson(json) as Dictionary<string, object>;
            if (parsed == null) throw new InvalidDataException("Player data is not an object; original file preserved.");
            var result = new Dictionary<string, string>();
            foreach (var entry in parsed) result.Add(entry.Key, entry.Value?.ToString() ?? "null");
            return result;
        }

        /// <summary>Bind only after authentication. An owner mismatch requires explicit account recovery.</summary>
        public void BeginAccountSession(string playFabId)
        {
            SuspendAccountSession();
            if (!PlayerDataSyncPolicy.CanBindAccount(_localOwner, playFabId))
                throw new InvalidOperationException("The local save belongs to another account. Account recovery is required.");
            PlayFabId = playFabId;
            PlayFabPlayerData = null;
            IsConflict = false;
            _sessionBackupCreated = false;
            _changes.Clear();
        }

        public void SuspendAccountSession()
        {
            IsServerDataReady = false;
            Managers.ServerM.CancelPendingRequests();
        }

        private async UniTask PeriodicLocalDataUpdateAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(60), cancellationToken: token).SuppressCancellationThrow()) return;
                UpdateLocalData("null", "null", updateAll: true);
            }
        }

        public void UpdateLocalData(string key, string value, bool updateAll = false)
        {
            if (updateAll)
            {
                if (Player.Instance) TryUpdateLocalData("Gold", Player.Instance.Gold.ToString());
                return;
            }
            TryUpdateLocalData(key, value);
        }

        /// <summary>Durable local mutation, including purchase restoration before Player exists.</summary>
        public bool TryUpdateLocalData(string key, string value)
        {
            if (!IsServerDataReady || LocalPlayerData == null || string.IsNullOrEmpty(key) || key == OwnerKey || value == null)
                return false;
            if (key == "GooglePlay")
            {
                LocalPlayerData.TryGetValue(key, out var entitlement);
                value = PlayerDataSyncPolicy.UnionEntitlements(entitlement, value);
            }
            bool existed = LocalPlayerData.TryGetValue(key, out var previous);
            LocalPlayerData[key] = value;
            try { SaveLocalData(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (existed) LocalPlayerData[key] = previous;
                else LocalPlayerData.Remove(key);
                Debug.LogWarning("[Tamer/Data] Local save failed; pending purchase or data must be retried.");
                return false;
            }
            if (!existed || previous != value) _changes.Track(key, value);
            return true;
        }

        /// <summary>True only after ProductNoAds is durable. Cloud failure does not revoke that grant.</summary>
        public bool TryGrantNoAds()
        {
            if (!TryUpdateLocalData("GooglePlay", "ProductNoAds")) return false;
            _changes.Track("GooglePlay", LocalPlayerData["GooglePlay"]);
            UpdatePlayerData();
            return true;
        }

        public void SaveLocalData()
        {
            if (LocalPlayerData == null || string.IsNullOrEmpty(_playerDataPath))
                throw new InvalidOperationException("Player data has not been initialized.");
            WritePlayerData(LocalPlayerData, _localOwner);
        }

        private void WritePlayerData(Dictionary<string, string> data, string owner)
        {
            var stored = new Dictionary<string, string>(data);
            if (!string.IsNullOrEmpty(owner)) stored[OwnerKey] = owner;
            WriteAtomically(_playerDataPath, Utility.SerializeToJson(stored));
        }

        private static void WriteAtomically(string path, string contents)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, contents);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public void UpdatePlayerData()
        {
            if (!IsServerDataReady)
            {
                Managers.ServerM.GetAllData(update: true);
                return;
            }
            var patch = _changes.Snapshot();
            if (patch.Count == 0)
            {
                Managers.ServerM.GetAllData(update: true);
                return;
            }
            // Acknowledge only the submitted values. Newer changes made in flight remain pending.
            Managers.ServerM.SetData(patch, getAllData: true, update: true,
                onWritten: () => _changes.Acknowledge(patch));
        }

        /// <summary>Called only for a successful server read. No write requests originate here.</summary>
        public void UpdateData()
        {
            if (PlayFabPlayerData == null || LocalPlayerData == null || _defaults == null)
                throw new InvalidOperationException("A successful server snapshot is required.");
            var server = new Dictionary<string, string>();
            foreach (var entry in PlayFabPlayerData)
            {
                if (entry.Key == OwnerKey) throw new InvalidDataException("Reserved local metadata appeared in the server snapshot.");
                if (entry.Value == null || entry.Value.Value == null)
                    throw new InvalidDataException("Incomplete server record; local save preserved.");
                server.Add(entry.Key, entry.Value.Value);
            }
            if (!PlayerDataSyncPolicy.CanBindAccount(_localOwner, PlayFabId))
                throw new InvalidOperationException("Local account mismatch; save preserved.");
            if (!_sessionBackupCreated)
            {
                if (File.Exists(_playerDataPath))
                {
                    var directory = Path.Combine(Path.GetDirectoryName(_playerDataPath), "PlayerDataBackups");
                    Directory.CreateDirectory(directory);
                    File.Copy(_playerDataPath, Path.Combine(directory, "PlayerData-" + Guid.NewGuid().ToString("N") + ".json"), false);
                }
                _sessionBackupCreated = true;
            }
            var merged = PlayerDataSyncPolicy.Merge(_defaults, LocalPlayerData, server, _changes.Snapshot());
            // A legacy file is bound on the first successful read; it is never used as a cloud patch.
            // Ownership and values are committed together in one atomic JSON replacement.
            WritePlayerData(merged, PlayFabId);
            LocalPlayerData = merged;
            _localOwner = PlayFabId;
            IsServerDataReady = true;
            IsConflict = false;
        }

        private void OnDestroy()
        {
            _ctsLocalDataUpdate?.Cancel();
            _ctsLocalDataUpdate?.Dispose();
        }
    }
}
