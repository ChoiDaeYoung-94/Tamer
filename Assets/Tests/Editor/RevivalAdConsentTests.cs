using System;
using System.Collections.Generic;
using AD.Advertising;
using NUnit.Framework;

public class RevivalAdConsentTests
{
    private sealed class Client : IAdConsentClient
    {
        public bool Allowed, Required, UnderAge;
        public int Updates, Forms, Privacy;
        public bool ThrowUpdate, ThrowGather, ThrowRead;
        public Action<bool> Updated, Gathered, PrivacyClosed;
        public bool CanRequestAds => ThrowRead ? throw new InvalidOperationException() : Allowed;
        public bool PrivacyOptionsRequired => Required;
        public void Update(bool underAge, Action<bool> done)
        {
            Updates++; UnderAge = underAge; Updated = done;
            if (ThrowUpdate) throw new InvalidOperationException();
        }
        public void Gather(Action<bool> done)
        {
            Forms++; Gathered = done;
            if (ThrowGather) throw new InvalidOperationException();
        }
        public void ShowPrivacyOptions(Action<bool> done) { Privacy++; PrivacyClosed = done; }
    }

    Client client;
    AdConsentGate gate;
    Queue<Action> callbacks;
    List<bool> results;
    [SetUp]
    public void SetUp()
    {
        client = new Client(); callbacks = new Queue<Action>(); results = new List<bool>();
        gate = new AdConsentGate(client, a => callbacks.Enqueue(a));
    }
    [TearDown] public void TearDown() => gate.Dispose();
    void Drain() { while (callbacks.Count > 0) callbacks.Dequeue()(); }
    void Ready()
    {
        gate.Request(results.Add); client.Updated(true); Drain();
        client.Allowed = true; client.Gathered(true); Drain();
    }

    [Test]
    public void Revival_ConsentDoesNoStartupWorkAndBlocksCachedConsentUntilUpdate()
    {
        client.Allowed = true;
        Assert.That(gate.CanRequestAds, Is.False);
        Assert.That(client.Updates + client.Forms, Is.Zero);
        gate.Request(results.Add);
        Assert.That(client.UnderAge, Is.True);
        Assert.That(gate.CanRequestAds, Is.False);
        Assert.That(client.Forms, Is.Zero);
        client.Updated(true);
        Assert.That(client.Forms, Is.Zero, "Worker callback must wait for owner dispatch.");
        Drain();
        Assert.That(client.Forms, Is.EqualTo(1));
        Assert.That(gate.CanRequestAds, Is.False);
        client.Gathered(true); Drain();
        Assert.That(results, Is.EqualTo(new[] { true }));
        Assert.That(gate.CanRequestAds, Is.True);
    }

    [Test]
    public void Revival_ConsentDuplicateRequestsAndCallbacksCannotInitializeTwice()
    {
        gate.Request(results.Add);
        Assert.That(gate.Request(results.Add), Is.False);
        client.Updated(true); client.Updated(true); Drain();
        client.Allowed = true;
        client.Gathered(true); client.Gathered(true); Drain();
        Assert.That(client.Updates, Is.EqualTo(1));
        Assert.That(client.Forms, Is.EqualTo(1));
        Assert.That(results, Is.EqualTo(new[] { true }));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void Revival_ConsentFailureBlocksEvenIfPreviousSessionAllows(bool updateSuccess, bool formSuccess)
    {
        client.Allowed = true;
        gate.Request(results.Add); client.Updated(updateSuccess); Drain();
        if (updateSuccess) { client.Gathered(formSuccess); Drain(); }
        Assert.That(gate.CanRequestAds, Is.False);
        Assert.That(results, Is.EqualTo(new[] { false }));
        Assert.That(gate.IsBusy, Is.False);
    }

    [Test]
    public void Revival_SuccessfulFormStillRequiresSdkCanRequestAds()
    {
        gate.Request(results.Add); client.Updated(true); Drain(); client.Gathered(true); Drain();
        Assert.That(gate.CanRequestAds, Is.False);
        Assert.That(results, Is.EqualTo(new[] { false }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Revival_DestroyedConsentOwnerDiscardsLateUpdateOrForm(bool atUpdate)
    {
        gate.Request(results.Add);
        if (!atUpdate) { client.Updated(true); Drain(); }
        gate.Dispose(); client.Allowed = true;
        if (atUpdate) client.Updated(true); else client.Gathered(true);
        Drain();
        Assert.That(results, Is.Empty);
        Assert.That(gate.CanRequestAds, Is.False);
        Assert.That(gate.Request(results.Add), Is.False);
    }

    [Test]
    public void Revival_ManualRetryRejectsCallbackFromPreviousConsentAttempt()
    {
        gate.Request(results.Add); var stale = client.Updated;
        stale(false); Drain();
        gate.Request(results.Add);
        stale(true); Drain();
        Assert.That(client.Forms, Is.Zero);
        client.Updated(true); Drain(); client.Allowed = true; client.Gathered(true); Drain();
        Assert.That(results, Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void Revival_PrivacyChoiceInvalidatesPermissionAndDoesNotRequestAnotherAd()
    {
        Ready(); client.Required = true;
        Assert.That(gate.OpenPrivacyOptions(results.Add), Is.True);
        Assert.That(gate.CanRequestAds, Is.False);
        Assert.That(gate.Request(results.Add), Is.False);
        client.Allowed = false; client.PrivacyClosed(true); Drain();
        Assert.That(gate.CanRequestAds, Is.False);
        Assert.That(client.Updates, Is.EqualTo(1));
        Assert.That(client.Forms, Is.EqualTo(1));
        Assert.That(results, Is.EqualTo(new[] { true, false }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Revival_ConsentSdkExceptionsReleaseBusyAndBlock(bool updateThrows)
    {
        client.ThrowUpdate = updateThrows; client.ThrowGather = !updateThrows;
        gate.Request(results.Add);
        if (!updateThrows) { client.Updated(true); Drain(); }
        Assert.That(results, Is.EqualTo(new[] { false }));
        Assert.That(gate.IsBusy, Is.False);
    }

    [Test]
    public void Revival_ConsentPermissionReadExceptionFailsClosed()
    {
        gate.Request(results.Add); client.Updated(true); Drain(); client.ThrowRead = true;
        client.Gathered(true); Drain();
        Assert.That(results, Is.EqualTo(new[] { false }));
        Assert.That(gate.IsBusy, Is.False);
    }
}
