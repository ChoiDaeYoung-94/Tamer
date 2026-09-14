using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class RevivalMonsterLifecycleTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> _objects = new List<GameObject>();
    private UnityEngine.Random.State _randomState;
    private FieldInfo _generatorInstance;
    private object _previousGenerator;
    private FieldInfo _playerInstance;
    private object _previousPlayer;
    private FieldInfo _canvasInstance;
    private object _previousCanvas;

    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    private static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Members | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }

    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Call(object target, string name, params object[] args)
    {
        try { target.GetType().GetMethod(name, Members).Invoke(target, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }

    private Component Create(string type)
    {
        var go = new GameObject("Revival isolated " + type);
        go.SetActive(false); // Prevent Awake/OnEnable from accessing live managers or player data.
        _objects.Add(go);
        return go.AddComponent(RuntimeType(type));
    }

    private Component CreateMonster()
    {
        Component monster = Create("Monster");
        Set(monster, "NavMeshAgent", monster.gameObject.AddComponent<NavMeshAgent>());
        Set(monster, "_capsuleCollider", monster.gameObject.AddComponent<CapsuleCollider>());
        var effect = new GameObject("Revival capture effect");
        effect.SetActive(false);
        _objects.Add(effect);
        Set(monster, "_captureEffect", effect);
        return monster;
    }

    [SetUp]
    public void SetUp()
    {
        _randomState = UnityEngine.Random.state;
        _generatorInstance = RuntimeType("MonsterGenerator").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        _previousGenerator = _generatorInstance.GetValue(null);
        _generatorInstance.SetValue(null, null);
        _playerInstance = RuntimeType("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        _previousPlayer = _playerInstance.GetValue(null);
        _playerInstance.SetValue(null, null);
        _canvasInstance = RuntimeType("PlayerUICanvas").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        _previousCanvas = _canvasInstance.GetValue(null);
        _canvasInstance.SetValue(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in _objects) UnityEngine.Object.DestroyImmediate(go);
        _objects.Clear();
        _generatorInstance.SetValue(null, _previousGenerator);
        _playerInstance.SetValue(null, _previousPlayer);
        _canvasInstance.SetValue(null, _previousCanvas);
        UnityEngine.Random.state = _randomState;
    }

    [Test]
    public void Revival_GeneratorInitializationHasOneLoopAndDisableIsRepeatable()
    {
        Component generator = Create("MonsterGenerator");
        Set(generator, "_maxMonsters", 0); // No prefabs, player, or service access.
        Call(generator, "Init");
        var first = (CancellationTokenSource)Get(generator, "_spawnLoopTokenSource");
        var token = first.Token;
        Call(generator, "Init");
        Assert.That(Get(generator, "_spawnLoopTokenSource"), Is.SameAs(first));
        Call(generator, "OnDisable");
        Call(generator, "OnDisable");
        Assert.That(token.IsCancellationRequested, Is.True);
        Assert.That(Get(generator, "_spawnLoopTokenSource"), Is.Null);
        Call(generator, "Init");
        Assert.That(Get(generator, "_spawnLoopTokenSource"), Is.Not.SameAs(first));
        Call(generator, "OnDisable");
    }

    [Test]
    public void Revival_OldGeneratorDestructionDoesNotClearNewOwner()
    {
        Component old = Create("MonsterGenerator");
        Component current = Create("MonsterGenerator");
        _generatorInstance.SetValue(null, current);
        Call(old, "OnDestroy");
        Assert.That(_generatorInstance.GetValue(null), Is.SameAs(current));
    }

    [Test]
    public void Revival_PlayerRebindingAndDisableReleaseCapturedUpdatePublisher()
    {
        Component player = Create("Player");
        Component first = Create("AD.UpdateManager");
        Component second = Create("AD.UpdateManager");
        Call(player, "BindUpdates", first);
        Call(player, "BindUpdates", first);
        Assert.That(((Delegate)Get(first, "OnUpdateEvent")).GetInvocationList().Length, Is.EqualTo(1));
        Call(player, "BindUpdates", second);
        Assert.That(Get(first, "OnUpdateEvent"), Is.Null);
        Call(player, "OnDisable");
        Call(player, "OnDisable");
        Assert.That(Get(second, "OnUpdateEvent"), Is.Null);
    }

    [Test]
    public void Revival_RepeatedBattleStartCancelsBothPreviousOwners()
    {
        Component monster = CreateMonster();
        Set(monster, "isDie", true); // Loops finish synchronously, with no scene or player access.
        CancellationToken battle = default;
        CancellationToken monitor = default;
        for (int i = 0; i < 3; i++)
        {
            Call(monster, "StartBattle");
            if (i > 0)
            {
                Assert.That(battle.IsCancellationRequested, Is.True);
                Assert.That(monitor.IsCancellationRequested, Is.True);
            }
            battle = ((CancellationTokenSource)Get(monster, "_battleTokenSource")).Token;
            monitor = ((CancellationTokenSource)Get(monster, "_monitorTargetDistanceTokenSource")).Token;
            Assert.That(battle.IsCancellationRequested || monitor.IsCancellationRequested, Is.False);
        }
        Call(monster, "Clear");
        Call(monster, "Clear");
        Assert.That(battle.IsCancellationRequested && monitor.IsCancellationRequested, Is.True);
        Assert.That(Get(monster, "_battleTokenSource"), Is.Null);
        Assert.That(Get(monster, "_monitorTargetDistanceTokenSource"), Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Revival_DeadOrDisabledNavigationDoesNotRunDetectionMovement(bool dead)
    {
        Component monster = CreateMonster();
        Set(monster, "isDie", dead);
        Set(monster, "_isDetection", true);
        Set(monster, "_isAlly", true);
        ((NavMeshAgent)Get(monster, "NavMeshAgent")).enabled = false;
        var playerInstance = RuntimeType("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = playerInstance.GetValue(null);
        try
        {
            playerInstance.SetValue(null, null);
            // Entering AfterDetection here would touch the absent Player/invalid agent.
            Assert.DoesNotThrow(() => Call(monster, "Update"));
        }
        finally { playerInstance.SetValue(null, previous); }
    }

    [Test]
    public void Revival_DetectionRestartRetiresPreviousLoopOwner()
    {
        Component monster = CreateMonster();
        Set(monster, "isDie", true); // Finish without physics or scene access.
        var original = new CancellationTokenSource();
        var previous = original.Token;
        Set(monster, "_detectionTokenSource", original);
        for (int i = 0; i < 3; i++)
        {
            Call(monster, "StartDetection");
            Assert.That(previous.IsCancellationRequested, Is.True);
            previous = ((CancellationTokenSource)Get(monster, "_detectionTokenSource")).Token;
            Assert.That(previous.IsCancellationRequested, Is.False);
        }
        Call(monster, "Clear");
        Assert.That(previous.IsCancellationRequested, Is.True);
    }

    [Test]
    public void Revival_NoOpCollectionChangesPreserveTheStoredPrefix()
    {
        Component player = Create("Player");
        var collection = new List<string> { "Bat", "Crab" };
        string key = "Revival-no-write-" + Guid.NewGuid().ToString("N");
        foreach (var operation in new[] { ("SavePrefs", "Bat"), ("RemovePrefs", "Missing") })
        {
            object result = player.GetType().GetMethod(operation.Item1, Members)
                .Invoke(player, new object[] { collection, "Bat,Crab", operation.Item2, key });
            Assert.That(result, Is.EqualTo("Bat,Crab"));
            Assert.That(collection, Is.EqualTo(new[] { "Bat", "Crab" }));
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
        }
    }

    [Test]
    public void Revival_PlayerGoldBelongsToTheQueriedPlayer()
    {
        Component old = Create("Player");
        Component current = Create("Player");
        Set(old, "_gold", 10);
        Set(current, "_gold", 20);
        var singleton = RuntimeType("Player").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = singleton.GetValue(null);
        try
        {
            singleton.SetValue(null, current);
            Assert.That(old.GetType().GetProperty("Gold").GetValue(old), Is.EqualTo(10));
            Assert.That(current.GetType().GetProperty("Gold").GetValue(current), Is.EqualTo(20));
        }
        finally { singleton.SetValue(null, previous); }
    }

    [TestCase("ShopMan", "_instance")]
    [TestCase("BuffingMan", "instance")]
    [TestCase("Portal", "_instance")]
    [TestCase("CameraManage", "_instance")]
    [TestCase("MiniMap", "_instance")]
    public void Revival_OldSceneObjectDoesNotClearReplacement(string type, string fieldName)
    {
        var field = RuntimeType(type).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        var previous = field.GetValue(null);
        try
        {
            var old = Create(type);
            var current = Create(type);
            field.SetValue(null, current);
            Call(old, "OnDestroy");
            Assert.That(field.GetValue(null), Is.SameAs(current));
            Call(current, "OnDestroy");
            Assert.That(field.GetValue(null), Is.Null);
        }
        finally { field.SetValue(null, previous); }
    }

    [Test]
    public void Revival_FogTextureIsReusedUpdatedAndReleasedWithRenderer()
    {
        Component renderer = Create("FogOfWarRenderer");
        var data = ScriptableObject.CreateInstance(RuntimeType("FogOfWarData"));
        Texture2D texture = null;
        try
        {
            var grid = Activator.CreateInstance(RuntimeType("FogOfWarGrid"));
            Set(grid, "<Size>k__BackingField", new Vector2(2, 2));
            Set(data, "<Grid>k__BackingField", grid);
            Set(renderer, "data", data);
            var cells = new HashSet<Vector2Int> { new Vector2Int(-1, -1) };
            var convert = renderer.GetType().GetMethod("CellsToTexture", Members);
            texture = (Texture2D)convert.Invoke(renderer, new object[] { cells, 2, 2, null });
            Set(renderer, "_dynamicCells", texture);
            Assert.That(texture.GetPixel(0, 0), Is.EqualTo(Color.white));
            cells.Clear();
            var next = convert.Invoke(renderer, new object[] { cells, 2, 2, texture });
            Assert.That(next, Is.SameAs(texture));
            Assert.That(texture.GetPixel(0, 0), Is.EqualTo(Color.black));
            Call(renderer, "OnDestroy");
            Assert.That(texture == null, Is.True);
            Call(renderer, "OnDestroy");
        }
        finally
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [TestCase(100, true)]
    [TestCase(9, false)]
    public void Revival_ShopConfirmationConsumesOnceAndRechecksFunds(int funds, bool expected)
    {
        Component shop = Create("ShopMan");
        Set(shop, "_purchasePending", true);
        Set(shop, "_currentItemPrice", 10);
        Set(shop, "_currentItemName", "Bat");
        var confirm = shop.GetType().GetMethod("TryConsumePurchase", Members);
        Assert.That(confirm.Invoke(shop, new object[] { funds }), Is.EqualTo(expected));
        Assert.That(confirm.Invoke(shop, new object[] { 100 }), Is.EqualTo(false));
    }

    [Test]
    public void Revival_MiniMapDisableReleasesItsCapturedUpdatePublisher()
    {
        Component map = Create("MiniMap");
        Component first = Create("AD.UpdateManager");
        Component replacement = Create("AD.UpdateManager");
        Call(map, "BindUpdates", first);
        Call(map, "BindUpdates", replacement);
        Assert.That(Get(first, "OnUpdateEvent"), Is.Null);
        Assert.That(((Delegate)Get(replacement, "OnUpdateEvent")).GetInvocationList().Length, Is.EqualTo(1));
        Call(map, "OnDisable");
        Assert.That(Get(replacement, "OnUpdateEvent"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_MiniMapDisableReleasesPauseWithoutOverwritingNewScale(bool changed)
    {
        Component map = Create("MiniMap");
        float original = Time.timeScale;
        try
        {
            Time.timeScale = 0.5f;
            Call(map, "AcquirePause");
            Call(map, "AcquirePause");
            Assert.That(Time.timeScale, Is.Zero);
            if (changed) Time.timeScale = 0.75f;
            Call(map, "OnDisable");
            Assert.That(Time.timeScale, Is.EqualTo(changed ? 0.75f : 0.5f));
            Time.timeScale = 0.25f;
            Call(map, "OnDisable");
            Assert.That(Time.timeScale, Is.EqualTo(0.25f));
        }
        finally { Time.timeScale = original; }
    }

    [TestCase("Item", "ItemList")]
    [TestCase("IAPItem", "IAPitemList")]
    public void Revival_DestroyedShopItemUnregistersFromItsCapturedShop(string type, string listName)
    {
        Component shop = Create("ShopMan");
        Component item = Create(type);
        var items = (System.Collections.IList)Get(shop, listName);
        items.Add(item);
        Set(item, "_shopOwner", shop);
        Call(item, "OnDestroy");
        Call(item, "OnDestroy");
        Assert.That(items.Count, Is.Zero);
    }

    [TestCase(false, false, 20)]
    [TestCase(true, false, 40)]
    [TestCase(true, true, 5)]
    public void Revival_EnemyRoleAppliesItsCaptureProbability(bool commander, bool boss, int percent)
    {
        Component monster = CreateMonster();
        for (int seed = 0; seed < 100; seed++)
        {
            UnityEngine.Random.InitState(seed);
            bool expected = UnityEngine.Random.Range(0, 100) < percent;
            UnityEngine.Random.InitState(seed);
            Call(monster, "SetEnemyRole", commander, boss);
            Assert.That(Get(monster, "_isAbleAlly"), Is.EqualTo(expected), "seed " + seed);
        }
    }

    [Test]
    public void Revival_BossClearUnregistersOnlyItsOwnObjectAndResetsPooledRole()
    {
        Component generator = Create("MonsterGenerator");
        _generatorInstance.SetValue(null, generator);
        Component boss = CreateMonster();
        Component replacement = CreateMonster();
        Call(boss, "SetEnemyRole", true, true);
        Set(generator, "BossMonster", boss.gameObject);
        Set(boss, "CommanderMonster", replacement);
        Call(boss, "Clear");
        Assert.That(Get(generator, "BossMonster"), Is.Null);
        Assert.That(Get(boss, "_isBoss"), Is.False);
        Assert.That(Get(boss, "IsCommander"), Is.False);
        Assert.That(Get(boss, "CommanderMonster"), Is.Null);

        Call(boss, "SetEnemyRole", true, true);
        Set(generator, "BossMonster", replacement.gameObject);
        Call(boss, "Clear");
        Assert.That(Get(generator, "BossMonster"), Is.SameAs(replacement.gameObject));
        Call(boss, "SetEnemyRole", false, false);
        Assert.That(Get(boss, "_isBoss"), Is.False);
        Assert.That(Get(boss, "IsCommander"), Is.False);
    }

    [Test]
    public void Revival_GroupSpawnsRespectActualRemainingCapacity()
    {
        MethodInfo countFollowers = RuntimeType("MonsterGenerator").GetMethod("GetFollowerCount", BindingFlags.Static | BindingFlags.NonPublic);
        for (int active = 0; active <= 16; active++)
        for (int requested = 1; requested <= 3; requested++)
        {
            int count = active;
            for (int group = 0; group < 5; group++)
            {
                int followers = (int)countFollowers.Invoke(null, new object[] { 15 - count, requested });
                if (15 - count < 2) Assert.That(followers, Is.Zero);
                if (followers == 0) break;
                Assert.That(followers, Is.InRange(1, requested));
                count += 1 + followers;
                Assert.That(count, Is.LessThanOrEqualTo(15));
            }
        }
    }

    private Component CreateCaptureSelection(out Component monster, out GameObject button, out Collider trigger)
    {
        Component player = Create("Player");
        _playerInstance.SetValue(null, player);
        Set(player, "_hp", 100f);
        Set(player, "_originalHp", 100f);
        Set(player, "_capsuleCollider", player.gameObject.AddComponent<CapsuleCollider>());
        Component canvas = Create("PlayerUICanvas");
        _canvasInstance.SetValue(null, canvas);
        button = new GameObject("Revival capture button");
        _objects.Add(button);
        Set(canvas, "_captureButton", button);
        monster = CreateMonster();
        var effect = (GameObject)Get(monster, "_captureEffect");
        effect.transform.SetParent(monster.transform);
        effect.tag = "Capture";
        trigger = effect.AddComponent<SphereCollider>();
        Set(player, "_ableCaptureMonster", monster);
        Set(player, "_captureTrigger", trigger);
        return player;
    }

    [TestCase("OnDeath")]
    [TestCase("OnDisable")]
    [TestCase("ReSetPlayer")]
    public void Revival_DeathSceneExitAndRespawnClearCaptureSelection(string lifecycle)
    {
        Component player = CreateCaptureSelection(out _, out GameObject button, out _);
        Call(player, lifecycle);
        Assert.That(Get(player, "_ableCaptureMonster"), Is.Null);
        Assert.That(Get(player, "_captureTrigger"), Is.Null);
        Assert.That(button.activeSelf, Is.False);
        // A stale queued click cannot alter allies or reach account/save services.
        Assert.DoesNotThrow(() => Call(player, "Capture"));
        Assert.That(((System.Collections.IList)Get(player, "_allyMonsters")).Count, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_TriggerExitClearsSelectedCorpseEvenAfterDeath(bool dead)
    {
        Component player = CreateCaptureSelection(out _, out GameObject button, out Collider trigger);
        Set(player, "isDie", dead);
        var other = new GameObject("Revival unrelated trigger");
        _objects.Add(other);
        Call(player, "OnTriggerExit", other.AddComponent<SphereCollider>());
        Assert.That(button.activeSelf, Is.True);
        Assert.That(Get(player, "_captureTrigger"), Is.SameAs(trigger));
        Call(player, "OnTriggerExit", trigger);
        Assert.That(button.activeSelf, Is.False);
        Assert.That(Get(player, "_ableCaptureMonster"), Is.Null);
    }

    [Test]
    public void Revival_OldPlayerDisableDoesNotHideCurrentCaptureButton()
    {
        Component current = CreateCaptureSelection(out Component selected, out GameObject button, out _);
        Component old = Create("Player");
        Set(old, "_ableCaptureMonster", selected);
        Call(old, "OnDisable");
        Assert.That(Get(old, "_ableCaptureMonster"), Is.Null);
        Assert.That(Get(current, "_ableCaptureMonster"), Is.SameAs(selected));
        Assert.That(button.activeSelf, Is.True);
    }

    [Test]
    public void Revival_PooledCorpseReleasesOnlyItsOwnCaptureSelection()
    {
        Component player = CreateCaptureSelection(out Component selected, out GameObject button, out _);
        Component other = CreateMonster();
        Call(other, "Clear");
        Assert.That(Get(player, "_ableCaptureMonster"), Is.SameAs(selected));
        Assert.That(button.activeSelf, Is.True);
        Call(selected, "Clear");
        Assert.That(Get(player, "_ableCaptureMonster"), Is.Null);
        Assert.That(button.activeSelf, Is.False);
        Assert.DoesNotThrow(() => Call(player, "Capture"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_InactiveOrDestroyedCorpseRejectsCaptureWithoutSaveAccess(bool destroyed)
    {
        Component player = CreateCaptureSelection(out Component monster, out GameObject button, out _);
        if (destroyed) UnityEngine.Object.DestroyImmediate(monster.gameObject);
        Assert.DoesNotThrow(() => Call(player, "Capture"));
        Assert.That(button.activeSelf, Is.False);
        Assert.That(Get(player, "_ableCaptureMonster"), Is.Null);
        Assert.That(((System.Collections.IList)Get(player, "_allyMonsters")).Count, Is.Zero);
    }

    [Test]
    public void Revival_RemainingOverlappingCorpseCanBeSelectedOnStay()
    {
        Component player = CreateCaptureSelection(out Component monster, out GameObject button, out Collider trigger);
        // EditMode-only objects: no player loop, production managers, or save transport.
        Set(monster, "isDie", true);
        Set(monster, "_hp", 0f);
        Set(monster, "_isAbleAlly", true);
        monster.gameObject.SetActive(true);
        trigger.gameObject.SetActive(true);
        Assert.That(monster.GetType().GetProperty("IsCaptureAvailable").GetValue(monster), Is.True);
        Call(player, "ClearCaptureTarget");
        Call(player, "OnTriggerStay", trigger);
        Assert.That(Get(player, "_ableCaptureMonster"), Is.SameAs(monster));
        Assert.That(button.activeSelf, Is.True);
        Call(player, "OnTriggerExit", trigger);
        Assert.That(button.activeSelf, Is.False);
        Set(monster, "_isAlly", true);
        Call(player, "OnTriggerStay", trigger);
        Assert.That(Get(player, "_ableCaptureMonster"), Is.Null);
        Assert.That(button.activeSelf, Is.False);
    }
}
