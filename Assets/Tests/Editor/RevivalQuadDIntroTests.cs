using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;

public class RevivalQuadDIntroTests
{
    private static object InvokeOutput(Component output, string method, params object[] arguments) =>
        output.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(output, arguments);

    private static Type UiType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType("UnityEngine.UI." + name)).First(type => type != null);

    private static void WithVideoOutput(Action<GameObject, Component, VideoPlayer, Behaviour> verify)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var owner = new GameObject("First video frame fixture");
            owner.SetActive(false);
            SceneManager.MoveGameObjectToScene(owner, preview);
            var output = owner.AddComponent(Type.GetType("QuadDIntroCanvasOutput, Assembly-CSharp", true));
            InvokeOutput(output, "Awake");
            var picture = owner.transform.Find("Quad D Video Output/Video");
            var image = (Behaviour)picture.GetComponent(UiType("RawImage"));
            verify(owner, output, owner.GetComponent<VideoPlayer>(), image);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    [Test]
    public void PreparingVideoHasNoWhiteImageAndUsesFullScreenIntroBackground()
    {
        WithVideoOutput((owner, output, video, image) => {
            Assert.That(image.enabled, Is.False, "A texture-less RawImage must never be visible.");
            Assert.That(image.GetType().GetProperty("texture").GetValue(image), Is.Null);
            var background = owner.transform.Find("Quad D Video Output/Preparation Background");
            var backdrop = background.GetComponent(UiType("Image"));
            Assert.That((Color)backdrop.GetType().GetProperty("color").GetValue(backdrop),
                Is.EqualTo((Color)new Color32(16, 21, 28, 255)));
            Assert.That((bool)backdrop.GetType().GetProperty("raycastTarget").GetValue(backdrop), Is.False);
            var rect = background.GetComponent<RectTransform>();
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
            InvokeOutput(output, "OnFrameReady", video, -1L);
            InvokeOutput(output, "LateUpdate");
            Assert.That(image.enabled, Is.False);
            Assert.That(video.sendFrameReadyEvents, Is.True, "Invalid frames must not end the first-frame wait.");
        });
    }

    [Test]
    public void DecodedFrameNotificationStillRequiresAnActualVideoTexture()
    {
        WithVideoOutput((owner, output, video, image) => {
            var other = new GameObject("Unrelated player").AddComponent<VideoPlayer>();
            SceneManager.MoveGameObjectToScene(other.gameObject, owner.scene);
            InvokeOutput(output, "OnFrameReady", other, 0L);
            Assert.That(video.sendFrameReadyEvents, Is.True);
            InvokeOutput(output, "OnFrameReady", video, 0L);
            Assert.That(video.sendFrameReadyEvents, Is.False, "Continuous per-frame events are unnecessary.");
            InvokeOutput(output, "LateUpdate");
            Assert.That(image.enabled, Is.False);
            Assert.That((Color)image.GetType().GetProperty("color").GetValue(image), Is.EqualTo(Color.white),
                "The decoded picture must retain its original colours.");
        });
    }

    [Test]
    public void CompletionCannotBeUndoneByALateVideoFrame()
    {
        WithVideoOutput((owner, output, video, image) => {
            InvokeOutput(output, "Complete");
            InvokeOutput(output, "Complete");
            InvokeOutput(output, "OnFrameReady", video, 0L);
            InvokeOutput(output, "LateUpdate");
            Assert.That(image.enabled, Is.False);
            Assert.That(image.GetType().GetProperty("texture").GetValue(image), Is.Null);
            Assert.That(owner.GetComponentInChildren<Canvas>(true).enabled, Is.False);
            Assert.That(video.sendFrameReadyEvents, Is.False);
        });
    }

    [Test]
    public void X86VideoPreviewRequiresItsOwnIdentityAndCannotLoadLogin()
    {
        var type = Type.GetType("RevivalQuadDAndroidProbe, Assembly-CSharp", true);
        var method = type.GetMethod("SafeX86Scope", BindingFlags.Public | BindingFlags.Static);
        string app = (string)type.GetField("X86ApplicationId").GetRawConstantValue();
        string scene = (string)type.GetField("ScenePath").GetRawConstantValue();
        string destination = (string)type.GetField("Destination").GetRawConstantValue();
        Func<string, string, int, bool, string, bool> safe = (a, s, n, login, d) =>
            (bool)method.Invoke(null, new object[] { a, s, n, login, d });
        Assert.That(safe(app, scene, 1, false, destination), Is.True);
        Assert.That(safe("com.AeDeong.MonsterTamer", scene, 1, false, destination), Is.False);
        Assert.That(safe(app, "Assets/Scenes/Login.unity", 1, false, destination), Is.False);
        Assert.That(safe(app, scene, 2, false, destination), Is.False);
        Assert.That(safe(app, scene, 1, true, destination), Is.False);
        Assert.That(safe(app, scene, 1, false, "Login"), Is.False);
    }

    [TestCase("QuadDIntro", "Assets/QuadD/Scenes/QuadDIntro.unity", true)]
    [TestCase("QuadDIntro", "Assets/Unknown/QuadDIntro.unity", false)]
    [TestCase("Main", "Assets/Scenes/Main.unity", false)]
    [TestCase("Test", "Assets/Tests/Scenes/QuadD/Test.unity", true)]
    [TestCase("Login", "Assets/Scenes/Login.unity", true)]
    public void EditorEntryKeepsOnlyTheApprovedIntroAndExistingEntries(string name, string path, bool expected)
    {
        var type = Type.GetType("RuntimeInitialize, Assembly-CSharp", true);
        var method = type.GetMethod("CanKeepEditorScene", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, new object[] { name, path }), Is.EqualTo(expected));
    }

    [Test]
    public void ProductionEntryIsIntroThenLoginWithoutTheOldSplashOrPreview()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Assert.That(scenes.Take(2), Is.EqualTo(new[] { "Assets/QuadD/Scenes/QuadDIntro.unity", "Assets/Scenes/Login.unity" }));
        Assert.That(scenes.Distinct().Count(), Is.EqualTo(scenes.Length));
        Assert.That(scenes.Any(s => s == "Assets/Tests/Scenes/QuadD/Test.unity"), Is.False);
        Assert.That(PlayerSettings.SplashScreen.show, Is.False);
        Assert.That(PlayerSettings.SplashScreen.logos, Is.Empty);
        Assert.That(PlayerSettings.SplashScreen.backgroundColor, Is.EqualTo((Color)new Color32(16, 21, 28, 255)));
        var video = AssetDatabase.LoadAssetAtPath<VideoClip>("Packages/com.quadd.intro/Runtime/Resources/QuadD/QuadDIntro.mp4");
        Assert.That(video, Is.Not.Null);
        Assert.That(video.width, Is.EqualTo(1920));
        Assert.That(video.height, Is.EqualTo(1080));
        Assert.That(video.length, Is.InRange(3.3, 3.5));
    }

    [Test]
    public void PreviewUsesAnExplicitUnavailableDestinationInsteadOfABuildIndexFallback()
    {
        const string path = "Assets/Tests/Scenes/QuadD/Test.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool alreadyLoaded = scene.isLoaded;
        try
        {
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var type = Type.GetType("QuadD.QuadDIntro, QuadD.Intro", true);
            var intro = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(type, true)).Single();
            string destination = (string)type.GetProperty("NextSceneName").GetValue(intro);
            Assert.That(destination, Is.EqualTo("RevivalQuadDIntroPreviewNoDestination"));
            Assert.That(Application.CanStreamedLevelBeLoaded(destination), Is.False);
        }
        finally { if (!alreadyLoaded && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ProductionAndPreviewHaveTheUrpCanvasOutputAdapter()
    {
        var type = Type.GetType("QuadDIntroCanvasOutput, Assembly-CSharp", true);
        foreach (string path in new[] { "Assets/QuadD/Scenes/QuadDIntro.unity", "Assets/Tests/Scenes/QuadD/Test.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool alreadyLoaded = scene.isLoaded;
            try
            {
                if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(type, true)).Count(), Is.EqualTo(1), path);
            }
            finally { if (!alreadyLoaded && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
        }
    }

    [Test]
    public void AndroidPreviewScopeRejectsOperatingIdentityLoginAndSceneFallbacks()
    {
        var type = Type.GetType("RevivalQuadDAndroidProbe, Assembly-CSharp", true);
        var method = type.GetMethod("SafeScope", BindingFlags.Public | BindingFlags.Static);
        string app = (string)type.GetField("ApplicationId").GetRawConstantValue();
        string scene = (string)type.GetField("ScenePath").GetRawConstantValue();
        string next = (string)type.GetField("Destination").GetRawConstantValue();
        Func<string, string, int, bool, string, bool> safe = (a, s, n, l, d) => (bool)method.Invoke(null, new object[] { a, s, n, l, d });
        Assert.That(safe(app, scene, 1, false, next), Is.True);
        Assert.That(safe("com.AeDeong.MonsterTamer", scene, 1, false, next), Is.False);
        Assert.That(safe(app, "Assets/Scenes/Login.unity", 1, false, next), Is.False);
        Assert.That(safe(app, scene, 2, false, next), Is.False);
        Assert.That(safe(app, scene, 1, true, next), Is.False);
        Assert.That(safe(app, scene, 1, false, "Login"), Is.False);
    }

    [Test]
    public void RepeatedSkipCompletesOnceAndDoesNotLoadAnUnknownScene()
    {
        var type = Type.GetType("QuadD.QuadDIntro, QuadD.Intro", true);
        var owner = new GameObject("Revival Quad D completion test");
        try
        {
            var intro = owner.AddComponent(type);
            const string missingScene = "RevivalQuadDIntroNonexistentDestination";
            type.GetProperty("NextSceneName").SetValue(intro, missingScene);
            int count = 0;
            ((UnityEvent)type.GetProperty("OnCompleted").GetValue(intro)).AddListener(() => count++);
            string before = SceneManager.GetActiveScene().path;
            LogAssert.Expect(LogType.Warning, "Quad D next scene is missing from Build Settings: " + missingScene);
            type.GetMethod("Skip").Invoke(intro, null);
            type.GetMethod("Skip").Invoke(intro, null);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(before));
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
}
