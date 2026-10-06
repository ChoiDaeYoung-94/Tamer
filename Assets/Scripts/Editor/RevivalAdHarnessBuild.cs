using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

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

    private sealed class IsolatedManifestScope : IDisposable
    {
        private const string PathName = "Assets/Plugins/Android/AndroidManifest.xml";
        private FileStream manifest, meta;
        private byte[] original, originalMeta;
        private string guid;

        public IsolatedManifestScope()
        {
            try
            {
                // No delete sharing: retain each original file object through import/build/restoration.
                manifest = new FileStream(PathName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                meta = new FileStream(PathName + ".meta", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                original = ReadBytes(manifest);
                originalMeta = ReadBytes(meta);
                guid = AssetDatabase.AssetPathToGUID(PathName);
                if (string.IsNullOrEmpty(guid)) throw new BuildFailedException("Manifest GUID missing.");
                string transformed = IsolatedUmpManifest(System.Text.Encoding.UTF8.GetString(original));
                WriteBytes(manifest, System.Text.Encoding.UTF8.GetBytes(transformed));
                AssetDatabase.ImportAsset(PathName, ImportAssetOptions.ForceUpdate);
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

        private static void WriteBytes(FileStream stream, byte[] bytes)
        {
            stream.Position = 0; stream.Write(bytes, 0, bytes.Length); stream.SetLength(bytes.Length); stream.Flush(true);
        }

        private void Restore()
        {
            if (original != null) { WriteBytes(manifest, original); AssetDatabase.ImportAsset(PathName, ImportAssetOptions.ForceUpdate); }
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

    private static void Build(bool development, bool umpOnly = false, bool publisher = false, bool privacyAge = false, bool isolatePgs = false)
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
        string applicationId = publisher ? "com.AeDeong.MonsterTamer.revival.umppublisher" : umpOnly ? "com.AeDeong.MonsterTamer.revival.ump" :
            development ? "com.AeDeong.MonsterTamer.revival.ads" : "com.AeDeong.MonsterTamer.revival.adscontrol";
        try
        {
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
                    PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.AeDeong.MonsterTamer.revival.umppublisher")
                    throw new BuildFailedException("Play Games isolation requires the explicit UMP privacy debug variant.");
                isolatedManifest = new IsolatedManifestScope();
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = "Build/revival/Tamer-ads-" + variant + ".apk",
                options = development ? BuildOptions.Development | BuildOptions.CompressWithLz4 : BuildOptions.None,
                extraScriptingDefines = publisher
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
