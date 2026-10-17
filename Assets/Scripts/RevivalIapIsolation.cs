#if UNITY_EDITOR || TAMER_IAP_HARNESS
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AD.Purchasing;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace AD
{
    /// <summary>Recovery-only isolated boot. Credentials remain in staging memory.</summary>
    public static class RevivalIapIsolation
    {
        public const string ApplicationId = "com.AeDeong.MonsterTamer.iaptest";
        [Serializable] public sealed class Configuration
        {
            public string testTitle;
            public string productionTitle;
            public string catalog;
        }
        private static Configuration _configuration;
        private static RevivalIapRecoveryStaging _staging;
        private static DataManager _stagingOwner;
        private static int _stagingOwnerGeneration;
        private static DateTime _deadline;
        private static bool _preparationAttempted;
        private static readonly RevivalIapRecoveryGate Gate = new RevivalIapRecoveryGate();
        public static ReceiptSession Session { get; private set; }
        public static string Status { get; private set; } = "STOP: two prior receipt failures. Recovery authentication required.";
        public static bool IsStaging => _staging != null;
        public static bool ResumeBuildApproved =>
#if TAMER_IAP_RECOVERY_RESUME_APPROVED && TAMER_IAP_HARNESS
            true;
#else
            false;
#endif
        public static bool CanPrepare => !_preparationAttempted && _staging == null && Session == null &&
            Managers.Instance != null && Managers.Instance.CanPrepareIapRecovery;

        public static void Validate(Configuration config, string applicationId)
        {
            if (config == null || applicationId != ApplicationId)
                throw new InvalidOperationException("Isolated IAP application and configuration required.");
            _ = new PlayFabIapReceiptVerifier(config.testTitle, config.productionTitle, applicationId, config.catalog);
        }
        public static void ValidateRuntime()
        {
            var asset = Resources.Load<TextAsset>("RevivalIapLocal");
            if (asset == null) throw new InvalidOperationException("Missing local IAP test configuration.");
            var config = JsonUtility.FromJson<Configuration>(asset.text);
            Validate(config, Application.identifier);
            if (!string.Equals(config.testTitle, RevivalIapRecoveryProfile.TestTitle, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Exact recovery test title required.");
            bool storeTestBuild = false;
#if TAMER_IAP_STORE_TEST
            storeTestBuild = true;
#endif
            if (Application.isEditor || (!Debug.isDebugBuild && !storeTestBuild))
                throw new InvalidOperationException("Dedicated development player required.");
            _configuration = config;
        }

        // Root is read-only in recovery boot, including identity.txt. Never migrate it.
        public static string CreateSavePath(string root)
        {
            if (_configuration == null) throw new InvalidOperationException("Configuration not validated.");
            return Path.Combine(RevivalIapRecoveryProfile.TitleRoot(root, _configuration.testTitle), "PlayerData.json");
        }
        public static IAPManager CreatePurchasing() => new IAPManager(
            new PlayFabIapReceiptVerifier(_configuration.testTitle, _configuration.productionTitle,
                ApplicationId, _configuration.catalog), () => Session, 1, Gate);

        public static LoginWithCustomIDRequest RecoveryLoginRequest(string customId)
        {
            if (customId == null || !Regex.IsMatch(customId, "\\Aiap-test-[a-f0-9]{32}\\z"))
                throw new ArgumentException("Existing test credential format required.");
            return new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = false };
        }
        public static bool RecoveryInventoryOwned(IEnumerable<ItemInstance> inventory, string catalog, DateTime now) =>
            !string.IsNullOrWhiteSpace(catalog) && inventory != null && inventory.Any(item => item != null &&
                item.ItemId == NoAdsPurchaseFulfillment.ProductId && item.CatalogVersion == catalog &&
                (!item.Expiration.HasValue || item.Expiration.Value.ToUniversalTime() > now.ToUniversalTime()));

        public static void LoginExisting(string customId, string expectedAccount, bool reopenProfile = false)
        {
            if (!CanPrepare || _configuration == null) return;
            LoginWithCustomIDRequest request;
            try
            {
                request = RecoveryLoginRequest(customId);
                if (expectedAccount == null || !Regex.IsMatch(expectedAccount, "\\A[A-Fa-f0-9]{8,32}\\z"))
                    throw new ArgumentException();
            }
            catch (Exception) { Status = "Existing test credential / expected account format required"; return; }
            _preparationAttempted = true; // No automatic login fallback/retry in this process.
            var stage = new RevivalIapRecoveryStaging(expectedAccount);
            var owner = Managers.DataM;
            _staging = stage; _stagingOwner = owner;
            _stagingOwnerGeneration = owner.AccountGeneration;
            _deadline = DateTime.UtcNow.AddSeconds(45);
            Status = "Authenticating existing test account; Store remains locked";
            var client = new PlayFabClientInstanceAPI(new PlayFabApiSettings
            {
                TitleId = _configuration.testTitle, DisableDeviceInfo = true, DisableFocusTimeCollection = true
            }, new PlayFabAuthenticationContext());
            try
            {
                client.LoginWithCustomID(request, result =>
                {
                    if (!Current(stage, owner)) return;
                    if (result == null || !stage.AcceptLogin(result.PlayFabId, result.SessionTicket, result.NewlyCreated))
                    { CancelRecovery("Existing test account authentication rejected; originals preserved"); return; }
                    var captured = stage.Session;
                    Status = "Checking existing No Ads inventory; Store remains locked";
                    try
                    {
                        client.GetUserInventory(new GetUserInventoryRequest(), inventory =>
                        {
                            if (!Current(stage, owner)) return;
                            bool owned = RecoveryInventoryOwned(inventory?.Inventory, _configuration.catalog, DateTime.UtcNow);
                            if (!stage.AcceptInventory(captured, owned))
                            { CancelRecovery("Expected active No Ads inventory missing; originals preserved"); return; }
                            try
                            {
                                if (AD.Privacy.DeletionRecoveryGuard.IsPending(captured.AccountId)) throw new InvalidOperationException();
                                var path = reopenProfile
                                    ? RevivalIapRecoveryProfile.Selected(Application.persistentDataPath, _configuration.testTitle, captured.AccountId)
                                    : RevivalIapRecoveryProfile.Prepare(Application.persistentDataPath,
                                        _configuration.testTitle, captured.AccountId, Guid.NewGuid().ToString("N"));
                                if (!Current(stage, owner)) throw new InvalidOperationException();
                                Managers.Instance.CompleteIapRecovery(path, captured, reopenProfile);
                                if (!reopenProfile) RevivalIapRecoveryProfile.MarkReady(Application.persistentDataPath, _configuration.testTitle, path);
                                if (!ReferenceEquals(_staging, stage) || DateTime.UtcNow >= _deadline ||
                                    Managers.DataM == null || !Managers.DataM.IsServerDataReady || Managers.DataM.PlayFabId != captured.AccountId)
                                    throw new InvalidOperationException();
                                Session = captured; // Publish only after owner, inventory and durable profile are ready.
                                stage.Cancel(); _staging = null; _stagingOwner = null;
                                Status = reopenProfile ? "Existing recovery profile reauthenticated. STOP remains locked."
                                    : "Existing account ready in a NEW blank local profile. STOP remains locked.";
                            }
                            catch (Exception) { CancelRecovery("Profile preparation stopped; all files preserved; restart requires reauthentication"); }
                        }, error => { if (Current(stage, owner)) CancelRecovery("Existing inventory check failed; originals preserved"); });
                    }
                    catch (Exception) { if (Current(stage, owner)) CancelRecovery("Existing inventory check could not start"); }
                }, error => { if (Current(stage, owner)) CancelRecovery("Existing test account authentication failed; no account created"); });
            }
            catch (Exception) { if (Current(stage, owner)) CancelRecovery("Existing test authentication could not start"); }
            // The SDK owns its transient request until completion. Never persist/log its contents.
        }

        private static bool Current(RevivalIapRecoveryStaging stage, DataManager owner) =>
            ReferenceEquals(_staging, stage) && ReferenceEquals(_stagingOwner, owner) && owner != null &&
            ReferenceEquals(Managers.DataM, owner) && owner.AccountGeneration == _stagingOwnerGeneration &&
            Managers.Instance != null && Managers.Instance.CanPrepareIapRecovery && DateTime.UtcNow < _deadline && Session == null;

        public static void Tick()
        {
            if (_staging != null && !Current(_staging, _stagingOwner))
                CancelRecovery("Recovery staging expired or owner changed; late callbacks ignored");
        }
        public static void CancelRecovery(string fixedStatus = "Recovery cancelled; originals preserved")
        {
            _staging?.Cancel(); _staging = null; _stagingOwner = null;
            Session = null; Gate.Close();
            if (Managers.DataM != null) Managers.DataM.SuspendAccountSession();
            Status = fixedStatus;
        }
        public static void StartApprovedRecovery(bool userConfirmed)
        {
            if (Session == null || Managers.DataM == null || !Managers.DataM.IsServerDataReady ||
                Managers.DataM.PlayFabId != Session.AccountId || !ResumeBuildApproved || !userConfirmed) return;
            if (!Gate.Open(RevivalIapRecoveryProfile.TitleRoot(Application.persistentDataPath, _configuration.testTitle),
                ResumeBuildApproved, userConfirmed))
            { Status = "Recovery attempt already reserved or unavailable; STOP"; return; }
            Status = "One approved Store fetch / receipt attempt reserved. No purchase or automatic retry.";
            Managers.IAPM.InitializePurchasing();
        }

        // Legacy harness entry points cannot create accounts or write probe data during recovery.
        public static void Login() { }
        public static RevivalProgressProbe CreateProgressProbe() =>
            throw new InvalidOperationException("Progress probe locked during account recovery.");
    }
}
#endif
