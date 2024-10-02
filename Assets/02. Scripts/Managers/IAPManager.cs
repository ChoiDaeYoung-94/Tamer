#pragma warning disable CS0618 // UnityPurchasing.Initialize(this, builder);

using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
using Unity.Services.Core;

namespace AD
{
    public class IAPManager : IStoreListener
    {
        private static IStoreController storeController;
        private static IExtensionProvider storeExtensionProvider;

        public string PRODUCT_NO_ADS = "com.aedeong.monstertamer.no_ads";

        async internal void Init()
        {
            await InitializeUGS();
        }

        private async System.Threading.Tasks.Task InitializeUGS()
        {
            try
            {
                await UnityServices.InitializeAsync();
                AD.Debug.Log("IAPManager", "Unity Gaming Services initialized successfully.");

                InitializePurchasing();
            }
            catch (Exception e)
            {
                AD.Debug.LogError("IAPManager", "Unity Gaming Services 초기화 실패: " + e.Message);
            }
        }

        public void InitializePurchasing()
        {
            if (IsInitialized())
                return;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            builder.AddProduct(PRODUCT_NO_ADS, ProductType.NonConsumable);

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
                    AD.Debug.Log("IAPManager", $"Purchasing product asynchronously: {product.definition.id}");
                    storeController.InitiatePurchase(product);
                }
                else
                {
                    AD.Debug.Log("IAPManager", "BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
                }
            }
            else
            {
                AD.Debug.Log("IAPManager", "BuyProductID FAIL. Not initialized.");
            }
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            AD.Debug.Log("IAPManager", "OnInitialized: PASS");

            storeController = controller;
            storeExtensionProvider = extensions;

            CheckProduct();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            AD.Debug.Log("IAPManager", $"OnInitializeFailed InitializationFailureReason:{error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            AD.Debug.Log("IAPManager", $"OnInitializeFailed InitializationFailureReason:{error}\nmessage:{message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (String.Equals(args.purchasedProduct.definition.id, PRODUCT_NO_ADS, StringComparison.Ordinal))
            {
                AD.Debug.Log("IAPManager", "ProcessPurchase: PASS. No Ads purchased.");
                GrantNoAds();
            }
            else
            {
                AD.Debug.Log("IAPManager", $"ProcessPurchase: FAIL. Unrecognized product: {args.purchasedProduct.definition.id}");
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            AD.Debug.Log("IAPManager", $"OnPurchaseFailed: FAIL. Product: '{product.definition.storeSpecificId}', PurchaseFailureReason: {failureReason}");
        }

        private void CheckProduct()
        {
            if (CheckProductID(PRODUCT_NO_ADS))
            {
                string temp_str = AD.Managers.DataM._dic_player["GooglePlay"];
                if (string.IsNullOrEmpty(temp_str))
                    temp_str = $"{AD.Define.IAPItems.PRODUCT_NO_ADS}";
                else
                    temp_str += $",{AD.Define.IAPItems.PRODUCT_NO_ADS}";

                AD.Managers.DataM.UpdateLocalData(key: "GooglePlay", value: temp_str);
            }
        }

        public bool CheckProductID(string productId)
        {
            if (IsInitialized())
            {
                Product product = storeController.products.WithID(productId);

                if (product != null)
                {
                    return product.hasReceipt;
                }
            }
            else
            {
                AD.Debug.Log("IAPManager", "BuyProductID FAIL. Not initialized.");
                return false;
            }

            return false;
        }

        private void GrantNoAds()
        {

        }
    }
}