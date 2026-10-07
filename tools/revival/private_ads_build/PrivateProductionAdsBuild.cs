// Template: never import into Assets except through the owned staging wrapper.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AD;
using AD.Advertising;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrivateProductionAdsBuild : IPreprocessBuildWithReport,
    IProcessSceneWithReport, IPostprocessBuildWithReport
{
    private const string Login = "Assets/Scenes/Login.unity";
    private static Snapshot snapshot;
    private static int injections, loginScenes;
    private static bool preprocessed, postprocessed;
    private static bool approvedProduction, approvedPrivacy;
    private static string ResourcePath => approvedPrivacy
        ? "Assets/Resources/RevivalPrivatePrivacyRelease.bytes" : "Assets/Resources/RevivalPrivateAdsRelease.bytes";
    private static string Mode => approvedPrivacy ? "privacy_only_adult"
        : approvedProduction ? "approved_adult_release" : "disabled_candidate";
    public int callbackOrder => int.MaxValue;

    private sealed class Snapshot
    {
        public readonly string ConfigPath, ConfigHash, Output, Receipt, Defines, PlayerCompilationPlan, RunId, SourceHead, ResourceHash;
        public readonly string AppId, RewardedUnit;
        public readonly string[] Scenes;
        public Snapshot(string root)
        {
            RunId = Required("TAMER_PRIVATE_ADS_RUN_ID");
            SourceHead = Required("TAMER_PRIVATE_ADS_SOURCE_HEAD");
            if (!System.Text.RegularExpressions.Regex.IsMatch(RunId, @"\A[0-9a-f]{32}\z") ||
                !System.Text.RegularExpressions.Regex.IsMatch(SourceHead, @"\A[0-9a-f]{40}\z")) throw Rejected();
            ConfigPath = Required("TAMER_PRIVATE_ADS_CONFIG");
            ConfigHash = Required("TAMER_PRIVATE_ADS_SHA256");
            Output = Required("TAMER_PRIVATE_ADS_OUTPUT");
            Receipt = Required("TAMER_PRIVATE_ADS_RECEIPT");
            ResourceHash = Required("TAMER_PRIVATE_ADS_RESOURCE_SHA256");
            var raw = File.ReadAllBytes(ConfigPath);
            if (Hash(raw) != ConfigHash) throw Rejected();
            if (approvedProduction || approvedPrivacy)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
                var app = asset == null ? null : new SerializedObject(asset).FindProperty("adMobAndroidAppId");
                if (app == null) throw Rejected();
                var package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                string unit = "";
                if (approvedPrivacy ? !PrivatePrivacyReleaseContract.TryReadApprovedBuildContract(raw, package, app.stringValue)
                    : !PrivateAdsReleaseContract.TryReadApprovedBuildContract(raw, package, app.stringValue, out unit)) throw Rejected();
                CheckSourceManifest(app.stringValue);
                AppId = app.stringValue; RewardedUnit = unit;
                if (Hash(raw) != ResourceHash) throw Rejected();
            }
            else
            {
                var config = PrivateAdsContract.Read(raw, root);
                AppId = config.AppId; RewardedUnit = config.RewardedUnit;
            }
            Defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            PlayerCompilationPlan = ReadPlayerCompilationPlan();
            Scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (Scenes.Count(s => s == Login) != 1 || Scenes.Distinct().Count() != Scenes.Length ||
                Scenes.Any(s => s.IndexOf("Harness", StringComparison.OrdinalIgnoreCase) >= 0) ||
                Defines.Split(';').Any(ForbiddenDefine)) throw Rejected();
        }
    }

    // Only these declared settings may be restored internally. Whole-tree recovery
    // remains an external exact-delta review even if this scope succeeds.
    private sealed class SettingsScope : IDisposable
    {
        private readonly bool bundle = EditorUserBuildSettings.buildAppBundle;
        private readonly bool development = EditorUserBuildSettings.development;
        private readonly bool export = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        private readonly ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        private readonly AndroidArchitecture architecture = PlayerSettings.Android.targetArchitectures;
        private readonly bool custom = PlayerSettings.Android.useCustomKeystore;
        private readonly string key = PlayerSettings.Android.keystoreName, alias = PlayerSettings.Android.keyaliasName;
        private readonly string keyPassword = PlayerSettings.Android.keystorePass, aliasPassword = PlayerSettings.Android.keyaliasPass;
        private readonly string receipt = Required("TAMER_PRIVATE_ADS_SETTINGS_RECEIPT");
        private readonly string runId = Required("TAMER_PRIVATE_ADS_RUN_ID");

        public void Apply()
        {
            var existingKey = Required("TAMER_PRIVATE_ADS_KEYSTORE");
            if (!Path.IsPathRooted(existingKey) || Hash(File.ReadAllBytes(existingKey)) !=
                Required("TAMER_PRIVATE_ADS_KEYSTORE_SHA256")) throw Rejected();
            EditorUserBuildSettings.buildAppBundle = !approvedPrivacy;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = existingKey;
            PlayerSettings.Android.keyaliasName = Required("TAMER_PRIVATE_ADS_KEY_ALIAS");
            PlayerSettings.Android.keystorePass = Required("TAMER_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasPass = Required("TAMER_KEYALIAS_PASS");
        }

        public void Dispose()
        {
            bool restored = false;
            try
            {
                EditorUserBuildSettings.buildAppBundle = bundle;
                EditorUserBuildSettings.development = development;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = export;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend);
                PlayerSettings.Android.targetArchitectures = architecture;
                PlayerSettings.Android.useCustomKeystore = custom;
                PlayerSettings.Android.keystoreName = key;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keystorePass = keyPassword;
                PlayerSettings.Android.keyaliasPass = aliasPassword;
                restored = EditorUserBuildSettings.buildAppBundle == bundle &&
                    EditorUserBuildSettings.development == development &&
                    EditorUserBuildSettings.exportAsGoogleAndroidProject == export &&
                    PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == backend &&
                    PlayerSettings.Android.targetArchitectures == architecture &&
                    PlayerSettings.Android.useCustomKeystore == custom &&
                    PlayerSettings.Android.keystoreName == key && PlayerSettings.Android.keyaliasName == alias &&
                    PlayerSettings.Android.keystorePass == keyPassword && PlayerSettings.Android.keyaliasPass == aliasPassword;
            }
            finally
            {
                using (var file = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(file)) writer.Write(JsonUtility.ToJson(
                    new SettingsReceipt { schema = 1, runId = runId, scopedSettingsRestored = restored }, true));
            }
            if (!restored) throw Rejected();
        }
    }

    public static void Build()
    { BuildMode(false); }

    // Separate entry/schema. It cannot interpret a disabled six-key configuration
    // as approval, nor accept approval bindings from environment variables.
    public static void BuildApprovedAdultRelease()
    { BuildMode(true); }

    public static void BuildApprovedAdultPrivacy()
    { BuildMode(false, true); }

    private static void BuildMode(bool approved, bool privacy = false)
    {
        approvedProduction = approved;
        approvedPrivacy = privacy;
        try
        {
            if (Environment.GetEnvironmentVariable("TAMER_PRIVATE_ADS_PREPARE") != "1" ||
                !Application.isBatchMode ||
                Environment.GetEnvironmentVariable("TAMER_PRIVATE_ADS_MODE") !=
                    Mode ||
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw Rejected();
            if (approved || privacy)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
                var app = asset == null ? null : new SerializedObject(asset).FindProperty("adMobAndroidAppId");
                if (app == null) throw Rejected();
                var raw = File.ReadAllBytes(Required("TAMER_PRIVATE_ADS_CONFIG"));
                var package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                if (privacy ? !PrivatePrivacyReleaseContract.TryReadApprovedBuildContract(raw, package, app.stringValue)
                    : !PrivateAdsReleaseContract.TryReadApprovedBuildContract(raw, package, app.stringValue, out _)) throw Rejected();
                CheckSourceManifest(app.stringValue);
            }
            var editor = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(Path.GetFullPath(editor), Path.GetFullPath(Required("TAMER_PRIVATE_ADS_EDITOR")),
                    StringComparison.OrdinalIgnoreCase) || Hash(File.ReadAllBytes(editor)) !=
                    Required("TAMER_PRIVATE_ADS_EDITOR_SHA256")) throw Rejected();
            using (var settings = new SettingsScope())
            {
            settings.Apply();
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            snapshot = new Snapshot(root);
            var args = Environment.GetCommandLineArgs();
            var outputs = Enumerable.Range(0, args.Length - 1).Where(i => args[i] == "-buildOutput").ToArray();
            if (outputs.Length != 1 || Path.GetFullPath(args[outputs[0] + 1]) != snapshot.Output ||
                !snapshot.Output.EndsWith(privacy ? ".apk" : ".aab", StringComparison.OrdinalIgnoreCase) ||
                (privacy && Path.GetFileName(snapshot.Output) != "privacy-only-adult.apk") ||
                File.Exists(snapshot.Output) || File.Exists(snapshot.Receipt)) throw Rejected();
            injections = loginScenes = 0;
            preprocessed = postprocessed = false;
            CheckSnapshot();
            BuildReport report;
            using (RevivalBuild.AndroidRelroLinkScope())
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = (string[])snapshot.Scenes.Clone(), locationPathName = snapshot.Output,
                target = BuildTarget.Android, options = BuildOptions.None,
                extraScriptingDefines = new string[0] });
            CheckSnapshot();
            if (report.summary.result != BuildResult.Succeeded || !preprocessed || !postprocessed ||
                injections != 1 || loginScenes != 1 || !File.Exists(snapshot.Output)) throw Rejected();
            // This confirms callbacks only. It does NOT prove the final serialized
            // AAB, merged manifest, player defines, or stripped player gates.
            Receipt receipt = privacy ? (Receipt)new PrivacyReceipt {
                approvedPrivacyBuildContractMatched = true, productionAdsEnabled = false } : new Receipt();
            receipt.schema = 1; receipt.runId = snapshot.RunId; receipt.sourceHead = snapshot.SourceHead;
            receipt.preprocessed = preprocessed; receipt.postprocessed = postprocessed;
            receipt.loginScenes = loginScenes; receipt.productionContractVerified = false;
            receipt.configSha256 = snapshot.ConfigHash; receipt.resourceSha256 = snapshot.ResourceHash;
            receipt.artifactSha256 = Hash(File.ReadAllBytes(snapshot.Output)); receipt.unityVersion = Application.unityVersion;
            receipt.configuredAndroidDefines = snapshot.Defines; receipt.injectedManagers = injections;
            receipt.prospectivePlayerDefinePlanSha256 = Hash(System.Text.Encoding.UTF8.GetBytes(snapshot.PlayerCompilationPlan));
            receipt.buildSceneValueMatched = true; receipt.compiledEditorGatesDisabled = !approved && !privacy;
            receipt.binaryVerified = false; receipt.distributable = false;
            receipt.mode = Mode; receipt.approvedBuildContractMatched = approved;
            using (var file = new FileStream(snapshot.Receipt, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file)) writer.Write(JsonUtility.ToJson(receipt, true));
            }
        }
        catch { throw Rejected(); } // Never emit private parser values or identifiers.
        finally { snapshot = null; approvedProduction = approvedPrivacy = false; }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        Guard(report);
        if (preprocessed) throw Rejected();
        preprocessed = true;
    }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return; // Ordinary Editor scene loading is not injection.
        Guard(report);
        if (!preprocessed || postprocessed || !snapshot.Scenes.Contains(scene.path)) throw Rejected();
        if (scene.path == Login && ++loginScenes != 1) throw Rejected();
        injections += PrivateAdsSceneInjection.Apply(scene, Login, snapshot.RewardedUnit, approvedPrivacy);
        if (injections > 1) throw Rejected();

    }
    public void OnPostprocessBuild(BuildReport report)
    {
        Guard(report);
        if (!preprocessed || postprocessed || injections != 1 || loginScenes != 1) throw Rejected();
        postprocessed = true;
    }
    private static void Guard(BuildReport report)
    {
        try
        {
            CheckSnapshot();
            if (report == null || report.summary.platform != BuildTarget.Android ||
                report.summary.options != BuildOptions.None ||
                Path.GetFullPath(report.summary.outputPath) != snapshot.Output) throw Rejected();
        }
        catch { throw Rejected(); }
    }
    private static void CheckSnapshot()
    {
        if (snapshot == null || Hash(File.ReadAllBytes(snapshot.ConfigPath)) != snapshot.ConfigHash ||
            Hash(File.ReadAllBytes(ResourcePath)) != snapshot.ResourceHash ||
            EditorUserBuildSettings.buildAppBundle == approvedPrivacy || EditorUserBuildSettings.development ||
            EditorUserBuildSettings.exportAsGoogleAndroidProject ||
            PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
            PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64 ||
            !PlayerSettings.Android.useCustomKeystore ||
            (AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed != approvedPrivacy) ||
            (!approvedProduction && !approvedPrivacy && (AdRequestPolicy.ProductionAdsEnabled || AgeTreatmentPolicy.RegionalConsentReviewed ||
            Enum.GetValues(typeof(AgeChoice)).Cast<AgeChoice>().Any(AgeTreatmentPolicy.IsReviewed))) ||
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) != snapshot.Defines ||
            ReadPlayerCompilationPlan() != snapshot.PlayerCompilationPlan ||
            !EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(snapshot.Scenes))
            throw Rejected();
