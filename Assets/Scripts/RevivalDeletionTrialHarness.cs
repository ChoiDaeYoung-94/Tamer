#if UNITY_EDITOR || TAMER_DELETION_HARNESS
using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using AD;
using AD.Privacy;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RevivalDeletionTrialHarness : MonoBehaviour
{
    public const string ApplicationId = "com.AeDeong.MonsterTamer.deletiontrial";
    public const string Title = "12B656";
    private string _custom = "", _expected = "", _status = "Manual disposable-account login only";
    private bool _busy, _attempted;
    private GameObject _panel;
    private Canvas _canvas;
    private GraphicRaycaster _raycaster;
    private float _deadline;
    private bool _offline;

    private void Start()
    {
        if (File.Exists(Path.Combine(Application.persistentDataPath, "OfflineDeletionChecks", "unknown", "pending.json")))
            _offline = true;
    }

    public static LoginWithCustomIDRequest LoginRequest(string custom, string expected)
    {
        if (!Regex.IsMatch(custom ?? "", "^deletion-disposable-[0-9a-f]{48}$") ||
            !Regex.IsMatch(expected ?? "", "^[A-Fa-f0-9]{1,32}$")) throw new InvalidOperationException();
        return new LoginWithCustomIDRequest { CustomId = custom, CreateAccount = false };
    }

#if TAMER_DELETION_HARNESS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.identifier != ApplicationId || !Debug.isDebugBuild) throw new InvalidOperationException();
        PlayFabSettings.staticPlayer.ForgetAllCredentials();
        PlayFabSettings.TitleId = Title;
        var root = new GameObject("Isolated deletion trial");
        DontDestroyOnLoad(root);
        root.AddComponent<Managers>();
        root.AddComponent<RevivalDeletionTrialHarness>();
    }
