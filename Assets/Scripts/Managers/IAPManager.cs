using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AD.Purchasing;
using UnityEngine;
using UnityEngine.Purchasing;

namespace AD
{
    public enum IAPStatus
    {
        NotInitialized, Connecting, LoadingProducts, Ready, Purchasing,
        Restoring, Deferred, WaitingForPersistence, Confirming, Failed, Unavailable,
        WaitingForVerification
    }

    /// <summary>
    /// Google Play / Apple platform billing for the existing No Ads SKU.
    /// Default construction retains the existing store-trust behavior. A reviewed
    /// server verifier can be explicitly injected before store initialization.
    /// Refund reconciliation is not performed by this adapter.
    /// </summary>
    public sealed class IAPManager : IDisposable
    {
        private StoreController _store;
        private bool _connecting;
        private bool _connected;
        private bool _productsLoaded;
        private bool _purchasesLoaded;
        private bool _initialFetchInProgress;
        private bool _purchaseInProgress;
        private bool _restoring;
        private bool _disposed;
        private DateTime _nextInitializationAttempt;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Dictionary<string, Order> _unfinished = new Dictionary<string, Order>();
        private readonly Dictionary<string, DateTime> _confirming = new Dictionary<string, DateTime>();
        private readonly HashSet<string> _completedTransactions = new HashSet<string>();
        private readonly NoAdsPurchaseFulfillment _fulfillment = new NoAdsPurchaseFulfillment(TryPersistNoAds);
        private readonly IReceiptVerifier _receiptVerifier;
        private readonly Func<ReceiptSession> _receiptSession;
        private readonly HashSet<string> _validating = new HashSet<string>();
        private int _validationGeneration;

        public IAPManager() { }

        // No production URL or secret is embedded. Deployment and test-account
        // setup must be reviewed before the composition root opts into this path.
        public IAPManager(IReceiptVerifier receiptVerifier, Func<ReceiptSession> receiptSession)
        {
            _receiptVerifier = receiptVerifier ?? throw new ArgumentNullException(nameof(receiptVerifier));
            _receiptSession = receiptSession ?? throw new ArgumentNullException(nameof(receiptSession));
        }

        public string ProductNoAds => NoAdsPurchaseFulfillment.ProductId;
        public IAPStatus Status { get; private set; } = IAPStatus.NotInitialized;
        public bool IsReadyToPurchase => !_disposed && _connected && _productsLoaded && _purchasesLoaded;

        // Kept for existing ShopMan callers. Initialization is explicit; isolated
        // baseline scenes never create a store or contact purchasing services.
        public void Init() => InitializePurchasing();

        public async void InitializePurchasing()
        {
#if TAMER_GAMEPLAY_HARNESS
            RevivalGameplayIsolation.BlockPurchase();
            Status = IAPStatus.Unavailable;
            return;
#endif
            if (_disposed || _connecting || _initialFetchInProgress || _restoring || _purchaseInProgress || IsReadyToPurchase) return;
            _connecting = true;
            _nextInitializationAttempt = DateTime.UtcNow.AddSeconds(30);
            try
            {
                if (_store == null)
                {
                    IapConsentDefaults.Apply();
                    _store = UnityIAPServices.StoreController();
                    _store.OnStoreConnected += OnStoreConnected;
                    _store.OnStoreDisconnected += OnStoreDisconnected;
                    _store.OnProductsFetched += OnProductsFetched;
                    _store.OnProductsFetchFailed += OnProductsFetchFailed;
                    _store.OnPurchasesFetched += OnPurchasesFetched;
                    _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                    _store.OnPurchasePending += OnPurchasePending;
                    _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                    _store.OnPurchaseFailed += OnPurchaseFailed;
                    _store.OnPurchaseDeferred += OnPurchaseDeferred;
                    _store.OnAuthAccountChanged += OnAuthAccountChanged;
                    // Keep the SDK's pending reroute enabled. On Android resume,
                    // its background fetch only emits OnPurchasePending, not
                    // OnPurchasesFetched (including newly approved deferred orders).
                    _store.ProcessPendingOrdersOnPurchasesFetched(true);
                    _ = RetryLoopAsync(_lifetime.Token);
                }

                if (_connected)
                {
                    if (_productsLoaded)
                    {
                        FetchOwnedPurchases();
                    }
                    else FetchCatalog();
                }
                else
                {
                    SetStatus(IAPStatus.Connecting);
                    await _store.Connect();
                }
            }
            catch (Exception)
            {
                _initialFetchInProgress = false;
                if (!_disposed) SetStatus(IAPStatus.Failed);
            }
            finally { _connecting = false; }
        }

