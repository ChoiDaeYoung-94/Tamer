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
    public void Revival_LateRewardPaysOnceWithoutRepeatingPresentationCompletion()
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
        Assert.That(rewards, Is.Zero);
        Assert.That(session.IsCompleted, Is.True);
        session.EarnReward();
        session.EarnReward();
        session.Complete(RewardedAdOutcome.Rewarded);

        Assert.That(rewards, Is.EqualTo(1));
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
    public void Revival_PresentationCompletionAndRewardAreConsumedBeforeReentrantCallbacks()
    {
        var rewards = 0;
        var completions = 0;
        RewardedAdSession session = null;
        session = new RewardedAdSession(() =>
        {
            Assert.That(session.IsCompleted, Is.True);
            session.Complete(RewardedAdOutcome.Cancelled);
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

    [TestCase(RewardedAdOutcome.Failed)]
    [TestCase(RewardedAdOutcome.Unavailable)]
    [TestCase(RewardedAdOutcome.PolicyBlocked)]
    public void Revival_FailureAfterCloseRevokesPendingRewardWithoutRepeatingCompletion(RewardedAdOutcome failure)
    {
        var rewards = 0;
        var completions = 0;
        var session = new RewardedAdSession(() => true, () => rewards++, _ => completions++);

        session.Complete(RewardedAdOutcome.Cancelled);
        session.Complete(failure);
        session.EarnReward();

        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_ExplicitInvalidationPermanentlySuppressesPendingReward(bool closeFirst)
    {
        var rewards = 0;
        var completions = 0;
        var session = new RewardedAdSession(() => true, () => rewards++, _ => completions++);

        if (closeFirst) session.Complete(RewardedAdOutcome.Cancelled);
        session.Invalidate();
        session.EarnReward();
        session.Complete(RewardedAdOutcome.Cancelled);

        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(closeFirst ? 1 : 0));
    }

    [Test]
    public void Revival_LateRewardRechecksOriginalOwnerAndCannotRetryAfterOwnerBecomesValid()
    {
        var ownerValid = true;
        var rewards = 0;
        var completions = 0;
        var session = new RewardedAdSession(() => ownerValid, () => rewards++, _ => completions++);

        session.Complete(RewardedAdOutcome.Cancelled);
        ownerValid = false;
        session.EarnReward();
        ownerValid = true;
        session.EarnReward();

        Assert.That(rewards, Is.Zero);
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void Revival_LateRewardExceptionCannotRetryOrRepeatCompletion()
    {
        var rewards = 0;
        var completions = 0;
        var failure = new InvalidOperationException("Late reward failed.");
        var session = new RewardedAdSession(() => true, () =>
        {
            rewards++;
            throw failure;
        }, _ => completions++);

        session.Complete(RewardedAdOutcome.Cancelled);
        Assert.That(Assert.Throws<InvalidOperationException>(session.EarnReward), Is.SameAs(failure));
        session.EarnReward();
        session.Complete(RewardedAdOutcome.Cancelled);

        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void Revival_LateRewardOwnerValidationCannotReenterRewardDelivery()
    {
        var rewards = 0;
        var completions = 0;
        var validations = 0;
        RewardedAdSession session = null;
        session = new RewardedAdSession(() =>
        {
            validations++;
            if (validations == 2) session.EarnReward();
            return true;
        }, () => rewards++, _ => completions++);

        session.Complete(RewardedAdOutcome.Cancelled);
        session.EarnReward();

        Assert.That(validations, Is.EqualTo(2));
        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void Revival_InvalidationDuringLateOwnerValidationStillSuppressesReward()
    {
        var validations = 0;
        var rewards = 0;
        var completions = 0;
        RewardedAdSession session = null;
        session = new RewardedAdSession(() =>
        {
            validations++;
            if (validations == 2) session.Invalidate();
            return true;
        }, () => rewards++, _ => completions++);

        session.Complete(RewardedAdOutcome.Cancelled);
        session.EarnReward();
        session.EarnReward();

        Assert.That(rewards, Is.Zero);
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
