using System;
using System.Collections.Generic;

namespace AD.Purchasing
{
    public enum PurchaseDeliveryState
    {
        Pending,
        Confirmed,
        Deferred,
        Failed
    }

    public enum PurchaseFulfillmentResult
    {
        Ignored,
        PersistenceFailed,
        ConfirmationFailed,
        Fulfilled
    }

    /// <summary>
    /// The one supported product is a permanent entitlement, not a consumable.
    /// The persistence callback must merge the existing GooglePlay CSV idempotently
    /// and return true only after the resulting entitlement is durably saved.
    /// Store receipt verification is outside this delivery boundary.
    /// </summary>
    public sealed class NoAdsPurchaseFulfillment
    {
        public const string ProductId = "com.aedeong.monstertamer.no_ads";
        private readonly Func<bool> _tryPersistEntitlement;

        public NoAdsPurchaseFulfillment(Func<bool> tryPersistEntitlement)
        {
            _tryPersistEntitlement = tryPersistEntitlement ??
                throw new ArgumentNullException(nameof(tryPersistEntitlement));
        }

        public PurchaseFulfillmentResult Process(PurchaseDeliveryState state,
            IReadOnlyList<string> productIds, Action confirmPurchase = null)
        {
            // Never acknowledge unknown or partially supported carts. A deferred
            // payment is not a paid purchase and must not grant any entitlement.
            if ((state != PurchaseDeliveryState.Pending && state != PurchaseDeliveryState.Confirmed) ||
                productIds == null || productIds.Count != 1 ||
                !string.Equals(productIds[0], ProductId, StringComparison.Ordinal) ||
                (state == PurchaseDeliveryState.Pending && confirmPurchase == null))
                return PurchaseFulfillmentResult.Ignored;

            try
            {
                if (!_tryPersistEntitlement())
                    return PurchaseFulfillmentResult.PersistenceFailed;
            }
            catch (Exception)
            {
                // A write failure must leave the store transaction unfinished.
                // Do not publish exception text, receipts, or account identifiers.
                return PurchaseFulfillmentResult.PersistenceFailed;
            }

            if (state == PurchaseDeliveryState.Pending)
            {
                try
                {
                    confirmPurchase();
                }
                catch (Exception)
                {
                    // The grant is already saved. A replay may safely try the
                    // same idempotent grant and store acknowledgement again.
                    return PurchaseFulfillmentResult.ConfirmationFailed;
                }
            }

            return PurchaseFulfillmentResult.Fulfilled;
        }
    }
}
