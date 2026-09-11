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
    /// Separates presentation completion from a one-time reward receipt.
    /// Call on the Unity main thread; Complete runs after ad/audio cleanup. An earned
    /// callback may arrive after close, but can only pay its original valid owner.
    /// </summary>
    public sealed class RewardedAdSession
    {
        private Func<bool> isOwnerValid;
        private Action grantReward;
        private Action<RewardedAdOutcome> finished;
        private bool rewardEarned;
        private bool rewardSettled;
        private bool rewardInvalidated;
        private bool completingPresentation;

        /// <summary>The presentation has finished; an earned receipt may still arrive.</summary>
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
            if (rewardSettled)
                return;

            rewardEarned = true;
            if (IsCompleted && !completingPresentation)
                GrantPendingReward(false);
        }

        public void Complete(RewardedAdOutcome outcome)
        {
            bool blocksReward = outcome != RewardedAdOutcome.Rewarded && outcome != RewardedAdOutcome.Cancelled;
            if (IsCompleted)
            {
                // A failure after close can revoke a receipt that has not paid yet.
                if (blocksReward) Invalidate();
                return;
            }

            // Presentation completion is one-shot, independently of reward delivery.
            IsCompleted = true;
            completingPresentation = true;
            var ownerIsValid = isOwnerValid;
            var completion = finished;
            finished = null;

            try
            {
                if (blocksReward) Invalidate();
                if (ownerIsValid == null || !ownerIsValid())
                {
                    Invalidate();
                    return;
                }
                // Owner validation may itself revoke the receipt through reentrancy.
                if (!blocksReward && rewardSettled)
                    return;

                if (!blocksReward)
                    outcome = rewardEarned ? RewardedAdOutcome.Rewarded : RewardedAdOutcome.Cancelled;

                try
                {
                    if (outcome == RewardedAdOutcome.Rewarded)
                        GrantPendingReward(true);
                }
                finally
                {
                    completingPresentation = false;
                    completion?.Invoke(outcome);
                }
            }
            finally
            {
                completingPresentation = false;
            }
        }

        /// <summary>Revokes pending callbacks without closing a native presentation.</summary>
        public void Invalidate()
        {
            rewardInvalidated = true;
            rewardSettled = true;
            rewardEarned = false;
            isOwnerValid = null;
            grantReward = null;
            finished = null;
        }

        private void GrantPendingReward(bool ownerAlreadyValidated)
        {
            if (rewardSettled || !rewardEarned)
                return;

            // Consume before validation/user code so reentrancy or an exception cannot
            // retry a partially applied reward. Late delivery never calls finished.
            var ownerIsValid = isOwnerValid;
            var reward = grantReward;
            rewardSettled = true;
            isOwnerValid = null;
            grantReward = null;
            if (reward != null && (ownerAlreadyValidated || (ownerIsValid != null && ownerIsValid()))
                && !rewardInvalidated)
                reward();
        }
    }
}
