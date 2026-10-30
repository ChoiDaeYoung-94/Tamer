// Run with the pinned Unity Pipeline run_script command; kept outside Assets.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

public static class QuadDIntroEditor
{
    public const string PackageUrl = "https://github.com/ChoiDaeYoung-94/ChoiDaeYoung-94.git?path=/unity/com.quadd.intro#55bf9e4aa48c2f95942f3c2c995f1d55414e97c0";
    public const string IntroPath = "Assets/QuadD/Scenes/QuadDIntro.unity";
    public const string LoginPath = "Assets/Scenes/Login.unity";
    public const string PreviewPath = "Assets/Tests/Scenes/QuadD/Test.unity";
    public const string PreviewDestination = "RevivalQuadDIntroPreviewNoDestination";
    private static string Root => Directory.GetParent(Application.dataPath).FullName;
    private static string PrivateRoot => Path.Combine(Root, ".revival-local", "quad-d-intro");

    // Return before UPM imports scripts/domain reloads; InspectPackage verifies completion.
    public static object Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        if (PackageInfo.GetAllRegisteredPackages().Any(p => p.name == "com.quadd.intro"))
            throw new InvalidOperationException("Package already exists; inspect its identity instead of reinstalling.");
        Directory.CreateDirectory(PrivateRoot);
        string receipt = Path.Combine(PrivateRoot, "package-install-requested.json");
        if (File.Exists(receipt)) throw new InvalidOperationException("An install was already requested; inspect its result.");
        File.WriteAllText(receipt, "{\"requestedOnce\":true,\"sourceCommit\":\"55bf9e4aa48c2f95942f3c2c995f1d55414e97c0\"}");
        var request = Client.Add(PackageUrl);
        if (request.IsCompleted && request.Status == StatusCode.Failure)
            throw new InvalidOperationException(request.Error?.message ?? "UPM failed.");
        return new { requested = true, completed = request.IsCompleted, source = PackageUrl,
            next = "Wait for import/compilation, then InspectPackage; do not repeat Install." };
    }

    public static object InspectPackage()
    {
        var package = PackageInfo.GetAllRegisteredPackages().SingleOrDefault(p => p.name == "com.quadd.intro");
        return package == null ? (object)new { installed = false } : new {
            installed = true, package.name, package.version, package.packageId, package.resolvedPath,
            videoExists = File.Exists(Path.Combine(package.resolvedPath, "Runtime/Resources/QuadD/QuadDIntro.mp4"))
        };
    }

    public static object InspectPreviewState()
    {
        if (SceneManager.GetActiveScene().path != PreviewPath) throw new InvalidOperationException("Not the isolated preview.");
        var root = SceneManager.GetActiveScene().GetRootGameObjects().Single();
        var probeType = Type.GetType("RevivalQuadDIntroProbe, Assembly-CSharp", true);
        var probe = root.GetComponent(probeType);
        var video = root.GetComponent<UnityEngine.Video.VideoPlayer>();
        return new { scene = SceneManager.GetActiveScene().path,
            completionCount = probeType.GetField("CompletionCount").GetValue(probe),
            highestFrame = probeType.GetField("HighestFrame").GetValue(probe),
            videoPlaying = video.isPlaying, audioPlaying = root.GetComponent<AudioSource>().isPlaying,
            loginLoaded = SceneManager.GetSceneByPath(LoginPath).isLoaded };
    }

    public static object ShowPreviewGameView()
    {
        if (SceneManager.GetActiveScene().path != PreviewPath) throw new InvalidOperationException("Not the isolated preview.");
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
        var window = EditorWindow.GetWindow(type);
        window.Show();
        window.Focus();
        return new { isolated = true, gameViewOpened = true };
    }

    public static object Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var introType = Type.GetType("QuadD.QuadDIntro, QuadD.Intro", true);
        var originals = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An existing scene has unsaved changes.");
        var gameScenes = EditorBuildSettings.scenes.Where(s => s.path != IntroPath).ToArray();
        var enabled = gameScenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (enabled.Length == 0 || enabled[0] != LoginPath || enabled.Count(p => p == LoginPath) != 1)
            throw new InvalidOperationException("The existing Login entry order changed.");
        string alias = OriginalSigningAlias();
        EnsureFolder("Assets/QuadD/Scenes");
        try
        {
            Scene scene;
            GameObject root;
            if (File.Exists(Path.Combine(Root, IntroPath)))
            {
                scene = EditorSceneManager.OpenScene(IntroPath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                if (roots.Length != 1 || roots[0].name != "Quad D Intro" || roots[0].GetComponent(introType) == null)
                    throw new InvalidOperationException("Existing intro scene is not the owned scene.");
                root = roots[0];
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                root = new GameObject("Quad D Intro", typeof(Camera), typeof(AudioListener));
                root.tag = "MainCamera";
                root.AddComponent(introType);
            }
            var serialized = new SerializedObject(root.GetComponent(introType));
            serialized.FindProperty("nextSceneName").stringValue = "Login";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EnsureCanvasOutput(root);
            if (!EditorSceneManager.SaveScene(scene, IntroPath)) throw new IOException("Intro scene was not saved.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(IntroPath, true) }.Concat(gameScenes).ToArray();
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
            PlayerSettings.Android.keyaliasName = alias;
            AssetDatabase.SaveAssets();
            return new { intro = IntroPath, next = LoginPath, oldSplashDisabled = !PlayerSettings.SplashScreen.show,
                signingAliasPreserved = PlayerSettings.Android.keyaliasName == alias,
                enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray() };
        }
        finally
        {
            if (originals.Length == 0 || originals.Any(s => string.IsNullOrEmpty(s.path)))
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(originals);
        }
    }

    public static object CreateIsolatedPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An existing scene has unsaved changes.");
        if (EditorBuildSettings.scenes.Any(s => s.path == PreviewPath))
            throw new InvalidOperationException("The isolated preview must not appear in production Build Settings.");
        EnsureFolder("Assets/Tests/Scenes/QuadD");
        var scene = EditorSceneManager.OpenScene(IntroPath, OpenSceneMode.Single);
        if (!File.Exists(Path.Combine(Root, PreviewPath)) && !EditorSceneManager.SaveScene(scene, PreviewPath, true))
            throw new IOException("Preview scene was not created.");
        scene = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
        var introType = Type.GetType("QuadD.QuadDIntro, QuadD.Intro", true);
        var intro = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(introType, true)).Single();
        var serialized = new SerializedObject(intro);
        serialized.FindProperty("nextSceneName").stringValue = PreviewDestination;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EnsureCanvasOutput(intro.gameObject);
        var probeType = Type.GetType("RevivalQuadDIntroProbe, Assembly-CSharp", true);
        if (intro.gameObject.GetComponent(probeType) == null) intro.gameObject.AddComponent(probeType);
        if (!EditorSceneManager.SaveScene(scene, PreviewPath)) throw new IOException("Preview scene was not saved.");
        return new { preview = scene.path, scene.name, nextSceneName = PreviewDestination, productionSceneExcluded = true,
            reason = "Existing Editor guard permits Test; no game/Login scene is loaded on completion." };
    }

    private static void EnsureFolder(string path)
    {
        string parent = "Assets";
        foreach (var name in path.Split('/').Skip(1))
        {
            string next = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, name);
            parent = next;
        }
    }

    public static object ConfigureCanvasOutput()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        foreach (var path in new[] { IntroPath, PreviewPath })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EnsureCanvasOutput(scene.GetRootGameObjects().Single());
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Canvas output was not saved.");
        }
        return new { productionAndPreviewCanvasOutput = true };
    }

    private static void EnsureCanvasOutput(GameObject root)
    {
        var type = Type.GetType("QuadDIntroCanvasOutput, Assembly-CSharp", true);
        if (root.GetComponent(type) == null) root.AddComponent(type);
    }

    public static object CreateAndroidPreview()
    {
        const string path = "Assets/Tests/Scenes/QuadDAndroid/Test.unity";
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorBuildSettings.scenes.Any(s => s.path == path))
            throw new InvalidOperationException("Android preview must remain excluded from production settings.");
        EnsureFolder("Assets/Tests/Scenes/QuadDAndroid");
        var scene = EditorSceneManager.OpenScene(IntroPath, OpenSceneMode.Single);
        if (!File.Exists(Path.Combine(Root, path)) && !EditorSceneManager.SaveScene(scene, path, true))
            throw new IOException("Android preview copy failed.");
        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().Single();
        var introType = Type.GetType("QuadD.QuadDIntro, QuadD.Intro", true);
        var serialized = new SerializedObject(root.GetComponent(introType));
        serialized.FindProperty("nextSceneName").stringValue = "RevivalQuadDAndroidPreviewNoDestination";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EnsureCanvasOutput(root);
        var probeType = Type.GetType("RevivalQuadDAndroidProbe, Assembly-CSharp", true);
        if (root.GetComponent(probeType) == null) root.AddComponent(probeType);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Android preview was not saved.");
        return new { isolatedAndroidScene = scene.path, noLoginDestination = true, productionSettingsExcluded = true };
    }

    public static object InspectExitState()
    {
        return new { playing = EditorApplication.isPlayingOrWillChangePlaymode,
            scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i)).Select(s => new {
                s.path, s.isDirty, roots = s.GetRootGameObjects().Select(r => new { r.name,
                    types = r.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().FullName).ToArray() }).ToArray() }).ToArray() };
    }

    public static object RequestNormalExit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop playback first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene has unsaved changes.");
        EditorApplication.delayCall += () => EditorApplication.Exit(0);
        return new { normalExitRequested = true };
    }

    [Serializable] private class Snapshot { public string ProjectPath = null; public string Backup = null; public string Sha256 = null; }
    private static string OriginalSigningAlias()
    {
        var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(Path.Combine(PrivateRoot, "settings-snapshot.json")));
        if (!string.Equals(Path.GetFullPath(snapshot.ProjectPath), Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Snapshot belongs to another project.");
        byte[] bytes = File.ReadAllBytes(snapshot.Backup);
        using (var hash = SHA256.Create())
            if (!BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").Equals(snapshot.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Protected settings snapshot changed.");
        var match = Regex.Match(System.Text.Encoding.UTF8.GetString(bytes), @"^  AndroidKeyaliasName: ([^\r\n]*)", RegexOptions.Multiline);
        if (!match.Success) throw new InvalidOperationException("Original signing alias field is missing.");
        return match.Groups[1].Value.Trim().Trim('"');
    }
}
