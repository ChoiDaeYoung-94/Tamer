using AD.Advertising;
using NUnit.Framework;

public class RevivalAdEntitlementTests
{
    [TestCase("ProductNoAds")]
    [TestCase("ProductOne,ProductNoAds,ProductTwo")]
    [TestCase("ProductNoAds,ProductOne")]
    [TestCase("ProductOne,ProductNoAds")]
    [TestCase("  ProductNoAds  ")]
    [TestCase("ProductOne, \tProductNoAds\r\n ,ProductTwo")]
    [TestCase(",,ProductNoAds,,")]
    public void Revival_ExistingExactNoAdsPurchaseRemainsRecognized(string purchases)
    {
        Assert.That(AdEntitlement.HasNoAds(purchases), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("null")]
    [TestCase(" \t\r\n ")]
    [TestCase(",,,")]
    [TestCase("ProductOne,ProductTwo")]
    [TestCase("ProductNoAdsExtra")]
    [TestCase("FakeProductNoAds")]
    [TestCase("ProductOne,ProductNoAdsExtra,ProductTwo")]
    [TestCase("ProductOne,FakeProductNoAds,ProductTwo")]
    [TestCase("productnoads")]
    [TestCase("PRODUCTNOADS")]
    [TestCase("Product NoAds")]
    public void Revival_MissingOrNonmatchingPurchasesDoNotGrantNoAds(string purchases)
    {
        Assert.That(AdEntitlement.HasNoAds(purchases), Is.False);
    }
}
