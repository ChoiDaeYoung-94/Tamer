using System;
using System.IO;
using System.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Reuses the existing memory-only manual UI fixture in a new application sandbox.
public static class RevivalFaceliftBuild
{
    private static string requestDirectory;
    private static bool characterPreview;
    private static string ApplicationId => characterPreview
        ? RevivalGameplayIsolation.FaceliftCharacterUiApplicationId : RevivalGameplayIsolation.FaceliftUiApplicationId;
    public static readonly string[] Scenes = { "Assets/Scenes/Login.unity", "Assets/Scenes/SetCharacter.unity",
        "Assets/Scenes/Main.unity", "Assets/Scenes/Game.unity", "Assets/Scenes/NextScene.unity" };

    public static bool IsSafeConfiguration(string appId, bool development, bool customKey, bool appBundle, string[] defines)
        => appId == RevivalGameplayIsolation.FaceliftUiApplicationId && development && !customKey && !appBundle &&
           defines != null && defines.OrderBy(x => x).SequenceEqual(new[] { "TAMER_REVIVAL_SMOKE",
               "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS", "TAMER_SHOP_UI", "TAMER_FACELIFT_UI_HARNESS" }.OrderBy(x => x));

    public static bool IsSafeCharacterConfiguration(string appId, bool development, bool customKey, bool appBundle, string[] defines)
        => appId == RevivalGameplayIsolation.FaceliftCharacterUiApplicationId && development && !customKey && !appBundle &&
           defines != null && defines.OrderBy(x => x).SequenceEqual(new[] { "TAMER_REVIVAL_SMOKE",
               "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS", "TAMER_SHOP_UI", "TAMER_FACELIFT_UI_HARNESS", "TAMER_CHARACTER_UI" }.OrderBy(x => x));

    public static object RequestBuild() => Request(false);
    public static object RequestCharacterBuild() => Request(true);

    private static object Request(bool character)
    {
        if (requestDirectory != null || BuildPipeline.isBuildingPlayer)
            throw new BuildFailedException("A facelift build has already been requested in this Editor.");
        RevivalBuild.ValidateBaseline();
        RevivalBuild.RequireSavedScenes();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new BuildFailedException("Android Edit Mode required.");
        if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Any(d => d.Trim().StartsWith("TAMER_", StringComparison.Ordinal)))
            throw new BuildFailedException("Harness defines must not be global.");
        if (Scenes.Any(path => !File.Exists(path))) throw new BuildFailedException("Original scene missing.");
        var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
        if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
            throw new BuildFailedException("IAP/UGS automatic initialization must remain disabled.");
        characterPreview = character;
        requestDirectory = Path.GetFullPath(Path.Combine(".revival-local/facelift-1029/android", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(requestDirectory);
        File.WriteAllText(Path.Combine(requestDirectory, "build-status.json"), "{\"result\":\"Requested\"}");
        File.WriteAllText(".revival-local/facelift-1029/android/latest-build.txt", requestDirectory);
        EditorApplication.delayCall += Build;
        return new { requestedOnce = true, directory = requestDirectory, applicationId = ApplicationId, characterPreview };
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
        var result = new Receipt { result = "Started", applicationId = ApplicationId,
            unity = Application.unityVersion, scenes = Scenes };
        try
        {
            using (var manifest = new RevivalAdHarnessBuild.IsolatedManifestScope(RevivalGameplayBuild.PlayerRestoreManifest))
            {
                var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(settingsPath));
                settings.FindProperty("adMobAndroidAppId").stringValue = "ca-app-pub-3940256099942544~3347511713";
                settings.ApplyModifiedPropertiesWithoutUndo();
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
                string[] defines = { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS", "TAMER_SHOP_UI", "TAMER_FACELIFT_UI_HARNESS" };
                if (characterPreview) defines = defines.Concat(new[] { "TAMER_CHARACTER_UI" }).ToArray();
                bool safe = characterPreview
                    ? IsSafeCharacterConfiguration(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), true,
                        PlayerSettings.Android.useCustomKeystore, EditorUserBuildSettings.buildAppBundle, defines)
                    : IsSafeConfiguration(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), true,
                        PlayerSettings.Android.useCustomKeystore, EditorUserBuildSettings.buildAppBundle, defines);
                if (!safe)
                    throw new BuildFailedException("Unsafe facelift preview configuration.");
                File.WriteAllText(Path.Combine(requestDirectory, "build-status.json"), JsonUtility.ToJson(result, true));
                BuildReport report;
                using (RevivalBuild.AndroidRelroLinkScope())
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                        scenes = Scenes, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                        locationPathName = Path.Combine(requestDirectory, "facelift-preview.apk"),
                        options = BuildOptions.Development | BuildOptions.CompressWithLz4, extraScriptingDefines = defines
                    });
                result.result = report.summary.result.ToString(); result.errors = report.summary.totalErrors;
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Facelift preview build failed.");
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
    [Serializable] private class CatalogFlags { public bool enableCodelessAutoInitialization; public bool enableUnityGamingServicesAutoInitialization; }
    [Serializable] private class Receipt { public string result, applicationId, unity; public string[] scenes; public int errors; public bool originalFilesRestored; }
}
