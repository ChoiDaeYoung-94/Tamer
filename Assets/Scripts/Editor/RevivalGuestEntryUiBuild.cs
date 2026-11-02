using System;
using System.IO;
using System.Linq;
using AD;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Only a separate offline smoke APK; never creates an operating account or signs with the operating key.</summary>
public static class RevivalGuestEntryUiBuild
{
    public const string Output = "Build/revival/Tamer-guest-entry-ui-1102.apk";
    private const string Manifest = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string AdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private const string AdsManifest = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";
    public static bool BuildActive { get; private set; }

    public static bool AllowsGlobalDefines(string defines) => !string.IsNullOrEmpty(defines)
        ? !defines.Split(';').Any(s => s.Trim().StartsWith("TAMER_", StringComparison.Ordinal)) : true;

    public static void BuildAndroid()
    {
        var originals = new[] { Manifest, AdsSettings, AdsManifest }.ToDictionary(p => p, File.ReadAllBytes);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string oldName = PlayerSettings.productName;
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        int code = 1;
        try
        {
            if (Application.dataPath.Replace('\\', '/') != "D:/codex/worktrees/tamer-working/Tamer/Assets")
                throw new BuildFailedException("Reviewed checkout required.");
            RevivalBuild.ValidateBaseline(); RevivalBuild.RequireSavedScenes();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || BuildActive ||
                !AllowsGlobalDefines(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)))
                throw new BuildFailedException("Dedicated Android build and no global Tamer symbols required.");
            if (File.Exists(Output) || Directory.Exists(Output)) throw new BuildFailedException("Preserve existing guest UI artifact.");
            var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
            if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
                throw new BuildFailedException("IAP/UGS auto initialization must remain disabled.");
            if (!File.Exists(RevivalBuild.SmokeScene)) throw new BuildFailedException("Existing empty smoke scene required.");
            RevivalBuild.VerifySmokeScene();
            File.WriteAllText(Manifest, RevivalGameplayBuild.PlayerRestoreManifest(File.ReadAllText(Manifest)));
            AssetDatabase.ImportAsset(Manifest, ImportAssetOptions.ForceUpdate);
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AdsSettings));
            settings.FindProperty("adMobAndroidAppId").stringValue = "ca-app-pub-3940256099942544~3347511713";
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, RevivalGuestEntryUiHarness.ApplicationId);
            PlayerSettings.productName = "Tamer Guest UI Trial";
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            BuildActive = true;
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            BuildReport report;
            using (RevivalBuild.AndroidRelroLinkScope())
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { RevivalBuild.SmokeScene }, target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android, locationPathName = Output,
                    options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                    extraScriptingDefines = new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GUEST_ENTRY_UI_HARNESS" }
                });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Isolated guest UI build failed.");
            Debug.Log("GUEST_ENTRY_UI_BUILD_OK operating_key_used=false operating_scenes=0");
            code = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            BuildActive = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.productName = oldName;
            PlayerSettings.Android.useCustomKeystore = oldKey;
            PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            AssetDatabase.SaveAssets();
            foreach (var item in originals) {
                File.WriteAllBytes(item.Key, item.Value);
                AssetDatabase.ImportAsset(item.Key, ImportAssetOptions.ForceUpdate);
            }
            if (originals.Any(item => !File.ReadAllBytes(item.Key).SequenceEqual(item.Value)))
                throw new BuildFailedException("Original guest UI build settings were not restored.");
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Isolated guest UI build failed.");
    }

    [Serializable] private class CatalogFlags { public bool enableCodelessAutoInitialization; public bool enableUnityGamingServicesAutoInitialization; }

    public sealed class BackupRules : UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != RevivalGuestEntryUiHarness.ApplicationId) return;
            if (!BuildActive) throw new BuildFailedException("Guest fixture backup rules require owned build scope.");
            string target = Path.Combine(path, "src/main/res/xml/tamer_playerrestore_rules.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, RevivalGameplayBuild.PlayerRestoreExtractionRules());
        }
    }
}

public sealed class RevivalGuestEntryUiBuildScene : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || !BuildPipeline.isBuildingPlayer ||
            PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != RevivalGuestEntryUiHarness.ApplicationId) return;
        if (!RevivalGuestEntryUiBuild.BuildActive || scene.path != RevivalBuild.SmokeScene ||
            (report.summary.options & BuildOptions.Development) == 0 || PlayerSettings.Android.useCustomKeystore)
            throw new BuildFailedException("Guest UI requires its dedicated debug-signed smoke build.");
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new BuildFailedException("The original smoke scene must have no game behaviours.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DungGeunMo SDF.asset");
        if (font == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font)) != "6b62d0bbc19501141b40b8d32a134954")
            throw new BuildFailedException("Existing Korean font required.");
        var created = new GameObject("Local guest UI fixture");
        SceneManager.MoveGameObjectToScene(created, scene);
        created.AddComponent<RevivalGuestEntryUiHarness>().Font = font;
    }
}
