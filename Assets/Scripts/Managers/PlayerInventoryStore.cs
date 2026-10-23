using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace AD
{
    // Local-only inventory. Legacy PlayerPrefs are deliberately never read, imported, or removed.
    public sealed class PlayerInventoryStore
    {
        private readonly string _directory;
        private readonly string[] _monsters;
        private readonly Dictionary<string, string> _slots;
        internal sealed class Envelope
        {
            internal string Session { get; }
            internal PlayerInventorySnapshot Snapshot { get; }
            internal Envelope(string session, PlayerInventorySnapshot snapshot) { Session = session; Snapshot = snapshot; }
        }
#if UNITY_EDITOR || TAMER_SHOP_HARNESS
        internal static Action<string, string, string> InventoryWriteCheckpoint;
#endif
        private sealed class Record
        {
            public int Version;
            public string Owner;
            public string[] Collection;
            public string[] OwnedItems;
            public Dictionary<string, string> Equipped;
            public string Session;
        }

        public PlayerInventoryStore(string directory, IEnumerable<string> monsters, IDictionary<string, string> slots)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _monsters = monsters.ToArray();
            _slots = new Dictionary<string, string>(slots, StringComparer.Ordinal);
        }

        public static string OwnerKey(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) throw new InvalidDataException("Authenticated inventory owner required.");
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account))).Replace("-", "").ToLowerInvariant();
        }
        public string PathFor(string account) => Path.Combine(_directory, OwnerKey(account) + ".json");

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("TAMER_GAMEPLAY_HARNESS")]
        private static void MarkSyntheticInventoryStage(string stage)
        {
#if UNITY_EDITOR || TAMER_GAMEPLAY_HARNESS
            RevivalGameplayIsolation.SetReadApplyStage("inventory-" + stage);
#endif
        }

        [System.Diagnostics.Conditional("TAMER_GAMEPLAY_HARNESS")]
        private static void ObserveSyntheticMove(string phase, string source, string destination, int expectedBytes)
        {
#if TAMER_GAMEPLAY_HARNESS
            try
            {
                string full = Path.GetFullPath(source);
                long readableBytes = -1;
                string readFailure = "none";
                try { readableBytes = File.ReadAllBytes(source).LongLength; }
                catch (Exception error) { readFailure = error.GetType().Name; }
                bool javaExists = false;
                long javaBytes = -1;
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var file = new UnityEngine.AndroidJavaObject("java.io.File", source))
                { javaExists = file.Call<bool>("exists"); javaBytes = file.Call<long>("length"); }
#endif
                // Only booleans, lengths and exception type: never paths, names or file contents.
                UnityEngine.Debug.Log("GAMEPLAY_INVENTORY_MOVE phase=" + phase +
                    " sourceExists=" + File.Exists(source) + " fullSourceExists=" + File.Exists(full) +
                    " parentExists=" + Directory.Exists(Path.GetDirectoryName(source)) +
                    " destinationExists=" + File.Exists(destination) +
                    " destinationDirectory=" + Directory.Exists(destination) +
                    " fullPathUnchanged=" + (source == full) +
                    " sameParent=" + (Path.GetDirectoryName(source) == Path.GetDirectoryName(destination)) +
                    " sourceChars=" + source.Length + " nameChars=" + Path.GetFileName(source).Length +
                    " expectedBytes=" + expectedBytes + " readableBytes=" + readableBytes +
                    " readFailure=" + readFailure + " javaExists=" + javaExists + " javaBytes=" + javaBytes);
            }
            catch (Exception error)
            { UnityEngine.Debug.LogWarning("GAMEPLAY_INVENTORY_MOVE observationException=" + error.GetType().Name); }
