#if UNITY_EDITOR || TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public const string ShopPurchaseApplicationId = "com.AeDeong.MonsterTamer.revival.shoppurchase";
        public const string DeletionGameplayApplicationId = "com.AeDeong.MonsterTamer.revival.deletiongameplayv4";
        public const string PrivacyProductUiApplicationId = "com.AeDeong.MonsterTamer.revival.privacyproductui28";
        public static string RuntimeApplicationId =>
#if TAMER_PRIVACY_PRODUCT_UI_HARNESS
            PrivacyProductUiApplicationId;
#elif TAMER_DELETION_GAMEPLAY
            DeletionGameplayApplicationId;
#elif TAMER_SHOP_HARNESS
            ShopPurchaseApplicationId;
#elif TAMER_INVENTORY_RESTORE
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
#if TAMER_SHOP_HARNESS
#if TAMER_CHARACTER_UI
        internal const string ShopFixtureDirectoryName = "RevivalCharacterManualUi";
#elif TAMER_SHOP_UI
        internal const string ShopFixtureDirectoryName = "RevivalShopManualUi";
#elif TAMER_SHOP_AFTER_INVENTORY
        internal const string ShopFixtureDirectoryName = "RevivalShopAfterInventory";
#else
        internal const string ShopFixtureDirectoryName = "RevivalShopPurchaseV2";
#endif
        internal static bool ShopCloudRestored { get; private set; }
        internal static int ShopCloudInitialGold { get; private set; } = -1;
#endif

        public static void SetReadApplyStage(string stage) => ReadApplyStage = stage;

        public static void BlockLogin() => BlockedLogins++;
        public static void BlockPurchase() => BlockedPurchases++;

        public static bool IsOfflinePlayerConnectionDiagnostic(string applicationId, bool development,
            bool editor, string message, UnityEngine.LogType type)
        {
            if (editor || !development || type != UnityEngine.LogType.Error ||
                (applicationId != ApplicationId && applicationId != PlayerRestoreApplicationId &&
                 applicationId != SessionApplicationId && applicationId != InventoryRestoreApplicationId &&
                 applicationId != AgeChoiceApplicationId && applicationId != ShopPurchaseApplicationId &&
                 applicationId != DeletionGameplayApplicationId && applicationId != PrivacyProductUiApplicationId)) return false;
            // These exact engine diagnostics occur because the offline development APK has
            // no INTERNET permission. Other socket errors and all exceptions still fail.
            return message == "Socket: Failed to set blocking mode" ||
                message == "Unable to setup multicast socket for player connection.";
        }

        public static string CreateSavePath(string persistentRoot)
        {
#if TAMER_DELETION_GAMEPLAY
            string directory = RevivalDeletionGameplayHarness.RequireSaveDirectory(persistentRoot);
#elif TAMER_SHOP_HARNESS
            ValidatePrivatePreferences(UnityEngine.Application.identifier, UnityEngine.Application.isEditor,
                UnityEngine.PlayerPrefs.HasKey, ShopPurchaseApplicationId);
            if (!UnityEngine.Debug.isDebugBuild) throw new InvalidOperationException("Debug shop fixture required.");
            string directory = Path.Combine(persistentRoot, ShopFixtureDirectoryName);
#elif TAMER_INVENTORY_RESTORE
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
#if TAMER_DELETION_GAMEPLAY
            return RevivalDeletionGameplayHarness.CreateServer(owner);
#else
#if TAMER_AGE_CHOICE
            ValidateAgeChoice(UnityEngine.Application.identifier, UnityEngine.Application.isEditor, UnityEngine.PlayerPrefs.HasKey);
#endif
#if TAMER_PLAYER_RESTORE
            ValidatePlayerRestore(UnityEngine.Application.identifier, UnityEngine.Application.isEditor, UnityEngine.PlayerPrefs.HasKey);
#endif
            _gameplayServer = CreateTestServer(owner, () => AccountId);
            return _gameplayServer;
#endif
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
#if TAMER_SHOP_HARNESS
            string cloudPath = null;
            bool shopCloudLoaded = false;
            void EnsureShopCloud()
            {
                if (shopCloudLoaded) return;
                // Managers constructs the server before DataManager creates its private path.
                // First synthetic request runs after initialization has established that path.
                cloudPath = Path.Combine(Path.GetDirectoryName(SavePath), "SyntheticCloud.json");
                if (UnityEngine.Application.identifier != ShopPurchaseApplicationId || !UnityEngine.Debug.isDebugBuild ||
                    Path.GetFileName(Path.GetDirectoryName(cloudPath)) != ShopFixtureDirectoryName)
                    throw new InvalidOperationException("Dedicated synthetic shop cloud required.");
                if (!File.Exists(cloudPath) && File.Exists(Path.Combine(Path.GetDirectoryName(cloudPath), "PendingSword.txt")))
                    throw new InvalidDataException("Synthetic cloud missing after purchase; preserve.");
                if (File.Exists(cloudPath))
                {
                    var stored = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(cloudPath));
                    if (stored == null || stored.Count > 256 || cloud.Keys.Any(key => !stored.ContainsKey(key)) ||
                        !int.TryParse(stored["Gold"], out int savedGold) || savedGold < 0)
                        throw new InvalidDataException("Synthetic cloud structure changed; preserve.");
                    cloud = stored;
                    ShopCloudRestored = true;
                }
                else cloud["Gold"] =
#if TAMER_SHOP_AFTER_INVENTORY
                    "20100";
#else
                    "20000";
#endif
#if TAMER_CHARACTER_UI
                if (!ShopCloudRestored) cloud["Sex"] = ""; // New synthetic profile; original UI must save the choice.
#endif
                ShopCloudInitialGold = int.Parse(cloud["Gold"], System.Globalization.CultureInfo.InvariantCulture);
                shopCloudLoaded = true;
            }
#endif
            return new ServerManager(() => owner != null ? owner.PlayFabId : null,
                () => owner != null && owner.IsServerDataReady,
                (account, success, failure) =>
                {
                    if (string.IsNullOrEmpty(account) || account != expectedAccount?.Invoke()) { failure(403); return; }
#if TAMER_SHOP_HARNESS
                    EnsureShopCloud();
#endif
                    Reads++;
                    success(new Dictionary<string, string>(cloud));
                },
                (account, patch, success, failure) =>
                {
                    if (string.IsNullOrEmpty(account) || account != expectedAccount?.Invoke()) { failure(403); return; }
#if TAMER_SHOP_HARNESS
                    EnsureShopCloud();
#endif
                    Writes++;
                    foreach (var entry in patch)
                        if (entry.Value == null) cloud.Remove(entry.Key);
                        else cloud[entry.Key] = entry.Value;
#if TAMER_SHOP_HARNESS
                    // Persist only this separate fixture's simulated server between processes.
                    // This is not evidence of real remote or multi-device atomicity.
                    string temporary = Path.Combine(Path.GetDirectoryName(cloudPath), ".cloud-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(cloud));
                        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                        if (File.Exists(cloudPath)) File.Replace(temporary, cloudPath, null);
                        else File.Move(temporary, cloudPath);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
#endif
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
