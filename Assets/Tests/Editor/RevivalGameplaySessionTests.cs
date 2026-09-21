using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RevivalGameplaySessionTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Fields).SetValue(obj, value);
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Fields).Invoke(obj, args);

    [TestCase("manager")]
    [TestCase("owner")]
    [TestCase("generation")]
    [TestCase("not-ready")]
    [TestCase("pool-reuse")]
    public void Revival_LeaseRejectsStaleRewardAndAllowsCurrentActivation(string change)
    {
        var lease = Activator.CreateInstance(Runtime("AD.GameplaySessionLease"));
        var manager = new object();
        Call(lease, "Bind", manager, "A", 1, true);
        int lifetime = (int)lease.GetType().GetProperty("Lifetime").GetValue(lease);
        if (change == "pool-reuse") Call(lease, "Bind", manager, "A", 1, true);
        Assert.That(Call(lease, "TryConsumeReward", change == "manager" ? new object() : manager,
            change == "owner" ? "B" : "A", change == "generation" ? 2 : 1, change != "not-ready", lifetime), Is.False);
        int current = (int)lease.GetType().GetProperty("Lifetime").GetValue(lease);
        Assert.That(Call(lease, "TryConsumeReward", manager, "A", 1, true, current), Is.True);
        Assert.That(Call(lease, "TryConsumeReward", manager, "A", 1, true, current), Is.False);
    }

    [Test]
    public void Revival_CurrentOffTargetDeathRewardsOnceAndPersistsGold()
    {
        using (var f = new Fixture())
        {
            // No selected target: an ally's valid kill must still reward the player.
            Call(f.Player, "NotifyPlayerOfDeath", f.Monster.gameObject, 25, f.Lifetime);
            Call(f.Player, "NotifyPlayerOfDeath", f.Monster.gameObject, 25, f.Lifetime);
            Assert.That(Get(f.Player, "_gold"), Is.EqualTo(125));
            Assert.That(f.Values["Gold"], Is.EqualTo("125"));
            Assert.That(File.Exists(f.SavePath), Is.True);
            Assert.That((IEnumerable)Get(f.Player, "PlayerMonsterCollection"), Is.Empty);
        }
    }

    [TestCase("generation")]
    [TestCase("manager")]
    [TestCase("not-ready")]
    [TestCase("deletion")]
    [TestCase("pool-reuse")]
    public void Revival_StaleMonsterCannotMutateGoldOrAllies(string change)
    {
        using (var f = new Fixture())
        {
            ((IList)Get(f.Player, "_allyMonsters")).Add(f.Monster);
            if (change == "generation") Set(f.Data, "_accountGeneration", 2);
            if (change == "not-ready") Set(f.Data, "<IsServerDataReady>k__BackingField", false);
            if (change == "deletion") Set(f.Data, "<DeletionInProgress>k__BackingField", true);
            if (change == "manager") Set(f.Managers, "_dataM", f.NewData());
            if (change == "pool-reuse") Call(Get(f.Monster, "_session"), "Invalidate");
            Call(f.Player, "NotifyPlayerOfDeath", f.Monster.gameObject, 25, f.Lifetime);
            Call(f.Player, "RemoveAllyMonster", f.Monster);
            Set(f.Player, "_ableCaptureMonster", f.Monster);
            Set(f.Player, "_captureLifetime", f.Lifetime);
            Call(f.Player, "Capture");
            Assert.That(Get(f.Player, "_gold"), Is.EqualTo(100));
            Assert.That(f.Values["Gold"], Is.EqualTo("100"));
            Assert.That(f.Values["AllyMonsters"], Is.EqualTo("null"));
            Assert.That(File.Exists(f.SavePath), Is.False);
        }
    }

    [Test]
    public void Revival_UnknownCurrentAllyDeathDoesNotRewriteRoster()
    {
        using (var f = new Fixture())
        {
            Call(f.Player, "RemoveAllyMonster", f.Monster);
            Assert.That(File.Exists(f.SavePath), Is.False);
            Assert.That(f.Values["AllyMonsters"], Is.EqualTo("null"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_ReadySessionReloadsGoldAndRetiresOldAllies(bool replaceManager)
    {
        using (var f = new Fixture())
        {
            ((IList)Get(f.Player, "_allyMonsters")).Add(f.Monster);
            if (replaceManager) { f.Data = f.NewData(); Set(f.Managers, "_dataM", f.Data); }
            else Set(f.Data, "_accountGeneration", 2);
            f.Values["Gold"] = "700";
            Call(f.Player, "RefreshInventorySession");
            Assert.That(Get(f.Player, "_gold"), Is.EqualTo(700));
            Assert.That((IEnumerable)Get(f.Player, "_allyMonsters"), Is.Empty);
            Assert.That(Call(f.Monster, "IsCurrentSession", f.Lifetime), Is.False);
            Call(f.Player, "NotifyPlayerOfDeath", f.Monster.gameObject, 25, f.Lifetime);
            Assert.That(Get(f.Player, "_gold"), Is.EqualTo(700));
            Assert.That(f.Values["Gold"], Is.EqualTo("700"));
            Assert.That(File.Exists(f.SavePath), Is.False);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
        private readonly GameObject _root;
        private readonly FieldInfo _managerInstance = Runtime("AD.Managers").GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object _oldManager;
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "TamerSession-" + Guid.NewGuid().ToString("N"));
        public Component Managers, Data, Player, Monster;
        public string SavePath => Path.Combine(_directory, "PlayerData.json");
        public Dictionary<string, string> Values => (Dictionary<string, string>)Get(Data, "LocalPlayerData");
        public int Lifetime;
        public Fixture()
        {
            _oldManager = _managerInstance.GetValue(null);
            Directory.CreateDirectory(_directory);
            _root = new GameObject("Offline gameplay session fixture");
            _root.SetActive(false); // Avoid Awake, authentication, services and gameplay loops.
            SceneManager.MoveGameObjectToScene(_root, _scene);
            Managers = _root.AddComponent(Runtime("AD.Managers"));
            Data = NewData(); Set(Managers, "_dataM", Data); _managerInstance.SetValue(null, Managers);
            Player = _root.AddComponent(Runtime("Player"));
            Monster = _root.AddComponent(Runtime("Monster"));
            Set(Player, "_gameplayData", Data); Set(Player, "_inventoryOwner", "A"); Set(Player, "_inventoryGeneration", 1); Set(Player, "_gold", 100);
            var lease = Get(Monster, "_session"); Call(lease, "Bind", Data, "A", 1, true);
            Lifetime = (int)lease.GetType().GetProperty("Lifetime").GetValue(lease);
        }
        public Component NewData()
        {
            var data = _root.AddComponent(Runtime("AD.DataManager"));
            Set(data, "<PlayFabId>k__BackingField", "A"); Set(data, "<IsServerDataReady>k__BackingField", true);
            Set(data, "_accountGeneration", 1); Set(data, "_localOwner", "A"); Set(data, "_playerDataPath", SavePath);
            Set(data, "LocalPlayerData", new Dictionary<string, string> { { "Gold", "100" }, { "AllyMonsters", "null" } });
            Set(data, "MonsterData", new Dictionary<string, object> { { "Bat", null } });
            Set(data, "ItemData", new Dictionary<string, object> { { "SimpleSword", null }, { "MasterSword", null }, { "SimpleShield", null }, { "MasterShield", null } });
            return data;
        }
        public void Dispose()
        {
            _managerInstance.SetValue(null, _oldManager);
            EditorSceneManager.ClosePreviewScene(_scene);
            Directory.Delete(_directory, true);
        }
    }
}
