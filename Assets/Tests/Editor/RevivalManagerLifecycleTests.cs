using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Preview objects never run service initialization, login, or persistent-file loading.
public class RevivalManagerLifecycleTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Type _type;
    private FieldInfo _singleton;
    private object _previous;
    private Scene _scene;
    private Component _owner;
    private Component _duplicate;

    [SetUp]
    public void SetUp()
    {
        _type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Managers")).First(t => t != null);
        _singleton = _type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        _previous = _singleton.GetValue(null);
        _singleton.SetValue(null, null);
        _scene = EditorSceneManager.NewPreviewScene();
        _owner = Create("Owner");
        _duplicate = Create("Duplicate");
    }

    private Component Create(string name)
    {
        var go = new GameObject("Revival service " + name);
        go.SetActive(false);
        SceneManager.MoveGameObjectToScene(go, _scene);
        return go.AddComponent(_type);
    }

    private object Call(Component target, string name) => _type.GetMethod(name, Members).Invoke(target, null);
    private object Service(string name) => _type.GetProperty(name).GetValue(null);

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (_owner != null) Call(_owner, "Shutdown");
            if (_duplicate != null) Call(_duplicate, "Shutdown");
            EditorSceneManager.ClosePreviewScene(_scene);
        }
        finally { _singleton.SetValue(null, _previous); }
    }

    [Test]
    public void Revival_GameOverGoLobbyDoesNotResetPlayerDuringTransition()
    {
        var sceneType = _type.Assembly.GetType("AD.SceneManager");
        var scene = _owner.gameObject.AddComponent(sceneType);
        _type.GetField("_sceneM", Members).SetValue(_owner, scene);
        Call(_owner, "TryClaimInstance");
        sceneType.GetProperty("IsTransitioning").GetSetMethod(true).Invoke(scene, new object[] { true });
        var gameType = _type.Assembly.GetType("AD.GameManager");
        var game = Activator.CreateInstance(gameType);
        // No Player or scene UI exists: reaching reset/transition would fail.
        Assert.DoesNotThrow(() => gameType.GetMethod("GameOverGoLobby").Invoke(game, null));
        Assert.That(gameType.GetProperty("IsGame").GetValue(game), Is.False);
    }

    [Test]
    public void Revival_Managers_DuplicateCannotReplaceOrShutdownLiveOwner()
    {
        Assert.That(Call(_owner, "TryClaimInstance"), Is.True);
        object iap = Service("IAPM");
        Assert.That(Call(_duplicate, "TryClaimInstance"), Is.False);
        Call(_duplicate, "Shutdown");
        Assert.That(Service("Instance"), Is.SameAs(_owner));
        Assert.That(Service("IAPM"), Is.SameAs(iap));
        Assert.That(iap.GetType().GetField("_disposed", Members).GetValue(iap), Is.False);
    }

    [Test]
    public void Revival_Managers_ShutdownClearsAccessAndCannotReclaimOwner()
    {
        Call(_owner, "TryClaimInstance");
        object iap = Service("IAPM");
        object pool = Service("PoolM");
        Call(_owner, "Shutdown");
        Call(_owner, "Shutdown");
        Assert.That(Call(_owner, "TryClaimInstance"), Is.False);
        foreach (string property in new[] { "Instance", "DataM", "ServerM", "PoolM", "IAPM", "UpdateM", "ResourceM" })
            Assert.That(Service(property), Is.Null, property);
        Assert.That(iap.GetType().GetField("_disposed", Members).GetValue(iap), Is.True);
        Assert.That(pool.GetType().GetMethod("PopFromPool").Invoke(pool, new object[] { "closed", null }), Is.Null);
        Assert.That(Call(_duplicate, "TryClaimInstance"), Is.True);
        Assert.That(Service("Instance"), Is.SameAs(_duplicate));
    }

    [Test]
    public void Revival_Managers_ShutdownCancelsOwnedServerWork()
    {
        Call(_owner, "TryClaimInstance");
        var serverType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.ServerManager")).First(t => t != null);
        Action<System.Collections.Generic.Dictionary<string, string>> late = null;
        int applied = 0;
        var server = Activator.CreateInstance(serverType, new object[] {
            (Func<string>)(() => "synthetic-owner"), (Func<bool>)(() => true),
            (Action<string, Action<System.Collections.Generic.Dictionary<string, string>>, Action<int>>)((id, ok, fail) => late = ok),
            (Action<string, System.Collections.Generic.Dictionary<string, string>, Action, Action<int>>)((id, data, ok, fail) => {}),
            (Action<TimeSpan, Action>)((delay, action) => {}),
            (Action<System.Collections.Generic.Dictionary<string, string>, bool>)((data, update) => applied++) });
        _type.GetField("_serverM", Members).SetValue(_owner, server);
        serverType.GetMethod("GetAllData").Invoke(server, new object[] { true });
        Call(_owner, "Shutdown");
        late(new System.Collections.Generic.Dictionary<string, string>());
        Assert.That(applied, Is.Zero);
        Assert.That(serverType.GetProperty("IsInProgress").GetValue(server), Is.False);
    }

    [Test]
    public void Revival_LoginDestructionSuspendsCapturedDataInsteadOfReplacement()
    {
        var dataType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.DataManager")).First(t => t != null);
        var loginType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Login")).First(t => t != null);
        var oldData = _owner.gameObject.AddComponent(dataType);
        var newData = _duplicate.gameObject.AddComponent(dataType);
        var setReady = dataType.GetProperty("IsServerDataReady").GetSetMethod(true);
        setReady.Invoke(oldData, new object[] { true });
        setReady.Invoke(newData, new object[] { true });
        _type.GetField("_dataM", Members).SetValue(_duplicate, newData);
        Call(_duplicate, "TryClaimInstance");
        var login = _owner.gameObject.AddComponent(loginType);
        loginType.GetField("_dataOwner", Members).SetValue(login, oldData);
        loginType.GetMethod("OnDestroy", Members).Invoke(login, null);
        Assert.That(dataType.GetProperty("IsServerDataReady").GetValue(oldData), Is.False);
        Assert.That(dataType.GetProperty("IsServerDataReady").GetValue(newData), Is.True);
    }

    [Test]
    public void Revival_ServerProductionBindingDoesNotFollowSingletonReplacement()
    {
        var dataType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.DataManager")).First(t => t != null);
        var serverType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.ServerManager")).First(t => t != null);
        var oldData = _owner.gameObject.AddComponent(dataType);
        var newData = _duplicate.gameObject.AddComponent(dataType);
        dataType.GetProperty("PlayFabId").GetSetMethod(true).Invoke(oldData, new object[] { "synthetic-old" });
        dataType.GetProperty("PlayFabId").GetSetMethod(true).Invoke(newData, new object[] { "synthetic-new" });
        var server = Activator.CreateInstance(serverType, new object[] { oldData });
        try
        {
            _type.GetField("_dataM", Members).SetValue(_duplicate, newData);
            Call(_duplicate, "TryClaimInstance");
            var account = (Func<string>)serverType.GetField("_accountId", Members).GetValue(server);
            Assert.That(account(), Is.EqualTo("synthetic-old"));
            var apply = (Action<System.Collections.Generic.Dictionary<string, string>, bool>)
                serverType.GetField("_applyRead", Members).GetValue(server);
            apply(new System.Collections.Generic.Dictionary<string, string> { { "Gold", "100" } }, false);
            Assert.That(dataType.GetField("PlayFabPlayerData").GetValue(oldData), Is.Not.Null);
            Assert.That(dataType.GetField("PlayFabPlayerData").GetValue(newData), Is.Null);
        }
        finally { ((IDisposable)server).Dispose(); }
    }
}
