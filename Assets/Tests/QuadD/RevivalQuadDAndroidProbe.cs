using System;
using System.IO;
using System.Threading;
using QuadD;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[DefaultExecutionOrder(-10000)]
public sealed class RevivalQuadDAndroidProbe : MonoBehaviour
{
    public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.quaddintro";
    public const string ScenePath = "Assets/Tests/Scenes/QuadDAndroid/Test.unity";
    public const string Destination = "RevivalQuadDAndroidPreviewNoDestination";

    public static bool SafeScope(string appId, string scene, int sceneCount, bool loginAvailable, string destination)
        => appId == ApplicationId && scene == ScenePath && sceneCount == 1 && !loginAvailable && destination == Destination;

#if TAMER_QUAD_D_INTRO_HARNESS
    private VideoPlayer video;
    private QuadDIntro intro;
    private string directory;
    private long highest = -1, samples;
    private volatile float peak;
    private int completions, pauses, resumes;
    private bool captured;
    private string captureRenderMode;
    private double nextReceipt;

    private void Awake()
    {
        intro = GetComponent<QuadDIntro>();
        if (!SafeScope(Application.identifier, gameObject.scene.path, SceneManager.sceneCountInBuildSettings,
                Application.CanStreamedLevelBeLoaded("Login"), intro.NextSceneName))
        {
            intro.enabled = false;
            enabled = false;
            throw new InvalidOperationException("Unsafe Android preview disabled before playback.");
        }
        video = GetComponent<VideoPlayer>();
        directory = Path.Combine(Application.persistentDataPath, "quad-d-observation");
        Directory.CreateDirectory(directory);
        intro.OnCompleted.AddListener(Completed);
        WriteReceipt();
    }
    private void Update()
    {
        highest = Math.Max(highest, video.frame);
        if (!captured && highest >= 40) {
            captured = true;
            captureRenderMode = video.renderMode.ToString();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "intro-frame.png"));
        }
        if (Time.realtimeSinceStartupAsDouble >= nextReceipt) {
            nextReceipt = Time.realtimeSinceStartupAsDouble + .5;
            WriteReceipt();
        }
    }
    private void Completed() { completions++; WriteReceipt(); }
    private void OnApplicationPause(bool paused)
    {
        if (paused) pauses++; else resumes++;
        WriteReceipt();
    }
    private void OnAudioFilterRead(float[] data, int channels)
    {
        float observed = peak;
        for (int i = 0; i < data.Length; i++) observed = Math.Max(observed, Math.Abs(data[i]));
        peak = observed;
        Interlocked.Add(ref samples, data.Length);
    }
    private void WriteReceipt()
    {
        if (directory == null || video == null) return;
        var value = new Receipt { appId = Application.identifier, scene = SceneManager.GetActiveScene().path,
            highestFrame = highest, completionCount = completions, screenshotRequested = captured,
            captureRenderMode = captureRenderMode, audioSamples = Interlocked.Read(ref samples), audioPeak = peak,
            videoPlaying = video.isPlaying, audioPlaying = GetComponent<AudioSource>().isPlaying,
            loadedScenes = SceneManager.sceneCount, loginAvailable = Application.CanStreamedLevelBeLoaded("Login"),
            pauses = pauses, resumes = resumes };
        File.WriteAllText(Path.Combine(directory, "observation.json"), JsonUtility.ToJson(value, true));
    }
    private void OnDestroy() { if (intro != null) intro.OnCompleted.RemoveListener(Completed); }
    [Serializable] private class Receipt {
        public string appId, scene, captureRenderMode;
        public long highestFrame, audioSamples;
        public float audioPeak;
        public int completionCount, loadedScenes, pauses, resumes;
        public bool screenshotRequested, videoPlaying, audioPlaying, loginAvailable;
    }
#else
    private void Awake()
    {
        // A preview scene accidentally opened without its build-only symbol cannot play.
        var intro = GetComponent<QuadDIntro>();
        if (intro != null) intro.enabled = false;
        enabled = false;
    }
#endif
}
