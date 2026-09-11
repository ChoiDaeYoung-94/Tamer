using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class RevivalSdkValidation : IPreprocessBuildWithReport
{
    const string SettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    public int callbackOrder => -100;

    public static void Configure()
    {
        var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
        settings.FindProperty("overrideDefaultGmaAndroidSdk").boolValue = true;
        settings.FindProperty("selectedGmaAndroidSdk").intValue = 0;
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("REVIVAL_SDK_CONFIGURATION_OK");
    }

    public static void Validate()
    {
        var settings = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(SettingsPath));
        if (!settings.FindProperty("overrideDefaultGmaAndroidSdk").boolValue || settings.FindProperty("selectedGmaAndroidSdk").intValue != 0)
            throw new BuildFailedException("This SDK baseline requires explicitly pinned GMA Standard (Families-certified Android artifact). Run RevivalSdkValidation.Configure.");
        string dependencies = File.ReadAllText("Assets/GoogleMobileAds/Editor/GoogleMobileAdsDependencies.xml");
        if (!dependencies.Contains("com.google.android.gms:play-services-ads:25.4.0") || dependencies.Contains("ads-mobile-sdk:"))
            throw new BuildFailedException("Expected GMA Standard play-services-ads:25.4.0 dependency.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android) Validate();
    }
}
