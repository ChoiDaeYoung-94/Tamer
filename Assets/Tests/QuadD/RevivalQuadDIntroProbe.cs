#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Editor-only observer on the excluded Test scene; never included in a Player.
[DefaultExecutionOrder(-10000)]
public sealed class RevivalQuadDIntroProbe : MonoBehaviour
{
    public int CompletionCount;
    public long HighestFrame = -1;
    private VideoPlayer video;
    private UnityEvent completed;
    private string directory;
    private bool captureRequested;
    private long captureFrame = -1;

    private void Awake()
    {
        video = GetComponent<VideoPlayer>();
        var type = Type.GetType("QuadD.QuadDIntro, QuadD.Intro", true);
        var intro = (Behaviour)GetComponent(type);
        string next = (string)type.GetProperty("NextSceneName").GetValue(intro);
        if (gameObject.scene.path != "Assets/Tests/Scenes/QuadD/Test.unity" ||
            SceneManager.GetActiveScene().name != "Test" ||
            next != "RevivalQuadDIntroPreviewNoDestination" ||
            EditorBuildSettings.scenes.Any(s => s.enabled && s.path == gameObject.scene.path) ||
            Application.CanStreamedLevelBeLoaded(next))
        {
            intro.enabled = false;
            enabled = false;
            throw new InvalidOperationException("Unsafe intro preview was disabled before playback.");
        }
        completed = (UnityEvent)type.GetProperty("OnCompleted").GetValue(intro);
        completed.AddListener(OnCompleted);
        directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs", "revival", "quad-d-intro-play-1029", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteReceipt();
    }

    private void Update()
    {
        HighestFrame = Math.Max(HighestFrame, video.frame);
        if (!captureRequested && HighestFrame >= 40)
        {
            captureRequested = true;
            CaptureFrame();
        }
    }

    private void CaptureFrame()
    {
        captureFrame = video.frame;
        var texture = video.texture;
        if (texture != null)
        {
            var temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            var previous = RenderTexture.active;
            Texture2D frame = null;
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                frame = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                frame.Apply();
                File.WriteAllBytes(Path.Combine(directory, "decoded-frame.png"), frame.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                if (frame != null) Destroy(frame);
            }
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "intro-frame.png"));
        WriteReceipt();
    }

    private void OnCompleted()
    {
        CompletionCount++;
        WriteReceipt();
    }

    private void WriteReceipt()
    {
        if (directory == null) return;
        var result = new Receipt { atUtc = DateTime.UtcNow.ToString("o"),
            completionCount = CompletionCount, highestFrame = HighestFrame, captureFrame = captureFrame,
            captureRequested = captureRequested, activeScene = SceneManager.GetActiveScene().path,
            loadedSceneCount = SceneManager.sceneCount,
            loginLoaded = SceneManager.GetSceneByPath("Assets/Scenes/Login.unity").isLoaded,
            videoPrepared = video != null && video.isPrepared,
            videoPlaying = video != null && video.isPlaying,
            audioPlaying = GetComponent<AudioSource>()?.isPlaying ?? false };
        File.WriteAllText(Path.Combine(directory, "observation.json"), JsonUtility.ToJson(result, true));
        File.WriteAllText(Path.Combine(Directory.GetParent(directory).FullName, "latest-observation.txt"), directory);
    }

    private void OnDestroy() { completed?.RemoveListener(OnCompleted); }

    [Serializable] private sealed class Receipt
    {
        public string atUtc, activeScene;
        public int completionCount, loadedSceneCount;
        public long highestFrame, captureFrame;
        public bool captureRequested, loginLoaded, videoPrepared, videoPlaying, audioPlaying;
    }
}
#endif
