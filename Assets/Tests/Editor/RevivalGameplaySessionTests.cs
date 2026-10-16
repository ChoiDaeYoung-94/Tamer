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
    private static object Call(object obj, string method, params object[] args)
    {
        try { return (obj as Type ?? obj.GetType()).GetMethod(method, Fields | BindingFlags.Static)
            .Invoke(obj is Type ? null : obj, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

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

    [Test]
    public void Revival_AllyShop_PresentsOnlyAfterCommitAndSameConfirmationCannotReenter()
    {
        using (var f = new Fixture())
        {
            f.ConfigureAllyShop();
            var intent = Call(f.Player, "PrepareAllyPurchase", "Bat", 10);
            int poolCalls = 0, successCalls = 0;
            bool reentry = true;
            Action<string> present = name =>
            {
                poolCalls++;
                Assert.That(name, Is.EqualTo("Bat"));
                Assert.That(Get(f.Player, "_gold"), Is.EqualTo(90));
                var saved = f.Stored();
                Assert.That(saved["Gold"], Is.EqualTo("90"));
                Assert.That(saved["AllyMonsters"], Is.EqualTo("Bat"));
                reentry = (bool)Call(f.Player, "TryPurchaseAlly", intent, (Action)(() => successCalls++), (Action<string>)(_ => poolCalls++));
            };
            Assert.That(Call(f.Player, "TryPurchaseAlly", intent, (Action)(() => successCalls++), present), Is.True);
            Assert.That(reentry, Is.False);
            Assert.That(Call(f.Player, "TryPurchaseAlly", intent, (Action)(() => successCalls++), present), Is.False);
            Assert.That(poolCalls, Is.EqualTo(1));
            Assert.That(successCalls, Is.EqualTo(1));
            Assert.That(f.Stored()["GooglePlay"], Is.EqualTo("ProductNoAds,FutureProduct"));
            Assert.That(f.Uploads.Count, Is.EqualTo(1));
            Assert.That(f.Uploads[0]["Gold"], Is.EqualTo("90"));
            Assert.That(f.Uploads[0]["AllyMonsters"], Is.EqualTo("Bat"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_AllyShop_PoolOrSuccessScreenFailureStopsOldGoldSavesWithoutRecharging(bool failInPool)
    {
        using (var f = new Fixture())
        {
            f.ConfigureAllyShop();
            var intent = Call(f.Player, "PrepareAllyPurchase", "Bat", 10);
            int poolCalls = 0, successCalls = 0;
            Action<string> present = _ => { poolCalls++; if (failInPool) throw new InvalidOperationException("Synthetic pool failure"); };
            Action success = () => { successCalls++; throw new InvalidOperationException("Synthetic screen failure"); };
            Assert.That(Call(f.Player, "TryPurchaseAlly", intent, success, present), Is.False);
            string committed = File.ReadAllText(f.SavePath);
            Assert.That(f.Stored()["Gold"], Is.EqualTo("90"));
            Assert.That(f.Stored()["AllyMonsters"], Is.EqualTo("Bat"));
            Assert.That(f.Stored()["GooglePlay"], Is.EqualTo("ProductNoAds,FutureProduct"));
            Assert.That(f.Data.GetType().GetProperty("IsServerDataReady").GetValue(f.Data), Is.False);
            Assert.That(Call(f.Player, "TryPurchaseAlly", intent, success, present), Is.False);
            Call(f.Data, "UpdateLocalData", "null", "null", true); // The same periodic-save entry point, with a stopped Player cache.
            Assert.That(Call(f.Data, "TryUpdateLocalData", "Gold", "100"), Is.False);
            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(committed));
            Assert.That(poolCalls, Is.EqualTo(1));
            Assert.That(successCalls, Is.EqualTo(failInPool ? 0 : 1));
            Assert.That(f.Uploads, Is.Empty);
        }
    }

    [Test]
    public void Revival_AllyShop_SaveFailureCannotReachPoolOrSuccessScreen()
    {
        using (var f = new Fixture())
        {
            f.ConfigureAllyShop();
            var intent = Call(f.Player, "PrepareAllyPurchase", "Bat", 10);
            string original = File.ReadAllText(f.SavePath);
            var hook = Runtime("AD.DataManager").GetField("JournalWriteCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = hook.GetValue(null);
            int poolCalls = 0, successCalls = 0;
            try
            {
                hook.SetValue(null, (Action<string, string, string>)((path, temp, stage) =>
                {
                    if (path == f.SavePath && stage == "temporary-closed") throw new IOException("Synthetic save failure");
                }));
                Assert.That(Call(f.Player, "TryPurchaseAlly", intent, (Action)(() => successCalls++),
                    (Action<string>)(_ => poolCalls++)), Is.False);
            }
            finally { hook.SetValue(null, previous); }
            Assert.That(poolCalls, Is.Zero);
            Assert.That(successCalls, Is.Zero);
            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(original));
            Assert.That(f.Uploads, Is.Empty);
        }
    }

    [Test]
    public void Revival_AllyShop_ReplacementPlayerCannotUsePreviousPlayersConfirmation()
    {
        using (var f = new Fixture())
        {
            f.ConfigureAllyShop();
            var intent = Call(f.Player, "PrepareAllyPurchase", "Bat", 10);
            string original = File.ReadAllText(f.SavePath);
            var replacement = f.Managers.gameObject.AddComponent(Runtime("Player"));
            Set(replacement, "_gameplayData", f.Data);
            Set(replacement, "_inventoryOwner", "A");
            Set(replacement, "_inventoryGeneration", 1);
            Set(replacement, "_gold", 100);
            Runtime("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, replacement);
            int screens = 0, presentations = 0;

            Assert.That(Call(replacement, "TryPurchaseAlly", intent, (Action)(() => screens++),
                (Action<string>)(_ => presentations++)), Is.False);
            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(original));
            Assert.That(f.Stored()["Gold"], Is.EqualTo("100"));
            Assert.That(f.Stored()["AllyMonsters"], Is.EqualTo("null"));
            Assert.That(Get(replacement, "_gold"), Is.EqualTo(100));
            Assert.That(f.Data.GetType().GetProperty("IsServerDataReady").GetValue(f.Data), Is.True);
            Assert.That(screens, Is.Zero);
            Assert.That(presentations, Is.Zero);
            Assert.That(f.Uploads, Is.Empty);
        }
    }

    [Test]
    public void Revival_AllyShop_ReplacementManagerRejectsOldConfirmationAndLatePresentation()
    {
        using (var f = new Fixture())
        {
            f.ConfigureAllyShop();
            var intent = Call(f.Player, "PrepareAllyPurchase", "Bat", 10);
            Component replacement = null;
            Component replacementPlayer = null;
            Assert.That(Call(f.Player, "TryPurchaseAlly", intent, (Action)(() => { }), (Action<string>)(_ =>
            {
                replacement = f.NewData();
                Set(replacement, "<PlayFabId>k__BackingField", "B");
                Set(replacement, "_localOwner", "B");
                Set(f.Managers, "_dataM", replacement);
                replacementPlayer = f.Managers.gameObject.AddComponent(Runtime("Player"));
                Set(replacementPlayer, "_gameplayData", replacement);
                Set(replacementPlayer, "_inventoryOwner", "B");
                Set(replacementPlayer, "_inventoryGeneration", 1);
                Set(replacementPlayer, "_gold", 700);
                Runtime("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, replacementPlayer);
            })), Is.False);
            Assert.That(replacement.GetType().GetProperty("IsServerDataReady").GetValue(replacement), Is.True,
                "The old purchase failure may stop only its original manager.");
            Assert.That(((Dictionary<string, string>)Get(replacement, "LocalPlayerData"))["Gold"], Is.EqualTo("100"));
            Assert.That(Get(replacementPlayer, "_gameplayData"), Is.SameAs(replacement));
            Assert.That(Get(replacementPlayer, "_gold"), Is.EqualTo(700), "Old teardown cannot clear a new Player's cache.");
            Assert.That(f.Stored()["Gold"], Is.EqualTo("90"));
            Assert.That(f.Stored()["AllyMonsters"], Is.EqualTo("Bat"));
            Assert.That(Call(f.Player, "TryPurchaseAlly", intent, (Action)(() => { }), (Action<string>)(_ => Assert.Fail("Unexpected replay"))), Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_EquipmentShop_ReplacementPlayerCannotUseOrCommitPreviousPlayersConfirmation(bool duringWrite)
    {
        using (var f = new Fixture())
        {
            f.ConfigureEquipmentShop();
            var intent = Call(f.Player, "PrepareEquipmentPurchase", "MasterSword", 10);
            string original = File.ReadAllText(f.SavePath);
            var replacement = f.Managers.gameObject.AddComponent(Runtime("Player"));
            Set(replacement, "_gameplayData", f.Data);
            Set(replacement, "_inventoryOwner", "A");
            Set(replacement, "_inventoryGeneration", 1);
            Set(replacement, "_gold", 100);
            Action replace = () => Runtime("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, replacement);
            var hook = Runtime("AD.DataManager").GetField("JournalWriteCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
            object previous = hook.GetValue(null);
            int screens = 0;
            try
            {
                if (duringWrite)
                    hook.SetValue(null, (Action<string, string, string>)((path, temporary, stage) =>
                    { if (path == f.SavePath && stage == "temporary-closed") replace(); }));
                else replace();
                Assert.That(Call(duringWrite ? f.Player : replacement, "TryPurchaseEquipment", intent,
                    (Action)(() => screens++), null).ToString(), Is.EqualTo(duringWrite ? "RecoveryRequired" : "Rejected"));
            }
            finally { hook.SetValue(null, previous); }

            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(original));
            Assert.That(f.Stored()["Gold"], Is.EqualTo("100"));
            Assert.That(Get(replacement, "_gold"), Is.EqualTo(duringWrite ? 0 : 100),
                "A stopped save session clears presentation caches while preserving durable Gold.");
            Assert.That(f.Data.GetType().GetProperty("IsServerDataReady").GetValue(f.Data), Is.EqualTo(!duringWrite));
            if (duringWrite) Assert.That(Get(replacement, "_gameplayData"), Is.Null);
            else Assert.That(Get(replacement, "_gameplayData"), Is.SameAs(f.Data));
            Assert.That(Get(replacement, "_inventoryOwner"), Is.EqualTo(duringWrite ? null : "A"));
            Assert.That(File.ReadAllText(f.SavePath + ".inventory/" + Call(Runtime("AD.PlayerInventoryStore"), "OwnerKey", "A") + ".json"),
                Does.Not.Contain("MasterSword"));
            Assert.That(screens, Is.Zero);
            Assert.That(f.Uploads, Is.Empty);
        }
    }

    [Test]
    public void Revival_EquipmentShop_AutoEquipsOnceAndCompletedReplayDoesNotReapplyStats()
    {
        using(var f=new Fixture())
        {
            f.ConfigureEquipmentShop();
            var intent=Call(f.Player,"PrepareEquipmentPurchase","MasterSword",10);int screens=0;
            Assert.That(Call(f.Player,"TryPurchaseEquipment",intent,(Action)(()=>screens++),null).ToString(),Is.EqualTo("Completed"));
            Assert.That(Get(f.Player,"_gold"),Is.EqualTo(90));Assert.That(Get(f.Player,"_power"),Is.EqualTo(50f));
            Assert.That(Get(f.Player,"_hp"),Is.EqualTo(150f));
            CollectionAssert.AreEquivalent(new[]{"MasterSword","SimpleShield"},(IEnumerable)Get(f.Player,"PlayerEquippedItems"));
            string file=File.ReadAllText(f.SavePath);
            Assert.That(Call(f.Player,"TryPurchaseEquipment",intent,(Action)(()=>screens++),null).ToString(),Is.EqualTo("AlreadyCommittedCompleted"));
            Call(f.Player,"ApplyPurchasedEquipment",Call(f.Data,"ReadInventory"));
            Assert.That(Get(f.Player,"_power"),Is.EqualTo(50f));Assert.That(screens,Is.EqualTo(1));
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(file));
            Assert.That(f.Uploads.All(p=>!p.ContainsKey("__TamerEquipmentTransaction")),Is.True);
        }
    }

    [TestCase("model")]
    [TestCase("hud")]
    public void Revival_EquipmentShop_CommittedPresentationFailureCannotRestoreOldGoldOrSpendAgain(string failure)
    {
        using(var f=new Fixture())
        {
            f.ConfigureEquipmentShop();var intent=Call(f.Player,"PrepareEquipmentPurchase","MasterSword",10);
            var hook=Runtime("AD.DataManager").GetField("JournalWriteCheckpoint",BindingFlags.NonPublic|BindingFlags.Static);
            object previous=hook.GetValue(null);int replaces=0,screens=0;
            try
            {
                hook.SetValue(null,(Action<string,string,string>)((p,t,s)=>
                {
                    if(p==f.SavePath&&s=="replaced"&&++replaces==2&&failure=="model")
                        ((IDictionary)Get(Get(f.Managers,"_equipmentM"),"EquipmentMapping"))["MasterSword"]=null;
                }));
                Assert.That(Call(f.Player,"TryPurchaseEquipment",intent,(Action)(()=>
                {screens++;if(failure=="hud")throw new InvalidOperationException("Synthetic HUD failure");}),null).ToString(),Is.EqualTo("RecoveryRequired"));
            }
            finally{hook.SetValue(null,previous);}
            string saved=File.ReadAllText(f.SavePath);
            Assert.That(f.Stored()["Gold"],Is.EqualTo("90"));
            Assert.That(f.Stored()["__TamerEquipmentTransaction"],Does.Contain("\"phase\":\"Complete\""));
            Assert.That(f.Data.GetType().GetProperty("IsServerDataReady").GetValue(f.Data),Is.False);
            Call(f.Data,"UpdateLocalData","null","null",true);
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(saved));
            Assert.That(Call(f.Player,"TryPurchaseEquipment",intent,(Action)(()=>screens++),null).ToString(),Is.EqualTo("Rejected"));
            Assert.That(screens,Is.EqualTo(failure=="hud"?1:0));
        }
    }

    [Test]
    public void Revival_EquipmentShop_PreCommitCutHasNoGoldStatsModelOrSuccessEffect()
    {
        using(var f=new Fixture())
        {
            f.ConfigureEquipmentShop();var intent=Call(f.Player,"PrepareEquipmentPurchase","MasterSword",10);
            string original=File.ReadAllText(f.SavePath);int screens=0;bool observed=false;
            var hook=Runtime("AD.DataManager").GetField("JournalWriteCheckpoint",BindingFlags.NonPublic|BindingFlags.Static);
            object previous=hook.GetValue(null);
            try
            {
                hook.SetValue(null,(Action<string,string,string>)((p,t,s)=>
                {
                    if(p!=f.SavePath||s!="temporary-closed")return;
                    observed=(int)Get(f.Player,"_gold")==100&&(float)Get(f.Player,"_power")==30f&&
                        !((GameObject)((IDictionary)Get(Get(f.Managers,"_equipmentM"),"EquipmentMapping"))["MasterSword"]).activeSelf;
                    throw new IOException("Synthetic equipment pre-commit cut");
                }));
                Assert.That(Call(f.Player,"TryPurchaseEquipment",intent,(Action)(()=>screens++),null).ToString(),Is.EqualTo("RecoveryRequired"));
            }
            finally{hook.SetValue(null,previous);}
            Assert.That(observed,Is.True);Assert.That(screens,Is.Zero);
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(original));
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
        private readonly FieldInfo _playerInstance = Runtime("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private object _oldPlayer;
        private bool _claimedPlayer;
        public readonly List<Dictionary<string, string>> Uploads = new List<Dictionary<string, string>>();
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
        public Dictionary<string, string> Stored() => (Dictionary<string, string>)Call(Runtime("AD.DataManager"),
            "ParseData", File.ReadAllText(SavePath));

        public void ConfigureAllyShop()
        {
            _oldPlayer = _playerInstance.GetValue(null);
            _playerInstance.SetValue(null, Player);
            _claimedPlayer = true;
            Values["GooglePlay"] = "ProductNoAds,FutureProduct";
            Set(Data, "_defaults", new Dictionary<string, string>(Values));
            Set(Data, "MonsterData", new Dictionary<string, object>
            {
                ["Bat"] = new Dictionary<string, object> { ["Price"] = "10" }
            });
            var server = Activator.CreateInstance(Runtime("AD.ServerManager"), new object[]
            {
                (Func<string>)(() => (string)Data.GetType().GetProperty("PlayFabId").GetValue(Data)),
                (Func<bool>)(() => (bool)Data.GetType().GetProperty("IsServerDataReady").GetValue(Data)),
                (Action<string, Action<Dictionary<string, string>>, Action<int>>)((account, success, failure) => { }),
                (Action<string, Dictionary<string, string>, Action, Action<int>>)((account, patch, success, failure) =>
                    Uploads.Add(new Dictionary<string, string>(patch))),
                (Action<TimeSpan, Action>)((delay, action) => { }),
                (Action<Dictionary<string, string>, bool>)((values, update) => { })
            });
            Set(Data, "_server", server);
            Call(Data, "SaveLocalData");
        }
        public void ConfigureEquipmentShop()
        {
            ConfigureAllyShop();
            Set(Data,"_inventorySession",new string('a',32));
            var catalog=new Dictionary<string,object>();
            string[] names={"SimpleSword","MasterSword","SimpleShield","MasterShield"};
            for(int i=0;i<names.Length;i++)catalog[names[i]]=new Dictionary<string,object>
            {
                ["Price"]="10",["Hp"]=i>=2?"50":"0",["Power"]=i==0?"20":i==1?"40":"0",["AttackSpeed"]="0",["MoveSpeed"]="0"
            };
            Set(Data,"ItemData",catalog);
            var mapping=(IDictionary)Get(Get(Managers,"_equipmentM"),"EquipmentMapping");
            foreach(string name in names)
            {
                var model=new GameObject("Offline "+name);model.transform.SetParent(_root.transform);
                model.SetActive(name=="SimpleSword"||name=="SimpleShield");mapping.Add(name,model);
            }
            Call(Data,"ReadInventory");
            Call(Data,"WriteInventory","A",1,new[]{"Bat"},new[]{"SimpleSword","SimpleShield"},
                new Dictionary<string,string>{{"Sword","SimpleSword"},{"Shield","SimpleShield"}});
            Set(Player,"PlayerEquippedItems",new List<string>{"SimpleSword","SimpleShield"});
            Set(Player,"_hp",150f);Set(Player,"_itemAdditionalHp",150f);Set(Player,"_power",30f);
            Set(Player,"_attackSpeed",0.5f);Set(Player,"_moveSpeed",3f);
        }
        public void Dispose()
        {
            if (_claimedPlayer) _playerInstance.SetValue(null, _oldPlayer);
            _managerInstance.SetValue(null, _oldManager);
            EditorSceneManager.ClosePreviewScene(_scene);
            Directory.Delete(_directory, true);
        }
    }
}
