#if TAMER_PRIVACY_COLD_HARNESS && !TAMER_PRIVACY_MANAGER_HARNESS
#error Cold privacy harness requires the native privacy manager harness.
#endif
#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS || TAMER_PRIVACY_UI_HARNESS
#if TAMER_UMP_PUBLISHER_HARNESS && !TAMER_UMP_ONLY_HARNESS
#error Publisher UMP harness requires the UMP-only path.
#endif
using System;
using System.Collections.Generic;
using System.Globalization;
using AD;
using AD.Advertising;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>Explicit, isolated sample-ad exercise. Never included in normal player builds.</summary>
public sealed class RevivalAdHarness : MonoBehaviour
{
    public GoogleAdMobManager Ads;
    public SoundManager Sound;
    public AudioSource Bgm;
    public TMPro.TMP_FontAsset PrivacyUiFont;
#if TAMER_PRIVACY_UI_HARNESS
    private Managers _privacyBridge;
    private string _privacyObservation;

    private void StartPrivacyUi()
    {
        if (Application.isEditor || Application.platform != RuntimePlatform.Android || !Debug.isDebugBuild ||
            Application.identifier != "com.AeDeong.MonsterTamer.revival.privacyui" ||
            SceneManager.GetActiveScene().path != "Assets/Tests/Scenes/RevivalSmoke.unity" ||
            Managers.Instance != null || PrivacyUiFont == null ||
            AdRequestPolicy.ProductionAdsEnabled || AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed)
            throw new InvalidOperationException("Isolated offline privacy UI context required.");
        var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var inactive = new GameObject("Inactive UI references");
        inactive.SetActive(false);
        _privacyBridge = inactive.AddComponent<Managers>(); // Awake/Init never dispatched.
        if ((bool)typeof(Managers).GetField("_initialized", fields).GetValue(_privacyBridge) ||
            (bool)typeof(Managers).GetField("_ownsServices", fields).GetValue(_privacyBridge))
            throw new InvalidOperationException("Service bootstrap detected.");
        var canvasObject = new GameObject("Actual settings UI", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = .5f;
        var popups = canvasObject.AddComponent<PopupManager>();
        typeof(PopupManager).GetField("_isException", fields).SetValue(popups, false);
        Ads = new GameObject("Uninitialized actual ad manager").AddComponent<GoogleAdMobManager>();
        typeof(Managers).GetField("_googleAdMobM", fields).SetValue(_privacyBridge, Ads);
        typeof(Managers).GetField("_popupM", fields).SetValue(_privacyBridge, popups);
        typeof(Managers).GetField("instance", System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic).SetValue(null, _privacyBridge);
        DeletionPresenter.RuntimeFlowFactory = () => new AD.Privacy.DeletionFlow(
            new AD.Privacy.UnavailableDeletionGateway(), () => null);
        var settings = DeletionView.Rect("Settings", canvasObject.transform);
        settings.anchorMin = Vector2.zero; settings.anchorMax = Vector2.one;
        settings.offsetMin = settings.offsetMax = Vector2.zero;
        var template = DeletionView.Label("SettingsTitle", settings, "설정", PrivacyUiFont, 42, 0);
        template.rectTransform.anchorMin = new Vector2(.1f, .85f);
        template.rectTransform.anchorMax = new Vector2(.9f, .95f);
        template.rectTransform.offsetMin = template.rectTransform.offsetMax = Vector2.zero;
        canvasObject.AddComponent<AgeChoicePresenter>().Bind(settings.gameObject, popups, Ads);
        DeletionSettingsEntry.Ensure(settings.gameObject, popups);
        new GameObject("UI touch input", typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        Record("privacy_ui_ready age=" + Ads.AgeSelection.Value + " managers_init=false store_login_ads_init=false");
    }

    private void ObservePrivacyUi()
    {
        var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        foreach (string name in new[] { "_consent", "_privacyConsent", "_rewardedAd", "_showingAd" })
            if (typeof(GoogleAdMobManager).GetField(name, fields).GetValue(Ads) != null)
                throw new InvalidOperationException("SDK ownership appeared in offline UI.");
        foreach (string name in new[] { "_initialized", "_initializing", "_loading" })
            if ((bool)typeof(GoogleAdMobManager).GetField(name, fields).GetValue(Ads))
                throw new InvalidOperationException("SDK initialization appeared in offline UI.");
        string current = Ads.AgeSelection.Value + ":" + Ads.PrivacySettingsResult;
        if (current == _privacyObservation) return;
        _privacyObservation = current;
        Record("privacy_ui_state=" + current + " sdk_init=false");
    }
#endif
#if !TAMER_PRIVACY_UI_HARNESS
    private readonly Queue<string> _events = new Queue<string>();
    private string[] _layoutEvents = Array.Empty<string>();
    private MonoBehaviour _owner;
    private int _ownerId, _requestId, _rewards, _finishes;
    private AudioClip _tone;
    private Vector2 _scroll;
    private Scene _initialScene, _alternateScene;
    private string _armedAction = "none";
    private double _actionAt = double.PositiveInfinity;
    private double _showAt = double.NaN, _openedAt = double.NaN;
#if TAMER_UMP_ONLY_HARNESS
    private static bool UmpOnly => true;
#else
    private static bool UmpOnly => false;
#endif
    private AdConsentGate _ump;
    private AdConsentGate _umpPrivacy;
    private bool _privacyAgeScenario, _privacyAgeArmed, _privacyWasBusy;
    private double _privacyAgeAt = double.PositiveInfinity;
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _umpCallbacks =
        new System.Collections.Concurrent.ConcurrentQueue<Action>();
    private AgeChoice _testAge = AgeChoice.Unknown;
    private DebugGeography _testGeography = DebugGeography.EEA;
    private string _testDeviceHash = "";
    private bool _sampleConfigured;
#if TAMER_UMP_PUBLISHER_HARNESS
    private bool _publisherContextAllowed;
#if TAMER_PRIVACY_MANAGER_HARNESS
    private string _nativePrivacyObservation;
#endif
    private static bool _registrationAttempted;
    private bool _registrationInFlight;
    private double _registrationStartedAt;
#endif
    private double _umpStartedAt;
    private static double Now => (double)System.Diagnostics.Stopwatch.GetTimestamp() /
        System.Diagnostics.Stopwatch.Frequency;

#if TAMER_PRIVACY_COLD_HARNESS
    // File transport invokes explicit synthetic fixture APIs, never native UI input.
    private const string ColdFixtureKey = "Tamer.Harness.PrivacyCold.FixtureId";
    private static readonly string ColdProcessToken = Guid.NewGuid().ToString("N");
    private static bool _coldDriverClaimed;
    private string _coldDirectory, _coldPhase = "boot", _coldRun = "", _coldFailure = "", _coldState;
    private int _coldStatus, _coldPid, _coldCommand, _coldSnapshot, _coldUpdate, _coldUpdateCallback, _coldShow, _coldShowCallback;
    private bool _coldFreshUpdate, _coldTerminal, _coldRegistration;
    private string _coldFreshStatus = "unread";

    private sealed class ColdFixtureFailure : Exception
    {
        public readonly string Name;
        public ColdFixtureFailure(string name) { Name = name; }
    }

    [Serializable] private sealed class ColdCommand
    {
        public string processToken, runId, action, testDeviceHash;
        public int pid, sequence;
    }

    private void StartPrivacyColdFixture()
    {
        if (_coldDriverClaimed) { FailPrivacyCold("driver_recreated"); return; }
        _coldDriverClaimed = true;
        using (var process = new AndroidJavaClass("android.os.Process"))
            _coldPid = process.CallStatic<int>("myPid");
        _coldDirectory = System.IO.Path.Combine(Application.persistentDataPath, "privacy-cold-fixture");
        System.IO.Directory.CreateDirectory(_coldDirectory);
        if (Ads == null || !Ads.ConfigureNativePrivacyColdFixture())
        { FailPrivacyCold("boot_context_or_purchase_input"); return; }
        Ads.NativePrivacyColdSdkEvent += ObservePrivacyColdSdk;
        Ads.HarnessEvent += ObservePrivacyColdManagedAds;
        Record("privacy_cold_boot token=" + ColdProcessToken + " pid=" + _coldPid +
            " synthetic_fixture_only=true automatic_sdk_request=false");
        ObservePrivacyColdState();
    }

    private void ObservePrivacyColdManagedAds(string name, double timestamp) =>
        FailPrivacyCold("unexpected_managed_ads_" + name);

    private void ObservePrivacyColdSdk(string value, double receivedAt)
    {
        // Delivered on the owner thread; SDK receive time is distinct from dispatch time.
        Record("privacy_cold_sdk received=" + receivedAt.ToString("F6", CultureInfo.InvariantCulture) + " " + value);
        if (value.StartsWith("update_call ", StringComparison.Ordinal))
        {
            if (++_coldUpdate > 1) FailPrivacyCold("multiple_update");
            if (!_coldRegistration && (Ads.AgeSelection.Value != AgeChoice.Adult || !Ads.HasNoAds))
                FailPrivacyCold("update_fixture_mismatch");
        }
        else if (value.StartsWith("update_callback ", StringComparison.Ordinal))
        {
            if (++_coldUpdateCallback > 1) FailPrivacyCold("duplicate_update_callback");
            _coldFreshUpdate = value.StartsWith("update_callback ok=True ", StringComparison.Ordinal);
        }
        else if (value.StartsWith("status_read ", StringComparison.Ordinal))
        {
            ++_coldStatus;
            if (!_coldFreshUpdate || _coldUpdate != 1 || _coldRegistration) FailPrivacyCold("status_without_fresh_update");
            _coldFreshStatus = value.Contains("value=Required ") ? "Required"
                : value.Contains("value=NotRequired ") ? "NotRequired" : "Unknown";
        }
        else if (value.StartsWith("show_call ", StringComparison.Ordinal))
        {
            if (++_coldShow > 1 || !_coldFreshUpdate || _coldFreshStatus != "Required" || _coldRegistration)
                FailPrivacyCold("show_without_fresh_required");
        }
        else if (value.StartsWith("show_callback ", StringComparison.Ordinal))
        {
            if (++_coldShowCallback > 1 || _coldShow != 1) FailPrivacyCold("unexpected_show_callback");
        }
        else if (value.StartsWith("unexpected_gather", StringComparison.Ordinal)) FailPrivacyCold("unexpected_gather");
    }

    private void FailPrivacyCold(string name)
    {
        if (_coldFailure.Length != 0) return;
        _coldFailure = name;
        _coldPhase = "failed"; // Seals developer commands; never closes an active native form.
        if (Ads != null) Ads.SealNativePrivacyColdFixture();
        Record("privacy_cold_failed reason=" + name);
    }

    private void PollPrivacyColdFixture()
    {
        if (_coldDirectory == null) return;
        if (Ads == null || !Ads.NativePrivacyColdContextAllowed || Ads.CanRequestAds)
            FailPrivacyCold("context_or_ad_state_changed");
        if (_coldFailure.Length == 0 && _coldPhase == "refreshPending" && !Ads.IsConsentBusy &&
            Ads.PrivacySettingsResult != AdPrivacyResult.Checking)
        {
            _coldTerminal = true;
            _coldPhase = Ads.PrivacySettingsResult == AdPrivacyResult.AgeRequired ? "ageBlocked" : "refreshTerminal";
        }
        if (_coldPhase == "registrationPending" && !_registrationInFlight)
            _coldPhase = "registrationTerminal"; // Callback/timeout details stay in SDK evidence.
        string ready = System.IO.Path.Combine(_coldDirectory, "command-" + ColdProcessToken + ".ready");
        if (_coldFailure.Length == 0 && System.IO.File.Exists(ready))
        {
            string processing = ready + ".processing";
            try
            {
                // Host writes .tmp then renames to .ready; claim is another atomic rename.
                System.IO.File.Move(ready, processing);
                if (new System.IO.FileInfo(processing).Length > 4096) throw new ColdFixtureFailure("command_size");
                var command = JsonUtility.FromJson<ColdCommand>(System.IO.File.ReadAllText(processing));
                System.IO.File.Delete(processing); // Do not retain a supplied SDK hash on disk.
                if (command == null || command.processToken != ColdProcessToken || command.pid != _coldPid ||
                    command.sequence != _coldCommand + 1 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(command.runId ?? "", "\\A[A-Za-z0-9_-]{1,64}\\z") ||
                    (_coldRun.Length != 0 && command.runId != _coldRun))
                    throw new ColdFixtureFailure("command_identity_or_sequence");
                _coldRun = command.runId;
                _coldCommand = command.sequence; // Consume before invoking the fixture API.
                ExecutePrivacyColdFixture(command);
                command.testDeviceHash = null;
            }
            catch (ColdFixtureFailure error) { FailPrivacyCold(error.Name); }
            catch (Exception error) { FailPrivacyCold("command_" + error.GetType().Name); }
            finally { if (System.IO.File.Exists(processing)) System.IO.File.Delete(processing); }
        }
        ObservePrivacyColdState();
    }

    private void ExecutePrivacyColdFixture(ColdCommand command)
    {
        if (_coldFailure.Length != 0 || Ads == null || !Ads.NativePrivacyColdContextAllowed)
            throw new ColdFixtureFailure("fixture_context");
        if (command.action == "prepareAdultNoAds")
        {
            if (_coldPhase != "boot" || GoogleAdMobManager.NativePrivacyHarnessAttempted ||
                PlayerPrefs.HasKey(ColdFixtureKey) || PlayerPrefs.HasKey(GoogleAdMobManager.AgeChoicePreferenceKey))
                throw new ColdFixtureFailure("prepare_requires_unseeded_package");
            if (!Ads.AgeSelection.Select(AgeChoice.Adult)) throw new ColdFixtureFailure("adult_save");
            PlayerPrefs.SetString(GoogleAdMobManager.NativePrivacyColdPurchaseKey, "ProductNoAds");
            PlayerPrefs.SetString(ColdFixtureKey, command.runId);
            PlayerPrefs.Save();
            if (!Ads.ConfigureNativePrivacyColdFixture() || !Ads.HasNoAds) throw new ColdFixtureFailure("purchase_fixture");
            _coldPhase = "fixturePrepared"; // SDK request requires a fresh process, enforced below.
        }
        else if (command.action == "requestSavedPrivacy")
        {
            if (_coldPhase != "boot" || PlayerPrefs.GetString(ColdFixtureKey, "") != command.runId ||
                !Ads.HasNoAds || _registrationAttempted)
                throw new ColdFixtureFailure("request_requires_cold_saved_fixture");
            _coldPhase = "refreshPending";
            if (!Ads.RequestPrivacySettingsForNativeHarness(command.testDeviceHash))
                throw new ColdFixtureFailure("request_prerequisite_or_hash");
        }
        else if (command.action == "saveDeclinedAfterTerminal")
        {
            if (_coldPhase != "refreshTerminal" || !_coldTerminal || Ads.IsConsentBusy ||
                (_coldShow != 0 && _coldShowCallback != 1))
                throw new ColdFixtureFailure("decline_requires_native_terminal");
            if (!Ads.AgeSelection.Select(AgeChoice.Declined)) throw new ColdFixtureFailure("declined_save");
            _coldPhase = "declinedSaved";
        }
        else if (command.action == "registrationOnly")
        {
            if (_coldPhase != "boot" || GoogleAdMobManager.NativePrivacyHarnessAttempted)
                throw new ColdFixtureFailure("registration_requires_unused_process");
            _coldRegistration = true;
            StartUmpRegistrationOnly();
            if (!_registrationAttempted) throw new ColdFixtureFailure("registration_prerequisite");
            _coldPhase = "registrationPending"; // No other commands allowed in this process.
        }
        else throw new ColdFixtureFailure("unknown_fixture_action");
        Record("privacy_cold_command sequence=" + _coldCommand + " action=" + command.action + " phase=" + _coldPhase);
    }

    private void ObservePrivacyColdState()
    {
        if (_coldDirectory == null) return;
        string state = "token=" + ColdProcessToken + " pid=" + _coldPid + " run=" + _coldRun +
            " fixture=" + PlayerPrefs.GetString(ColdFixtureKey, "") + " phase=" + _coldPhase + " failure=" + _coldFailure +
            " command=" + _coldCommand + " age=" + (Ads == null ? "absent" : Ads.AgeSelection.Value.ToString()) +
            " editing=" + (Ads != null && Ads.AgeSelection.IsEditing) +
            " syntheticNoAds=" + (Ads != null && Ads.HasNoAds) +
            " ageEncoded=" + (Ads == null ? "" : LocalAgeChoice.Encode(Ads.AgeSelection.Value)) +
            " result=" + (Ads == null ? "absent" : Ads.PrivacySettingsResult.ToString()) +
            " busy=" + (Ads != null && Ads.IsConsentBusy) + " required=" + (Ads != null && Ads.PrivacyOptionsRequired) +
            " noManagedAdState=" + (Ads != null && Ads.NativePrivacyHarnessNoAdsState) +
            " managersAbsent=" + (Managers.Instance == null) + " update=" + _coldUpdate +
            " updateCallback=" + _coldUpdateCallback + " freshStatus=" + _coldFreshStatus +
            " statusReads=" + _coldStatus + " show=" + _coldShow + " showCallback=" + _coldShowCallback;
        if (state == _coldState) return;
        _coldState = state;
        Record("privacy_cold_state " + state);
        // Immutable snapshots use atomic rename; no delete/overwrite of another process's evidence.
        string path = System.IO.Path.Combine(_coldDirectory, "state-" + ColdProcessToken + "-" + (++_coldSnapshot) + ".txt");
        try
        {
            System.IO.File.WriteAllText(path + ".tmp", state);
            System.IO.File.Move(path + ".tmp", path);
        }
        catch (Exception error) { FailPrivacyCold("evidence_write_" + error.GetType().Name); }
    }
#endif

    private void Start()
    {
        if (Managers.Instance != null) throw new InvalidOperationException("Harness must not contain Managers.");
        var clearCamera = new GameObject("Harness background camera").AddComponent<Camera>();
        clearCamera.clearFlags = CameraClearFlags.SolidColor;
        clearCamera.backgroundColor = Color.black;
        clearCamera.cullingMask = 0;
        if (UmpOnly)
        {
#if TAMER_UMP_PUBLISHER_HARNESS
#if TAMER_PRIVACY_COLD_HARNESS
            const string publisherHarnessPackage = AgeTreatmentPolicy.NativePrivacyColdHarnessPackage;
#elif TAMER_PRIVACY_MANAGER_HARNESS
            const string publisherHarnessPackage = AgeTreatmentPolicy.NativePrivacyHarnessPackage;
#else
            const string publisherHarnessPackage = "com.AeDeong.MonsterTamer.revival.umppublisher";
#endif
            if (Application.isEditor || Application.platform != RuntimePlatform.Android ||
                !Debug.isDebugBuild || Application.identifier != publisherHarnessPackage ||
                SceneManager.GetActiveScene().path != "Assets/Tests/Scenes/RevivalAdHarness.unity" ||
                AdRequestPolicy.ProductionAdsEnabled || AgeTreatmentPolicy.RegionalConsentReviewed)
                throw new InvalidOperationException("Isolated disabled publisher UMP context required.");
            _publisherContextAllowed = true;
            Record("ump_only_boot no_ad_manager_init publisher_app_identity");
#if TAMER_PRIVACY_COLD_HARNESS
            StartPrivacyColdFixture();
#endif
#else
            Record("ump_only_boot no_ad_manager_init sample_app_identity");
#endif
            return;
        }
        _initialScene = SceneManager.GetActiveScene();
        _alternateScene = SceneManager.CreateScene("AdHarnessEmpty");
        Ads.ConfigureHarnessAudio(Sound);
        Ads.HarnessEvent += OnAdEvent;
        NewOwner();
        _tone = AudioClip.Create("Harness tone", 44100, 1, 44100, false);
        var samples = new float[44100];
        for (int i = 0; i < samples.Length; i++) samples[i] = 0.04f * Mathf.Sin(2 * Mathf.PI * 220 * i / 44100);
        _tone.SetData(samples, 0);
        Sound.PlayBGM(_tone);
        Record("boot can_request=" + Ads.CanRequestAds);
    }

    private void OnAdEvent(string name, double timestamp)
    {
        if (name == "show_call") { _showAt = timestamp; _openedAt = double.NaN; }
        if (name == "opened_callback") _openedAt = timestamp;
        Record(name + " callback_monotonic=" + timestamp.ToString("F3", CultureInfo.InvariantCulture)
            + (name == "closed_callback" || name == "earned_callback"
                ? " since_show=" + Elapsed(timestamp, _showAt) + " since_open_callback=" + Elapsed(timestamp, _openedAt)
                : "")
            + " bgm_playing=" + Bgm.isPlaying + " sample=" + Bgm.timeSamples);
        if (name == "opened_callback" && _armedAction != "none") _actionAt = Now + 2;
    }

    private static string Elapsed(double timestamp, double origin) => double.IsNaN(origin)
        ? "unknown" : (timestamp - origin).ToString("F3", CultureInfo.InvariantCulture);

    private void Record(string value)
    {
        if (UmpOnly && _privacyAgeArmed &&
            (value == "consent_form_call" || value == "privacy_options_call"))
        {
            _privacyAgeArmed = false;
            _privacyAgeAt = Now + 2;
        }
        string line = Now.ToString("F3", CultureInfo.InvariantCulture) + " " + value;
        _events.Enqueue(line);
        while (_events.Count > 14) _events.Dequeue();
        Debug.Log("AD_HARNESS " + line);
    }

    private void NewOwner()
    {
        if (_owner != null) Destroy(_owner.gameObject);
        _owner = new GameObject("Receipt owner " + ++_ownerId).AddComponent<RevivalAdReceiptOwner>();
        Record("owner_created=" + _ownerId);
    }

    private void Show()
    {
        if (UmpOnly) return;
        int owner = _ownerId, request = ++_requestId;
        Record("show_button request=" + request + " owner=" + owner);
        bool accepted = Ads.ShowRewardedAd(_owner,
            () => { _rewards++; Record("reward request=" + request + " owner=" + owner + " count=" + _rewards); },
            outcome => { _finishes++; Record("finished request=" + request + " owner=" + owner + " outcome=" + outcome); });
        Record("show_accepted=" + accepted);
    }

    private void Update()
    {
        if (UmpOnly)
        {
#if TAMER_PRIVACY_COLD_HARNESS
            PollPrivacyColdFixture();
#endif
#if TAMER_UMP_PUBLISHER_HARNESS
            if (_registrationInFlight && Now - _registrationStartedAt >= 30)
            {
                _registrationInFlight = false;
                Record("ump_registration_halted timeout no_retry");
            }
#endif
            while (_umpCallbacks.TryDequeue(out var callback)) callback();
            if (Now >= _privacyAgeAt)
            {
                _privacyAgeAt = double.PositiveInfinity;
                SuspendUmpAge();
            }
            bool privacyBusy = (_ump != null && _ump.IsBusy) || (_umpPrivacy != null && _umpPrivacy.IsBusy);
            if (_privacyAgeScenario && _testAge == AgeChoice.Declined && _privacyWasBusy && !privacyBusy)
                Record("ump_age_native_settled required=" + (UmpPrivacyOwner != null)
                    + " can_request=" + ((_ump != null && _ump.CanRequestAds) ||
                        (_umpPrivacy != null && _umpPrivacy.CanRequestAds)));
            _privacyWasBusy = privacyBusy;
            if (_ump != null && _ump.IsUpdating && Now - _umpStartedAt >= 30) _ump.ExpireUpdate();
            return;
        }
        if (Now < _actionAt) return;
        _actionAt = double.PositiveInfinity;
        string action = _armedAction;
        _armedAction = "none";
        Record("scheduled_action=" + action + " bgm_playing=" + Bgm.isPlaying + " sample=" + Bgm.timeSamples);
        if (action == "destroy owner" && _owner != null) Destroy(_owner.gameObject);
        else if (action == "destroy manager") Destroy(Ads.gameObject);
        else if (action == "scene A-B-A")
        {
            SceneManager.SetActiveScene(_alternateScene);
            SceneManager.SetActiveScene(_initialScene);
        }
        else if (action == "replace BGM") Sound.PlayBGM(_tone);
        else if (action == "duplicate show") Show();
    }

    private void OnApplicationPause(bool paused) => Record("application_pause=" + paused
        + " bgm_playing=" + (Bgm != null && Bgm.isPlaying));
    private void OnApplicationFocus(bool focused) => Record("application_focus=" + focused);

    private void OnGUI()
    {
#if !TAMER_PRIVACY_COLD_HARNESS
        // Keep the same log controls throughout each Layout/input/Repaint cycle.
        if (Event.current.type == EventType.Layout) _layoutEvents = _events.ToArray();
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.width / 720f));
        GUILayout.BeginArea(new Rect(16, 16, 688, Screen.height * 720f / Screen.width - 32));
        _scroll = GUILayout.BeginScrollView(_scroll);
        if (UmpOnly) DrawUmpOnly();
        else
        {
            GUILayout.Label("SAMPLE ADS ONLY - isolated account/save/billing-free harness");
            GUILayout.Label("Rewards: " + _rewards + " / finishes: " + _finishes + " / owner: " + _ownerId);
            GUILayout.Label("BGM playing: " + Bgm.isPlaying + " / sample: " + Bgm.timeSamples);
            GUILayout.Label("SDK callbacks are not native first pixel/X. Capture screen video and actual X tap separately.");
#if UNITY_EDITOR || TAMER_AD_SAMPLE_CLOSE_HARNESS
            GUILayout.Label("Synthetic sample age and UMP region; test-device hash is never saved or logged.");
#if UNITY_EDITOR
            GUILayout.Label("Editor preview only. Network sample requests require the isolated Android APK.");
#endif
            GUI.enabled = Ads != null && !_sampleConfigured;
            foreach (AgeChoice age in Enum.GetValues(typeof(AgeChoice)))
                if (GUILayout.Button("Sample age: " + age + (_testAge == age ? " [selected]" : ""))) _testAge = age;
            foreach (var geography in new[] { DebugGeography.EEA, DebugGeography.RegulatedUSState, DebugGeography.Other })
                if (GUILayout.Button("Sample region: " + geography + (_testGeography == geography ? " [selected]" : "")))
                    _testGeography = geography;
            GUILayout.Label("Local UMP test-device hash (32 hex):");
            _testDeviceHash = GUILayout.PasswordField(_testDeviceHash, '*', 32);
            GUI.enabled = true;
            GUILayout.Label(_sampleConfigured
                ? "Sample case locked. Clear app data and relaunch before a different age/region case."
                : "Select a sample case before the first Load. Clear app data between cases.");
#endif
            GUI.enabled = Ads != null && !Application.isEditor;
            if (GUILayout.Button("Load sample (explicit UMP + SDK initialization)", GUILayout.Height(52)))
            {
                Record("load_button");
#if UNITY_EDITOR || TAMER_AD_SAMPLE_CLOSE_HARNESS
                if (Ads.ConfigureSampleHarness(_testAge, _testGeography, _testDeviceHash))
                {
                    _sampleConfigured = true;
                    Ads.LoadRewardedAd();
                }
                else Record("sample_configuration_blocked");
#else
                Ads.LoadRewardedAd();
#endif
            }
            GUI.enabled = Ads != null;
            if (GUILayout.Button("Show sample / policy-block control", GUILayout.Height(52))) Show();
            bool samplePrivacyOptionsRequired = Ads != null && Ads.PrivacyOptionsRequired;
            GUI.enabled = samplePrivacyOptionsRequired;
            if (GUILayout.Button("Privacy options", GUILayout.Height(44)) && samplePrivacyOptionsRequired)
                Ads.ShowPrivacyOptions();
            GUI.enabled = true;
            if (GUILayout.Button("New receipt owner", GUILayout.Height(44))) NewOwner();
            foreach (string action in new[] { "none", "destroy owner", "destroy manager", "scene A-B-A", "replace BGM", "duplicate show" })
                if (GUILayout.Button("Arm: " + action + (_armedAction == action ? " [selected]" : ""), GUILayout.Height(38))) _armedAction = action;
            GUILayout.Label("Armed action runs >=2s after opened callback when Unity updates; never closes or rewards an ad.");
        }
        foreach (string line in _layoutEvents) GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
#endif
    }

