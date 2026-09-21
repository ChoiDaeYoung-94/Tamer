#if UNITY_EDITOR || TAMER_PGS_HARNESS
using System;
using AD;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using PlayFab;
#endif

/// <summary>Manual authentication only. Never creates managers, reads saves or starts IAP.</summary>
public sealed class RevivalPgsHarness : MonoBehaviour
{
    private RevivalPgsTestConfiguration _config;
    private string _status = "Configuration unavailable; sign-in disabled.";
    private bool _busy;
    private int _attempt;
    private float _deadline;
    private int _stage;

#if TAMER_PGS_HARNESS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // Third-party authentication errors may contain sensitive payloads.
        // This isolated player reports fixed UI messages instead of Unity logs.
        Debug.unityLogger.logEnabled = false;
        var root = new GameObject("Manual PGS test");
        DontDestroyOnLoad(root);
        root.AddComponent<RevivalPgsHarness>();
    }
#endif
    private void Awake()
    {
        try
        {
            var asset = Resources.Load<TextAsset>("RevivalPgsLocal");
            if (asset == null) return;
            _config = JsonUtility.FromJson<RevivalPgsTestConfiguration>(asset.text);
            _config.Validate(Application.identifier);
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.DebugLogEnabled = false;
            GooglePlayGames.OurUtils.Logger.WarningLogEnabled = false;
            var settings = PlayGamesSettings.LoadInstance();
            if (settings == null || settings.WebClientId != _config.webClientId || settings.AppId != _config.gameId)
                throw new InvalidOperationException();
            _status = "Ready. Only an already linked test account can sign in. No save or purchase operations.";
#else
            _config = null;
            _status = "Android test player required. Editor sign-in is disabled.";
#endif
        }
        catch (Exception) { _config = null; _status = "Invalid local PGS configuration; sign-in disabled."; }
    }

    private bool Current(int attempt, int stage) => this != null && _busy && _attempt == attempt && _stage == stage;
    private void Finish(string status) { _attempt++; _busy = false; _stage = 0; _status = status; }
    private void OnDestroy() { _attempt++; _busy = false; }
    private void Update()
    {
        if (_busy && Time.realtimeSinceStartup >= _deadline)
            Finish("Timed out. Retry obtains a fresh authentication code.");
    }

    private void Login()
    {
        if (_config == null || _busy) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        _busy = true; _stage = 1; int attempt = ++_attempt;
        _deadline = Time.realtimeSinceStartup + 60;
        _status = "Waiting for Google Play authentication";
        try
        {
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                if (!Current(attempt, 1)) return;
                if (status != SignInStatus.Success) { Finish("Google Play sign-in failed."); return; }
                _stage = 2;
                try
                {
                    PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                    {
                        if (!Current(attempt, 2)) return;
                        _stage = 3; // Duplicate callbacks cannot exchange a code twice.
                        if (string.IsNullOrWhiteSpace(code)) { Finish("Server code unavailable. Check approved OAuth configuration."); return; }
                        try
                        {
                            var client = new PlayFabClientInstanceAPI(new PlayFabApiSettings
                            {
                                TitleId = RevivalPgsTestConfiguration.Title,
                                DisableDeviceInfo = true, DisableFocusTimeCollection = true,
                                ProductionEnvironmentUrl = "https://12B656.playfabapi.com"
                            }, new PlayFabAuthenticationContext());
                            client.LoginWithGooglePlayGamesServices(_config.CreateRequest(code), result =>
                            {
                                if (!Current(attempt, 3)) return;
                                Finish(result != null && !result.NewlyCreated && !string.IsNullOrEmpty(result.PlayFabId)
                                    ? "Test authentication succeeded. Session discarded; no account data read or written."
                                    : "Unexpected response rejected. No session retained.");
                            }, error => { if (Current(attempt, 3)) Finish("Test authentication failed or account not linked. No account was created."); });
                        }
                        catch (Exception) { if (Current(attempt, 3)) Finish("Test authentication could not start."); }
                    });
                }
                catch (Exception) { if (Current(attempt, 2)) Finish("Server authentication configuration unavailable."); }
            });
        }
        catch (Exception) { if (_busy) Finish("Google Play authentication could not start."); }
#endif
    }

    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 900f, Screen.width / 900f, 1));
        GUILayout.BeginArea(new Rect(20, 20, 860, 500), GUI.skin.box);
        GUILayout.Label("PGS TEST — title 12B656 — existing linked test account only");
        GUILayout.Label(_status);
        GUI.enabled = _config != null && !_busy;
        if (GUILayout.Button("Sign into Google Play and verify test authentication", GUILayout.Height(85))) Login();
        GUI.enabled = true;
        GUILayout.Label("No automatic login, account creation, linking, storage, advertising or purchases.");
        GUILayout.EndArea();
    }
}
#endif

