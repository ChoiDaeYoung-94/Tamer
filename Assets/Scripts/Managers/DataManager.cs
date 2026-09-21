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
    public partial class DataManager : MonoBehaviour
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
        private const string JournalKey = "__TamerPendingJournal";
        private string _localOwner = string.Empty;
        private bool _sessionBackupCreated;
        private Dictionary<string, string> _defaults;
        private ServerManager _server;
        private PlayerDataChanges _changes = new PlayerDataChanges();
        private int _accountGeneration;
        private CancellationTokenSource _ctsLocalDataUpdate;
        private bool _initialized;
        private bool _shutdown;
        public bool DeletionInProgress { get; private set; }
        private bool _deletionSignedOut;
        public int AccountGeneration => _accountGeneration;
        public int DeletionEpoch { get; private set; }
        private bool _readyBeforeDeletion;
        public const string DeletionLoginPauseKey = "AD_DeletionAcceptedNeedsLogin";

        public AD.Privacy.DeletionSession DeletionSession() => string.IsNullOrEmpty(PlayFabId) ? null
            : new AD.Privacy.DeletionSession(this, PlayFabId, _accountGeneration.ToString());

        public void BeginDeletionSubmission(AD.Privacy.DeletionSession session)
        {
            if (session == null || !session.Matches(DeletionSession())) throw new InvalidOperationException();
            if (!DeletionInProgress)
            {
                _readyBeforeDeletion = IsServerDataReady;
                DeletionEpoch++;
            }
            DeletionInProgress = true;
            IsServerDataReady = false;
            _server?.CancelPendingRequests();
        }

        // Only a validated terminal cancellation proves no submission can still occur.
        public void FinishCancelledDeletion(AD.Privacy.DeletionSession session)
        {
            if (session == null || !session.Matches(DeletionSession())) throw new InvalidOperationException();
            if (!DeletionInProgress) return;
            DeletionInProgress = false;
            IsServerDataReady = _readyBeforeDeletion;
        }

        public void FinishAcceptedDeletion(AD.Privacy.DeletionSession session)
        {
            if (session == null || !session.Matches(DeletionSession())) throw new InvalidOperationException();
            if (!string.IsNullOrEmpty(PlayFab.PlayFabSettings.staticPlayer.PlayFabId) &&
                PlayFab.PlayFabSettings.staticPlayer.PlayFabId != session.AccountId) throw new InvalidOperationException();
            // Invalidate writes/callbacks and credentials even if a local disk operation fails.
            SuspendAccountSession();
            PlayFab.PlayFabSettings.staticPlayer.ForgetAllCredentials();
            PlayerPrefs.SetInt(DeletionLoginPauseKey, 1);
            PlayerPrefs.Save();
            PlayFabId = string.Empty;
            _deletionSignedOut = true;
            PlayFabPlayerData = null;
            try
            {
                if (!string.IsNullOrEmpty(_playerDataPath) && File.Exists(_playerDataPath))
                {
                    var stored = ParseData(File.ReadAllText(_playerDataPath));
                    if (stored.TryGetValue(OwnerKey, out var owner) && owner == session.AccountId)
                    {
                        // Keep account-bound entitlement evidence outside active progress; never grant it to a new account.
                        if (stored.TryGetValue("GooglePlay", out var entitlement) && !string.IsNullOrEmpty(entitlement))
                        {
                            var evidence = new Dictionary<string, string> { [OwnerKey] = owner, ["GooglePlay"] = entitlement };
                            WriteAtomically(_playerDataPath + ".deletion-entitlement-" + Guid.NewGuid().ToString("N"), Utility.SerializeToJson(evidence));
                        }
                        File.Delete(_playerDataPath);
                    }
                }
            }
            finally
            {
                DeletionInProgress = false;
                if (!string.IsNullOrEmpty(_playerDataPath) && File.Exists(_playerDataPath))
                    LoadStoredData(); // Preserve the owner fence of an untouched foreign/legacy file for subsequent login.
                else
                {
                    LocalPlayerData = _defaults == null ? new Dictionary<string, string>() : new Dictionary<string, string>(_defaults);
                    _localOwner = string.Empty;
                    _changes = new PlayerDataChanges();
                }
            }
        }

        public void InitializeData()
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(DataManager));
            if (_initialized) return;
            _server = Managers.ServerM ?? throw new InvalidOperationException("Server service is not initialized.");
            LoadPlayerData();
            MonsterData = Utility.DeserializeFromJson(Managers.ResourceM.Load<TextAsset>("DataManager", "Data/MonstersData").ToString()) as Dictionary<string, object>;
            ItemData = Utility.DeserializeFromJson(Managers.ResourceM.Load<TextAsset>("DataManager", "Data/ItemsData").ToString()) as Dictionary<string, object>;
            _ctsLocalDataUpdate?.Cancel();
            _ctsLocalDataUpdate?.Dispose();
            _ctsLocalDataUpdate = new CancellationTokenSource();
            _initialized = true;
            PeriodicLocalDataUpdateAsync(_ctsLocalDataUpdate.Token).Forget();
        }

        private void LoadPlayerData()
        {
#if TAMER_IAP_HARNESS
            _playerDataPath = RevivalIapIsolation.CreateSavePath(Application.persistentDataPath);
#elif TAMER_GAMEPLAY_HARNESS
            _playerDataPath = RevivalGameplayIsolation.CreateSavePath(Application.persistentDataPath);
#else
            _playerDataPath = Path.Combine(Application.persistentDataPath, "PlayerData.json");
#endif
            _defaults = ParseData(Managers.ResourceM.Load<TextAsset>("DataManager", "Data/PlayerData").ToString());
            LoadStoredData();
        }

        private void LoadStoredData()
        {
            // Never rewrite a legacy or malformed save during initialization.
            var local = File.Exists(_playerDataPath)
                ? ParseData(File.ReadAllText(_playerDataPath))
                : new Dictionary<string, string>(_defaults);
            var owner = local.TryGetValue(OwnerKey, out var storedOwner) ? storedOwner : string.Empty;
            var changes = local.TryGetValue(JournalKey, out var journal)
                ? PlayerDataChanges.Deserialize(journal, owner, local) : new PlayerDataChanges();
            local.Remove(OwnerKey);
            local.Remove(JournalKey);
            foreach (var entry in _defaults)
                if (!local.ContainsKey(entry.Key)) local.Add(entry.Key, entry.Value);
            LocalPlayerData = local;
            _localOwner = owner;
            _changes = changes;
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
            if (DeletionInProgress) throw new InvalidOperationException("Deletion submission is unresolved.");
            if (_shutdown) throw new ObjectDisposedException(nameof(DataManager));
            SuspendAccountSession();
            if (!PlayerDataSyncPolicy.CanBindAccount(_localOwner, playFabId))
                throw new InvalidOperationException("The local save belongs to another account. Account recovery is required.");
            PlayFabId = playFabId;
            _deletionSignedOut = false;
            PlayFabPlayerData = null;
            IsConflict = false;
            _sessionBackupCreated = false;
        }

        public void SuspendAccountSession()
        {
            _accountGeneration++;
            IsServerDataReady = false;
            _server?.CancelPendingRequests();
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
            // Keep the legacy void API's failure visible to callers (notably old IAP callbacks).
            if (!IsServerDataReady) throw new InvalidOperationException("Account data is not ready for local writes.");
            if (!TryUpdateLocalData(key, value)) throw new IOException("Player data could not be persisted.");
        }

        /// <summary>Durable local mutation, including purchase restoration before Player exists.</summary>
        public bool TryUpdateLocalData(string key, string value)
        {
            if (_shutdown || !IsServerDataReady || string.IsNullOrEmpty(_localOwner) || _localOwner != PlayFabId
                || LocalPlayerData == null || string.IsNullOrEmpty(key) || key == OwnerKey || key == JournalKey || value == null)
                return false;
            if (key == "GooglePlay")
            {
                LocalPlayerData.TryGetValue(key, out var entitlement);
                value = PlayerDataSyncPolicy.UnionEntitlements(entitlement, value);
            }
            bool existed = LocalPlayerData.TryGetValue(key, out var previous);
            var candidate = _changes.Clone();
            var local = new Dictionary<string, string>(LocalPlayerData) { [key] = value };
            try
            {
                // Repeated entitlement grants must also remain uploadable after a restart.
                if (!existed || previous != value || key == "GooglePlay") candidate.Track(key, value);
                WritePlayerData(local, _localOwner, candidate);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException)
            {
                Debug.LogWarning("[Tamer/Data] Local save failed; pending purchase or data must be retried.");
                return false;
            }
            LocalPlayerData[key] = value;
            _changes = candidate;
            return true;
        }

        /// <summary>True only after ProductNoAds is durable. Cloud failure does not revoke that grant.</summary>
        public bool TryGrantNoAds()
        {
            if (!TryUpdateLocalData("GooglePlay", "ProductNoAds")) return false;
            UpdatePlayerData();
            return true;
        }

        public void SaveLocalData()
        {
            if (DeletionInProgress || _deletionSignedOut) throw new InvalidOperationException("No writable account session.");
            if (_shutdown) throw new ObjectDisposedException(nameof(DataManager));
            if (LocalPlayerData == null || string.IsNullOrEmpty(_playerDataPath))
                throw new InvalidOperationException("Player data has not been initialized.");
            WritePlayerData(LocalPlayerData, _localOwner);
        }

        private void WritePlayerData(Dictionary<string, string> data, string owner, PlayerDataChanges changes = null)
        {
            var stored = new Dictionary<string, string>(data);
            if (string.IsNullOrEmpty(owner) && (changes ?? _changes).Snapshot().Count != 0)
                throw new InvalidDataException("Pending changes require an account owner.");
            if (!string.IsNullOrEmpty(owner))
            {
                stored[OwnerKey] = owner;
                stored[JournalKey] = (changes ?? _changes).Serialize(owner);
            }
            WriteAtomically(_playerDataPath, Utility.SerializeToJson(stored));
        }

        private static void WriteAtomically(string path, string contents)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, contents);
