using System;
using AD.Advertising;
using NUnit.Framework;

public class RevivalAgeChoiceTests
{
    [TestCase("0", AgeChoice.Under13)]
    [TestCase("12", AgeChoice.Under13)]
    [TestCase("13", AgeChoice.From13To15)]
    [TestCase("15", AgeChoice.From13To15)]
    [TestCase("16", AgeChoice.From16To17)]
    [TestCase("17", AgeChoice.From16To17)]
    [TestCase("18", AgeChoice.Adult)]
    [TestCase("99", AgeChoice.Adult)]
    [TestCase("100", AgeChoice.Adult)]
    public void Revival_FreeAgeEntryPreservesTreatmentBoundariesWithoutSavingExactAge(
        string input, AgeChoice expected)
    {
        Assert.That(LocalAgeChoice.TryParseAgeInput(input, out var bucket), Is.True);
        Assert.That(bucket, Is.EqualTo(expected));
        string saved = null;
        var choice = new LocalAgeChoice(() => null, s => saved = s);
        Assert.That(choice.Select(bucket), Is.True);
        Assert.That(saved, Is.EqualTo(LocalAgeChoice.Encode(expected)));
        Assert.That(AgeTreatmentPolicy.IsReviewed(choice.Value), Is.EqualTo(expected == AgeChoice.Adult));
    }

    [TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase("-1")]
    [TestCase("+18")] [TestCase("18.0")] [TestCase("2008")]
    [TestCase("18 ")] [TestCase("１８")] [TestCase("2147483648")]
    public void Revival_InvalidAgeInputNeverBecomesAnAdultDefault(string input)
    {
        Assert.That(LocalAgeChoice.TryParseAgeInput(input, out var bucket), Is.False);
        Assert.That(bucket, Is.EqualTo(AgeChoice.Unknown));
        Assert.That(AgeTreatmentPolicy.IsReviewed(bucket), Is.False);
    }

    [TestCase(AgeChoice.Under13)]
    [TestCase(AgeChoice.From13To15)]
    [TestCase(AgeChoice.From16To17)]
    [TestCase(AgeChoice.Adult)]
    [TestCase(AgeChoice.Declined)]
    public void Revival_AgeChoicePersistsOnlyVersionAndBucketAndDoesNotAskAgain(AgeChoice value)
    {
        string stored = null;
        var choice = new LocalAgeChoice(() => stored, s => stored = s);
        Assert.That(choice.NeedsQuestion, Is.True);
        Assert.That(choice.Select(value), Is.True);
        var restarted = new LocalAgeChoice(() => stored, _ => Assert.Fail("Reading must not write."));
        Assert.That(restarted.Value, Is.EqualTo(value));
        Assert.That(restarted.NeedsQuestion, Is.False);
        Assert.That(restarted.HasAge, Is.EqualTo(value != AgeChoice.Declined));
        Assert.That(stored.Split('|').Length, Is.EqualTo(2));
    }

    [TestCase(null)] [TestCase("")] [TestCase("2|under13")]
    [TestCase("1|unknown")] [TestCase("1|18plus|extra")]
    [TestCase("1|18plus ")] [TestCase("1|999")] [TestCase("garbage")]
    public void Revival_AgeMissingCorruptOrUnsupportedRequiresQuestion(string stored)
    {
        var choice = new LocalAgeChoice(() => stored, _ => { });
        Assert.That(choice.Value, Is.EqualTo(AgeChoice.Unknown));
        Assert.That(choice.NeedsQuestion, Is.True);
        Assert.That(choice.HasAge, Is.False);
    }

    [Test]
    public void Revival_AgeChangeBlocksBeforePersistenceAndNotifiesNewContext()
    {
        LocalAgeChoice choice = null;
        int events = 0;
        choice = new LocalAgeChoice(() => "1|18plus", value =>
        {
            Assert.That(choice.HasAge, Is.False);
            Assert.That(events, Is.EqualTo(1));
        });
        choice.Changed += () => events++;
        choice.BeginEdit();
        choice.BeginEdit();
        Assert.That(choice.Select(AgeChoice.Declined), Is.True);
        Assert.That(events, Is.EqualTo(2));
        Assert.That(choice.NeedsQuestion, Is.False);
        Assert.That(choice.HasAge, Is.False);
    }

