using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AD.Purchasing;
using UnityEngine;
using UnityEngine.Networking;

namespace AD
{
    /// <summary>Explicit opt-in transport to the reviewed server endpoint. No default endpoint.</summary>
    public sealed class IapReceiptHttpVerifier : IReceiptVerifier
    {
        private readonly string _endpoint;
        public IapReceiptHttpVerifier(string endpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath != "/v1/no-ads/verify")
                throw new ArgumentException("A reviewed HTTPS receipt endpoint is required.", nameof(endpoint));
            _endpoint = uri.AbsoluteUri;
        }

        [Serializable] private sealed class Request
        {
            public string requestId;
            public string receipt;
            public string sessionTicket;
        }
        [Serializable] private sealed class Response
        {
            public bool verified;
            public string requestId;
            public string accountId;
            public string productId;
        }

        public async Task<bool> VerifyAsync(string receipt, ReceiptSession session, CancellationToken token)
        {
            if (session == null || !session.IsValid || string.IsNullOrEmpty(receipt) || receipt.Length > 32768)
                return false;
            string nonce = Guid.NewGuid().ToString("N");
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Request
                { requestId = nonce, receipt = receipt, sessionTicket = session.SessionTicket }));
            using (var request = new UnityWebRequest(_endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 20;
                request.redirectLimit = 0;
                var operation = request.SendWebRequest();
                try
                {
                    while (!operation.isDone)
                    {
                        token.ThrowIfCancellationRequested();
                        if (request.downloadedBytes > 4096) return false;
                        await Task.Delay(50, token);
                    }
                    token.ThrowIfCancellationRequested();
                    if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200 ||
                        request.downloadedBytes > 4096) return false;
                    var result = JsonUtility.FromJson<Response>(request.downloadHandler.text);
                    return result != null && result.verified && result.requestId == nonce &&
                        result.accountId == session.AccountId && result.productId == NoAdsPurchaseFulfillment.ProductId;
                }
                finally { if (!operation.isDone) request.Abort(); }
            }
        }
    }
}
