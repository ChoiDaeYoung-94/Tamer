using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AD.Advertising;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Same-package debug 27 to 28 upgrade. Never changes ordinary privacy approval.</summary>
public static class RevivalPrivacyProductUmpBuild
{
    internal const string Output = "Build/revival/Tamer-privacy-product-ump-v1.apk";
    private const string ExpectedCheckout = "D:/codex/worktrees/tamer-working/Tamer";
    internal const string RulesName = "tamer_privacy_product_ump_rules";
    internal static bool Active { get; private set; }
    private static readonly string[] Scenes = { "Login", "Main", "Game", "NextScene" };
    private static readonly string[] Defines = { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS",
        "TAMER_AD_TEST_HARNESS", "TAMER_PRIVACY_PRODUCT_UMP_HARNESS" };
    private static readonly string[] MutableAssets = {
        "ProjectSettings/ProjectSettings.asset", "ProjectSettings/UnityConnectSettings.asset",
        "Assets/Plugins/Android/AndroidManifest.xml", "Assets/Plugins/Android/AndroidManifest.xml.meta",
        "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset",
        "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset.meta",
        "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml",
        "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml.meta" };
    public static void BuildAndroid()
    {
        int code = 1;
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore, oldBundle = EditorUserBuildSettings.buildAppBundle;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        int oldVersion = PlayerSettings.Android.bundleVersionCode;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        var originals = new Dictionary<string, byte[]>();
        FileStream claim = null;
        bool configured = false;
        try
        {
            string checkout = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!string.Equals(checkout.TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(ExpectedCheckout).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
                Environment.GetEnvironmentVariable("TAMER_PRIVACY_PRODUCT_UMP_BUILD_OPT_IN") != "1" ||
                Environment.GetEnvironmentVariable("TAMER_PRIVACY_PRODUCT_UMP_INSTALLED_VERSION_CODE") != "27")
                throw new BuildFailedException("Exact checkout, explicit opt-in and observed installed version 27 required.");
            RevivalBuild.RequireSavedScenes();
            RevivalBuild.ValidateBaseline();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android ||
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Any(symbol => symbol.Trim().StartsWith("TAMER_", StringComparison.Ordinal)) ||
                AdRequestPolicy.ProductionAdsEnabled || !AgeTreatmentPolicy.NativePrivacyHarnessReviewsDisabled ||
                PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial ||
                PlayerSettings.Android.minSdkVersion != (AndroidSdkVersions)25 ||
                PlayerSettings.Android.targetSdkVersion != (AndroidSdkVersions)36 ||
                PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                throw new BuildFailedException("Disabled policies, clean harness symbols and min25/target36/ARM64 baseline required.");
            if (File.Exists("ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json") ||
                File.Exists("Assets/Settings/Pipeline/Resources/RuntimePipelineConfig.asset"))
                throw new BuildFailedException("Runtime Pipeline configuration must be absent.");
            var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
            if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
                throw new BuildFailedException("IAP and UGS auto initialization must be disabled.");
            // Same exact-checkout/duplicate/explicit-false/current-setting validator as the cold publisher build.
            RevivalAdHarnessBuild.ReadPrivatePublisherAppId();
            var scenePaths = Scenes.Select(name => "Assets/Scenes/" + name + ".unity").ToArray();
            foreach (string path in MutableAssets.Concat(scenePaths).Concat(scenePaths.Select(path => path + ".meta")))
            {
                if (!File.Exists(path)) throw new BuildFailedException("Restoration input missing; preserve source.");
                originals.Add(path, File.ReadAllBytes(path));
            }
            Directory.CreateDirectory("Build/revival");
            claim = new FileStream(Output + ".building", FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 1, FileOptions.DeleteOnClose);
            if (File.Exists(Output)) throw new BuildFailedException("Existing product UMP artifact must be preserved.");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AgeTreatmentPolicy.NativePrivacyColdHarnessPackage);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.bundleVersionCode = 28;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            configured = true;
            using (var manifest = new RevivalAdHarnessBuild.IsolatedManifestScope(Manifest))
            using (var analytics = new RevivalAdHarnessBuild.PrivacyUiAnalyticsScope())
            using (RevivalBuild.AndroidRelroLinkScope())
            {
                Active = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenePaths, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                    locationPathName = Output, options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                    extraScriptingDefines = Defines });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Privacy product UMP build failed; inspect private log.");
                code = 0;
            }
        }
        catch (Exception) { code = 1; Debug.LogError("PRIVACY_PRODUCT_UMP_BUILD_FAILED private details suppressed"); }
        finally
        {
            Active = false;
            try
            {
                if (configured && (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != AgeTreatmentPolicy.NativePrivacyColdHarnessPackage ||
                    PlayerSettings.Android.useCustomKeystore || PlayerSettings.Android.keyaliasName != oldAlias ||
                    PlayerSettings.Android.bundleVersionCode != 28 || EditorUserBuildSettings.buildAppBundle ||
                    PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP))
                    throw new BuildFailedException("Concurrent owned setting change; preserve for external recovery.");
                if (configured)
                {
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
                    PlayerSettings.Android.useCustomKeystore = oldKey;
                    PlayerSettings.Android.keyaliasName = oldAlias;
                    PlayerSettings.Android.bundleVersionCode = oldVersion;
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
                    EditorUserBuildSettings.buildAppBundle = oldBundle;
                    AssetDatabase.SaveAssets();
                }
                // Manifest/Analytics scopes restore their own changes. Never overwrite unknown file deltas.
                // Unity can normalize ProjectSettings bytes when restoring identical semantic values.
                // The external 62-input protected executor must capture and review these deltas before restoring bytes.
                if (originals.Where(entry => entry.Key != "ProjectSettings/ProjectSettings.asset" &&
                    entry.Key != "ProjectSettings/UnityConnectSettings.asset")
                    .Any(entry => !File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)) ||
                    PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != oldId ||
                    PlayerSettings.Android.keyaliasName != oldAlias || PlayerSettings.Android.useCustomKeystore != oldKey ||
                    PlayerSettings.Android.bundleVersionCode != oldVersion ||
                    PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != oldBackend ||
                    EditorUserBuildSettings.buildAppBundle != oldBundle)
                    throw new BuildFailedException("Privacy product UMP restoration mismatch; preserve for external recovery.");
            }
            finally { claim?.Dispose(); }
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Privacy product UMP build failed.");
    }
    internal static string Manifest(string original)
    {
        var document = XDocument.Parse(RevivalAdHarnessBuild.IsolatedUmpManifest(original));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var root = document.Root;
        foreach (var permission in root.Elements().Where(node => node.Name.LocalName.StartsWith("uses-permission", StringComparison.Ordinal)).ToArray())
            permission.Remove();
        foreach (string permission in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE" })
            root.Add(new XElement("uses-permission", new XAttribute(android + "name", permission)));
        foreach (string permission in new[] { "com.android.vending.BILLING", "com.google.android.gms.permission.AD_ID",
            "android.permission.ACCESS_ADSERVICES_AD_ID", "android.permission.ACCESS_ADSERVICES_ATTRIBUTION", "android.permission.ACCESS_ADSERVICES_TOPICS" })
            root.Add(new XElement("uses-permission", new XAttribute(android + "name", permission), new XAttribute(tools + "node", "remove")));
        var app = root.Element("application");
        app.SetAttributeValue(android + "allowBackup", "false");
        app.SetAttributeValue(android + "fullBackupContent", "false");
        app.SetAttributeValue(android + "dataExtractionRules", "@xml/" + RulesName);
        var replacements = ((string)app.Attribute(tools + "replace") ?? "").Split(',')
            .Select(value => value.Trim()).Where(value => value.Length > 0);
        app.SetAttributeValue(tools + "replace", string.Join(",", replacements.Concat(new[] {
            "android:allowBackup", "android:fullBackupContent", "android:dataExtractionRules" }).Distinct()));
        return document.ToString();
    }
    [Serializable] private sealed class CatalogFlags
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
    }
}

public sealed class RevivalPrivacyProductUmpBackupRules : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 101;
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (!RevivalPrivacyProductUmpBuild.Active ||
            PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != AgeTreatmentPolicy.NativePrivacyColdHarnessPackage) return;
        string destination = Path.Combine(path, "src/main/res/xml/" + RevivalPrivacyProductUmpBuild.RulesName + ".xml");
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.WriteAllText(destination, RevivalGameplayBuild.PlayerRestoreExtractionRules());
    }
}
