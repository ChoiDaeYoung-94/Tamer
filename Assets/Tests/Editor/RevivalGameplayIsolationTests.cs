using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RevivalGameplayIsolationTests
{
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    [Test]
    public void Revival_GameplaySavePathCannotReuseExistingPlayerSave()
    {
        string root = Path.Combine(Path.GetTempPath(), "RevivalGameplayTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string existing = Path.Combine(root, "PlayerData.json");
            File.WriteAllText(existing, "preserved original");
            var factory = Runtime("AD.RevivalGameplayIsolation").GetMethod("CreateSavePath");
            string first = (string)factory.Invoke(null, new object[] { root });
            string second = (string)factory.Invoke(null, new object[] { root });
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Does.StartWith(Path.Combine(root, "RevivalGameplay") + Path.DirectorySeparatorChar));
            Assert.That(File.Exists(first), Is.False);
            Assert.That(File.ReadAllText(existing), Is.EqualTo("preserved original"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public void Revival_GameplayMemoryTransportCopiesDataAndRejectsAnotherAccount()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        object server = null;
        try
        {
            var go = new GameObject("Offline transport fixture");
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, scene);
            var dataType = Runtime("AD.DataManager");
            var data = go.AddComponent(dataType);
            dataType.GetProperty("PlayFabId").GetSetMethod(true).Invoke(data, new object[] { "revival-offline-gameplay" });
            dataType.GetProperty("IsServerDataReady").GetSetMethod(true).Invoke(data, new object[] { true });
            server = Runtime("AD.RevivalGameplayIsolation").GetMethod("CreateServer").Invoke(null, new object[] { data });
            var type = server.GetType();
            type.GetMethod("GetAllData").Invoke(server, new object[] { false });
            var records = (IDictionary)dataType.GetField("PlayFabPlayerData").GetValue(data);
            Assert.That(records.Contains("Sex"), Is.True);
            records.Clear();
            type.GetMethod("GetAllData").Invoke(server, new object[] { false });
            Assert.That(((IDictionary)dataType.GetField("PlayFabPlayerData").GetValue(data)).Contains("Sex"), Is.True);
            dataType.GetProperty("PlayFabId").GetSetMethod(true).Invoke(data, new object[] { "not-the-offline-account" });
            type.GetMethod("GetAllData").Invoke(server, new object[] { false });
            Assert.That(type.GetProperty("HasFailed").GetValue(server), Is.True);
        }
        finally
        {
            (server as IDisposable)?.Dispose();
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void Revival_GameplayManifestRemovesNetworkBillingAndAdProvider()
    {
        const string original = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><uses-permission android:name='android.permission.INTERNET'/><application><activity android:name='kept'/></application></manifest>";
        string result = (string)Runtime("RevivalGameplayBuild").GetMethod("OfflineManifest").Invoke(null, new object[] { original });
        var document = XDocument.Parse(result);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var permissions = document.Root.Elements("uses-permission").ToArray();
        Assert.That(permissions.Length, Is.EqualTo(4));
        Assert.That(permissions.All(p => (string)p.Attribute(tools + "node") == "remove"), Is.True);
        Assert.That(document.Root.Element("application").Element("activity").Attribute(android + "name").Value, Is.EqualTo("kept"));
        Assert.That(document.Root.Element("application").Element("provider").Attribute(tools + "node").Value, Is.EqualTo("remove"));
    }
}
