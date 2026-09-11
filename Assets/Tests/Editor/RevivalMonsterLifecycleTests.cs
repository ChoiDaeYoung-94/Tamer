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
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in _objects) UnityEngine.Object.DestroyImmediate(go);
        _objects.Clear();
        _generatorInstance.SetValue(null, _previousGenerator);
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
}
