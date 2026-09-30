// Synthetic-only template. Never stage together with PrivateProductionAdsBuild.
// No Build entry: a separately reviewed runner must prepare fixtures/settings and call Prepare.
using System;
using System.Linq;
using System.IO;
using UnityEditor.Compilation;
using AD;
using AD.Advertising;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrivateAdsSyntheticCallbacks : IPreprocessBuildWithReport,
    IProcessSceneWithReport, IPostprocessBuildWithReport
{
    public const string Package = "com.tamer.revival.privateads.synthetic";
    public const string Fixture = "Assets/RevivalPrivateAdsSynthetic/Fixture.unity";
    // Public Google Android rewarded test unit. No arbitrary ID input is accepted.
    public const string TestApp = "ca-app-pub-3940256099942544~3347511713";
    public const string TestUnit = "ca-app-pub-3940256099942544/5224354917";
    private static bool prepared, preprocessed, postprocessed;
    private static int injections, scenes;
    private static string output;
    public int callbackOrder => int.MaxValue;

    public static void Prepare(string expectedOutput)
    {
        if (prepared) throw Rejected();
        RequireSyntheticSettings();
        var expected = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/revival/privateads-synthetic.aab"));
        if (!Path.IsPathRooted(expectedOutput) || !string.Equals(Path.GetFullPath(expectedOutput), expected,
            Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            File.Exists(expected)) throw Rejected();
        output = expected;
        prepared = true;
        preprocessed = postprocessed = false;
        injections = scenes = 0;
    }
    public static object ReadResult()
    {
        if (!prepared || !preprocessed || !postprocessed || injections != 1 || scenes != 1) throw Rejected();
        return new { syntheticCallbacksCompleted = true, injectedManagers = injections,
            productionContractVerified = false, binaryVerified = false, distributable = false };
    }
    public static void Reset() { prepared = preprocessed = postprocessed = false; injections = scenes = 0; output = null; }
    public void OnPreprocessBuild(BuildReport report)
    {
        Guard(report);
        if (preprocessed) throw Rejected();
        preprocessed = true;
    }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        Guard(report);
        if (!preprocessed || postprocessed || scene.path != Fixture || ++scenes != 1) throw Rejected();
        var roots = scene.GetRootGameObjects();
        if (roots.Length != 1 || roots[0].activeSelf ||
            roots[0].GetComponentsInChildren<MonoBehaviour>(true).Any(component => !(component is GoogleAdMobManager)))
            throw Rejected();
        var managers = roots.SelectMany(root => root.GetComponentsInChildren<GoogleAdMobManager>(true)).ToArray();
        if (managers.Length != 1 || managers[0].gameObject.activeInHierarchy) throw Rejected();
        injections += PrivateAdsSceneInjection.Apply(scene, Fixture, TestUnit);
        if (injections != 1) throw Rejected();
    }
    public void OnPostprocessBuild(BuildReport report)
    {
        Guard(report);
        if (!preprocessed || postprocessed || injections != 1 || scenes != 1) throw Rejected();
        postprocessed = true;
    }
    private static void Guard(BuildReport report)
    {
        RequireSyntheticSettings();
        if (!prepared || report == null || report.summary.platform != BuildTarget.Android ||
            report.summary.options != BuildOptions.None || string.IsNullOrEmpty(output) ||
            !string.Equals(Path.GetFullPath(report.summary.outputPath), output,
                Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw Rejected();
    }
    private static void RequireSyntheticSettings()
    {
        if (!Application.isBatchMode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android ||
            EditorUserBuildSettings.development || !EditorUserBuildSettings.buildAppBundle ||
            EditorUserBuildSettings.exportAsGoogleAndroidProject || PlayerSettings.Android.useCustomKeystore ||
            PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != Package ||
            !EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).SequenceEqual(new[] { Fixture }) ||
            AdRequestPolicy.ProductionAdsEnabled || AgeTreatmentPolicy.RegionalConsentReviewed ||
            Enum.GetValues(typeof(AgeChoice)).Cast<AgeChoice>().Any(AgeTreatmentPolicy.IsReviewed)) throw Rejected();
        var configured = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
        if (configured.Split(';').Any(value => value.StartsWith("TAMER_", StringComparison.Ordinal) ||
            value.IndexOf("TEST", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("HARNESS", StringComparison.OrdinalIgnoreCase) >= 0 || value == "DEVELOPMENT_BUILD")) throw Rejected();
        var settings = AssetDatabase.LoadMainAssetAtPath("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
        if (settings == null) throw Rejected();
        var app = new SerializedObject(settings).FindProperty("adMobAndroidAppId");
        if (app == null || app.stringValue != TestApp) throw Rejected();
        var signingNames = new[] { "TAMER_KEYSTORE_PASS", "TAMER_KEYALIAS_PASS", "TAMER_UPLOAD_KEYSTORE_PATH",
            "TAMER_UPLOAD_KEY_ALIAS", "TAMER_UPLOAD_CERT_SHA256", "TAMER_IAP_TEST_KEY_PASSWORD" };
        if (Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(key =>
            key.StartsWith("TAMER_PRIVATE_ADS_", StringComparison.OrdinalIgnoreCase) ||
            signingNames.Contains(key, StringComparer.OrdinalIgnoreCase))) throw Rejected();
        if (Application.unityVersion != "6000.3.25f1" || EditorUserBuildSettings.activeScriptCompilationDefines.Any(value =>
            value.StartsWith("TAMER_", StringComparison.Ordinal) || value.IndexOf("HARNESS", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value == "DEVELOPMENT_BUILD")) throw Rejected();
        var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
        if (assemblies == null || assemblies.Length == 0 || assemblies.Any(assembly => assembly == null || assembly.defines == null ||
            !assembly.defines.Contains("UNITY_ANDROID") || assembly.defines.Contains("UNITY_EDITOR") ||
            assembly.defines.Any(value => string.IsNullOrEmpty(value) ||
                (value != "ENABLE_MARSHALLING_TESTS" && (value.StartsWith("TAMER_", StringComparison.Ordinal) ||
                value.IndexOf("TEST", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("HARNESS", StringComparison.OrdinalIgnoreCase) >= 0 || value == "DEVELOPMENT_BUILD"))))) throw Rejected();
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            assembly.GetType("PrivateProductionAdsBuild", false) != null)) throw Rejected();
    }
    private static Exception Rejected() { return new BuildFailedException("Synthetic-only callback contract rejected."); }
}
