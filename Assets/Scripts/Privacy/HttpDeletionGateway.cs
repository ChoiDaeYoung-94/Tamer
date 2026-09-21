using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Privacy
{
    public sealed class DeletionSessionChallenge
    {
        internal string Nonce { get; }
        internal string Ticket { get; }
        internal DeletionSession Session { get; }
        internal DeletionSessionChallenge(string nonce, string ticket, DeletionSession session)
        { Nonce = nonce; Ticket = ticket; Session = session; }
    }

    public interface ISessionConfirmationGateway
    {
        bool UsesSessionConfirmation { get; }
        Task<DeletionSessionChallenge> BeginSessionAsync(DeletionSession session, string clientKey, CancellationToken token);
        Task<DeletionAuthorization> ConfirmSessionAsync(DeletionSession session, DeletionSessionChallenge challenge, CancellationToken token);
    }

    public sealed class UnsupportedDeletionAccountException : Exception { }

    /// <summary>Explicit HTTPS intake adapter. Requires an independently configured fresh-auth exchange.</summary>
    public sealed class HttpDeletionGateway : IDeletionGateway, ISessionConfirmationGateway, IDeletionReceiptGateway, IDisposable
    {
        private readonly Func<DeletionSession, CancellationToken, Task<DeletionAuthorization>> _reauthenticate;
        private readonly HttpClient _client;
        private Func<DeletionSession, string> _sessionTicket;
        private bool _disposed;
        public bool IsAvailable => !_disposed;
        public bool IsSynthetic => false;
        public bool UsesSessionConfirmation => _sessionTicket != null;

        public static HttpDeletionGateway ForSessionConfirmation(Uri endpoint, Func<DeletionSession, string> sessionTicket)
        {
            if (sessionTicket == null) throw new ArgumentNullException(nameof(sessionTicket));
            return new HttpDeletionGateway(endpoint, (s, t) => Task.FromException<DeletionAuthorization>(new InvalidOperationException()))
                { _sessionTicket = sessionTicket };
        }

        public async Task<DeletionSessionChallenge> BeginSessionAsync(DeletionSession session, string clientKey, CancellationToken token)
        {
            if (!UsesSessionConfirmation || session == null || !session.HasEntityBinding) throw new InvalidOperationException();
            var config = await Send<Config>("v1/deletion/config", null, token);
            if (!config.available || config.synthetic || config.evidenceKind != "session_confirmation" || !config.receiptRecovery)
                throw new InvalidOperationException("Session confirmation is unavailable.");
            var ticket = _sessionTicket(session);
            if (string.IsNullOrEmpty(ticket) || ticket.Length > 4096) throw new InvalidOperationException();
            var result = await Send<SessionChallengeBody>("v1/deletion/session-challenge",
                new SessionBeginBody { sessionTicket = ticket, clientKey = clientKey }, token);
            if (result.evidenceKind != "session_confirmation" || result.purpose != "delete_title_account" ||
                string.IsNullOrEmpty(result.nonce) || result.nonce.Length != 43)
                throw new InvalidOperationException("Invalid session confirmation.");
            return new DeletionSessionChallenge(result.nonce, ticket, session);
        }

        public async Task<DeletionAuthorization> ConfirmSessionAsync(DeletionSession session, DeletionSessionChallenge challenge, CancellationToken token)
        {
            if (!UsesSessionConfirmation || challenge == null || !challenge.Session.Matches(session) ||
                _sessionTicket(session) != challenge.Ticket) throw new InvalidOperationException();
            var result = await Send<SessionAuthorizationBody>("v1/deletion/session-confirm",
                new SessionConfirmBody { sessionTicket = challenge.Ticket, nonce = challenge.Nonce, confirmed = true }, token);
            if (result.evidenceKind != "session_confirmation" || result.accountId != session.AccountId || string.IsNullOrEmpty(result.proof))
                throw new InvalidOperationException("Invalid session confirmation.");
            return new DeletionAuthorization(result.accountId, result.proof);
        }

        public HttpDeletionGateway(Uri endpoint, Func<DeletionSession, CancellationToken, Task<DeletionAuthorization>> reauthenticate, TimeSpan? timeout = null)
        {
            // Pin an explicit HTTPS origin. Never follow redirects or use ambient proxy credentials.
            if (endpoint == null || !endpoint.IsAbsoluteUri || endpoint.Scheme != "https" ||
                string.IsNullOrEmpty(endpoint.Host) || endpoint.UserInfo != "" || endpoint.AbsolutePath != "/" ||
                endpoint.Query != "" || endpoint.Fragment != "")
                throw new ArgumentException("An explicit HTTPS origin is required.", nameof(endpoint));
            _reauthenticate = reauthenticate ?? throw new ArgumentNullException(nameof(reauthenticate));
            var duration = timeout ?? TimeSpan.FromSeconds(5);
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromSeconds(30))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            { BaseAddress = endpoint, Timeout = duration, MaxResponseContentBufferSize = 16384 };
        }

        public async Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession session, CancellationToken token)
        {
            if (session == null || !session.IsValid) throw new InvalidOperationException();
            var config = await Send<Config>("v1/deletion/config", null, token);
            if (!config.available || config.synthetic) throw new InvalidOperationException("Deletion service unavailable.");
            var auth = await _reauthenticate(session, token);
            Validate(auth);
            if (auth.AccountId != session.AccountId) throw new InvalidOperationException();
            return auth;
        }

        private static void Validate(DeletionAuthorization auth)
        {
            if (auth == null || string.IsNullOrEmpty(auth.AccountId) || string.IsNullOrEmpty(auth.Proof))
                throw new InvalidOperationException("Fresh authorization required.");
        }

        public Task<DeletionSnapshot> RequestAsync(DeletionAuthorization auth, string key, CancellationToken token)
        { Validate(auth); return Snapshot("request", new RequestBody { proof = auth.Proof, clientKey = key }, token); }
        public Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization auth, DeletionSnapshot request, CancellationToken token)
        {
            Validate(auth);
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Snapshot("confirm", new ConfirmBody { proof = auth.Proof, requestId = request.RequestId,
                challenge = request.Challenge, policyRevision = request.PolicyRevision }, token);
        }
        public Task<DeletionSnapshot> StatusAsync(DeletionAuthorization auth, string id, CancellationToken token)
        { Validate(auth); return Snapshot("status", new IdBody { proof = auth.Proof, requestId = id }, token); }
        public Task<DeletionSnapshot> CancelAsync(DeletionAuthorization auth, string id, CancellationToken token)
        { Validate(auth); return Snapshot("cancel", new IdBody { proof = auth.Proof, requestId = id }, token); }

        public Task<DeletionReceipt> RegisterReceiptAsync(DeletionAuthorization auth, string id, string verifier, CancellationToken token)
        { Validate(auth); return Send<DeletionReceipt>("v1/deletion/receipt-register", new ReceiptRegisterBody { proof = auth.Proof, requestId = id, verifier = verifier }, token); }
        public Task<DeletionReceipt> ReadReceiptAsync(string id, string capability, CancellationToken token) =>
            Send<DeletionReceipt>("v1/deletion/receipt-status", new ReceiptAccessBody { requestId = id, capability = capability }, token);
        public async Task AcknowledgeReceiptAsync(string id, string capability, CancellationToken token)
        {
            var response = await Send<ReceiptAckBody>("v1/deletion/receipt-ack", new ReceiptAccessBody { requestId = id, capability = capability }, token);
            if (!response.acknowledged) throw new InvalidOperationException();
        }

        private async Task<DeletionSnapshot> Snapshot(string operation, object body, CancellationToken token)
        {
            var result = await Send<SnapshotBody>("v1/deletion/" + operation, body, token);
            DeletionState state;
            switch (result.state)
            {
                case "awaiting_confirmation": state = DeletionState.AwaitingConfirmation; break;
                case "queued": state = DeletionState.Queued; break;
                case "processing": state = DeletionState.Processing; break;
                case "cancelled": state = DeletionState.Cancelled; break;
                default: throw new InvalidOperationException("Invalid deletion state.");
            }
            if (result.submissionState == "accepted" && result.state == "processing") state = DeletionState.Accepted;
            else if (result.submissionState == "submission_unknown" && result.state == "processing") state = DeletionState.SubmissionUnknown;
            else if (result.submissionState != "not_submitted" && result.submissionState != "ready")
                throw new InvalidOperationException("Invalid submission state.");
            if (string.IsNullOrEmpty(result.requestId) || string.IsNullOrEmpty(result.policyRevision) ||
                result.scope != "title" ||
                (state == DeletionState.Completed && string.IsNullOrEmpty(result.completionEvidence)))
                throw new InvalidOperationException("Invalid deletion response.");
            return new DeletionSnapshot(result.requestId, result.policyRevision, result.scope, state,
                result.challenge, result.completionEvidence);
        }

        private async Task<T> Send<T>(string path, object body, CancellationToken token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HttpDeletionGateway));
            using (var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, path))
            {
                if (body != null)
                {
                    using (var stream = new MemoryStream())
                    {
                        new DataContractJsonSerializer(body.GetType()).WriteObject(stream, body);
                        request.Content = new StringContent(Encoding.UTF8.GetString(stream.ToArray()), Encoding.UTF8, "application/json");
                    }
                }
                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token))
                {
                    // Do not expose a response body, server exception or proof through errors.
                    if (!response.IsSuccessStatusCode)
                    {
                        // Only a whitelisted classification is surfaced; never return response bodies or credentials.
                        if (response.Content.Headers.ContentType?.MediaType == "application/json")
                        {
                            ErrorBody error = null;
                            try
                            {
                                using (var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync()))
                                    error = (ErrorBody)new DataContractJsonSerializer(typeof(ErrorBody)).ReadObject(stream);
                            }
                            catch (Exception) { }
                            if (error?.code == "account_type_unsupported") throw new UnsupportedDeletionAccountException();
                        }
                        throw new InvalidOperationException("Deletion HTTP request failed.");
                    }
                    if (response.Content.Headers.ContentType?.MediaType != "application/json")
                        throw new InvalidOperationException("Invalid deletion content type.");
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    token.ThrowIfCancellationRequested();
                    using (var stream = new MemoryStream(bytes))
                        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                }
            }
        }

        public void Dispose() { if (_disposed) return; _disposed = true; _client.Dispose(); }

        [DataContract] private sealed class Config
        { [DataMember(IsRequired = true)] public bool available { get; set; } [DataMember(IsRequired = true)] public bool synthetic { get; set; }
          [DataMember] public string evidenceKind { get; set; } [DataMember] public bool receiptRecovery { get; set; } }
        [DataContract] private sealed class ReceiptRegisterBody
        { [DataMember] public string proof; [DataMember] public string requestId; [DataMember] public string verifier; }
        [DataContract] private sealed class ReceiptAccessBody
        { [DataMember] public string requestId; [DataMember] public string capability; }
        [DataContract] private sealed class ReceiptAckBody { [DataMember(IsRequired = true)] public bool acknowledged; }
        [DataContract] private sealed class ErrorBody { [DataMember] public string code { get; set; } }
        [DataContract] private sealed class SessionBeginBody
        { [DataMember] public string sessionTicket { get; set; } [DataMember] public string clientKey { get; set; } }
        [DataContract] private sealed class SessionConfirmBody
        { [DataMember] public string sessionTicket { get; set; } [DataMember] public string nonce { get; set; } [DataMember] public bool confirmed { get; set; } }
        [DataContract] private sealed class SessionChallengeBody
        { [DataMember(IsRequired = true)] public string nonce { get; set; } [DataMember(IsRequired = true)] public string evidenceKind { get; set; }
          [DataMember(IsRequired = true)] public string purpose { get; set; } }
        [DataContract] private sealed class SessionAuthorizationBody
        { [DataMember(IsRequired = true)] public string accountId { get; set; } [DataMember(IsRequired = true)] public string proof { get; set; }
          [DataMember(IsRequired = true)] public string evidenceKind { get; set; } }
        [DataContract] private sealed class RequestBody
        { [DataMember] public string proof { get; set; } [DataMember] public string clientKey { get; set; } }
        [DataContract] private sealed class IdBody
        { [DataMember] public string proof { get; set; } [DataMember] public string requestId { get; set; } }
        [DataContract] private sealed class ConfirmBody
        {
            [DataMember] public string proof { get; set; } [DataMember] public string requestId { get; set; }
            [DataMember] public string challenge { get; set; } [DataMember] public string policyRevision { get; set; }
        }
        [DataContract] private sealed class SnapshotBody
        {
            [DataMember(IsRequired = true)] public string requestId { get; set; }
            [DataMember(IsRequired = true)] public string policyRevision { get; set; }
            [DataMember(IsRequired = true)] public string scope { get; set; }
            [DataMember(IsRequired = true)] public string state { get; set; }
            [DataMember] public string challenge { get; set; }
            [DataMember(IsRequired = true)] public string submissionState { get; set; }
            [DataMember] public string completionEvidence { get; set; }
        }
    }
}