    private void DrawUmpOnly()
    {
#if TAMER_PRIVACY_MANAGER_HARNESS
        GUILayout.Label("Actual manager privacy refresh / synthetic Adult / EEA / no ad initialization");
        GUILayout.Label("One Update and at most one explicit reopen per process. No reset, Gather or automatic retry.");
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && _publisherContextAllowed && Ads != null &&
            !GoogleAdMobManager.NativePrivacyHarnessAttempted;
        GUILayout.Label("Local registered UMP test-device input (not saved or logged):");
        _testDeviceHash = GUILayout.PasswordField(_testDeviceHash, '*', 32);
        if (GUILayout.Button("Explicit manager privacy refresh (network, once)", GUILayout.Height(52)))
            Record("manager_privacy_request_accepted=" + Ads.RequestPrivacySettingsForNativeHarness(_testDeviceHash));
        GUI.enabled = enabled && _publisherContextAllowed && Ads != null && Ads.NativePrivacyHarnessCanReopen;
        if (GUILayout.Button("Reopen existing manager privacy options (Show only, once)", GUILayout.Height(52)))
            Record("manager_privacy_reopen_accepted=" + Ads.ReopenPrivacySettingsForNativeHarness());
        GUI.enabled = enabled;
        if (Ads != null)
        {
            if (!Ads.NativePrivacyHarnessNoAdsState) throw new InvalidOperationException("Ad ownership appeared in privacy harness.");
            GUILayout.Label("Manager privacy result: " + Ads.PrivacySettingsResult);
            string observed = Ads.PrivacySettingsResult.ToString();
            if (_nativePrivacyObservation != observed)
            {
                _nativePrivacyObservation = observed;
                Record("manager_privacy_result=" + observed + " ads_init_load=false");
            }
        }
#else
#if TAMER_UMP_PUBLISHER_HARNESS
        GUILayout.Label("UMP ONLY / PUBLISHER APP / no managed Mobile Ads initialize, load or show");
        GUILayout.Label("Explicit Update contacts the publisher's UMP service; this is not an ad activation.");
#else
        GUILayout.Label("UMP ONLY / SAMPLE APP ID / no Mobile Ads initialize, load or show");
        GUILayout.Label("This sample build cannot verify the publisher's own messages.");
#endif
        GUILayout.Label("Synthetic age case; proposed TFUA mapping is not regional approval.");
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && (_ump == null || !_ump.IsBusy);
#if TAMER_UMP_PUBLISHER_HARNESS
        GUI.enabled = GUI.enabled && _publisherContextAllowed && !_registrationAttempted && _ump == null;
        GUILayout.Label("Registration only: one TFUA=true Update, no debug region/hash or consent form.");
        if (GUILayout.Button("Explicit UMP registration only (network, once)", GUILayout.Height(52)))
            StartUmpRegistrationOnly();
        GUILayout.Label(!_registrationAttempted ? "Registration has not started."
            : _registrationInFlight ? "Registration pending; all consent controls locked."
            : "Registration halted. Verify exactly one original SDK hash in private own-PID logs before any next test.");
        GUI.enabled = previousEnabled && _publisherContextAllowed && !_registrationAttempted &&
            (_ump == null || !_ump.IsBusy);
#endif
        bool originalControlsEnabled = GUI.enabled;
        GUI.enabled = originalControlsEnabled && !_privacyAgeScenario;
        foreach (AgeChoice age in Enum.GetValues(typeof(AgeChoice)))
            if (GUILayout.Button("Test age: " + age + (_testAge == age ? " [selected]" : "")))
            { DisposeUmp(); _testAge = age; }
        foreach (var geography in new[] { DebugGeography.EEA, DebugGeography.RegulatedUSState, DebugGeography.Other })
            if (GUILayout.Button("Test geography: " + geography + (_testGeography == geography ? " [selected]" : "")))
            { DisposeUmp(); _testGeography = geography; }
        GUILayout.Label("Local UMP test-device hash (not saved or logged):");
        string hash = GUILayout.PasswordField(_testDeviceHash, '*', 32);
        if (hash != _testDeviceHash) { DisposeUmp(); _testDeviceHash = hash; }
        GUI.enabled = originalControlsEnabled && (!_privacyAgeScenario ||
            (_privacyAgeArmed && _testAge == AgeChoice.Adult && _ump == null));
        if (GUILayout.Button("Explicit UMP Update + required form (network)", GUILayout.Height(52))) StartUmpOnly();
        bool consentControlsEnabled = previousEnabled &&
            (_ump == null || !_ump.IsBusy) && (_umpPrivacy == null || !_umpPrivacy.IsBusy);
        bool privacyOptionsRequired = UmpPrivacyOwner != null;
        GUI.enabled = consentControlsEnabled && privacyOptionsRequired;
        if (GUILayout.Button("UMP privacy options", GUILayout.Height(44)) && privacyOptionsRequired)
            UmpPrivacyOwner.OpenPrivacyOptions(allowed => Record("ump_privacy_finished can_request=" + allowed));
        GUI.enabled = originalControlsEnabled && consentControlsEnabled && !_privacyAgeScenario &&
            _testAge == AgeChoice.Adult && (_ump == null || privacyOptionsRequired);
        if (GUILayout.Button("Arm Declined age change 2s into next native form", GUILayout.Height(44)))
        {
            _privacyAgeScenario = true;
            _privacyAgeArmed = true;
            Record("ump_age_arm explicit_native_form_required no_auto_network");
        }
        GUI.enabled = consentControlsEnabled && !_privacyAgeScenario &&
            _testAge == AgeChoice.Adult && privacyOptionsRequired;
        if (GUILayout.Button("Preserve existing privacy owner; change age to Declined", GUILayout.Height(44)))
            SuspendUmpAge();
        GUILayout.Label("Privacy preservation: " + (_privacyAgeScenario ? "locked until restart" : "not started")
            + "; Required=" + privacyOptionsRequired);
        GUI.enabled = consentControlsEnabled && !_privacyAgeScenario;
        GUI.enabled = GUI.enabled && AgeTreatmentPolicy.TryCreatePlan(_testAge, out _);
        if (GUILayout.Button("Reset test consent locally", GUILayout.Height(44)))
        { DisposeUmp(); ConsentInformation.Reset(); Record("ump_test_reset"); }
        GUI.enabled = previousEnabled;
#endif
    }

