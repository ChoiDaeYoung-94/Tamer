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
