#if UNITY_EDITOR || TAMER_DELETION_HARNESS
using System;
using System.Text.RegularExpressions;
using AD;
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
        if (_attempted || _busy || Application.identifier != ApplicationId) return;
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
        if (!_attempted)
        {
            GUILayout.Label("Disposable CustomId (masked)"); _custom = GUILayout.PasswordField(_custom, '*', 100, GUILayout.Height(70));
            GUILayout.Label("Expected PlayFabId"); _expected = GUILayout.TextField(_expected, 32, GUILayout.Height(70));
            if (GUILayout.Button("Login existing disposable account", GUILayout.Height(90))) Login();
        }
        if (_panel != null && GUILayout.Button("Open account deletion panel", GUILayout.Height(90))) OpenPanel();
        if (Managers.DataM != null)
            GUILayout.Label("Local marker=" + Managers.DataM.DeletionTrialLocalExists + " saveReady=" + Managers.DataM.IsServerDataReady +
                " pending=" + Managers.DataM.DeletionInProgress + " signedIn=" + !string.IsNullOrEmpty(Managers.DataM.PlayFabId));
        GUILayout.EndArea();
    }
}
#endif