#endif
        }

        // Unlike Load, this never creates a directory or changes a session binding.
        internal Envelope ReadEnvelope(string account)
        {
            MarkSyntheticInventoryStage("read-envelope");
            var record = ReadRecord(PathFor(account));
            return record == null ? null : new Envelope(record.Session,
                Validate(account, record.Owner, record.Collection, record.OwnedItems, record.Equipped));
        }

        internal void ApplyCommittedPurchase(EquipmentPurchaseRecord purchase, Func<bool> canCommit)
        {
            if (purchase == null || !purchase.Materializing || canCommit == null || !canCommit())
                throw new InvalidOperationException("Equipment recovery is not current.");
            purchase.Before.Validate(purchase.Owner, _monsters, _slots);
            var after = purchase.After.Validate(purchase.Owner, _monsters, _slots);
            if (!_slots.TryGetValue(purchase.ItemId, out var slot) || slot != purchase.Slot)
                throw new InvalidDataException("Equipment catalog changed; originals preserved.");
            var current = ReadEnvelope(purchase.Owner);
            if (current == null || current.Session != purchase.SourceSession)
                throw new InvalidDataException("Equipment source session changed; original preserved.");
            if (purchase.After.Matches(current.Snapshot)) return; // No second materialization write.
            if (!purchase.Before.Matches(current.Snapshot))
                throw new InvalidDataException("Equipment source content changed; original preserved.");
            WriteRecord(purchase.Owner, after, purchase.SourceSession, () =>
            {
                if (!canCommit()) return false;
                var latest = ReadEnvelope(purchase.Owner);
                return latest != null && latest.Session == purchase.SourceSession && purchase.Before.Matches(latest.Snapshot);
            });
        }

        internal void BindSessionCurrent(string account, string session, Func<bool> canCommit)
        {
            if (canCommit == null || !canCommit()) throw new InvalidOperationException("Inventory binding is not current.");
            if (string.IsNullOrEmpty(session)) throw new InvalidDataException("Inventory session required.");
            var snapshot = Load(account);
            var previous = ReadEnvelope(account);
            if (previous != null && previous.Session == session) return;
            WriteRecord(account, snapshot, session, () =>
            {
                if (!canCommit()) return false;
                var latest = ReadEnvelope(account);
                return previous == null ? latest == null : latest != null && latest.Session == previous.Session &&
                    new EquipmentPurchaseRecord.InventoryImage(previous.Snapshot).Matches(latest.Snapshot);
            });
        }

        private static Record ReadRecord(string path)
        {
            MarkSyntheticInventoryStage("read-record");
            string json;
            try { json = File.ReadAllText(path, Encoding.UTF8); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException)
            {
                if (IsMissingDirectory(Path.GetDirectoryName(path))) return null;
                throw;
            }
            var record = JsonConvert.DeserializeObject<Record>(json);
            if (record == null || record.Version != 1 || string.IsNullOrWhiteSpace(record.Owner))
                throw new InvalidDataException("Unsupported inventory record; original preserved.");
            return record;
        }

        public static bool IsMissingDirectory(string directory)
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(directory); }
            catch (FileNotFoundException) { return true; }
            catch (DirectoryNotFoundException) { return true; }
            if ((attributes & FileAttributes.Directory) == 0) throw new IOException("Inventory directory is occupied by a file.");
            return false;
        }

        public void BindSession(string account, string session)
        {
            if (string.IsNullOrEmpty(session)) throw new InvalidDataException("Inventory session required.");
            var snapshot = Load(account);
            var previous = ReadRecord(PathFor(account));
            if (previous != null && previous.Session == session) return;
            WriteRecord(account, snapshot, session);
        }

        public static void DeleteBound(string directory, string ownerKey, string session, Func<string, bool> matchesOwner)
        {
            if (ownerKey == null || ownerKey.Length != 64 || ownerKey.Any(c => !"0123456789abcdef".Contains(c)))
                throw new InvalidDataException("Inventory file binding unavailable.");
            string path = Path.Combine(directory, ownerKey + ".json");
            var record = ReadRecord(path); // Access errors are never interpreted as absence.
            if (record == null) return; // A previous cleanup may already have removed this exact file.
            if (OwnerKey(record.Owner) != ownerKey || !matchesOwner(record.Owner) ||
                string.IsNullOrEmpty(session) || record.Session != session || record.Collection == null ||
                record.OwnedItems == null || record.Equipped == null || record.Equipped.Count != 2 ||
                !record.Equipped.ContainsKey("Sword") || !record.Equipped.ContainsKey("Shield"))
                throw new InvalidDataException("Inventory owner/session changed; file preserved.");
            File.Delete(path); // A sharing/access failure propagates, retaining receipt recovery.
        }

        public PlayerInventorySnapshot Load(string account)
        {
            MarkSyntheticInventoryStage("load-path-key");
            string path = PathFor(account);
            // Creating the directory fails visibly if storage is unavailable; absence is not an I/O fallback.
            MarkSyntheticInventoryStage("create-directory");
            Directory.CreateDirectory(_directory);
            var record = ReadRecord(path);
            if (record == null) return Validate(account, account, Array.Empty<string>(), Array.Empty<string>(), EmptySlots());
            return Validate(account, record.Owner, record.Collection, record.OwnedItems, record.Equipped);
        }

        public PlayerInventorySnapshot Save(string account, IEnumerable<string> collection, IEnumerable<string> owned,
            IDictionary<string, string> equipped)
        {
            var candidate = Validate(account, account, collection, owned, equipped);
            // Validate any previous record before replacing it. Never overwrite malformed/foreign data with an empty fallback.
            Load(account);
            WriteRecord(account, candidate, ReadRecord(PathFor(account))?.Session);
            return candidate;
        }

        // Direct equipment changes keep the exact source image/session until the replacement boundary.
        // General inventory writers and playerless binding retain their existing API and policy.
        internal PlayerInventorySnapshot SaveCurrent(string account, PlayerInventorySnapshot before, string session,
            IDictionary<string, string> equipped, Func<bool> canCommit, Action replacing)
        {
            if (before == null || before.Owner != account || string.IsNullOrEmpty(session) || canCommit == null)
                throw new InvalidOperationException("Inventory change binding is unavailable.");
            var candidate = Validate(account, account, before.Collection, before.OwnedItems, equipped);
            var image = new EquipmentPurchaseRecord.InventoryImage(before);
            Func<bool> current = () =>
            {
                if (!canCommit()) return false;
                var latest = ReadEnvelope(account);
                return latest != null && latest.Session == session && image.Matches(latest.Snapshot);
            };
            if (!current()) throw new InvalidOperationException("Inventory source changed.");
            WriteRecord(account, candidate, session, current, replacing);
            return candidate;
        }

        private void WriteRecord(string account, PlayerInventorySnapshot candidate, string session, Func<bool> canCommit = null,
            Action replacing = null)
        {
            MarkSyntheticInventoryStage("write-path-key");
            string path = PathFor(account);
            // Keep the atomic temporary beside its destination without repeating the 64-char
            // owner key. Some Android translation layers truncate long paths on open while
            // stat/move use the full path, leaving a written temporary that cannot be moved.
            string temporary = Path.Combine(_directory, ".pending-" + Guid.NewGuid().ToString("N"));
            var record = new Record { Version = 1, Owner = account, Collection = candidate.Collection.ToArray(),
                OwnedItems = candidate.OwnedItems.ToArray(), Equipped = new Dictionary<string, string>(candidate.Equipped), Session = session };
            MarkSyntheticInventoryStage("serialize");
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record));
            MarkSyntheticInventoryStage("open-pending");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                MarkSyntheticInventoryStage("write-pending");
                stream.Write(bytes, 0, bytes.Length);
                MarkSyntheticInventoryStage("flush-pending");
                stream.Flush(true);
            }
