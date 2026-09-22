using System;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using NUnit.Framework;

public class RevivalCloudScriptDeletionTests
{
    private sealed class Journal : IDeletionRecoveryStore
    {
        public DeletionRecovery Record;
        public bool FailSave;
        public DeletionRecovery Load() => Record;
        public void Save(DeletionRecovery value) { if (FailSave) throw new Exception(); value.Validate(); Record = value; }
        public void Clear() { Record = null; }
    }
    private DeletionSession current;
    private Journal journal;
    private int calls, cleanup, fence;
    [SetUp] public void Setup()
    {
        current = new DeletionSession(new object(), "synthetic-account", "session-a", "TEST1", "entity-a");
        journal = new Journal(); calls = cleanup = fence = 0;
    }
    private DeletionFlow Flow(Func<Task<bool>> submit)
        => new DeletionFlow(new CloudScriptDeletionGateway((s, id, token) => { calls++; return submit(); }, true),
            () => current, s => fence++, s => cleanup++, recovery: journal, binding: new string('a', 64));

    [Test] public async Task Revival_CloudScriptDeletionAcceptedOnlyAfterExplicitConfirm()
    {
        using (var flow = Flow(() => Task.FromResult(true)))
        {
            Assert.True(await flow.RequestAsync()); Assert.AreEqual(0, calls);
            Assert.True(await flow.ConfirmAsync());
            Assert.AreEqual(DeletionState.Accepted, flow.State);
            Assert.AreEqual(1, cleanup); Assert.AreEqual(1, fence); Assert.IsNull(journal.Record);
            Assert.False(await flow.ConfirmAsync()); Assert.AreEqual(1, calls);
        }
    }
    [TestCase(false)] [TestCase(true)]
    public async Task Revival_CloudScriptDeletionUnknownNeverResendsAfterReopen(bool throws)
    {
        using (var flow = Flow(() => throws ? Task.FromException<bool>(new TimeoutException()) : Task.FromResult(false)))
        {
            await flow.RequestAsync(); await flow.ConfirmAsync();
            Assert.AreEqual(DeletionState.SubmissionUnknown, flow.State);
            Assert.AreEqual(0, cleanup); Assert.True(journal.Record.SubmissionStarted);
            Assert.False(await flow.ConfirmAsync()); Assert.False(await flow.RefreshAsync());
            Assert.False(await flow.ReauthenticateAsync()); Assert.False(await flow.CancelAsync());
        }
        using (var reopened = Flow(() => Task.FromResult(true)))
        {
            Assert.AreEqual(DeletionState.SubmissionUnknown, reopened.State);
            Assert.False(await reopened.RequestAsync()); Assert.False(await reopened.ReauthenticateAsync());
            Assert.False(await reopened.ConfirmAsync()); Assert.AreEqual(1, calls);
        }
    }
    [Test] public async Task Revival_CloudScriptDeletionJournalFailurePreventsSubmission()
    {
        using (var flow = Flow(() => Task.FromResult(true)))
        {
            await flow.RequestAsync(); journal.FailSave = true;
            Assert.False(await flow.ConfirmAsync()); Assert.AreEqual(0, calls); Assert.AreEqual(0, fence);
        }
    }
    [Test] public async Task Revival_CloudScriptDeletionLateAcceptedCannotCleanAnotherSession()
    {
        var completion = new TaskCompletionSource<bool>();
        using (var flow = Flow(() => completion.Task))
        {
            await flow.RequestAsync(); var pending = flow.ConfirmAsync();
            current = new DeletionSession(new object(), "other-account", "session-b", "TEST1", "entity-b");
            completion.SetResult(true); Assert.False(await pending);
            Assert.AreEqual(0, cleanup); Assert.True(journal.Record.SubmissionStarted);
            Assert.False(await flow.ConfirmAsync()); Assert.AreEqual(1, calls);
        }
    }
    [Test] public async Task Revival_CloudScriptDeletionCancelPreviewDoesNotContactServer()
    {
        using (var flow = Flow(() => Task.FromResult(true)))
        {
            await flow.RequestAsync(); Assert.True(await flow.CancelAsync());
            Assert.AreEqual(0, calls); Assert.AreEqual(0, cleanup); Assert.IsNull(journal.Record);
        }
    }
}
