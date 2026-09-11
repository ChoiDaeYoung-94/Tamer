using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AD.Advertising;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Exercises the manager's lifecycle with fake receipts and audio callbacks only.
/// No Managers prefab, account initialization, or real/sample ad object is created.
/// </summary>
[NonParallelizable]
public class RevivalAdManagerTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Type managerType;
    private Component manager;
    private Scene previousScene;
    private Scene testScene;
    private Scene changedScene;

    [SetUp]
    public void SetUp()
    {
        managerType = FindType("AD.GoogleAdMobManager");
        changedScene = default;
        previousScene = SceneManager.GetActiveScene();
        testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
        var root = new GameObject("Revival isolated ad manager");
        SceneManager.MoveGameObjectToScene(root, testScene);
        manager = root.AddComponent(managerType);
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null)
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);
        if (changedScene.IsValid() && changedScene.isLoaded)
            EditorSceneManager.CloseScene(changedScene, true);
        if (testScene.IsValid() && testScene.isLoaded)
            EditorSceneManager.CloseScene(testScene, true);
    }

    [TestCase(RewardedAdOutcome.Cancelled, true, RewardedAdOutcome.Rewarded)]
    [TestCase(RewardedAdOutcome.Cancelled, false, RewardedAdOutcome.Cancelled)]
    [TestCase(RewardedAdOutcome.Failed, true, RewardedAdOutcome.Failed)]
    public void Revival_CloseOrFailureRestoresAudioBeforeSettlingOnce(
        RewardedAdOutcome closeOutcome, bool earned, RewardedAdOutcome expected)
    {
        var calls = new List<string>();
        var session = new RewardedAdSession(() => true, () =>
        {
            AssertCleared();
            calls.Add("reward");
        }, outcome =>
        {
            AssertCleared();
            Assert.That(outcome, Is.EqualTo(expected));
            calls.Add("finished");
        });
        if (earned) session.EarnReward();
        Install(session, () =>
        {
            AssertCleared();
            calls.Add("resume");
        });

        Invoke("CompleteSession", session, closeOutcome);
        Invoke("CompleteSession", session, closeOutcome);

        Assert.That(session.IsCompleted, Is.True);
        Assert.That(calls, Is.EqualTo(expected == RewardedAdOutcome.Rewarded
            ? new[] { "resume", "reward", "finished" }
            : new[] { "resume", "finished" }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Revival_RewardAndCloseInEitherCallbackOrderSettleAfterQueueDrain(bool rewardFirst)
    {
        var rewards = 0;
        var resumes = 0;
        var completions = 0;
        var closeKeptLock = false;
        var rewardKeptLock = false;
        var result = RewardedAdOutcome.Failed;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            result = outcome;
            completions++;
        });
        Install(session, () => resumes++);
        Action reward = () =>
        {
            session.EarnReward();
            rewardKeptLock = !session.IsCompleted && Property<bool>("IsInProgress");
        };
        Action close = () =>
        {
            Invoke("QueueClose", session);
            closeKeptLock = !session.IsCompleted && Property<bool>("IsInProgress");
        };
        Enqueue(rewardFirst ? reward : close);
        Enqueue(rewardFirst ? close : reward);

        Invoke("Update");
        Assert.That(closeKeptLock, Is.True);
        Assert.That(rewardKeptLock, Is.True);
        // Duplicate callbacks in a later frame cannot pay or resume again.
        Enqueue(close);
        Enqueue(reward);
        Invoke("Update");

        AssertCleared();
        Assert.That(session.IsCompleted, Is.True);
        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(RewardedAdOutcome.Rewarded));
    }

    [Test]
    public void Revival_CloseThenFailureThenRewardInSameQueueCannotGrantReward()
    {
        var rewards = 0;
        var resumes = 0;
        var completions = 0;
        var result = RewardedAdOutcome.Rewarded;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            result = outcome;
            completions++;
        });
        Install(session, () => resumes++);
        Enqueue(() => Invoke("QueueClose", session));
        Enqueue(() => Invoke("CompleteSession", session, RewardedAdOutcome.Failed));
        Enqueue(session.EarnReward);

        Invoke("Update");
        Invoke("Update");

        AssertCleared();
        Assert.That(session.IsCompleted, Is.True);
        Assert.That(rewards, Is.Zero);
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(RewardedAdOutcome.Failed));
    }

    [Test]
    public void Revival_RewardInLaterFrameCannotReviveCompletedClose()
    {
        var rewards = 0;
        var resumes = 0;
        var completions = 0;
        var result = RewardedAdOutcome.Rewarded;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            result = outcome;
            completions++;
        });
        Install(session, () => resumes++);
        Enqueue(() => Invoke("QueueClose", session));
        Invoke("Update");

        Assert.That(session.IsCompleted, Is.True);
        Enqueue(session.EarnReward);
        Invoke("Update");

        AssertCleared();
        Assert.That(rewards, Is.Zero);
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(RewardedAdOutcome.Cancelled));
    }

    [Test]
    public void Revival_RewardExceptionCannotLeaveAudioOrSessionLocked()
    {
        var resumes = 0;
        var completions = 0;
        var failure = new InvalidOperationException("Synthetic reward failure.");
        var session = new RewardedAdSession(() => true, () => { throw failure; }, _ => completions++);
        session.EarnReward();
        Install(session, () => resumes++);

        Assert.That(Assert.Throws<InvalidOperationException>(() =>
            Invoke("CompleteSession", session, RewardedAdOutcome.Cancelled)), Is.SameAs(failure));
        Invoke("CompleteSession", session, RewardedAdOutcome.Cancelled);

        AssertCleared();
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void Revival_AudioRestoreExceptionStillSettlesSessionOnce()
    {
        var resumes = 0;
        var rewards = 0;
        var completions = 0;
        var failure = new InvalidOperationException("Synthetic audio failure.");
        var session = new RewardedAdSession(() => true, () => rewards++, _ => completions++);
        session.EarnReward();
        Install(session, () => { resumes++; throw failure; });

        Assert.That(Assert.Throws<InvalidOperationException>(() =>
            Invoke("CompleteSession", session, RewardedAdOutcome.Cancelled)), Is.SameAs(failure));
        Invoke("CompleteSession", session, RewardedAdOutcome.Cancelled);

        AssertCleared();
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void Revival_StaleCloseCannotSettleNewSessionOrResumeItsAudio()
    {
        var oldRewards = 0;
        var oldCompletions = 0;
        var newRewards = 0;
        var newCompletions = 0;
        var resumes = 0;
        var oldSession = new RewardedAdSession(() => true, () => oldRewards++, _ => oldCompletions++);
        oldSession.EarnReward();
        var newSession = new RewardedAdSession(() => true, () => newRewards++, _ => newCompletions++);
        newSession.EarnReward();
        Install(newSession, () => resumes++);

        Invoke("CompleteSession", oldSession, RewardedAdOutcome.Cancelled);

        Assert.That(Get<RewardedAdSession>("_session"), Is.SameAs(newSession));
        Assert.That(newSession.IsCompleted, Is.False);
        Assert.That(Property<bool>("IsInProgress"), Is.True);
        Assert.That(resumes, Is.Zero);
        Assert.That(oldRewards, Is.Zero);
        Assert.That(oldCompletions, Is.Zero);
        Assert.That(newRewards, Is.Zero);
        Assert.That(newCompletions, Is.Zero);

        Invoke("CompleteSession", newSession, RewardedAdOutcome.Cancelled);
        Invoke("CompleteSession", oldSession, RewardedAdOutcome.Failed);

        AssertCleared();
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(newRewards, Is.EqualTo(1));
        Assert.That(newCompletions, Is.EqualTo(1));
        Assert.That(oldRewards, Is.Zero);
        Assert.That(oldCompletions, Is.Zero);
    }

    [Test]
    public void Revival_ManagerDestructionReleasesAudioWithoutGrantingEarnedReward()
    {
        var resumes = 0;
        var rewards = 0;
        var completions = 0;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            Assert.That(outcome, Is.EqualTo(RewardedAdOutcome.Failed));
            completions++;
        });
        session.EarnReward();
        Install(session, () => resumes++);
        Invoke("Init");

        UnityEngine.Object.DestroyImmediate(manager.gameObject);

        Assert.That(session.IsCompleted, Is.True);
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
        session.EarnReward();
        session.Complete(RewardedAdOutcome.Rewarded);
        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void Revival_MissingManagersHasNoAdsIsSafelyFalse()
    {
        WithoutManagers(() => Assert.That(Property<bool>("HasNoAds"), Is.False));
    }

    [Test]
    public void Revival_DestructionDiscardsPendingAndLateCallbacksWithoutRunningThem()
    {
        var runs = 0;
        var discards = 0;
        Invoke("Enqueue", (Action)(() => runs++), (Action)(() => discards++));

        // Invoke the lifecycle method directly to retain the managed component for
        // a synthetic late callback after destruction; no ad object is involved.
        Invoke("OnDestroy");
        Invoke("Enqueue", (Action)(() => runs++), (Action)(() => discards++));
        Invoke("OnDestroy");

        Assert.That(Get<bool>("_destroyed"), Is.True);
        Assert.That(runs, Is.Zero);
        Assert.That(discards, Is.EqualTo(2));
    }

    [Test]
    public void Revival_InitSubscribesWithoutInitializingOrLoadingAds()
    {
        Invoke("Init");
        Invoke("Init");

        Assert.That(Get<bool>("_subscribed"), Is.True);
        AssertNoSdkActivity();
        AssertCleared();
    }

    [Test]
    public void Revival_BatchPolicyRejectsPublicAdEntryPointsBeforeSdkInitialization()
    {
        // Never call these entry points in an interactive Editor, where sample ads
        // are allowed. The pure policy suite covers all environments separately.
        if (!Application.isBatchMode)
            Assert.Ignore("The public batch-mode request guard is verified only in a batch Editor.");

        Assert.That(Property<bool>("CanRequestAds"), Is.False);
        var rewards = 0;
        var completions = 0;
        WithoutManagers(() =>
        {
            Invoke("LoadRewardedAd");
            var accepted = (bool)Invoke("ShowRewardedAd", manager,
                (Action)(() => rewards++), (Action<RewardedAdOutcome>)(outcome =>
                {
                    Assert.That(outcome, Is.EqualTo(RewardedAdOutcome.PolicyBlocked));
                    completions++;
                }));
            Assert.That(accepted, Is.True);
        });

        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
        AssertNoSdkActivity();
        AssertCleared();
    }

    [Test]
    public void Revival_SceneChangeInvalidatesRewardButKeepsLockUntilNativeClose()
    {
        var originalHandle = testScene.handle;
        var resumes = 0;
        var rewards = 0;
        var completions = 0;
        var session = new RewardedAdSession(
            () => SceneManager.GetActiveScene().handle == originalHandle,
            () => rewards++, _ => completions++);
        session.EarnReward();
        Install(session, () => resumes++);
        Invoke("Init");

        changedScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(changedScene);
        // The runtime activeSceneChanged event is Play-mode-only. Deliver the same
        // handler after changing real scene handles in this EditMode test.
        Invoke("OnSceneChanged", testScene, changedScene);

        Assert.That(session.IsCompleted, Is.True);
        Assert.That(Get<RewardedAdSession>("_session"), Is.SameAs(session));
        Assert.That(Property<bool>("IsInProgress"), Is.True);
        Assert.That(resumes, Is.Zero);
        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.Zero);

        Invoke("CompleteSession", session, RewardedAdOutcome.Cancelled);

        AssertCleared();
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.Zero);
    }

    private void Install(RewardedAdSession session, Action resume)
    {
        managerType.GetField("_session", InstanceMembers).SetValue(manager, session);
        managerType.GetField("_resumeBgm", InstanceMembers).SetValue(manager, resume);
    }

    private void AssertCleared()
    {
        Assert.That(Property<bool>("IsInProgress"), Is.False);
        Assert.That(Get<RewardedAdSession>("_closingSession"), Is.Null);
        Assert.That(Get<object>("_showingAd"), Is.Null);
        Assert.That(Get<Action>("_resumeBgm"), Is.Null);
    }

    private void AssertNoSdkActivity()
    {
        Assert.That(Get<bool>("_initialized"), Is.False);
        Assert.That(Get<bool>("_initializing"), Is.False);
        Assert.That(Get<bool>("_loading"), Is.False);
        Assert.That(Get<int>("_loadVersion"), Is.Zero);
        Assert.That(Get<object>("_rewardedAd"), Is.Null);
    }

    private T Get<T>(string name) => (T)managerType.GetField(name, InstanceMembers).GetValue(manager);

    private void Enqueue(Action callback) => Invoke("Enqueue", callback, null);

    private T Property<T>(string name) => (T)managerType.GetProperty(name, InstanceMembers).GetValue(manager);

    private object Invoke(string name, params object[] arguments)
    {
        try { return managerType.GetMethod(name, InstanceMembers).Invoke(manager, arguments); }
        catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
    }

    private static Type FindType(string name)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Required runtime type: " + name);
        return type;
    }

    private static void WithoutManagers(Action test)
    {
        var singleton = FindType("AD.Managers").GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = singleton.GetValue(null);
        try
        {
            singleton.SetValue(null, null);
            test();
        }
        finally { singleton.SetValue(null, previous); }
    }
}
