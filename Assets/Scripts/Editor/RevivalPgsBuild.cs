using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AD;
using GooglePlayGames;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RevivalPgsBuild
{
    private const string ConfigPath = "Assets/Resources/RevivalPgsLocal.json";
    private const string SettingsPath = "Assets/Resources/PlayGamesSettings.asset";
    private const string Manifest = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string PgsManifest = "Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml";
    private static readonly string[] Defines = { "TAMER_REVIVAL_SMOKE", "TAMER_PGS_HARNESS" };

    public static string IsolatedManifest(string original)
    {
        var xml = XDocument.Parse(RevivalIapBuild.IsolatedManifest(original));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        foreach (var item in xml.Root.Elements("uses-permission")
            .Where(e => (string)e.Attribute(android + "name") == "com.android.vending.BILLING").ToArray()) item.Remove();
        xml.Root.Add(new XElement("uses-permission", new XAttribute(android + "name", "com.android.vending.BILLING"), new XAttribute(tools + "node", "remove")));
        xml.Root.Element("application").SetAttributeValue(android + "allowBackup", "false");
        return xml.ToString();
    }

    // Compilation does not load local configuration or authenticate with either service.
    public static void CompileScripts()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new BuildFailedException("Android target required.");
        ValidateDefines();
        string output = Path.GetFullPath("Logs/revival/pgs-player-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
        {
            target = BuildTarget.Android, group = BuildTargetGroup.Android,
            options = ScriptCompilationOptions.DevelopmentBuild, extraScriptingDefines = Defines
        }, output);
        if (result.assemblies == null || !File.Exists(Path.Combine(output, "Assembly-CSharp.dll")))
            throw new BuildFailedException("PGS player compilation failed.");
        File.WriteAllText(Path.Combine(output, "conditions.txt"), "Android player; UNITY_EDITOR absent; DevelopmentBuild; " + string.Join(";", Defines));
        Debug.Log("PGS_TEST_COMPILE_OK");
    }

    private static void ValidateDefines()
    {
        if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';')
            .Any(d => d.StartsWith("TAMER_", StringComparison.Ordinal)))
            throw new BuildFailedException("Test symbols must be build-local.");
    }

    public static void BuildAndroid()
    {
        // Non-secret inputs only. No fallback to production or implicit project selection.
        var config = new RevivalPgsTestConfiguration
        {
            testTitle = Environment.GetEnvironmentVariable("TAMER_PGS_TEST_TITLE"),
            webClientId = Environment.GetEnvironmentVariable("TAMER_PGS_WEB_CLIENT_ID"),
            gameId = Environment.GetEnvironmentVariable("TAMER_PGS_GAME_ID")
        };
        config.Validate(RevivalPgsTestConfiguration.ApplicationId);
        RevivalBuild.ValidateBaseline();
        RevivalBuild.RequireSavedScenes();
        ValidateDefines();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new BuildFailedException("Android target required.");
        if (File.Exists(ConfigPath) || File.Exists(ConfigPath + ".meta"))
            throw new BuildFailedException("Existing temporary PGS configuration found; preserve and review it.");
        var settingsPaths = AssetDatabase.FindAssets("t:PlayGamesSettings").Select(AssetDatabase.GUIDToAssetPath).ToArray();
        if (settingsPaths.Length > 1 || (settingsPaths.Length == 0 && (File.Exists(SettingsPath) || File.Exists(SettingsPath + ".meta"))))
            throw new BuildFailedException("Ambiguous local PGS settings; preserve and review them.");
        string settingsPath = settingsPaths.Length == 1 ? settingsPaths[0] : SettingsPath;
        byte[] originalSettings = settingsPaths.Length == 1 ? File.ReadAllBytes(settingsPath) : null;
        byte[] originalSettingsMeta = originalSettings != null ? File.ReadAllBytes(settingsPath + ".meta") : null;
        if (originalSettings != null)
        {
            string backup = Path.Combine("Logs/revival/pgs-settings-snapshots", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            File.WriteAllBytes(Path.Combine(backup, "PlayGamesSettings.asset"), originalSettings);
            File.WriteAllBytes(Path.Combine(backup, "PlayGamesSettings.asset.meta"), originalSettingsMeta);
        }
        // Verify the existing isolated scene before allowing any build-time configuration changes.
        RevivalBuild.VerifySmokeScene();
        var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
        if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
            throw new BuildFailedException("IAP/UGS automatic initialization must be disabled.");
        var originals = new[] { Manifest, PgsManifest }.ToDictionary(p => p, File.ReadAllBytes);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            File.WriteAllText(ConfigPath, JsonUtility.ToJson(config));
            AssetDatabase.ImportAsset(ConfigPath);
            var settings = originalSettings == null ? ScriptableObject.CreateInstance<PlayGamesSettings>()
                : AssetDatabase.LoadAssetAtPath<PlayGamesSettings>(settingsPath);
            settings.AppId = config.gameId; settings.WebClientId = config.webClientId;
            if (originalSettings == null) AssetDatabase.CreateAsset(settings, settingsPath);
            else EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Manifest, IsolatedManifest(File.ReadAllText(Manifest)));
            var pgs = XDocument.Parse(File.ReadAllText(PgsManifest));
            XNamespace android = "http://schemas.android.com/apk/res/android";
            pgs.Descendants("meta-data").Single(e => (string)e.Attribute(android + "name") == "com.google.android.gms.games.APP_ID")
                .SetAttributeValue(android + "value", "\\u003" + config.gameId);
            File.WriteAllText(PgsManifest, pgs.ToString());
            AssetDatabase.ImportAsset(Manifest); AssetDatabase.ImportAsset(PgsManifest);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, RevivalPgsTestConfiguration.ApplicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { RevivalBuild.SmokeScene }, target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android, locationPathName = "Build/revival/Tamer-pgs-test.apk",
                options = BuildOptions.Development | BuildOptions.CompressWithLz4, extraScriptingDefines = Defines
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("PGS test build failed.");
            Debug.Log("PGS_TEST_BUILD_OK debugSigning=true manualAuthenticationOnly=true");
        }
        finally
        {
            AssetDatabase.DeleteAsset(ConfigPath);
            if (originalSettings == null) AssetDatabase.DeleteAsset(settingsPath);
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.Android.useCustomKeystore = oldKey;
            PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
            AssetDatabase.SaveAssets();
            if (originalSettings != null)
            {
                File.WriteAllBytes(settingsPath, originalSettings);
                File.WriteAllBytes(settingsPath + ".meta", originalSettingsMeta);
                AssetDatabase.ImportAsset(settingsPath, ImportAssetOptions.ForceUpdate);
                if (!File.ReadAllBytes(settingsPath).SequenceEqual(originalSettings) ||
                    !File.ReadAllBytes(settingsPath + ".meta").SequenceEqual(originalSettingsMeta))
                    throw new BuildFailedException("PGS settings restoration failed; private backup retained.");
            }
            foreach (var entry in originals) { File.WriteAllBytes(entry.Key, entry.Value); AssetDatabase.ImportAsset(entry.Key); }
            if (originals.Any(e => !File.ReadAllBytes(e.Key).SequenceEqual(e.Value))) throw new BuildFailedException("PGS manifest restoration failed.");
        }
    }
    [Serializable] private sealed class CatalogFlags
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
    }
}
