// Shared build-scene operation; never saves a scene, prefab or asset.
using System;
using System.Linq;
using AD;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.SceneManagement;

internal static class PrivateAdsSceneInjection
{
    internal static int Apply(Scene scene, string targetScene, string rewardedUnit, bool privacyOnly = false)
    {
        if (!scene.IsValid() || string.IsNullOrEmpty(targetScene) ||
            (privacyOnly ? rewardedUnit != "" : string.IsNullOrEmpty(rewardedUnit)))
            throw Rejected();
        var managers = scene.GetRootGameObjects()
            .SelectMany(obj => obj.GetComponentsInChildren<GoogleAdMobManager>(true)).ToArray();
        if (scene.path != targetScene)
        {
            if (managers.Length != 0) throw Rejected();
            return 0;
        }
        if (managers.Length != 1) throw Rejected();
        var serialized = new SerializedObject(managers[0]);
        var field = serialized.FindProperty("_productionRewardedAdUnit");
        if (field == null || !string.IsNullOrEmpty(field.stringValue)) throw Rejected();
        if (privacyOnly) return 1; // Validate the manager; leave its advertising unit blank.
        field.stringValue = rewardedUnit;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.Update();
        if (serialized.FindProperty("_productionRewardedAdUnit").stringValue != rewardedUnit)
            throw Rejected();
        return 1;
    }
    private static Exception Rejected()
    { return new BuildFailedException("Private build-scene injection rejected."); }
}
