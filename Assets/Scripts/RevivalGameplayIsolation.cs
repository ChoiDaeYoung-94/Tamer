#if UNITY_EDITOR || TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using PlayFab.ClientModels;

namespace AD
{
    /// <summary>Compiled into the explicitly isolated gameplay APK, never the normal player.</summary>
    public static class RevivalGameplayIsolation
    {
        public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.gameplay";
        public const string PlayerRestoreApplicationId = "com.AeDeong.MonsterTamer.revival.playerrestore";
        public const string SessionApplicationId = "com.AeDeong.MonsterTamer.revival.sessionguard";
        public const string InventoryRestoreApplicationId = "com.AeDeong.MonsterTamer.revival.inventoryrestore";
        public const string AgeChoiceApplicationId = "com.AeDeong.MonsterTamer.revival.agechoice";
        public static string RuntimeApplicationId =>
#if TAMER_INVENTORY_RESTORE
            InventoryRestoreApplicationId;
#elif TAMER_SESSION_HARNESS
            SessionApplicationId;
#elif TAMER_AGE_CHOICE
            AgeChoiceApplicationId;
#else
#if TAMER_PLAYER_RESTORE
            PlayerRestoreApplicationId;
#else
            ApplicationId;
#endif
#endif

        public static void ValidatePlayerRestore(string applicationId, bool editor, Func<string, bool> hasKey)
        {
            ValidatePrivatePreferences(applicationId, editor, hasKey, PlayerRestoreApplicationId);
        }

        public static void ValidateAgeChoice(string applicationId, bool editor, Func<string, bool> hasKey)
        {
            ValidatePrivatePreferences(applicationId, editor, hasKey, AgeChoiceApplicationId);
        }

        private static void ValidatePrivatePreferences(string applicationId, bool editor, Func<string, bool> hasKey, string expectedId)
        {
            if (editor || applicationId != expectedId)
                throw new InvalidOperationException("Separate isolated application required.");
            foreach (var key in new[] { "AllyMonsters", "playerEquippedItems", "LocalItem" })
                if (hasKey(key)) throw new InvalidOperationException("Existing legacy preferences; preserve and stop.");
        }

#if UNITY_EDITOR || TAMER_PLAYER_RESTORE
        public static Dictionary<string, string> PlayerRestoreSeed()
        {
            var values = RevivalGameSaveSchema.Fixture(2);
            values["GoogleAdMob"] = "null";
            values["GooglePlay"] = "";
            return values;
        }
#endif
        public const string AccountId = "revival-offline-gameplay";
        public static int Reads { get; private set; }
        public static int Writes { get; private set; }
        public static int BlockedLogins { get; private set; }
        public static int BlockedPurchases { get; private set; }
        public static string SavePath { get; private set; }
        public static string ReadApplyStage { get; private set; } = "not-started";

        public static void SetReadApplyStage(string stage) => ReadApplyStage = stage;

        public static void BlockLogin() => BlockedLogins++;
        public static void BlockPurchase() => BlockedPurchases++;

        public static bool IsOfflinePlayerConnectionDiagnostic(string applicationId, bool development,
            bool editor, string message, UnityEngine.LogType type)
        {
            if (editor || !development || type != UnityEngine.LogType.Error ||
                (applicationId != ApplicationId && applicationId != PlayerRestoreApplicationId &&
                 applicationId != SessionApplicationId && applicationId != InventoryRestoreApplicationId &&
                 applicationId != AgeChoiceApplicationId)) return false;
            // These exact engine diagnostics occur because the offline development APK has
            // no INTERNET permission. Other socket errors and all exceptions still fail.
            return message == "Socket: Failed to set blocking mode" ||
                message == "Unable to setup multicast socket for player connection.";
        }

