using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using AD.Purchasing;
using NUnit.Framework;
using UnityEngine.Purchasing;

public class RevivalIAPOrderTests
{
    private sealed class NoNetworkVerifier : IReceiptVerifier
    {
        public System.Threading.Tasks.Task<bool> VerifyAsync(string receipt, ReceiptSession session,
            System.Threading.CancellationToken token) => throw new InvalidOperationException("Unexpected network path");
    }

    private sealed class ControlledVerifier : IReceiptVerifier
    {
        public readonly System.Threading.Tasks.TaskCompletionSource<bool> Completion =
            new System.Threading.Tasks.TaskCompletionSource<bool>();
        public int Calls;
        public System.Threading.Tasks.Task<bool> VerifyAsync(string receipt, ReceiptSession session,
            System.Threading.CancellationToken token) { Calls++; return Completion.Task; }
    }

    [TestCase(0, 3, false)]
    [TestCase(2, 2, true)]
    public void Revival_IAP_TrialFailureCapRetainsOrderAndStopsThirdAttempt(
        int limit, int expectedSessionReads, bool stopped)
    {
        int sessionReads = 0;
        var manager = Activator.CreateInstance(ManagerType, new object[] {
            new NoNetworkVerifier(), (Func<ReceiptSession>)(() => { sessionReads++; return null; }), limit });
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var pending = new PendingOrder(new Cart(Item()), new FakeOrderInfo());
            ManagerType.GetMethod("OnPurchasePending", flags).Invoke(manager, new object[] { pending });
            ManagerType.GetField("_connected", flags).SetValue(manager, true);
            for (int attempt = 0; attempt < 3; attempt++)
                ManagerType.GetMethod("RetryPendingPurchases").Invoke(manager, null);
            Assert.That(sessionReads, Is.EqualTo(expectedSessionReads));
            Assert.That(ManagerType.GetProperty("VerificationRetryStopped").GetValue(manager), Is.EqualTo(stopped));
            Assert.That(ManagerType.GetProperty("Status").GetValue(manager).ToString(), Is.EqualTo("VerificationFailed"));
            Assert.That(ManagerType.GetProperty("VerificationDiagnostic").GetValue(manager),
                Is.EqualTo("SessionOrPersistenceOwnerNotReady"));
            var unfinished = (System.Collections.IDictionary)ManagerType.GetField("_unfinished", flags).GetValue(manager);
            Assert.That(unfinished.Count, Is.EqualTo(1), "A failed receipt must remain recoverable, without grant or confirmation.");
            if (stopped)
            {
                var purchases = new OfflinePurchases();
                var store = (StoreController)FormatterServices.GetUninitializedObject(typeof(StoreController));
                typeof(StoreController).GetField("m_PurchaseService", flags).SetValue(store, purchases);
                ManagerType.GetField("_store", flags).SetValue(manager, store);
                ManagerType.GetMethod("RestorePurchases").Invoke(manager, null);
                Assert.That(purchases.Restores, Is.EqualTo(1));
                Assert.That(ManagerType.GetProperty("Status").GetValue(manager).ToString(), Is.EqualTo("Restoring"));
                ManagerType.GetMethod("OnPurchasesFetched", flags).Invoke(manager, new object[] {
                    new Orders(Array.Empty<ConfirmedOrder>(), new[] { pending }, Array.Empty<DeferredOrder>()) });
                Assert.That(ManagerType.GetProperty("Status").GetValue(manager).ToString(),
                    Is.EqualTo("VerificationFailed"), "Restore completion must expose the paused verification failure.");
                Assert.That(sessionReads, Is.EqualTo(2), "Restoring must not bypass the failure limit.");
                Assert.That(unfinished.Count, Is.EqualTo(1));
            }
        }
        finally
        {
            ManagerType.GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, null);
            ((IDisposable)manager).Dispose();
        }
    }

    // Drive the real StoreController forwarding methods without initializing a store.
    private sealed class OfflinePurchases : IPurchaseService
    {
        public int Fetches, Restores;
        public bool ThrowOnFetch;
        public IAppleStoreExtendedPurchaseService Apple => null;
        public IGooglePlayStoreExtendedPurchaseService Google => null;
        public IPaymentProvidersExtendedPurchaseService PaymentProviders => null;
        public void FetchPurchases() { Fetches++; if (ThrowOnFetch) throw new InvalidOperationException(); }
        public void RestoreTransactions(Action<bool, string> callback) { Restores++; callback(true, null); }
        public void PurchaseProduct(Product product) => throw new InvalidOperationException("Unexpected sale");
        public void PurchaseProduct(string id) => throw new InvalidOperationException("Unexpected sale");
        public void Purchase(ICart cart) => throw new InvalidOperationException("Unexpected sale");
        public void ConfirmPurchase(PendingOrder order) => throw new InvalidOperationException("Unexpected confirmation");
        public void CheckEntitlement(Product product) => throw new NotSupportedException();
        public ReadOnlyObservableCollection<Order> GetPurchases() => throw new NotSupportedException();
        public void ProcessPendingOrdersOnPurchasesFetched(bool value) { }
        public event Action<PendingOrder> OnPurchasePending { add { } remove { } }
        public event Action<Order> OnPurchaseConfirmed { add { } remove { } }
        public event Action<FailedOrder> OnPurchaseFailed { add { } remove { } }
        public event Action<DeferredOrder> OnPurchaseDeferred { add { } remove { } }
        public event Action<Orders> OnPurchasesFetched { add { } remove { } }
        public event Action<PurchasesFetchFailureDescription> OnPurchasesFetchFailed { add { } remove { } }
        public event Action<Entitlement> OnCheckEntitlement { add { } remove { } }
    }

    private sealed class OfflineConnection : IStoreService
    {
        public int Connects;
        public IRetryPolicy Policy;
        public IAppleStoreExtendedService Apple => null;
        public IGooglePlayStoreExtendedService Google => null;
        public IPaymentProvidersExtendedService PaymentProviders => null;
        public System.Threading.Tasks.Task Connect() { Connects++; return System.Threading.Tasks.Task.CompletedTask; }
        public ConnectionState GetConnectionState() => ConnectionState.Connected;
        public void SetStoreReconnectionRetryPolicyOnDisconnection(IRetryPolicy policy) => Policy = policy;
        public event Action<StoreConnectionFailureDescription> OnStoreDisconnected { add { } remove { } }
        public event Action OnStoreConnected { add { } remove { } }
        public event Action OnAuthAccountChanged { add { } remove { } }
    }

    private sealed class OfflineProducts : IProductService
    {
        public int Fetches;
        public IRetryPolicy Policy;
        public IAppleStoreExtendedProductService Apple => null;
        public void FetchProductsWithNoRetries(List<ProductDefinition> products) =>
            throw new InvalidOperationException("Use an explicit policy: the pinned SDK convenience method forwards null.");
        public void FetchProducts(List<ProductDefinition> products, IRetryPolicy retryPolicy = null)
        { Fetches++; Policy = retryPolicy; }
        public ReadOnlyObservableCollection<Product> GetProducts() => throw new NotSupportedException();
        public Product GetProductById(string id) => null;
        public Product GetProductByCatalogListingId(string id) => null;
        public event Action<List<Product>> OnProductsFetched { add { } remove { } }
        public event Action<ProductFetchFailed> OnProductsFetchFailed { add { } remove { } }
    }

    private static Type RecoveryGateType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD.RevivalIapRecoveryGate")).First(t => t != null);

    private static void WithOfflineServices(bool recovery,
        Action<object, OfflineConnection, OfflineProducts, OfflinePurchases, IIapExecutionGate> check)
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RevivalIapSingleFetch-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var gate = recovery ? (IIapExecutionGate)Activator.CreateInstance(RecoveryGateType) : null;
        object manager = null;
        try
        {
            if (gate != null)
                Assert.That(RecoveryGateType.GetMethod("Open").Invoke(gate, new object[] { root, true, true }), Is.True);
            manager = recovery ? Activator.CreateInstance(ManagerType, new object[] {
                new NoNetworkVerifier(), (Func<ReceiptSession>)(() => null), 1, gate }) : Activator.CreateInstance(ManagerType);
            var connection = new OfflineConnection();
            var products = new OfflineProducts();
            var purchases = new OfflinePurchases();
            var store = (StoreController)FormatterServices.GetUninitializedObject(typeof(StoreController));
            typeof(StoreController).GetField("m_StoreService", flags).SetValue(store, connection);
            typeof(StoreController).GetField("m_ProductService", flags).SetValue(store, products);
            typeof(StoreController).GetField("m_PurchaseService", flags).SetValue(store, purchases);
            ManagerType.GetField("_store", flags).SetValue(manager, store);
            check(manager, connection, products, purchases, gate);
        }
        finally
        {
            if (manager != null)
            {
                ManagerType.GetField("_store", flags).SetValue(manager, null);
                ((IDisposable)manager).Dispose();
            }
            System.IO.Directory.Delete(root, true); // Only this unique synthetic test root.
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_IAP_OnlyRecoveryOverridesSdkConnectionAndCatalogRetryPolicies(bool recovery)
    {
        WithOfflineServices(recovery, (manager, connection, products, purchases, gate) =>
        {
            ManagerType.GetMethod("InitializePurchasing").Invoke(manager, null);
            Assert.That(connection.Connects, Is.EqualTo(1));
            ManagerType.GetMethod("OnStoreConnected", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Assert.That(products.Fetches, Is.EqualTo(1));
            if (recovery)
            {
                Assert.That(connection.Policy, Is.TypeOf<NoRetriesPolicy>());
                Assert.That(products.Policy, Is.TypeOf<NoRetriesPolicy>());
            }
            else
            {
                Assert.That(connection.Policy, Is.Null, "Normal purchasing keeps the SDK connection policy.");
                Assert.That(products.Policy, Is.Null, "Normal catalog fetch keeps the SDK default policy.");
            }
            Assert.That(purchases.Fetches, Is.Zero);
        });
    }

    [TestCase("in-flight")]
    [TestCase("empty")]
    [TestCase("failed")]
    public void Revival_IAP_RecoveryCannotRefetchAfterDuplicateCallbacksOrExplicitRetry(string outcome)
    {
        WithOfflineServices(true, (manager, connection, products, purchases, gate) =>
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ManagerType.GetMethod("InitializePurchasing").Invoke(manager, null);
            ManagerType.GetMethod("OnStoreConnected", flags).Invoke(manager, null);
            ManagerType.GetMethod("OnProductsFetched", flags).Invoke(manager, new object[] { new List<Product>() });
            Assert.That(purchases.Fetches, Is.EqualTo(1));
            if (outcome == "empty")
                ManagerType.GetMethod("OnPurchasesFetched", flags).Invoke(manager, new object[] {
                    new Orders(Array.Empty<ConfirmedOrder>(), Array.Empty<PendingOrder>(), Array.Empty<DeferredOrder>()) });
            else if (outcome == "failed")
                ManagerType.GetMethod("OnPurchasesFetchFailed", flags).Invoke(manager, new object[] { null });
            ManagerType.GetMethod("OnProductsFetched", flags).Invoke(manager, new object[] { new List<Product>() });
            ManagerType.GetMethod("OnProductsFetchFailed", flags).Invoke(manager, new object[] { null });
            ManagerType.GetMethod("OnStoreConnected", flags).Invoke(manager, null);
            ManagerType.GetMethod("InitializePurchasing").Invoke(manager, null);
            ManagerType.GetMethod("RestorePurchases").Invoke(manager, null);
            ManagerType.GetMethod("RetryPendingPurchases").Invoke(manager, null);
            Assert.That(purchases.Fetches, Is.EqualTo(1), "An open receipt budget must not permit a second Store fetch.");
            Assert.That(purchases.Restores, Is.Zero);
            Assert.That(connection.Connects, Is.EqualTo(1));
            Assert.That(products.Fetches, Is.EqualTo(1));
            Assert.That(gate.CanAccessStore, Is.EqualTo(outcome != "failed"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_IAP_RecoveryClosedOrSpentGateRejectsLateFetchAndPendingCallbacks(bool spent)
    {
        WithOfflineServices(true, (manager, connection, products, purchases, gate) =>
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ManagerType.GetField("_connected", flags).SetValue(manager, true);
            var first = new PendingOrder(new Cart(Item()), new FakeOrderInfo());
            var unfinished = (System.Collections.IDictionary)ManagerType.GetField("_unfinished", flags).GetValue(manager);
            if (spent)
            {
                unfinished[first.Info.TransactionID] = first; // Captured order awaiting its only verification.
                Assert.That(gate.TryBeginVerification(), Is.True);
            }
            else ManagerType.GetMethod("OnStoreDisconnected", flags).Invoke(manager, new object[] { null });
            var late = new PendingOrder(new Cart(Item()), new FakeOrderInfo());
            ManagerType.GetMethod("OnPurchasePending", flags).Invoke(manager, new object[] { late });
            ManagerType.GetMethod("OnPurchasesFetched", flags).Invoke(manager, new object[] {
                new Orders(Array.Empty<ConfirmedOrder>(), new[] { late }, Array.Empty<DeferredOrder>()) });
            ManagerType.GetMethod("OnPurchasesFetchFailed", flags).Invoke(manager, new object[] { null });
            ManagerType.GetMethod("OnStoreConnected", flags).Invoke(manager, null);
            Assert.That(gate.CanAccessStore || gate.TryBeginVerification(), Is.False);
            Assert.That(unfinished.Count, Is.EqualTo(spent ? 1 : 0));
            if (spent) Assert.That(unfinished[first.Info.TransactionID], Is.SameAs(first),
                "A duplicate must not replace the order captured by the only in-flight verification.");
            Assert.That(purchases.Fetches + purchases.Restores + products.Fetches + connection.Connects, Is.Zero);
        });
    }

    [Test]
    public async System.Threading.Tasks.Task Revival_IAP_RecoveryDuplicateDuringVerificationKeepsOriginalCompletion()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var hostType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Managers")).First(t => t != null);
        var dataType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.DataManager")).First(t => t != null);
        var singleton = hostType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(singleton.GetValue(null), Is.Null, "Requires an isolated synthetic manager host.");
        var go = new UnityEngine.GameObject("Synthetic receipt continuation owner");
        go.SetActive(false); // Never run Managers.Awake or initialize services.
        object manager = null;
        var session = new ReceiptSession("synthetic-account", "synthetic-ticket");
        var verifier = new ControlledVerifier();
        var gate = (IIapExecutionGate)Activator.CreateInstance(RecoveryGateType);
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RevivalIapContinuation-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var host = go.AddComponent(hostType);
            var data = go.AddComponent(dataType);
            hostType.GetField("_dataM", flags).SetValue(host, data);
            dataType.GetField("<PlayFabId>k__BackingField", flags).SetValue(data, session.AccountId);
            dataType.GetField("<IsServerDataReady>k__BackingField", flags).SetValue(data, true);
            singleton.SetValue(null, host);
            Assert.That(RecoveryGateType.GetMethod("Open").Invoke(gate, new object[] { root, true, true }), Is.True);
            Assert.That(gate.TryBeginVerification(), Is.True);
            manager = Activator.CreateInstance(ManagerType, new object[] { verifier, (Func<ReceiptSession>)(() => session), 1, gate });
            ManagerType.GetField("_connected", flags).SetValue(manager, true);
            var first = new PendingOrder(new Cart(Item()), new FakeOrderInfo());
            var unfinished = (System.Collections.IDictionary)ManagerType.GetField("_unfinished", flags).GetValue(manager);
            unfinished[first.Info.TransactionID] = first;
            var completion = (System.Threading.Tasks.Task)ManagerType.GetMethod("VerifyAndFulfillAsync", flags)
                .Invoke(manager, new object[] { first.Info.TransactionID, first, 0 });
            Assert.That(completion.IsCompleted, Is.False);
            Assert.That(verifier.Calls, Is.EqualTo(1));
            var duplicate = new PendingOrder(new Cart(Item()), new FakeOrderInfo());
            ManagerType.GetMethod("OnPurchasePending", flags).Invoke(manager, new object[] { duplicate });
            ManagerType.GetMethod("OnPurchasesFetched", flags).Invoke(manager, new object[] {
                new Orders(Array.Empty<ConfirmedOrder>(), new[] { duplicate }, Array.Empty<DeferredOrder>()) });
            verifier.Completion.SetResult(true);
            await completion;
            Assert.That(unfinished[first.Info.TransactionID], Is.SameAs(first));
            Assert.That(verifier.Calls, Is.EqualTo(1));
            // No local save was initialized. Reaching this state proves the verified result
            // survived duplicate delivery and was stopped by the real persistence guard.
            Assert.That(ManagerType.GetProperty("Status").GetValue(manager).ToString(), Is.EqualTo("WaitingForPersistence"));
            Assert.That(ManagerType.GetField("_store", flags).GetValue(manager), Is.Null,
                "No Store, Confirm or network was started by this continuation test.");
        }
        finally
        {
            verifier.Completion.TrySetResult(false);
            (manager as IDisposable)?.Dispose();
            singleton.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(go);
            System.IO.Directory.Delete(root, true); // Only this unique synthetic test root.
        }
    }

    private static void WithOfflinePurchases(Action<object, OfflinePurchases> check)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = Activator.CreateInstance(ManagerType);
        var purchases = new OfflinePurchases();
        var store = (StoreController)FormatterServices.GetUninitializedObject(typeof(StoreController));
        typeof(StoreController).GetField("m_PurchaseService", flags).SetValue(store, purchases);
        ManagerType.GetField("_store", flags).SetValue(manager, store);
        ManagerType.GetField("_connected", flags).SetValue(manager, true);
        try { check(manager, purchases); }
        finally
        {
            // Only the purchase service was supplied; no event subscriptions were made.
            ManagerType.GetField("_store", flags).SetValue(manager, null);
            ((IDisposable)manager).Dispose();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_IAP_UnavailableCatalogStillFetchesOwnedPurchases(bool failed)
    {
        WithOfflinePurchases((manager, purchases) =>
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ManagerType.GetField("_initialFetchInProgress", flags).SetValue(manager, true);
            ManagerType.GetMethod(failed ? "OnProductsFetchFailed" : "OnProductsFetched", flags)
                .Invoke(manager, new object[] { failed ? null : (object)new List<Product>() });
            Assert.That(purchases.Fetches, Is.EqualTo(1));
            Assert.That(ManagerType.GetProperty("IsReadyToPurchase").GetValue(manager), Is.False);
            Assert.That(ManagerType.GetField("_initialFetchInProgress", flags).GetValue(manager), Is.True);
            ManagerType.GetMethod("OnPurchasesFetched", flags).Invoke(manager, new object[] {
                new Orders(Array.Empty<ConfirmedOrder>(), Array.Empty<PendingOrder>(), Array.Empty<DeferredOrder>()) });
            Assert.That(ManagerType.GetField("_purchasesLoaded", flags).GetValue(manager), Is.True);
            Assert.That(ManagerType.GetProperty("IsReadyToPurchase").GetValue(manager), Is.False);
        });
    }

    [Test]
    public void Revival_IAP_RestoreDoesNotRequirePurchasableCatalogAndDoesNotOverlap()
    {
        WithOfflinePurchases((manager, purchases) =>
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var restore = ManagerType.GetMethod("RestorePurchases");
            ManagerType.GetField("_initialFetchInProgress", flags).SetValue(manager, true);
            restore.Invoke(manager, null);
            Assert.That(purchases.Restores, Is.Zero);
            ManagerType.GetField("_initialFetchInProgress", flags).SetValue(manager, false);
            restore.Invoke(manager, null);
            restore.Invoke(manager, null);
            Assert.That(purchases.Restores, Is.EqualTo(1));
            Assert.That(ManagerType.GetProperty("IsReadyToPurchase").GetValue(manager), Is.False);
            Assert.That(ManagerType.GetMethod("ShouldRetryInitialization", flags)
                .Invoke(manager, new object[] { DateTime.UtcNow.AddMinutes(1) }), Is.False,
                "Catalog retry must not overlap an active restore when sales are unavailable.");
        });
    }

    [Test]
    public void Revival_IAP_OwnedPurchaseFetchExceptionReleasesRetryGuard()
    {
        WithOfflinePurchases((manager, purchases) =>
        {
            purchases.ThrowOnFetch = true;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ManagerType.GetMethod("OnProductsFetchFailed", flags).Invoke(manager, new object[] { null });
            Assert.That(purchases.Fetches, Is.EqualTo(1));
            Assert.That(ManagerType.GetField("_initialFetchInProgress", flags).GetValue(manager), Is.False);
            Assert.That(ManagerType.GetField("_purchasesLoaded", flags).GetValue(manager), Is.False);
        });
    }

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
