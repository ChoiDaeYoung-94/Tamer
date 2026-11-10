#if TAMER_GUEST_ENTRY_UI_HARNESS && (!TAMER_REVIVAL_SMOKE || TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS || TAMER_TEST_ADS || TAMER_DELETION_HARNESS)
#error Guest entry UI requires its dedicated offline smoke variant.
#endif
#if TAMER_GUEST_ENTRY_UI_RELEASE_HARNESS && !TAMER_GUEST_ENTRY_UI_HARNESS
#error The release-engine diagnostic requires the dedicated guest UI fixture.
#endif
#if UNITY_EDITOR || TAMER_GUEST_ENTRY_UI_HARNESS
using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using AD;
using TMPro;
using UnityEngine;

/// <summary>Local-only UI/preferences fixture. No authentication adapter or game manager is created.</summary>
public sealed class RevivalGuestEntryUiHarness : MonoBehaviour
{
    public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.guestentry1102";
    public const string ReleaseApplicationId = "com.AeDeong.MonsterTamer.revival.guestentry1102release";
    public const string FullEngineApplicationId = "com.AeDeong.MonsterTamer.revival.guestentry1102full";
    public const string X86ApplicationId = "com.AeDeong.MonsterTamer.revival.guestentry1102x86";
    public const string FreshX86ApplicationId = "com.AeDeong.MonsterTamer.revival.guestentry1110x86";
    public TMP_FontAsset Font;
    private readonly LoginGuestEntryGate _gate = new LoginGuestEntryGate();
    private readonly LoginGuestIdentityPreparation _identities = new LoginGuestIdentityPreparation();
    private InitialAccountChoiceView _view;
    private TMP_Text _diagnostics;
    private TMP_Text _saved;
    private int _generation;
    private bool _busy, _quitting;
    private string _expectedIdHash;

    public static bool AllowsContext(string package, bool editor, bool development, bool android) =>
        package == ApplicationId && !editor && development && android;

    public static bool AllowsReleaseContext(string package, bool editor, bool development, bool android) =>
        (package == ReleaseApplicationId || package == FullEngineApplicationId || package == X86ApplicationId || package == FreshX86ApplicationId) &&
        !editor && !development && android;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void TraceManagedStartup()
    {
#if TAMER_GUEST_ENTRY_UI_HARNESS && !UNITY_EDITOR
        Debug.Log("GUEST_ENTRY_UI_BOOT phase=managed-subsystem");
#endif
    }

