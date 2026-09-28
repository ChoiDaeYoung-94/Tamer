using AD.Advertising;
using NUnit.Framework;

public class RevivalAdRequestPolicyTests
{
    [Test]
    public void Revival_ProductionAdvertisingRemainsDisabled()
    {
        Assert.That(AdRequestPolicy.ProductionAdsEnabled, Is.False);
        for (int flags = 0; flags < 32; flags++)
            Assert.That(AdRequestPolicy.CanRequestProductionAds((flags & 1) != 0,
                (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, (flags & 16) != 0), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("ca-app-pub-3940256099942544/5224354917")]
    [TestCase("ca-app-pub-3940256099942544/9999999999")]
    [TestCase(" ca-app-pub-0123456789012345/0123456789")]
    [TestCase("ca-app-pub-0123456789012345~0123456789")]
    public void Revival_ProductionInventoryRejectsMissingInvalidAndSampleUnits(string configured)
    {
        Assert.That(AdRequestPolicy.TrySelectRewardedAdUnit(false, true, false, configured, out var unit), Is.False);
        Assert.That(unit, Is.Null);
    }

    [Test]
    public void Revival_TestInventoryNeverUsesConfiguredProductionUnit()
    {
        const string syntheticUnit = "ca-app-pub-0123456789012345/0123456789";
        Assert.That(AdRequestPolicy.TrySelectRewardedAdUnit(true, false, false, syntheticUnit, out var unit), Is.True);
        Assert.That(unit, Is.EqualTo(AdRequestPolicy.TestRewardedAdUnit(false)));
        Assert.That(AdRequestPolicy.TrySelectRewardedAdUnit(false, true, false, syntheticUnit, out unit), Is.True);
        Assert.That(unit, Is.EqualTo(syntheticUnit));
        Assert.That(AdRequestPolicy.TrySelectRewardedAdUnit(false, true, true, syntheticUnit, out unit), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_AmbiguousOrBlockedInventoryHasNoUnit(bool bothAllowed)
    {
        Assert.That(AdRequestPolicy.TrySelectRewardedAdUnit(bothAllowed, bothAllowed, false,
            "ca-app-pub-0123456789012345/0123456789", out var unit), Is.False);
        Assert.That(unit, Is.Null);
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
