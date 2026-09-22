using System;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Privacy
{
    // The preview is local. Only Confirm invokes the authenticated CloudScript once.
    public sealed class CloudScriptDeletionGateway : IDeletionGateway
    {
        public const string FunctionName = "requestCurrentPlayerDeletionV1";
        public const string Protocol = "tamer-title-deletion-v1";
        private readonly Func<DeletionSession, string, CancellationToken, Task<bool>> _submit;
        private readonly Func<DeletionSession, CancellationToken, Task<bool>> _prepare;
        private DeletionSession _session;
        private DeletionAuthorization _authorization;
        private DeletionSnapshot _request;
        private bool _attempted;
        public bool IsAvailable => true;
        public bool IsSynthetic { get; }

        public CloudScriptDeletionGateway(Func<DeletionSession, string, CancellationToken, Task<bool>> submit,
            bool synthetic = false, Func<DeletionSession, CancellationToken, Task<bool>> prepare = null)
        { _submit = submit ?? throw new ArgumentNullException(nameof(submit)); IsSynthetic = synthetic; _prepare = prepare; }

        // Interface compatibility: this captures the current session, not fresh authentication.
        public async Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession session, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_attempted || session == null || !session.HasEntityBinding) throw new InvalidOperationException();
            _session = session;
            if (_prepare != null && !await _prepare(session, token)) throw new InvalidOperationException("Deletion is unavailable.");
            _authorization = new DeletionAuthorization(session.AccountId, Guid.NewGuid().ToString("N"));
            return _authorization;
        }
        public Task<DeletionSnapshot> RequestAsync(DeletionAuthorization authorization, string clientKey, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_attempted || !ReferenceEquals(authorization, _authorization) || _session == null ||
                !Guid.TryParseExact(clientKey, "N", out _)) throw new InvalidOperationException();
            _request = new DeletionSnapshot(clientKey, Protocol, "title", DeletionState.AwaitingConfirmation, "confirm-current-account");
            return Task.FromResult(_request);
        }
        public async Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization authorization, DeletionSnapshot request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_attempted || !ReferenceEquals(authorization, _authorization) || !ReferenceEquals(request, _request))
                throw new InvalidOperationException();
            _attempted = true;
            bool accepted = await _submit(_session, request.RequestId, token);
            return new DeletionSnapshot(request.RequestId, Protocol, "title",
                accepted ? DeletionState.Accepted : DeletionState.SubmissionUnknown);
        }
        public Task<DeletionSnapshot> StatusAsync(DeletionAuthorization authorization, string requestId, CancellationToken token)
            => Task.FromException<DeletionSnapshot>(new InvalidOperationException("Contact support; no status service is configured."));
        public Task<DeletionSnapshot> CancelAsync(DeletionAuthorization authorization, string requestId, CancellationToken token)
        {
            if (_attempted || !ReferenceEquals(authorization, _authorization) || _request?.RequestId != requestId)
                throw new InvalidOperationException();
            return Task.FromResult(new DeletionSnapshot(requestId, Protocol, "title", DeletionState.Cancelled));
        }
    }
}
