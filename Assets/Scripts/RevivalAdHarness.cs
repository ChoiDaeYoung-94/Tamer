#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
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
    private readonly Queue<string> _events = new Queue<string>();
    private MonoBehaviour _owner;
    private int _ownerId, _requestId, _rewards, _finishes;
    private AudioClip _tone;
    private Vector2 _scroll;
    private Scene _initialScene, _alternateScene;
    private string _armedAction = "none";
    private double _actionAt = double.PositiveInfinity;
#if TAMER_UMP_ONLY_HARNESS
    private const bool UmpOnly = true;
#else
    private const bool UmpOnly = false;
#endif
    private AdConsentGate _ump;
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _umpCallbacks =
        new System.Collections.Concurrent.ConcurrentQueue<Action>();
    private AgeChoice _testAge = AgeChoice.Unknown;
    private DebugGeography _testGeography = DebugGeography.EEA;
    private string _testDeviceHash = "";
    private double _umpStartedAt;
    private static double Now => (double)System.Diagnostics.Stopwatch.GetTimestamp() /
        System.Diagnostics.Stopwatch.Frequency;

    private void Start()
    {
        if (Managers.Instance != null) throw new InvalidOperationException("Harness must not contain Managers.");
        var clearCamera = new GameObject("Harness background camera").AddComponent<Camera>();
        clearCamera.clearFlags = CameraClearFlags.SolidColor;
        clearCamera.backgroundColor = Color.black;
        clearCamera.cullingMask = 0;
        if (UmpOnly)
        {
            Record("ump_only_boot no_ad_manager_init sample_app_identity");
            return;
        }
        _initialScene = SceneManager.GetActiveScene();
        _alternateScene = SceneManager.CreateScene("AdHarnessEmpty");
        Ads.ConfigureHarnessAudio(Sound);
        Ads.HarnessEvent += OnAdEvent;
        Ads.Init(); // Consent and SDK work remain subject to the manager's release policy.
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
        Record(name + " callback_monotonic=" + timestamp.ToString("F3", CultureInfo.InvariantCulture)
            + " bgm_playing=" + Bgm.isPlaying + " sample=" + Bgm.timeSamples);
        if (name == "opened_callback" && _armedAction != "none") _actionAt = Now + 2;
    }

    private void Record(string value)
    {
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
            while (_umpCallbacks.TryDequeue(out var callback)) callback();
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
        GUILayout.Label("Opened callback is not native first pixel. Record native close UI separately.");
        GUI.enabled = Ads != null;
        if (GUILayout.Button("Load sample (explicit SDK initialization)", GUILayout.Height(52))) { Record("load_button"); Ads.LoadRewardedAd(); }
        if (GUILayout.Button("Show sample / policy-block control", GUILayout.Height(52))) Show();
        if (Ads != null && Ads.PrivacyOptionsRequired &&
            GUILayout.Button("Privacy options", GUILayout.Height(44))) Ads.ShowPrivacyOptions();
        GUI.enabled = true;
        if (GUILayout.Button("New receipt owner", GUILayout.Height(44))) NewOwner();
        foreach (string action in new[] { "none", "destroy owner", "destroy manager", "scene A-B-A", "replace BGM", "duplicate show" })
            if (GUILayout.Button("Arm: " + action + (_armedAction == action ? " [selected]" : ""), GUILayout.Height(38))) _armedAction = action;
        GUILayout.Label("Armed action runs >=2s after opened callback when Unity updates; never closes or rewards an ad.");
        }
        foreach (string line in _events) GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
    }

    private void DrawUmpOnly()
    {
        GUILayout.Label("UMP ONLY / SAMPLE APP ID / no Mobile Ads initialize, load or show");
        GUILayout.Label("Synthetic age case; proposed TFUA mapping is not regional approval.");
        GUILayout.Label("This sample build cannot verify the publisher's own messages.");
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && (_ump == null || !_ump.IsBusy);
        foreach (AgeChoice age in Enum.GetValues(typeof(AgeChoice)))
            if (GUILayout.Button("Test age: " + age + (_testAge == age ? " [selected]" : "")))
            { DisposeUmp(); _testAge = age; }
        foreach (var geography in new[] { DebugGeography.EEA, DebugGeography.RegulatedUSState, DebugGeography.Other })
            if (GUILayout.Button("Test geography: " + geography + (_testGeography == geography ? " [selected]" : "")))
            { DisposeUmp(); _testGeography = geography; }
        GUILayout.Label("Local UMP test-device hash (not saved or logged):");
        string hash = GUILayout.PasswordField(_testDeviceHash, '*', 32);
        if (hash != _testDeviceHash) { DisposeUmp(); _testDeviceHash = hash; }
        if (GUILayout.Button("Explicit UMP Update + required form (network)", GUILayout.Height(52))) StartUmpOnly();
        if (_ump != null && _ump.PrivacyOptionsRequired && GUILayout.Button("UMP privacy options", GUILayout.Height(44)))
            _ump.OpenPrivacyOptions(allowed => Record("ump_privacy_finished can_request=" + allowed));
        if (GUILayout.Button("Reset test consent locally", GUILayout.Height(44)))
        { DisposeUmp(); ConsentInformation.Reset(); Record("ump_test_reset"); }
        GUI.enabled = previousEnabled;
    }

    private void StartUmpOnly()
    {
        if (!UmpOnly || (_ump != null && _ump.IsBusy)) return;
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

    private void DisposeUmp()
    {
        _ump?.Dispose();
        _ump = null;
    }

    private void OnDestroy()
    {
        DisposeUmp();
        if (Ads != null) Ads.HarnessEvent -= OnAdEvent;
        if (_tone != null) Destroy(_tone);
    }
}
#endif
