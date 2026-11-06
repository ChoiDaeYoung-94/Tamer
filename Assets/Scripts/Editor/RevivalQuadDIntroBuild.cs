using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using UnityEngine;

// Offline, debug-signed video fixture. Never includes Login or gameplay scenes.
public static class RevivalQuadDIntroBuild
{
    private static string requestDirectory;
    private static bool x86Preview;
    public static string PreviewManifest(string original)
    {
        var document = System.Xml.Linq.XDocument.Parse(RevivalGameplayBuild.PlayerRestoreManifest(original));
        System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
        System.Xml.Linq.XNamespace tools = "http://schemas.android.com/tools";
        var app = document.Root.Element("application");
        // A literal fixture label also wins over localized app_name resources.
        app.SetAttributeValue(android + "label", "Tamer Intro Check");
        app.SetAttributeValue(tools + "replace", (string)app.Attribute(tools + "replace") + ",android:label");
        return document.ToString();
    }
    public static string X86Template(string original)
    {
        const string pattern = @"(?m)^[ \t]*exclude \('/lib/x86_64/\*' \+ '\*'\)[ \t]*\r?\n";
        var matches = System.Text.RegularExpressions.Regex.Matches(original, pattern);
        if (matches.Count != 1) throw new BuildFailedException("Expected one reviewed x86_64 packaging exclusion.");
        return original.Remove(matches[0].Index, matches[0].Length);
    }
    public static object RequestBuild() => RequestBuildForAbi(false);
    public static object RequestX86Build() => RequestBuildForAbi(true);
    private static object RequestBuildForAbi(bool x86)
    {
        if (requestDirectory != null || BuildPipeline.isBuildingPlayer)
            throw new BuildFailedException("An intro build has already been requested in this Editor.");
        RevivalBuild.ValidateBaseline();
        RevivalBuild.RequireSavedScenes();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new BuildFailedException("Android Edit Mode required.");
        if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Any(d => d.StartsWith("TAMER_", StringComparison.Ordinal)))
            throw new BuildFailedException("Harness defines must not be global.");
        if (!File.Exists(RevivalQuadDAndroidProbe.ScenePath)) throw new BuildFailedException("Prepare the isolated scene first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != RevivalQuadDAndroidProbe.ScenePath || scene.rootCount != 1)
            throw new BuildFailedException("Open the saved isolated Android scene first.");
        var root = scene.GetRootGameObjects().Single();
        if (root.GetComponent<QuadD.QuadDIntro>()?.NextSceneName != RevivalQuadDAndroidProbe.Destination ||
            root.GetComponent<QuadDIntroCanvasOutput>() == null || root.GetComponent<RevivalQuadDAndroidProbe>() == null)
            throw new BuildFailedException("Unexpected Android preview components/destination.");
        var allowed = new[] { typeof(QuadD.QuadDIntro), typeof(QuadDIntroCanvasOutput), typeof(RevivalQuadDAndroidProbe) };
        if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null || !allowed.Contains(c.GetType())))
            throw new BuildFailedException("Unexpected behaviour in the isolated scene.");
        string catalog = File.ReadAllText("Assets/Resources/IAPProductCatalog.json");
        if (!System.Text.RegularExpressions.Regex.IsMatch(catalog, "\"enableCodelessAutoInitialization\"\\s*:\\s*false") ||
            !System.Text.RegularExpressions.Regex.IsMatch(catalog, "\"enableUnityGamingServicesAutoInitialization\"\\s*:\\s*false"))
            throw new BuildFailedException("IAP/UGS automatic initialization must remain disabled.");
        x86Preview = x86;
        requestDirectory = Path.GetFullPath(Path.Combine(".revival-local/quad-d-intro/android", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(requestDirectory);
        File.WriteAllText(Path.Combine(requestDirectory, "build-status.json"), "{\"result\":\"Requested\"}");
        File.WriteAllText(".revival-local/quad-d-intro/android/latest-build.txt", requestDirectory);
        EditorApplication.delayCall += Build;
        return new { requestedOnce = true, directory = requestDirectory,
            applicationId = x86Preview ? RevivalQuadDAndroidProbe.X86ApplicationId : RevivalQuadDAndroidProbe.ApplicationId,
            abi = x86Preview ? "x86_64" : "arm64-v8a" };
    }

    private static void Build()
    {
        const string settingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        const string adsManifestPath = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";
        const string templatePath = "Assets/Plugins/Android/mainTemplate.gradle";
        var files = new[] { settingsPath, settingsPath + ".meta", adsManifestPath, adsManifestPath + ".meta",
            templatePath, templatePath + ".meta" };
        var originals = files.ToDictionary(p => p, File.ReadAllBytes);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string oldName = PlayerSettings.productName;
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        var oldArchitecture = PlayerSettings.Android.targetArchitectures;
        string applicationId = x86Preview ? RevivalQuadDAndroidProbe.X86ApplicationId : RevivalQuadDAndroidProbe.ApplicationId;
        var result = new Receipt { result = "Started", applicationId = applicationId,
            abi = x86Preview ? "x86_64" : "arm64-v8a",
            unity = Application.unityVersion, scene = RevivalQuadDAndroidProbe.ScenePath };
        try
        {
            using (var manifest = new RevivalAdHarnessBuild.IsolatedManifestScope(PreviewManifest))
            {
                if (x86Preview)
                    File.WriteAllBytes(templatePath, System.Text.Encoding.UTF8.GetBytes(
                        X86Template(System.Text.Encoding.UTF8.GetString(originals[templatePath]))));
                var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(settingsPath));
                settings.FindProperty("adMobAndroidAppId").stringValue = "ca-app-pub-3940256099942544~3347511713";
                settings.ApplyModifiedPropertiesWithoutUndo();
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
                if (x86Preview) PlayerSettings.productName = "Tamer Intro Check";
                if (x86Preview) PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(requestDirectory, "build-status.json"), JsonUtility.ToJson(result, true));
                BuildReport report;
                using (RevivalBuild.AndroidRelroLinkScope())
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                        scenes = new[] { RevivalQuadDAndroidProbe.ScenePath }, target = BuildTarget.Android,
                        targetGroup = BuildTargetGroup.Android, locationPathName = Path.Combine(requestDirectory, "intro-preview.apk"),
                        options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                        extraScriptingDefines = x86Preview
                            ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_QUAD_D_INTRO_HARNESS", "TAMER_QUAD_D_INTRO_X86_HARNESS" }
                            : new[] { "TAMER_REVIVAL_SMOKE", "TAMER_QUAD_D_INTRO_HARNESS" }
                    });
                result.result = report.summary.result.ToString(); result.errors = report.summary.totalErrors;
                result.bytes = report.summary.totalSize.ToString();
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Isolated intro build failed.");
            }
        }
        catch (Exception error) { result.result = "Failed"; Debug.LogException(error); }
        finally
        {
            try {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
                PlayerSettings.productName = oldName;
                PlayerSettings.Android.useCustomKeystore = oldKey;
                PlayerSettings.Android.keyaliasName = oldAlias;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
                PlayerSettings.Android.targetArchitectures = oldArchitecture;
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                AssetDatabase.SaveAssets();
            }
            finally {
                foreach (var pair in originals) File.WriteAllBytes(pair.Key, pair.Value);
                AssetDatabase.ImportAsset(settingsPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(adsManifestPath, ImportAssetOptions.ForceSynchronousImport);
                result.originalFilesRestored = originals.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value));
                if (!result.originalFilesRestored) result.result = "RestorationFailed";
                File.WriteAllText(Path.Combine(requestDirectory, "build-status.json"), JsonUtility.ToJson(result, true));
            }
        }
    }
    [Serializable] private class Receipt {
        public string result, applicationId, unity, scene, bytes, abi;
        public int errors;
        public bool originalFilesRestored;
    }
}
