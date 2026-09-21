using System;
using AD.Advertising;
using NUnit.Framework;

public class RevivalAgeChoiceTests
{
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
    public void Revival_AgeChoiceIsNotRegionalConsentOrProductionPermission()
    {
        foreach (AgeChoice choice in Enum.GetValues(typeof(AgeChoice)))
            Assert.That(AgeTreatmentPolicy.IsReviewed(choice), Is.False);
        Assert.That(AdRequestPolicy.ProductionAdsEnabled, Is.False);
    }
}
