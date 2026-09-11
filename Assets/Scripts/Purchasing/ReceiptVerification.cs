using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Purchasing
{
    public sealed class ReceiptSession
    {
        public string AccountId { get; }
        public string SessionTicket { get; }
        public ReceiptSession(string accountId, string sessionTicket)
        {
            AccountId = accountId;
            SessionTicket = sessionTicket;
        }
        public bool IsValid => !string.IsNullOrEmpty(AccountId) && !string.IsNullOrEmpty(SessionTicket);
        public bool Matches(ReceiptSession other) => other != null && IsValid &&
            AccountId == other.AccountId && SessionTicket == other.SessionTicket;
        public string ObfuscatedAccountId
        {
            get
            {
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                        "tamer-iap-v1:" + AccountId))).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public interface IReceiptVerifier
    {
        Task<bool> VerifyAsync(string receipt, ReceiptSession session, CancellationToken token);
    }

    /// <summary>Account/session changes invalidate late validation before any grant.</summary>
    public static class ReceiptVerification
    {
        public static async Task<bool> VerifyCurrentAsync(IReceiptVerifier verifier, string receipt,
            Func<ReceiptSession> currentSession, CancellationToken token)
        {
            try
            {
                var session = currentSession();
                if (session == null || !session.IsValid || string.IsNullOrEmpty(receipt)) return false;
                bool valid = await verifier.VerifyAsync(receipt, session, token);
                return valid && !token.IsCancellationRequested && session.Matches(currentSession());
            }
            catch (Exception) { return false; }
        }
    }
}
