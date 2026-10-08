using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RevivalIapBuild
{
    private const string PlayFabResource = "Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset";
    private const string Manifest = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string AdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private const string AdsManifest = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";

    public static string IsolatedManifest(string original)
    {
        var document = XDocument.Parse(original);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var root = document.Root ?? throw new BuildFailedException("Missing manifest root.");
        root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
        foreach (string permission in new[] { "com.google.android.gms.permission.AD_ID" })
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

    private static void UseTestPlayFabResource(string testTitle)
    {
        // Resources are included even when all production login calls are gated.
        // Preserve the original asset/meta bytes outside this temporary build scope.
        var asset = AssetDatabase.LoadAssetAtPath<PlayFabSharedSettings>(PlayFabResource);
        if (asset == null) throw new BuildFailedException("Existing PlayFab settings resource required.");
        var settings = new SerializedObject(asset);
        void Text(string name, string value)
        {
            var field = settings.FindProperty(name);
            if (field == null) throw new BuildFailedException("PlayFab settings schema changed.");
            field.stringValue = value;
        }
        void Flag(string name, bool value)
        {
            var field = settings.FindProperty(name);
            if (field == null) throw new BuildFailedException("PlayFab settings schema changed.");
            field.boolValue = value;
        }
        Text("TitleId", testTitle);
        Text("DeveloperSecretKey", "");
        Text("ProductionEnvironmentUrl", "");
        Text("LoggerHost", "");
        Flag("DisableDeviceInfo", true);
        Flag("DisableFocusTimeCollection", true);
        Flag("EnableRealTimeLogging", false);
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        // Do not print settings, identifiers, or secrets in build logs.
        if (asset.TitleId != testTitle || !string.IsNullOrEmpty(asset.DeveloperSecretKey) ||
            !string.IsNullOrEmpty(asset.ProductionEnvironmentUrl) || !string.IsNullOrEmpty(asset.LoggerHost) ||
            !asset.DisableDeviceInfo || !asset.DisableFocusTimeCollection || asset.EnableRealTimeLogging)
            throw new BuildFailedException("IAP test resource isolation failed.");
    }

    public static void BuildAndroid() => Build(false);
    public static void BuildStoreTestBundle() => Build(true);

    private static void Build(bool storeBundle)
    {
        bool reviewedRecovery = Environment.GetEnvironmentVariable("TAMER_IAP_REVIEWED_RECOVERY") == "1";
        if (reviewedRecovery && !storeBundle)
            throw new BuildFailedException("Reviewed source recovery is only supported for a test bundle.");
        int code = 1;
        const string configPath = "Assets/Resources/RevivalIapLocal.json";
        if (File.Exists(configPath) || File.Exists(configPath + ".meta"))
            throw new BuildFailedException("Temporary IAP config already exists; preserve and inspect it.");
        var config = new RevivalIapIsolation.Configuration
        {
            testTitle = Environment.GetEnvironmentVariable("TAMER_IAP_TEST_TITLE"),
            productionTitle = Environment.GetEnvironmentVariable("TAMER_IAP_PRODUCTION_TITLE"),
            catalog = Environment.GetEnvironmentVariable("TAMER_IAP_TEST_CATALOG")
        };
        RevivalIapIsolation.Validate(config, RevivalIapIsolation.ApplicationId);
        var files = new[] { Manifest, AdsSettings, AdsManifest, PlayFabResource, PlayFabResource + ".meta" };
        if (files.Any(path => !File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
            throw new BuildFailedException("Existing regular build settings and metadata required.");
        var originals = files.ToDictionary(path => path, File.ReadAllBytes);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        string oldKeystore = PlayerSettings.Android.keystoreName;
        string oldStorePass = PlayerSettings.Android.keystorePass;
        string oldAliasPass = PlayerSettings.Android.keyaliasPass;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        int oldVersionCode = PlayerSettings.Android.bundleVersionCode;
        try
        {
            RevivalBuild.ValidateBaseline();
            string selectedVersionCode = Environment.GetEnvironmentVariable("TAMER_IAP_TEST_VERSION_CODE");
            if (selectedVersionCode != null)
            {
                if (!storeBundle || !int.TryParse(selectedVersionCode,
                    System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                    out int testVersionCode) || testVersionCode <= 26 || testVersionCode > 2100000000)
                    throw new BuildFailedException("Explicit test bundle version code must exceed baseline 26 within Android limits.");
                PlayerSettings.Android.bundleVersionCode = testVersionCode;
            }
            UseTestPlayFabResource(config.testTitle);
            File.WriteAllText(configPath, JsonUtility.ToJson(config));
            AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
            RevivalBuild.RequireSavedScenes();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Launch with Android build target.");
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            if (defines.Split(';').Any(d => d == "TAMER_TEST_ADS" || d == "TAMER_GAMEPLAY_HARNESS" || d == "TAMER_IAP_HARNESS" || d == "TAMER_IAP_STORE_TEST"))
                throw new BuildFailedException("Harness symbols must not be global.");
            var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
            if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
                throw new BuildFailedException("IAP/UGS auto initialization must stay disabled.");
            var scenes = new[] { "Login" }
                .Select(name => "Assets/Scenes/" + name + ".unity").ToArray();
            if (scenes.Any(path => !File.Exists(path))) throw new BuildFailedException("Original scene missing.");
            File.WriteAllText(Manifest, IsolatedManifest(File.ReadAllText(Manifest)));
            AssetDatabase.ImportAsset(Manifest, ImportAssetOptions.ForceUpdate);
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AdsSettings));
            settings.FindProperty("adMobAndroidAppId").stringValue = "ca-app-pub-3940256099942544~3347511713";
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, RevivalIapIsolation.ApplicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            if (storeBundle)
            {
                string selectedKey = Environment.GetEnvironmentVariable("TAMER_IAP_TEST_KEY_PATH");
                if (!string.IsNullOrEmpty(selectedKey) && !Path.IsPathFullyQualified(selectedKey))
                    throw new BuildFailedException("Explicit test signing key path must be absolute.");
                string testKey = Path.GetFullPath(string.IsNullOrEmpty(selectedKey)
                    ? ".revival-local/iap-signing/test-upload.jks" : selectedKey);
                string password = Environment.GetEnvironmentVariable("TAMER_IAP_TEST_KEY_PASSWORD");
                if (!File.Exists(testKey) || string.IsNullOrEmpty(password))
                    throw new BuildFailedException("Dedicated local test signing key/password required.");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = testKey;
                PlayerSettings.Android.keyaliasName = "tamer-iap-test-upload";
                PlayerSettings.Android.keystorePass = password;
                PlayerSettings.Android.keyaliasPass = password;
            }
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = storeBundle;
            Directory.CreateDirectory("Build/revival");
            BuildReport report;
            using (RevivalBuild.AndroidRelroLinkScope())
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = "Build/revival/Tamer-iap-test." + (storeBundle ? "aab" : "apk"),
                options = storeBundle ? BuildOptions.CompressWithLz4 : BuildOptions.Development | BuildOptions.CompressWithLz4,
                extraScriptingDefines = storeBundle
                    ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_IAP_HARNESS", "TAMER_IAP_STORE_TEST" }
                    : new[] { "TAMER_REVIVAL_SMOKE", "TAMER_IAP_HARNESS" }
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("IAP test build failed.");
            Debug.Log("IAP_BUILD_OK isolated=true storeTestBundle=" + storeBundle);
            code = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            try
            {
                PlayerSettings.Android.bundleVersionCode = oldVersionCode;
                if (!reviewedRecovery) AssetDatabase.DeleteAsset(configPath);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
                PlayerSettings.Android.useCustomKeystore = oldKey;
                PlayerSettings.Android.keyaliasName = oldAlias;
                PlayerSettings.Android.keystoreName = oldKeystore;
                PlayerSettings.Android.keystorePass = oldStorePass;
                PlayerSettings.Android.keyaliasPass = oldAliasPass;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                if (!reviewedRecovery) AssetDatabase.SaveAssets();
            }
            finally
            {
                // Settings cleanup failures must not skip resource/metadata restoration.
                // Reviewed mode preserves the actual disk delta until the Editor
                // closes. Memory credentials/settings above are still cleared.
                // The owner restores only separately reviewed handle-bound files.
                if (!reviewedRecovery)
                {
                    foreach (var entry in originals)
                        File.WriteAllBytes(entry.Key, entry.Value);
                    foreach (var path in files.Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)))
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    if (originals.Any(entry => !File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)))
                        throw new BuildFailedException("IAP test build settings restoration failed.");
                }
            }
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("IAP test build failed.");
    }
    [Serializable] private class CatalogFlags
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
    }
}
