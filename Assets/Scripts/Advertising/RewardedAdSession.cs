using System;

namespace AD.Advertising
{
    public enum RewardedAdOutcome
    {
        Rewarded,
        Cancelled,
        Failed,
        Unavailable,
        PolicyBlocked
    }

    /// <summary>
    /// Settles one reward request. Call from the Unity main thread after ad cleanup.
    /// The direct Google rewarded-ad flow reports earned rewards before close;
    /// callbacks arriving after completion cannot revive or reward this session.
    /// </summary>
    public sealed class RewardedAdSession
    {
        private Func<bool> isOwnerValid;
        private Action grantReward;
        private Action<RewardedAdOutcome> finished;
        private bool rewardEarned;

        public bool IsCompleted { get; private set; }

        public RewardedAdSession(
            Func<bool> isOwnerValid,
            Action grantReward,
            Action<RewardedAdOutcome> finished)
        {
            this.isOwnerValid = isOwnerValid ?? throw new ArgumentNullException(nameof(isOwnerValid));
            this.grantReward = grantReward ?? throw new ArgumentNullException(nameof(grantReward));
            this.finished = finished ?? throw new ArgumentNullException(nameof(finished));
        }

        public void EarnReward()
        {
            if (!IsCompleted)
                rewardEarned = true;
        }

        public void Complete(RewardedAdOutcome outcome)
        {
            if (IsCompleted)
                return;

            // Become terminal and release captured owners before invoking any user code.
            // Reentrant or duplicate SDK callbacks therefore cannot settle twice.
            IsCompleted = true;
            var ownerIsValid = isOwnerValid;
            var reward = grantReward;
            var completion = finished;
            isOwnerValid = null;
            grantReward = null;
            finished = null;

            if (!ownerIsValid())
                return;

            if (outcome == RewardedAdOutcome.Rewarded || outcome == RewardedAdOutcome.Cancelled)
                outcome = rewardEarned ? RewardedAdOutcome.Rewarded : RewardedAdOutcome.Cancelled;

            try
            {
                if (outcome == RewardedAdOutcome.Rewarded)
                    reward();
            }
            finally
            {
                completion(outcome);
            }
        }
    }
}
