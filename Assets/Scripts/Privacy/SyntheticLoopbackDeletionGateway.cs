#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
    /// <summary>Explicit local_demo adapter. Fixed fake identity; never accepts production credentials.</summary>
    public sealed class SyntheticLoopbackDeletionGateway : IDeletionGateway, IDisposable
    {
        public const string AccountId = "synthetic-demo-account";
        public const string SessionKey = "synthetic-demo-session";
        private const string Proof = "synthetic-demo-proof";
        private readonly HttpClient _client;
        private bool _disposed;
        public bool IsAvailable => !_disposed;
        public bool IsSynthetic => true;

        public SyntheticLoopbackDeletionGateway(Uri endpoint, TimeSpan? timeout = null)
        {
            // Numeric IPv4 only: no DNS, proxy, credentials, path injection or redirect.
            if (endpoint == null || !endpoint.IsAbsoluteUri || endpoint.Scheme != "http" ||
                endpoint.Host != "127.0.0.1" || endpoint.UserInfo != "" || endpoint.AbsolutePath != "/" ||
                endpoint.Query != "" || endpoint.Fragment != "")
                throw new ArgumentException("A numeric loopback HTTP origin is required.", nameof(endpoint));
            var duration = timeout ?? TimeSpan.FromSeconds(5);
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromSeconds(30))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            { BaseAddress = endpoint, Timeout = duration, MaxResponseContentBufferSize = 16384 };
        }

        public async Task<DeletionAuthorization> ReauthenticateAsync(DeletionSession session, CancellationToken token)
        {
            if (session == null || !session.IsValid || session.AccountId != AccountId || session.SessionKey != SessionKey)
                throw new InvalidOperationException("Synthetic demo session required.");
            var config = await Send<Config>("v1/deletion/config", null, token);
            if (!config.available || !config.synthetic) throw new InvalidOperationException("Synthetic service unavailable.");
            // This is a demo identity check, not real reauthentication.
            return new DeletionAuthorization(AccountId, Proof);
        }

        private static void Validate(DeletionAuthorization auth)
        {
            if (auth == null || auth.AccountId != AccountId || auth.Proof != Proof)
                throw new InvalidOperationException("Synthetic demo authorization required.");
        }

        public Task<DeletionSnapshot> RequestAsync(DeletionAuthorization auth, string key, CancellationToken token)
        { Validate(auth); return Snapshot("request", new RequestBody { proof = Proof, clientKey = key }, token); }
        public Task<DeletionSnapshot> ConfirmAsync(DeletionAuthorization auth, DeletionSnapshot request, CancellationToken token)
        {
            Validate(auth);
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Snapshot("confirm", new ConfirmBody { proof = Proof, requestId = request.RequestId,
                challenge = request.Challenge, policyRevision = request.PolicyRevision }, token);
        }
        public Task<DeletionSnapshot> StatusAsync(DeletionAuthorization auth, string id, CancellationToken token)
        { Validate(auth); return Snapshot("status", new IdBody { proof = Proof, requestId = id }, token); }
        public Task<DeletionSnapshot> CancelAsync(DeletionAuthorization auth, string id, CancellationToken token)
        { Validate(auth); return Snapshot("cancel", new IdBody { proof = Proof, requestId = id }, token); }

        private async Task<DeletionSnapshot> Snapshot(string operation, object body, CancellationToken token)
        {
            var result = await Send<SnapshotBody>("v1/deletion/" + operation, body, token);
            DeletionState state;
            switch (result.state)
            {
                case "awaiting_confirmation": state = DeletionState.AwaitingConfirmation; break;
                case "queued": state = DeletionState.Queued; break;
                case "processing": state = DeletionState.Processing; break;
                case "completed": state = DeletionState.Completed; break;
                case "cancelled": state = DeletionState.Cancelled; break;
                default: throw new InvalidOperationException("Invalid deletion state.");
            }
            if (string.IsNullOrEmpty(result.requestId) || string.IsNullOrEmpty(result.policyRevision) ||
                (result.scope != "title" && result.scope != "master") ||
                (state == DeletionState.Completed && string.IsNullOrEmpty(result.completionEvidence)))
                throw new InvalidOperationException("Invalid deletion response.");
            return new DeletionSnapshot(result.requestId, result.policyRevision, result.scope, state,
                result.challenge, result.completionEvidence);
        }

        private async Task<T> Send<T>(string path, object body, CancellationToken token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SyntheticLoopbackDeletionGateway));
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
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Synthetic deletion HTTP request failed.");
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
        { [DataMember(IsRequired = true)] public bool available { get; set; } [DataMember(IsRequired = true)] public bool synthetic { get; set; } }
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
            [DataMember] public string completionEvidence { get; set; }
        }
    }
}
#endif
