using System;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Purchasing
{
    public enum PlayFabReceiptOutcome { Rejected, Granted, AlreadyUsed }

    /// <summary>A duplicate receipt alone never establishes this account's entitlement.</summary>
    public sealed class PlayFabNoAdsVerification : IReceiptVerifier
    {
        private readonly Func<string, ReceiptSession, CancellationToken, Task<PlayFabReceiptOutcome>> _validate;
        private readonly Func<ReceiptSession, CancellationToken, Task<bool>> _ownsNoAds;

        public PlayFabNoAdsVerification(
            Func<string, ReceiptSession, CancellationToken, Task<PlayFabReceiptOutcome>> validate,
            Func<ReceiptSession, CancellationToken, Task<bool>> ownsNoAds)
        {
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
            _ownsNoAds = ownsNoAds ?? throw new ArgumentNullException(nameof(ownsNoAds));
        }

        public async Task<bool> VerifyAsync(string receipt, ReceiptSession session, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid || string.IsNullOrEmpty(receipt)) return false;
            var outcome = await _validate(receipt, session, token);
            token.ThrowIfCancellationRequested();
            if (outcome == PlayFabReceiptOutcome.Granted) return true;
            if (outcome != PlayFabReceiptOutcome.AlreadyUsed) return false;
            bool owned = await _ownsNoAds(session, token);
            token.ThrowIfCancellationRequested();
            return owned;
        }
    }
}