    [Test]
    public void Revival_AgeStorageFailureNeverPublishesNewChoice()
    {
        var choice = new LocalAgeChoice(() => "1|18plus", _ => throw new Exception());
        Assert.That(choice.Select(AgeChoice.Under13), Is.False);
        Assert.That(choice.Value, Is.EqualTo(AgeChoice.Adult));
        Assert.That(choice.NeedsQuestion, Is.True);
        Assert.That(choice.HasAge, Is.False);
        Assert.That(choice.Select((AgeChoice)999), Is.False);
        var unavailable = new LocalAgeChoice(() => throw new Exception(), _ => { });
        Assert.That(unavailable.NeedsQuestion, Is.True);
    }

    [Test]
    public void Revival_ReviewedReleaseIsAdultOnlyAndPrivacyEnvironmentRemainsClosed()
    {
        foreach (AgeChoice choice in Enum.GetValues(typeof(AgeChoice)))
            Assert.That(AgeTreatmentPolicy.IsReviewed(choice), Is.EqualTo(choice == AgeChoice.Adult));
        Assert.That(AgeTreatmentPolicy.IsReviewed((AgeChoice)999), Is.False);
        Assert.That(AgeTreatmentPolicy.RegionalConsentReviewed, Is.True);
        Assert.That(AdRequestPolicy.ProductionAdsEnabled, Is.True);
        Assert.That(AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed, Is.False);
    }

    private sealed class UnstartedConsentClient : IAdConsentClient
    {
        public bool CanRequestAds => true; // Even a cached SDK permission is insufficient.
        public bool PrivacyOptionsRequired => false;
        public void Update(bool underAge, Action<bool> done) => Assert.Fail("Age alone must not update consent.");
        public void Gather(Action<bool> done) => Assert.Fail("Age alone must not gather consent.");
        public void ShowPrivacyOptions(Action<bool> done) => Assert.Fail("Age alone must not open a form.");
    }

    [Test]
    public void Revival_SelfReportedAdultAndSourceApprovalDoNotGrantConsent()
    {
        var choice = new LocalAgeChoice(() => "1|18plus", _ => Assert.Fail("Reading must not write."));
        Assert.That(choice.HasAge, Is.True);
        Assert.That(AgeTreatmentPolicy.IsReviewed(choice.Value), Is.True);
        Assert.That(AgeTreatmentPolicy.TryCreatePlan(choice.Value, out var plan), Is.True);
        using (var gate = new AdConsentGate(new UnstartedConsentClient(), plan.UmpUnderAgeOfConsent, a => a()))
        {
            Assert.That(gate.CanRequestAds, Is.False);
            Assert.That(gate.PrivacyOptionsRequired, Is.False);
        }
    }

    [TestCase(AgeChoice.Unknown, false)]
    [TestCase(AgeChoice.Under13, false)]
    [TestCase(AgeChoice.From13To15, false)]
    [TestCase(AgeChoice.From16To17, false)]
    [TestCase(AgeChoice.Adult, true)]
    [TestCase(AgeChoice.Declined, false)]
    public void Revival_FullscreenRewardedAgeGuardAndReleaseReviewRemainAdultOnly(
        AgeChoice age, bool candidate)
    {
        Assert.That(AgeTreatmentPolicy.AllowsFullscreenRewarded(age), Is.EqualTo(candidate));
        Assert.That(AgeTreatmentPolicy.IsReviewed(age), Is.EqualTo(candidate));
        Assert.That(AdRequestPolicy.ProductionAdsEnabled, Is.True);
    }

    [TestCase(AgeChoice.Under13, AdAgeTreatment.Child, true)]
    [TestCase(AgeChoice.From13To15, AdAgeTreatment.Child, true)]
    [TestCase(AgeChoice.From16To17, AdAgeTreatment.Teen, false)]
    [TestCase(AgeChoice.Adult, AdAgeTreatment.Unspecified, false)]
    public void Revival_AgeProposalSeparatesAdvertisingFromUmp(AgeChoice age, AdAgeTreatment advertising, bool tfua)
    {
        Assert.That(AgeTreatmentPolicy.TryCreatePlan(age, out var plan), Is.True);
        Assert.That(plan.Advertising, Is.EqualTo(advertising));
        Assert.That(plan.UmpUnderAgeOfConsent, Is.EqualTo(tfua));
        Assert.That(AgeTreatmentPolicy.IsReviewed(age), Is.EqualTo(age == AgeChoice.Adult));
    }

    [TestCase(AgeChoice.Unknown)]
    [TestCase(AgeChoice.Declined)]
    [TestCase((AgeChoice)999)]
    public void Revival_UnknownOrDeclinedNeverProducesTreatmentPlan(AgeChoice age)
    {
        Assert.That(AgeTreatmentPolicy.TryCreatePlan(age, out var plan), Is.False);
        Assert.That(plan, Is.Null);
    }
}
