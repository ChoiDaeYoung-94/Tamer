using AD.Advertising;
using NUnit.Framework;

public class RevivalAdRequestPolicyTests
{
    [Test]
    public void Revival_ProductionAdvertisingRemainsDisabled()
    {
        Assert.That(AdRequestPolicy.ProductionAdsEnabled, Is.False);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void Revival_OrdinaryMobileReleaseCannotRequestAds(bool android, bool ios)
    {
        Assert.That(AdRequestPolicy.CanRequestTestAds(false, false, false, android, ios, false),
            Is.False);
    }

    [TestCase(true, false, false, false, false)]
    [TestCase(false, true, false, true, false)]
    [TestCase(false, true, false, false, true)]
    [TestCase(false, false, true, true, false)]
    [TestCase(false, false, true, false, true)]
    public void Revival_InteractiveSupportedTestEnvironmentCanRequestTestAds(
        bool editor, bool development, bool explicitTestAds, bool android, bool ios)
    {
        Assert.That(AdRequestPolicy.CanRequestTestAds(
            editor, development, explicitTestAds, android, ios, false), Is.True);
    }

    [TestCase(true, false, false, false, false)]
    [TestCase(false, true, false, true, false)]
    [TestCase(false, false, true, false, true)]
    [TestCase(true, true, true, true, true)]
    public void Revival_BatchModeBlocksEvenPermittedTestEnvironments(
        bool editor, bool development, bool explicitTestAds, bool android, bool ios)
    {
        Assert.That(AdRequestPolicy.CanRequestTestAds(
            editor, development, explicitTestAds, android, ios, true), Is.False);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Revival_UnsupportedPlayerCannotRequestAds(bool development, bool explicitTestAds)
    {
        Assert.That(AdRequestPolicy.CanRequestTestAds(
            false, development, explicitTestAds, false, false, false), Is.False);
    }

    [TestCase(false, "ca-app-pub-3940256099942544/5224354917")]
    [TestCase(true, "ca-app-pub-3940256099942544/1712485313")]
    public void Revival_RewardedAdUnitsUseOfficialPlatformTestInventory(bool ios, string expected)
    {
        Assert.That(AdRequestPolicy.TestRewardedAdUnit(ios), Is.EqualTo(expected));
    }
}