        private void OnStoreConnected()
        {
            if (_disposed) return;
            _connected = true;
            FetchCatalog();
        }

        private void FetchCatalog()
        {
            _initialFetchInProgress = true;
            _productsLoaded = false;
            _purchasesLoaded = false;
            SetStatus(IAPStatus.LoadingProducts);
            _store.FetchProducts(new List<ProductDefinition>
            {
                new ProductDefinition(ProductNoAds, ProductType.NonConsumable)
            });
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            _connected = false;
            _productsLoaded = false;
            _purchasesLoaded = false;
            _initialFetchInProgress = false;
            _purchaseInProgress = false;
            _restoring = false;
            _confirming.Clear();
            SetStatus(IAPStatus.Unavailable);
        }

        private void OnProductsFetched(List<Product> products)
        {
            if (_disposed) return;
            _productsLoaded = products != null && products.Any(p => p != null &&
                p.definition != null && p.definition.id == ProductNoAds &&
                p.definition.type == ProductType.NonConsumable);
            // Catalog availability gates new sales, not the attempt to recover ownership.
            FetchOwnedPurchases();
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            if (_disposed) return;
            _productsLoaded = false;
            FetchOwnedPurchases();
        }

        private void FetchOwnedPurchases()
        {
            if (_disposed) return;
            if (!_connected)
            {
                _initialFetchInProgress = false;
                SetStatus(IAPStatus.Unavailable);
                return;
            }
            _initialFetchInProgress = true;
            try { _store.FetchPurchases(); }
            catch (Exception) { OnPurchasesFetchFailed(null); }
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            _initialFetchInProgress = false;
            _purchasesLoaded = false;
            _restoring = false;
            SetStatus(IAPStatus.Unavailable);
        }

        private void OnAuthAccountChanged()
        {
            if (_disposed) return;
            _validationGeneration++;
            _validating.Clear();
            // IAP 5.4 clears its caches before this event. Never use old Product
            // or Order objects for a new authenticated store session.
            _unfinished.Clear();
            _confirming.Clear();
            _completedTransactions.Clear();
            _purchaseInProgress = false;
            _restoring = false;
            FetchCatalog();
        }

        public void BuyProductID(string productId)
        {
            if (!string.Equals(productId, ProductNoAds, StringComparison.Ordinal)) return;
            if (!IsReadyToPurchase)
            {
                InitializePurchasing();
                return;
            }
            if (_purchaseInProgress || _restoring || _unfinished.Count != 0)
                return;

            Product product = _store.GetProductById(productId);
            if (product == null || !product.availableToPurchase)
            {
                SetStatus(IAPStatus.Unavailable);
                return;
            }
            if (_receiptVerifier != null)
            {
                try
                {
                    var session = _receiptSession();
                    if (session == null || !session.IsValid || _store.GooglePlayStoreExtendedService == null)
                    {
                        SetStatus(IAPStatus.Unavailable);
                        return;
                    }
                    _store.GooglePlayStoreExtendedService.SetObfuscatedAccountId(session.ObfuscatedAccountId);
                }
                catch (Exception)
                {
                    SetStatus(IAPStatus.Unavailable);
                    return;
                }
            }
            _purchaseInProgress = true;
            SetStatus(IAPStatus.Purchasing);
            try { _store.PurchaseProduct(product); }
            catch (Exception)
            {
                _purchaseInProgress = false;
                SetStatus(IAPStatus.Failed);
            }
        }

        // Can be wired to an explicit Restore Purchases button. Startup also
        // fetches confirmed non-consumables for automatic Google Play restore.
        public void RestorePurchases()
        {
            if (_disposed) return;
            if (!_connected)
            {
                InitializePurchasing();
                return;
            }
            if (_restoring || _purchaseInProgress || _initialFetchInProgress) return;
            _restoring = true;
            SetStatus(IAPStatus.Restoring);
            try
            {
                _store.RestoreTransactions((success, message) =>
                {
                    if (_disposed) return;
                    // The v5.4 SDK already fetches purchases after a successful
                    // restore. Its OnPurchasesFetched event finishes our restore.
                    if (!success)
                    {
                        _restoring = false;
                        SetStatus(IAPStatus.Failed);
                    }
                });
            }
            catch (Exception)
            {
                _restoring = false;
                SetStatus(IAPStatus.Failed);
            }
        }

