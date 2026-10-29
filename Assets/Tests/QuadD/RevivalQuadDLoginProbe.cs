#if UNITY_EDITOR || TAMER_QUAD_D_LOGIN_HARNESS
using System;
using System.Collections;
using System.Reflection;
using AD;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using RuntimeSceneManager = UnityEngine.SceneManagement.SceneManager;

// Build-only observer for the canonical Intro -> Login path, with the existing
// memory login guard. Never compiled into the operating player.
public sealed class RevivalQuadDLoginProbe : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string mode;
    private int introEntries, loginEntries, completions;
    private long highestFrame = -1;
    private bool videoStoppedAtCallback, audioStoppedAtCallback;
    private VideoPlayer video;
    private AudioSource audio;
    private QuadD.QuadDIntro intro;

#if TAMER_QUAD_D_LOGIN_HARNESS
#if !TAMER_REVIVAL_SMOKE || !TAMER_GAMEPLAY_HARNESS || !TAMER_SHOP_HARNESS || !TAMER_SHOP_UI || !TAMER_FACELIFT_UI_HARNESS
#error Intro/login probe requires the complete offline UI isolation contract.
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor || !Debug.isDebugBuild ||
            Application.identifier != RevivalGameplayIsolation.FaceliftIntroLoginApplicationId)
            throw new InvalidOperationException("Separate offline intro/login application required.");
        var root = new GameObject("Offline canonical intro/login observer");
        DontDestroyOnLoad(root);
        root.AddComponent<RevivalQuadDLoginProbe>();
    }
#endif

    private void Awake()
    {
        mode = "natural";
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            mode = intent.Call<string>("getStringExtra", "revivalQuadDProbe") ?? "natural";
#endif
        if (mode != "natural" && mode != "decoder-error" && mode != "stalled-playback")
            throw new InvalidOperationException("Unknown isolated intro probe mode.");
        RuntimeSceneManager.sceneLoaded += SceneLoaded;
        StartCoroutine(ObserveResult());
    }

    private void OnDestroy() => RuntimeSceneManager.sceneLoaded -= SceneLoaded;

    private void SceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name == "Login") { loginEntries++; return; }
        if (scene.path != "Assets/QuadD/Scenes/QuadDIntro.unity") return;
        introEntries++;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<QuadD.QuadDIntro>() != null) intro = root.GetComponent<QuadD.QuadDIntro>();
        if (intro == null || intro.NextSceneName != "Login" || intro.GetComponent<QuadDIntroCanvasOutput>() == null)
            throw new InvalidOperationException("Canonical intro configuration changed.");
        video = intro.GetComponent<VideoPlayer>();
        audio = intro.GetComponent<AudioSource>();
        intro.OnCompleted.AddListener(() => {
            completions++;
            videoStoppedAtCallback = video != null && !video.isPlaying;
            audioStoppedAtCallback = audio == null || !audio.isPlaying;
        });
        StartCoroutine(ExerciseFailure());
    }

    private void Update()
    {
        if (video != null && video.frame > highestFrame) highestFrame = video.frame;
    }

    private IEnumerator ExerciseFailure()
    {
        yield return null; // Let the original Start assign/prepare its VideoPlayer.
        if (mode == "decoder-error" && intro != null)
        {
            Debug.Log("INTRO_LOGIN_SIMULATED decoder-error callback");
            typeof(QuadD.QuadDIntro).GetMethod("HandleError", Private)
                .Invoke(intro, new object[] { video, "isolated decoder callback fixture" });
        }
        else if (mode == "stalled-playback")
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (video != null && !video.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            if (video == null || !video.isPlaying) yield break;
            Debug.Log("INTRO_LOGIN_SIMULATED paused decoder with shortened playback deadline");
            video.Pause();
            typeof(QuadD.QuadDIntro).GetField("playbackDeadline", Private)
                .SetValue(intro, Time.realtimeSinceStartupAsDouble + .5d);
        }
    }

    private IEnumerator ObserveResult()
    {
        float deadline = Time.realtimeSinceStartup + 22;
        while ((loginEntries == 0 || RevivalGameplayIsolation.BlockedLogins != 1 ||
            RevivalGameplayIsolation.Reads == 0) && Time.realtimeSinceStartup < deadline) yield return null;
        // Native Stop/AudioSource state may settle after the completion callback.
        // Observe the following frame and the actual destination, not callback timing.
        yield return null;
        bool stoppedAtDestination = (video == null || !video.isPlaying) && (audio == null || !audio.isPlaying);
        bool passed = introEntries == 1 && loginEntries == 1 && completions == 1 && stoppedAtDestination &&
            (mode != "natural" || highestFrame > 0) && RevivalGameplayIsolation.BlockedLogins == 1 &&
            RevivalGameplayIsolation.Reads > 0 && Managers.Instance != null && Managers.DataM.IsServerDataReady &&
            !Managers.GoogleAdMobM.CanRequestAds && Managers.IAPM.Status == IAPStatus.Unavailable;
        Debug.Log((passed ? "INTRO_LOGIN_PASS" : "INTRO_LOGIN_FAIL") + " mode=" + mode +
            " intro=" + introEntries + " login=" + loginEntries + " completions=" + completions +
            " highestFrame=" + highestFrame + " stoppedAtCallback=" + videoStoppedAtCallback + "," + audioStoppedAtCallback +
            " stoppedAtDestination=" + stoppedAtDestination +
            " blockedLogins=" + RevivalGameplayIsolation.BlockedLogins + " memoryReads=" + RevivalGameplayIsolation.Reads);
    }
}
#endif
