#if TAMER_SESSION_HARNESS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AD;
using UnityEngine;
using UnityEngine.AI;

// Only the separate, offline sessionguard APK can execute these synthetic account transitions.
public sealed class RevivalSessionGameplayHarness : MonoBehaviour
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<int, int> Events = new Dictionary<int, int>();
    private readonly List<string> _checks = new List<string>();
    private int _errors;
    private string _status = "Preparing session checks";
    private Player _player;
    private MonsterGenerator _generator;
    private DataManager _data;

    public static void ObserveDeathEvent(Monster monster)
    {
        int key = monster.GetInstanceID();
        if (Events.ContainsKey(key)) Events[key]++;
    }
    private static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static void Require(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); }
    private void Check(bool condition, string label)
    {
        Require(condition, label);
        _checks.Add(label); Debug.Log("SESSION_CHECK_PASS " + label);
    }
    private void OnLog(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++; }

    public IEnumerator Run()
    {
        Application.logMessageReceived += OnLog;
        IEnumerator scenario = Scenario();
        while (true)
        {
            object next;
            try
            {
                if (!scenario.MoveNext()) break;
                next = scenario.Current;
            }
            catch (Exception error)
            {
                Finish(false, error.GetBaseException().Message);
                yield break;
            }
            yield return next;
        }
        Finish(true, "complete");
    }

    private IEnumerator Scenario()
    {
        Require(!Application.isEditor && Application.identifier == RevivalGameplayIsolation.SessionApplicationId &&
            RevivalGameplayIsolation.AllowsCaptureAssist, "isolated-runtime");
        _data = Managers.DataM; _player = Player.Instance;
        Managers.GameM.SwitchMainOrGameScene();
        float deadline = Time.realtimeSinceStartup + 40;
        while ((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Game" ||
            Managers.SceneM.IsTransitioning || MonsterGenerator.Instance == null) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(MonsterGenerator.Instance != null && !Managers.SceneM.IsTransitioning, "game-ready");
        _generator = MonsterGenerator.Instance;
        Call(_generator, "StopSpawning"); ClearEnemies(); Call(_player, "StopBattle");
        Check(_player == Player.Instance && _data.IsServerDataReady && !Managers.GoogleAdMobM.CanRequestAds &&
            Managers.IAPM.Status == IAPStatus.Unavailable, "original-scene-and-service-guards");

        // Product path: pooled original prefab, NavMesh, GetDamage, real Die clip and its AnimationEvent.
        Monster current = SpawnTarget();
        int before = _player.Gold;
        current.GetDamage(current.Hp + 1);
        deadline = Time.realtimeSinceStartup + 8;
        while (!(bool)Get(current, "_deathHandled") && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Events[current.GetInstanceID()] >= 1 && (bool)Get(current, "_deathHandled") &&
            _player.Gold == before + 25 && SavedGold() == before + 25, "natural-die-animation-reward-once");
        yield return new WaitForSecondsRealtime(.3f);
        Check(_player.Gold == before + 25, "normal-reward-remains-single");
        current.BackPool();

        _player.BuyAllyMonster("Bat");
        Monster oldAlly = Allies().Single();
        int oldAllyLifetime = oldAlly.SessionLifetime;
        HaltCombat(oldAlly);
        Monster stale = SpawnTarget();
        Animator staleAnimator = (Animator)Get(stale, "_animator");
        staleAnimator.speed = 0;
        stale.GetDamage(stale.Hp + 1);
        staleAnimator.Update(0);
        int oldGeneration = _data.AccountGeneration;
        int staleLifetime = stale.SessionLifetime;
        _data.BeginAccountSession(RevivalGameplayIsolation.AccountId);
        Managers.ServerM.GetAllData(update: true);
        Call(_player, "StopBattle");
        before = _player.Gold;
        string roster = _data.LocalPlayerData["AllyMonsters"];
        // Synchronous animation advance delivers the old event before the next Update can retire it.
        staleAnimator.speed = 1;
        staleAnimator.Update(DeathDuration(staleAnimator) + .2f);
        Check(_data.AccountGeneration != oldGeneration && !stale.IsCurrentSession(staleLifetime) &&
            Events[stale.GetInstanceID()] >= 1 && !(bool)Get(stale, "_deathHandled") &&
            _player.Gold == before && SavedGold() == before && _data.LocalPlayerData["AllyMonsters"] == roster,
            "old-generation-animation-event-rejected");
        yield return null;
        Check(!stale.gameObject.activeInHierarchy && !ActiveEnemies().Contains(stale), "stale-enemy-retired-from-generator");
        Call(_generator, "SpawnMonsters", 1);
        Check(ActiveEnemies().Count > 0 && ActiveEnemies().All(m => m.IsCurrentSession(m.SessionLifetime)), "new-generation-spawn-recovered");
        ClearEnemies();

        Monster ally = Allies().Single();
        HaltCombat(ally);
        Require(ally != oldAlly || ally.SessionLifetime != oldAllyLifetime, "restored-ally-session");
        Vector3 allyPoint = Sample(_player.transform.position + _player.transform.forward * 4);
        Require(ally.NavMeshAgent.Warp(allyPoint), "ally-on-navmesh");
        Monster pending = SpawnTarget();
        Animator pendingAnimator = (Animator)Get(pending, "_animator");
        pendingAnimator.speed = 0;
        pending.GetDamage(pending.Hp + 1); pendingAnimator.Update(0);
        var deletion = _data.DeletionSession();
        int allyLifetime = ally.SessionLifetime;
        _data.BeginDeletionSubmission(deletion);
        before = _player.Gold;
        pendingAnimator.speed = 1; pendingAnimator.Update(DeathDuration(pendingAnimator) + .2f);
        yield return null;
        Check(Events[pending.GetInstanceID()] >= 1 && (bool)Get(pending, "_deathCallbackPending") &&
            !(bool)Get(pending, "_deathHandled") && _player.Gold == before && ally.gameObject.activeInHierarchy &&
            ally.SessionLifetime == allyLifetime && Allies().Contains(ally), "deletion-wait-defers-event-and-preserves-ally");
        _data.FinishCancelledDeletion(deletion);
        deadline = Time.realtimeSinceStartup + 3;
        while (!(bool)Get(pending, "_deathHandled") && Time.realtimeSinceStartup < deadline) yield return null;
        Check((bool)Get(pending, "_deathHandled") && !(bool)Get(pending, "_deathCallbackPending") &&
            _player.Gold == before + 25 && SavedGold() == before + 25 && ally.IsCurrentSession(allyLifetime),
            "cancel-resumes-pending-reward-once");
        Vector3 oldPosition = ally.transform.position;
        Call(_player, "AllyMove");
        deadline = Time.realtimeSinceStartup + 3;
        while (Vector3.Distance(oldPosition, ally.transform.position) < .2f && Time.realtimeSinceStartup < deadline) yield return null;
        Check(ally.gameObject.activeInHierarchy && ally.NavMeshAgent.isOnNavMesh &&
            Vector3.Distance(oldPosition, ally.transform.position) >= .2f && _player.Gold == before + 25,
            "cancelled-session-ally-navmesh-movement-resumes");
        Check(_errors == 0 && !Managers.GoogleAdMobM.CanRequestAds && Managers.IAPM.Status == IAPStatus.Unavailable,
            "no-runtime-errors-and-services-remain-blocked");
    }

    private static float DeathDuration(Animator animator)
    {
        var clips = animator.runtimeAnimatorController.animationClips.Where(c =>
            c.events.Any(e => e.functionName == "AfterDie")).ToArray();
        Require(clips.Length > 0, "original-death-animation-event-present");
        return clips.Max(c => c.length);
    }
    private static Vector3 Sample(Vector3 desired)
    {
        Require(NavMesh.SamplePosition(desired, out var hit, 5, NavMesh.AllAreas), "navmesh-sample");
        return hit.position;
    }
    private static void HaltCombat(Monster monster)
    {
        Call(monster, "StopBattle");
        (Get(monster, "_detectionTokenSource") as System.Threading.CancellationTokenSource)?.Cancel();
        Call(monster, "RemoveTarget");
    }
    private Monster SpawnTarget()
    {
        Monster monster = Managers.PoolM.PopFromPool("Bat").GetComponent<Monster>();
        monster.transform.SetParent(_generator.transform, true);
        Require(monster.NavMeshAgent.Warp(Sample(_player.transform.position + _player.transform.right * 3)), "target-navmesh-warp");
        monster.SetEnemyRole(false); HaltCombat(monster);
        Set(monster, "_isAbleAlly", true); Set(monster, "_rewardGold", 25);
        var animator = (Animator)Get(monster, "_animator");
        animator.speed = 1; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Call(_generator, "PlusMonster", monster);
        Events[monster.GetInstanceID()] = 0;
        Require(monster.NavMeshAgent.isOnNavMesh, "target-on-navmesh");
        return monster;
    }
    private List<Monster> ActiveEnemies() => (List<Monster>)Get(_generator, "_activeMonsters");
    private List<Monster> Allies() => (List<Monster>)Get(_player, "_allyMonsters");
    private void ClearEnemies()
    {
        foreach (Monster monster in ActiveEnemies().ToArray()) if (monster != null) monster.BackPool();
    }
    private int SavedGold()
    {
        var save = (Dictionary<string, object>)Utility.DeserializeFromJson(File.ReadAllText(RevivalGameplayIsolation.SavePath));
        return int.Parse(save["Gold"].ToString());
    }
    private void Finish(bool passed, string reason)
    {
        Application.logMessageReceived -= OnLog;
        _status = passed ? "SESSION_DEVICE_PASS" : "SESSION_DEVICE_FAIL " + reason;
        var result = new Result { passed = passed, reason = reason, checks = _checks.ToArray(), errors = _errors,
            applicationId = Application.identifier, unity = Application.unityVersion };
        string path = Path.Combine(Path.GetDirectoryName(RevivalGameplayIsolation.SavePath), "session-result.json");
        File.WriteAllText(path, JsonUtility.ToJson(result, true));
        Debug.Log(_status + " checks=" + _checks.Count + " errors=" + _errors);
    }
    private void OnGUI()
    {
        GUI.depth = -2000;
        GUI.Label(new Rect(10, Screen.height - 100, Screen.width - 20, 90), _status + " | checks=" + _checks.Count + " | errors=" + _errors);
    }
    [Serializable] private sealed class Result
    {
        public bool passed;
        public string reason, applicationId, unity;
        public string[] checks;
        public int errors;
    }
}
#endif