    private void Start()
    {
#if TAMER_GUEST_ENTRY_UI_RELEASE_HARNESS
        bool allowed = AllowsReleaseContext(Application.identifier, Application.isEditor, Debug.isDebugBuild,
            Application.platform == RuntimePlatform.Android);
#else
        bool allowed = AllowsContext(Application.identifier, Application.isEditor, Debug.isDebugBuild,
            Application.platform == RuntimePlatform.Android);
#endif
        if (!allowed || Font == null)
            throw new InvalidOperationException("Dedicated Android UI fixture and matching engine variant required.");
        Debug.Log("GUEST_ENTRY_UI_BOOT phase=start-context-accepted");
        if (UnityEngine.Object.FindAnyObjectByType<Managers>() != null ||
            UnityEngine.Object.FindAnyObjectByType<AD.Login>() != null)
            throw new InvalidOperationException("No game manager or operating login may exist in this fixture.");
        foreach (var key in new[] { "Gold", "AllyMonsters", "GooglePlay", "LocalItem", "playerEquippedItems" })
            if (PlayerPrefs.HasKey(key)) throw new InvalidOperationException("Unexpected prior preferences; preserved.");

        var root = new GameObject("Guest UI trial canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 200;
        root.AddComponent<UnityEngine.UI.Image>().color = new Color32(18, 39, 36, 255);
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        var panel = DeletionView.Rect("InitialAccountChoice", root.transform);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        _view = panel.gameObject.AddComponent<InitialAccountChoiceView>();
        _view.Build(Font, null, null);
        Debug.Log("GUEST_ENTRY_UI_BOOT phase=view-built");
        _diagnostics = Label(root.transform, "Diagnostics", "", 25, .91f, .99f);
        _saved = Label(root.transform, "SavedState", "", 38, .30f, .70f);
        _saved.gameObject.SetActive(false);
        new GameObject("Trial EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule)).transform.SetParent(root.transform, false);

        var mode = PlayerPrefs.GetString(LoginGuestIdentityPreparation.ModeKey, "");
        var id = PlayerPrefs.GetString(LoginGuestIdentityPreparation.CustomIdKey, "");
        if (string.IsNullOrEmpty(mode) && string.IsNullOrEmpty(id)) ShowChoice();
        else if (mode == "custom-pending" && LoginGuestIdentityPreparation.IsValidGuestIdentity(id))
        {
            _expectedIdHash = Hash(id);
            ShowSaved("앱 재시작 뒤 같은 손님 설정을 복원했습니다.", "restored");
        }
        else throw new InvalidOperationException("Unrecognized trial state; preserved without reset.");
    }

    private bool Eligible() => !_busy && !_quitting && !_identities.HasUncertainWrites &&
        LoginGuestEntryPolicy.CanOffer(false, false,
            PlayerPrefs.GetString(LoginGuestIdentityPreparation.ModeKey, ""),
            PlayerPrefs.GetString(LoginGuestIdentityPreparation.GoogleIdKey, ""),
            PlayerPrefs.GetString(LoginGuestIdentityPreparation.CustomIdKey, ""), false,
            PlayerPrefs.GetInt(DataManager.DeletionLoginPauseKey, 0) != 0, false,
            PlayerPrefs.HasKey(LoginGuestIdentityPreparation.GooglePendingKey), true);

    private void ShowChoice(string message = null)
    {
        if (!_gate.Offer(this, _generation, 0, Eligible()))
            throw new InvalidOperationException("Trial first-choice state is not fresh.");
        int generation = _generation; long version = _gate.Version;
        _view.SetChoices(() => Choose(false, generation, version), () => Choose(true, generation, version));
        _view.SetStatus(message);
        _view.GoogleButton.interactable = _view.GuestButton.interactable = true;
        _view.gameObject.SetActive(true);
        _diagnostics.transform.SetAsLastSibling();
        _diagnostics.text = "격리 UI 검사 · Google 버튼은 모의 실패 · 서버 요청 0";
        Mark(message == null ? "fresh" : "google-simulated-failure");
    }

    private void Choose(bool guest, int generation, long version)
    {
        if (!_gate.TryChoose(this, _generation, 0, version, Eligible())) return;
        _busy = true;
        _view.GoogleButton.interactable = _view.GuestButton.interactable = false;
        if (!guest) { StartCoroutine(SimulatedGoogleFailure()); return; }
        bool saved = _identities.TryPrepare(key => PlayerPrefs.GetString(key, ""),
            PlayerPrefs.SetString, PlayerPrefs.Save, LoginGuestIdentityPreparation.NewIdentity,
            () => !_quitting && this != null && generation == _generation);
        _busy = false;
        if (!saved)
        {
            _diagnostics.text = "시험 저장에 실패했습니다. 설정을 보존하고 중단합니다.";
            Mark("save-failed"); return;
        }
        _expectedIdHash = Hash(PlayerPrefs.GetString(LoginGuestIdentityPreparation.CustomIdKey, ""));
        ShowSaved("손님 설정을 기기에 저장했습니다.", "prepared");
    }

    private IEnumerator SimulatedGoogleFailure()
    {
        yield return null;
        _generation++;
        _busy = false;
        ShowChoice("Google 로그인에 연결하지 못했습니다.\n다시 선택해 주세요.");
    }

    private void ShowSaved(string message, string phase)
    {
        var id = PlayerPrefs.GetString(LoginGuestIdentityPreparation.CustomIdKey, "");
        if (PlayerPrefs.GetString(LoginGuestIdentityPreparation.ModeKey, "") != "custom-pending" ||
            !LoginGuestIdentityPreparation.IsValidGuestIdentity(id) || Hash(id) != _expectedIdHash)
            throw new InvalidOperationException("Saved identity changed; no replacement is permitted.");
        _gate.Revoke();
        _view.gameObject.SetActive(false);
        _saved.text = message + "\n\n실제 PlayerPrefs 읽기 확인 완료\n서버 계정 생성·인증 요청 0";
        _saved.gameObject.SetActive(true);
        _diagnostics.text = "격리 저장·재시작 검사 · 기존 게임 앱과 분리";
        Mark(phase);
    }

    private void OnDestroy() { _quitting = true; _gate.Revoke(); }

    private void Mark(string phase)
    {
        var id = PlayerPrefs.GetString(LoginGuestIdentityPreparation.CustomIdKey, "");
        Debug.Log("GUEST_ENTRY_UI phase=" + phase + " mode=" +
            PlayerPrefs.GetString(LoginGuestIdentityPreparation.ModeKey, "empty") +
            " id_sha=" + (string.IsNullOrEmpty(id) ? "none" : Hash(id)) + " auth_stub_requests=0");
    }

    private static string Hash(string value)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }

    private TMP_Text Label(Transform parent, string name, string message, int size, float bottom, float top)
    {
        var rect = DeletionView.Rect(name, parent);
        rect.anchorMin = new Vector2(.04f, bottom); rect.anchorMax = new Vector2(.96f, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font; text.fontSize = size; text.text = message;
        text.color = new Color32(246, 241, 221, 255);
        text.alignment = TextAlignmentOptions.Center; text.richText = false; text.raycastTarget = false;
        return text;
    }
}
#endif
