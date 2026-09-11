using System;
using AD.Advertising;
using NUnit.Framework;

public class RevivalRewardedAdSessionTests
{
    [TestCase(RewardedAdOutcome.Cancelled)]
    [TestCase(RewardedAdOutcome.Rewarded)]
    public void Revival_CloseWithoutEarnedRewardCancels(RewardedAdOutcome closeOutcome)
    {
        var rewards = 0;
        var completions = 0;
        var result = RewardedAdOutcome.Failed;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            completions++;
            result = outcome;
        });

        session.Complete(closeOutcome);

        Assert.That(session.IsCompleted, Is.True);
        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(RewardedAdOutcome.Cancelled));
    }

    [TestCase(RewardedAdOutcome.Cancelled)]
    [TestCase(RewardedAdOutcome.Rewarded)]
    public void Revival_EarnedRewardWaitsForCloseAndSettlesOnce(RewardedAdOutcome closeOutcome)
    {
        var rewards = 0;
        var completions = 0;
        var result = RewardedAdOutcome.Failed;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            completions++;
            result = outcome;
        });

        session.EarnReward();
        session.EarnReward();
        Assert.That(session.IsCompleted, Is.False);
        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.Zero);

        session.Complete(closeOutcome);
        session.Complete(closeOutcome);
        session.Complete(RewardedAdOutcome.Failed);

        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(RewardedAdOutcome.Rewarded));
    }

    [TestCase(RewardedAdOutcome.Failed)]
    [TestCase(RewardedAdOutcome.Unavailable)]
    [TestCase(RewardedAdOutcome.PolicyBlocked)]
    public void Revival_FailureNeverGrantsAnEarnedReward(RewardedAdOutcome failure)
    {
        var rewards = 0;
        var completions = 0;
        var result = RewardedAdOutcome.Cancelled;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            completions++;
            result = outcome;
        });

        session.EarnReward();
        session.Complete(failure);
        session.Complete(RewardedAdOutcome.Rewarded);

        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(failure));
    }

    [Test]
    public void Revival_LateRewardCannotReviveClosedSession()
    {
        var rewards = 0;
        var completions = 0;
        var result = RewardedAdOutcome.Failed;
        var session = new RewardedAdSession(() => true, () => rewards++, outcome =>
        {
            completions++;
            result = outcome;
        });

        session.Complete(RewardedAdOutcome.Cancelled);
        session.EarnReward();
        session.Complete(RewardedAdOutcome.Rewarded);

        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(RewardedAdOutcome.Cancelled));
    }

    [Test]
    public void Revival_InvalidOwnerReceivesNeitherRewardNorCompletion()
    {
        var ownerValid = true;
        var validations = 0;
        var rewards = 0;
        var completions = 0;
        var session = new RewardedAdSession(() =>
        {
            validations++;
            return ownerValid;
        }, () => rewards++, _ => completions++);

        session.EarnReward();
        ownerValid = false;
        session.Complete(RewardedAdOutcome.Cancelled);
        ownerValid = true;
        session.Complete(RewardedAdOutcome.Rewarded);

        Assert.That(session.IsCompleted, Is.True);
        Assert.That(validations, Is.EqualTo(1));
        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.Zero);
    }

    [Test]
    public void Revival_CompletionIsTerminalBeforeOwnerAndRewardCallbacks()
    {
        var rewards = 0;
        var completions = 0;
        RewardedAdSession session = null;
        session = new RewardedAdSession(() =>
        {
            Assert.That(session.IsCompleted, Is.True);
            session.Complete(RewardedAdOutcome.Failed);
            return true;
        }, () =>
        {
            Assert.That(session.IsCompleted, Is.True);
            rewards++;
            session.EarnReward();
            session.Complete(RewardedAdOutcome.Cancelled);
        }, outcome =>
        {
            Assert.That(outcome, Is.EqualTo(RewardedAdOutcome.Rewarded));
            completions++;
            session.Complete(RewardedAdOutcome.Rewarded);
        });

        session.EarnReward();
        session.Complete(RewardedAdOutcome.Cancelled);

        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void Revival_RewardExceptionStillNotifiesCompletionAndCannotRetryReward()
    {
        var rewards = 0;
        var completions = 0;
        var failure = new InvalidOperationException("Reward failed.");
        var session = new RewardedAdSession(() => true, () =>
        {
            rewards++;
            throw failure;
        }, outcome =>
        {
            Assert.That(outcome, Is.EqualTo(RewardedAdOutcome.Rewarded));
            completions++;
        });

        session.EarnReward();
        var thrown = Assert.Throws<InvalidOperationException>(() => session.Complete(RewardedAdOutcome.Cancelled));
        session.Complete(RewardedAdOutcome.Cancelled);

        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(session.IsCompleted, Is.True);
        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
    }
}
