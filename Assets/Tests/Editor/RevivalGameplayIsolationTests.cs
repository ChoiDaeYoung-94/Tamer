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
    [TestCase(1)]
    [TestCase(2)]
    public void Revival_RestoredPlayerValuesReachHudAndPopup(int step)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var scene = EditorSceneManager.NewPreviewScene();
        var managersType = Runtime("AD.Managers");
        var playerType = Runtime("Player");
        var managerInstance = managersType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var playerInstance = playerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var oldManager = managerInstance.GetValue(null);
        var oldPlayer = playerInstance.GetValue(null);
        var oldCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var root = new GameObject("Offline restored HUD fixture");
            root.SetActive(false); // No Awake, services, scene transitions, or PlayerPrefs.
            SceneManager.MoveGameObjectToScene(root, scene);
            var data = root.AddComponent(Runtime("AD.DataManager"));
            var values = (System.Collections.Generic.Dictionary<string, string>)Runtime("AD.RevivalGameSaveSchema")
                .GetMethod("Fixture").Invoke(null, new object[] { step });
            data.GetType().GetField("LocalPlayerData").SetValue(data, values);
            var managers = root.AddComponent(managersType);
            managersType.GetField("_dataM", fields).SetValue(managers, data);
            managerInstance.SetValue(null, managers);
            var player = root.AddComponent(playerType);
            playerInstance.SetValue(null, player);
            playerType.GetField("_gold", fields).SetValue(player, int.Parse(values["Gold"]));
            var creature = Runtime("Creature");
            var kind = creature.GetField("CreatureType");
            kind.SetValue(player, Enum.Parse(kind.FieldType, "Player"));
            creature.GetMethod("Settings", fields).Invoke(player, null);
            var canvas = root.AddComponent(Runtime("PlayerUICanvas"));
            var labels = new System.Collections.Generic.Dictionary<string, Component>();
            foreach (var field in canvas.GetType().GetFields(fields).Where(f => f.FieldType.FullName == "TMPro.TMP_Text"))
            {
                var label = new GameObject(field.Name, typeof(RectTransform));
                label.SetActive(false);
                label.transform.SetParent(root.transform);
                var text = label.AddComponent(Runtime("TMPro.TextMeshProUGUI"));
                field.SetValue(canvas, text);
                labels.Add(field.Name, text);
            }
            var sliderObject = new GameObject("Offline HP slider", typeof(RectTransform));
            sliderObject.SetActive(false);
            sliderObject.transform.SetParent(root.transform);
            canvas.GetType().GetField("_playerHpSlider", fields).SetValue(canvas,
                sliderObject.AddComponent(Runtime("UnityEngine.UI.Slider")));
            canvas.GetType().GetMethod("DataSettings", fields).Invoke(canvas, null);
            string Text(string name) => (string)labels[name].GetType().GetProperty("text").GetValue(labels[name]);
            Assert.That(Text("_playerNickNameText"), Is.EqualTo(values["NickName"]));
            Assert.That(Text("_popupNickNameText"), Is.EqualTo("NickName - " + values["NickName"]));
            Assert.That(Text("_goldText"), Is.EqualTo("Gold - " + values["Gold"]));
            Assert.That(Text("_popupGoldText"), Is.EqualTo("Gold - " + values["Gold"]));
            foreach (string stat in new[] { "Power", "AttackSpeed", "MoveSpeed" })
                Assert.That(Text("_popup" + stat + "Text"), Is.EqualTo(stat + " - " + values[stat]));
            Assert.That(Text("_playerHpText"), Is.EqualTo("100 / 100"));
        }
        finally
        {
            managerInstance.SetValue(null, oldManager);
            playerInstance.SetValue(null, oldPlayer);
            EditorSceneManager.ClosePreviewScene(scene);
            System.Globalization.CultureInfo.CurrentCulture = oldCulture;
        }
    }

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
