using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using NUnit.Framework;

public class RevivalDeletionSessionTests
{
    private const string Account = "synthetic-session-account";
    private const string Entity = "synthetic-title-entity";
    private const string Ticket = "synthetic-ticket-secret";
    private const string Proof = "synthetic-proof-secret";
    private string _directory, _binding;
    private FileDeletionRecoveryStore _store;
    private DeletionSession _current;
    private string _ticket;

    private sealed class Server : HttpMessageHandler
    {
        public readonly List<string> Paths = new List<string>();
        public readonly List<string> Bodies = new List<string>();
        public string Status = "submission_unknown";
        public bool LoseSubmit, Unsupported, WrongEvidence;
        public TaskCompletionSource<HttpResponseMessage> PendingSubmit;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri.AbsolutePath;
            Paths.Add(path);
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync());
            if (path.EndsWith("config")) return Json("{\"available\":true,\"synthetic\":false,\"evidenceKind\":\"" +
                (WrongEvidence ? "provider_reauthentication" : "session_confirmation") + "\"}");
            if (path.EndsWith("session-challenge")) return Unsupported
                ? Json("{\"code\":\"account_type_unsupported\"}", HttpStatusCode.Conflict)
                : Json("{\"nonce\":\"" + new string('n', 43) + "\",\"purpose\":\"delete_title_account\",\"evidenceKind\":\"session_confirmation\"}");
            if (path.EndsWith("session-confirm")) return Json("{\"accountId\":\"" + Account + "\",\"proof\":\"" + Proof + "\",\"evidenceKind\":\"session_confirmation\"}");
            if (path.EndsWith("/request")) return Snapshot("awaiting_confirmation", "not_submitted", true);
            if (path.EndsWith("/confirm"))
            {
                if (LoseSubmit) throw new HttpRequestException("synthetic transport loss");
                if (PendingSubmit != null) return await PendingSubmit.Task;
            }
            if (path.EndsWith("/cancel")) return Snapshot("cancelled", "not_submitted");
            return Snapshot("processing", Status);
        }
        public static HttpResponseMessage Snapshot(string state, string submission, bool challenge = false) =>
            Json("{\"requestId\":\"request-one\",\"policyRevision\":\"v1\",\"scope\":\"title\",\"state\":\"" + state +
                "\",\"submissionState\":\"" + submission + "\"" + (challenge ? ",\"challenge\":\"synthetic-delete-challenge\"" : "") + "}");
        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }

    [SetUp] public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "tamer-deletion-" + Guid.NewGuid().ToString("N"));
        _store = new FileDeletionRecoveryStore(Path.Combine(_directory, "pending.json"));
        _binding = DeletionRecovery.Hash("https://example.invalid/", "TEST1", Account, Entity);
        _current = new DeletionSession(new object(), Account, "session-one", "TEST1", Entity);
        _ticket = Ticket;
    }
    [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private DeletionFlow Flow(Server server, IDeletionRecoveryStore store = null, Action<DeletionSession> accepted = null, string binding = null)
    {
        var gateway = HttpDeletionGateway.ForSessionConfirmation(new Uri("https://example.invalid/"), session => _ticket);
        var client = typeof(HttpDeletionGateway).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
        ((HttpClient)client.GetValue(gateway)).Dispose();
        client.SetValue(gateway, new HttpClient(server) { BaseAddress = new Uri("https://example.invalid/") });
        return new DeletionFlow(gateway, () => _current, accepted: accepted, recovery: store ?? _store, binding: binding ?? _binding);
    }
    private static int Count(Server server, string operation) => server.Paths.FindAll(p => p == "/v1/deletion/" + operation).Count;

    [Test] public async Task Revival_DeletionSessionExplicitConfirmationSharesIntentAndKeepsSecretsOffDisk()
    {
        var server = new Server();
        using (var flow = Flow(server))
        {
            Assert.That(await flow.RequestAsync(), Is.True);
            Assert.That(flow.State, Is.EqualTo(DeletionState.AwaitingSessionConfirmation));
            Assert.That(Count(server, "session-confirm"), Is.Zero);
            Assert.That(Count(server, "request"), Is.Zero);
            Assert.That(await flow.ConfirmAsync(), Is.False);
            Assert.That(await flow.ConfirmSessionAsync(), Is.True);
            Assert.That(Count(server, "confirm"), Is.Zero);
            string key = _store.Load().ClientKey;
            StringAssert.Contains(key, server.Bodies[1]);
            StringAssert.Contains(key, server.Bodies[3]);
            StringAssert.Contains("\"confirmed\":true", server.Bodies[2]);
            StringAssert.Contains(Proof, server.Bodies[3]);
            string disk = File.ReadAllText(Path.Combine(_directory, "pending.json"));
            foreach (string secret in new[] { Account, Entity, Ticket, Proof, new string('n', 43), "synthetic-delete-challenge" })
                StringAssert.DoesNotContain(secret, disk);
        }
    }

    [Test] public async Task Revival_DeletionSessionRestartUnknownUsesStatusOnlyAndAcceptedCleansOnce()
    {
        var first = new Server { LoseSubmit = true };
        string key;
        using (var flow = Flow(first))
        {
            await flow.RequestAsync(); await flow.ConfirmSessionAsync();
            Assert.That(await flow.ConfirmAsync(), Is.False);
            Assert.That(flow.State, Is.EqualTo(DeletionState.SubmissionUnknown));
            Assert.That(_store.Load().SubmissionStarted, Is.True);
            Assert.That(await flow.ConfirmAsync(), Is.False);
            key = _store.Load().ClientKey;
        }
        _current = new DeletionSession(new object(), Account, "restarted-session", "TEST1", Entity);
        _ticket = "synthetic-new-ticket";
        var second = new Server { Status = "accepted" };
        int cleanups = 0;
        using (var flow = Flow(second, accepted: s => cleanups++))
        {
            Assert.That(flow.State, Is.EqualTo(DeletionState.RecoveryRequired));
            Assert.That(await flow.ReauthenticateAsync(), Is.True);
            Assert.That(await flow.ConfirmSessionAsync(), Is.True);
            Assert.That(flow.State, Is.EqualTo(DeletionState.Accepted));
            Assert.That(cleanups, Is.EqualTo(1));
            Assert.That(Count(second, "request"), Is.Zero);
            Assert.That(Count(second, "confirm"), Is.Zero);
            Assert.That(Count(second, "status"), Is.EqualTo(1));
            StringAssert.Contains(key, second.Bodies[1]);
            StringAssert.Contains("request-one", second.Bodies[3]);
            Assert.That(_store.Load(), Is.Null);
            Assert.That(await flow.RefreshAsync(), Is.False);
        }
    }

    [Test] public async Task Revival_DeletionSessionUnsubmittedRestartRetainsKeyAndRequiresNewConfirmation()
    {
        using (var flow = Flow(new Server())) { await flow.RequestAsync(); await flow.ConfirmSessionAsync(); }
        string key = _store.Load().ClientKey;
        var server = new Server();
        using (var flow = Flow(server))
        {
            Assert.That(await flow.ConfirmAsync(), Is.False);
            await flow.ReauthenticateAsync();
            Assert.That(Count(server, "request"), Is.Zero);
            await flow.ConfirmSessionAsync();
            Assert.That(_store.Load().ClientKey, Is.EqualTo(key));
            Assert.That(flow.Request.RequestId, Is.EqualTo("request-one"));
            Assert.That(Count(server, "confirm"), Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public async Task Revival_DeletionSessionCorruptOrMismatchedRecoveryCannotStartNewIntent(bool corrupt)
    {
        using (var flow = Flow(new Server())) await flow.RequestAsync();
        var path = Path.Combine(_directory, "pending.json");
        if (corrupt) File.WriteAllText(path, "broken fixture");
        string original = File.ReadAllText(path);
        var server = new Server();
        using (var flow = Flow(server, binding: DeletionRecovery.Hash("other-entity")))
        {
            Assert.That(flow.State, Is.EqualTo(DeletionState.Unavailable));
            Assert.That(await flow.RequestAsync(), Is.False);
            Assert.That(server.Paths, Is.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
        }
    }

    private sealed class FailingStore : IDeletionRecoveryStore
    {
        public DeletionRecovery Load() => null;
        public void Save(DeletionRecovery record) { if (record.SubmissionStarted) throw new IOException(); }
        public void Clear() { }
    }
    [Test] public async Task Revival_DeletionSessionJournalFailurePreventsExternalSubmission()
    {
        var server = new Server();
        using (var flow = Flow(server, new FailingStore()))
        {
            await flow.RequestAsync(); await flow.ConfirmSessionAsync();
            Assert.That(await flow.ConfirmAsync(), Is.False);
            Assert.That(Count(server, "confirm"), Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)] public async Task Revival_DeletionSessionLateAcceptedAfterCloseOrReplacementPreservesPending(bool close)
    {
        var server = new Server { PendingSubmit = new TaskCompletionSource<HttpResponseMessage>() };
        int cleanups = 0;
        using (var flow = Flow(server, accepted: s => cleanups++))
        {
            await flow.RequestAsync(); await flow.ConfirmSessionAsync();
            var pending = flow.ConfirmAsync();
            if (close) flow.Dispose();
            else _current = new DeletionSession(new object(), Account, "replacement", "TEST1", Entity);
            server.PendingSubmit.SetResult(Server.Snapshot("processing", "accepted"));
            Assert.That(await pending, Is.False);
            Assert.That(cleanups, Is.Zero);
            Assert.That(_store.Load().SubmissionStarted, Is.True);
        }
    }

    [Test] public async Task Revival_DeletionSessionTicketChangeCannotConsumeNonce()
    {
        var server = new Server();
        using (var flow = Flow(server))
        {
            await flow.RequestAsync(); _ticket = "changed-ticket";
            Assert.That(await flow.ConfirmSessionAsync(), Is.False);
            Assert.That(Count(server, "session-confirm"), Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)] public async Task Revival_DeletionSessionUnsupportedOrWrongEvidenceFailsClosed(bool evidence)
    {
        var server = new Server { Unsupported = !evidence, WrongEvidence = evidence };
        using (var flow = Flow(server))
        {
            Assert.That(await flow.RequestAsync(), Is.False);
            Assert.That(flow.State, Is.EqualTo(evidence ? DeletionState.RetryableFailure : DeletionState.UnsupportedAccount));
            Assert.That(Count(server, "session-confirm"), Is.Zero);
            Assert.That(Count(server, "request"), Is.Zero);
        }
    }
}
