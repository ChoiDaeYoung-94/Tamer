using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PlayFab;

public class RevivalDeletionIntakeTests
{
    private static Type DataType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD.DataManager")).First(t => t != null);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    [TestCase("synthetic-a", true)]
    [TestCase("synthetic-other", false)]
    [TestCase("", false)]
    public void Revival_DeletionIntakeCleanupRequiresDiskOwnerAndPreservesEntitlements(string diskOwner, bool removes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tamer-deletion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "PlayerData.json");
        var original = "{\"Gold\":\"123\",\"GooglePlay\":\"ProductNoAds\"" +
            (diskOwner == "" ? "" : ",\"__TamerAccountOwner\":\"" + diskOwner + "\"") + "}";
        File.WriteAllText(path, original);
        var root = new GameObject("Deletion intake isolated owner");
        root.SetActive(false);
        var credentials = new PlayFabAuthenticationContext();
        credentials.CopyFrom(PlayFabSettings.staticPlayer);
        const string key = "AD_DeletionAcceptedNeedsLogin";
        bool hadPause = PlayerPrefs.HasKey(key);
        int pause = PlayerPrefs.GetInt(key);
        try
        {
            PlayFabSettings.staticPlayer.PlayFabId = "synthetic-a";
            var data = root.AddComponent(DataType);
            Set(data, "_playerDataPath", path);
            Set(data, "_localOwner", diskOwner);
            Set(data, "_defaults", new Dictionary<string, string> { ["Gold"] = "0", ["GooglePlay"] = "" });
            DataType.GetProperty("PlayFabId").SetValue(data, "synthetic-a");
            var session = Call(data, "DeletionSession");
            Call(data, "BeginDeletionSubmission", session);
            Assert.That((bool)DataType.GetProperty("DeletionInProgress").GetValue(data), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo(original), "In-flight/unknown never cleans data");
            Assert.Throws<TargetInvocationException>(() => Call(data, "BeginAccountSession", "synthetic-other"));
            Call(data, "FinishAcceptedDeletion", session);
            Assert.That(DataType.GetProperty("PlayFabId").GetValue(data), Is.EqualTo(""));
            Assert.That((bool)DataType.GetProperty("DeletionInProgress").GetValue(data), Is.False);
            Assert.That(File.Exists(path), Is.EqualTo(!removes));
            var evidence = Directory.GetFiles(directory, "*.deletion-entitlement-*");
            Assert.That(evidence.Length, Is.EqualTo(removes ? 1 : 0));
            if (removes)
            {
                Assert.That(File.ReadAllText(evidence[0]), Does.Contain("ProductNoAds"));
                Assert.That(File.ReadAllText(evidence[0]), Does.Not.Contain("Gold"));
            }
            else Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            Assert.Throws<TargetInvocationException>(() => Call(data, "FinishAcceptedDeletion", session));
            if (diskOwner == "synthetic-other")
                Assert.Throws<TargetInvocationException>(() => Call(data, "BeginAccountSession", "synthetic-a"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            PlayFabSettings.staticPlayer.CopyFrom(credentials);
            if (hadPause) PlayerPrefs.SetInt(key, pause); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Directory.Delete(directory, true);
        }
    }
}
