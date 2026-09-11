using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;

internal static class Program
{
    private static void Check(bool result, string label)
    { if (!result) throw new Exception(label); }
    private static async Task Main(string[] args)
    {
        var uri = new Uri(args[0]);
        var mode = args[1];
        using var gateway = new SyntheticLoopbackDeletionGateway(uri, TimeSpan.FromMilliseconds(500));
        var session = new DeletionSession(new object(), SyntheticLoopbackDeletionGateway.AccountId,
            SyntheticLoopbackDeletionGateway.SessionKey);
        using var flow = new DeletionFlow(gateway, () => session);
        if (mode == "identity")
        {
            session = new DeletionSession(new object(), "synthetic-other", SyntheticLoopbackDeletionGateway.SessionKey);
            Check(!await flow.RequestAsync() && flow.State == DeletionState.RetryableFailure, "identity rejected");
        }
        else if (mode == "config" || mode == "redirect" || mode == "unknown" || mode == "evidence" ||
                 mode == "timeout" || mode == "malformed" || mode == "oversize" || mode == "error")
        {
            Check(!await flow.RequestAsync() && flow.State == DeletionState.RetryableFailure && flow.Request == null,
                "invalid HTTP response rejected: " + mode);
        }
        else if (mode == "cancel-token")
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await gateway.ReauthenticateAsync(session, cancel.Token); throw new Exception("cancellation ignored"); }
            catch (OperationCanceledException) { }
        }
        else
        {
            Check(await flow.RequestAsync() && flow.State == DeletionState.AwaitingConfirmation, "request");
            var id = flow.Request.RequestId;
            var challenge = flow.Request.Challenge;
            Check(!string.IsNullOrEmpty(challenge), "challenge from actual JSON");
            Check(await flow.ReauthenticateAsync() && flow.Request.RequestId == id &&
                flow.Request.Challenge != challenge, "reauth rotates challenge, preserves id");
            if (mode == "cancel")
                Check(await flow.CancelAsync() && flow.State == DeletionState.Cancelled, "cancel");
            else
            {
                if (mode == "retry")
                    Check(!await flow.ConfirmAsync() && flow.State == DeletionState.RetryableFailure, "lost confirmation response");
                Check(await flow.ConfirmAsync() && flow.State == DeletionState.Queued && flow.Request.RequestId == id, "confirm/retry");
                using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false });
                foreach (var complete in new[] { false, true })
                {
                    var json = "{\"proof\":\"synthetic-demo-proof\",\"requestId\":\"" + id + "\",\"complete\":" +
                        (complete ? "true" : "false") + "}";
                    using var response = await http.PostAsync(new Uri(uri, "demo/advance"),
                        new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
                    Check(response.IsSuccessStatusCode, "fake worker");
                    Check(await flow.RefreshAsync() && flow.State == (complete ? DeletionState.Completed : DeletionState.Processing), "status");
                }
                Check(!string.IsNullOrEmpty(flow.Request.CompletionEvidence), "completion evidence");
            }
        }
        foreach (var rejected in new[] { "https://127.0.0.1/", "http://localhost/", "http://example.com/", "http://127.0.0.1/path", "http://user@127.0.0.1/" })
        {
            try { using var invalid = new SyntheticLoopbackDeletionGateway(new Uri(rejected)); throw new Exception("origin accepted"); }
            catch (ArgumentException) { }
        }
        Console.WriteLine("PASS " + mode);
    }
}
