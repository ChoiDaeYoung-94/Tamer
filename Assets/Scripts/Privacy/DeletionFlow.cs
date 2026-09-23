using System;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Privacy
{
    public enum DeletionState { Unavailable, Idle, Authenticating, AwaitingConfirmation, Queued, Processing, Completed, Cancelled, RetryableFailure, SessionChanged, Accepted, SubmissionUnknown, AwaitingSessionConfirmation, RecoveryRequired, UnsupportedAccount, RecoveryUnavailable }

    public sealed class DeletionSession
    {
        public object Owner { get; }
        public string AccountId { get; }
        public string SessionKey { get; }
        public string TitleId { get; }
        public string EntityId { get; }
        public string InventoryOwnerKey { get; }
        public string InventorySession { get; }
        public DeletionSession(object owner, string accountId, string sessionKey, string titleId = null, string entityId = null)
            : this(owner, accountId, sessionKey, titleId, entityId, null, null) { }
        public DeletionSession(object owner, string accountId, string sessionKey, string titleId, string entityId,
            string inventoryOwnerKey, string inventorySession)
        { Owner = owner; AccountId = accountId; SessionKey = sessionKey; TitleId = titleId; EntityId = entityId;
            InventoryOwnerKey = inventoryOwnerKey; InventorySession = inventorySession; }
        public DeletionSession ForCleanup(DeletionRecovery record) => new DeletionSession(Owner, AccountId, SessionKey,
            TitleId, EntityId, record.InventoryOwnerKey, record.InventorySession);
        public bool IsValid => Owner != null && !string.IsNullOrEmpty(AccountId) && !string.IsNullOrEmpty(SessionKey);
        public bool HasEntityBinding => IsValid && !string.IsNullOrEmpty(TitleId) && !string.IsNullOrEmpty(EntityId);
        public bool Matches(DeletionSession other) => other != null && IsValid && other.IsValid &&
            ReferenceEquals(Owner, other.Owner) && AccountId == other.AccountId && SessionKey == other.SessionKey &&
            TitleId == other.TitleId && EntityId == other.EntityId;
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
        private string _clientKey = Guid.NewGuid().ToString("N");
        private readonly IDeletionRecoveryStore _recovery;
        private readonly string _binding;
        private readonly DeletionReceiptClient _receipts;
        private readonly string _origin;
        private DeletionRecovery _record;
        private DeletionSessionChallenge _sessionChallenge;
        private bool _submissionStarted;
        private bool _recoveryBlocked;
        private ISessionConfirmationGateway SessionGateway => _gateway as ISessionConfirmationGateway;
        public bool UsesSessionConfirmation => SessionGateway?.UsesSessionConfirmation == true;
        public bool UsesCloudScript => _gateway is CloudScriptDeletionGateway;
        public bool CloudScriptSubmissionStarted => UsesCloudScript && _submissionStarted;
        public bool NeedsAuthorization => _authorization == null;
        public bool CanRecoverReceipt => _record?.ReceiptRegistered == true && _receipts != null;
        public bool CanConfirmDeletion => Request?.State == DeletionState.AwaitingConfirmation &&
            !string.IsNullOrEmpty(Request.Challenge) && !_submissionStarted &&
            (!UsesSessionConfirmation && _receipts == null || _authorization != null);
        private DeletionSession _session;
        private DeletionAuthorization _authorization;
        private bool _disposed;
        private readonly Action<DeletionSession> _beforeSubmit;
        private readonly Action<DeletionSession> _accepted;
        private readonly Action<DeletionSession> _cancelled;
        public DeletionState State { get; private set; }
        public DeletionSnapshot Request { get; private set; }
        public bool IsBusy { get; private set; }
        public bool AcceptedCleanupFailed { get; private set; }
        public bool IsSynthetic => _gateway.IsSynthetic;
        public bool IsAvailable => !_disposed && _gateway.IsAvailable;
        public event Action Changed;

        public DeletionFlow(IDeletionGateway gateway, Func<DeletionSession> currentSession,
            Action<DeletionSession> beforeSubmit = null, Action<DeletionSession> accepted = null,
            Action<DeletionSession> cancelled = null, IDeletionRecoveryStore recovery = null, string binding = null,
            DeletionReceiptClient receipts = null, string origin = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _current = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
            _beforeSubmit = beforeSubmit;
            _accepted = accepted;
            _cancelled = cancelled;
            _recovery = recovery;
            _binding = binding;
            _receipts = receipts;
            _origin = origin;
            if ((UsesSessionConfirmation || UsesCloudScript) && (recovery == null || string.IsNullOrEmpty(binding)))
                throw new ArgumentException("Session confirmation requires durable account-bound recovery.");
            State = gateway.IsAvailable ? DeletionState.Idle : DeletionState.Unavailable;
            if (recovery != null)
            {
                try
                {
                    _session = _current(); // Bind the journal to this live owner before any delayed user action.
                    var record = recovery.Load();
                    _record = record;
                    if (record != null)
                    {
                        record.Validate();
                        if (record.Binding != binding) throw new InvalidOperationException();
                        _clientKey = record.ClientKey;
                        _submissionStarted = record.SubmissionStarted;
                        if (record.RequestId != null)
                            Request = new DeletionSnapshot(record.RequestId, record.Revision, "title",
                                _submissionStarted ? DeletionState.SubmissionUnknown : DeletionState.AwaitingConfirmation);
                        State = UsesCloudScript && _submissionStarted ? DeletionState.SubmissionUnknown : DeletionState.RecoveryRequired;
                    }
                }
                catch (Exception) { _recoveryBlocked = true; State = DeletionState.Unavailable; }
            }
        }

        private void Notify() => Changed?.Invoke();
        private bool Current()
        {
            try { return !_disposed && !_lifetime.IsCancellationRequested && _session != null && _session.Matches(_current()); }
            catch (Exception) { return false; }
        }

        public Task<bool> ReauthenticateAsync() => UsesSessionConfirmation ? BeginSessionAsync() : Request == null ? RequestAsync() : Run(async token =>
        {
            var authorization = await _gateway.ReauthenticateAsync(_session, token);
            if (!Current() || authorization == null || authorization.AccountId != _session.AccountId ||
                string.IsNullOrEmpty(authorization.Proof)) throw new InvalidOperationException();
            _authorization = authorization;
            return !_submissionStarted && Request.State == DeletionState.AwaitingConfirmation
                ? await _gateway.RequestAsync(authorization, _clientKey, token)
                : await _gateway.StatusAsync(authorization, Request.RequestId, token);
        });

        public Task<bool> RequestAsync() => UsesSessionConfirmation ? BeginSessionAsync() : Run(async token =>
        {
            Persist(); // Retain the same intent if provider authentication or the request response is lost.
            // Reauthentication is an explicit adapter boundary, not reuse of a cached ID.
            var authorization = await _gateway.ReauthenticateAsync(_session, token);
            if (!Current() || authorization == null || authorization.AccountId != _session.AccountId ||
                string.IsNullOrEmpty(authorization.Proof)) throw new InvalidOperationException();
            _authorization = authorization;
            return await _gateway.RequestAsync(authorization, _clientKey, token);
        }, true);

        private async Task<bool> BeginSessionAsync()
        {
            if (!CanRun()) return false;
            IsBusy = true;
            try
            {
                if (_session == null) _session = _current();
                if (!Current() || !_session.HasEntityBinding) { State = DeletionState.SessionChanged; return false; }
                _authorization = null;
                _sessionChallenge = null;
                Persist(); // Preserve the intent key even if the first HTTP response is lost.
                State = DeletionState.Authenticating;
                Notify();
                var challenge = await SessionGateway.BeginSessionAsync(_session, _clientKey, _lifetime.Token);
                if (!Current()) { if (!_disposed) State = DeletionState.SessionChanged; return false; }
                _sessionChallenge = challenge ?? throw new InvalidOperationException();
                State = DeletionState.AwaitingSessionConfirmation;
                return true;
            }
            catch (UnsupportedDeletionAccountException) { if (!_disposed) State = Current() ? DeletionState.UnsupportedAccount : DeletionState.SessionChanged; return false; }
            catch (Exception) { if (!_disposed) State = Current() ? DeletionState.RetryableFailure : DeletionState.SessionChanged; return false; }
            finally { IsBusy = false; if (!_disposed) Notify(); }
        }

        // Called only by the explicit confirmation button, never by a status/retry operation.
        public Task<bool> ConfirmSessionAsync() => State != DeletionState.AwaitingSessionConfirmation || _sessionChallenge == null
            ? Task.FromResult(false) : Run(async token =>
            {
                var challenge = _sessionChallenge;
                _sessionChallenge = null; // A consumed or uncertain nonce must never be reused.
                var authorization = await SessionGateway.ConfirmSessionAsync(_session, challenge, token);
                if (!Current() || authorization == null || authorization.AccountId != _session.AccountId || string.IsNullOrEmpty(authorization.Proof))
                    throw new InvalidOperationException();
                _authorization = authorization;
                // Once submission has started, only status may follow renewed confirmation.
                return _submissionStarted
                    ? await _gateway.StatusAsync(authorization, Request.RequestId, token)
                    : await _gateway.RequestAsync(authorization, _clientKey, token);
            });

        public Task<bool> ConfirmAsync() => !CanConfirmDeletion
            ? Task.FromResult(false) : Run(async token =>
            {
                if (UsesSessionConfirmation || UsesCloudScript || _receipts != null)
                {
                    if (_receipts != null) await _receipts.RegisterAsync(_authorization, _record, token);
                    else if (!IsSynthetic && !UsesCloudScript) throw new InvalidOperationException("Protected receipt recovery is required.");
                    if (!Current()) throw new InvalidOperationException();
                    _submissionStarted = true;
                    try { Persist(); } // A failed journal write must prevent the external submit.
                    catch { _submissionStarted = false; throw; }
                }
                _beforeSubmit?.Invoke(_session);
                return await _gateway.ConfirmAsync(_authorization, Request, token);
            });
        public Task<bool> RefreshAsync() => CanRecoverReceipt ? RecoverReceiptAsync() : Request == null ? Task.FromResult(false)
            : Run(token => _gateway.StatusAsync(_authorization, Request.RequestId, token));
        public Task<bool> CancelAsync() => Request == null ||
            (UsesCloudScript && _submissionStarted) ||
            (Request.State != DeletionState.AwaitingConfirmation && Request.State != DeletionState.Queued)
            ? Task.FromResult(false) : Run(token => _gateway.CancelAsync(_authorization, Request.RequestId, token));

        private bool CanRun() => IsAvailable && !_recoveryBlocked && !IsBusy && State != DeletionState.Accepted &&
            !(UsesCloudScript && _submissionStarted) &&
            State != DeletionState.Completed && State != DeletionState.Cancelled && State != DeletionState.SessionChanged && State != DeletionState.UnsupportedAccount;

        private void Persist()
        {
            if (_recovery == null) return;
            if (_record == null) _record = new DeletionRecovery { Binding = _binding, ClientKey = _clientKey,
                Origin = _origin, Title = _session?.TitleId,
                InventoryOwnerKey = _session?.InventoryOwnerKey, InventorySession = _session?.InventorySession,
                OwnerHash = _origin == null ? null : DeletionRecovery.Hash(_origin, _session.TitleId, _session.AccountId) };
            _record.RequestId = Request?.RequestId;
            _record.Revision = Request?.PolicyRevision;
            _record.SubmissionStarted = _submissionStarted;
            _recovery.Save(_record);
        }

        private async Task<bool> RecoverReceiptAsync()
        {
            if (!CanRun()) return false;
            IsBusy = true;
            try
            {
                if (!Current()) { State = DeletionState.SessionChanged; return false; }
                var snapshot = await _receipts.ReadAsync(_record, _lifetime.Token);
                if (!Current()) { if (!_disposed) State = DeletionState.SessionChanged; return false; }
                Request = snapshot;
                State = snapshot.State;
                if (State == DeletionState.Accepted || State == DeletionState.Cancelled)
                {
                    try { await _receipts.FinishAsync(_record, () =>
                    {
                        if (!Current()) throw new InvalidOperationException();
                        if (snapshot.State == DeletionState.Accepted) _accepted?.Invoke(_session.ForCleanup(_record));
                        else _cancelled?.Invoke(_session);
                    }, _lifetime.Token); }
                    catch (Exception) { AcceptedCleanupFailed = true; }
                }
                return true;
            }
            catch (Exception) { if (!_disposed) State = DeletionState.RecoveryUnavailable; return false; }
            finally { IsBusy = false; if (!_disposed) Notify(); }
        }

        private async Task<bool> Run(Func<CancellationToken, Task<DeletionSnapshot>> action, bool authenticate = false)
        {
            if (!CanRun()) return false;
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
                     result.State != DeletionState.Processing && result.State != DeletionState.Completed && result.State != DeletionState.Cancelled &&
                     result.State != DeletionState.Accepted && result.State != DeletionState.SubmissionUnknown) ||
                    (result.State == DeletionState.AwaitingConfirmation && string.IsNullOrEmpty(result.Challenge) && !_submissionStarted) ||
                    (result.State == DeletionState.Accepted && result.Scope != "title") ||
                    (result.State == DeletionState.Completed && string.IsNullOrEmpty(result.CompletionEvidence)))
                    throw new InvalidOperationException();
                Request = result;
                State = result.State;
                if (State == DeletionState.Cancelled)
                {
                    if (CanRecoverReceipt)
                    {
                        _receipts.RememberTerminal(_record, State);
                        await _receipts.FinishAsync(_record, () => _cancelled?.Invoke(_session), _lifetime.Token);
                    }
                    else { _recovery?.Clear(); _cancelled?.Invoke(_session); }
                }
                if (State == DeletionState.Accepted)
                {
                    try
                    {
                        if (CanRecoverReceipt)
                        {
                            _receipts.RememberTerminal(_record, State);
                            await _receipts.FinishAsync(_record, () => _accepted?.Invoke(_session.ForCleanup(_record)), _lifetime.Token);
                        }
                        else { _accepted?.Invoke(_session); _recovery?.Clear(); }
                    }
                    catch (Exception) { AcceptedCleanupFailed = true; } // Never resubmit an accepted deletion to retry a local cleanup.
                }
                else if (State != DeletionState.Cancelled) Persist();
                return true;
            }
            catch (Exception)
            {
                if (!_disposed) State = !Current() ? DeletionState.SessionChanged
                    : !_gateway.IsAvailable && !_submissionStarted ? DeletionState.Unavailable
                    : _submissionStarted ? DeletionState.SubmissionUnknown : DeletionState.RetryableFailure;
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
            _sessionChallenge = null;
            if (_gateway is IDisposable disposable) disposable.Dispose();
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
