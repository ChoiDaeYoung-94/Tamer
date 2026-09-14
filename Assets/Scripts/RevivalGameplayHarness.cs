#if UNITY_EDITOR || TAMER_GAMEPLAY_HARNESS
using System;
using System.Collections;
using System.IO;
using AD;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>Exercises original scenes with memory transport and a separate application sandbox.</summary>
public sealed class RevivalGameplayHarness : MonoBehaviour
{
    private string _status = "Starting isolated gameplay";
    private int _errors;
    private int _roundTrips;
    private bool _busy;
    private Player _originalPlayer;
    private Managers _originalManagers;
    private string _lastObservation;
    private float _nextObservation;
    private string _observedScene;
    private MonsterGenerator _previousGenerator;
    private int _previousGameSceneHandle;
    private Monster[] _previousMonsters = Array.Empty<Monster>();
    private string _lastCaptureObservation;

    // Observe the real gameplay state; never set HP, spawn enemies, or grant captures.
    private void Update()
    {
        if (Time.realtimeSinceStartup < _nextObservation) return;
        _nextObservation = Time.realtimeSinceStartup + .25f;
        var player = Player.Instance;
        if (player == null || Managers.Instance != _originalManagers) return;
        ObserveSceneLifetime();
        ObserveCaptures(player);
        string observation = "scene=" + UnitySceneManager.GetActiveScene().name +
            " hp=" + player.Hp.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
            " gold=" + player.Gold + " allies=" + player.GetCurMonsterCount() +
            " reads=" + RevivalGameplayIsolation.Reads + " writes=" + RevivalGameplayIsolation.Writes;
        if (observation == _lastObservation) return;
        _lastObservation = observation;
        Debug.Log("GAMEPLAY_OBSERVATION " + observation);
    }

    private void ObserveCaptures(Player player)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var selected = typeof(Player).GetField("_ableCaptureMonster", flags).GetValue(player) as Monster;
        var playerCollider = typeof(Creature).GetField("_capsuleCollider", flags).GetValue(player) as Collider;
        var text = new System.Text.StringBuilder("playerHp=" + player.Hp + " selected=" + (selected != null));
        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            if (monster.Hp > 0) continue;
            var effect = typeof(Monster).GetField("_captureEffect", flags).GetValue(monster) as GameObject;
            bool overlap = false;
            if (effect != null && playerCollider != null && playerCollider.enabled)
                foreach (Collider trigger in effect.GetComponentsInChildren<Collider>())
                    if (trigger.enabled && Physics.ComputePenetration(playerCollider, playerCollider.transform.position,
                        playerCollider.transform.rotation, trigger, trigger.transform.position, trigger.transform.rotation,
                        out _, out _)) overlap = true;
            text.Append(" | type=").Append(monster.CreatureType).Append(" available=").Append(monster.IsCaptureAvailable)
                .Append(" dead=").Append(typeof(Creature).GetField("isDie", flags).GetValue(monster))
                .Append(" rolled=").Append(typeof(Monster).GetField("_isAbleAlly", flags).GetValue(monster))
                .Append(" effect=").Append(effect != null && effect.activeInHierarchy)
                .Append(" overlap=").Append(overlap).Append(" distance=")
                .Append(Vector3.Distance(player.transform.position, monster.transform.position).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }
        string observation = text.ToString();
        if (observation == _lastCaptureObservation) return;
        _lastCaptureObservation = observation;
        Debug.Log("GAMEPLAY_CAPTURE_OBSERVATION " + observation);
    }

    private void ObserveSceneLifetime()
    {
        string scene = UnitySceneManager.GetActiveScene().name;
        if (scene == _observedScene || (scene != "Main" && scene != "Game") || !Ready(scene)) return;
        _observedScene = scene;
        int previousInOldScene = 0, reusedActive = 0;
        foreach (Monster monster in _previousMonsters)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy || !monster.CompareTag("Monster")) continue;
            if (monster.gameObject.scene.handle == _previousGameSceneHandle) previousInOldScene++;
            else reusedActive++; // A pooled object may legitimately be reused in the new scene.
        }
        MonsterGenerator generator = MonsterGenerator.Instance;
        Monster[] monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        int enemies = 0, outsideGenerator = 0;
        foreach (Monster monster in monsters)
        {
            if (!monster.CompareTag("Monster")) continue;
            enemies++;
            if (generator == null || !monster.transform.IsChildOf(generator.transform)) outsideGenerator++;
        }
        Debug.Log("GAMEPLAY_SCENE_LIFETIME scene=" + scene +
            " previousGeneratorAlive=" + (_previousGenerator != null) +
            " previousEnemiesInOldScene=" + previousInOldScene + " reusedEnemiesActive=" + reusedActive + " enemies=" + enemies +
            " outsideGenerator=" + outsideGenerator);
        if (scene == "Game")
        {
            _previousGenerator = generator;
            _previousGameSceneHandle = UnitySceneManager.GetActiveScene().handle;
            _previousMonsters = monsters;
        }
    }

#if TAMER_GAMEPLAY_HARNESS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor || Application.identifier != RevivalGameplayIsolation.ApplicationId)
            throw new InvalidOperationException("Gameplay harness requires its separate Android application.");
        var root = new GameObject("Offline gameplay verification");
        DontDestroyOnLoad(root);
        root.AddComponent<RevivalGameplayHarness>();
    }
