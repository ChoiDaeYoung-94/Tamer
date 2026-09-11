using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public class RevivalSdkMigrationTests
{
    const string SettingsAsset = "Assets/GooglePlayGames/Resources/PlayGamesSettings.asset";

    static Type FindType(string name)
    {
        var result = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
        Assert.That(result, Is.Not.Null, "Required SDK type is missing: " + name);
        return result;
    }

    [Test]
    public void Revival_GmaStandardArtifactIsExplicitlyPinned()
    {
        FindType("RevivalSdkValidation").GetMethod("Validate").Invoke(null, null);
    }

    [Test]
    public void Revival_GmaNextGenSelectionFailsBuildPreflight()
    {
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
        var settings = new SerializedObject(asset);
        var selection = settings.FindProperty("selectedGmaAndroidSdk");
        int before = selection.intValue;
        try
        {
            selection.intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var error = Assert.Throws<TargetInvocationException>(() =>
                FindType("RevivalSdkValidation").GetMethod("Validate").Invoke(null, null));
            Assert.That(error.InnerException, Is.TypeOf<UnityEditor.Build.BuildFailedException>());
        }
        finally
        {
            selection.intValue = before;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void EnsureMigrated()
    {
#if UNITY_ANDROID
        FindType("GooglePlayGames.Editor.GPGSUpgrader").GetMethod("EnsureMigrated").Invoke(null, null);
#else
        Assert.Ignore("GPGS migration is compiled only for the Android target.");
#endif
    }

    [Test]
    public void Revival_GpgsMigrationPreservesConfiguredValuesAndRuntimeShim()
    {
        EnsureMigrated();
        var projectSettingsType = FindType("GooglePlayGames.Editor.GPGSProjectSettings");
        var projectSettings = projectSettingsType.GetProperty("Instance").GetValue(null);
        var getSetting = projectSettingsType.GetMethod("Get", new[] { typeof(string) });
        var utilType = FindType("GooglePlayGames.Editor.GPGSUtil");
        var settingsType = FindType("GooglePlayGames.PlayGamesSettings");
        var settings = settingsType.GetMethod("LoadInstance").Invoke(null, null);
        Assert.That(settings, Is.Not.Null, "Configured settings must be created by the Android importer.");
        var shimType = FindType("GooglePlayGames.GameInfo");

        var fieldMap = new[] {
            new[] { "APPIDKEY", "AppId", "ApplicationId" },
            new[] { "WEBCLIENTIDKEY", "WebClientId", "WebClientId" },
            new[] { "SERVICEIDKEY", "NearbyServiceId", "NearbyConnectionServiceId" }
        };
        foreach (var fields in fieldMap)
        {
            var key = (string)utilType.GetField(fields[0]).GetValue(null);
            var configured = (string)getSetting.Invoke(projectSettings, new object[] { key });
            var migrated = (string)settingsType.GetProperty(fields[1]).GetValue(settings);
            var compatibility = (string)shimType.GetProperty(fields[2]).GetValue(null);
            // Assert only booleans so failures cannot print service configuration values.
            Assert.That(string.Equals(configured, migrated, StringComparison.Ordinal), Is.True,
                fields[1] + " changed during migration.");
            Assert.That(string.Equals(migrated, compatibility, StringComparison.Ordinal), Is.True,
                fields[1] + " differs through the legacy API.");
        }
    }

    [Test]
    public void Revival_GpgsMigrationIsIdempotentAndPreservesAssetGuid()
    {
        EnsureMigrated();
        Assert.That(File.Exists(SettingsAsset), Is.True, "The configured Android settings asset was not generated.");
        var bytesBefore = File.ReadAllBytes(SettingsAsset);
        var guidBefore = AssetDatabase.AssetPathToGUID(SettingsAsset);
        Assert.That(string.IsNullOrEmpty(guidBefore), Is.False, "The migrated settings must have an asset GUID.");
        EnsureMigrated();
        EnsureMigrated();
        Assert.That(bytesBefore.SequenceEqual(File.ReadAllBytes(SettingsAsset)), Is.True,
            "Repeated migration must not rewrite service settings.");
        Assert.That(string.Equals(guidBefore, AssetDatabase.AssetPathToGUID(SettingsAsset), StringComparison.Ordinal), Is.True,
            "Repeated migration must preserve the settings GUID.");
    }
}
