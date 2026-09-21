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

        private static Record ReadRecord(string path)
        {
            string json;
            try { json = File.ReadAllText(path, Encoding.UTF8); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) when (IsMissingDirectory(Path.GetDirectoryName(path))) { return null; }
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
            string path = PathFor(account);
            // Creating the directory fails visibly if storage is unavailable; absence is not an I/O fallback.
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

        private void WriteRecord(string account, PlayerInventorySnapshot candidate, string session)
        {
            string path = PathFor(account), temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            var record = new Record { Version = 1, Owner = account, Collection = candidate.Collection.ToArray(),
                OwnedItems = candidate.OwnedItems.ToArray(), Equipped = new Dictionary<string, string>(candidate.Equipped), Session = session };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path)) { File.Replace(temporary, path, null); }
        }

        private PlayerInventorySnapshot Validate(string account, string owner, IEnumerable<string> collection,
            IEnumerable<string> owned, IDictionary<string, string> equipped) =>
            PlayerInventorySnapshot.Validate(account, owner, collection, owned, equipped, _monsters, _slots);

        public static Dictionary<string, string> EmptySlots() =>
            new Dictionary<string, string> { { "Sword", null }, { "Shield", null } };
    }
}