    private void StartUmpOnly()
    {
        if (!UmpOnly || (_ump != null && _ump.IsBusy) || (_umpPrivacy != null && _umpPrivacy.IsBusy) ||
            (_privacyAgeScenario && (!_privacyAgeArmed || _testAge != AgeChoice.Adult || _ump != null))) return;
#if TAMER_UMP_PUBLISHER_HARNESS
        if (!_publisherContextAllowed || _registrationAttempted)
        { Record("ump_blocked publisher_context_or_registration_latch"); return; }
#endif
        DisposeUmp();
        if (!AgeTreatmentPolicy.TryCreatePlan(_testAge, out var plan))
        { Record("ump_blocked unknown_or_declined_age"); return; }
        GoogleUmpConsentClient client;
        try { client = new GoogleUmpConsentClient(_testGeography, _testDeviceHash); }
        catch (ArgumentException) { Record("ump_blocked invalid_test_configuration"); return; }
        _ump = new AdConsentGate(client, plan.UmpUnderAgeOfConsent, action => _umpCallbacks.Enqueue(action), Record);
        _umpStartedAt = Now;
        Record("ump_test_start geography=" + _testGeography + " tfua=" + plan.UmpUnderAgeOfConsent);
        _ump.Request(allowed => Record("ump_finished can_request=" + allowed + " ads_disabled=true"));
    }

#if TAMER_UMP_PUBLISHER_HARNESS
    private void StartUmpRegistrationOnly()
    {
        if (!UmpOnly || !_publisherContextAllowed || _registrationAttempted || _ump != null ||
            Managers.Instance != null || Application.isEditor || Application.platform != RuntimePlatform.Android ||
            !Debug.isDebugBuild ||
#if TAMER_PRIVACY_COLD_HARNESS
            Application.identifier != AgeTreatmentPolicy.NativePrivacyColdHarnessPackage ||
#else
            Application.identifier != "com.AeDeong.MonsterTamer.revival.umppublisher" ||
#endif
            SceneManager.GetActiveScene().path != "Assets/Tests/Scenes/RevivalAdHarness.unity" ||
            AdRequestPolicy.ProductionAdsEnabled || AgeTreatmentPolicy.RegionalConsentReviewed) return;
#if TAMER_PRIVACY_COLD_HARNESS
        if (Ads == null || !Ads.ReserveNativePrivacyColdRegistration()) return;
#endif
        // Process-lifetime latch survives owner recreation. Neither timeout nor callback permits retry.
        _registrationAttempted = true;
        _registrationInFlight = true;
        _registrationStartedAt = Now;
        Record("ump_registration_start tfua=true forced_debug_settings=false forms=false ads_disabled=true");
        try
        {
#if TAMER_PRIVACY_COLD_HARNESS
            var registrationClient = new GoogleUmpConsentClient((value, receivedAt) =>
                _umpCallbacks.Enqueue(() => ObservePrivacyColdSdk(value, receivedAt)));
#else
            var registrationClient = new GoogleUmpConsentClient();
#endif
            registrationClient.Update(true, succeeded => _umpCallbacks.Enqueue(() =>
            {
                if (this == null || !_registrationInFlight) return;
                _registrationInFlight = false;
                Record(succeeded ? "ump_registration_halted update_completed await_private_sdk_hash no_retry"
                    : "ump_registration_halted update_failed no_retry");
            }));
        }
        catch (Exception exception)
        {
            _registrationInFlight = false;
            Record("ump_registration_halted exception no_retry exception_type=" + exception.GetType().Name);
        }
    }
#endif

