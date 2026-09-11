using System;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using NUnit.Framework;

public class RevivalDeletionFlowTests
{
    private DeletionSession _current;
    private SyntheticDeletionGateway _gateway;
    private DeletionFlow _flow;
    [SetUp] public void SetUp()
    {
        _current = new DeletionSession(new object(), "synthetic-test", "session-a");
        _gateway = new SyntheticDeletionGateway();
        _flow = new DeletionFlow(_gateway, () => _current);
    }
    [TearDown] public void TearDown() => _flow.Dispose();

    [Test] public async Task Revival_DeletionSyntheticEndToEndRequiresCompletionEvidence()
    {
        Assert.That(await _flow.RequestAsync(), Is.True);
        Assert.That(_flow.State, Is.EqualTo(DeletionState.AwaitingConfirmation));
        Assert.That(await _flow.ConfirmAsync(), Is.True);
        Assert.That(_flow.State, Is.EqualTo(DeletionState.Queued));
        _gateway.Advance(false);
        await _flow.RefreshAsync();
        Assert.That(_flow.State, Is.EqualTo(DeletionState.Processing));
        _gateway.Advance(true);
        await _flow.RefreshAsync();
        Assert.That(_flow.State, Is.EqualTo(DeletionState.Completed));
        Assert.That(_flow.Request.CompletionEvidence, Is.Not.Empty);
        Assert.That(_flow.IsSynthetic, Is.True);
    }
    [Test] public async Task Revival_DeletionDefaultGatewayNeverAuthenticatesOrCompletes()
    {
        using (var flow = new DeletionFlow(new UnavailableDeletionGateway(), () => throw new Exception()))
        {
            Assert.That(await flow.RequestAsync(), Is.False);
            Assert.That(flow.State, Is.EqualTo(DeletionState.Unavailable));
        }
    }
    [Test] public async Task Revival_DeletionRetryKeepsOneRequest()
    {
        _gateway.FailNext = true;
        Assert.That(await _flow.RequestAsync(), Is.False);
        Assert.That(_flow.State, Is.EqualTo(DeletionState.RetryableFailure));
        Assert.That(await _flow.RequestAsync(), Is.True);
        _gateway.FailNext = true;
        Assert.That(await _flow.ConfirmAsync(), Is.False);
        Assert.That(await _flow.ConfirmAsync(), Is.True);
        Assert.That(_gateway.RequestCount, Is.EqualTo(1));
    }
    [Test] public async Task Revival_DeletionCancellationRetryKeepsCancellationIntent()
    {
        await _flow.RequestAsync();
        _gateway.FailNext = true;
        Assert.That(await _flow.CancelAsync(), Is.False);
        Assert.That(await _flow.CancelAsync(), Is.True);
        Assert.That(_flow.State, Is.EqualTo(DeletionState.Cancelled));
        Assert.That(await _flow.ConfirmAsync(), Is.False);
    }
    [Test] public async Task Revival_DeletionProcessingCannotBeCancelled()
    {
        await _flow.RequestAsync(); await _flow.ConfirmAsync();
        _gateway.Advance(false); await _flow.RefreshAsync();
        Assert.That(await _flow.CancelAsync(), Is.False);
        Assert.That(_flow.State, Is.EqualTo(DeletionState.Processing));
    }
    [Test] public async Task Revival_DeletionReauthenticationPreservesRequestIdentity()
    {
        await _flow.RequestAsync(); var id = _flow.Request.RequestId;
        Assert.That(await _flow.ReauthenticateAsync(), Is.True);
        Assert.That(_flow.Request.RequestId, Is.EqualTo(id));
        Assert.That(_gateway.RequestCount, Is.EqualTo(1));
    }
    [Test] public async Task Revival_DeletionSyntheticGatewayRejectsRealAccount()
    {
        _current = new DeletionSession(new object(), "real-account", "session");
        Assert.That(await _flow.RequestAsync(), Is.False);
        Assert.That(_flow.Request, Is.Null);
    }
    [Test] public async Task Revival_DeletionSessionReplacementStopsConfirmation()
    {
        await _flow.RequestAsync();
        _current = new DeletionSession(new object(), "synthetic-test", "session-a");
        Assert.That(await _flow.ConfirmAsync(), Is.False);
        Assert.That(_flow.State, Is.EqualTo(DeletionState.SessionChanged));
    }

    private sealed class DelayedGateway : IDeletionGateway
    {
        public bool IsAvailable => true;
        public bool IsSynthetic => true;
        public readonly TaskCompletionSource<DeletionSnapshot> Pending = new TaskCompletionSource<DeletionSnapshot>();
        public int Calls;
        public Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession s, CancellationToken t) => Task.FromResult(new DeletionAuthorization(s.AccountId, "fresh"));
        public Task<DeletionSnapshot> RequestAsync(DeletionAuthorization a, string k, CancellationToken t) { Calls++; return Pending.Task; }
        public Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization a, DeletionSnapshot r, CancellationToken t) => Pending.Task;
        public Task<DeletionSnapshot> StatusAsync(DeletionAuthorization a, string r, CancellationToken t) => Pending.Task;
        public Task<DeletionSnapshot> CancelAsync(DeletionAuthorization a, string r, CancellationToken t) => Pending.Task;
    }
    [Test] public async Task Revival_DeletionDuplicateClicksDoNotStartAnotherRequest()
    {
        var gateway = new DelayedGateway();
        using (var flow = new DeletionFlow(gateway, () => _current))
        {
            var pending = flow.RequestAsync();
            Assert.That(await flow.RequestAsync(), Is.False);
            gateway.Pending.SetResult(new DeletionSnapshot("id", "v1", "title", DeletionState.AwaitingConfirmation, "challenge"));
            Assert.That(await pending, Is.True);
            Assert.That(gateway.Calls, Is.EqualTo(1));
        }
    }
    [TestCase(false)] [TestCase(true)] public async Task Revival_DeletionLateCompletionCannotChangeDisposedOrReplacedOwner(bool dispose)
    {
        var gateway = new DelayedGateway();
        using (var flow = new DeletionFlow(gateway, () => _current))
        {
            var pending = flow.RequestAsync();
            if (dispose) flow.Dispose();
            else _current = new DeletionSession(new object(), "synthetic-other", "session-b");
            gateway.Pending.SetResult(new DeletionSnapshot("id", "v1", "title", DeletionState.Completed, completionEvidence: "evidence"));
            Assert.That(await pending, Is.False);
            Assert.That(flow.State, Is.Not.EqualTo(DeletionState.Completed));
        }
    }
    [Test] public async Task Revival_DeletionCompletedWithoutEvidenceIsRejected()
    {
        var gateway = new DelayedGateway();
        using (var flow = new DeletionFlow(gateway, () => _current))
        {
            var pending = flow.RequestAsync();
            gateway.Pending.SetResult(new DeletionSnapshot("id", "v1", "title", DeletionState.Completed));
            Assert.That(await pending, Is.False);
            Assert.That(flow.State, Is.EqualTo(DeletionState.RetryableFailure));
        }
    }
}