        public static string CreateSavePath(string persistentRoot)
        {
#if TAMER_INVENTORY_RESTORE
            ValidatePrivatePreferences(UnityEngine.Application.identifier, UnityEngine.Application.isEditor,
                UnityEngine.PlayerPrefs.HasKey, InventoryRestoreApplicationId);
            // Only this separate offline application reopens its own previous process's inventory.
            string directory = Path.Combine(persistentRoot, "RevivalInventoryRestore");
#else
            string directory = Path.Combine(persistentRoot, "RevivalGameplay", Guid.NewGuid().ToString("N"));
#endif
            Directory.CreateDirectory(directory);
            SavePath = Path.Combine(directory, "PlayerData.json");
            return SavePath;
        }

        public static Dictionary<string, string> Seed() => new Dictionary<string, string>
        {
            ["NickName"] = "OfflineHarness", ["Sex"] = "Man", ["Tutorial"] = "done",
            ["GoogleAdMob"] = "null", ["Gold"] = "1000", ["Power"] = "10",
            ["AttackSpeed"] = "0.5", ["MoveSpeed"] = "3.0", ["AllyMonsters"] = "null",
            ["GooglePlay"] = ""
        };

        private static ServerManager _gameplayServer;
        public static ServerManager CreateServer(DataManager owner)
        {
#if TAMER_AGE_CHOICE
            ValidateAgeChoice(UnityEngine.Application.identifier, UnityEngine.Application.isEditor, UnityEngine.PlayerPrefs.HasKey);
#endif
#if TAMER_PLAYER_RESTORE
            ValidatePlayerRestore(UnityEngine.Application.identifier, UnityEngine.Application.isEditor, UnityEngine.PlayerPrefs.HasKey);
#endif
            _gameplayServer = CreateTestServer(owner, () => AccountId);
            return _gameplayServer;
        }

#if TAMER_GAMEPLAY_HARNESS
        public static bool AllowsCaptureAssist => !UnityEngine.Application.isEditor &&
            (UnityEngine.Application.identifier == ApplicationId || UnityEngine.Application.identifier == SessionApplicationId) && Managers.Instance != null &&
            Managers.DataM.PlayFabId == AccountId && Managers.DataM.IsServerDataReady &&
            _gameplayServer != null && ReferenceEquals(Managers.ServerM, _gameplayServer);
#endif

        public static ServerManager CreateTestServer(DataManager owner, Func<string> expectedAccount,
            IDictionary<string, string> recoveryDefaults = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var cloud = recoveryDefaults != null ? new Dictionary<string, string>(recoveryDefaults) :
#if TAMER_PLAYER_RESTORE
                PlayerRestoreSeed();
#else
                Seed();
#endif
            return new ServerManager(() => owner != null ? owner.PlayFabId : null,
                () => owner != null && owner.IsServerDataReady,
                (account, success, failure) =>
                {
                    if (string.IsNullOrEmpty(account) || account != expectedAccount?.Invoke()) { failure(403); return; }
                    Reads++;
                    success(new Dictionary<string, string>(cloud));
                },
                (account, patch, success, failure) =>
                {
                    if (string.IsNullOrEmpty(account) || account != expectedAccount?.Invoke()) { failure(403); return; }
                    Writes++;
                    foreach (var entry in patch)
                        if (entry.Value == null) cloud.Remove(entry.Key);
                        else cloud[entry.Key] = entry.Value;
                    success();
                },
                // This transport completes synchronously; no external timer or retry is needed.
                (delay, action) => { },
                (snapshot, update) =>
                {
                    if (owner == null) throw new ObjectDisposedException(nameof(owner));
                    var records = new Dictionary<string, UserDataRecord>();
                    foreach (var entry in snapshot) records.Add(entry.Key, new UserDataRecord { Value = entry.Value });
                    owner.PlayFabPlayerData = records;
                    if (update)
                    {
                        SetReadApplyStage("enter-update");
                        try { owner.UpdateData(); }
                        catch (Exception error)
                        {
                            // Fixed checkpoint + type only: never print exception messages,
                            // account/save values, paths, or the snapshot itself.
                            UnityEngine.Debug.LogWarning("GAMEPLAY_HARNESS SYNTHETIC_READ_FAILED stage=" +
                                ReadApplyStage + " exception=" + error.GetType().Name +
                                " hresult=0x" + error.HResult.ToString("X8"));
                            throw;
                        }
                    }
                });
        }
    }
}
#endif
