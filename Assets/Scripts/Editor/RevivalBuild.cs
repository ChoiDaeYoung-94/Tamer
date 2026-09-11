using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated baseline; never invokes legacy version/marker/signing automation.</summary>
public static class RevivalBuild
{
    public const string SmokeScene = "Assets/Tests/Scenes/RevivalSmoke.unity";
    public const string ApplicationId = "com.AeDeong.MonsterTamer.revival";

    public static void RequireSavedScenes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            // A new batch Editor can start with an empty, untouched default scene.
            bool emptyBatchScene = Application.isBatchMode && !scene.isDirty && scene.rootCount == 0;
            if (scene.isDirty || (string.IsNullOrEmpty(scene.path) && !emptyBatchScene))
                throw new InvalidOperationException("Save all open scenes before baseline scene operations.");
        }
    }

    public static void PrepareSmokeScene()
    {
        RequireSavedScenes();
        Directory.CreateDirectory(Path.GetDirectoryName(SmokeScene));
        if (!File.Exists(SmokeScene))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Revival baseline - isolated";
            cube.transform.position = Vector3.zero;
            if (!EditorSceneManager.SaveScene(scene, SmokeScene))
                throw new InvalidOperationException("Smoke scene save failed.");
        }
        VerifySmokeScene();
    }

    public static void VerifySmokeScene()
    {
        RequireSavedScenes();
        var guid = AssetDatabase.AssetPathToGUID(SmokeScene);
        if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Missing smoke scene GUID.");
        var scene = EditorSceneManager.OpenScene(SmokeScene, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                throw new InvalidOperationException("Smoke scene must contain no gameplay behaviours or missing scripts.");
        if (scene.GetRootGameObjects().Length < 3) throw new InvalidOperationException("Incomplete smoke scene.");
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.OpenScene(SmokeScene, OpenSceneMode.Single);
        if (AssetDatabase.AssetPathToGUID(SmokeScene) != guid) throw new InvalidOperationException("Scene GUID changed.");
        Debug.Log("REVIVAL_SCENE_ROUNDTRIP_OK");
    }

    public static void ValidateBaseline()
    {
        if (Application.unityVersion != "6000.0.81f1") throw new BuildFailedException("Expected Unity 6000.0.81f1.");
        foreach (var file in new[] { "Build/AOSSettingAPK.txt", "Build/AOSSettingAAB.txt", "Build/checkedBuilding.txt" })
            if (File.Exists(file)) throw new BuildFailedException("Legacy build marker exists: " + file);
        if (PlayerSettings.bundleVersion != "1.0.5" || PlayerSettings.Android.bundleVersionCode != 26)
            throw new BuildFailedException("Expected unchanged version 1.0.5/code26.");
        if ((int)PlayerSettings.Android.minSdkVersion != 24 || (int)PlayerSettings.Android.targetSdkVersion != 36
            || PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            throw new BuildFailedException("Expected min24/target36/ARM64.");
    }

    public static void BuildAndroidDevelopment()
    {
        int code = 1;
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool oldKey = PlayerSettings.Android.useCustomKeystore;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        try
        {
            Directory.CreateDirectory("Logs/revival");
            File.WriteAllText("Logs/revival/build-summary.json", "{\"result\":\"Started\"}");
            ValidateBaseline();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Launch with -buildTarget Android.");
            PrepareSmokeScene();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            var scenes = new[] { SmokeScene }.Concat(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)).Distinct().ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = "Build/revival/Tamer-development.apk",
                options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                extraScriptingDefines = new[] { "TAMER_REVIVAL_SMOKE", "TAMER_TEST_ADS" }
            });
            Directory.CreateDirectory("Logs/revival");
            File.WriteAllText("Logs/revival/build-summary.json", JsonUtility.ToJson(new Summary {
                result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                bytes = report.summary.totalSize.ToString(), unity = Application.unityVersion,
                applicationId = ApplicationId, version = PlayerSettings.bundleVersion,
                versionCode = PlayerSettings.Android.bundleVersionCode, scenes = scenes
            }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Baseline build failed.");
            code = 0;
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Directory.CreateDirectory("Logs/revival");
            File.WriteAllText("Logs/revival/build-summary.json", "{\"result\":\"Failed\"}");
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.Android.useCustomKeystore = oldKey;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            AssetDatabase.SaveAssets();
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Baseline build failed; inspect Editor log.");
    }

    [Serializable] private class Summary
    {
        public string result, bytes, unity, applicationId, version;
        public int errors;
        public int versionCode;
        public string[] scenes;
    }
}
