using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UnityConsent;

public class RevivalIAPConfigurationTests
{
    [Serializable]
    private class Catalog
    {
        public bool enableCodelessAutoInitialization;
        public bool enableUnityGamingServicesAutoInitialization;
        public CatalogProduct[] products;
    }

    [Serializable]
    private class CatalogProduct
    {
        public string id;
        public int type;
    }

    [Test]
    public void Revival_IAP_CatalogPreservesNoAdsWithoutAutomaticServiceStartup()
    {
        var json = Resources.Load<TextAsset>("IAPProductCatalog");
        Assert.That(json, Is.Not.Null);
        var catalog = JsonUtility.FromJson<Catalog>(json.text);
        Assert.That(catalog.enableCodelessAutoInitialization, Is.False);
        Assert.That(catalog.enableUnityGamingServicesAutoInitialization, Is.False);
        Assert.That(catalog.products.Length, Is.EqualTo(1));
        Assert.That(catalog.products[0].id, Is.EqualTo(AD.Purchasing.NoAdsPurchaseFulfillment.ProductId));
        Assert.That(catalog.products[0].type, Is.EqualTo(1), "Keep the existing non-consumable product type.");
    }

    private static ConsentState SafeDefaults(ConsentState state)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("AD.IapConsentDefaults")).First(t => t != null);
        return (ConsentState)type.GetMethod("WithSafeDefaults", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { state });
    }

    [Test]
    public void Revival_IAP_UnspecifiedConsentDefaultsToDenied()
    {
        // Exercise the pure mapping only: this test never changes native consent,
        // starts IAP/UGS, or reads an account or a purchase receipt.
        var result = SafeDefaults(new ConsentState
        {
            AdsIntent = ConsentStatus.Unspecified,
            AnalyticsIntent = ConsentStatus.Unspecified
        });
        Assert.That(result.AdsIntent, Is.EqualTo(ConsentStatus.Denied));
        Assert.That(result.AnalyticsIntent, Is.EqualTo(ConsentStatus.Denied));
    }

    [Test]
    public void Revival_IAP_ExplicitConsentChoicesArePreserved()
    {
        var result = SafeDefaults(new ConsentState
        {
            AdsIntent = ConsentStatus.Denied,
            AnalyticsIntent = ConsentStatus.Granted
        });
        Assert.That(result.AdsIntent, Is.EqualTo(ConsentStatus.Denied));
        Assert.That(result.AnalyticsIntent, Is.EqualTo(ConsentStatus.Granted));
    }
}
