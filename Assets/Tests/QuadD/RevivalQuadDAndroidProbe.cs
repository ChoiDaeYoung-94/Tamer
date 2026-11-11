using System;
using System.IO;
using QuadD;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[DefaultExecutionOrder(-10000)]
public sealed class RevivalQuadDAndroidProbe : MonoBehaviour
{
    public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.quaddintro";
    public const string X86ApplicationId = "com.AeDeong.MonsterTamer.revival.quaddintro1106x86";
    public const string ReleaseApplicationId = "com.AeDeong.MonsterTamer.revival.quaddintro1111release";
    public const string ScenePath = "Assets/Tests/Scenes/QuadDAndroid/Test.unity";
    public const string Destination = "RevivalQuadDAndroidPreviewNoDestination";

    public static bool SafeScope(string appId, string scene, int sceneCount, bool loginAvailable, string destination)
        => appId == ApplicationId && scene == ScenePath && sceneCount == 1 && !loginAvailable && destination == Destination;

    public static bool SafeX86Scope(string appId, string scene, int sceneCount, bool loginAvailable, string destination)
        => appId == X86ApplicationId && SafeScope(ApplicationId, scene, sceneCount, loginAvailable, destination);

    public static bool SafeReleaseScope(string appId, string scene, int sceneCount, bool loginAvailable,
        string destination, bool editor, bool development, bool android) =>
        appId == ReleaseApplicationId && !editor && !development && android &&
        SafeScope(ApplicationId, scene, sceneCount, loginAvailable, destination);

#if TAMER_QUAD_D_RELEASE_HARNESS && (!TAMER_REVIVAL_SMOKE || !TAMER_QUAD_D_INTRO_HARNESS || TAMER_QUAD_D_INTRO_X86_HARNESS)
#error Release intro preview requires the dedicated smoke/intro contract without x86.
#endif

#if TAMER_QUAD_D_INTRO_HARNESS
    private VideoPlayer video;
    private QuadDIntro intro;
    private string directory;
    private long highest = -1, samples;
    private float peak;
    private readonly float[] audioWindow = new float[512];
    private int completions, pauses, resumes;
    private bool captured;
    private string captureRenderMode;
    private double nextReceipt;
    private double awakeSeconds, preparedSeconds = -1, playbackStartedSeconds = -1, decodedSeconds = -1, visibleSeconds = -1;
    private int visibleBeforeDecode;
    private UnityEngine.UI.RawImage picture;

    private void Awake()
    {
        intro = GetComponent<QuadDIntro>();
        bool safe = SafeScope(Application.identifier, gameObject.scene.path, SceneManager.sceneCountInBuildSettings,
                Application.CanStreamedLevelBeLoaded("Login"), intro.NextSceneName);
#if TAMER_QUAD_D_INTRO_X86_HARNESS
        safe = SafeX86Scope(Application.identifier, gameObject.scene.path, SceneManager.sceneCountInBuildSettings,
                Application.CanStreamedLevelBeLoaded("Login"), intro.NextSceneName);
#endif
#if TAMER_QUAD_D_RELEASE_HARNESS
        safe = SafeReleaseScope(Application.identifier, gameObject.scene.path, SceneManager.sceneCountInBuildSettings,
            Application.CanStreamedLevelBeLoaded("Login"), intro.NextSceneName,
            Application.isEditor, Debug.isDebugBuild, Application.platform == RuntimePlatform.Android);
#endif
        if (!safe)
        {
            intro.enabled = false;
            enabled = false;
            throw new InvalidOperationException("Unsafe Android preview disabled before playback.");
        }
        video = GetComponent<VideoPlayer>();
        awakeSeconds = Time.realtimeSinceStartupAsDouble;
        video.prepareCompleted += Prepared;
        video.started += PlaybackStarted;
        video.frameReady += Decoded;
        directory = Path.Combine(Application.persistentDataPath, "quad-d-observation");
        Directory.CreateDirectory(directory);
        intro.OnCompleted.AddListener(Completed);
        WriteReceipt();
    }
    private void Start() => ScreenCapture.CaptureScreenshot("quad-d-observation/startup-frame.png");
    private void Prepared(VideoPlayer source) => preparedSeconds = Time.realtimeSinceStartupAsDouble;
    private void PlaybackStarted(VideoPlayer source) => playbackStartedSeconds = Time.realtimeSinceStartupAsDouble;
    private void Decoded(VideoPlayer source, long frame)
    {
        if (frame >= 0 && decodedSeconds < 0) decodedSeconds = Time.realtimeSinceStartupAsDouble;
    }
    private void Update()
    {
        if (picture == null) picture = GetComponentInChildren<UnityEngine.UI.RawImage>(true);
        if (picture != null && picture.enabled)
        {
            if (decodedSeconds < 0) visibleBeforeDecode++;
            if (visibleSeconds < 0) visibleSeconds = Time.realtimeSinceStartupAsDouble;
        }
        highest = Math.Max(highest, video.frame);
        if (video.isPlaying) {
            // Observe the listener without inserting a DSP filter on the shared
            // AudioListener/AudioSource GameObject.
            AudioListener.GetOutputData(audioWindow, 0);
            foreach (float sample in audioWindow) peak = Math.Max(peak, Math.Abs(sample));
            samples += audioWindow.Length;
        }
        if (!captured && highest >= 40) {
            captured = true;
            captureRenderMode = video.renderMode.ToString();
            // Android appends this relative name to Application.persistentDataPath.
            ScreenCapture.CaptureScreenshot("quad-d-observation/intro-frame.png");
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
    private void WriteReceipt()
    {
        if (directory == null || video == null) return;
        var value = new Receipt { appId = Application.identifier, scene = SceneManager.GetActiveScene().path,
            highestFrame = highest, completionCount = completions, screenshotRequested = captured,
            captureRenderMode = captureRenderMode, audioSamples = samples, audioPeak = peak,
            videoPlaying = video.isPlaying, audioPlaying = GetComponent<AudioSource>().isPlaying,
            loadedScenes = SceneManager.sceneCount, loginAvailable = Application.CanStreamedLevelBeLoaded("Login"),
            pauses = pauses, resumes = resumes, awakeSeconds = awakeSeconds, preparedSeconds = preparedSeconds,
            playbackStartedSeconds = playbackStartedSeconds, firstDecodedSeconds = decodedSeconds,
            firstVisibleSeconds = visibleSeconds, visibleBeforeDecode = visibleBeforeDecode };
        File.WriteAllText(Path.Combine(directory, "observation.json"), JsonUtility.ToJson(value, true));
    }
    private void OnDestroy()
    {
        if (intro != null) intro.OnCompleted.RemoveListener(Completed);
        if (video != null)
        {
            video.prepareCompleted -= Prepared;
            video.started -= PlaybackStarted;
            video.frameReady -= Decoded;
        }
    }
    [Serializable] private class Receipt {
        public string appId, scene, captureRenderMode;
        public long highestFrame, audioSamples;
        public float audioPeak;
        public int completionCount, loadedScenes, pauses, resumes;
        public bool screenshotRequested, videoPlaying, audioPlaying, loginAvailable;
        public double awakeSeconds, preparedSeconds, playbackStartedSeconds, firstDecodedSeconds, firstVisibleSeconds;
        public int visibleBeforeDecode;
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
