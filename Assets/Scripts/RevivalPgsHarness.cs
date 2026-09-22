#if UNITY_EDITOR || TAMER_PGS_HARNESS
using System;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json.Linq;
using AD;
using PlayFab;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
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

    public enum OAuthDiagnostic { Unknown, InvalidClient, InvalidGrant, RedirectUriMismatch, AccessDenied }

    // No input text escapes this boundary, including exceptions and unknown values.
    public static OAuthDiagnostic ClassifyOAuthFailure(PlayFabError error)
    {
        if (error == null || error.Error != PlayFabErrorCode.GoogleOAuthError) return OAuthDiagnostic.Unknown;
        try
        {
            OAuthDiagnostic structured = OAuthDiagnostic.Unknown;
            bool hasStructured = error.ErrorDetails != null && error.ErrorDetails.ContainsKey("error");
            if (hasStructured)
            {
                var values = error.ErrorDetails["error"];
                if (values == null || values.Count == 0 || values.Count > 16) return OAuthDiagnostic.Unknown;
                foreach (string value in values)
                {
                    OAuthDiagnostic next = ExactOAuthToken(value);
                    if (next == OAuthDiagnostic.Unknown || (structured != OAuthDiagnostic.Unknown && structured != next))
                        return OAuthDiagnostic.Unknown;
                    structured = next;
                }
            }

            string message = error.ErrorMessage;
            if (message != null && message.Length > 4096) return OAuthDiagnostic.Unknown;
            // Parse JSON-shaped payloads conservatively. Never fall back from malformed,
            // duplicate, escaped or non-string fields to descriptive prose.
            int jsonStart = (message ?? "").IndexOf('{');
            bool jsonLike = jsonStart >= 0 || (message ?? "").Contains("\"error");
            if (jsonLike)
            {
                int jsonEnd = message.LastIndexOf('}');
                if (jsonStart < 0 || jsonEnd < jsonStart || message.Contains("\\")) return OAuthDiagnostic.Unknown;
                var payload = JObject.Parse(message.Substring(jsonStart, jsonEnd - jsonStart + 1),
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                var fields = payload.Descendants().OfType<JProperty>().Where(p => p.Name == "error").ToArray();
                if (fields.Length == 0) return OAuthDiagnostic.Unknown;
                foreach (var field in fields)
                {
                    if (field.Value.Type != JTokenType.String) return OAuthDiagnostic.Unknown;
                    OAuthDiagnostic next = ExactOAuthToken((string)field.Value);
                    if (next == OAuthDiagnostic.Unknown || (hasStructured && structured != next)) return OAuthDiagnostic.Unknown;
                    structured = next;
                    hasStructured = true;
                }
            }
            OAuthDiagnostic found = OAuthDiagnostic.Unknown;
            // URL/path/token-like neighbours are not word boundaries for diagnostics.
            foreach (Match match in Regex.Matches(message ?? "", @"(?<![A-Za-z0-9_./:%?&=+\-])(invalid_client|invalid_grant|redirect_uri_mismatch|access_denied)(?![A-Za-z0-9_./:%?&=+\-])"))
            {
                OAuthDiagnostic next = ExactOAuthToken(match.Value);
                if (found != OAuthDiagnostic.Unknown && found != next) return OAuthDiagnostic.Unknown;
                found = next;
            }
            if (hasStructured)
                return found != OAuthDiagnostic.Unknown && found != structured ? OAuthDiagnostic.Unknown : structured;
            return found;
        }
        catch (Exception) { return OAuthDiagnostic.Unknown; }
    }

    private static OAuthDiagnostic ExactOAuthToken(string value)
    {
        switch (value)
        {
            case "invalid_client": return OAuthDiagnostic.InvalidClient;
            case "invalid_grant": return OAuthDiagnostic.InvalidGrant;
            case "redirect_uri_mismatch": return OAuthDiagnostic.RedirectUriMismatch;
            case "access_denied": return OAuthDiagnostic.AccessDenied;
            default: return OAuthDiagnostic.Unknown;
        }
    }

    public static string SafeOAuthAuthenticationFailure(PlayFabError error)
    {
        switch (ClassifyOAuthFailure(error))
        {
            case OAuthDiagnostic.InvalidClient: return "Test authentication: GoogleOAuthError / invalid_client.";
            case OAuthDiagnostic.InvalidGrant: return "Test authentication: GoogleOAuthError / invalid_grant.";
            case OAuthDiagnostic.RedirectUriMismatch: return "Test authentication: GoogleOAuthError / redirect_uri_mismatch.";
            case OAuthDiagnostic.AccessDenied: return "Test authentication: GoogleOAuthError / access_denied.";
            default: return "Test authentication: GoogleOAuthError / Unknown.";
        }
    }

    // Accept only the enum: server text, details and identity data never enter the formatter.
    public static string SafeAuthenticationFailure(PlayFabErrorCode? code)
    {
        switch (code)
        {
            case PlayFabErrorCode.AccountNotFound: return "Test authentication: AccountNotFound. No account was created.";
            case PlayFabErrorCode.AccountNotLinked: return "Test authentication: AccountNotLinked. No account was created.";
            case PlayFabErrorCode.InvalidGooglePlayGamesServerAuthCode: return "Test authentication: InvalidGooglePlayGamesServerAuthCode.";
            case PlayFabErrorCode.InvalidGoogleToken: return "Test authentication: InvalidGoogleToken.";
            case PlayFabErrorCode.GoogleOAuthNotConfiguredForTitle: return "Test authentication: GoogleOAuthNotConfiguredForTitle.";
            case PlayFabErrorCode.MissingTitleGoogleProperties: return "Test authentication: MissingTitleGoogleProperties.";
            case PlayFabErrorCode.GoogleOAuthError: return "Test authentication: GoogleOAuthError.";
            case PlayFabErrorCode.GoogleOAuthNoIdTokenIncludedInResponse: return "Test authentication: GoogleOAuthNoIdTokenIncludedInResponse.";
            case PlayFabErrorCode.InvalidTitleId: return "Test authentication: InvalidTitleId.";
            case PlayFabErrorCode.NotAuthorized: return "Test authentication: NotAuthorized.";
            case PlayFabErrorCode.NotAuthorizedByTitle: return "Test authentication: NotAuthorizedByTitle.";
            case PlayFabErrorCode.ConnectionError: return "Test authentication: ConnectionError.";
            case PlayFabErrorCode.ServiceUnavailable: return "Test authentication: ServiceUnavailable.";
            case PlayFabErrorCode.DownstreamServiceUnavailable: return "Test authentication: DownstreamServiceUnavailable.";
            case PlayFabErrorCode.APIClientRequestRateLimitExceeded: return "Test authentication: APIClientRequestRateLimitExceeded.";
            default: return "Test authentication failed. Diagnostic unavailable. No account was created.";
        }
    }

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
                            }, error =>
                            {
                                if (Current(attempt, 3)) Finish(error?.Error == PlayFabErrorCode.GoogleOAuthError
                                    ? SafeOAuthAuthenticationFailure(error) : SafeAuthenticationFailure(error?.Error));
                            });
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