        private void OnPurchasesFetched(Orders orders)
        {
            if (_disposed || orders == null) return;
            _initialFetchInProgress = false;
            _purchasesLoaded = true;
            _restoring = false;
            if (_unfinished.Count == 0) SetStatus(IAPStatus.Ready);
            foreach (var order in orders.ConfirmedOrders) QueueOrder(order);
            // Also collect pending orders as a fallback. The SDK may suppress
            // rerouting an already-seen pending order after this manager is recreated,
            // because its shared PurchaseService outlives the manager. QueueOrder's
            // in-flight and completed guards prevent duplicate acknowledgement.
            foreach (var order in orders.PendingOrders) QueueOrder(order);
            // DeferredOrders are unpaid and deliberately not granted.
        }

        private void OnPurchasePending(PendingOrder order)
        {
            _purchaseInProgress = false;
            QueueOrder(order);
        }

        private void QueueOrder(Order order)
        {
            if (_disposed || !IsSupportedPaidOrder(order)) return;
            string key = OrderKey(order);
            // A stale fetch callback may redeliver PendingOrder after a successful
            // confirmation. Google Play silently ignores a second acknowledgement
            // of that token, so never reopen a completed transaction in this session.
            if (order is PendingOrder && _completedTransactions.Contains(key)) return;
            if (order is ConfirmedOrder)
            {
                _completedTransactions.Add(key);
                _confirming.Remove(key);
            }
            _unfinished[key] = order;
            Fulfill(key, order);
        }

        private static bool IsSupportedPaidOrder(Order order)
        {
            if (!(order is PendingOrder) && !(order is ConfirmedOrder)) return false;
            if (order.Info == null || string.IsNullOrEmpty(order.Info.Receipt)) return false;
            // The v5 store cannot acknowledge an order without a transaction ID.
            if (order is PendingOrder && string.IsNullOrEmpty(order.Info.TransactionID)) return false;
            var items = order.CartOrdered?.Items();
            if (items == null || items.Count != 1) return false;
            var item = items[0];
            return item != null && item.Quantity == 1 && item.Product?.definition != null &&
                item.Product.definition.type == ProductType.NonConsumable &&
                string.Equals(item.Product.definition.id, NoAdsPurchaseFulfillment.ProductId,
                    StringComparison.Ordinal);
        }

        private static string OrderKey(Order order) =>
            string.IsNullOrEmpty(order.Info.TransactionID) ? "restored-no-ads" : order.Info.TransactionID;

        private void Fulfill(string key, Order order)
        {
            if (_disposed || !_connected) return;
            if (_confirming.TryGetValue(key, out DateTime since) &&
                DateTime.UtcNow - since < TimeSpan.FromSeconds(60)) return;
            _confirming.Remove(key);

            if (_receiptVerifier != null)
            {
                if (_validating.Add(key)) _ = VerifyAndFulfillAsync(key, order, _validationGeneration);
                return;
            }
            FulfillVerified(key, order);
        }

        private async Task VerifyAndFulfillAsync(string key, Order order, int generation)
        {
            SetStatus(IAPStatus.WaitingForVerification);
            try
            {
                var session = _receiptSession();
                var owner = Managers.Instance != null ? Managers.DataM : null;
                if (owner == null || session == null || !session.IsValid ||
                    !owner.IsServerDataReady || owner.PlayFabId != session.AccountId) return;
                bool verified = await ReceiptVerification.VerifyCurrentAsync(_receiptVerifier,
                    order.Info.Receipt, _receiptSession, _lifetime.Token);
                if (_disposed || !_connected || generation != _validationGeneration ||
                    !_unfinished.TryGetValue(key, out var current) || !ReferenceEquals(order, current)) return;
                if (verified)
                {
                    var fulfillment = new NoAdsPurchaseFulfillment(() => ReceiptVerification.TryPersistCurrent(
                        owner, session, () => Managers.Instance != null ? Managers.DataM : null,
                        _receiptSession, () => owner != null && owner.IsServerDataReady,
                        () => owner.PlayFabId, () => owner.TryGrantNoAds()));
                    FulfillVerified(key, order, fulfillment);
                }
                // Rejected/unavailable verification never grants or confirms.
                // Keep the order for the existing bounded retry loop.
            }
            catch (Exception)
            {
                // A session supplier may be torn down with its owning scene.
                // Retain the order and never expose account/receipt exception text.
            }
            finally
            {
                if (generation == _validationGeneration) _validating.Remove(key);
            }
        }

