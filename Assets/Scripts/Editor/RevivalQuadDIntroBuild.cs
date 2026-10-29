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
    public static object RequestBuild()
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
        requestDirectory = Path.GetFullPath(Path.Combine(".revival-local/quad-d-intro/android", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(requestDirectory);
        File.WriteAllText(Path.Combine(requestDirectory, "build-status.json"), "{\"result\":\"Requested\"}");
        File.WriteAllText(".revival-local/quad-d-intro/android/latest-build.txt", requestDirectory);
        EditorApplication.delayCall += Build;
        return new { requestedOnce = true, directory = requestDirectory, applicationId = RevivalQuadDAndroidProbe.ApplicationId };
    }

    private static void Build()
    {
        const string settingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        const string adsManifestPath = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";
        var files = new[] { settingsPath, settingsPath + ".meta", adsManifestPath, adsManifestPath + ".meta" };
        var originals = files.ToDictionary(p => p, File.ReadAllBytes);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        var result = new Receipt { result = "Started", applicationId = RevivalQuadDAndroidProbe.ApplicationId,
            unity = Application.unityVersion, scene = RevivalQuadDAndroidProbe.ScenePath };
        try
        {
            using (var manifest = new RevivalAdHarnessBuild.IsolatedManifestScope(RevivalGameplayBuild.OfflineManifest))
            {
                var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(settingsPath));
                settings.FindProperty("adMobAndroidAppId").stringValue = "ca-app-pub-3940256099942544~3347511713";
                settings.ApplyModifiedPropertiesWithoutUndo();
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, RevivalQuadDAndroidProbe.ApplicationId);
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
                        extraScriptingDefines = new[] { "TAMER_REVIVAL_SMOKE", "TAMER_QUAD_D_INTRO_HARNESS" }
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
                PlayerSettings.Android.useCustomKeystore = oldKey;
                PlayerSettings.Android.keyaliasName = oldAlias;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
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
        public string result, applicationId, unity, scene, bytes;
        public int errors;
        public bool originalFilesRestored;
    }
}
