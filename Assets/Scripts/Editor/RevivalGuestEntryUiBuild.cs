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
    public const string ReleaseOutput = "Build/revival/Tamer-guest-entry-ui-release-1102.apk";
    public const string FullEngineOutput = "Build/revival/Tamer-guest-entry-ui-full-1102.apk";
    public const string PlainOutput = "Build/revival/Tamer-guest-empty-1102.apk";
    public const string X86Output = "Build/revival/Tamer-guest-entry-ui-x86-1102.apk";
    public const string FreshOutputRoot = "Build/revival/fresh-guest-x86";
    public const string PlainApplicationId = "com.AeDeong.MonsterTamer.revival.guestempty1102";
    private const string Manifest = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string AdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    private const string AdsManifest = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";
    public static bool BuildActive { get; private set; }
    public static bool ReleaseEngineBuild { get; private set; }
    public static bool FullEngineBuild { get; private set; }
    public static bool PlainSceneBuild { get; private set; }
    public static bool X86Build { get; private set; }
    public static bool FreshX86Build { get; private set; }
    public static string ExpectedApplicationId => FreshX86Build ? RevivalGuestEntryUiHarness.FreshX86ApplicationId : X86Build ? RevivalGuestEntryUiHarness.X86ApplicationId
        : PlainSceneBuild ? PlainApplicationId
        : FullEngineBuild ? RevivalGuestEntryUiHarness.FullEngineApplicationId
        : ReleaseEngineBuild ? RevivalGuestEntryUiHarness.ReleaseApplicationId : RevivalGuestEntryUiHarness.ApplicationId;

    public static bool IsFixtureId(string id) => id == RevivalGuestEntryUiHarness.ApplicationId ||
        id == RevivalGuestEntryUiHarness.ReleaseApplicationId || id == RevivalGuestEntryUiHarness.FullEngineApplicationId ||
        id == PlainApplicationId || id == RevivalGuestEntryUiHarness.X86ApplicationId || id == RevivalGuestEntryUiHarness.FreshX86ApplicationId;

    public static bool AllowsGlobalDefines(string defines) => !string.IsNullOrEmpty(defines)
        ? !defines.Split(';').Any(s => s.Trim().StartsWith("TAMER_", StringComparison.Ordinal)) : true;

    public static void BuildAndroid() => Build(false);
    public static void BuildReleaseAndroid() => Build(true);
    public static void BuildFullEngineAndroid() => Build(true, true);
    public static void BuildPlainAndroid() => Build(true, true, true);
    public static void BuildX86Android() => Build(true, true, false, true);
    public static void BuildFreshX86Android() => Build(true, true, false, true, true);

    private static void Build(bool releaseEngine, bool fullEngine = false, bool plainScene = false, bool x86 = false, bool freshX86 = false)
    {
        if (fullEngine && !releaseEngine) throw new BuildFailedException("Full-engine diagnostic uses the release engine only.");
        if (plainScene && !fullEngine) throw new BuildFailedException("Plain-scene diagnostic preserves the same full release engine.");
        if (x86 && (!releaseEngine || !fullEngine || plainScene))
            throw new BuildFailedException("The separate x86 diagnostic requires its full release guest fixture.");
        if (freshX86 && !x86) throw new BuildFailedException("Fresh output is restricted to the exact x86 guest profile.");
        string output = freshX86 ? Path.Combine(FreshOutputRoot, Guid.NewGuid().ToString("N"), "Tamer-guest-entry-ui-x86.apk")
            : x86 ? X86Output : plainScene ? PlainOutput : fullEngine ? FullEngineOutput : releaseEngine ? ReleaseOutput : Output;
        string applicationId = freshX86 ? RevivalGuestEntryUiHarness.FreshX86ApplicationId : x86 ? RevivalGuestEntryUiHarness.X86ApplicationId : plainScene ? PlainApplicationId
            : fullEngine ? RevivalGuestEntryUiHarness.FullEngineApplicationId
            : releaseEngine ? RevivalGuestEntryUiHarness.ReleaseApplicationId
            : RevivalGuestEntryUiHarness.ApplicationId;
        const string template = "Assets/Plugins/Android/mainTemplate.gradle";
        var originals = new[] { Manifest, Manifest + ".meta", AdsSettings, AdsSettings + ".meta", AdsManifest, AdsManifest + ".meta", template, template + ".meta" }.ToDictionary(p => p, File.ReadAllBytes);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string oldName = PlayerSettings.productName;
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        bool oldStripEngineCode = PlayerSettings.stripEngineCode;
        var oldArchitectures = PlayerSettings.Android.targetArchitectures;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        int code = 1;
        bool freshOutputCreated = false;
        FreshReceipt receipt = freshX86 ? new FreshReceipt {
            result = "Prepared", applicationId = applicationId,
            artifact = Path.GetFullPath(output), abi = "x86_64"
        } : null;
        try
        {
            if (Application.dataPath.Replace('\\', '/') != "D:/codex/worktrees/tamer-working/Tamer/Assets")
                throw new BuildFailedException("Reviewed checkout required.");
            RevivalBuild.ValidateBaseline(); RevivalBuild.RequireSavedScenes();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || BuildActive ||
                !AllowsGlobalDefines(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)))
                throw new BuildFailedException("Dedicated Android build and no global Tamer symbols required.");
            if (File.Exists(output) || Directory.Exists(output) ||
                (freshX86 && Directory.Exists(Path.GetDirectoryName(output))))
                throw new BuildFailedException("Preserve existing guest UI artifact.");
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
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
            PlayerSettings.productName = "Tamer Guest UI Trial";
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            if (fullEngine) PlayerSettings.stripEngineCode = false;
            // The initial reviewed ARM64 baseline was checked above. Only this separate
            // offline fixture changes the build architecture, and finally restores it.
            if (x86)
            {
                File.WriteAllBytes(template, System.Text.Encoding.UTF8.GetBytes(RevivalQuadDIntroBuild.X86Template(System.Text.Encoding.UTF8.GetString(originals[template]))));
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
            }
            X86Build = x86;
            FreshX86Build = freshX86;
            FullEngineBuild = fullEngine;
            PlainSceneBuild = plainScene;
            ReleaseEngineBuild = releaseEngine;
            BuildActive = true;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            freshOutputCreated = freshX86;
            if (receipt != null) {
                receipt.result = "Started";
                WriteFreshReceipt(output, receipt);
            }
            BuildReport report;
            using (RevivalBuild.AndroidRelroLinkScope())
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { RevivalBuild.SmokeScene }, target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android, locationPathName = output,
                    options = BuildOptions.CompressWithLz4 | (releaseEngine ? BuildOptions.None : BuildOptions.Development),
                    extraScriptingDefines = plainScene ? new[] { "TAMER_REVIVAL_SMOKE" } : releaseEngine
                        ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GUEST_ENTRY_UI_HARNESS", "TAMER_GUEST_ENTRY_UI_RELEASE_HARNESS" }
                        : new[] { "TAMER_REVIVAL_SMOKE", "TAMER_GUEST_ENTRY_UI_HARNESS" }
                });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Isolated guest UI build failed.");
            Debug.Log("GUEST_ENTRY_UI_ENGINE full_engine=" + fullEngine + " strip_engine=" + PlayerSettings.stripEngineCode);
            Debug.Log("GUEST_ENTRY_UI_VARIANT plain_scene=" + plainScene);
            Debug.Log("GUEST_ENTRY_UI_ABI target=" + PlayerSettings.Android.targetArchitectures);
            Debug.Log("GUEST_ENTRY_UI_BUILD_OK operating_key_used=false operating_scenes=0");
            if (receipt != null) {
                receipt.result = "BuildSucceededRestorationPending";
                receipt.errors = (int)report.summary.totalErrors;
                WriteFreshReceipt(output, receipt);
            }
            code = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            BuildActive = false;
            ReleaseEngineBuild = false;
            FullEngineBuild = false;
            PlainSceneBuild = false;
            X86Build = false;
            FreshX86Build = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.productName = oldName;
            PlayerSettings.Android.useCustomKeystore = oldKey;
            PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            PlayerSettings.stripEngineCode = oldStripEngineCode;
            PlayerSettings.Android.targetArchitectures = oldArchitectures;
            AssetDatabase.SaveAssets();
            foreach (var item in originals) {
                File.WriteAllBytes(item.Key, item.Value);
                if (!item.Key.EndsWith(".meta", StringComparison.Ordinal))
                    AssetDatabase.ImportAsset(item.Key, ImportAssetOptions.ForceUpdate);
            }
            if (originals.Any(item => !File.ReadAllBytes(item.Key).SequenceEqual(item.Value)))
                throw new BuildFailedException("Original guest UI build settings were not restored.");
        }
        // A build report alone is not a completed fixture: restoration above must finish first.
        if (receipt != null && freshOutputCreated) {
            receipt.result = code == 0 ? "Succeeded" : "Failed";
            WriteFreshReceipt(output, receipt);
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Isolated guest UI build failed.");
    }

    private static void WriteFreshReceipt(string output, FreshReceipt receipt) =>
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-status.json"),
            JsonUtility.ToJson(receipt, true));

    [Serializable] private class FreshReceipt { public string result, applicationId, artifact, abi; public int errors; }

    [Serializable] private class CatalogFlags { public bool enableCodelessAutoInitialization; public bool enableUnityGamingServicesAutoInitialization; }

    public sealed class BackupRules : UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (!IsFixtureId(id)) return;
            if (!BuildActive || id != ExpectedApplicationId)
                throw new BuildFailedException("Guest fixture backup rules require owned build scope and variant.");
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
        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (report == null || !BuildPipeline.isBuildingPlayer ||
            !RevivalGuestEntryUiBuild.IsFixtureId(id)) return;
        bool development = (report.summary.options & BuildOptions.Development) != 0;
        if (!RevivalGuestEntryUiBuild.BuildActive || id != RevivalGuestEntryUiBuild.ExpectedApplicationId ||
            scene.path != RevivalBuild.SmokeScene || development == RevivalGuestEntryUiBuild.ReleaseEngineBuild ||
            PlayerSettings.Android.useCustomKeystore)
            throw new BuildFailedException("Guest UI requires its dedicated debug-signed smoke build and matching engine variant.");
        if (RevivalGuestEntryUiBuild.FullEngineBuild && PlayerSettings.stripEngineCode)
            throw new BuildFailedException("Full-engine diagnostic must preserve engine code for this build only.");
        var expectedArchitecture = RevivalGuestEntryUiBuild.X86Build ? AndroidArchitecture.X86_64 : AndroidArchitecture.ARM64;
        if (PlayerSettings.Android.targetArchitectures != expectedArchitecture)
            throw new BuildFailedException("Isolated fixture architecture does not match its dedicated build variant.");
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new BuildFailedException("The original smoke scene must have no game behaviours.");
        if (RevivalGuestEntryUiBuild.PlainSceneBuild)
        {
            Debug.Log("GUEST_ENTRY_UI_SCENE plain=True guest_components=0");
            return;
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DungGeunMo SDF.asset");
        if (font == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font)) != "6b62d0bbc19501141b40b8d32a134954")
            throw new BuildFailedException("Existing Korean font required.");
        var created = new GameObject("Local guest UI fixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, scene);
        created.AddComponent<RevivalGuestEntryUiHarness>().Font = font;
    }
}
