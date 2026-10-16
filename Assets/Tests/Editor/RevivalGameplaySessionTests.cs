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
    public void Revival_CancelledDeletionPreservesSameSessionMonstersAndRewards()
    {
        using (var f = new Fixture())
        {
            ((IList)Get(f.Player, "_allyMonsters")).Add(f.Monster);
            Set(f.Data, "<IsServerDataReady>k__BackingField", false);
            Set(f.Data, "<DeletionInProgress>k__BackingField", true);
            Call(f.Monster, "Update");
            Call(f.Player, "NotifyPlayerOfDeath", f.Monster.gameObject, 25, f.Lifetime);
            Assert.That(File.Exists(f.SavePath), Is.False);
            Assert.That((int)f.Monster.GetType().GetProperty("SessionLifetime").GetValue(f.Monster), Is.EqualTo(f.Lifetime));
            Set(f.Data, "<DeletionInProgress>k__BackingField", false);
            Set(f.Data, "<IsServerDataReady>k__BackingField", true);
            Assert.That(Call(f.Monster, "IsCurrentSession", f.Lifetime), Is.True);
            Assert.That((IEnumerable)Get(f.Player, "_allyMonsters"), Has.Count.EqualTo(1));
            Call(f.Player, "NotifyPlayerOfDeath", f.Monster.gameObject, 25, f.Lifetime);
            Assert.That(f.Values["Gold"], Is.EqualTo("125"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_DeferredDeathResumesOnceOrRejectsReplacement(bool replaceSession)
    {
        using (var f = new Fixture())
        {
            var creature = Runtime("Creature");
            creature.GetField("isDie", Fields).SetValue(f.Monster, true);
            creature.GetField("_hp", Fields).SetValue(f.Monster, 0f);
            Call(f.Monster, "OnDeath");
            Set(f.Data, "<IsServerDataReady>k__BackingField", false);
            Set(f.Data, "<DeletionInProgress>k__BackingField", true);
            Call(f.Monster, "AfterDie");
            Assert.That(Get(f.Monster, "_deathCallbackPending"), Is.True);
            Assert.That(Get(f.Monster, "_deathHandled"), Is.False);
            if (replaceSession) Set(f.Data, "_accountGeneration", 2);
            Set(f.Data, "<DeletionInProgress>k__BackingField", false);
            Set(f.Data, "<IsServerDataReady>k__BackingField", true);
            Assert.That(Call(f.Monster, "TryBeginDeathCallback"), Is.EqualTo(!replaceSession));
            Assert.That(Call(f.Monster, "TryBeginDeathCallback"), Is.False);
            Assert.That(Get(f.Monster, "_deathHandled"), Is.EqualTo(!replaceSession));
            Assert.That(File.Exists(f.SavePath), Is.False);
        }
    }

    [Test]
    public void Revival_UpdateResumesDeferredDeathAndPersistsRewardExactlyOnce()
    {
        using (var f = new Fixture())
        using (var cancelled = new System.Threading.CancellationTokenSource())
        {
            var playerInstance = Runtime("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var generatorInstance = Runtime("MonsterGenerator").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object oldPlayer = playerInstance.GetValue(null), oldGenerator = generatorInstance.GetValue(null);
            try
            {
                playerInstance.SetValue(null, f.Player);
                generatorInstance.SetValue(null, f.Monster.gameObject.AddComponent(Runtime("MonsterGenerator")));
                var creature = Runtime("Creature");
                creature.GetField("isDie", Fields).SetValue(f.Monster, true);
                creature.GetField("_hp", Fields).SetValue(f.Monster, 0f);
                Set(f.Monster, "NavMeshAgent", f.Monster.gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>());
                Set(f.Monster, "_rewardGold", 25); Set(f.Monster, "_isAbleAlly", true);
                var effect = new GameObject("Offline corpse effect");
                effect.transform.SetParent(f.Monster.transform); effect.SetActive(false);
                Set(f.Monster, "_captureEffect", effect);
                cancelled.Cancel(); // No background corpse timer in this synchronous fixture.
                Set(f.Monster, "_afterDieTokenSource", cancelled);
                Call(f.Monster, "OnDeath");
                Set(f.Data, "<IsServerDataReady>k__BackingField", false);
                Set(f.Data, "<DeletionInProgress>k__BackingField", true);
                Call(f.Monster, "AfterDie");
                Assert.That(Get(f.Monster, "_deathCallbackPending"), Is.True);
                Set(f.Data, "<DeletionInProgress>k__BackingField", false);
                Set(f.Data, "<IsServerDataReady>k__BackingField", true);
                Call(f.Monster, "Update");
                Call(f.Monster, "Update");
                Call(f.Monster, "AfterDie");
                Assert.That(Get(f.Monster, "_deathCallbackPending"), Is.False);
                Assert.That(Get(f.Monster, "_deathHandled"), Is.True);
                Assert.That(Get(f.Player, "_gold"), Is.EqualTo(125));
                Assert.That(f.Values["Gold"], Is.EqualTo("125"));
                Assert.That(File.Exists(f.SavePath), Is.True);
            }
            finally { playerInstance.SetValue(null, oldPlayer); generatorInstance.SetValue(null, oldGenerator); }
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

    [TestCase("captured")]
    [TestCase("pool-reuse")]
    [TestCase("inactive")]
    [TestCase("dead")]
    public void Revival_LateMonsterAttackRejectsChangedTargetAndClearsReference(string change)
    {
        using (var f = new Fixture())
        {
            var attacker = f.CombatMonster(false);
            var target = f.CombatMonster(true);
            if (change == "captured")
            {
                Set(attacker, "_isAlly", true);
                target.gameObject.layer = LayerMask.NameToLayer("Enemy");
                Set(target, "_isAlly", false);
            }
            Set(attacker, change == "captured" ? "_allyTarget" : "_followerTarget", target.gameObject);
            Set(attacker, "_isTarget", true);
            Call(attacker, "BindCombatTarget", target);
            int lifetime = (int)target.GetType().GetProperty("SessionLifetime").GetValue(target);
            if (change == "captured")
            {
                // Capture changes faction without changing the pooled activation.
                target.gameObject.layer = LayerMask.NameToLayer("Ally");
                Set(target, "_isAlly", true);
            }
            if (change == "pool-reuse") Call(Get(target, "_session"), "Bind", f.Data, "A", 1, true);
            if (change == "inactive") target.gameObject.SetActive(false);
            if (change == "dead") Set(target, "_hp", 0f);
            float hp = (float)Get(target, "_hp");

            Call(attacker, "AttackTarget");

            Assert.That(Get(target, "_hp"), Is.EqualTo(hp));
            Assert.That(Get(attacker, "_allyTargetCreature"), Is.Null);
            Assert.That(Get(attacker, "_followerTargetCreature"), Is.Null);
            Assert.That(Get(attacker, "_isTarget"), Is.False);
            if (change == "captured")
                Assert.That(target.GetType().GetProperty("SessionLifetime").GetValue(target), Is.EqualTo(lifetime));
        }
    }

    [Test]
    public void Revival_CurrentOpposingTargetStillTakesNormalDamage()
    {
        using (var f = new Fixture())
        {
            var attacker = f.CombatMonster(false);
            var target = f.CombatMonster(true); // Ally layer avoids damage-popup services in this offline fixture.
            Set(attacker, "_followerTarget", target.gameObject);
            Set(attacker, "_isTarget", true);
            Call(attacker, "BindCombatTarget", target);
            Call(attacker, "AttackTarget");
            Assert.That(Get(target, "_hp"), Is.EqualTo(90f));
            Assert.That(Get(attacker, "_followerTargetCreature"), Is.SameAs(target));
        }
    }

    [TestCase("pool-reuse")]
    [TestCase("captured")]
    [TestCase("inactive")]
    public void Revival_LatePlayerAttackRejectsChangedTarget(string change)
    {
        using (var f = new Fixture())
        {
            var target = f.CombatMonster(false);
            Set(f.Player, "enemyLayer", LayerMask.NameToLayer("Enemy"));
            Call(f.Player, "SetCombatTarget", target);
            if (change == "pool-reuse") Call(Get(target, "_session"), "Bind", f.Data, "A", 1, true);
            if (change == "captured") target.gameObject.layer = LayerMask.NameToLayer("Ally");
            if (change == "inactive") target.gameObject.SetActive(false);
            // Make an erroneous hit observable without invoking enemy damage-popup services.
            Set(target, "enemyLayer", LayerMask.NameToLayer("Die"));
            Set(f.Player, "_power", 10f);
            Call(f.Player, "AttackTarget");
            Assert.That(Get(target, "_hp"), Is.EqualTo(100f));
            Assert.That(Get(f.Player, "_curTargetMonster"), Is.Null);
        }
    }

    [Test]
    public void Revival_CurrentPlayerTargetStillTakesNormalDamage()
    {
        using (var f = new Fixture())
        {
            var target = f.CombatMonster(false);
            Set(f.Player, "enemyLayer", LayerMask.NameToLayer("Enemy"));
            Set(f.Player, "_power", 10f);
            // Keep the real target faction while avoiding unrelated damage-popup services.
            Set(target, "enemyLayer", LayerMask.NameToLayer("Die"));
            Call(f.Player, "SetCombatTarget", target);

            Call(f.Player, "AttackTarget");

            Assert.That(Get(target, "_hp"), Is.EqualTo(90f));
            Assert.That(Get(f.Player, "_curTargetMonster"), Is.SameAs(target));
        }
    }

    [Test]
    public void Revival_SelectedCorpseRemainsAvailableForItsCollectionCallback()
    {
        using (var f = new Fixture())
        {
            var target = f.CombatMonster(false);
            Set(f.Player, "enemyLayer", LayerMask.NameToLayer("Enemy"));
            Set(f.Player, "dieLayer", LayerMask.NameToLayer("Die"));
            Call(f.Player, "SetCombatTarget", target);
            Set(target, "_hp", 0f);
            target.gameObject.layer = LayerMask.NameToLayer("Die");
            Call(f.Player, "AttackTarget");
            Assert.That(Get(f.Player, "_curTargetMonster"), Is.SameAs(target));
            Assert.That(Get(target, "_hp"), Is.Zero);
            Set(f.Data, "_inventorySession", new string('a', 32));
            int lifetime = (int)target.GetType().GetProperty("SessionLifetime").GetValue(target);

            Call(f.Player, "NotifyPlayerOfDeath", target.gameObject, 0, lifetime);

            Assert.That((IEnumerable)Get(f.Player, "PlayerMonsterCollection"), Is.EqualTo(new[] { "Bat" }));
            var snapshot = Call(f.Data, "ReadInventory");
            Assert.That((IEnumerable)snapshot.GetType().GetProperty("Collection").GetValue(snapshot),
                Is.EqualTo(new[] { "Bat" }));
            Assert.That(Get(f.Player, "_curTargetMonster"), Is.Null);
            Call(f.Player, "NotifyPlayerOfDeath", target.gameObject, 0, lifetime);
            Assert.That((IEnumerable)Get(f.Player, "PlayerMonsterCollection"), Is.EqualTo(new[] { "Bat" }));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
        private readonly GameObject _root;
        private readonly FieldInfo _managerInstance = Runtime("AD.Managers").GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object _oldManager;
        private readonly FieldInfo _playerInstance = Runtime("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private object _oldCombatPlayer;
        private bool _combatConfigured;
        private readonly List<GameObject> _combatObjects = new List<GameObject>();
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
        public Component CombatMonster(bool ally)
        {
            if (!_combatConfigured)
            {
                _oldCombatPlayer = _playerInstance.GetValue(null);
                _playerInstance.SetValue(null, Player);
                _combatConfigured = true;
                Set(Data, "MonsterData", new Dictionary<string, object>
                {
                    ["Bat"] = new Dictionary<string, object>
                    { ["Hp"] = "100", ["Power"] = "10", ["AttackSpeed"] = "1", ["MoveSpeed"] = "3", ["Gold"] = "0" }
                });
            }
            var go = new GameObject("Offline combat target");
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, _scene);
            _combatObjects.Add(go);
            var monster = go.AddComponent(Runtime("Monster"));
            Set(monster, "CreatureType", Enum.Parse(Runtime("AD.GameConstants+Creatures"), "Bat"));
            var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.enabled = false;
            Set(monster, "NavMeshAgent", agent);
            Set(monster, "_capsuleCollider", go.AddComponent<CapsuleCollider>());
            var effect = new GameObject("Offline effect");
            effect.transform.SetParent(go.transform);
            Set(monster, "_spawnEffect", effect);
            Set(monster, "_captureEffect", effect);
            Set(monster, "isDie", true); // Any activation loops finish without scheduling battle work.
            go.SetActive(true);
            Call(monster, "StopBattle");
            Set(monster, "isDie", false);
            Set(monster, "_hp", 100f); Set(monster, "_power", 10f);
            Set(monster, "allyLayer", LayerMask.NameToLayer("Ally"));
            Set(monster, "enemyLayer", LayerMask.NameToLayer("Enemy"));
            Set(monster, "dieLayer", LayerMask.NameToLayer("Die"));
            Set(monster, "_isAlly", ally);
            go.layer = LayerMask.NameToLayer(ally ? "Ally" : "Enemy");
            Call(Get(monster, "_session"), "Bind", Data, "A", 1, true);
            return monster;
        }
        public void Dispose()
        {
            foreach (var go in _combatObjects) UnityEngine.Object.DestroyImmediate(go);
            if (_combatConfigured) _playerInstance.SetValue(null, _oldCombatPlayer);
            _managerInstance.SetValue(null, _oldManager);
            EditorSceneManager.ClosePreviewScene(_scene);
            Directory.Delete(_directory, true);
        }
    }
}
