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
        bool restoreFailed = false;
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("InactiveSyntheticFixture");
            root.SetActive(false); // Must precede AddComponent: never run manager Awake in the fixture.
            root.AddComponent<GoogleAdMobManager>();
            if (!EditorSceneManager.SaveScene(scene, fixture, false)) throw Rejected();
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
            PrivateAdsSyntheticCallbacks.Prepare(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { fixture }, target = BuildTarget.Android, locationPathName = output,
                options = BuildOptions.None, extraScriptingDefines = new string[0] });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw Rejected();
            result = PrivateAdsSyntheticCallbacks.ReadResult();
        }
        catch { throw Rejected(); }
        finally
        {
            PrivateAdsSyntheticCallbacks.Reset();
            Action<Action> restore = action => { try { action(); } catch { restoreFailed = true; } };
            restore(() => PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId));
            restore(() => PlayerSettings.Android.useCustomKeystore = oldKey);
            restore(() => PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend));
            restore(() => PlayerSettings.Android.targetArchitectures = oldArchitecture);
            restore(() => EditorUserBuildSettings.development = oldDevelopment);
            restore(() => EditorUserBuildSettings.buildAppBundle = oldBundle);
            restore(() => EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport);
            restore(() => EditorBuildSettings.scenes = oldScenes);
            restore(() => { settings.Update(); settings.FindProperty("adMobAndroidAppId").stringValue = oldApp;
                settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(settings.targetObject); });
            restore(() => EditorSceneManager.RestoreSceneManagerSetup(oldSceneSetup));
            if (restoreFailed) throw new BuildFailedException("Synthetic settings recovery requires owner review.");
        }
        return result;
    }
    private static Exception Rejected() { return new BuildFailedException("Synthetic build rejected; output not distributable."); }
}
