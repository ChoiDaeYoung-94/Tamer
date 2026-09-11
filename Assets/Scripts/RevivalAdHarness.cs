#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
using System;
using System.Collections.Generic;
using System.Globalization;
using AD;
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
    private static double Now => (double)System.Diagnostics.Stopwatch.GetTimestamp() /
        System.Diagnostics.Stopwatch.Frequency;

    private void Start()
    {
        if (Managers.Instance != null) throw new InvalidOperationException("Harness must not contain Managers.");
        _initialScene = SceneManager.GetActiveScene();
        _alternateScene = SceneManager.CreateScene("AdHarnessEmpty");
        Ads.ConfigureHarnessAudio(Sound);
        Ads.HarnessEvent += OnAdEvent;
        Ads.Init(); // Subscribes to scene changes only; never initializes the SDK.
        NewOwner();
        _tone = AudioClip.Create("Harness tone", 44100, 1, 44100, false);
        var samples = new float[44100];
        for (int i = 0; i < samples.Length; i++) samples[i] = 0.04f * Mathf.Sin(2 * Mathf.PI * 220 * i / 44100);
        _tone.SetData(samples, 0);
        Sound.PlayBGM(_tone);
        Record("boot can_request=" + Ads.CanRequestAds + " managed_sdk_idle=true");
    }

    private void OnAdEvent(string name, double timestamp)
    {
        Record(name + " callback_monotonic=" + timestamp.ToString("F3", CultureInfo.InvariantCulture));
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
        int owner = _ownerId, request = ++_requestId;
        Record("show_button request=" + request + " owner=" + owner);
        bool accepted = Ads.ShowRewardedAd(_owner,
            () => { _rewards++; Record("reward request=" + request + " owner=" + owner + " count=" + _rewards); },
            outcome => { _finishes++; Record("finished request=" + request + " owner=" + owner + " outcome=" + outcome); });
        Record("show_accepted=" + accepted);
    }

    private void Update()
    {
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
    }

    private void OnApplicationPause(bool paused) => Record("application_pause=" + paused);
    private void OnApplicationFocus(bool focused) => Record("application_focus=" + focused);

    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.width / 720f));
        GUILayout.BeginArea(new Rect(16, 16, 688, Screen.height * 720f / Screen.width - 32));
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Label("SAMPLE ADS ONLY - isolated account/save/billing-free harness");
        GUILayout.Label("Rewards: " + _rewards + " / finishes: " + _finishes + " / owner: " + _ownerId);
        GUILayout.Label("BGM playing: " + Bgm.isPlaying + " / sample: " + Bgm.timeSamples);
        GUILayout.Label("Opened callback is not native first pixel. Record native close UI separately.");
        GUI.enabled = Ads != null;
        if (GUILayout.Button("Load sample (explicit SDK initialization)", GUILayout.Height(52))) { Record("load_button"); Ads.LoadRewardedAd(); }
        if (GUILayout.Button("Show sample / policy-block control", GUILayout.Height(52))) Show();
        GUI.enabled = true;
        if (GUILayout.Button("New receipt owner", GUILayout.Height(44))) NewOwner();
        foreach (string action in new[] { "none", "destroy owner", "destroy manager", "scene A-B-A", "replace BGM" })
            if (GUILayout.Button("Arm: " + action + (_armedAction == action ? " [selected]" : ""), GUILayout.Height(38))) _armedAction = action;
        GUILayout.Label("Armed action runs >=2s after opened callback when Unity updates; never closes or rewards an ad.");
        foreach (string line in _events) GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (Ads != null) Ads.HarnessEvent -= OnAdEvent;
        if (_tone != null) Destroy(_tone);
    }
}
#endif