#if UNITY_EDITOR || TAMER_JOURNAL_HARNESS
                JournalWriteCheckpoint?.Invoke(path, temporary, "temporary-closed");
#endif
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
#if UNITY_EDITOR || TAMER_JOURNAL_HARNESS
                JournalWriteCheckpoint?.Invoke(path, temporary, "replaced");
#endif
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public void UpdatePlayerData()
        {
            if (DeletionInProgress || _deletionSignedOut) return;
            if (_shutdown) return;
            if (!IsServerDataReady)
            {
                _server.GetAllData(update: true);
                return;
            }
            var patch = _changes.Snapshot();
            var revisions = _changes.SnapshotRevisions();
            if (patch.Count == 0)
            {
                _server.GetAllData(update: true);
                return;
            }
            // Match mutation revisions as well as values, including an A -> B -> A change in flight.
            var generation = _accountGeneration;
            _server.SetData(patch, getAllData: true, update: true,
                onWritten: () =>
                {
                    if (!_shutdown && generation == _accountGeneration) AcknowledgeChanges(patch, revisions);
                });
        }

        private void AcknowledgeChanges(Dictionary<string, string> patch, Dictionary<string, long> revisions)
        {
            var candidate = _changes.Clone();
            candidate.Acknowledge(patch, revisions);
            WritePlayerData(LocalPlayerData, _localOwner, candidate);
            _changes = candidate;
        }

        /// <summary>Called only for a successful server read. No write requests originate here.</summary>
        public void UpdateData()
        {
            if (DeletionInProgress || _deletionSignedOut) return;
            if (_shutdown) throw new ObjectDisposedException(nameof(DataManager));
            if (PlayFabPlayerData == null || LocalPlayerData == null || _defaults == null)
                throw new InvalidOperationException("A successful server snapshot is required.");
            var server = new Dictionary<string, string>();
            foreach (var entry in PlayFabPlayerData)
            {
                if (entry.Key == OwnerKey || entry.Key == JournalKey) throw new InvalidDataException("Reserved local metadata appeared in the server snapshot.");
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
            PlayerDataSyncPolicy.ValidateAllyMonsters(merged, MonsterData?.Keys);
            // A legacy file is bound on the first successful read; it is never used as a cloud patch.
            // Ownership and values are committed together in one atomic JSON replacement.
            WritePlayerData(merged, PlayFabId);
            LocalPlayerData = merged;
            _localOwner = PlayFabId;
            IsServerDataReady = true;
            IsConflict = false;
        }

        public void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            SuspendAccountSession();
            _ctsLocalDataUpdate?.Cancel();
            _ctsLocalDataUpdate?.Dispose();
            _ctsLocalDataUpdate = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
