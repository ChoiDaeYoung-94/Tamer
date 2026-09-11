using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AD.Purchasing;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace AD
{
    /// <summary>Explicit test-title adapter. Never changes PlayFab's static settings or credentials.</summary>
    public sealed class PlayFabIapReceiptVerifier : IReceiptVerifier
    {
        private readonly string _title;
        private readonly string _package;
        private readonly string _catalog;
        private readonly PlayFabNoAdsVerification _policy;

        public PlayFabIapReceiptVerifier(string testTitle, string productionTitle, string package, string catalog)
        {
            if (string.IsNullOrEmpty(testTitle) || string.IsNullOrEmpty(productionTitle) ||
                !Regex.IsMatch(testTitle, "^[A-Fa-f0-9]{3,32}$") ||
                !Regex.IsMatch(productionTitle, "^[A-Fa-f0-9]{3,32}$") ||
                string.Equals(testTitle, productionTitle, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A separate test title is required.");
            if (string.IsNullOrEmpty(package) || !Regex.IsMatch(package, @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+\.iaptest$"))
                throw new ArgumentException("An isolated iaptest package is required.");
            if (string.IsNullOrWhiteSpace(catalog)) throw new ArgumentException("An explicit test catalog is required.");
            _title = testTitle;
            _package = package;
            _catalog = catalog;
            _policy = new PlayFabNoAdsVerification(Validate, OwnsNoAds);
        }

        [Serializable] private sealed class Receipt { public string Store; public string Payload; }
        [Serializable] private sealed class Payload { public string json; public string signature; }
        [Serializable] private sealed class Purchase { public string packageName; public string productId; }

        public Task<bool> VerifyAsync(string receipt, ReceiptSession session, CancellationToken token)
            => _policy.VerifyAsync(receipt, session, token);

        private PlayFabClientInstanceAPI Client(ReceiptSession session) => new PlayFabClientInstanceAPI(
            new PlayFabApiSettings { TitleId = _title }, new PlayFabAuthenticationContext
            { PlayFabId = session.AccountId, ClientSessionTicket = session.SessionTicket });

        private static async Task<T> Wait<T>(Task<T> task, CancellationToken token)
        {
            // SDK requests cannot be aborted here. Stop waiting and ignore late callbacks;
            // IAPManager separately guards the current session/persistence owner.
            var cancelled = new TaskCompletionSource<T>();
            using (token.Register(() => cancelled.TrySetCanceled()))
            {
                var finished = await Task.WhenAny(task, cancelled.Task);
                token.ThrowIfCancellationRequested();
                return await finished;
            }
        }

        private Task<PlayFabReceiptOutcome> Validate(string raw, ReceiptSession session, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Payload payload;
            try
            {
                if (raw.Length > 32768) return Task.FromResult(PlayFabReceiptOutcome.Rejected);
                var receipt = JsonUtility.FromJson<Receipt>(raw);
                if (receipt == null || receipt.Store != "GooglePlay") return Task.FromResult(PlayFabReceiptOutcome.Rejected);
                payload = JsonUtility.FromJson<Payload>(receipt.Payload);
                if (payload == null || string.IsNullOrEmpty(payload.signature)) return Task.FromResult(PlayFabReceiptOutcome.Rejected);
                var purchase = JsonUtility.FromJson<Purchase>(payload.json);
                if (purchase == null || purchase.packageName != _package || purchase.productId != NoAdsPurchaseFulfillment.ProductId)
                    return Task.FromResult(PlayFabReceiptOutcome.Rejected);
            }
            catch (ArgumentException) { return Task.FromResult(PlayFabReceiptOutcome.Rejected); }
            var completion = new TaskCompletionSource<PlayFabReceiptOutcome>();
            Client(session).ValidateGooglePlayPurchase(new ValidateGooglePlayPurchaseRequest
            { ReceiptJson = payload.json, Signature = payload.signature, CatalogVersion = _catalog }, result =>
                completion.TrySetResult(result?.Fulfillments != null && result.Fulfillments.Any(f =>
                    f?.FulfilledItems != null && f.FulfilledItems.Any(IsNoAds))
                    ? PlayFabReceiptOutcome.Granted : PlayFabReceiptOutcome.Rejected),
                error => completion.TrySetResult(error != null && error.Error == PlayFabErrorCode.ReceiptAlreadyUsed
                    ? PlayFabReceiptOutcome.AlreadyUsed : PlayFabReceiptOutcome.Rejected));
            return Wait(completion.Task, token);
        }

        private bool IsNoAds(ItemInstance item) => item != null &&
            item.ItemId == NoAdsPurchaseFulfillment.ProductId && item.CatalogVersion == _catalog &&
            (!item.Expiration.HasValue || item.Expiration.Value.ToUniversalTime() > DateTime.UtcNow);

        private Task<bool> OwnsNoAds(ReceiptSession session, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<bool>();
            Client(session).GetUserInventory(new GetUserInventoryRequest(),
                result => completion.TrySetResult(result?.Inventory != null && result.Inventory.Any(IsNoAds)),
                error => completion.TrySetResult(false));
            return Wait(completion.Task, token);
        }
    }
}
