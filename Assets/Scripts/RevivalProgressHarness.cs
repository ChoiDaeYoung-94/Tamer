#if UNITY_EDITOR || TAMER_PROGRESS_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AD.Purchasing;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace AD
{
    /// <summary>Standalone probe. No Managers, store, existing identity or game save access.</summary>
    public sealed class RevivalProgressHarness : MonoBehaviour
    {
        public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.progress";
        public const string TestTitle = "12B656";
        private RevivalProgressProbe _probe;
        private ReceiptSession _session;
        private bool _loggingIn;
        private int _generation;
        private string _customId = "";
        private string _status = "Local synthetic mode or separately approved test authentication required";
        private bool _local;
        private string _localPath;

        public static void ValidatePackage(string package)
        {
            if (package != ApplicationId) throw new InvalidOperationException("Dedicated progress package required.");
        }
        public static LoginWithCustomIDRequest CreateLoginRequest(string customId)
        {
            if (customId == null || !Regex.IsMatch(customId, "^progress-probe-[a-f0-9]{32}$"))
                throw new ArgumentException("A separately provisioned progress-only identity is required.");
            return new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = false };
        }
#if TAMER_PROGRESS_HARNESS
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            ValidatePackage(Application.identifier);
            if (!Debug.isDebugBuild) throw new InvalidOperationException("Development player required.");
            var root = new GameObject("Progress-only probe");
            DontDestroyOnLoad(root);
            root.AddComponent<RevivalProgressHarness>();
        }
#endif
        private void CloseProbe()
        {
            ++_generation;
            _probe?.Dispose(); _probe = null; _session = null; _loggingIn = false;
        }
        private void OnDestroy() { CloseProbe(); _customId = ""; }

        private void LocalMode()
        {
            ValidatePackage(Application.identifier);
            CloseProbe(); _local = true;
            // Only this package's synthetic file; never IapTest or PlayerData.json.
            _localPath = Path.Combine(Application.persistentDataPath, "ProgressProbeSyntheticV1.txt");
            _session = new ReceiptSession("local-synthetic-progress", "not-a-server-ticket");
            _probe = new RevivalProgressProbe(() => _session,
                (account, ok, fail) =>
                {
                    var data = new Dictionary<string, string>();
                    if (File.Exists(_localPath)) data.Add(RevivalProgressProbe.Key, File.ReadAllText(_localPath));
                    ok(data);
                },
                (account, patch, ok, fail) =>
                {
                    RevivalProgressProbe.ValidatePatch(patch);
                    File.WriteAllText(_localPath, patch[RevivalProgressProbe.Key]);
                    ok();
                });
            _status = "LOCAL SYNTHETIC FILE — no PlayFab persistence evidence";
            _probe.Read();
        }
        private void Login()
        {
            ValidatePackage(Application.identifier);
            LoginWithCustomIDRequest request;
            try { request = CreateLoginRequest(_customId); }
            catch (ArgumentException) { _status = "Dedicated identity format required; no request sent"; return; }
            CloseProbe(); _local = false; _loggingIn = true;
            int generation = _generation;
            _customId = "";
            _status = "Explicit test authentication pending";
            var client = new PlayFabClientInstanceAPI(new PlayFabApiSettings
            { TitleId = TestTitle, DisableDeviceInfo = true, DisableFocusTimeCollection = true }, new PlayFabAuthenticationContext());
            try
            {
                client.LoginWithCustomID(request, result =>
                {
                    if (this == null || generation != _generation) return;
                    _loggingIn = false;
                    if (result == null || string.IsNullOrEmpty(result.PlayFabId) || string.IsNullOrEmpty(result.SessionTicket))
                    { _status = "Invalid authentication response"; return; }
                    _session = new ReceiptSession(result.PlayFabId, result.SessionTicket);
                    _probe = RevivalProgressProbe.Connect(TestTitle, Application.identifier, () => _session);
                    _status = "TEST PLAYFAB — dedicated key only";
                    _probe.Read();
                }, error =>
                {
                    if (this == null || generation != _generation) return;
                    _loggingIn = false; _status = "Test login failed; provisioned identity required";
                });
            }
            catch (Exception) { _loggingIn = false; _status = "Authentication could not start"; }
        }
        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 900f, Screen.width / 900f, 1));
            GUILayout.BeginArea(new Rect(15, 25, 870, 1100), GUI.skin.box);
            GUILayout.Label("PROGRESS ONLY — no store, purchases or restoration");
            GUILayout.Label(_status);
            GUILayout.Label(_probe?.Status ?? "No progress session");
            GUI.enabled = !_loggingIn && (_probe == null || !_probe.IsBusy);
            if (GUILayout.Button("Open / reconnect LOCAL synthetic file", GUILayout.Height(80))) LocalMode();
            GUILayout.Label("Cloud login only after separate external-account approval and provisioning");
            _customId = GUILayout.PasswordField(_customId, '*', 64, GUILayout.Height(65));
            if (GUILayout.Button("Explicitly authenticate provisioned TEST identity", GUILayout.Height(80))) Login();
            GUI.enabled = _probe != null && !_probe.IsBusy;
            if (GUILayout.Button("Reconnect / read current mode", GUILayout.Height(80)))
            {
                if (_local) LocalMode();
                else { _probe.Dispose(); _probe = RevivalProgressProbe.Connect(TestTitle, Application.identifier, () => _session); _probe.Read(); }
            }
            GUI.enabled = _probe != null && _probe.CanSave;
            if (GUILayout.Button("Save synthetic step 1 / read back", GUILayout.Height(80))) _probe.Save(1);
            if (GUILayout.Button("Save synthetic step 2 / read back", GUILayout.Height(80))) _probe.Save(2);
            GUI.enabled = true;
            if (GUILayout.Button("Disconnect (preserve saved data)", GUILayout.Height(80))) { CloseProbe(); _status = "Disconnected"; }
            GUILayout.EndArea();
        }
    }
}
#endif
