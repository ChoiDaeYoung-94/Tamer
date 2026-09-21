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
        }

        public PlayerInventoryStore(string directory, IEnumerable<string> monsters, IDictionary<string, string> slots)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _monsters = monsters.ToArray();
            _slots = new Dictionary<string, string>(slots, StringComparer.Ordinal);
        }

        public string PathFor(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) throw new InvalidDataException("Authenticated inventory owner required.");
            using (var sha = SHA256.Create())
                return Path.Combine(_directory, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account))).Replace("-", "").ToLowerInvariant() + ".json");
        }

        public PlayerInventorySnapshot Load(string account)
        {
            string path = PathFor(account);
            // Creating the directory fails visibly if storage is unavailable; absence is not an I/O fallback.
            Directory.CreateDirectory(_directory);
            string json;
            try { json = File.ReadAllText(path, Encoding.UTF8); }
            catch (FileNotFoundException) { return Validate(account, account, Array.Empty<string>(), Array.Empty<string>(), EmptySlots()); }
            var record = JsonConvert.DeserializeObject<Record>(json);
            if (record == null || record.Version != 1) throw new InvalidDataException("Unsupported inventory record; original preserved.");
            return Validate(account, record.Owner, record.Collection, record.OwnedItems, record.Equipped);
        }

        public PlayerInventorySnapshot Save(string account, IEnumerable<string> collection, IEnumerable<string> owned,
            IDictionary<string, string> equipped)
        {
            var candidate = Validate(account, account, collection, owned, equipped);
            // Validate any previous record before replacing it. Never overwrite malformed/foreign data with an empty fallback.
            Load(account);
            string path = PathFor(account), temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            var record = new Record { Version = 1, Owner = account, Collection = candidate.Collection.ToArray(),
                OwnedItems = candidate.OwnedItems.ToArray(), Equipped = new Dictionary<string, string>(candidate.Equipped) };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path)) { File.Replace(temporary, path, null); }
            return candidate; // Publish to memory only after the durable atomic replacement succeeds.
        }

        private PlayerInventorySnapshot Validate(string account, string owner, IEnumerable<string> collection,
            IEnumerable<string> owned, IDictionary<string, string> equipped) =>
            PlayerInventorySnapshot.Validate(account, owner, collection, owned, equipped, _monsters, _slots);

        public static Dictionary<string, string> EmptySlots() =>
            new Dictionary<string, string> { { "Sword", null }, { "Shield", null } };
    }
}
