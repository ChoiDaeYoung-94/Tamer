using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AD.Purchasing;
using NUnit.Framework;
using UnityEngine.Purchasing;

public class RevivalIAPOrderTests
{
    private sealed class FakeOrderInfo : IOrderInfo
    {
        public IAppleOrderInfo Apple => null;
        public IGoogleOrderInfo Google => null;
        public IPaymentProvidersOrderInfo PaymentProviders => null;
        public List<IPurchasedProductInfo> PurchasedProductInfo { get; set; } = new List<IPurchasedProductInfo>();
        public string Receipt { get; set; } = "offline-test-receipt";
        public string TransactionID { get; set; } = "offline-test-transaction";
    }

    private static Type ManagerType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD.IAPManager")).First(t => t != null);

    private static CartItem Item(string id = NoAdsPurchaseFulfillment.ProductId,
        ProductType type = ProductType.NonConsumable, int quantity = 1)
    {
        // SDK Product has no public constructor. Construct its data-only model
        // without obtaining a StoreController or running UnityIAPServices.
        var constructor = typeof(Product).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(ProductDefinition), typeof(ProductMetadata), typeof(bool) }, null);
        Assert.That(constructor, Is.Not.Null, "Review the pinned IAP SDK's Product model constructor.");
        var product = (Product)constructor.Invoke(new object[]
        {
            new ProductDefinition(id, type), new ProductMetadata(), false
        });
        return new CartItem(product, quantity);
    }

    private static bool IsSupported(Order order) => (bool)ManagerType
        .GetMethod("IsSupportedPaidOrder", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new object[] { order });

    [Test]
    public void Revival_IAP_ActualV5PaidAndRestoredOrderModelsAreAccepted()
    {
        Assert.That(IsSupported(new PendingOrder(new Cart(Item()), new FakeOrderInfo())), Is.True);
        Assert.That(IsSupported(new ConfirmedOrder(new Cart(Item()), new FakeOrderInfo())), Is.True);
    }

    [TestCase(0)]
    [TestCase(2)]
    public void Revival_IAP_InvalidNoAdsQuantityIsNotGranted(int quantity)
    {
        Assert.That(IsSupported(new PendingOrder(new Cart(Item(quantity: quantity)), new FakeOrderInfo())), Is.False);
    }

    [TestCase(ProductType.Consumable)]
    [TestCase(ProductType.Subscription)]
    [TestCase(ProductType.Unknown)]
    public void Revival_IAP_UnexpectedProductTypeIsNotGranted(ProductType type)
    {
        Assert.That(IsSupported(new PendingOrder(new Cart(Item(type: type)), new FakeOrderInfo())), Is.False);
    }

    [Test]
    public void Revival_IAP_UnknownAndMixedV5CartsAreNotAcknowledged()
    {
        Assert.That(IsSupported(new PendingOrder(new Cart(Item("unknown")), new FakeOrderInfo())), Is.False);
        Assert.That(IsSupported(new PendingOrder(new Cart(new List<CartItem> { Item(), Item("unknown") }),
            new FakeOrderInfo())), Is.False);
    }

    [Test]
    public void Revival_IAP_IncompletePaidOrderMetadataStaysUnacknowledged()
    {
        Assert.That(IsSupported(new PendingOrder(new Cart(Item()),
            new FakeOrderInfo { Receipt = "" })), Is.False);
        Assert.That(IsSupported(new PendingOrder(new Cart(Item()),
            new FakeOrderInfo { TransactionID = "" })), Is.False);
    }

    [Test]
    public void Revival_IAP_DeferredV5OrderIsNotPaid()
    {
        Assert.That(IsSupported(new DeferredOrder(new Cart(Item()), new FakeOrderInfo())), Is.False);
    }

    [Test]
    public void Revival_IAP_DeferredApprovalCanArriveViaPendingCallbackWithoutPublicFetchCallback()
    {
        var manager = Activator.CreateInstance(ManagerType);
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var cart = new Cart(Item());
            var info = new FakeOrderInfo();
            var queue = (System.Collections.IDictionary)ManagerType.GetField("_unfinished", flags).GetValue(manager);
            ManagerType.GetMethod("OnPurchaseDeferred", flags)
                .Invoke(manager, new object[] { new DeferredOrder(cart, info) });
            Assert.That(queue.Count, Is.Zero);
            // Android's resume fetch reroutes approved orders to this callback
            // without publishing OnPurchasesFetched. Keep that SDK routing enabled.
            ManagerType.GetMethod("OnPurchasePending", flags)
                .Invoke(manager, new object[] { new PendingOrder(cart, info) });
            Assert.That(queue.Count, Is.EqualTo(1));
        }
        finally { ((IDisposable)manager).Dispose(); }
    }

    [Test]
    public void Revival_IAP_RecreatedManagerCanRecoverPendingSuppressedBySharedSdkReroute()
    {
        var manager = Activator.CreateInstance(ManagerType);
        try
        {
            var pending = new PendingOrder(new Cart(Item()), new FakeOrderInfo());
            var orders = new Orders(Array.Empty<ConfirmedOrder>(), new[] { pending }, Array.Empty<DeferredOrder>());
            ManagerType.GetMethod("OnPurchasesFetched", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { orders });
            var queue = (System.Collections.IDictionary)ManagerType
                .GetField("_unfinished", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.That(queue.Count, Is.EqualTo(1),
                "SDK's shared PurchaseService may suppress OnPurchasePending already emitted to the previous manager.");
        }
        finally { ((IDisposable)manager).Dispose(); }
    }

    [Test]
    public void Revival_IAP_DisconnectedCallbackRetainsPendingUntilRetryOrDispose()
    {
        // Do not call Init: no account, store, network, gameplay, or file writes.
        var manager = Activator.CreateInstance(ManagerType);
        try
        {
            var pending = new PendingOrder(new Cart(Item()), new FakeOrderInfo());
            ManagerType.GetMethod("OnPurchasePending", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { pending });
            var queue = (System.Collections.IDictionary)ManagerType
                .GetField("_unfinished", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.That(queue.Count, Is.EqualTo(1));
            ManagerType.GetMethod("RetryPendingPurchases").Invoke(manager, null);
            Assert.That(queue.Count, Is.EqualTo(1), "Disconnected orders must not be dropped or acknowledged.");
            Assert.That(ManagerType.GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager), Is.Null);
            ((IDisposable)manager).Dispose();
            Assert.That(queue.Count, Is.Zero);
        }
        finally { ((IDisposable)manager).Dispose(); }
    }

    [Test]
    public void Revival_IAP_StalePendingAfterConfirmationCannotReopenCompletedTransaction()
    {
        var manager = Activator.CreateInstance(ManagerType);
        try
        {
            var cart = new Cart(Item());
            var info = new FakeOrderInfo();
            var pending = new PendingOrder(cart, info);
            var pendingHandler = ManagerType.GetMethod("OnPurchasePending", BindingFlags.Instance | BindingFlags.NonPublic);
            pendingHandler.Invoke(manager, new object[] { pending });
            ManagerType.GetMethod("OnPurchaseConfirmed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { new ConfirmedOrder(cart, info) });
            pendingHandler.Invoke(manager, new object[] { new PendingOrder(cart, info) });

            var queue = (System.Collections.IDictionary)ManagerType
                .GetField("_unfinished", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.That(queue.Count, Is.Zero, "Google ignores a second confirmation callback for a completed token.");
            Assert.That(ManagerType.GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager), Is.Null, "Data-only callback tests must not start a store.");
        }
        finally { ((IDisposable)manager).Dispose(); }
    }

    [Test]
    public void Revival_IAP_FetchFailureBecomesRetryableAfterBackoffWithoutSceneReload()
    {
        var manager = Activator.CreateInstance(ManagerType);
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ManagerType.GetField("_connected", flags).SetValue(manager, true);
            ManagerType.GetField("_productsLoaded", flags).SetValue(manager, true);
            ManagerType.GetField("_purchasesLoaded", flags).SetValue(manager, true);
            var retry = ManagerType.GetMethod("ShouldRetryInitialization", flags);
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(retry.Invoke(manager, new object[] { now }), Is.False);

            ManagerType.GetMethod("OnPurchasesFetchFailed", flags).Invoke(manager, new object[] { null });
            ManagerType.GetField("_nextInitializationAttempt", flags).SetValue(manager, now.AddSeconds(30));
            Assert.That(retry.Invoke(manager, new object[] { now }), Is.False);
            Assert.That(retry.Invoke(manager, new object[] { now.AddSeconds(30) }), Is.True);
            ManagerType.GetField("_initialFetchInProgress", flags).SetValue(manager, true);
            Assert.That(retry.Invoke(manager, new object[] { now.AddMinutes(1) }), Is.False,
                "An active SDK fetch must not be duplicated.");
        }
        finally { ((IDisposable)manager).Dispose(); }
    }
}
