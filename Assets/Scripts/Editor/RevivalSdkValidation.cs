using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        settings.FindProperty("enableExternalDependencyManager").boolValue = false;
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
        if (settings.FindProperty("enableExternalDependencyManager").boolValue)
            throw new BuildFailedException("This SDK baseline retains the existing Google EDM. Disable Unity EDM automatic installation.");
        string dependencies = File.ReadAllText("Assets/GoogleMobileAds/Editor/GoogleMobileAdsDependencies.xml");
        if (!dependencies.Contains("com.google.android.gms:play-services-ads:25.5.0") || dependencies.Contains("ads-mobile-sdk:"))
            throw new BuildFailedException("Expected GMA Standard play-services-ads:25.5.0 dependency.");
    }

    // Pinned to the web client shared by the Play Games game server and the PlayFab Google add-on.
    const string ServerClientPinPath = "ProjectSettings/TamerPlayGamesServerClient.sha256";

    internal static bool MatchesServerClient(string clientId, string expectedSha256)
    {
        if (string.IsNullOrEmpty(clientId)) return false;
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(clientId));
            return string.Equals(BitConverter.ToString(hash).Replace("-", ""), expectedSha256,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        Validate();
        if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.AeDeong.MonsterTamer") return;

        var settings = Resources.LoadAll<GooglePlayGames.PlayGamesSettings>("PlayGamesSettings");
        string expected = File.ReadAllText(ServerClientPinPath).Trim();
        if (settings.Length != 1 || !MatchesServerClient(settings[0].WebClientId, expected))
            throw new BuildFailedException("Restore the verified Play Games server OAuth settings before building Monster Tamer.");
    }
}
