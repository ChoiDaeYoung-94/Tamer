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

    [Test]
    public void Revival_DirectEquipment_PublicEquipCommitsOnceAndRefreshesCurrentHud()
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment();
            var hud = f.ConfigureDirectUi(false);
            string progress = File.ReadAllText(f.SavePath);
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = hook.GetValue(null); int replaces = 0;
            try
            {
                hook.SetValue(null, (Action<string, string, string>)((p, t, s) => { if (p == f.InventoryPath && s == "replaced") replaces++; }));
                Call(Get(f.Managers, "_equipmentM"), "Equip", "MasterSword");
                Call(Get(f.Managers, "_equipmentM"), "Equip", "MasterSword");
            }
            finally { hook.SetValue(null, previous); }
            Assert.That(replaces, Is.EqualTo(1));
            Assert.That(f.Sword(), Is.EqualTo("MasterSword"));
            Assert.That(Get(f.Player, "_power"), Is.EqualTo(50f));
            Assert.That(Get(f.Player, "_hp"), Is.EqualTo(150f));
            CollectionAssert.AreEquivalent(new[] { "MasterSword", "SimpleShield" }, (IEnumerable)Get(f.Player, "PlayerEquippedItems"));
            Assert.That(f.Model("SimpleSword").activeSelf, Is.False);
            Assert.That(f.Model("MasterSword").activeSelf, Is.True);
            Assert.That(f.Model("SimpleShield").activeSelf, Is.True);
            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(progress));
            Assert.That(Get(f.Player, "_gold"), Is.EqualTo(100));
            Assert.That(f.Uploads, Is.Empty);
            var text = Get(hud, "_goldText");
            Assert.That(text.GetType().GetProperty("text").GetValue(text), Is.EqualTo("Gold - 100"));
            Assert.That(f.Ready, Is.True);
        }
    }

    [TestCase("unowned")]
    [TestCase("stats")]
    [TestCase("model")]
    [TestCase("unready")]
    [TestCase("deletion")]
    public void Revival_DirectEquipment_PreflightFailurePreservesInventoryStatsAndModels(string failure)
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment();
            if (failure == "unowned") Call(f.Data, "WriteInventory", "A", 1, new[] { "Bat" }, new[] { "SimpleSword", "SimpleShield" },
                new Dictionary<string, string> { ["Sword"] = "SimpleSword", ["Shield"] = "SimpleShield" });
            if (failure == "stats") ((Dictionary<string, object>)((Dictionary<string, object>)Get(f.Data, "ItemData"))["MasterSword"])["Power"] = "invalid";
            if (failure == "model") ((IDictionary)Get(Get(f.Managers, "_equipmentM"), "EquipmentMapping"))["SimpleSword"] = null;
            if (failure == "unready") Set(f.Data, "<IsServerDataReady>k__BackingField", false);
            if (failure == "deletion") Set(f.Data, "<DeletionInProgress>k__BackingField", true);
            string inventory = f.Inventory(), progress = File.ReadAllText(f.SavePath); int effects = 0;
            Assert.That(f.Direct(() => effects++, () => effects++), Is.False);
            Assert.That(f.Inventory(), Is.EqualTo(inventory));
            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(progress));
            Assert.That(Get(f.Player, "_power"), Is.EqualTo(30f));
            Assert.That(Get(f.Player, "_hp"), Is.EqualTo(150f));
            Assert.That((IEnumerable)Get(f.Player, "PlayerEquippedItems"), Is.EqualTo(new[] { "SimpleSword", "SimpleShield" }));
            Assert.That(f.Model("MasterSword").activeSelf, Is.False);
            Assert.That(effects, Is.Zero);
            Assert.That(f.Ready, Is.EqualTo(failure != "unready"));
        }
    }

    [TestCase("temporary-closed")]
    [TestCase("replaced")]
    public void Revival_DirectEquipment_WriteCutPreservesDurableResultAndReauthRestoresOnce(string stage)
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment(); string inventory = f.Inventory(); int effects = 0;
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = hook.GetValue(null);
            try
            {
                hook.SetValue(null, (Action<string, string, string>)((p, t, s) =>
                { if (p == f.InventoryPath && s == stage) throw new IOException("Synthetic direct-equipment cut"); }));
                Assert.That(f.Direct(() => effects++, () => effects++), Is.False);
            }
            finally { hook.SetValue(null, previous); }
            Assert.That(effects, Is.Zero);
            if (stage == "temporary-closed")
            {
                Assert.That(f.Inventory(), Is.EqualTo(inventory));
                Assert.That(Get(f.Player, "_power"), Is.EqualTo(30f));
                Assert.That(f.Ready, Is.True);
                Assert.That(f.Model("MasterSword").activeSelf, Is.False);
            }
            else
            {
                string durable = f.Inventory();
                Assert.That(f.Sword(), Is.EqualTo("MasterSword"));
                Assert.That(f.Ready, Is.False);
                Assert.That(Get(f.Player, "_gameplayData"), Is.Null);
                Assert.That(f.Direct(), Is.False);
                Assert.That(Call(f.Data, "TryUpdateLocalData", "Gold", "0"), Is.False);
                Assert.That(f.Inventory(), Is.EqualTo(durable));
                f.ReauthenticateDirect();
                Assert.That(f.Ready, Is.True);
                Assert.That(Get(f.Player, "_power"), Is.EqualTo(50f));
                Assert.That(f.Model("MasterSword").activeSelf, Is.True);
                Call(f.Player, "RefreshInventorySession");
                Assert.That(Get(f.Player, "_power"), Is.EqualTo(50f));
            }
        }
    }

    [TestCase("model")]
    [TestCase("shop")]
    [TestCase("hud")]
    public void Revival_DirectEquipment_PostCommitFailureStopsOnlyOriginalSession(string failure)
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment(); var model = f.Model("MasterSword");
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = hook.GetValue(null); int shop = 0, hud = 0;
            try
            {
                if (failure == "model") hook.SetValue(null, (Action<string, string, string>)((p, t, s) =>
                { if (p == f.InventoryPath && s == "replaced") ((IDictionary)Get(Get(f.Managers, "_equipmentM"), "EquipmentMapping"))["MasterSword"] = null; }));
                Assert.That(f.Direct(() => { shop++; if (failure == "shop") throw new InvalidOperationException("Synthetic shop failure"); },
                    () => { hud++; if (failure == "hud") throw new InvalidOperationException("Synthetic HUD failure"); }), Is.False);
            }
            finally { hook.SetValue(null, previous); }
            string durable = f.Inventory(); Assert.That(f.Sword(), Is.EqualTo("MasterSword"));
            Assert.That(f.Ready, Is.False);
            Assert.That(shop, Is.EqualTo(failure == "model" ? 0 : 1));
            Assert.That(hud, Is.EqualTo(failure == "hud" ? 1 : 0));
            Assert.That(f.Direct(), Is.False);
            Assert.That(f.Inventory(), Is.EqualTo(durable));
            ((IDictionary)Get(Get(f.Managers, "_equipmentM"), "EquipmentMapping"))["MasterSword"] = model;
            f.ReauthenticateDirect();
            Assert.That(Get(f.Player, "_power"), Is.EqualTo(50f));
            Assert.That(f.Model("MasterSword").activeSelf, Is.True);
            Assert.That(f.Stored()["GooglePlay"], Is.EqualTo("ProductNoAds,FutureProduct"));
        }
    }

    [Test]
    public void Revival_DirectEquipment_PublicEquipIncludesActualHudFailureBoundary()
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment(); f.ConfigureDirectUi(true); // Missing TMP reference in the actual HUD method.
            Call(Get(f.Managers, "_equipmentM"), "Equip", "MasterSword");
            Assert.That(f.Sword(), Is.EqualTo("MasterSword"));
            Assert.That(f.Ready, Is.False);
            Assert.That(Get(f.Player, "_gameplayData"), Is.Null);
        }
    }

    [TestCase("player", false)]
    [TestCase("manager", false)]
    [TestCase("player", true)]
    [TestCase("manager", true)]
    [TestCase("generation", true)]
    [TestCase("session", true)]
    [TestCase("epoch", true)]
    public void Revival_DirectEquipment_ReplacementIdentityIsRejectedAtEntryAndBeforeReplace(string change, bool duringWrite)
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment(); string inventory = f.Inventory(); int effects = 0;
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = hook.GetValue(null); Component replacement = null;
            try
            {
                if (duringWrite) hook.SetValue(null, (Action<string, string, string>)((p, t, s) =>
                { if (p == f.InventoryPath && s == "temporary-closed") replacement = f.ReplaceDirectContext(change); }));
                else replacement = f.ReplaceDirectContext(change);
                Assert.That(f.Direct(() => effects++, () => effects++), Is.False);
            }
            finally { hook.SetValue(null, previous); }
            Assert.That(f.Inventory(), Is.EqualTo(inventory));
            Assert.That(effects, Is.Zero);
            Assert.That(Get(f.Player, "_power"), Is.EqualTo(30f));
            Assert.That(f.Model("MasterSword").activeSelf, Is.False);
            Assert.That(replacement.GetType().GetProperty("IsServerDataReady").GetValue(replacement), Is.True);
            if (change == "player" || change == "manager")
            {
                var currentPlayer = Runtime("Player").GetProperty("Instance").GetValue(null);
                Assert.That(Get(currentPlayer, "_power"), Is.EqualTo(700f));
                Assert.That((IEnumerable)Get(currentPlayer, "PlayerEquippedItems"), Is.EqualTo(new[] { "SimpleSword", "SimpleShield" }));
                Assert.That(Call(replacement, "ReadInventory"), Is.Not.Null, "The replacement is a valid ready inventory, not rejected because its fixture is incomplete.");
            }
        }
    }

    [TestCase("manager")]
    [TestCase("generation")]
    [TestCase("session")]
    [TestCase("epoch")]
    [TestCase("owner")]
    public void Revival_DirectEquipment_LateEffectCannotStopReplacementAccountOrSession(string change)
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment(); Component replacement = null; int hud = 0;
            Assert.That(f.Direct(() =>
            {
                replacement = f.ReplaceDirectContext(change);
                throw new InvalidOperationException("Synthetic late effect after context replacement");
            }, () => hud++), Is.False);
            Assert.That(f.Sword(), Is.EqualTo("MasterSword"));
            Assert.That(replacement.GetType().GetProperty("IsServerDataReady").GetValue(replacement), Is.True);
            Assert.That(hud, Is.Zero);
            if (change == "manager")
            {
                var currentPlayer = Runtime("Player").GetProperty("Instance").GetValue(null);
                Assert.That(Get(currentPlayer, "_power"), Is.EqualTo(700f));
                Assert.That(Get(currentPlayer, "_gameplayData"), Is.SameAs(replacement));
            }
        }
    }

    [TestCase("session")]
    [TestCase("image")]
    public void Revival_DirectEquipment_PreReplaceFencePreservesChangedInventoryFile(string change)
    {
        using (var f = new Fixture())
        {
            f.ConfigureDirectEquipment(); string originalInventory = f.Inventory(), changed = null; int effects = 0;
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = hook.GetValue(null);
            try
            {
                hook.SetValue(null, (Action<string, string, string>)((p, t, s) =>
                {
                    if (p != f.InventoryPath || s != "temporary-closed") return;
                    string original = f.Inventory();
                    changed = change == "session" ? original.Replace(new string('a', 32), new string('b', 32))
                        : original.Replace("\"Sword\":\"SimpleSword\"", "\"Sword\":\"MasterSword\"");
                    Assert.That(changed, Is.Not.EqualTo(original));
                    File.WriteAllText(p, changed);
                }));
                Assert.That(f.Direct(() => effects++, () => effects++), Is.False);
            }
            finally { hook.SetValue(null, previous); }
            Assert.That(changed, Is.Not.Null);
            Assert.That(changed, Is.Not.EqualTo(originalInventory));
            Assert.That(f.Inventory(), Is.EqualTo(changed));
            Assert.That(Get(f.Player, "_power"), Is.EqualTo(30f));
            Assert.That(f.Model("MasterSword").activeSelf, Is.False);
            Assert.That(effects, Is.Zero);
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
        private readonly FieldInfo _shopInstance = Runtime("ShopMan").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly FieldInfo _hudInstance = Runtime("PlayerUICanvas").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private object _oldShop, _oldHud;
        private bool _claimedUi;
        private bool _combatConfigured;
        private readonly List<GameObject> _combatObjects = new List<GameObject>();
        public readonly List<Dictionary<string, string>> Uploads = new List<Dictionary<string, string>>();
        public string SavePath => Path.Combine(_directory, "PlayerData.json");
        public string InventoryPath => Path.Combine(SavePath + ".inventory", (string)Call(Runtime("AD.PlayerInventoryStore"), "OwnerKey", "A") + ".json");
        public string Inventory() => File.ReadAllText(InventoryPath);
        public string Sword()
        {
            var snapshot = Call(Get(Data, "_inventoryStore"), "Load", "A");
            return ((IDictionary<string, string>)snapshot.GetType().GetProperty("Equipped").GetValue(snapshot))["Sword"];
        }
        public bool Ready => (bool)Data.GetType().GetProperty("IsServerDataReady").GetValue(Data);
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

        private void ClaimPlayer()
        {
            if (!_claimedPlayer) { _oldPlayer = _playerInstance.GetValue(null); _claimedPlayer = true; }
            _playerInstance.SetValue(null, Player);
        }

        public void ConfigureAllyShop()
        {
            ClaimPlayer();
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
        public void ConfigureDirectEquipment()
        {
            _oldShop = _shopInstance.GetValue(null); _oldHud = _hudInstance.GetValue(null); _claimedUi = true;
            _shopInstance.SetValue(null, null); _hudInstance.SetValue(null, null);
            ConfigureEquipmentShop();
            var categories = (IDictionary)Get(Get(Managers, "_equipmentM"), "SegmentedEquipment");
            categories.Add("Sword", new List<string> { "SimpleSword", "MasterSword" });
            categories.Add("Shield", new List<string> { "SimpleShield", "MasterShield" });
            Call(Data, "WriteInventory", "A", 1, new[] { "Bat" }, new[] { "SimpleSword", "MasterSword", "SimpleShield" },
                new Dictionary<string, string> { ["Sword"] = "SimpleSword", ["Shield"] = "SimpleShield" });
        }
        public bool Direct(Action shop = null, Action hud = null) => (bool)Call(Player, "TryEquipInventoryItem", "MasterSword", shop, hud);
        public GameObject Model(string name) => (GameObject)((IDictionary)Get(Get(Managers, "_equipmentM"), "EquipmentMapping"))[name];
        public Component ConfigureDirectUi(bool brokenHud)
        {
            if (!_claimedUi)
            { _oldShop = _shopInstance.GetValue(null); _oldHud = _hudInstance.GetValue(null); _claimedUi = true; }
            var shop = _root.AddComponent(Runtime("ShopMan")); var hud = _root.AddComponent(Runtime("PlayerUICanvas"));
            _shopInstance.SetValue(null, shop); _hudInstance.SetValue(null, hud);
            if (!brokenHud)
            {
                foreach (string field in new[] { "_captureCapacityText", "_goldText", "_playerHpText", "_playerHpSlider" })
                {
                    var go = new GameObject("Offline " + field, typeof(RectTransform)); go.transform.SetParent(_root.transform);
                    Set(hud, field, go.AddComponent(Runtime(field == "_playerHpSlider" ? "UnityEngine.UI.Slider" : "TMPro.TextMeshProUGUI")));
                }
            }
            return hud;
        }
        public Component ReplaceDirectContext(string change)
        {
            if (change == "manager")
            {
                var replacement = NewData();
                Set(replacement, "ItemData", Get(Data, "ItemData")); Set(replacement, "MonsterData", Get(Data, "MonsterData"));
                Set(replacement, "_inventoryStore", Get(Data, "_inventoryStore"));
                Set(replacement, "_inventorySession", new string('a', 32)); Set(replacement, "_inventoryBoundSession", new string('a', 32));
                var manager = _root.AddComponent(Runtime("AD.Managers")); Set(manager, "_dataM", replacement);
                var equipment = Get(manager, "_equipmentM");
                var categories = (IDictionary)Get(equipment, "SegmentedEquipment");
                categories.Add("Sword", new List<string> { "SimpleSword", "MasterSword" });
                categories.Add("Shield", new List<string> { "SimpleShield", "MasterShield" });
                var mapping = (IDictionary)Get(equipment, "EquipmentMapping");
                foreach (string name in new[] { "SimpleSword", "MasterSword", "SimpleShield", "MasterShield" })
                {
                    var model = new GameObject("Replacement " + name); model.transform.SetParent(_root.transform);
                    model.SetActive(name == "SimpleSword" || name == "SimpleShield"); mapping.Add(name, model);
                }
                _managerInstance.SetValue(null, manager);
                var player = _root.AddComponent(Runtime("Player"));
                Set(player, "_gameplayData", replacement); Set(player, "_inventoryOwner", "A"); Set(player, "_inventoryGeneration", 1);
                Set(player, "PlayerEquippedItems", new List<string> { "SimpleSword", "SimpleShield" });
                Set(player, "_power", 700f); _playerInstance.SetValue(null, player);
                return replacement;
            }
            if (change == "player")
            {
                var player = _root.AddComponent(Runtime("Player"));
                Set(player, "_gameplayData", Data); Set(player, "_inventoryOwner", "A"); Set(player, "_inventoryGeneration", 1);
                Set(player, "PlayerEquippedItems", new List<string> { "SimpleSword", "SimpleShield" });
                Set(player, "_power", 700f); _playerInstance.SetValue(null, player);
            }
            if (change == "generation") Set(Data, "_accountGeneration", 2);
            if (change == "session") Set(Data, "_inventorySession", new string('b', 32));
            if (change == "epoch") Set(Data, "<DeletionEpoch>k__BackingField", 1);
            if (change == "owner") { Set(Data, "<PlayFabId>k__BackingField", "B"); Set(Data, "_localOwner", "B"); }
            return Data;
        }
        public void ReauthenticateDirect()
        {
            Call(Data, "BeginAccountSession", "A");
            Set(Data, "PlayFabPlayerData", new Dictionary<string, PlayFab.ClientModels.UserDataRecord>
            {
                ["Gold"] = new PlayFab.ClientModels.UserDataRecord { Value = "100" },
                ["AllyMonsters"] = new PlayFab.ClientModels.UserDataRecord { Value = "null" },
                ["GooglePlay"] = new PlayFab.ClientModels.UserDataRecord { Value = "ProductNoAds,FutureProduct" }
            });
            Call(Data, "UpdateData"); // Actual ready/read/RefreshInventorySession path with the offline server fixture.
        }
        public Component CombatMonster(bool ally)
        {
            if (!_combatConfigured)
            {
                ClaimPlayer();
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
            if (_claimedUi) { _shopInstance.SetValue(null, _oldShop); _hudInstance.SetValue(null, _oldHud); }
            if (_claimedPlayer) _playerInstance.SetValue(null, _oldPlayer);
            _managerInstance.SetValue(null, _oldManager);
            EditorSceneManager.ClosePreviewScene(_scene);
            Directory.Delete(_directory, true);
        }
    }
}
