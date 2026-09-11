using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        Call(damage, "Clear");
        Assert.That(isActive.Invoke(null, new[] { sequence }), Is.False,
            "A pooled popup must not retain a completion callback from its old use.");
        Assert.That(Get(damage, "_effect"), Is.Null);
    }
}
