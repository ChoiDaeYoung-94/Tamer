using System.Threading;
using System.Threading.Tasks;
using AD.Purchasing;
using NUnit.Framework;

public class RevivalPlayFabReceiptTests
{
    [TestCase(PlayFabReceiptOutcome.Granted, false, true, 0)]
    [TestCase(PlayFabReceiptOutcome.Rejected, true, false, 0)]
    [TestCase(PlayFabReceiptOutcome.AlreadyUsed, false, false, 1)]
    [TestCase(PlayFabReceiptOutcome.AlreadyUsed, true, true, 1)]
    public async Task Revival_PlayFab_DuplicateRequiresCurrentAccountsInventory(
        PlayFabReceiptOutcome outcome, bool owned, bool expected, int expectedReads)
    {
        var session = new ReceiptSession("test-account", "synthetic-ticket");
        int reads = 0;
        var verifier = new PlayFabNoAdsVerification((r, s, t) => Task.FromResult(outcome),
            (s, t) => { Assert.That(s, Is.SameAs(session)); reads++; return Task.FromResult(owned); });
        Assert.That(await verifier.VerifyAsync("receipt", session, CancellationToken.None), Is.EqualTo(expected));
        Assert.That(reads, Is.EqualTo(expectedReads));
    }

    [Test]
    public async Task Revival_PlayFab_AccountSwitchRejectsLateInventorySuccess()
    {
        var inventory = new TaskCompletionSource<bool>();
        var session = new ReceiptSession("account-a", "ticket-a");
        var verifier = new PlayFabNoAdsVerification((r, s, t) => Task.FromResult(PlayFabReceiptOutcome.AlreadyUsed),
            (s, t) => inventory.Task);
        var pending = ReceiptVerification.VerifyCurrentAsync(verifier, "receipt", () => session, CancellationToken.None);
        session = new ReceiptSession("account-b", "ticket-b");
        inventory.SetResult(true);
        Assert.That(await pending, Is.False);
    }

    [Test]
    public async Task Revival_PlayFab_CancelledValidationNeverReadsInventory()
    {
        using (var source = new CancellationTokenSource())
        {
            int reads = 0;
            var verifier = new PlayFabNoAdsVerification((r, s, t) =>
            { source.Cancel(); return Task.FromResult(PlayFabReceiptOutcome.AlreadyUsed); },
                (s, t) => { reads++; return Task.FromResult(true); });
            Assert.That(await ReceiptVerification.VerifyCurrentAsync(verifier, "receipt",
                () => new ReceiptSession("test-a", "ticket"), source.Token), Is.False);
            Assert.That(reads, Is.Zero);
        }
    }
}
