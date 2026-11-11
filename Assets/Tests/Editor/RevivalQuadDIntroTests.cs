using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
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
    [Test]
    public void ReleaseVideoPreviewRequiresItsOwnAndroidIdentityAndSingleOfflineScene()
    {
        var type = Type.GetType("RevivalQuadDAndroidProbe, Assembly-CSharp", true);
        var method = type.GetMethod("SafeReleaseScope", BindingFlags.Public | BindingFlags.Static);
        string app = (string)type.GetField("ReleaseApplicationId").GetRawConstantValue();
        string scene = (string)type.GetField("ScenePath").GetRawConstantValue();
        string destination = (string)type.GetField("Destination").GetRawConstantValue();
        Func<string, string, int, bool, string, bool, bool, bool, bool> safe = (a, s, n, login, d, editor, dev, android) =>
            (bool)method.Invoke(null, new object[] { a, s, n, login, d, editor, dev, android });
        Assert.That(safe(app, scene, 1, false, destination, false, false, true), Is.True);
        foreach (string id in new[] { "com.AeDeong.MonsterTamer", (string)type.GetField("ApplicationId").GetRawConstantValue(),
            (string)type.GetField("X86ApplicationId").GetRawConstantValue(), "" })
            Assert.That(safe(id, scene, 1, false, destination, false, false, true), Is.False);
        Assert.That(safe(app, "Assets/Scenes/Login.unity", 1, false, destination, false, false, true), Is.False);
        Assert.That(safe(app, scene, 2, false, destination, false, false, true), Is.False);
        Assert.That(safe(app, scene, 1, true, destination, false, false, true), Is.False);
        Assert.That(safe(app, scene, 1, false, "Login", false, false, true), Is.False);
        Assert.That(safe(app, scene, 1, false, destination, true, false, true), Is.False);
        Assert.That(safe(app, scene, 1, false, destination, false, true, true), Is.False);
        Assert.That(safe(app, scene, 1, false, destination, false, false, false), Is.False);
    }

    [Test]
    public void ReleaseVideoBuildRejectsOperatingIdentityKeyBundleAndOtherArchitecturesOrSymbols()
    {
        var probe = Type.GetType("RevivalQuadDAndroidProbe, Assembly-CSharp", true);
        var type = Type.GetType("RevivalQuadDIntroBuild, Assembly-CSharp-Editor", true);
        var method = type.GetMethod("IsSafeReleaseConfiguration", BindingFlags.Public | BindingFlags.Static);
        string app = (string)probe.GetField("ReleaseApplicationId").GetRawConstantValue();
        string[] defines = { "TAMER_REVIVAL_SMOKE", "TAMER_QUAD_D_INTRO_HARNESS", "TAMER_QUAD_D_RELEASE_HARNESS" };
        Func<string, bool, bool, bool, bool, AndroidArchitecture, string[], bool> safe = (a, dev, key, bundle, x86, abi, symbols) =>
            (bool)method.Invoke(null, new object[] { a, dev, key, bundle, x86, abi, symbols });
        Assert.That(safe(app, false, false, false, false, AndroidArchitecture.ARM64, defines.Reverse().ToArray()), Is.True);
        Assert.That(safe("com.AeDeong.MonsterTamer", false, false, false, false, AndroidArchitecture.ARM64, defines), Is.False);
        Assert.That(safe(app, true, false, false, false, AndroidArchitecture.ARM64, defines), Is.False);
        Assert.That(safe(app, false, true, false, false, AndroidArchitecture.ARM64, defines), Is.False);
        Assert.That(safe(app, false, false, true, false, AndroidArchitecture.ARM64, defines), Is.False);
        Assert.That(safe(app, false, false, false, true, AndroidArchitecture.ARM64, defines), Is.False);
        foreach (var abi in new[] { AndroidArchitecture.X86_64, AndroidArchitecture.ARMv7,
            AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64 })
            Assert.That(safe(app, false, false, false, false, abi, defines), Is.False);
        foreach (var symbols in new[] { null, Array.Empty<string>(), defines.Take(2).ToArray(),
            defines.Concat(new[] { defines[0] }).ToArray(), defines.Concat(new[] { "TAMER_GAMEPLAY_HARNESS" }).ToArray(),
            defines.Concat(new[] { "TAMER_QUAD_D_INTRO_X86_HARNESS" }).ToArray() })
            Assert.That(safe(app, false, false, false, false, AndroidArchitecture.ARM64, symbols), Is.False);
    }

    [Test]
    public void ReleaseVideoLauncherChangesOnlyTheReviewedActivityLabel()
    {
        var type = Type.GetType("RevivalQuadDIntroBuild, Assembly-CSharp-Editor", true);
        string original = System.IO.File.ReadAllText("Assets/Plugins/Android/AndroidManifest.xml");
        string normal = (string)type.GetMethod("PreviewManifest").Invoke(null, new object[] { original });
        string release = (string)type.GetMethod("ReleasePreviewManifest").Invoke(null, new object[] { original });
        var normalXml = XDocument.Parse(normal);
        var releaseXml = XDocument.Parse(release);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        var activity = releaseXml.Root.Element("application").Elements("activity").Single(x =>
            (string)x.Attribute(android + "name") == "com.unity3d.player.UnityPlayerActivity");
        Assert.That((string)activity.Attribute(android + "label"), Is.EqualTo("Tamer Intro Release"));
        activity.SetAttributeValue(android + "label", "Tamer Intro Check");
        Assert.That(XNode.DeepEquals(normalXml, releaseXml), Is.True);
    }

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

    [Test]
    public void X86PackagingChangesOnlyItsSingleReviewedExclusion()
    {
        var type = Type.GetType("RevivalQuadDIntroBuild, Assembly-CSharp-Editor", true);
        var method = type.GetMethod("X86Template", BindingFlags.Public | BindingFlags.Static);
        const string excluded = "      exclude ('/lib/x86_64/*' + '*')\r\n";
        const string prefix = "packaging {\r\n      exclude ('/lib/armeabi-v7a/*' + '*')\r\n";
        const string suffix = "      exclude ('/lib/x86/*' + '*')\r\n}\r\n";
        Assert.That((string)method.Invoke(null, new object[] { prefix + excluded + suffix }), Is.EqualTo(prefix + suffix));
        Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { prefix + suffix }));
        Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { prefix + excluded + excluded + suffix }));
    }

    [TestCase("com.AeDeong.MonsterTamer.revival.quaddintro")]
    [TestCase("com.AeDeong.MonsterTamer.revival.quaddintro1106x86")]
    [TestCase("com.AeDeong.MonsterTamer.revival.quaddintro1111release")]
    public void OfflinePreviewDisablesBackupAndRetainsItsBoundaries(string identity)
    {
        var builder = Type.GetType("RevivalQuadDIntroBuild, Assembly-CSharp-Editor", true);
        bool release = identity == "com.AeDeong.MonsterTamer.revival.quaddintro1111release";
        var method = builder.GetMethod(release ? "ReleasePreviewManifest" : "PreviewManifest", BindingFlags.Public | BindingFlags.Static);
        const string original = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'>" +
            "<uses-permission android:name='android.permission.INTERNET'/>" +
            "<application android:label='keep' android:allowBackup='true' android:fullBackupContent='true'>" +
            "<activity android:name='com.unity3d.player.UnityPlayerActivity' android:label='@string/app_name'/>" +
            "<activity android:name='UnrelatedActivity' android:label='keep activity'/>" +
            "<provider android:name='com.google.android.gms.ads.MobileAdsInitProvider'/>" +
            "</application></manifest>";
        var result = XDocument.Parse((string)method.Invoke(null, new object[] { original }));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var app = result.Root.Element("application");
        Assert.That((string)app.Attribute(android + "label"), Is.EqualTo("keep"));
        Assert.That(((string)app.Attribute(tools + "replace")).Split(','), Does.Not.Contain("android:label"));
        var entry = app.Elements("activity").Single(n => (string)n.Attribute(android + "name") == "com.unity3d.player.UnityPlayerActivity");
        Assert.That((string)entry.Attribute(android + "label"), Is.EqualTo(release ? "Tamer Intro Release" : "Tamer Intro Check"));
        Assert.That((string)app.Elements("activity").Single(n => (string)n.Attribute(android + "name") == "UnrelatedActivity").Attribute(android + "label"), Is.EqualTo("keep activity"));
        Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] {
            original.Replace("com.unity3d.player.UnityPlayerActivity", "MissingEntry") }));
        Assert.That((string)app.Attribute(android + "allowBackup"), Is.EqualTo("false"));
        Assert.That((string)app.Attribute(android + "fullBackupContent"), Is.EqualTo("false"));
        Assert.That((string)app.Attribute(android + "dataExtractionRules"), Is.EqualTo("@xml/tamer_playerrestore_rules"));
        Assert.That(result.Root.Elements("uses-permission").Count(), Is.EqualTo(7));
        Assert.That(result.Root.Elements("uses-permission").All(n => (string)n.Attribute(tools + "node") == "remove"), Is.True);
        Assert.That(app.Elements("provider").Count(), Is.EqualTo(2));
        Assert.That(app.Elements("provider").All(n => (string)n.Attribute(tools + "node") == "remove"), Is.True);

        var rules = Type.GetType("RevivalGameplayBuild+PlayerRestoreBackupRules, Assembly-CSharp-Editor", true);
        var supports = rules.GetMethod("SupportsIdentity", BindingFlags.Public | BindingFlags.Static);
        Assert.That((bool)supports.Invoke(null, new object[] { identity }), Is.True);
        Assert.That((bool)supports.Invoke(null, new object[] { "com.AeDeong.MonsterTamer" }), Is.False);
        Assert.That((bool)supports.Invoke(null, new object[] { identity + ".unreviewed" }), Is.False);
        var gameplay = Type.GetType("RevivalGameplayBuild, Assembly-CSharp-Editor", true);
        var extraction = XDocument.Parse((string)gameplay.GetMethod("PlayerRestoreExtractionRules").Invoke(null, null));
        Assert.That(extraction.Root.Name.LocalName, Is.EqualTo("data-extraction-rules"));
        string[] domains = { "root", "file", "database", "sharedpref", "external", "device_root", "device_file", "device_database", "device_sharedpref" };
        foreach (string section in new[] { "cloud-backup", "device-transfer" })
        {
            var excludes = extraction.Root.Element(section).Elements("exclude").ToArray();
            Assert.That(excludes.Select(n => (string)n.Attribute("domain")), Is.EquivalentTo(domains));
            Assert.That(excludes.All(n => (string)n.Attribute("path") == "."), Is.True);
        }
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
