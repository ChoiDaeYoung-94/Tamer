using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RevivalGameSaveBuild
{
    private const string Manifest = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string AdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private const string AdsManifest = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";

    public static string GameSaveManifest(string original)
    {
        var document = XDocument.Parse(original);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var root = document.Root ?? throw new BuildFailedException("Missing manifest root.");
        root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
        foreach (string permission in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE", "com.android.vending.BILLING", "com.google.android.gms.permission.AD_ID" })
        {
            foreach (var node in root.Elements("uses-permission").Where(n => (string)n.Attribute(android + "name") == permission).ToArray())
                node.Remove();
            root.Add(new XElement("uses-permission", new XAttribute(android + "name", permission),
                new XAttribute(tools + "node", "remove")));
        }
        var app = root.Element("application") ?? throw new BuildFailedException("Missing application.");
        app.Add(new XElement("provider", new XAttribute(android + "name", "com.google.android.gms.ads.MobileAdsInitProvider"),
            new XAttribute(tools + "node", "remove")));
        return document.ToString();
    }

    public static string CloudManifest(string original)
    {
        var document = XDocument.Parse(GameSaveManifest(original));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        foreach (var permission in document.Root.Elements("uses-permission")
            .Where(e => (string)e.Attribute(android + "name") == "android.permission.INTERNET").ToArray()) permission.Remove();
        document.Root.Add(new XElement("uses-permission", new XAttribute(android + "name", "android.permission.INTERNET")));
        return document.ToString();
    }

    public static void BuildAndroid() => Build(false);
    public static void BuildCloudAndroid() => Build(true);
    private static void Build(bool cloud)
    {
        int code = 1;
        var files = new[] { Manifest, AdsSettings, AdsManifest };
        var originals = files.ToDictionary(path => path, File.ReadAllBytes);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        try
        {
            RevivalBuild.ValidateBaseline();
            RevivalBuild.RequireSavedScenes();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Launch with Android build target.");
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            if (defines.Split(';').Any(d => d == "TAMER_TEST_ADS" || d.StartsWith("TAMER_", StringComparison.Ordinal)))
                throw new BuildFailedException("Harness symbols must not be global.");
            var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
            if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
                throw new BuildFailedException("IAP/UGS auto initialization must stay disabled.");
            RevivalBuild.PrepareSmokeScene();
            var scenes = new[] { RevivalBuild.SmokeScene };
            if (scenes.Any(path => !File.Exists(path))) throw new BuildFailedException("Original scene missing.");
            File.WriteAllText(Manifest, cloud ? CloudManifest(File.ReadAllText(Manifest)) : GameSaveManifest(File.ReadAllText(Manifest)));
            AssetDatabase.ImportAsset(Manifest, ImportAssetOptions.ForceUpdate);
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AdsSettings));
            settings.FindProperty("adMobAndroidAppId").stringValue = "ca-app-pub-3940256099942544~3347511713";
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, cloud ? RevivalGameSaveSchema.CloudApplicationId : RevivalGameSaveSchema.ApplicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = cloud ? "Build/revival/Tamer-gamesave-cloud.apk" : "Build/revival/Tamer-gamesave-offline.apk",
                options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                extraScriptingDefines = cloud ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMESAVE_HARNESS", "TAMER_GAMESAVE_CLOUD" }
                    : new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMESAVE_HARNESS" }
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Game-save offline build failed.");
            Debug.Log("GAMESAVE_BUILD_OK dedicated=true noPurchasing=true");
            code = 0;
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
            foreach (var entry in originals)
            {
                File.WriteAllBytes(entry.Key, entry.Value);
                AssetDatabase.ImportAsset(entry.Key, ImportAssetOptions.ForceUpdate);
            }
            if (originals.Any(entry => !File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)))
                throw new BuildFailedException("Game-save offline build settings restoration failed.");
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Game-save offline build failed.");
    }
    [Serializable] private class CatalogFlags
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
    }
}
