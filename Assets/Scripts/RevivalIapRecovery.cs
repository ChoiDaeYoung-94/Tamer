#if UNITY_EDITOR || TAMER_IAP_HARNESS
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AD.Purchasing;

namespace AD
{
    // A new authentication generation on the same object invalidates an old receipt session.
    public sealed class RevivalIapRecoveryBinding
    {
        private readonly object _owner;
        private readonly int _generation;
        private readonly ReceiptSession _session;
        public RevivalIapRecoveryBinding(object owner, int generation, ReceiptSession session)
        {
            if (owner == null || session == null || !session.IsValid) throw new ArgumentException("Ready recovery binding required.");
            _owner = owner; _generation = generation; _session = session;
        }
        public ReceiptSession Current(object owner, int generation, bool ready, string account) =>
            ReferenceEquals(_owner, owner) && _generation == generation && ready && account == _session.AccountId ? _session : null;
    }

    // No credential, account identifier or receipt is persisted by this class.
    public sealed class RevivalIapRecoveryGate : IIapExecutionGate
    {
        private bool _opened, _spent, _closed;
        public bool CanAccessStore => _opened && !_spent && !_closed;
        public bool AllowPurchases => false;
        public bool AllowAutomaticRetry => false;
        public bool Open(string titleRoot, bool approvedBuild, bool userConfirmed)
        {
            if (_opened || _closed || !approvedBuild || !userConfirmed) return false;
            try
            {
                RevivalIapRecoveryProfile.CheckAncestors(titleRoot);
                // Reserve the entire attempt BEFORE connecting. Crash/network failure burns it.
                // This title-wide ledger survives manager/profile replacement and app restart.
                using (var stream = new FileStream(Path.Combine(titleRoot, "recovery-attempt-v1.lock"),
                    FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = Encoding.UTF8.GetBytes("STOP inherited; one explicit recovery attempt reserved\n");
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                _opened = true;
                return true;
            }
            catch (Exception) { _closed = true; return false; }
        }
        public bool TryBeginVerification()
        {
            if (!CanAccessStore) return false;
            _spent = true;
            return true;
        }
        public void Close() { _closed = true; }
    }

    // Sequential staging rejects duplicate and late login/inventory callbacks.
    public sealed class RevivalIapRecoveryStaging
    {
        private string _expected;
        private int _phase;
        public ReceiptSession Session { get; private set; }
        public RevivalIapRecoveryStaging(string expected)
        {
            if (string.IsNullOrWhiteSpace(expected)) throw new ArgumentException("Expected account required.");
            _expected = expected;
        }
        public bool AcceptLogin(string account, string ticket, bool newlyCreated)
        {
            if (_phase != 0) return false;
            _phase = 1;
            if (newlyCreated || account != _expected || string.IsNullOrWhiteSpace(ticket)) { Cancel(); return false; }
            Session = new ReceiptSession(account, ticket);
            _expected = null;
            return true;
        }
        public bool AcceptInventory(ReceiptSession session, bool owned)
        {
            if (_phase != 1 || Session == null || !Session.Matches(session)) return false;
            _phase = 2;
            if (!owned) { Cancel(); return false; }
            return true;
        }
        public void Cancel() { _phase = 3; _expected = null; Session = null; }
    }

    public static class RevivalIapRecoveryProfile
    {
        public const string TestTitle = "12B656";
        public static string TitleRoot(string root, string title)
        {
            if (!Path.IsPathRooted(root) || !string.Equals(title, TestTitle, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Exact isolated recovery target required.");
            string path = Path.Combine(Path.GetFullPath(root), "IapTest", TestTitle);
            CheckAncestors(path);
            return path;
        }
        public static void CheckAncestors(string path)
        {
            if (!Path.IsPathRooted(path)) throw new InvalidOperationException("Absolute local root required.");
            for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            {
                try
                {
                    var attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
                        throw new InvalidDataException("Unexpected recovery path; preserve and stop.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
        public static string Prepare(string root, string title, string account, string slot)
        {
            if (string.IsNullOrWhiteSpace(account) || !Guid.TryParseExact(slot, "N", out _))
                throw new InvalidOperationException("Authenticated account and fresh slot required.");
            string selection = Path.Combine(TitleRoot(root, title), "recovery-profile-v1.selection");
            if (File.Exists(selection) || Directory.Exists(selection) || File.Exists(selection + ".pending") || Directory.Exists(selection + ".pending"))
                throw new IOException("Committed recovery profile exists; explicitly reauthenticate it.");
            string key;
            using (var sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(TestTitle + ":" + account))).Replace("-", "");
            string parent = Path.Combine(TitleRoot(root, title), "RecoveryProfilesV1", key);
            string directory = Path.Combine(parent, slot);
            CheckAncestors(directory);
            if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Recovery profile collision; preserved.");
            Directory.CreateDirectory(parent);
            // An interrupted reservation remains evidence. Never delete or silently reuse it.
            using (var reservation = new FileStream(directory + ".reserved", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                reservation.Flush(true);
            Directory.CreateDirectory(directory);
            if (Directory.GetFileSystemEntries(directory).Length != 0) throw new IOException("Recovery profile is not empty.");
            return Path.Combine(directory, "PlayerData.json");
        }
        public static void ValidatePrepared(string root, string title, string account, string path)
        {
            if (string.IsNullOrWhiteSpace(account)) throw new InvalidOperationException("Authenticated owner required.");
            string key;
            using (var sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(TestTitle + ":" + account))).Replace("-", "");
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            string slot = Path.GetFileName(directory);
            if (!Guid.TryParseExact(slot, "N", out _) || Path.GetFullPath(path) !=
                Path.Combine(TitleRoot(root, title), "RecoveryProfilesV1", key, slot, "PlayerData.json"))
                throw new InvalidDataException("Foreign recovery profile; preserved.");
            CheckAncestors(directory);
            string reservation = directory + ".reserved";
            if ((File.GetAttributes(reservation) & FileAttributes.ReparsePoint) != 0 ||
                Directory.GetFileSystemEntries(directory).Length != 0)
                throw new InvalidDataException("Profile already used or unexpected reservation; preserved.");
        }
        public static void CheckNoDeletionRecovery(string persistentRoot)
        {
            // Recovery harness has no deletion gateway. Any retained record requires separate review.
            foreach (string name in new[] { "CloudScriptDeletion", "DeletionRecovery" })
            {
                string directory = Path.Combine(persistentRoot, name);
                CheckAncestors(directory);
                if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0)
                    throw new InvalidDataException("Retained deletion recovery requires review; preserved.");
            }
        }
        public static string Selected(string root, string title, string account)
        {
            string titleRoot = TitleRoot(root, title);
            string selection = Path.Combine(titleRoot, "recovery-profile-v1.selection");
            if ((File.GetAttributes(selection) & FileAttributes.ReparsePoint) != 0 || new FileInfo(selection).Length > 128)
                throw new InvalidDataException("Invalid profile selection; preserved.");
            string relative = File.ReadAllText(selection);
            if (!Regex.IsMatch(relative, "\\A[A-F0-9]{64}/[a-f0-9]{32}\\z"))
                throw new InvalidDataException("Invalid profile selection; preserved.");
            string key;
            using (var sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(TestTitle + ":" + account))).Replace("-", "");
            if (!relative.StartsWith(key + "/", StringComparison.Ordinal))
                throw new InvalidDataException("Selected profile belongs to another authenticated account.");
            string directory = Path.Combine(titleRoot, "RecoveryProfilesV1", key, relative.Substring(65));
            CheckAncestors(directory);
            foreach (var file in new[] { Path.Combine(directory, "ready"), Path.Combine(directory, "PlayerData.json") })
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Unexpected selected profile; preserved.");
            return Path.Combine(directory, "PlayerData.json");
        }
        public static void MarkReady(string root, string title, string path)
        {
            CheckAncestors(Path.GetDirectoryName(path));
            using (var stream = new FileStream(Path.Combine(Path.GetDirectoryName(path), "ready"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes("Prepared profile bound; explicit reauthentication required\n");
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            string directory = Path.GetDirectoryName(path);
            string relative = Path.GetFileName(Path.GetDirectoryName(directory)) + "/" + Path.GetFileName(directory);
            string selection = Path.Combine(TitleRoot(root, title), "recovery-profile-v1.selection");
            // Flush pending selection, then atomically rename without replacement. Interrupted
            // pending records are retained and block new profiles; no automatic fallback.
            using (var stream = new FileStream(selection + ".pending",
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(relative);
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            File.Move(selection + ".pending", selection);
        }
    }
}
#endif