#endif

    private void Login()
    {
        if (_offline || _attempted || _busy || Application.identifier != ApplicationId) return;
        LoginWithCustomIDRequest request;
        try { request = LoginRequest(_custom, _expected); }
        catch { _status = "Invalid disposable-account input"; return; }
        string expected = _expected;
        _custom = ""; _expected = "";
        _attempted = _busy = true;
        _deadline = Time.realtimeSinceStartup + 30;
        var api = new PlayFabClientInstanceAPI(new PlayFabApiSettings { TitleId = Title }, new PlayFabAuthenticationContext());
        api.LoginWithCustomID(request, result =>
        {
            if (!_busy) return;
            _busy = false;
            if (result == null || result.NewlyCreated || result.PlayFabId != expected || result.EntityToken?.Entity?.Type != "title_player_account" ||
                string.IsNullOrEmpty(result.EntityToken.Entity.Id) || string.IsNullOrEmpty(result.SessionTicket))
            { _status = "Identity rejected; no deletion UI"; return; }
            try
            {
                PlayFabSettings.staticPlayer.CopyFrom(api.authenticationContext);
                Managers.DataM.BindDeletionTrial(expected);
                _status = "Expected disposable identity verified; open production deletion UI";
                OpenPanel();
            }
            catch { PlayFabSettings.staticPlayer.ForgetAllCredentials(); _status = "Local binding rejected; no deletion UI"; }
        }, error => { if (_busy) { _busy = false; _status = "Login failed; no auto retry or account creation"; } });
    }

    private void Update()
    {
        if (_busy && Time.realtimeSinceStartup >= _deadline)
        { _busy = false; _status = "Login response unknown; no auto retry"; }
    }

    private void OpenPanel()
    {
        if (_panel != null) { _canvas.enabled = _raycaster.enabled = true; return; }
        var canvasObject = new GameObject("Deletion trial canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas = canvasObject.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900,1600);
        if (EventSystem.current == null) new GameObject("Trial events", typeof(EventSystem), typeof(StandaloneInputModule));
        var rect = DeletionView.Rect("Production deletion panel", canvasObject.transform);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        _panel = rect.gameObject; _panel.SetActive(false);
        var view = _panel.AddComponent<DeletionView>();
        view.Build(Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"), () => { _canvas.enabled = _raycaster.enabled = false; });
        _panel.AddComponent<DeletionPresenter>().Bind(view);
        _panel.SetActive(true); // Uses unchanged runtime factory, Live preflight and Specific confirmation.
    }

    private void OnGUI()
    {
        if (_canvas != null && _canvas.enabled) return;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.width / 900f));
        GUILayout.BeginArea(new Rect(30,40,840,1200));
        GUILayout.Label("ISOLATED DISPOSABLE ACCOUNT DELETION TRIAL");
        GUILayout.Label(_status);
        if (!_attempted && !_offline)
        {
            GUILayout.Label("Disposable CustomId (masked)"); _custom = GUILayout.PasswordField(_custom, '*', 100, GUILayout.Height(70));
            GUILayout.Label("Expected PlayFabId"); _expected = GUILayout.TextField(_expected, 32, GUILayout.Height(70));
            if (GUILayout.Button("Login existing disposable account", GUILayout.Height(90))) Login();
        }
        if (!_attempted && !_busy && GUILayout.Button("Run offline deletion checks (no server)", GUILayout.Height(90)))
            RunOffline();
        if (_panel != null && GUILayout.Button("Open account deletion panel", GUILayout.Height(90))) OpenPanel();
        if (Managers.DataM != null)
            GUILayout.Label("Local marker=" + Managers.DataM.DeletionTrialLocalExists + " saveReady=" + Managers.DataM.IsServerDataReady +
                " pending=" + Managers.DataM.DeletionInProgress + " signedIn=" + !string.IsNullOrEmpty(Managers.DataM.PlayFabId));
        GUILayout.EndArea();
    }

    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Offline deletion assertion failed."); }

    private static void SyntheticIdentity(string account)
    {
        // No ticket or token: this context cannot authenticate an SDK request.
        PlayFabSettings.staticPlayer.CopyFrom(new PlayFabAuthenticationContext(null, null, account,
            "synthetic-entity", "title_player_account"));
    }

    private async void RunOffline()
    {
        _offline = _busy = true;
        try
        {
            Require(Application.identifier == ApplicationId && Debug.isDebugBuild &&
                string.IsNullOrEmpty(PlayFabSettings.staticPlayer.ClientSessionTicket));
            var data = Managers.DataM;
            string root = Path.Combine(Application.persistentDataPath, "OfflineDeletionChecks");
            string unknownDirectory = Path.Combine(root, "unknown");
            var journal = new FileDeletionRecoveryStore(Path.Combine(unknownDirectory, "pending.json"));
            bool restart = journal.Load() != null;
            if (!restart)
            {
                Require(!Directory.Exists(root)); // Preserve prior/uncertain evidence; never silently rerun.
                string directory = data.BindOfflineDeletionTrial("accepted", "synthetic-accepted");
                string backups = Path.Combine(directory, "PlayerDataBackups"); Directory.CreateDirectory(backups);
                string own = Path.Combine(backups, "PlayerData-" + Guid.NewGuid().ToString("N") + ".json");
                string foreign = Path.Combine(backups, "PlayerData-" + Guid.NewGuid().ToString("N") + ".json");
                string legacy = Path.Combine(backups, "PlayerData-" + Guid.NewGuid().ToString("N") + ".json");
                string malformed = Path.Combine(backups, "PlayerData-" + Guid.NewGuid().ToString("N") + ".json");
                string foreignText = "{\"__TamerAccountOwner\":\"synthetic-other\",\"Gold\":\"8\"}";
                string legacyText = "{\"Gold\":\"7\"}";
                File.WriteAllText(own, "{\"__TamerAccountOwner\":\"synthetic-accepted\",\"GooglePlay\":\"SyntheticNoAds\"}");
                File.WriteAllText(foreign, foreignText); File.WriteAllText(legacy, legacyText); File.WriteAllText(malformed, "{bad-json");
                string archive = Path.Combine(directory, "PlayerData.json.deletion-entitlement-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(archive, "{\"__TamerAccountOwner\":\"synthetic-accepted\",\"GooglePlay\":\"SyntheticNoAds\"}");
                string inventory = data.BindOfflineTrialInventory("synthetic-accepted");
                SyntheticIdentity("synthetic-accepted");
                int acceptedCalls = 0;
                using (var flow = new DeletionFlow(new CloudScriptDeletionGateway((s, id, t) =>
                    { acceptedCalls++; return Task.FromResult(true); }, true), data.DeletionSession,
                    data.BeginDeletionSubmission, data.FinishAcceptedDeletion, data.FinishCancelledDeletion,
                    new FileDeletionRecoveryStore(Path.Combine(directory, "pending.json")), new string('a', 64)))
                {
                    Require(await flow.RequestAsync() && flow.State == DeletionState.AwaitingConfirmation && acceptedCalls == 0);
                    Require(await flow.ConfirmAsync() && flow.State == DeletionState.Accepted && !flow.AcceptedCleanupFailed && acceptedCalls == 1);
                }
                Require(!data.DeletionTrialLocalExists && !File.Exists(own) && !File.Exists(inventory) && !File.Exists(archive));
                Require(File.ReadAllText(foreign) == foreignText && File.ReadAllText(legacy) == legacyText && File.ReadAllText(malformed) == "{bad-json");
                Require(Directory.GetFiles(directory, "*.deletion-entitlement-*").Length == 0 &&
                    !data.IsServerDataReady && !data.DeletionInProgress && string.IsNullOrEmpty(data.PlayFabId) &&
                    string.IsNullOrEmpty(PlayFabSettings.staticPlayer.PlayFabId));
                File.WriteAllText(Path.Combine(root, "accepted.txt"), "owner-progress-backup-inventory-old-archive-removed foreign-legacy-malformed-preserved signed-out no-new-archive synthetic-submits=1 server-deletes=0");
            }
            DeletionRecoveryGuard.HasPendingSubmission = account => account == "synthetic-unknown" && journal.Load()?.SubmissionStarted == true;
            PlayFabSettings.staticPlayer.ForgetAllCredentials();
            data.BindOfflineDeletionTrial("unknown", "synthetic-unknown");
            SyntheticIdentity("synthetic-unknown");
            int calls = 0;
            using (var flow = new DeletionFlow(new CloudScriptDeletionGateway((s, id, t) =>
                { calls++; return Task.FromResult(false); }, true), data.DeletionSession,
                data.BeginDeletionSubmission, data.FinishAcceptedDeletion, data.FinishCancelledDeletion, journal, new string('b', 64)))
            {
                if (!restart)
                {
                    Require(await flow.RequestAsync() && flow.State == DeletionState.AwaitingConfirmation && calls == 0);
                    Require(await flow.ConfirmAsync() && flow.State == DeletionState.SubmissionUnknown && calls == 1);
                }
                Require(flow.State == DeletionState.SubmissionUnknown && !flow.CanConfirmDeletion &&
                    !await flow.RequestAsync() && !await flow.ConfirmAsync() && !await flow.RefreshAsync());
                Require(calls == (restart ? 0 : 1) && journal.Load()?.SubmissionStarted == true &&
                    data.DeletionTrialLocalExists && data.DeletionInProgress && !data.IsServerDataReady &&
                    !data.TryUpdateLocalData("Gold", "99"));
                bool blocked = false;
                try { data.BeginAccountSession("synthetic-unknown"); } catch (InvalidOperationException) { blocked = true; }
                Require(blocked);
            }
            PlayFabSettings.staticPlayer.ForgetAllCredentials();
            _status = restart ? "OFFLINE PASS: unknown survives process restart; relogin/write/resubmit blocked; server calls=0"
                : "OFFLINE PASS: owned cleanup/foreign preservation; unknown journal retained. Restart app then run checks again.";
            File.WriteAllText(Path.Combine(root, restart ? "restart.txt" : "phase1.txt"), _status);
            Debug.Log("DELETION_OFFLINE_CHECK " + _status);
        }
        catch (Exception error)
        {
            _status = "OFFLINE FAIL: " + error.GetType().Name + "; evidence preserved; no automatic retry";
            Debug.LogError("DELETION_OFFLINE_CHECK " + _status);
        }
        finally { _busy = false; }
    }
}
#endif
