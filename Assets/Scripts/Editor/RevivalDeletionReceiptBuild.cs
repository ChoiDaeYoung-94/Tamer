using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RevivalDeletionReceiptBuild
{
    public static void BuildAndroid()
    {
        const string manifest = "Assets/Plugins/Android/AndroidManifest.xml";
        const string ads = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        const string adsManifest = "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml";
        byte[] oldManifest = File.ReadAllBytes(manifest), oldAds = File.ReadAllBytes(ads), oldAdsManifest = File.ReadAllBytes(adsManifest);
        string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), oldAlias = PlayerSettings.Android.keyaliasName;
        bool oldKey = PlayerSettings.Android.useCustomKeystore, oldBundle = EditorUserBuildSettings.buildAppBundle;
        var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        int code = 1;
        try
        {
            RevivalBuild.ValidateBaseline(); RevivalBuild.PrepareSmokeScene();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new BuildFailedException("Android target required.");
            File.WriteAllText(manifest, RevivalGameplayBuild.OfflineManifest(File.ReadAllText(manifest)));
            var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(ads));
            settings.FindProperty("adMobAndroidAppId").stringValue = RevivalAdHarnessBuild.SampleAppId;
            settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, RevivalDeletionReceiptHarness.ApplicationId);
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Build/revival");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { RevivalBuild.SmokeScene },
                target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, locationPathName = "Build/revival/Tamer-receipt.apk",
                options = BuildOptions.Development | BuildOptions.CompressWithLz4,
                extraScriptingDefines = new[] { "TAMER_REVIVAL_SMOKE", "TAMER_RECEIPT_HARNESS" } });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Receipt harness build failed.");
            Debug.Log("RECEIPT_HARNESS_BUILD_OK offline=true isolated-smoke-only=true"); code = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
            PlayerSettings.Android.useCustomKeystore = oldKey; PlayerSettings.Android.keyaliasName = oldAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend); EditorUserBuildSettings.buildAppBundle = oldBundle;
            AssetDatabase.SaveAssets();
            File.WriteAllBytes(manifest, oldManifest); File.WriteAllBytes(ads, oldAds); File.WriteAllBytes(adsManifest, oldAdsManifest);
            AssetDatabase.ImportAsset(manifest, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(ads, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(adsManifest, ImportAssetOptions.ForceUpdate);
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (code != 0) throw new BuildFailedException("Receipt harness failed.");
    }
}
