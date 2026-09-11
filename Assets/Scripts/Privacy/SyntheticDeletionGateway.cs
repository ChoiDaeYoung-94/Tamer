#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Privacy
{
    /// <summary>Local synthetic state only; no files, SDK calls or real account access.</summary>
    public sealed class SyntheticDeletionGateway : IDeletionGateway
    {
        private DeletionSnapshot _request;
        private string _account;
        private string _key;
        public bool IsAvailable => true;
        public bool IsSynthetic => true;
        public int RequestCount { get; private set; }
        public bool FailNext { get; set; }
        public Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession session, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid || !session.AccountId.StartsWith("synthetic-", StringComparison.Ordinal))
                throw new InvalidOperationException("Synthetic identity required.");
            if (_account != null && _account != session.AccountId) throw new InvalidOperationException();
            _account = session.AccountId;
            return Task.FromResult(new DeletionAuthorization(_account, "synthetic-proof"));
        }
        private void Check(DeletionAuthorization authorization, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (FailNext) { FailNext = false; throw new InvalidOperationException("Synthetic failure."); }
            if (authorization == null || authorization.AccountId != _account || authorization.Proof != "synthetic-proof")
                throw new InvalidOperationException();
        }
        public Task<DeletionSnapshot> RequestAsync(DeletionAuthorization auth, string key, CancellationToken token)
        {
            Check(auth, token);
            if (_key != null && _key != key) throw new InvalidOperationException();
            _key = key;
            if (_request == null) { RequestCount++; _request = new DeletionSnapshot(Guid.NewGuid().ToString("N"), "synthetic-policy-v1", "title", DeletionState.AwaitingConfirmation, "synthetic-challenge"); }
            return Task.FromResult(_request);
        }
        public Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization auth, DeletionSnapshot request, CancellationToken token)
        {
            Check(auth, token);
            if (_request == null || request.RequestId != _request.RequestId || request.Challenge != _request.Challenge)
                throw new InvalidOperationException();
            if (_request.State == DeletionState.AwaitingConfirmation) Set(DeletionState.Queued);
            return Task.FromResult(_request);
        }
        public Task<DeletionSnapshot> StatusAsync(DeletionAuthorization auth, string id, CancellationToken token)
        {
            Check(auth, token);
            if (_request == null || id != _request.RequestId) throw new InvalidOperationException();
            return Task.FromResult(_request);
        }
        public Task<DeletionSnapshot> CancelAsync(DeletionAuthorization auth, string id, CancellationToken token)
        {
            Check(auth, token);
            if (_request == null || id != _request.RequestId ||
                (_request.State != DeletionState.AwaitingConfirmation && _request.State != DeletionState.Queued && _request.State != DeletionState.Cancelled))
                throw new InvalidOperationException();
            Set(DeletionState.Cancelled);
            return Task.FromResult(_request);
        }
        public void Advance(bool complete)
        {
            if (_request == null || (_request.State != DeletionState.Queued && _request.State != DeletionState.Processing))
                throw new InvalidOperationException();
            Set(complete ? DeletionState.Completed : DeletionState.Processing);
        }
        private void Set(DeletionState state) => _request = new DeletionSnapshot(_request.RequestId, _request.PolicyRevision,
            _request.Scope, state, _request.Challenge, state == DeletionState.Completed ? "synthetic-completion-evidence" : null);
    }
}
#endif