#if UNITY_EDITOR || TAMER_SHOP_HARNESS
            InventoryWriteCheckpoint?.Invoke(path, temporary, "temporary-closed");
#endif
            MarkSyntheticInventoryStage("validate-current-owner");
            if (canCommit != null && !canCommit()) throw new InvalidOperationException("Inventory owner changed.");
            MarkSyntheticInventoryStage("replace-callback");
            replacing?.Invoke(); // Any error from this point may follow a durable replacement.
            MarkSyntheticInventoryStage("move-to-destination");
            ObserveSyntheticMove("before", temporary, path, bytes.Length);
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path))
            {
                MarkSyntheticInventoryStage("replace-existing");
                File.Replace(temporary, path, null);
            }
            catch (IOException)
            {
                ObserveSyntheticMove("failed", temporary, path, bytes.Length);
                throw;
            }
#if UNITY_EDITOR || TAMER_SHOP_HARNESS
            InventoryWriteCheckpoint?.Invoke(path, temporary, "replaced");
#endif
        }

        private PlayerInventorySnapshot Validate(string account, string owner, IEnumerable<string> collection,
            IEnumerable<string> owned, IDictionary<string, string> equipped) =>
            PlayerInventorySnapshot.Validate(account, owner, collection, owned, equipped, _monsters, _slots);

        public static Dictionary<string, string> EmptySlots() =>
            new Dictionary<string, string> { { "Sword", null }, { "Shield", null } };
    }
}
