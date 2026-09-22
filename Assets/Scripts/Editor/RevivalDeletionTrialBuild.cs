using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RevivalDeletionTrialBuild
{
    public static string TrialManifest(string original)
    {
        var document = System.Xml.Linq.XDocument.Parse(RevivalGameplayBuild.OfflineManifest(original));
        System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
        System.Xml.Linq.XNamespace tools = "http://schemas.android.com/tools";
        foreach (var element in document.Root.Elements("uses-permission"))
            if ((string)element.Attribute(android + "name") == "android.permission.INTERNET" ||
                (string)element.Attribute(android + "name") == "android.permission.ACCESS_NETWORK_STATE")
                element.Attribute(tools + "node")?.Remove();
        document.Root.Element("application").SetAttributeValue(android + "allowBackup", "false");
        document.Root.Element("application").SetAttributeValue(android + "fullBackupContent", "false");
        document.Root.Element("application").SetAttributeValue(android + "dataExtractionRules", "@xml/tamer_playerrestore_rules");
        document.Root.Element("application").SetAttributeValue(tools + "replace", "android:allowBackup,android:fullBackupContent,android:dataExtractionRules");
        return document.ToString();
    }

    public static void BuildAndroid()
    {
        if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';')
            .Any(d => d.StartsWith("TAMER_", StringComparison.Ordinal)))
            throw new BuildFailedException("Global harness defines are forbidden.");
        var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
        if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
            throw new BuildFailedException("Automatic IAP/UGS initialization must be disabled.");
        const string manifest = "Assets/Plugins/Android/AndroidManifest.xml";
        const string ads = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        const string adsManifest = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";
        byte[] oldManifest = File.ReadAllBytes(manifest), oldAds = File.ReadAllBytes(ads), oldAdsManifest = File.ReadAllBytes(adsManifest);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), oldAlias = PlayerSettings.Android.keyaliasName;
        bool oldKey = PlayerSettings.Android.useCustomKeystore, oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        int code = 1;
        try
        {
            RevivalBuild.ValidateBaseline(); RevivalBuild.PrepareSmokeScene();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new BuildFailedException("Android target required.");
            File.WriteAllText(manifest, TrialManifest(File.ReadAllText(manifest)));
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(ads));
            settings.FindProperty("adMobAndroidAppId").stringValue = RevivalAdHarnessBuild.SampleAppId;
            settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, RevivalDeletionTrialHarness.ApplicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { RevivalBuild.SmokeScene },
                target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, locationPathName = "Build/revival/Tamer-deletion-trial.apk",
                options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                extraScriptingDefines = new[] { "TAMER_REVIVAL_SMOKE", "TAMER_DELETION_HARNESS" } });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Deletion trial harness build failed.");
            Debug.Log("DELETION_TRIAL_BUILD_OK manualAuthentication=true isolated-smoke-only=true"); code = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.Android.useCustomKeystore = oldKey; PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend); EditorUserBuildSettings.buildAppBundle = oldBundle;
            AssetDatabase.SaveAssets();
            File.WriteAllBytes(manifest, oldManifest); File.WriteAllBytes(ads, oldAds); File.WriteAllBytes(adsManifest, oldAdsManifest);
            AssetDatabase.ImportAsset(manifest, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(ads, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(adsManifest, ImportAssetOptions.ForceUpdate);
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Deletion trial harness failed.");
    }
    [Serializable] private sealed class CatalogFlags
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
    }
}
