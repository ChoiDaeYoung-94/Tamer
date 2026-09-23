using System;
using System.IO;
using System.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Sample identity is temporary and restored even after a failed build.</summary>
public static class RevivalAdHarnessBuild
{
    public const string ScenePath = "Assets/Tests/Scenes/RevivalAdHarness.unity";
    public const string SampleAppId = "ca-app-pub-3940256099942544~3347511713";
    private const string SettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private const string ManifestPath = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";

    public static void PrepareScene()
    {
        RevivalBuild.RequireSavedScenes();
        bool existing = File.Exists(ScenePath);
        var scene = existing ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!existing)
        {
            var root = new GameObject("Ad harness receipts and controls");
            var created = root.AddComponent<RevivalAdHarness>();
            created.Ads = new GameObject("Sample ad manager").AddComponent<GoogleAdMobManager>();
            created.Sound = new GameObject("Harness audio").AddComponent<SoundManager>();
            created.Bgm = created.Sound.gameObject.AddComponent<AudioSource>();
            created.Sound.gameObject.AddComponent<AudioListener>();
            created.Bgm.playOnAwake = false;
            created.Bgm.loop = true;
            var sound = new SerializedObject(created.Sound);
            sound.FindProperty("_bgmAudioSource").objectReferenceValue = created.Bgm;
            sound.ApplyModifiedPropertiesWithoutUndo();
        }
        var harness = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<RevivalAdHarness>(true)).SingleOrDefault();
        if (harness == null || harness.Ads == null || harness.Sound == null || harness.Bgm == null ||
            new SerializedObject(harness.Sound).FindProperty("_bgmAudioSource").objectReferenceValue != harness.Bgm)
            throw new BuildFailedException("Harness references must be complete.");
        var allowed = new[] { typeof(RevivalAdHarness), typeof(GoogleAdMobManager), typeof(SoundManager) };
        foreach (var component in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)))
            if (component == null || !allowed.Contains(component.GetType()))
                throw new BuildFailedException("Unexpected harness behaviour.");
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new BuildFailedException("Harness scene save failed.");
        Debug.Log("AD_HARNESS_SCENE_ALLOWLIST_OK");
    }

    public static void BuildSample() => Build(true);
    public static void BuildReleaseControl() => Build(false);
    // Sample identity only. This cannot validate this publisher's configured messages.
    public static void BuildUmpSample() => Build(true, true);

    private static void Build(bool development, bool umpOnly = false)
    {
        int exitCode = 1;
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        byte[] settingsBytes = File.ReadAllBytes(SettingsPath), manifestBytes = File.ReadAllBytes(ManifestPath);
        string variant = umpOnly ? "ump-sample" : development ? "sample" : "control";
        string applicationId = umpOnly ? "com.AeDeong.MonsterTamer.revival.ump" :
            development ? "com.AeDeong.MonsterTamer.revival.ads" : "com.AeDeong.MonsterTamer.revival.adscontrol";
        try
        {
            RevivalBuild.ValidateBaseline();
            var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
            if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
                throw new BuildFailedException("Harness requires IAP/UGS catalog auto initialization disabled.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Launch with -buildTarget Android.");
            if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Contains("TAMER_TEST_ADS"))
                throw new BuildFailedException("Remove global TAMER_TEST_ADS: control must retain ordinary release policy.");
            PrepareScene();
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
            settings.FindProperty("adMobAndroidAppId").stringValue = SampleAppId;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = "Build/revival/Tamer-ads-" + variant + ".apk",
                options = development ? BuildOptions.Development | BuildOptions.CompressWithLz4 : BuildOptions.None,
                extraScriptingDefines = umpOnly
                    ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS", "TAMER_UMP_ONLY_HARNESS" }
                    : development
                        ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS", "TAMER_AD_SAMPLE_CLOSE_HARNESS" }
                        : new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS" }
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Ad harness build failed.");
            Debug.Log("AD_HARNESS_BUILD_OK variant=" + variant);
            exitCode = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.Android.useCustomKeystore = oldKey;
            PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            AssetDatabase.SaveAssets();
            File.WriteAllBytes(SettingsPath, settingsBytes);
            File.WriteAllBytes(ManifestPath, manifestBytes);
            AssetDatabase.ImportAsset(SettingsPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate);
            if (!File.ReadAllBytes(SettingsPath).SequenceEqual(settingsBytes) || !File.ReadAllBytes(ManifestPath).SequenceEqual(manifestBytes))
                throw new BuildFailedException("Sample identity restoration failed.");
            Debug.Log("AD_HARNESS_IDENTITY_RESTORED");
        }
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        else if (exitCode != 0) throw new BuildFailedException("Ad harness failed; inspect Editor log.");
    }

    [Serializable] private class CatalogFlags
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
    }
}
