// These are EditMode behavioral tests. The real PopupObject handlers are invoked
// explicitly on disabled components, so no test depends on Play-mode event dispatch.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AD.Advertising;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RevivalAdPopupTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private FieldInfo singletonField;
    private object previousSingleton;
    private Type popupManagerType;
    private Type popupObjectType;
    private Component popupManager;
    private GameObject fixtureRoot;
    private GameObject inactiveManagersRoot;

    private Scene testScene;
    private bool capturedSingleton;

    [SetUp]
    public void SetUp()
    {
        var managersType = FindType("AD.Managers");
        popupManagerType = FindType("AD.PopupManager");
        popupObjectType = FindType("AD.PopupObject");
        singletonField = managersType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        previousSingleton = singletonField.GetValue(null);
        capturedSingleton = true;

        testScene = EditorSceneManager.NewPreviewScene();


        fixtureRoot = new GameObject("Revival isolated popup fixture");
        SceneManager.MoveGameObjectToScene(fixtureRoot, testScene);
        popupManager = fixtureRoot.AddComponent(popupManagerType);

        inactiveManagersRoot = new GameObject("Revival inactive Managers bridge");
        inactiveManagersRoot.SetActive(false);
        inactiveManagersRoot.transform.SetParent(fixtureRoot.transform, false);
        // Never activate this object or call Managers/PopupManager.Init: its Awake
        // path initializes account/game managers and is outside this fixture.
        var managers = inactiveManagersRoot.AddComponent(managersType);
        Assert.That(inactiveManagersRoot.activeInHierarchy, Is.False);
        Assert.That(singletonField.GetValue(null), Is.SameAs(previousSingleton),
            "Adding the inactive bridge must not execute Managers.Awake.");
        managersType.GetField("_popupM", InstanceMembers).SetValue(managers, popupManager);
        singletonField.SetValue(null, managers);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (fixtureRoot != null) UnityEngine.Object.DestroyImmediate(fixtureRoot);


            if (testScene.IsValid() && testScene.isLoaded)
                EditorSceneManager.ClosePreviewScene(testScene);
        }
        finally
        {
            // Managers.OnDestroy can clear the static field. Restore only after
            // all fixture objects and their lifecycle callbacks have been removed.
            if (capturedSingleton) singletonField.SetValue(null, previousSingleton);
            capturedSingleton = false;
        }
    }

    [Test]
    public void Revival_CloseTargetRemovesPopupObjectRegistration()
    {
        var heal = CreatePopup("Heal");
        ActivatePopup(heal);
        AssertStack(heal);

        ClosePopup(heal);

        Assert.That(heal.activeSelf, Is.False);
        AssertStack();
    }

    [Test]
    public void Revival_CloseTargetRemovesAllDuplicatesAndPreservesOtherPopupOrder()
    {
        var bottom = CreatePopup("Bottom");
        var heal = CreatePopup("Heal");
        var middle = CreatePopup("Middle");
        var top = CreatePopup("Top");
        ActivatePopup(bottom);
        ActivatePopup(heal);
        ActivatePopup(middle);
        HidePopup(heal);
        ActivatePopup(heal);
        ActivatePopup(top);
        AssertStack(top, heal, middle, bottom);

        ClosePopup(heal);

        Assert.That(heal.activeSelf, Is.False);
        Assert.That(top.activeSelf && middle.activeSelf && bottom.activeSelf, Is.True);
        AssertStack(top, middle, bottom);
    }

    [Test]
    public void Revival_LateRewardRemovesReopenedHealEntriesWithoutClosingNewTopPopup()
    {
        var bottom = CreatePopup("Bottom");
        var heal = CreatePopup("Heal");
        var newerPopup = CreatePopup("Opened after ad close");
        ActivatePopup(bottom);
        ActivatePopup(heal);
        HidePopup(heal);
        var rewards = 0;
        var completions = 0;
        var receipt = new RewardedAdSession(() => true, () =>
        {
            rewards++;
            ClosePopup(heal);
        }, outcome =>
        {
            Assert.That(outcome, Is.EqualTo(RewardedAdOutcome.Cancelled));
            completions++;
            ActivatePopup(heal);
        });

        receipt.Complete(RewardedAdOutcome.Cancelled);
        ActivatePopup(newerPopup);
        AssertStack(newerPopup, heal, bottom);
        receipt.EarnReward();
        receipt.EarnReward();

        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(heal.activeSelf, Is.False);
        Assert.That(newerPopup.activeSelf, Is.True);
        AssertStack(newerPopup, bottom);
    }

    [Test]
    public void Revival_RepeatedReopenAndLateCloseDoesNotAccumulateGhostEntries()
    {
        var bottom = CreatePopup("Bottom");
        var top = CreatePopup("Top");
        var heal = CreatePopup("Heal");
        ActivatePopup(bottom);
        ActivatePopup(top);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            ActivatePopup(heal);
            HidePopup(heal);
            ActivatePopup(heal);
            AssertStack(heal, top, bottom);

            ClosePopup(heal);
            ClosePopup(heal);

            Assert.That(heal.activeSelf, Is.False);
            AssertStack(top, bottom);
        }
    }

    [Test]
    public void Revival_CloseUnregisteredTargetPreservesRegisteredPopups()
    {
        var top = CreatePopup("Top");
        var unregistered = CreatePopup("Unregistered");
        ActivatePopup(top);
        unregistered.SetActive(true);

        ClosePopup(unregistered);

        Assert.That(unregistered.activeSelf, Is.False);
        Assert.That(top.activeSelf, Is.True);
        AssertStack(top);
    }

    [Test]
    public void Revival_CloseNullTargetDoesNotChangeStackOrVisibility()
    {
        var top = CreatePopup("Top");
        ActivatePopup(top);

        Invoke(popupManager, "ClosePopup", new object[] { null });

        Assert.That(top.activeSelf, Is.True);
        AssertStack(top);
    }

    [Test]
    public void Revival_ExternalDisableRemovesOnlyItsRegistration()
    {
        var bottom = CreatePopup("Bottom");
        var top = CreatePopup("Top");
        ActivatePopup(bottom);
        ActivatePopup(top);
        HidePopup(bottom);
        AssertStack(top);
        Assert.That(top.activeSelf, Is.True);
    }

    [Test]
    public void Revival_PopupButtonClosesItsOwnerInsteadOfNewerTop()
    {
        Invoke(popupManager, "ReleaseException");
        var bottom = CreatePopup("Bottom");
        var top = CreatePopup("Top");
        ActivatePopup(bottom);
        ActivatePopup(top);
        Invoke(bottom.GetComponent(popupObjectType), "DisablePop");
        Assert.That(bottom.activeSelf, Is.False);
        Assert.That(top.activeSelf, Is.True);
        AssertStack(top);
    }

    [Test]
    public void Revival_RepeatedRegistrationIsUnique()
    {
        var popup = CreatePopup("Repeated");
        ActivatePopup(popup);
        Invoke(popup.GetComponent(popupObjectType), "OnEnable");
        AssertStack(popup);
    }

    [TestCase(1, "IsException")]
    [TestCase(2, "IsFlow")]
    public void Revival_OverlappingBlockersReleaseOnlyTheirOwnLease(int kind, string property)
    {
        Invoke(popupManager, "ReleaseException");
        var first = CreatePopup("First blocker");
        var second = CreatePopup("Second blocker");
        var field = popupObjectType.GetField("_checkType", InstanceMembers);
        field.SetValue(first.GetComponent(popupObjectType), Enum.ToObject(field.FieldType, kind));
        field.SetValue(second.GetComponent(popupObjectType), Enum.ToObject(field.FieldType, kind));
        ActivatePopup(first);
        ActivatePopup(second);
        HidePopup(first);
        Assert.That(popupManagerType.GetProperty(property, InstanceMembers).GetValue(popupManager), Is.True);
        HidePopup(second);
        Assert.That(popupManagerType.GetProperty(property, InstanceMembers).GetValue(popupManager), Is.False);
    }

    [Test]
    public void Revival_DisableUsesOriginalManagerAfterSingletonIsCleared()
    {
        var popup = CreatePopup("Teardown");
        ActivatePopup(popup);
        singletonField.SetValue(null, null);
        HidePopup(popup);
        AssertStack();
    }

    [Test]
    public void Revival_ResetClosesAllPopupsAndToleratesDestroyedEntries()
    {
        var first = CreatePopup("Destroyed");
        var second = CreatePopup("Active");
        ActivatePopup(first);
        ActivatePopup(second);
        UnityEngine.Object.DestroyImmediate(first);
        Invoke(popupManager, "SetPopup");
        Assert.That(second.activeSelf, Is.False);
        AssertStack();
    }

    [Test]
    public void Revival_PendingSceneRejectsDuplicateTargetAndGoScene()
    {
        var sceneType = FindType("AD.SceneManager");
        var scene = fixtureRoot.AddComponent(sceneType);
        var targetField = sceneType.GetField("_scene", InstanceMembers);
        var original = Enum.Parse(targetField.FieldType, "Main");
        targetField.SetValue(scene, original);
        sceneType.GetProperty("IsTransitioning").SetValue(scene, true);
        var source = new System.Threading.CancellationTokenSource();
        sceneType.GetField("_ctsGoScene", InstanceMembers).SetValue(scene, source);
        try
        {
            // No Sound/Data/Server bridge exists: accepted duplicate work would fail.
            Invoke(scene, "NextScene", Enum.Parse(targetField.FieldType, "Game"));
            Invoke(scene, "GoScene");
            Assert.That(targetField.GetValue(scene), Is.EqualTo(original));
            Assert.That(sceneType.GetField("_ctsGoScene", InstanceMembers).GetValue(scene), Is.SameAs(source));
            Invoke(scene, "OnDestroy");
            Assert.That(source.IsCancellationRequested, Is.True);
        }
        finally
        {
            sceneType.GetField("_ctsGoScene", InstanceMembers).SetValue(scene, null);
            source.Dispose();
        }
    }

    [Test]
    public void Revival_CancelledSceneLoadDoesNotStartNativeLoading()
    {
        var sceneType = FindType("AD.SceneManager");
        var scene = fixtureRoot.AddComponent(sceneType);
        var targetType = sceneType.GetField("_scene", InstanceMembers).FieldType;
        using (var source = new System.Threading.CancellationTokenSource())
        {
            source.Cancel();
            // Invalid target would log a Unity scene-load error if reached.
            var task = Invoke(scene, "LoadTargetSceneAsync", Enum.ToObject(targetType, 999), source.Token, (Func<bool>)(() => true));
            var awaiter = task.GetType().GetMethod("GetAwaiter").Invoke(task, null);
            Assert.That(awaiter.GetType().GetProperty("IsCompleted").GetValue(awaiter), Is.True);
            var error = Assert.Throws<TargetInvocationException>(() =>
                awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null));
            Assert.That(error.InnerException, Is.InstanceOf<OperationCanceledException>());
        }
    }

    [Test]
    public void Revival_UserCloseHonorsExceptionButRewardCleanupCanCloseTarget()
    {
        var popup = CreatePopup("Blocked");
        ActivatePopup(popup);
        Invoke(popup.GetComponent(popupObjectType), "DisablePop");
        Assert.That(popup.activeSelf, Is.True);
        AssertStack(popup);
        ClosePopup(popup);
        AssertStack();
    }

    [UnityTest]
    public IEnumerator Revival_ServerWaitStopsWhenCapturedManagersIsReplaced() => WaitForLostServices(false);

    [UnityTest]
    public IEnumerator Revival_ServerWaitStopsWhenCapturedDataIsDestroyed() => WaitForLostServices(true);

    private IEnumerator WaitForLostServices(bool destroyData)
    {
        var managers = (Component)singletonField.GetValue(null);
        var managersType = managers.GetType();
        var data = inactiveManagersRoot.AddComponent(FindType("AD.DataManager"));
        var sound = inactiveManagersRoot.AddComponent(FindType("AD.SoundManager"));
        managersType.GetField("_dataM", InstanceMembers).SetValue(managers, data);
        managersType.GetField("_soundM", InstanceMembers).SetValue(managers, sound);
        var server = managersType.GetField("_serverM", InstanceMembers).GetValue(managers);
        var operationType = server.GetType().GetNestedType("Operation", BindingFlags.NonPublic);
        server.GetType().GetField("_active", InstanceMembers).SetValue(server,
            Activator.CreateInstance(operationType, true));
        var sceneType = FindType("AD.SceneManager");
        var scene = fixtureRoot.AddComponent(sceneType);
        var servicesType = sceneType.GetNestedType("SceneServices", BindingFlags.NonPublic);
        var services = Activator.CreateInstance(servicesType, new object[] { managers });
        var task = Invoke(scene, "WaitForServerAsync", services, System.Threading.CancellationToken.None);
        var awaiter = task.GetType().GetMethod("GetAwaiter").Invoke(task, null);
        var completed = awaiter.GetType().GetProperty("IsCompleted");
        Assert.That(completed.GetValue(awaiter), Is.False);
        if (destroyData) UnityEngine.Object.DestroyImmediate(data);
        else
        {
            // New inactive owner has no service fields. Re-querying it would dereference null.
            var replacementRoot = new GameObject("Replacement inactive Managers");
            replacementRoot.SetActive(false);
            replacementRoot.transform.SetParent(fixtureRoot.transform);
            singletonField.SetValue(null, replacementRoot.AddComponent(managersType));
        }
        var deadline = UnityEditor.EditorApplication.timeSinceStartup + 2;
        while (!(bool)completed.GetValue(awaiter) && UnityEditor.EditorApplication.timeSinceStartup < deadline)
            yield return null;
        Assert.That(completed.GetValue(awaiter), Is.True);
        Assert.That(awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null), Is.False);
        Assert.That(server.GetType().GetField("_active", InstanceMembers).GetValue(server), Is.Not.Null,
            "Ownership loss stops waiting without modifying the old server request.");
    }

    private GameObject CreatePopup(string name)
    {
        var popup = new GameObject("Revival " + name);
        popup.SetActive(false);
        popup.transform.SetParent(fixtureRoot.transform, false);
        // Explicit handler dispatch avoids EditMode/PlayMode differences and double
        // registration. _checkType stays its real default, Normal.
        var behavior = (Behaviour)popup.AddComponent(popupObjectType);
        behavior.enabled = false;
        return popup;
    }

    private void ActivatePopup(GameObject popup)
    {
        if (popup.activeSelf) return;
        popup.SetActive(true);
        Invoke(popup.GetComponent(popupObjectType), "OnEnable");
    }

    private void HidePopup(GameObject popup)
    {
        popup.SetActive(false);
        // Mirror the runtime lifecycle after an external SetActive(false).
        Invoke(popup.GetComponent(popupObjectType), "OnDisable");
    }

    private void ClosePopup(GameObject popup)
    {
        Invoke(popupManager, "ClosePopup", popup);
        // Mirror actual PopupObject.Normal.OnDisable after SetActive(false), proving
        // that its behavior does not remove an unrelated top popup after filtering.
        if (popup != null) Invoke(popup.GetComponent(popupObjectType), "OnDisable");
    }

    private void AssertStack(params GameObject[] expectedTopFirst)
    {
        var stack = (Stack<GameObject>)popupManagerType.GetField("_popupStack", InstanceMembers).GetValue(popupManager);
        Assert.That(stack.ToArray(), Is.EqualTo(expectedTopFirst));
        Assert.That(inactiveManagersRoot.activeInHierarchy, Is.False,
            "The account/game manager bridge must remain inactive throughout the test.");
    }

    private static object Invoke(Component component, string method, params object[] arguments)
    {
        var member = component.GetType().GetMethod(method, InstanceMembers);
        Assert.That(member, Is.Not.Null, "Required runtime method: " + method);
        try { return member.Invoke(component, arguments); }
        catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
    }

    private static Type FindType(string name)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Required runtime type: " + name);
        return type;
    }
}
