using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using NUnit.Framework;

public class RevivalDeletionReceiptTests
{
    private string _directory;
    private FaultStore _store;
    private ReceiptTestKeys _keys;
    private Gateway _gateway;
    private DeletionRecovery _record;
    private DeletionReceiptClient Client() => new DeletionReceiptClient(_gateway, _keys, _store);
    private sealed class FaultStore : IDeletionRecoveryStore
    {
        public FileDeletionRecoveryStore Inner;
        public bool FailRegisteredSave;
        public DeletionRecovery Load() => Inner.Load();
        public void Save(DeletionRecovery r) { if (FailRegisteredSave && r.ReceiptRegistered) throw new IOException(); Inner.Save(r); }
        public void Clear() { Inner.Clear(); }
    }
    private sealed class Gateway : IDeletionReceiptGateway
    {
        public DeletionRecovery Record;
        public string Verifier;
        public bool Accepted, LoseRegistration, Acked, Mismatch;
        public int Reads, Registers, Acks;
        public Action AfterAck;
        public Task<DeletionReceipt> RegisterReceiptAsync(DeletionAuthorization auth, string id, string verifier, CancellationToken t)
        {
            Registers++;
            Assert.That(auth.Proof, Is.EqualTo("synthetic-proof"));
            if (Verifier != null) Assert.That(verifier, Is.EqualTo(Verifier));
            Verifier = verifier;
            if (LoseRegistration) { LoseRegistration = false; throw new IOException(); }
            return Task.FromResult(Response(false));
        }
        public Task<DeletionReceipt> ReadReceiptAsync(string id, string cap, CancellationToken t)
        {
            Reads++;
            Assert.That(DeletionReceiptClient.Verifier(cap), Is.EqualTo(Verifier));
            Assert.That(Acked, Is.False);
            return Task.FromResult(Response(true));
        }
        public Task AcknowledgeReceiptAsync(string id, string cap, CancellationToken t)
        { Acks++; Assert.That(DeletionReceiptClient.Verifier(cap), Is.EqualTo(Verifier)); Acked = true; AfterAck?.Invoke(); return Task.CompletedTask; }
        private DeletionReceipt Response(bool reading) => new DeletionReceipt { requestId = Record.RequestId, clientKey = Record.ClientKey,
            ownerHash = Mismatch ? DeletionRecovery.Hash("foreign-owner") : Record.OwnerHash, binding = Record.Binding,
            expiresAt = 2000, policyRevision = Record.Revision, scope = "title",
            state = reading ? "processing" : "awaiting_confirmation", submissionState = reading ? (Accepted ? "accepted" : "submission_unknown") : "not_submitted" };
    }
    [SetUp] public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "receipt-fixture-" + Guid.NewGuid().ToString("N"));
        _store = new FaultStore { Inner = new FileDeletionRecoveryStore(Path.Combine(_directory, "pending.json")) };
        _keys = new ReceiptTestKeys();
        _record = new DeletionRecovery { Origin = "https://example.invalid/", Title = "ABC12", ClientKey = new string('a', 32),
            OwnerHash = DeletionRecovery.Hash("https://example.invalid/", "ABC12", "synthetic-a"),
            Binding = DeletionRecovery.Hash("https://example.invalid/", "ABC12", "synthetic-a", "entity-a"), RequestId = "request-one", Revision = "v1" };
        _gateway = new Gateway { Record = _record };
    }
    [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private Task Register() => Client().RegisterAsync(new DeletionAuthorization("synthetic-a", "synthetic-proof"), _record, CancellationToken.None);

    [Test] public async Task Revival_DeletionReceiptAcceptedLostSessionRestartNeedsNoTicketAndNeverResubmits()
    {
        await Register();
        _record.SubmissionStarted = true; _store.Save(_record);
        _gateway.Accepted = true; // Server accepted; response and all login credentials are gone.
        var restarted = _store.Load();
        var client = Client(); // New process-equivalent client, same protected key store and metadata only.
        Assert.That((await client.ReadAsync(restarted, CancellationToken.None)).State, Is.EqualTo(DeletionState.Accepted));
        int applied = 0;
        await client.FinishAsync(restarted, () => applied++, CancellationToken.None);
        Assert.That(applied, Is.EqualTo(1));
        Assert.That(_gateway.Reads, Is.EqualTo(1)); Assert.That(_gateway.Registers, Is.EqualTo(1));
        Assert.That(_store.Load(), Is.Null); Assert.That(_keys.Keys, Is.Empty);
    }
    [Test] public async Task Revival_DeletionReceiptLostRegistrationReusesProtectedKeyAndDurableMetadata()
    {
        _gateway.LoseRegistration = true;
        Assert.ThrowsAsync<IOException>(async () => await Register());
        _record = _store.Load(); _gateway.Record = _record;
        Assert.That(_record.KeyCreated, Is.True); Assert.That(_record.ReceiptRegistered, Is.False);
        await Register();
        Assert.That(_keys.Keys.Count, Is.EqualTo(1)); Assert.That(_gateway.Registers, Is.EqualTo(2));
        Assert.That(_store.Load().ReceiptRegistered, Is.True);
        string capability = _keys.Sign(_record.KeyAlias, DeletionReceiptClient.AccessMessage(_record));
        StringAssert.DoesNotContain(capability, File.ReadAllText(Path.Combine(_directory, "pending.json")));
    }
    [Test] public async Task Revival_DeletionReceiptAckThenLocalFailureRestartsFromSealedTerminalWithoutCleanupTwice()
    {
        await Register(); _gateway.Accepted = true;
        await Client().ReadAsync(_record, CancellationToken.None);
        string otherAlias = "tamer.deletion.receipt." + new string('b', 32); _keys.Create(otherAlias);
        int applied = 0;
        FileStream deletionLock = null;
        _gateway.AfterAck = () => deletionLock = new FileStream(Path.Combine(_directory, "pending.json"),
            FileMode.Open, FileAccess.Read, FileShare.Read); // Real Windows sharing denial, after the terminal save.
        try
        {
            Assert.ThrowsAsync<IOException>(async () => await Client().FinishAsync(_record, () => applied++, CancellationToken.None));
            Assert.That(_keys.Keys.ContainsKey(_record.KeyAlias), Is.True, "Failed journal removal must preserve its verification key");
            Assert.That(_store.Load().CleanupApplied, Is.True);
        }
        finally { deletionLock?.Dispose(); _gateway.AfterAck = null; }
        Assert.That(_gateway.Acked, Is.True);
        var restarted = _store.Load();
        Assert.That((await Client().ReadAsync(restarted, CancellationToken.None)).State, Is.EqualTo(DeletionState.Accepted));
        await Client().FinishAsync(restarted, () => applied++, CancellationToken.None);
        Assert.That(applied, Is.EqualTo(1)); Assert.That(_gateway.Reads, Is.EqualTo(1)); Assert.That(_gateway.Acks, Is.EqualTo(2));
        Assert.That(_keys.Keys.ContainsKey(otherAlias), Is.True); Assert.That(_keys.Keys.Count, Is.EqualTo(1));
        Assert.That(_store.Load(), Is.Null);
        Assert.DoesNotThrow(() => _store.Clear(), "An already absent journal remains idempotent");
    }
    [Test] public async Task Revival_DeletionReceiptMissingKeyOrWrongOwnerNeverBecomesAccepted()
    {
        await Register(); _gateway.Accepted = true; _gateway.Mismatch = true;
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Client().ReadAsync(_record, CancellationToken.None));
        Assert.That(_record.TerminalState, Is.Null);
        _gateway.Mismatch = false; _keys.Delete(_record.KeyAlias);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Client().ReadAsync(_record, CancellationToken.None));
        Assert.That(_gateway.Reads, Is.EqualTo(1)); Assert.That(_gateway.Acks, Is.Zero); Assert.That(_keys.Keys, Is.Empty);
    }
    [Test] public void Revival_DeletionReceiptRegistrationNotDurableCannotBeUsedForSubmission()
    {
        _store.FailRegisteredSave = true;
        Assert.ThrowsAsync<IOException>(async () => await Register());
        Assert.That(_store.Load().ReceiptRegistered, Is.False);
        Assert.That(_gateway.Reads, Is.Zero); Assert.That(_gateway.Acks, Is.Zero);
    }
}
