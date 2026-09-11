using System;
using System.Collections.Generic;
using AD.Purchasing;
using NUnit.Framework;

public class RevivalIAPFulfillmentTests
{
    private static readonly string[] NoAds = { NoAdsPurchaseFulfillment.ProductId };

    [Test]
    public void Revival_IAP_ConfirmsOnlyAfterPersistenceCompletes()
    {
        var actions = new List<string>();
        bool saved = false;
        var delivery = new NoAdsPurchaseFulfillment(() =>
        {
            actions.Add("save");
            saved = true;
            return true;
        });

        var result = delivery.Process(PurchaseDeliveryState.Pending, NoAds, () =>
        {
            Assert.That(saved, Is.True, "Store acknowledgement cannot precede durable storage.");
            actions.Add("confirm");
        });

        Assert.That(result, Is.EqualTo(PurchaseFulfillmentResult.Fulfilled));
        Assert.That(actions, Is.EqualTo(new[] { "save", "confirm" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_IAP_FailedSaveLeavesTransactionPendingAndReplayCanRecover(bool throws)
    {
        int attempts = 0;
        int confirmations = 0;
        var delivery = new NoAdsPurchaseFulfillment(() =>
        {
            if (++attempts > 1) return true;
            if (throws) throw new System.IO.IOException("Simulated disk failure");
            return false;
        });

        Assert.That(delivery.Process(PurchaseDeliveryState.Pending, NoAds, () => confirmations++),
            Is.EqualTo(PurchaseFulfillmentResult.PersistenceFailed));
        Assert.That(confirmations, Is.Zero);
        Assert.That(delivery.Process(PurchaseDeliveryState.Pending, NoAds, () => confirmations++),
            Is.EqualTo(PurchaseFulfillmentResult.Fulfilled));
        Assert.That(confirmations, Is.EqualTo(1));
    }

    [Test]
    public void Revival_IAP_ConfirmationFailureKeepsSavedEntitlementAndAllowsReplay()
    {
        bool entitlement = false;
        int grants = 0;
        Func<bool> persist = () =>
        {
            if (!entitlement) grants++;
            entitlement = true;
            return true;
        };
        var beforeRestart = new NoAdsPurchaseFulfillment(persist);
        Assert.That(beforeRestart.Process(PurchaseDeliveryState.Pending, NoAds,
            () => throw new InvalidOperationException("Simulated store disconnect")),
            Is.EqualTo(PurchaseFulfillmentResult.ConfirmationFailed));
        Assert.That(entitlement, Is.True);

        // A fresh processor represents an app restart. The persistent entitlement,
        // rather than an in-memory transaction set, makes redelivery idempotent.
        var afterRestart = new NoAdsPurchaseFulfillment(persist);
        int confirmations = 0;
        Assert.That(afterRestart.Process(PurchaseDeliveryState.Pending, NoAds, () => confirmations++),
            Is.EqualTo(PurchaseFulfillmentResult.Fulfilled));
        Assert.That(grants, Is.EqualTo(1));
        Assert.That(confirmations, Is.EqualTo(1));
    }

    [Test]
    public void Revival_IAP_RestoredConfirmedOrderGrantsWithoutAcknowledgingAgain()
    {
        int saves = 0;
        int confirmations = 0;
        var delivery = new NoAdsPurchaseFulfillment(() => { saves++; return true; });
        Assert.That(delivery.Process(PurchaseDeliveryState.Confirmed, NoAds, () => confirmations++),
            Is.EqualTo(PurchaseFulfillmentResult.Fulfilled));
        Assert.That(saves, Is.EqualTo(1));
        Assert.That(confirmations, Is.Zero);
    }

    [TestCase(PurchaseDeliveryState.Deferred)]
    [TestCase(PurchaseDeliveryState.Failed)]
    public void Revival_IAP_UnpaidOrderNeverGrantsOrAcknowledges(PurchaseDeliveryState state)
    {
        int saves = 0;
        int confirmations = 0;
        var delivery = new NoAdsPurchaseFulfillment(() => { saves++; return true; });
        Assert.That(delivery.Process(state, NoAds, () => confirmations++),
            Is.EqualTo(PurchaseFulfillmentResult.Ignored));
        Assert.That(saves, Is.Zero);
        Assert.That(confirmations, Is.Zero);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("unknown_product")]
    [TestCase("com.aedeong.monstertamer.no_ads.other")]
    public void Revival_IAP_UnknownProductNeverGrantsOrAcknowledges(string productId)
    {
        int calls = 0;
        var delivery = new NoAdsPurchaseFulfillment(() => { calls++; return true; });
        Assert.That(delivery.Process(PurchaseDeliveryState.Pending, new[] { productId }, () => calls++),
            Is.EqualTo(PurchaseFulfillmentResult.Ignored));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void Revival_IAP_MixedOrEmptyCartCannotBePartiallyGranted()
    {
        int calls = 0;
        var delivery = new NoAdsPurchaseFulfillment(() => { calls++; return true; });
        foreach (var products in new[] { null, Array.Empty<string>(), new[] { NoAds[0], "unknown" } })
            Assert.That(delivery.Process(PurchaseDeliveryState.Pending, products, () => calls++),
                Is.EqualTo(PurchaseFulfillmentResult.Ignored));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void Revival_IAP_PendingWithoutAcknowledgementRouteIsNotGranted()
    {
        int calls = 0;
        var delivery = new NoAdsPurchaseFulfillment(() => { calls++; return true; });
        Assert.That(delivery.Process(PurchaseDeliveryState.Pending, NoAds),
            Is.EqualTo(PurchaseFulfillmentResult.Ignored));
        Assert.That(calls, Is.Zero);
    }
}
