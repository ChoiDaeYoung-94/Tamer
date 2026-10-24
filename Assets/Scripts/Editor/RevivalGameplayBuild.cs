using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RevivalGameplayBuild
{
    private const string Manifest = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string AdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private const string AdsManifest = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";

    public static string OfflineManifest(string original)
    {
        var document = XDocument.Parse(original);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var root = document.Root ?? throw new BuildFailedException("Missing manifest root.");
        root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
        foreach (string permission in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE",
            "com.android.vending.BILLING", "com.google.android.gms.permission.AD_ID",
            "android.permission.ACCESS_ADSERVICES_AD_ID", "android.permission.ACCESS_ADSERVICES_ATTRIBUTION",
            "android.permission.ACCESS_ADSERVICES_TOPICS" })
        {
            foreach (var node in root.Elements("uses-permission").Where(n => (string)n.Attribute(android + "name") == permission).ToArray())
                node.Remove();
            root.Add(new XElement("uses-permission", new XAttribute(android + "name", permission),
                new XAttribute(tools + "node", "remove")));
        }
        var app = root.Element("application") ?? throw new BuildFailedException("Missing application.");
        // Native ContentProviders run before Unity's guarded Login.Start. Offline
        // harnesses must also suppress Play Games' automatic sign-in entry point.
        foreach (string provider in new[] { "com.google.android.gms.ads.MobileAdsInitProvider",
            "com.google.android.gms.games.provider.PlayGamesInitProvider" })
        {
            foreach (var node in app.Elements("provider")
                .Where(n => (string)n.Attribute(android + "name") == provider).ToArray()) node.Remove();
            app.Add(new XElement("provider", new XAttribute(android + "name", provider),
                new XAttribute(tools + "node", "remove")));
        }
        return document.ToString();
    }

    public static void BuildSessionAndroid() => Build(false, session: true);
    public static void BuildDeletionGameplayAndroid() => Build(false, deletionGameplay: true);
    public static void BuildInventoryRestoreAndroid() => Build(false, inventoryRestore: true);
    public static void BuildShopPurchaseAndroid() => Build(false, shopPurchase: true);
    public static void BuildShopInventoryCutAndroid() => Build(false, shopPurchase: true, shopAfterInventory: true);
    public static void BuildShopUiAndroid() => Build(false, shopPurchase: true, shopUi: true);
    public static void BuildCharacterUiAndroid() => Build(false, shopPurchase: true, characterUi: true);
    public static void BuildAndroid() => Build(false);
    public static void BuildPhotoAndroid() => Build(true);
    public static void BuildAgeChoiceAndroid() => Build(false, false, true);
    public static void BuildPlayerRestoreAndroid() => Build(false, true);

    public static string PlayerRestoreManifest(string original)
    {
        var document = XDocument.Parse(OfflineManifest(original));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var app = document.Root.Element("application");
        app.SetAttributeValue(android + "allowBackup", "false");
        app.SetAttributeValue(android + "fullBackupContent", "false");
        app.SetAttributeValue(android + "dataExtractionRules", "@xml/tamer_playerrestore_rules");
        app.SetAttributeValue(tools + "replace", "android:allowBackup,android:fullBackupContent,android:dataExtractionRules");
        return document.ToString();
    }

    public static string PlayerRestoreExtractionRules()
    {
        XElement Excludes() => new XElement("rules", new[] { "root", "file", "database", "sharedpref", "external",
            "device_root", "device_file", "device_database", "device_sharedpref" }
            .Select(domain => new XElement("exclude", new XAttribute("domain", domain), new XAttribute("path", "."))));
        return new XElement("data-extraction-rules", new XElement("cloud-backup", Excludes().Elements()),
            new XElement("device-transfer", Excludes().Elements())).ToString();
    }

    public sealed class PlayerRestoreBackupRules : UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string identity = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (identity != RevivalGameplayIsolation.PlayerRestoreApplicationId &&
                identity != RevivalGameplayIsolation.AgeChoiceApplicationId &&
                identity != RevivalGameplayIsolation.SessionApplicationId &&
                identity != RevivalGameplayIsolation.InventoryRestoreApplicationId &&
                identity != RevivalGameplayIsolation.ShopPurchaseApplicationId &&
                identity != RevivalGameplayIsolation.DeletionGameplayApplicationId &&
                identity != RevivalDeletionTrialHarness.ApplicationId &&
                identity != RevivalDeletionTrialHarness.OnlineApplicationId) return;
            string destination = Path.Combine(path, "src/main/res/xml/tamer_playerrestore_rules.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllText(destination, PlayerRestoreExtractionRules());
        }
    }

    private static void Build(bool photo, bool playerRestore = false, bool ageChoice = false, bool session = false, bool inventoryRestore = false, bool shopPurchase = false, bool shopAfterInventory = false, bool shopUi = false, bool characterUi = false, bool deletionGameplay = false)
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
            if (defines.Split(';').Contains("TAMER_DELETION_GAMEPLAY"))
                throw new BuildFailedException("Deletion gameplay symbol must not be global.");
            if (defines.Split(';').Any(d => d == "TAMER_TEST_ADS" || d == "TAMER_GAMEPLAY_HARNESS" || d == "TAMER_GAMEPLAY_PHOTO" || d == "TAMER_PLAYER_RESTORE" || d == "TAMER_AGE_CHOICE" || d == "TAMER_SESSION_HARNESS" || d == "TAMER_INVENTORY_RESTORE" || d == "TAMER_SHOP_HARNESS" || d == "TAMER_SHOP_AFTER_INVENTORY" || d == "TAMER_SHOP_UI" || d == "TAMER_CHARACTER_UI"))
                throw new BuildFailedException("Harness symbols must not be global.");
            var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
            if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
                throw new BuildFailedException("IAP/UGS auto initialization must stay disabled.");
            var scenes = (characterUi ? new[] { "Login", "SetCharacter", "Main", "Game", "NextScene" } : new[] { "Login", "Main", "Game", "NextScene" })
                .Select(name => "Assets/Scenes/" + name + ".unity").ToArray();
            if (scenes.Any(path => !File.Exists(path))) throw new BuildFailedException("Original scene missing.");
            File.WriteAllText(Manifest, (playerRestore || ageChoice || session || inventoryRestore || shopPurchase || deletionGameplay) ? PlayerRestoreManifest(File.ReadAllText(Manifest)) : OfflineManifest(File.ReadAllText(Manifest)));
            AssetDatabase.ImportAsset(Manifest, ImportAssetOptions.ForceUpdate);
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AdsSettings));
            settings.FindProperty("adMobAndroidAppId").stringValue = "ca-app-pub-3940256099942544~3347511713";
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, deletionGameplay ? RevivalGameplayIsolation.DeletionGameplayApplicationId : shopPurchase ? RevivalGameplayIsolation.ShopPurchaseApplicationId : inventoryRestore ? RevivalGameplayIsolation.InventoryRestoreApplicationId : session ? RevivalGameplayIsolation.SessionApplicationId : ageChoice ? RevivalGameplayIsolation.AgeChoiceApplicationId : playerRestore ? RevivalGameplayIsolation.PlayerRestoreApplicationId : RevivalGameplayIsolation.ApplicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            var options = new BuildPlayerOptions
            {
                scenes = scenes, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = characterUi ? "Build/revival/Tamer-characterui.apk" : shopUi ? "Build/revival/Tamer-shopui.apk" : shopAfterInventory ? "Build/revival/Tamer-shopinventorycut.apk" : shopPurchase ? "Build/revival/Tamer-shoppurchase.apk" : inventoryRestore ? "Build/revival/Tamer-inventoryrestore.apk" : session ? "Build/revival/Tamer-sessionguard.apk" : ageChoice ? "Build/revival/Tamer-agechoice.apk" : playerRestore ? "Build/revival/Tamer-playerrestore.apk" : photo ? "Build/revival/Tamer-gameplay-photo.apk" : "Build/revival/Tamer-gameplay.apk",
                options = BuildOptions.CompressWithLz4 | (photo ? BuildOptions.None : BuildOptions.Development),
                extraScriptingDefines = characterUi ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS", "TAMER_CHARACTER_UI" } : shopUi ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS", "TAMER_SHOP_UI" } : shopAfterInventory ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS", "TAMER_SHOP_AFTER_INVENTORY" } : shopPurchase ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS" } : inventoryRestore ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_INVENTORY_RESTORE" } : session ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_SESSION_HARNESS" } : ageChoice ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_AGE_CHOICE" } : playerRestore ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_PLAYER_RESTORE" } : photo
                    ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_GAMEPLAY_PHOTO" }
                    : new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS" }
            };
            if (deletionGameplay)
            {
                options.locationPathName = "Build/revival/Tamer-deletiongameplay.apk";
                options.extraScriptingDefines = new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_DELETION_GAMEPLAY" };
            }
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Gameplay build failed.");
            Debug.Log("GAMEPLAY_BUILD_OK originalScenes=" + scenes.Length + " offline=true");
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
                throw new BuildFailedException("Gameplay build settings restoration failed.");
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Gameplay build failed.");
    }
    [Serializable] private class CatalogFlags
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
    }
}
