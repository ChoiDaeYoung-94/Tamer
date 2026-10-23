using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RevivalUILifecycleTests
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    GameObject root;
    Scene preview;

    static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    static object Call(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, Members).Invoke(instance, args);
    static void Set(object instance, string field, object value) =>
        instance.GetType().GetField(field, Members).SetValue(instance, value);
    static object Get(object instance, string field) =>
        instance.GetType().GetField(field, Members).GetValue(instance);

    [SetUp]
    public void SetUp()
    {
        preview = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Revival UI lifecycle fixture");
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, preview);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        EditorSceneManager.ClosePreviewScene(preview);
    }

    [Test]
    public void Revival_JoystickBeforePlayerInitializationDoesNotDereferenceCamera()
    {
        var joystick = root.AddComponent(Find("JoyStick"));
        Assert.DoesNotThrow(() => Call(joystick, "FixedUpdate"));
    }

    [TestCase("OnDisable")]
    [TestCase("OnApplicationFocus")]
    public void Revival_JoystickInterruptionClearsHeldDirection(string method)
    {
        var joystick = root.AddComponent(Find("JoyStick"));
        Set(joystick, "_joystickVector", Vector3.right);
        Set(joystick, "_joystickDistance", 50f);
        Set(joystick, "_isPointerUp", false);
        if (method == "OnDisable") Call(joystick, method);
        else Call(joystick, method, false);
        Assert.That(Get(joystick, "_joystickVector"), Is.EqualTo(Vector3.zero));
        Assert.That(Get(joystick, "_joystickDistance"), Is.EqualTo(0f));
        Assert.That(Get(joystick, "_isPointerUp"), Is.True);
    }

    [Test]
    public void Revival_PlayerCanvasDestroyRemovesBuffSubscriberFromOriginalPublisher()
    {
        var publisher = root.AddComponent(Find("AD.UpdateManager"));
        var canvas = root.AddComponent(Find("PlayerUICanvas"));
        var handler = Delegate.CreateDelegate(typeof(Action), canvas,
            canvas.GetType().GetMethod("UpdateBuffPanel", Members));
        publisher.GetType().GetEvent("OnUpdateEvent").AddEventHandler(publisher, handler);
        Set(canvas, "_updateManager", publisher);
        Call(canvas, "OnDestroy");
        Assert.That(Get(publisher, "OnUpdateEvent"), Is.Null);
    }

    [Test]
    public void Revival_CancelledCharacterMoveDoesNotTouchDestroyedTargets()
    {
        var selection = root.AddComponent(Find("CanvasSelectCharacter"));
        Set(selection, "_isMoving", true);
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();
            var task = Call(selection, "Move", source.Token);
            var awaiter = task.GetType().GetMethod("GetAwaiter").Invoke(task, null);
            Assert.DoesNotThrow(() => awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null));
            Assert.That(Get(selection, "_isMoving"), Is.False);
        }
    }

    [UnityTest]
    public IEnumerator Revival_CharacterSaveWaitRejectsChangedSessionWithoutReleasingItsPopup()
    {
        foreach (string change in new[] { "manager", "data", "server", "scene", "popup", "account",
            "generation", "epoch", "not-ready", "deletion", "receipt-only", "view" })
        {
            using (var f = new CharacterSaveFixture(root))
            {
                var save = f.Capture();
                f.BeginSave();
                var task = Call(f.Selection, "WaitForSaveAsync", save, "Man", CancellationToken.None);
                var awaiter = Call(task, "GetAwaiter");
                Assert.That(Completed(awaiter), Is.False, change);
                f.Change(change);
                var deadline = UnityEditor.EditorApplication.timeSinceStartup + 2;
                while (!Completed(awaiter) && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(Completed(awaiter), Is.True, change);
                Assert.That(Call(awaiter, "GetResult"), Is.False, change);
                Assert.That(f.Server.GetType().GetProperty("IsInProgress").GetValue(f.Server), Is.True,
                    "An invalid old view must stop waiting without cancelling the shared server: " + change);
                Call(f.Selection, "FinishSave", save);
                Assert.That(Get(f.Selection, "_isSaving"), Is.False, change);
                Assert.That(Get(f.CurrentPopup, "_isException"), Is.True,
                    "Old cleanup must preserve the new session's popup gate: " + change);
                Assert.That(Get(f.OriginalPopup, "_isException"), Is.True, change);
            }
        }
    }

    [UnityTest]
    public IEnumerator Revival_CharacterSaveWaitConfirmsOnlyMatchingSuccessAndAllowsRetryCleanup()
    {
        foreach (string outcome in new[] { "saved", "failed", "different-gender", "cancelled" })
        {
            using (var f = new CharacterSaveFixture(root))
            using (var source = new CancellationTokenSource())
            {
                var save = f.Capture();
                f.BeginSave();
                var task = Call(f.Selection, "WaitForSaveAsync", save, "Man", source.Token);
                var awaiter = Call(task, "GetAwaiter");
                Assert.That(Completed(awaiter), Is.False, outcome);
                if (outcome == "cancelled") source.Cancel();
                else if (outcome == "failed") Call(f.Server, "CancelPendingRequests");
                else f.CompleteSave(outcome == "saved" ? "Man" : "Woman");
                var deadline = UnityEditor.EditorApplication.timeSinceStartup + 2;
                while (!Completed(awaiter) && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(Completed(awaiter), Is.True, outcome);
                Assert.That(Call(awaiter, "GetResult"), Is.EqualTo(outcome == "saved"), outcome);
                Call(f.Selection, "FinishSave", save);
                Assert.That(Get(f.Selection, "_isSaving"), Is.False, outcome);
                Assert.That(Get(f.OriginalPopup, "_isException"), Is.False,
                    "The same current screen must be usable after failed/cancelled saves: " + outcome);
            }
        }
    }

    static bool Completed(object awaiter) => (bool)awaiter.GetType().GetProperty("IsCompleted").GetValue(awaiter);

    // Inactive preview objects and fake transport only: no Animator, login, files or native scene loads.
    sealed class CharacterSaveFixture : IDisposable
    {
        readonly GameObject host;
        readonly FieldInfo singleton = Find("AD.Managers").GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        readonly object previous;
        readonly List<IDisposable> servers = new List<IDisposable>();
        readonly Component manager, data;
        Action<Dictionary<string, string>> read;
        Action written;
        public readonly Component Selection, Scene, OriginalPopup;
        public readonly object Server;
        public Component CurrentPopup;

        public CharacterSaveFixture(GameObject root)
        {
            host = new GameObject("Character save context");
            host.SetActive(false);
            host.transform.SetParent(root.transform);
            previous = singleton.GetValue(null);
            manager = host.AddComponent(Find("AD.Managers"));
            data = NewData();
            Scene = host.AddComponent(Find("AD.SceneManager"));
            OriginalPopup = CurrentPopup = host.AddComponent(Find("AD.PopupManager"));
            Selection = host.AddComponent(Find("CanvasSelectCharacter"));
            Set(Selection, "_isSaving", true);
            Server = NewServer();
            Set(manager, "_dataM", data); Set(manager, "_serverM", Server);
            Set(manager, "_sceneM", Scene); Set(manager, "_popupM", OriginalPopup);
            singleton.SetValue(null, manager);
        }

        Component NewData()
        {
            var value = host.AddComponent(Find("AD.DataManager"));
            value.GetType().GetProperty("PlayFabId").SetValue(value, "A");
            value.GetType().GetProperty("IsServerDataReady").SetValue(value, true);
            Set(value, "LocalPlayerData", new Dictionary<string, string> { { "Sex", "Man" } });
            return value;
        }

        object NewServer()
        {
            var value = Activator.CreateInstance(Find("AD.ServerManager"), new object[]
            {
                (Func<string>)(() => "A"), (Func<bool>)(() => true),
                (Action<string, Action<Dictionary<string, string>>, Action<int>>)((account, ok, fail) => read = ok),
                (Action<string, Dictionary<string, string>, Action, Action<int>>)((account, patch, ok, fail) => written = ok),
                (Action<TimeSpan, Action>)((delay, action) => { }),
                (Action<Dictionary<string, string>, bool>)((values, update) => Set(data, "LocalPlayerData", values))
            });
            servers.Add((IDisposable)value);
            return value;
        }

        public object Capture() => Activator.CreateInstance(Selection.GetType().GetNestedType("CharacterSave", BindingFlags.NonPublic));
        public void BeginSave() => Call(Server, "SetData", new Dictionary<string, string> { { "Sex", "Man" } }, true, true, null);
        public void CompleteSave(string gender)
        {
            written(); // Complete the fake write, then its real ServerManager read-after-write continuation.
            read(new Dictionary<string, string> { { "Sex", gender } });
        }

        public void Change(string change)
        {
            if (change == "manager")
            {
                var replacement = host.AddComponent(Find("AD.Managers"));
                Set(replacement, "_dataM", data); Set(replacement, "_serverM", Server);
                Set(replacement, "_sceneM", Scene); Set(replacement, "_popupM", OriginalPopup);
                singleton.SetValue(null, replacement);
            }
            if (change == "data") Set(manager, "_dataM", NewData());
            if (change == "server") Set(manager, "_serverM", NewServer());
            if (change == "scene") Set(manager, "_sceneM", host.AddComponent(Find("AD.SceneManager")));
            if (change == "popup") Set(manager, "_popupM", CurrentPopup = host.AddComponent(Find("AD.PopupManager")));
            if (change == "account") data.GetType().GetProperty("PlayFabId").SetValue(data, "B");
            if (change == "generation") Set(data, "_accountGeneration", 1);
            if (change == "epoch") data.GetType().GetProperty("DeletionEpoch").SetValue(data, 1);
            if (change == "not-ready") data.GetType().GetProperty("IsServerDataReady").SetValue(data, false);
            if (change == "deletion") data.GetType().GetProperty("DeletionInProgress").SetValue(data, true);
            if (change == "receipt-only") data.GetType().GetProperty("ReceiptOnly", Members).SetValue(data, true);
            if (change == "view") UnityEngine.Object.DestroyImmediate(Selection);
        }

        public void Dispose()
        {
            singleton.SetValue(null, previous);
            foreach (var server in servers) server.Dispose();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Revival_DamageClearKillsOwnedSequenceBeforePoolReuse()
    {
        var damageObject = new GameObject("Damage", typeof(RectTransform));
        damageObject.SetActive(false);
        damageObject.transform.SetParent(root.transform);
        var text = damageObject.AddComponent(Find("TMPro.TextMeshProUGUI"));
        var damage = damageObject.AddComponent(Find("TMP_Damage"));
        Set(damage, "_thisTransform", damageObject.transform);
        Set(damage, "_thisText", text);
        Call(damage, "Awake");
        Call(damage, "Init", 10f);
        var sequence = Get(damage, "_effect");
        var isActive = Find("DG.Tweening.TweenExtensions").GetMethod("IsActive", BindingFlags.Static | BindingFlags.Public);
        Assert.That(isActive.Invoke(null, new[] { sequence }), Is.True);
        // DOTween intentionally skips Init outside Play mode; Kill is otherwise a
        // no-op in EditMode. Scope its runtime-ready flag to this synchronous test
        // without starting a player, account managers, or the global update loop.
        var initialized = Find("DG.Tweening.DOTween").GetField("initialized",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var previous = initialized.GetValue(null);
        try
        {
            initialized.SetValue(null, true);
            Call(damage, "Clear");
            Assert.That(isActive.Invoke(null, new[] { sequence }), Is.False,
                "A pooled popup must not retain a completion callback from its old use.");
            Assert.That(Get(damage, "_effect"), Is.Null);
        }
        finally
        {
            Find("DG.Tweening.TweenExtensions").GetMethod("Kill", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { sequence, false });
            initialized.SetValue(null, previous);
        }
    }
}
