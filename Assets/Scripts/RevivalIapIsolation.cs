#if UNITY_EDITOR || TAMER_IAP_HARNESS
using System;
using System.IO;
using AD.Purchasing;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace AD
{
    /// <summary>One dedicated test identity per installation; no static PlayFab authentication.</summary>
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
        private static string _identity;
        private static bool _loggingIn;
        public static ReceiptSession Session { get; private set; }
        public static string Status { get; private set; } = "Login required";
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
            bool storeTestBuild = false;
#if TAMER_IAP_STORE_TEST
            storeTestBuild = true;
#endif
            if (Application.isEditor || (!Debug.isDebugBuild && !storeTestBuild))
                throw new InvalidOperationException("Dedicated development player required.");
            _configuration = config;
        }
        public static string CreateSavePath(string root)
        {
            if (_configuration == null) throw new InvalidOperationException("Configuration not validated.");
            string directory = Path.Combine(root, "IapTest", _configuration.testTitle.ToUpperInvariant());
            Directory.CreateDirectory(directory);
            string identityPath = Path.Combine(directory, "identity.txt");
            _identity = File.Exists(identityPath) ? File.ReadAllText(identityPath) : Guid.NewGuid().ToString("N");
            if (!Guid.TryParseExact(_identity, "N", out _)) throw new InvalidDataException("Invalid test identity; preserved.");
            if (!File.Exists(identityPath)) File.WriteAllText(identityPath, _identity);
            return Path.Combine(directory, "PlayerData.json");
        }
        public static IAPManager CreatePurchasing() => new IAPManager(
            new PlayFabIapReceiptVerifier(_configuration.testTitle, _configuration.productionTitle,
                ApplicationId, _configuration.catalog), () => Session);

        public static void Login()
        {
            if (_loggingIn || Session != null || _configuration == null || string.IsNullOrEmpty(_identity)) return;
            var owner = Managers.DataM;
            if (owner == null) return;
            _loggingIn = true;
            Status = "Logging into test title";
            var client = new PlayFabClientInstanceAPI(new PlayFabApiSettings
            {
                TitleId = _configuration.testTitle, DisableDeviceInfo = true, DisableFocusTimeCollection = true
            }, new PlayFabAuthenticationContext());
            try
            {
                client.LoginWithCustomID(new LoginWithCustomIDRequest
                { CustomId = "iap-test-" + _identity, CreateAccount = true }, result =>
                {
                    _loggingIn = false;
                    if (owner == null || Managers.DataM != owner) { Status = "Owner changed; restart app"; return; }
                    try
                    {
                        owner.BeginAccountSession(result.PlayFabId);
                        Session = new ReceiptSession(result.PlayFabId, result.SessionTicket);
                        Managers.ServerM.GetAllData(update: true);
                        if (!owner.IsServerDataReady) throw new InvalidOperationException();
                        Status = "Test login ready; game data stays local";
                    }
                    catch (Exception)
                    {
                        Session = null;
                        owner.SuspendAccountSession();
                        Status = "Test account binding failed; save preserved";
                    }
                }, error =>
                {
                    _loggingIn = false;
                    Status = "Test login failed: " + (error == null ? "Unknown" : error.Error.ToString());
                });
            }
            catch (Exception)
            {
                _loggingIn = false;
                Status = "Test login could not start; retry available";
            }
        }
    }
}
#endif
