#pragma warning disable CS0618 // UnityPurchasing.Initialize(this, builder);

using System;

using UnityEngine.Purchasing;

using Unity.Services.Core;

namespace AD
{
    public class IAPManager : IStoreListener
    {
        private static IStoreController storeController;
        private static IExtensionProvider storeExtensionProvider;

        public string ProductNoAds = "com.aedeong.monstertamer.no_ads";

        async public void Init()
        {
            await InitializeUGS();
        }

        private async System.Threading.Tasks.Task InitializeUGS()
        {
            try
            {
                await UnityServices.InitializeAsync();
                AD.DebugLogger.Log("IAPManager", "Unity Gaming Services initialized successfully.");

                InitializePurchasing();
            }
            catch (Exception e)
            {
                AD.DebugLogger.LogError("IAPManager", $"Unity Gaming Services 초기화 실패: {e.Message}");
            }
        }

        public void InitializePurchasing()
        {
            if (IsInitialized())
                return;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            builder.AddProduct(ProductNoAds, ProductType.NonConsumable);

            UnityPurchasing.Initialize(this, builder);
        }

        private bool IsInitialized()
        {
            return storeController != null && storeExtensionProvider != null;
        }

        public void BuyProductID(string productId)
        {
            if (IsInitialized())
            {
                Product product = storeController.products.WithID(productId);

                if (product != null && product.availableToPurchase)
                {
                    AD.DebugLogger.Log("IAPManager", $"Purchasing product asynchronously: {product.definition.id}");
                    storeController.InitiatePurchase(product);
                }
                else
                {
                    AD.DebugLogger.Log("IAPManager", "BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
                }
            }
            else
            {
                AD.DebugLogger.Log("IAPManager", "BuyProductID FAIL. Not initialized.");
            }
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            AD.DebugLogger.Log("IAPManager", "OnInitialized: PASS");
            storeController = controller;
            storeExtensionProvider = extensions;
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            AD.DebugLogger.Log("IAPManager", $"OnInitializeFailed InitializationFailureReason:{error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            AD.DebugLogger.Log("IAPManager", $"OnInitializeFailed InitializationFailureReason:{error}\nmessage:{message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (String.Equals(args.purchasedProduct.definition.id, ProductNoAds, StringComparison.Ordinal))
            {
                AD.DebugLogger.Log("IAPManager", "ProcessPurchase: PASS. No Ads purchased.");
                GrantNoAds();
            }
            else
            {
                AD.DebugLogger.Log("IAPManager", $"ProcessPurchase: FAIL. Unrecognized product: {args.purchasedProduct.definition.id}");
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            AD.DebugLogger.Log("IAPManager", $"OnPurchaseFailed: FAIL. Product: '{product.definition.storeSpecificId}', PurchaseFailureReason: {failureReason}");
        }

        private void RegisterIAPData(AD.GameConstants.IAPItem iapItem)
        {
            string existingData = AD.Managers.DataM.LocalPlayerData["GooglePlay"];
            string newData = string.IsNullOrEmpty(existingData) ? $"{iapItem}" : $"{existingData},{iapItem}";

            AD.Managers.DataM.UpdateLocalData(key: "GooglePlay", value: newData);
        }

        private void GrantNoAds()
        {
            RegisterIAPData(AD.GameConstants.IAPItem.ProductNoAds);
            AD.Managers.DataM.UpdatePlayerData();

            ShopMan.Instance.IAPReset();
        }
    }
}