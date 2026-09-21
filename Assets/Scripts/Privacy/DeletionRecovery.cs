using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace AD.Privacy
{
    // Metadata only. Tickets, proofs, nonces and confirmation challenges never enter this journal.
    [DataContract]
    public sealed class DeletionRecovery
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember(IsRequired = true)] public string Binding;
        [DataMember(IsRequired = true)] public string ClientKey;
        [DataMember] public string RequestId;
        [DataMember] public string Revision;
        [DataMember(IsRequired = true)] public bool SubmissionStarted;
        [DataMember] public string Origin;
        [DataMember] public string Title;
        [DataMember] public string OwnerHash;
        [DataMember] public string KeyAlias;
        [DataMember] public bool KeyCreated;
        [DataMember] public bool ReceiptRegistered;
        [DataMember] public double ReceiptExpires;
        [DataMember] public string TerminalState;
        [DataMember] public string TerminalSeal;
        [DataMember] public bool CleanupApplied;

        public void Validate()
        {
            if (Version != 1 || !Hex(Binding, 64) || !Hex(ClientKey, 32) ||
                (RequestId == null) != (Revision == null) ||
                RequestId != null && (RequestId.Length == 0 || RequestId.Length > 128 || Revision.Length == 0 || Revision.Length > 128) ||
                SubmissionStarted && RequestId == null)
                throw new InvalidDataException("Deletion recovery is unavailable; original record preserved.");
            if (KeyCreated || ReceiptRegistered || TerminalState != null)
            {
                if (string.IsNullOrEmpty(Origin) || string.IsNullOrEmpty(Title) || !Hex(OwnerHash, 64) ||
                    KeyAlias != "tamer.deletion.receipt." + ClientKey || RequestId == null ||
                    ReceiptRegistered && (!KeyCreated || double.IsNaN(ReceiptExpires) || double.IsInfinity(ReceiptExpires) || ReceiptExpires <= 0) ||
                    TerminalState != null && (!ReceiptRegistered || (TerminalState != "accepted" && TerminalState != "cancelled") || !Hex(TerminalSeal, 64)))
                    throw new InvalidDataException("Deletion receipt binding is unavailable.");
            }
        }

        private static bool Hex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        public static string Hash(params string[] values)
        {
            // Length prefixes avoid ambiguous title/account/entity boundaries.
            var text = new StringBuilder();
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("Account binding is required.");
                text.Append(Encoding.UTF8.GetByteCount(value)).Append(':').Append(value);
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
    }

    public interface IDeletionRecoveryStore
    {
        DeletionRecovery Load();
        void Save(DeletionRecovery recovery);
        void Clear();
    }

    public sealed class FileDeletionRecoveryStore : IDeletionRecoveryStore
    {
        private readonly string _path;
        public FileDeletionRecoveryStore(string path) { _path = Path.GetFullPath(path); }
        public DeletionRecovery Load()
        {
            FileStream input;
            // File.Exists also returns false on some access errors; only true absence means no pending intent.
            try { input = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            using (var stream = input)
            {
                if (stream.Length > 4096) throw new InvalidDataException("Deletion recovery is unavailable.");
                var record = (DeletionRecovery)new DataContractJsonSerializer(typeof(DeletionRecovery)).ReadObject(stream);
                if (record == null) throw new InvalidDataException();
                record.Validate();
                return record;
            }
        }
        public void Save(DeletionRecovery recovery)
        {
            recovery.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temporary = _path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(DeletionRecovery)).WriteObject(stream, recovery);
                    stream.Flush(true);
                }
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        // File.Delete is already idempotent for an absent file. Do not hide access failures with File.Exists:
        // the caller must retain the protected key whenever removal of the journal fails.
        public void Clear() { File.Delete(_path); }
    }

    // Set by explicit application bootstrap before login. An unreadable record fails closed.
    public static class DeletionRecoveryGuard
    {
        public static Func<string, bool> HasPendingSubmission { get; set; }
        public static bool IsPending(string account)
        {
            try { return HasPendingSubmission?.Invoke(account) ?? false; }
            catch (Exception) { return true; }
        }
    }
}
