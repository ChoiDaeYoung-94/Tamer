using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    public static void BuildUmpPublisher() => Build(true, true, true);
    // A separate artifact preserves the earlier publisher APK and its evidence.
    public static void BuildUmpPrivacyAge() => Build(true, true, true, true);
    public static void BuildUmpPrivacyAgeIsolated() => Build(true, true, true, true, true);
    public static void BuildUmpPrivacyManagerIsolated() => Build(true, true, true, true, true, true);
    internal static bool PrivacyUiBuildActive { get; private set; }

    public static void BuildPrivacyUi()
    {
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore, oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        int code = 1;
        try
        {
            RevivalBuild.ValidateBaseline();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android ||
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Contains("TAMER_") ||
                AD.Advertising.AdRequestPolicy.ProductionAdsEnabled ||
                AD.Advertising.AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed)
                throw new BuildFailedException("Disabled policy and clean Android baseline required.");
            if (File.Exists("ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json") ||
                File.Exists("Assets/Settings/Pipeline/Resources/RuntimePipelineConfig.asset"))
                throw new BuildFailedException("Existing runtime Pipeline configuration requires review.");
            string catalog = File.ReadAllText("Assets/Resources/IAPProductCatalog.json");
            if (Regex.IsMatch(catalog, "\"enableCodelessAutoInitialization\"\\s*:\\s*true") ||
                Regex.IsMatch(catalog, "\"enableUnityGamingServicesAutoInitialization\"\\s*:\\s*true"))
                throw new BuildFailedException("Automatic purchasing/services must be disabled.");
            RevivalBuild.PrepareSmokeScene();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.AeDeong.MonsterTamer.revival.privacyui");
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            using (var scope = new IsolatedManifestScope(PrivacyUiManifest))
            using (var analytics = new PrivacyUiAnalyticsScope())
            using (RevivalBuild.AndroidRelroLinkScope())
            {
                PrivacyUiBuildActive = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { RevivalBuild.SmokeScene }, target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android, locationPathName = "Build/revival/Tamer-privacy-ui.apk",
                    options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                    extraScriptingDefines = new[] { "TAMER_REVIVAL_SMOKE", "TAMER_PRIVACY_UI_HARNESS" }
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Offline privacy UI build failed.");
            }
            Debug.Log("PRIVACY_UI_BUILD_OK single_scene=true sdk_init_not_requested=true");
            code = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            PrivacyUiBuildActive = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.Android.useCustomKeystore = oldKey;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            AssetDatabase.SaveAssets();
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Offline privacy UI build failed; inspect preserved log.");
    }

    private static string PrivacyUiManifest(string original)
    {
        var document = XDocument.Parse(RevivalGameplayBuild.OfflineManifest(IsolatedUmpManifest(original)));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var app = document.Root.Element("application");
        app.SetAttributeValue(android + "allowBackup", "false");
        app.SetAttributeValue(tools + "replace", "android:allowBackup");
        return document.ToString();
    }

    private sealed class PrivacyUiAnalyticsScope : IDisposable
    {
        private readonly SerializedObject settings;
        private readonly string[] names = { "UnityAnalyticsSettings.m_Enabled", "UnityAnalyticsSettings.m_InitializeOnStartup" };
        private readonly bool[] previous;
        public PrivacyUiAnalyticsScope()
        {
            var target = Unsupported.GetSerializedAssetInterfaceSingleton("UnityConnectSettings");
            if (target == null) throw new BuildFailedException("Unity service settings unavailable.");
            settings = new SerializedObject(target);
            previous = names.Select(name =>
            {
                var value = settings.FindProperty(name);
                if (value == null || value.propertyType != SerializedPropertyType.Boolean)
                    throw new BuildFailedException("Exact Analytics initialization settings required.");
                return value.boolValue;
            }).ToArray();
            try
            {
                foreach (string name in names) settings.FindProperty(name).boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                settings.Update();
                if (names.Any(name => settings.FindProperty(name).boolValue))
                    throw new BuildFailedException("Analytics startup must be disabled for this artifact.");
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            settings.Update();
            for (int i = 0; i < names.Length; i++) settings.FindProperty(names[i]).boolValue = previous[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            settings.Update();
            if (names.Where((name, i) => settings.FindProperty(name).boolValue != previous[i]).Any())
                throw new BuildFailedException("Analytics settings restoration failed.");
        }
    }

    public static string IsolatedUmpManifest(string original)
    {
        var document = XDocument.Parse(original);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var root = document.Root ?? throw new BuildFailedException("Missing manifest root.");
        var app = root.Element("application") ?? throw new BuildFailedException("Missing manifest application.");
        var targets = new[] {
            new { Element = "provider", Name = "com.google.android.gms.games.provider.PlayGamesInitProvider" },
            new { Element = "meta-data", Name = "com.google.android.gms.games.APP_ID" }
        };
        foreach (var target in targets)
            if (app.Elements(target.Element).Any(node => (string)node.Attribute(android + "name") == target.Name))
                throw new BuildFailedException("Existing Play Games isolation target requires review.");
        root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
        foreach (var target in targets)
            app.Add(new XElement(target.Element, new XAttribute(android + "name", target.Name),
                new XAttribute(tools + "node", "remove")));
        return document.ToString();
    }

    private static string NativePrivacyManagerManifest(string original)
    {
        var document = XDocument.Parse(IsolatedUmpManifest(original));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var app = document.Root.Element("application");
        app.SetAttributeValue(android + "allowBackup", "false");
        var replacements = ((string)app.Attribute(tools + "replace") ?? "")
            .Split(',').Select(value => value.Trim()).Where(value => value.Length != 0);
        app.SetAttributeValue(tools + "replace", string.Join(",", replacements
            .Concat(new[] { "android:allowBackup" }).Distinct()));
        return document.ToString();
    }

    private sealed class IsolatedManifestScope : IDisposable
    {
        private const string PathName = "Assets/Plugins/Android/AndroidManifest.xml";
        private FileStream manifest, meta;
        private byte[] original, originalMeta, expected;
        private string guid;

        public IsolatedManifestScope(Func<string, string> transform = null)
        {
            try
            {
                // No delete sharing: retain each original file object through import/build/restoration.
                manifest = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                meta = new FileStream(PathName + ".meta", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                original = ReadBytes(manifest);
                expected = original;
                originalMeta = ReadBytes(meta);
                guid = AssetDatabase.AssetPathToGUID(PathName);
                if (string.IsNullOrEmpty(guid)) throw new BuildFailedException("Manifest GUID missing.");
                string input = System.Text.Encoding.UTF8.GetString(original);
                string transformed = transform == null ? IsolatedUmpManifest(input) : transform(input);
                WriteBytes(System.Text.Encoding.UTF8.GetBytes(transformed));
                AssetDatabase.ImportAsset(PathName, ImportAssetOptions.ForceUpdate);
                VerifyUnchanged();
            }
            catch
            {
                try { Restore(); }
                catch { Debug.LogError("UMP_PGS_MANIFEST_CONSTRUCTION_RESTORE_FAILED"); }
                finally { Close(); }
                throw;
            }
        }

        private static byte[] ReadBytes(FileStream stream)
        {
            stream.Position = 0;
            using (var copy = new MemoryStream()) { stream.CopyTo(copy); return copy.ToArray(); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(IntPtr handle, out FileInformation information);

        private static string Identity(FileStream stream)
        {
            FileInformation info;
            if (!GetFileInformationByHandle(stream.SafeFileHandle.DangerousGetHandle(), out info))
                throw new BuildFailedException("Manifest file identity unavailable.");
            return info.Volume + ":" + info.IndexHigh + ":" + info.IndexLow;
        }

        private void VerifyUnchanged()
        {
            if (expected != null && (!ReadBytes(manifest).SequenceEqual(expected) ||
                !File.ReadAllBytes(PathName).SequenceEqual(expected)))
                throw new BuildFailedException("Concurrent manifest byte change; preserve for external recovery.");
            if (originalMeta != null && (!ReadBytes(meta).SequenceEqual(originalMeta) ||
                !File.ReadAllBytes(PathName + ".meta").SequenceEqual(originalMeta) ||
                AssetDatabase.AssetPathToGUID(PathName) != guid))
                throw new BuildFailedException("Concurrent manifest metadata change; preserve for external recovery.");
        }

        private void WriteBytes(byte[] bytes)
        {
            VerifyUnchanged();
            // Unity's ordinary readers can coexist with a read anchor, but not a long-lived write handle.
            using (var writer = new FileStream(PathName, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                if (Identity(writer) != Identity(manifest)) throw new BuildFailedException("Manifest file object changed.");
                if (!ReadBytes(manifest).SequenceEqual(expected))
                    throw new BuildFailedException("Concurrent manifest change before exclusive write; preserve for external recovery.");
                writer.Write(bytes, 0, bytes.Length); writer.SetLength(bytes.Length); writer.Flush(true);
            }
            expected = bytes;
        }

        private void Restore()
        {
            if (original != null) { WriteBytes(original); AssetDatabase.ImportAsset(PathName, ImportAssetOptions.ForceUpdate); }
            if (original != null && (!ReadBytes(manifest).SequenceEqual(original) || !File.ReadAllBytes(PathName).SequenceEqual(original)))
                throw new BuildFailedException("UMP manifest byte restoration failed.");
            if (originalMeta != null && (!ReadBytes(meta).SequenceEqual(originalMeta) ||
                !File.ReadAllBytes(PathName + ".meta").SequenceEqual(originalMeta) ||
                AssetDatabase.AssetPathToGUID(PathName) != guid))
                throw new BuildFailedException("UMP manifest metadata restoration failed.");
        }

        private void Close() { meta?.Dispose(); manifest?.Dispose(); }
        public void Dispose() { try { Restore(); } finally { Close(); } }
    }

    private static string ReadPrivatePublisherAppId()
    {
        if (Environment.GetEnvironmentVariable("TAMER_UMP_PUBLISHER_BUILD_OPT_IN") != "1")
            throw new BuildFailedException("Publisher UMP build requires explicit private opt-in.");
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.Combine(root, "Logs/revival/production-ads-prepared/production-ads.private.json");
        string json = File.ReadAllText(path);
        foreach (string field in new[] { "checkout", "androidAppId", "productionActivationApproved", "regionalReviewApproved" })
            if (Regex.Matches(json, "\"" + field + "\"\\s*:").Count != 1)
                throw new BuildFailedException("Private publisher configuration must be complete and unambiguous.");
        foreach (string field in new[] { "productionActivationApproved", "regionalReviewApproved" })
            if (!Regex.IsMatch(json, "\"" + field + "\"\\s*:\\s*false\\s*(?=[,}])"))
                throw new BuildFailedException("Private publisher approvals must be explicit JSON false.");
        var config = JsonUtility.FromJson<PublisherConfig>(json);
        if (config == null || string.IsNullOrEmpty(config.checkout) ||
            !string.Equals(Path.GetFullPath(config.checkout), root, StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(config.androidAppId ?? "", @"\Aca-app-pub-[0-9]{16}~[0-9]{10}\z") ||
            config.androidAppId.StartsWith("ca-app-pub-3940256099942544~", StringComparison.Ordinal) ||
            config.productionActivationApproved || config.regionalReviewApproved ||
            AD.Advertising.AdRequestPolicy.ProductionAdsEnabled || AD.Advertising.AgeTreatmentPolicy.RegionalConsentReviewed)
            throw new BuildFailedException("Private publisher configuration is missing or outside disabled scope.");
        var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
        if (settings.FindProperty("adMobAndroidAppId").stringValue.Trim() != config.androidAppId)
            throw new BuildFailedException("Private publisher App ID must match the existing app settings.");
        return config.androidAppId;
    }

    private static void Build(bool development, bool umpOnly = false, bool publisher = false, bool privacyAge = false,
        bool isolatePgs = false, bool nativePrivacyManager = false)
    {
        int exitCode = 1;
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        string oldAlias = PlayerSettings.Android.keyaliasName;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        byte[] settingsBytes = File.ReadAllBytes(SettingsPath), manifestBytes = File.ReadAllBytes(ManifestPath);
        byte[] publisherSceneBytes = null, publisherSceneMetaBytes = null;
        IsolatedManifestScope isolatedManifest = null;
        string variant = publisher ? "ump-publisher" : umpOnly ? "ump-sample" : development ? "sample" : "control";
        if (privacyAge) variant += "-privacy-age";
        if (isolatePgs) variant += "-pgs-isolated";
        if (nativePrivacyManager) variant += "-privacy-manager-reopen-no-backup";
        string applicationId = nativePrivacyManager ? AD.Advertising.AgeTreatmentPolicy.NativePrivacyHarnessPackage :
            publisher ? "com.AeDeong.MonsterTamer.revival.umppublisher" : umpOnly ? "com.AeDeong.MonsterTamer.revival.ump" :
            development ? "com.AeDeong.MonsterTamer.revival.ads" : "com.AeDeong.MonsterTamer.revival.adscontrol";
        try
        {
            if (nativePrivacyManager && (!development || !umpOnly || !publisher || !privacyAge || !isolatePgs ||
                Environment.GetEnvironmentVariable("TAMER_PRIVACY_MANAGER_BUILD_OPT_IN") != "1" ||
                AD.Advertising.AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed))
                throw new BuildFailedException("Native privacy manager requires separate explicit isolated build opt-in.");
            if (isolatePgs && File.Exists("Build/revival/Tamer-ads-" + variant + ".apk"))
                throw new BuildFailedException("Existing isolated UMP artifact must be preserved.");
            RevivalBuild.ValidateBaseline();
            var catalog = JsonUtility.FromJson<CatalogFlags>(File.ReadAllText("Assets/Resources/IAPProductCatalog.json"));
            if (catalog == null || catalog.enableCodelessAutoInitialization || catalog.enableUnityGamingServicesAutoInitialization)
                throw new BuildFailedException("Harness requires IAP/UGS catalog auto initialization disabled.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Launch with -buildTarget Android.");
            if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Contains("TAMER_TEST_ADS"))
                throw new BuildFailedException("Remove global TAMER_TEST_ADS: control must retain ordinary release policy.");
            string appId = publisher ? ReadPrivatePublisherAppId() : SampleAppId;
            if (publisher)
            {
                if (!File.Exists(ScenePath) || !File.Exists(ScenePath + ".meta"))
                    throw new BuildFailedException("Publisher UMP requires the existing restored harness scene.");
                publisherSceneBytes = File.ReadAllBytes(ScenePath);
                publisherSceneMetaBytes = File.ReadAllBytes(ScenePath + ".meta");
            }
            PrepareScene();
            if (nativePrivacyManager)
            {
                var behaviours = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var manager = behaviours.OfType<GoogleAdMobManager>().SingleOrDefault();
                var harness = behaviours.OfType<RevivalAdHarness>().SingleOrDefault();
                if (manager == null || harness == null || harness.Ads != manager ||
                    !manager.enabled || !manager.gameObject.activeInHierarchy || behaviours.OfType<Managers>().Any())
                    throw new BuildFailedException("Native privacy harness requires one active actual ad manager and no Managers initializer.");
            }
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
            settings.FindProperty("adMobAndroidAppId").stringValue = appId;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            if (isolatePgs)
            {
                if (!development || !umpOnly || !publisher || !privacyAge ||
                    PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != applicationId)
                    throw new BuildFailedException("Play Games isolation requires the explicit UMP privacy debug variant.");
                isolatedManifest = nativePrivacyManager
                    ? new IsolatedManifestScope(NativePrivacyManagerManifest)
                    : new IsolatedManifestScope();
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = "Build/revival/Tamer-ads-" + variant + ".apk",
                options = development ? BuildOptions.Development | BuildOptions.CompressWithLz4 : BuildOptions.None,
                extraScriptingDefines = nativePrivacyManager
                    ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS", "TAMER_UMP_ONLY_HARNESS", "TAMER_UMP_PUBLISHER_HARNESS", "TAMER_PRIVACY_MANAGER_HARNESS" }
                    : publisher
                    ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS", "TAMER_UMP_ONLY_HARNESS", "TAMER_UMP_PUBLISHER_HARNESS" }
                    : umpOnly
                    ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS", "TAMER_UMP_ONLY_HARNESS" }
                    : development
                        ? new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS", "TAMER_AD_SAMPLE_CLOSE_HARNESS" }
                        : new[] { "TAMER_REVIVAL_SMOKE", "TAMER_AD_TEST_HARNESS" }
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Ad harness build failed.");
            Debug.Log("AD_HARNESS_BUILD_OK variant=" + variant);
            exitCode = 0;
        }
        catch (Exception error)
        {
            if (publisher) Debug.LogError("Publisher UMP build failed; private details suppressed.");
            else Debug.LogException(error);
        }
        finally
        {
            if (isolatedManifest != null)
            {
                try { isolatedManifest.Dispose(); Debug.Log("UMP_PGS_MANIFEST_RESTORED"); }
                catch { exitCode = 1; Debug.LogError("UMP_PGS_MANIFEST_RESTORE_FAILED"); }
            }
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.Android.useCustomKeystore = oldKey;
            PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            AssetDatabase.SaveAssets();
            File.WriteAllBytes(SettingsPath, settingsBytes);
            File.WriteAllBytes(ManifestPath, manifestBytes);
            if (publisherSceneBytes != null)
            {
                File.WriteAllBytes(ScenePath, publisherSceneBytes);
                File.WriteAllBytes(ScenePath + ".meta", publisherSceneMetaBytes);
            }
            AssetDatabase.ImportAsset(SettingsPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate);
            if (!File.ReadAllBytes(SettingsPath).SequenceEqual(settingsBytes) || !File.ReadAllBytes(ManifestPath).SequenceEqual(manifestBytes))
                throw new BuildFailedException("Sample identity restoration failed.");
            if (publisherSceneBytes != null && (!File.ReadAllBytes(ScenePath).SequenceEqual(publisherSceneBytes) ||
                !File.ReadAllBytes(ScenePath + ".meta").SequenceEqual(publisherSceneMetaBytes)))
                throw new BuildFailedException("Publisher harness scene restoration failed.");
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
    [Serializable] private class PublisherConfig
    {
        public string checkout, androidAppId;
        public bool productionActivationApproved, regionalReviewApproved;
    }
}

// Only the build copy is changed. The original smoke scene remains behaviour-free.
public sealed class RevivalPrivacyUiBuildScene : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || !BuildPipeline.isBuildingPlayer ||
            PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.AeDeong.MonsterTamer.revival.privacyui") return;
        if (!RevivalAdHarnessBuild.PrivacyUiBuildActive || scene.path != RevivalBuild.SmokeScene ||
            (report.summary.options & BuildOptions.Development) == 0)
            throw new BuildFailedException("Privacy UI requires only the isolated development smoke scene.");
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/DungGeunMo SDF.asset");
        if (font == null || font.material == null || font.atlasTextures == null || font.atlasTextures.Length == 0 ||
            font.atlasTextures.Any(texture => texture == null) || font.sourceFontFile == null ||
            AssetDatabase.AssetPathToGUID("Assets/Fonts/DungGeunMo SDF.asset") != "6b62d0bbc19501141b40b8d32a134954")
            throw new BuildFailedException("Existing Korean font, material, atlas and source are required.");
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new BuildFailedException("Unexpected original smoke behaviour.");
        var created = new GameObject("Offline actual privacy UI fixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, scene);
        created.AddComponent<RevivalAdHarness>().PrivacyUiFont = font;
        Debug.Log("PRIVACY_UI_BUILD_SCENE_READY font_guid_preserved=true original_scene_saved=false");
    }
}