    private void DisposeUmp()
    {
        _ump?.Dispose();
        _ump = null;
        _umpPrivacy?.Dispose();
        _umpPrivacy = null;
    }

    private AdConsentGate UmpPrivacyOwner => _ump != null && _ump.PrivacyOptionsRequired ? _ump
        : _umpPrivacy != null && _umpPrivacy.PrivacyOptionsRequired ? _umpPrivacy : null;

    private void SuspendUmpAge()
    {
        if (!UmpOnly || _testAge != AgeChoice.Adult || _ump == null) return;
        bool busyBefore = _ump.IsBusy;
        _privacyAgeScenario = true;
        _testAge = AgeChoice.Declined;
        AdConsentGate.SuspendAndRetainPrivacy(ref _ump, ref _umpPrivacy);
        _privacyWasBusy = (_ump != null && _ump.IsBusy) || (_umpPrivacy != null && _umpPrivacy.IsBusy);
        Record("ump_age_suspended declined=true busy_before=" + busyBefore + " busy_after=" + _privacyWasBusy
            + " required=" + (UmpPrivacyOwner != null) + " can_request=" +
            ((_ump != null && _ump.CanRequestAds) || (_umpPrivacy != null && _umpPrivacy.CanRequestAds)));
    }

    private void OnDestroy()
    {
#if TAMER_UMP_PUBLISHER_HARNESS
        _registrationInFlight = false;
#endif
        DisposeUmp();
#if TAMER_PRIVACY_COLD_HARNESS
        if (Ads != null)
        {
            Ads.NativePrivacyColdSdkEvent -= ObservePrivacyColdSdk;
            Ads.HarnessEvent -= ObservePrivacyColdManagedAds;
        }
#endif
        if (Ads != null) Ads.HarnessEvent -= OnAdEvent;
        if (_tone != null) Destroy(_tone);
    }
#else
    private void Start() => StartPrivacyUi();
    private void Update() { if (Ads != null) ObservePrivacyUi(); }
    private void Record(string value) => Debug.Log("PRIVACY_UI " + value);
    private void OnDestroy()
    {
        DeletionPresenter.RuntimeFlowFactory = null;
        if (_privacyBridge != null) Destroy(_privacyBridge.gameObject);
    }
#endif
}
#endif