#endif

    private void Awake() => Application.logMessageReceived += OnLog;
    private void OnDestroy() => Application.logMessageReceived -= OnLog;
    private void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
    }
    private void Mark(string value)
    {
        _status = value;
        Debug.Log("GAMEPLAY_HARNESS " + value);
    }

    private IEnumerator Start()
    {
        yield return null;
        _originalManagers = Managers.Instance;
        if (_originalManagers == null || RevivalGameplayIsolation.BlockedLogins != 1)
        { Mark("FAIL bootstrap/login guard"); yield break; }
        Managers.DataM.BeginAccountSession(RevivalGameplayIsolation.AccountId);
        Managers.ServerM.GetAllData(update: true);
        if (!Managers.DataM.IsServerDataReady || !File.Exists(RevivalGameplayIsolation.SavePath))
        { Mark("FAIL synthetic data"); yield break; }
        Managers.GoogleAdMobM.LoadRewardedAd();
        if (Managers.GoogleAdMobM.CanRequestAds)
        { Mark("FAIL advertising guard"); yield break; }
        Mark("ISOLATION_OK login=blocked ads=blocked memory-server=ready app-private-save=ready");
        Managers.SceneM.NextScene(GameConstants.Scene.Main);
        yield return WaitForScene("Main");
        if (!Ready("Main")) { Mark("FAIL Main entry"); yield break; }
        _originalPlayer = Player.Instance;
        Mark("MAIN_READY iapBlocked=" + RevivalGameplayIsolation.BlockedPurchases);
        // Keep the real lobby available for visual inspection before the automatic round trip.
        yield return new WaitForSecondsRealtime(8);
        yield return RoundTrip();
    }

    private bool Ready(string scene) => UnitySceneManager.GetActiveScene().name == scene &&
        Managers.Instance == _originalManagers && Managers.SceneM != null && !Managers.SceneM.IsTransitioning &&
        Player.Instance != null && Player.Instance.gameObject.activeInHierarchy &&
        CameraManage.Instance != null && JoyStick.Instance != null && PlayerUICanvas.Instance != null;

    private IEnumerator WaitForScene(string scene)
    {
        float deadline = Time.realtimeSinceStartup + 40;
        while (!Ready(scene) && Time.realtimeSinceStartup < deadline) yield return null;
        // Start methods and one rendered frame must run after the scene-load callback.
        yield return null;
    }

    private IEnumerator RoundTrip()
    {
        if (_busy || !Ready("Main")) yield break;
        _busy = true;
        Managers.GameM.SwitchMainOrGameScene();
        yield return WaitForScene("Game");
        if (!Ready("Game") || Player.Instance != _originalPlayer || MonsterGenerator.Instance == null)
        { Mark("FAIL Game entry/owner"); _busy = false; yield break; }
        Mark("GAME_READY generator=present player=preserved");
        yield return new WaitForSecondsRealtime(10);
        Managers.GameM.SwitchMainOrGameScene();
        yield return WaitForScene("Main");
        if (!Ready("Main") || Player.Instance != _originalPlayer || Time.timeScale != 1 ||
            Managers.IAPM.Status != IAPStatus.Unavailable || Managers.GoogleAdMobM.CanRequestAds || _errors != 0)
        { Mark("FAIL return/guard/errors=" + _errors); _busy = false; yield break; }
        _roundTrips++;
        Mark("ROUNDTRIP_OK count=" + _roundTrips + " reads=" + RevivalGameplayIsolation.Reads +
            " writes=" + RevivalGameplayIsolation.Writes + " errors=" + _errors);
        _busy = false;
    }

    private void OnGUI()
    {
        GUI.depth = -1000;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1000f, Screen.width / 1000f, 1));
        GUILayout.BeginArea(new Rect(15, 15, 970, 225), GUI.skin.box);
        GUILayout.Label("OFFLINE TEST APP — original Main/Game scenes, synthetic account only");
        GUILayout.Label(_status + " | errors=" + _errors);
        GUILayout.Label(_lastObservation ?? "Waiting for player");
        GUI.enabled = !_busy && Ready("Main");
        if (GUILayout.Button("Repeat Main / Game / Main", GUILayout.Height(55))) StartCoroutine(RoundTrip());
        GUI.enabled = !_busy && Time.timeScale == 1 && Player.Instance != null && Player.Instance.Hp > 0 &&
            (Ready("Main") || Ready("Game"));
        if (GUILayout.Button(Ready("Game") ? "Return to Main (manual)" : "Enter Game (manual play)", GUILayout.Height(55)))
            StartCoroutine(ManualTransition());
        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private IEnumerator ManualTransition()
    {
        if (_busy || (!Ready("Main") && !Ready("Game"))) yield break;
        _busy = true;
        string destination = Ready("Main") ? "Game" : "Main";
        Managers.GameM.SwitchMainOrGameScene();
        yield return WaitForScene(destination);
        Mark(Ready(destination) && Player.Instance == _originalPlayer
            ? "MANUAL_READY " + destination + " use original gameplay controls"
            : "FAIL manual transition " + destination);
        _busy = false;
    }
}
#endif
