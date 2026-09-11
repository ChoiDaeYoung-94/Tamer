// These are EditMode behavioral tests. The real PopupObject handlers are invoked
// explicitly on disabled components, so no test depends on Play-mode event dispatch.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AD.Advertising;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        HideWithoutRemovingStack(heal);
        ActivatePopup(heal);
        ActivatePopup(top);
        AssertStack(top, heal, middle, heal, bottom);

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
        HideWithoutRemovingStack(heal);
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
        AssertStack(newerPopup, heal, heal, bottom);
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
            HideWithoutRemovingStack(heal);
            ActivatePopup(heal);
            AssertStack(heal, heal, top, bottom);

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

    private void HideWithoutRemovingStack(GameObject popup)
    {
        popup.SetActive(false);
        // The current Normal handler deliberately performs no stack mutation; this
        // recreates the old SetActive(false) path that left stale stack entries.
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
