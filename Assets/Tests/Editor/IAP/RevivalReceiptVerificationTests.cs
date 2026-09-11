using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AD.Purchasing;
using NUnit.Framework;

public class RevivalReceiptVerificationTests
{
    private sealed class FakeVerifier : IReceiptVerifier
    {
        public readonly TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>();
        public int Calls;
        public Task<bool> VerifyAsync(string receipt, ReceiptSession session, CancellationToken token)
        {
            Calls++;
            return Completion.Task;
        }
    }

    [Test]
    public async Task Revival_Receipt_OnlyVerifiedCurrentSessionCanReachDurableGrantThenConfirm()
    {
        var verifier = new FakeVerifier();
        var session = new ReceiptSession("test-a", "synthetic-ticket");
        string sequence = "";
        var fulfillment = new NoAdsPurchaseFulfillment(() => { sequence += "persist;"; return true; });
        var pending = ReceiptVerification.VerifyCurrentAsync(verifier, "synthetic-receipt", () => session, CancellationToken.None);
        Assert.That(pending.IsCompleted, Is.False);
        Assert.That(sequence, Is.Empty);
        verifier.Completion.SetResult(true);
        if (await pending) fulfillment.Process(PurchaseDeliveryState.Pending,
            new[] { NoAdsPurchaseFulfillment.ProductId }, () => sequence += "confirm;");
        Assert.That(sequence, Is.EqualTo("persist;confirm;"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Revival_Receipt_AccountSwitchOrTicketRenewalRejectsLateSuccess(bool renewTicket)
    {
        var verifier = new FakeVerifier();
        var session = new ReceiptSession("test-a", "ticket-a");
        var pending = ReceiptVerification.VerifyCurrentAsync(verifier, "receipt", () => session, CancellationToken.None);
        session = renewTicket ? new ReceiptSession("test-a", "ticket-b") : new ReceiptSession("test-b", "ticket-a");
        verifier.Completion.SetResult(true);
        Assert.That(await pending, Is.False);
    }

    [Test]
    public async Task Revival_Receipt_TransportRejectionOrFaultNeverVerifies()
    {
        var session = new ReceiptSession("test-a", "ticket");
        var rejected = new FakeVerifier();
        rejected.Completion.SetResult(false);
        Assert.That(await ReceiptVerification.VerifyCurrentAsync(rejected, "receipt", () => session, CancellationToken.None), Is.False);
        var failed = new FakeVerifier();
        failed.Completion.SetException(new Exception("synthetic upstream failure"));
        Assert.That(await ReceiptVerification.VerifyCurrentAsync(failed, "receipt", () => session, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task Revival_Receipt_DisposalCancellationRejectsLateSuccess()
    {
        var verifier = new FakeVerifier();
        using (var lifetime = new CancellationTokenSource())
        {
            var pending = ReceiptVerification.VerifyCurrentAsync(verifier, "receipt",
                () => new ReceiptSession("test-a", "ticket"), lifetime.Token);
            lifetime.Cancel();
            verifier.Completion.SetResult(true);
            Assert.That(await pending, Is.False);
        }
    }

    [Test]
    public async Task Revival_Receipt_MissingAccountNeverContactsVerifier()
    {
        var verifier = new FakeVerifier();
        Assert.That(await ReceiptVerification.VerifyCurrentAsync(verifier, "receipt", () => null, CancellationToken.None), Is.False);
        Assert.That(await ReceiptVerification.VerifyCurrentAsync(verifier, "receipt",
            () => new ReceiptSession("test-a", ""), CancellationToken.None), Is.False);
        Assert.That(verifier.Calls, Is.Zero);
    }

    [Test]
    public async Task Revival_Receipt_SessionProviderFailureDoesNotEscapeOrContactVerifier()
    {
        var verifier = new FakeVerifier();
        Assert.That(await ReceiptVerification.VerifyCurrentAsync(verifier, "receipt",
            () => throw new InvalidOperationException("Account shutdown"), CancellationToken.None), Is.False);
        Assert.That(verifier.Calls, Is.Zero);
    }

    [Test]
    public void Revival_Receipt_AccountBindingMatchesServerContract()
    {
        Assert.That(new ReceiptSession("TEST-ACCOUNT-A", "ticket").ObfuscatedAccountId,
            Is.EqualTo("744cf32980ca96595ea8c259570a1b821c194285c0811f2265a232ec39ae273b"));
    }

    [Test]
    public void Revival_Receipt_CapturedSessionCannotGrantToReplacementPersistenceOwner()
    {
        object ownerA = new object(), ownerB = new object();
        object currentOwner = ownerA;
        var session = new ReceiptSession("account-a", "ticket-a");
        int grants = 0, confirmations = 0;
        var fulfillment = new NoAdsPurchaseFulfillment(() => ReceiptVerification.TryPersistCurrent(
            ownerA, session, () => currentOwner, () => session, () => true,
            () => "account-a", () => { grants++; return true; }));
        // The injected supplier still returns A, but actual Data ownership changed.
        currentOwner = ownerB;
        Assert.That(fulfillment.Process(PurchaseDeliveryState.Pending,
            new[] { NoAdsPurchaseFulfillment.ProductId }, () => confirmations++),
            Is.EqualTo(PurchaseFulfillmentResult.PersistenceFailed));
        Assert.That(grants, Is.Zero);
        Assert.That(confirmations, Is.Zero);
        currentOwner = ownerA;
        Assert.That(fulfillment.Process(PurchaseDeliveryState.Pending,
            new[] { NoAdsPurchaseFulfillment.ProductId }, () => confirmations++),
            Is.EqualTo(PurchaseFulfillmentResult.Fulfilled));
        Assert.That(grants, Is.EqualTo(1));
        Assert.That(confirmations, Is.EqualTo(1));
    }

    [TestCase(false, "account-a")]
    [TestCase(true, "account-b")]
    public void Revival_Receipt_SuspendedOrReboundOwnerCannotPersist(bool ready, string ownerAccount)
    {
        var owner = new object();
        var session = new ReceiptSession("account-a", "ticket-a");
        bool called = false;
        Assert.That(ReceiptVerification.TryPersistCurrent(owner, session, () => owner, () => session,
            () => ready, () => ownerAccount, () => { called = true; return true; }), Is.False);
        Assert.That(called, Is.False);
    }

    [Test]
    public void Revival_Receipt_PreCancelledTransportNeverStartsRequest()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.IapReceiptHttpVerifier")).First(t => t != null);
        var verifier = (IReceiptVerifier)Activator.CreateInstance(type, "https://example.test/v1/no-ads/verify");
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await verifier.VerifyAsync(
                "receipt", new ReceiptSession("test-a", "ticket"), source.Token));
        }
    }

    [TestCase("http://example.test/v1/no-ads/verify")]
    [TestCase("https://user:password@example.test/v1/no-ads/verify")]
    [TestCase("https://example.test/v1/no-ads/verify?redirect=elsewhere")]
    [TestCase("https://example.test/other")]
    public void Revival_Receipt_TransportRejectsUnsafeEndpointBeforeNetwork(string endpoint)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.IapReceiptHttpVerifier")).First(t => t != null);
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => Activator.CreateInstance(type, endpoint));
        Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
    }
}