#if TAMER_TEST_ADS || TAMER_REVIVAL_SMOKE || TAMER_AD_TEST_HARNESS || TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS
        throw Rejected();
#endif
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
        if (asset == null) throw Rejected();
        var app = new SerializedObject(asset).FindProperty("adMobAndroidAppId");
        if (app == null || app.stringValue != snapshot.AppId) throw Rejected();
        if (approvedProduction && (!PrivateAdsReleaseContract.TryReadApprovedBuildContract(
                File.ReadAllBytes(snapshot.ConfigPath), PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                app.stringValue, out var unit) || unit != snapshot.RewardedUnit)) throw Rejected();
        if (approvedPrivacy && (!PrivatePrivacyReleaseContract.TryReadApprovedBuildContract(
                File.ReadAllBytes(snapshot.ConfigPath), PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), app.stringValue) ||
                snapshot.RewardedUnit != "" || Resources.LoadAll<TextAsset>("RevivalPrivateAdsRelease").Length != 0 ||
                Resources.LoadAll<TextAsset>("RevivalPrivatePrivacyRelease").Length != 1)) throw Rejected();
        if (approvedPrivacy) CheckSourceManifest(app.stringValue);
        var resource = AssetDatabase.LoadAssetAtPath<TextAsset>(ResourcePath);
        if (resource == null || Hash(resource.bytes) != snapshot.ResourceHash ||
            PlayerSettings.Android.keystoreName != Required("TAMER_PRIVATE_ADS_KEYSTORE") ||
            PlayerSettings.Android.keyaliasName != Required("TAMER_PRIVATE_ADS_KEY_ALIAS") ||
            Hash(File.ReadAllBytes(PlayerSettings.Android.keystoreName)) !=
                Required("TAMER_PRIVATE_ADS_KEYSTORE_SHA256")) throw Rejected();
    }
    private static void CheckSourceManifest(string appId)
    {
        var xml = new System.Xml.XmlDocument();
        xml.Load("Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml");
        var ids = xml.GetElementsByTagName("meta-data").Cast<System.Xml.XmlElement>()
            .Where(n => n.GetAttribute("name", "http://schemas.android.com/apk/res/android") == "com.google.android.gms.ads.APPLICATION_ID")
            .Select(n => n.GetAttribute("value", "http://schemas.android.com/apk/res/android")).ToArray();
        if (ids.Length != 1 || ids[0] != appId) throw Rejected();
    }

    private static string ReadPlayerCompilationPlan()
    {
        // Inspect the prospective Player view, not the Editor hosting this callback.
        // Fixed reason codes disclose no source paths, symbols or private identifiers.
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw PlanRejected("P01");
            if (EditorUserBuildSettings.development) throw PlanRejected("P02");
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            if (assemblies == null || assemblies.Length == 0) throw PlanRejected("P03");
            if (assemblies.Any(a => a == null || string.IsNullOrEmpty(a.name) ||
                a.defines == null || a.sourceFiles == null || a.sourceFiles.Any(string.IsNullOrEmpty)))
                throw PlanRejected("P04");
            if (assemblies.Any(a => !a.defines.Contains("UNITY_ANDROID") || a.defines.Contains("UNITY_EDITOR")))
                throw PlanRejected("P05");
            if (assemblies.Any(a => a.defines.Any(d => string.IsNullOrEmpty(d) || ForbiddenPlayerDefine(d))))
                throw PlanRejected("P06");
            if (assemblies.Select(a => a.name).Distinct(StringComparer.Ordinal).Count() != assemblies.Length)
                throw PlanRejected("P07");
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var pathComparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var required = new[] { "Assets/Scripts/Managers/GoogleAdMobManager.cs",
                "Assets/Scripts/Advertising/AdRequestPolicy.cs", "Assets/Scripts/Advertising/AgeTreatmentPolicy.cs" };
            var sourceCodes = new[] { "P08", "P09", "P10" };
            if (approvedPrivacy)
            {
                required = required.Concat(new[] { "Assets/Scripts/Advertising/PrivatePrivacyReleaseContract.cs" }).ToArray();
                sourceCodes = sourceCodes.Concat(new[] { "P12" }).ToArray();
            }
            for (int i = 0; i < required.Length; i++)
            {
                var expected = Path.GetFullPath(Path.Combine(root, required[i]));
                if (assemblies.Sum(a => a.sourceFiles.Count(path => string.Equals(
                    Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path)),
                    expected, pathComparison))) != 1) throw PlanRejected(sourceCodes[i]);
            }
            return string.Join("\n", assemblies.OrderBy(a => a.name, StringComparer.Ordinal).Select(a =>
                a.name + ":" + string.Join(";", a.defines.Distinct(StringComparer.Ordinal).OrderBy(d => d, StringComparer.Ordinal))));
        }
        catch (PlayerPlanRejectedException) { throw; }
        catch { throw PlanRejected("P11"); }
    }
    private sealed class PlayerPlanRejectedException : Exception
    {
        public PlayerPlanRejectedException(string code) : base("Private player plan rejected (" + code + ").") { }
    }
    private static PlayerPlanRejectedException PlanRejected(string code)
    { return new PlayerPlanRejectedException(code); }
    // Narrow metadata exception observed by the approved SHA-256 probe on this version.
    // Configured Android symbols still use ForbiddenDefine without this exception.
    // This recognizes a name, not its provenance or final binary safety.
    private static bool ForbiddenPlayerDefine(string value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        if (string.Equals(Application.unityVersion, "6000.3.25f1", StringComparison.Ordinal) &&
            string.Equals(value, "ENABLE_MARSHALLING_TESTS", StringComparison.Ordinal)) return false;
        return ForbiddenDefine(value);
    }
    private static bool ForbiddenDefine(string value)
    { return value.StartsWith("TAMER_", StringComparison.Ordinal) ||
        value.IndexOf("TEST", StringComparison.OrdinalIgnoreCase) >= 0 ||
        value.IndexOf("HARNESS", StringComparison.OrdinalIgnoreCase) >= 0 || value == "DEVELOPMENT_BUILD"; }
    private static string Required(string name)
    { var value = Environment.GetEnvironmentVariable(name); if (string.IsNullOrEmpty(value)) throw Rejected(); return value; }
    private static string Hash(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static BuildFailedException Rejected()
    { return new BuildFailedException("Private disabled preparation rejected; output is not distributable."); }
    [Serializable] private class Receipt
    {
        public string runId, sourceHead, configSha256, artifactSha256, unityVersion, configuredAndroidDefines,
            prospectivePlayerDefinePlanSha256, resourceSha256, mode;
        public int schema, injectedManagers, loginScenes;
        public bool preprocessed, postprocessed, productionContractVerified, buildSceneValueMatched, compiledEditorGatesDisabled,
            approvedBuildContractMatched, binaryVerified, distributable;
    }
    [Serializable] private sealed class PrivacyReceipt : Receipt
    { public bool approvedPrivacyBuildContractMatched, productionAdsEnabled; }
    [Serializable] private sealed class SettingsReceipt
    { public int schema; public string runId; public bool scopedSettingsRestored; }
}
