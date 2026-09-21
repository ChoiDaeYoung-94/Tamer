using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using NUnit.Framework;

public class RevivalDeletionHttpGatewayTests
{
    private sealed class Replies : HttpMessageHandler
    {
        public string Body;
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    [TestCase("accepted", DeletionState.Accepted)]
    [TestCase("submission_unknown", DeletionState.SubmissionUnknown)]
    public async Task Revival_DeletionHttpGatewayProjectsSubmissionWithoutCompletion(string receipt, DeletionState expected)
    {
        var replies = new Replies { Body = "{\"requestId\":\"request\",\"policyRevision\":\"v1\",\"scope\":\"title\",\"state\":\"processing\",\"submissionState\":\"" + receipt + "\"}" };
        using (var gateway = new HttpDeletionGateway(new Uri("https://example.invalid/"),
            (session, token) => Task.FromResult(new DeletionAuthorization(session.AccountId, "server-proof"))))
        {
            var field = typeof(HttpDeletionGateway).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
            ((HttpClient)field.GetValue(gateway)).Dispose();
            field.SetValue(gateway, new HttpClient(replies) { BaseAddress = new Uri("https://example.invalid/") });
            var snapshot = await gateway.StatusAsync(new DeletionAuthorization("synthetic-a", "server-proof"), "request", CancellationToken.None);
            Assert.That(snapshot.State, Is.EqualTo(expected));
            Assert.That(replies.Calls, Is.EqualTo(1));
        }
    }

    [TestCase("http://example.invalid/")]
    [TestCase("https://example.invalid/path")]
    [TestCase("https://example.invalid/?token=value")]
    public void Revival_DeletionHttpGatewayRejectsUnsafeOrigins(string origin)
    {
        Assert.Throws<ArgumentException>(() => new HttpDeletionGateway(new Uri(origin), (session, token) => null));
    }
}
