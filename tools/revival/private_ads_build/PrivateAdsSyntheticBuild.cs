// Synthetic-only Editor template. The external owner snapshots files before invocation.
using System;
using System.IO;
using System.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrivateAdsSyntheticBuild
{
    private const string SettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    public static object Run()
    {
        if (!Application.isBatchMode || Application.unityVersion != "6000.3.25f1" ||
            EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw Rejected();
        var blockedNames = new[] { "TAMER_KEYSTORE_PASS", "TAMER_KEYALIAS_PASS", "TAMER_UPLOAD_KEYSTORE_PATH",
            "TAMER_UPLOAD_KEY_ALIAS", "TAMER_UPLOAD_CERT_SHA256", "TAMER_IAP_TEST_KEY_PASSWORD" };
        if (Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(key =>
            key.StartsWith("TAMER_PRIVATE_ADS_", StringComparison.OrdinalIgnoreCase) ||
            blockedNames.Contains(key, StringComparer.OrdinalIgnoreCase))) throw Rejected();
        var fixture = PrivateAdsSyntheticCallbacks.Fixture;
        if (File.Exists(fixture) || File.Exists(fixture + ".meta") ||
            !Directory.Exists(Path.GetDirectoryName(fixture))) throw Rejected();
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/revival/privateads-synthetic.aab"));
        if (File.Exists(output) || !Directory.Exists(Path.GetDirectoryName(output))) throw Rejected();
        var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
        var app = settings.FindProperty("adMobAndroidAppId");
        if (app == null) throw Rejected();
        var oldApp = app.stringValue;
        var oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        var oldScenes = EditorBuildSettings.scenes;
        var oldSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        int initialSceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        int initialSetupCount = oldSceneSetup == null ? -1 : oldSceneSetup.Length;
        int initialLoadedCount = oldSceneSetup == null ? -1 : oldSceneSetup.Count(item => item.isLoaded);
        int initialActiveCount = oldSceneSetup == null ? -1 : oldSceneSetup.Count(item => item.isActive);
        Debug.Log(string.Format("Synthetic scene baseline counts: setup={0}, scene={1}, loaded={2}, active={3}.",
            initialSetupCount, initialSceneCount, initialLoadedCount, initialActiveCount));
        bool hadNoScenes = oldSceneSetup != null && oldSceneSetup.Length == 0 &&
            initialSceneCount == 0;
        if (oldSceneSetup == null || (!hadNoScenes &&
            (!oldSceneSetup.Any(item => item.isLoaded) || oldSceneSetup.Count(item => item.isActive) != 1 ||
             oldSceneSetup.Any(item => item.isActive && !item.isLoaded)))) throw Rejected();
        if (hadNoScenes) Debug.Log("Synthetic scene baseline empty; restoration deferred to batch exit.");
        foreach (var scene in oldSceneSetup)
            if (string.IsNullOrEmpty(scene.path)) throw Rejected();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw Rejected();
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        bool oldDevelopment = EditorUserBuildSettings.development;
        bool oldExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        var oldArchitecture = PlayerSettings.Android.targetArchitectures;
        object result = null;
        string firstRestoreFailure = null;
        string phase = "S10";
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("InactiveSyntheticFixture");
            root.SetActive(false); // Must precede AddComponent: never run manager Awake in the fixture.
            root.AddComponent<GoogleAdMobManager>();
            if (!EditorSceneManager.SaveScene(scene, fixture, false)) throw Rejected();
            phase = "S20";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PrivateAdsSyntheticCallbacks.Package);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(fixture, true) };
            app.stringValue = PrivateAdsSyntheticCallbacks.TestApp;
            settings.ApplyModifiedPropertiesWithoutUndo();
            // Only this settings asset is saved; no global SaveAssets.
            AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            phase = "S30";
            PrivateAdsSyntheticCallbacks.Prepare(output);
            phase = "S40";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { fixture }, target = BuildTarget.Android, locationPathName = output,
                options = BuildOptions.None, extraScriptingDefines = new string[0] });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw Rejected();
            phase = "S50";
            result = PrivateAdsSyntheticCallbacks.ReadResult();
        }
        catch (Exception error)
        {
            var allowed = new[] { "Synthetic diagnostic C01.", "Synthetic diagnostic C02.",
                "Synthetic diagnostic C03.", "Synthetic diagnostic C04.", "Synthetic diagnostic C05." };
            // Emit only fixed literals; never propagate exception messages, paths or IDs.
            throw new BuildFailedException(allowed.Contains(error.Message) ? error.Message : "Synthetic diagnostic " + phase + ".");
        }
        finally
        {
            PrivateAdsSyntheticCallbacks.Reset();
            Action<string, Action> restore = (reason, action) => { try { action(); } catch { if (firstRestoreFailure == null) firstRestoreFailure = reason; } };
            restore("R01", () => PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId));
            restore("R02", () => PlayerSettings.Android.useCustomKeystore = oldKey);
            restore("R03", () => PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend));
            restore("R04", () => PlayerSettings.Android.targetArchitectures = oldArchitecture);
            restore("R05", () => EditorUserBuildSettings.development = oldDevelopment);
            restore("R06", () => EditorUserBuildSettings.buildAppBundle = oldBundle);
            restore("R07", () => EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport);
            restore("R08", () => EditorBuildSettings.scenes = oldScenes);
            restore("R09", () => { settings.Update(); settings.FindProperty("adMobAndroidAppId").stringValue = oldApp;
                settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(settings.targetObject); });
            // A batch process starting with zero scenes has no valid scene setup to restore.
            if (!hadNoScenes) restore("R10", () => EditorSceneManager.RestoreSceneManagerSetup(oldSceneSetup));
            if (firstRestoreFailure != null) throw new BuildFailedException("Synthetic diagnostic " + firstRestoreFailure + ".");
        }
        return result;
    }
    private static Exception Rejected() { return new BuildFailedException("Synthetic build rejected; output not distributable."); }
}