        private void FulfillVerified(string key, Order order, NoAdsPurchaseFulfillment verifiedFulfillment = null)
        {
            var pending = order as PendingOrder;
            var result = (verifiedFulfillment ?? _fulfillment).Process(pending != null ? PurchaseDeliveryState.Pending :
                PurchaseDeliveryState.Confirmed, new[] { ProductNoAds }, pending == null ? null : (Action)(() =>
                {
                    _confirming[key] = DateTime.UtcNow;
                    SetStatus(IAPStatus.Confirming);
                    _store.ConfirmPurchase(pending);
                }));

            if (result == PurchaseFulfillmentResult.PersistenceFailed)
                SetStatus(IAPStatus.WaitingForPersistence);
            else if (result == PurchaseFulfillmentResult.ConfirmationFailed)
            {
                _confirming.Remove(key);
                SetStatus(IAPStatus.Failed);
            }
            else if (result == PurchaseFulfillmentResult.Fulfilled)
            {
                if (pending == null)
                {
                    _unfinished.Remove(key);
                    if (_unfinished.Count == 0) SetStatus(IAPStatus.Ready);
                }
                // This is a UI refresh, not a new grant. Missing shop UI must not
                // turn a completed durable purchase into a failed transaction.
                try { if (ShopMan.Instance != null) ShopMan.Instance.IAPReset(); }
                catch (Exception) { DebugLogger.LogWarning("IAPManager", "No Ads UI refresh deferred."); }
            }
        }

        private static bool TryPersistNoAds() => Managers.Instance != null &&
            Managers.DataM != null && Managers.DataM.TryGrantNoAds();

        public void RetryPendingPurchases()
        {
            if (_disposed || !_connected) return;
            foreach (var entry in _unfinished.ToArray()) Fulfill(entry.Key, entry.Value);
        }

        private async Task RetryLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    if (ShouldRetryInitialization(DateTime.UtcNow))
                        InitializePurchasing();
                    RetryPendingPurchases();
                }
            }
            catch (OperationCanceledException) { }
        }

        private bool ShouldRetryInitialization(DateTime now) => !_disposed && !_connecting &&
            !_initialFetchInProgress && !_restoring && !_purchaseInProgress &&
            !IsReadyToPurchase && now >= _nextInitializationAttempt;

        private void OnPurchaseConfirmed(Order order)
        {
            if (_disposed || order?.Info == null) return;
            string key = OrderKey(order);
            _confirming.Remove(key);
            if (order is ConfirmedOrder)
            {
                _completedTransactions.Add(key);
                _unfinished.Remove(key);
                SetStatus(IAPStatus.Ready);
            }
            else if (order is FailedOrder)
                SetStatus(IAPStatus.Failed); // Retain the saved grant and retry acknowledgement.
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            _purchaseInProgress = false;
            SetStatus(IAPStatus.Failed);
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            _purchaseInProgress = false;
            SetStatus(IAPStatus.Deferred);
        }

        private void SetStatus(IAPStatus status)
        {
            if (_disposed || Status == status) return;
            Status = status;
            // SDK details may contain receipts or account identifiers.
            DebugLogger.Log("IAPManager", "Purchase state: " + status);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _validationGeneration++;
            _validating.Clear();
            _lifetime.Cancel();
            _lifetime.Dispose();
            if (_store != null)
            {
                _store.OnStoreConnected -= OnStoreConnected;
                _store.OnStoreDisconnected -= OnStoreDisconnected;
                _store.OnProductsFetched -= OnProductsFetched;
                _store.OnProductsFetchFailed -= OnProductsFetchFailed;
                _store.OnPurchasesFetched -= OnPurchasesFetched;
                _store.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
                _store.OnPurchasePending -= OnPurchasePending;
                _store.OnPurchaseConfirmed -= OnPurchaseConfirmed;
                _store.OnPurchaseFailed -= OnPurchaseFailed;
                _store.OnPurchaseDeferred -= OnPurchaseDeferred;
                _store.OnAuthAccountChanged -= OnAuthAccountChanged;
            }
            _unfinished.Clear();
            _confirming.Clear();
            _completedTransactions.Clear();
            _store = null;
        }
    }
}
