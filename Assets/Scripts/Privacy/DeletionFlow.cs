using System;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Privacy
{
    public enum DeletionState { Unavailable, Idle, Authenticating, AwaitingConfirmation, Queued, Processing, Completed, Cancelled, RetryableFailure, SessionChanged }

    public sealed class DeletionSession
    {
        public object Owner { get; }
        public string AccountId { get; }
        public string SessionKey { get; }
        public DeletionSession(object owner, string accountId, string sessionKey)
        { Owner = owner; AccountId = accountId; SessionKey = sessionKey; }
        public bool IsValid => Owner != null && !string.IsNullOrEmpty(AccountId) && !string.IsNullOrEmpty(SessionKey);
        public bool Matches(DeletionSession other) => other != null && IsValid && other.IsValid &&
            ReferenceEquals(Owner, other.Owner) && AccountId == other.AccountId && SessionKey == other.SessionKey;
    }

    public sealed class DeletionAuthorization
    {
        public string AccountId { get; }
        public string Proof { get; }
        public DeletionAuthorization(string accountId, string proof) { AccountId = accountId; Proof = proof; }
    }

    public sealed class DeletionSnapshot
    {
        public string RequestId { get; }
        public string PolicyRevision { get; }
        public string Scope { get; }
        public string Challenge { get; }
        public string CompletionEvidence { get; }
        public DeletionState State { get; }
        public DeletionSnapshot(string requestId, string revision, string scope, DeletionState state, string challenge = null, string completionEvidence = null)
        { RequestId = requestId; PolicyRevision = revision; Scope = scope; State = state; Challenge = challenge; CompletionEvidence = completionEvidence; }
    }

    public interface IDeletionGateway
    {
        bool IsAvailable { get; }
        bool IsSynthetic { get; }
        Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession session, CancellationToken token);
        Task<DeletionSnapshot> RequestAsync(DeletionAuthorization authorization, string clientKey, CancellationToken token);
        Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization authorization, DeletionSnapshot request, CancellationToken token);
        Task<DeletionSnapshot> StatusAsync(DeletionAuthorization authorization, string requestId, CancellationToken token);
        Task<DeletionSnapshot> CancelAsync(DeletionAuthorization authorization, string requestId, CancellationToken token);
    }

    /// <summary>No default endpoint, account deletion, local wipe or entitlement changes.</summary>
    public sealed class DeletionFlow : IDisposable
    {
        private readonly IDeletionGateway _gateway;
        private readonly Func<DeletionSession> _current;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly string _clientKey = Guid.NewGuid().ToString("N");
        private DeletionSession _session;
        private DeletionAuthorization _authorization;
        private bool _disposed;
        public DeletionState State { get; private set; }
        public DeletionSnapshot Request { get; private set; }
        public bool IsBusy { get; private set; }
        public bool IsSynthetic => _gateway.IsSynthetic;
        public bool IsAvailable => !_disposed && _gateway.IsAvailable;
        public event Action Changed;

        public DeletionFlow(IDeletionGateway gateway, Func<DeletionSession> currentSession)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _current = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
            State = gateway.IsAvailable ? DeletionState.Idle : DeletionState.Unavailable;
        }

        private void Notify() => Changed?.Invoke();
        private bool Current()
        {
            try { return !_disposed && !_lifetime.IsCancellationRequested && _session != null && _session.Matches(_current()); }
            catch (Exception) { return false; }
        }

        public Task<bool> ReauthenticateAsync() => Request == null ? RequestAsync() : Run(async token =>
        {
            var authorization = await _gateway.ReauthenticateAsync(_session, token);
            if (!Current() || authorization == null || authorization.AccountId != _session.AccountId ||
                string.IsNullOrEmpty(authorization.Proof)) throw new InvalidOperationException();
            _authorization = authorization;
            return Request.State == DeletionState.AwaitingConfirmation
                ? await _gateway.RequestAsync(authorization, _clientKey, token)
                : await _gateway.StatusAsync(authorization, Request.RequestId, token);
        });

        public Task<bool> RequestAsync() => Run(async token =>
        {
            // Reauthentication is an explicit adapter boundary, not reuse of a cached ID.
            var authorization = await _gateway.ReauthenticateAsync(_session, token);
            if (!Current() || authorization == null || authorization.AccountId != _session.AccountId ||
                string.IsNullOrEmpty(authorization.Proof)) throw new InvalidOperationException();
            _authorization = authorization;
            return await _gateway.RequestAsync(authorization, _clientKey, token);
        }, true);

        public Task<bool> ConfirmAsync() => Request == null || Request.State != DeletionState.AwaitingConfirmation
            ? Task.FromResult(false) : Run(token => _gateway.ConfirmAsync(_authorization, Request, token));
        public Task<bool> RefreshAsync() => Request == null ? Task.FromResult(false)
            : Run(token => _gateway.StatusAsync(_authorization, Request.RequestId, token));
        public Task<bool> CancelAsync() => Request == null ||
            (Request.State != DeletionState.AwaitingConfirmation && Request.State != DeletionState.Queued)
            ? Task.FromResult(false) : Run(token => _gateway.CancelAsync(_authorization, Request.RequestId, token));

        private async Task<bool> Run(Func<CancellationToken, Task<DeletionSnapshot>> action, bool authenticate = false)
        {
            if (!IsAvailable || IsBusy || State == DeletionState.Completed || State == DeletionState.Cancelled || State == DeletionState.SessionChanged)
                return false;
            if (authenticate && Request != null) return false;
            IsBusy = true;
            try
            {
                if (_session == null) _session = _current();
                if (!Current()) { State = DeletionState.SessionChanged; return false; }
                if (authenticate) State = DeletionState.Authenticating;
                Notify();
                var result = await action(_lifetime.Token);
                if (!Current()) { if (!_disposed) State = DeletionState.SessionChanged; return false; }
                if (result != null && result.State == DeletionState.AwaitingConfirmation && string.IsNullOrEmpty(result.Challenge) && Request != null)
                    result = new DeletionSnapshot(result.RequestId, result.PolicyRevision, result.Scope, result.State, Request.Challenge);
                if (result == null || string.IsNullOrEmpty(result.RequestId) || string.IsNullOrEmpty(result.PolicyRevision) ||
                    (result.Scope != "title" && result.Scope != "master") ||
                    (Request != null && (result.RequestId != Request.RequestId || result.PolicyRevision != Request.PolicyRevision || result.Scope != Request.Scope)) ||
                    (result.State != DeletionState.AwaitingConfirmation && result.State != DeletionState.Queued &&
                     result.State != DeletionState.Processing && result.State != DeletionState.Completed && result.State != DeletionState.Cancelled) ||
                    (result.State == DeletionState.AwaitingConfirmation && string.IsNullOrEmpty(result.Challenge)) ||
                    (result.State == DeletionState.Completed && string.IsNullOrEmpty(result.CompletionEvidence)))
                    throw new InvalidOperationException();
                Request = result;
                State = result.State;
                return true;
            }
            catch (Exception)
            {
                if (!_disposed) State = Current() ? DeletionState.RetryableFailure : DeletionState.SessionChanged;
                return false;
            }
            finally { IsBusy = false; if (!_disposed) Notify(); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
            _authorization = null;
            Changed = null;
        }
    }

    public sealed class UnavailableDeletionGateway : IDeletionGateway
    {
        public bool IsAvailable => false;
        public bool IsSynthetic => false;
        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("Deletion service is unavailable."));
        public Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession session, CancellationToken token) => Unavailable<DeletionAuthorization>();
        public Task<DeletionSnapshot> RequestAsync(DeletionAuthorization auth, string key, CancellationToken token) => Unavailable<DeletionSnapshot>();
        public Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization auth, DeletionSnapshot request, CancellationToken token) => Unavailable<DeletionSnapshot>();
        public Task<DeletionSnapshot> StatusAsync(DeletionAuthorization auth, string id, CancellationToken token) => Unavailable<DeletionSnapshot>();
        public Task<DeletionSnapshot> CancelAsync(DeletionAuthorization auth, string id, CancellationToken token) => Unavailable<DeletionSnapshot>();
    }
}
