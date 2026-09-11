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
public class RevivalAdManagerTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Type managerType;
    private Component manager;
    private Scene previousScene;
    private Scene testScene;

    [SetUp]
    public void SetUp()
    {
        managerType = FindType("AD.GoogleAdMobManager");
        previousScene = SceneManager.GetActiveScene();
        // A preview scene isolates objects without saving/replacing the user's scene
        // or failing when the batch runner's active scene is untitled.
        testScene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Revival isolated ad manager");
        SceneManager.MoveGameObjectToScene(root, testScene);
        manager = root.AddComponent(managerType);
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null)
            DestroyManager();
        if (testScene.IsValid() && testScene.isLoaded)
            EditorSceneManager.ClosePreviewScene(testScene);
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
    public void Revival_RewardInLaterFrameGrantsOnceWithoutRepeatingPresentationCompletion()
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
        Enqueue(session.EarnReward);
        Invoke("Update");

        AssertCleared();
        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(resumes, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(RewardedAdOutcome.Cancelled));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_LateRewardOrFailureCannotReleaseNewPresentation(bool failureFirst)
    {
        var oldRewards = 0;
        var oldResumes = 0;
        var newResumes = 0;
        var oldSession = new RewardedAdSession(() => true, () => oldRewards++, _ => { });
        Install(oldSession, () => oldResumes++);
        Enqueue(() => Invoke("QueueClose", oldSession));
        Invoke("Update");
        var nextSession = new RewardedAdSession(() => true, () => { }, _ => { });
        Install(nextSession, () => newResumes++);

        if (failureFirst)
            Enqueue(() => Invoke("CompleteSession", oldSession, RewardedAdOutcome.Failed));
        Enqueue(oldSession.EarnReward);
        Enqueue(() => Invoke("QueueClose", oldSession));
        Enqueue(oldSession.EarnReward);
        Invoke("Update");

        Assert.That(oldRewards, Is.EqualTo(failureFirst ? 0 : 1));
        Assert.That(oldResumes, Is.EqualTo(1));
        Assert.That(newResumes, Is.Zero);
        Assert.That(Get<RewardedAdSession>("_session"), Is.SameAs(nextSession));
        Assert.That(nextSession.IsCompleted, Is.False);
        Assert.That(Property<bool>("IsInProgress"), Is.True);
    }

    [Test]
    public void Revival_ClosedReceiptCannotRewardAfterLeavingAndReturningToSameScene()
    {
        var rewards = 0;
        var session = CreateOwnedSession(() => rewards++);
        Install(session, () => { });
        Invoke("CompleteSession", session, RewardedAdOutcome.Cancelled);

        // Deliver A -> B -> A notifications. The active handle matches A at receipt
        // time, so only the captured scene generation can reject the late reward.
        Invoke("OnSceneChanged", previousScene, testScene);
        Invoke("OnSceneChanged", testScene, previousScene);
        Enqueue(session.EarnReward);
        Invoke("Update");

        Assert.That(rewards, Is.Zero);
        AssertCleared();
    }

    [Test]
    public void Revival_ClosedReceiptCannotRewardAfterManagerDestruction()
    {
        var rewards = 0;
        var session = CreateOwnedSession(() => rewards++);
        Install(session, () => { });
        Invoke("CompleteSession", session, RewardedAdOutcome.Cancelled);
        DestroyManager();

        session.EarnReward();
        Assert.That(rewards, Is.Zero);
    }

    private RewardedAdSession CreateOwnedSession(Action reward) =>
        (RewardedAdSession)Invoke("CreateSession", manager, reward,
            (Action<RewardedAdOutcome>)(_ => { }));

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

        DestroyManager();

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
        var enqueue = (Action<Action, Action>)Delegate.CreateDelegate(typeof(Action<Action, Action>),
            manager, managerType.GetMethod("Enqueue", InstanceMembers));
        enqueue(() => runs++, () => discards++);

        // Deliver through the retained managed callback after fixture destruction,
        // exactly as an SDK worker can after the native object is gone.
        DestroyManager();
        enqueue(() => runs++, () => discards++);

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
        var resumes = 0;
        var rewards = 0;
        var completions = 0;
        var session = new RewardedAdSession(
            () => true,
            () => rewards++, _ => completions++);
        session.EarnReward();
        Install(session, () => resumes++);
        Invoke("Init");

        // Runtime activeSceneChanged is Play-mode-only. Invoke the production handler
        // with isolated scene handles without replacing the runner's active scene.
        Invoke("OnSceneChanged", previousScene, testScene);

        Assert.That(session.IsCompleted, Is.False);
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

    [Test]
    public void Revival_HarnessTraceCapturesWorkerTimeButDeliversOnUpdate()
    {
        string delivered = null;
        double timestamp = 0;
        int deliveredThread = 0;
        var mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        Action<string, double> handler = (name, time) =>
        {
            delivered = name;
            timestamp = time;
            deliveredThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        };
        managerType.GetEvent("HarnessEvent").AddEventHandler(manager, handler);
        var trace = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), manager,
            managerType.GetMethod("TraceHarness", InstanceMembers));
        double before = (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        var worker = new System.Threading.Thread(() => trace("worker_event"));
        worker.Start();
        Assert.That(worker.Join(5000), Is.True);
        double after = (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        Assert.That(delivered, Is.Null);
        Invoke("Update");
        Assert.That(delivered, Is.EqualTo("worker_event"));
        Assert.That(timestamp, Is.InRange(before, after));
        Assert.That(deliveredThread, Is.EqualTo(mainThread));
        AssertNoSdkActivity();
    }

    private void Install(RewardedAdSession session, Action resume)
    {
        managerType.GetField("_session", InstanceMembers).SetValue(manager, session);
        managerType.GetField("_resumeBgm", InstanceMembers).SetValue(manager, resume);
    }

    private void DestroyManager()
    {
        // This runtime MonoBehaviour does not receive OnDestroy automatically in
        // EditMode. Dispatch its production handler through a bound delegate before
        // destroying the native object, avoiding reflection on a destroyed component.
        var nativeObject = manager.gameObject;
        var destroy = (Action)Delegate.CreateDelegate(typeof(Action), manager,
            managerType.GetMethod("OnDestroy", InstanceMembers));
        try { destroy(); }
        finally { UnityEngine.Object.DestroyImmediate(nativeObject); }
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
