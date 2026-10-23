using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AD
{
    // Local commit metadata, never a game key or a server patch. No authentication secrets.
    internal sealed class EquipmentPurchaseRecord
    {
        internal const string Key = "__TamerEquipmentTransaction";
        internal const int MaximumBytes = 1024 * 1024;
        internal string Owner { get; private set; }
        internal long Sequence { get; private set; }
        internal string TransactionId { get; private set; }
        internal string RequestHash { get; private set; }
        internal bool Materializing { get; private set; }
        internal string ItemId { get; private set; }
        internal string Slot { get; private set; }
        internal int Price { get; private set; }
        internal int GoldBefore { get; private set; }
        internal int GoldAfter { get; private set; }
        internal long GoldRevision { get; private set; }
        internal string SourceSession { get; private set; }
        internal InventoryImage Before { get; private set; }
        internal InventoryImage After { get; private set; }

        internal sealed class InventoryImage
        {
            internal ReadOnlyCollection<string> Collection { get; }
            internal ReadOnlyCollection<string> OwnedItems { get; }
            internal ReadOnlyDictionary<string, string> Equipped { get; }

            internal InventoryImage(PlayerInventorySnapshot snapshot)
                : this(snapshot.Collection, snapshot.OwnedItems, snapshot.Equipped) { }

            private InventoryImage(IEnumerable<string> collection, IEnumerable<string> owned,
                IEnumerable<KeyValuePair<string, string>> equipped)
            {
                Collection = new List<string>(collection).AsReadOnly();
                OwnedItems = new List<string>(owned).AsReadOnly();
                Equipped = new ReadOnlyDictionary<string, string>(equipped.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
            }

            internal PlayerInventorySnapshot Validate(string owner, IEnumerable<string> monsters, IDictionary<string, string> slots) =>
                PlayerInventorySnapshot.Validate(owner, owner, Collection, OwnedItems,
                    new Dictionary<string, string>(Equipped), monsters, slots);

            internal bool Matches(PlayerInventorySnapshot value) => value != null &&
                Collection.SequenceEqual(value.Collection, StringComparer.Ordinal) &&
                OwnedItems.SequenceEqual(value.OwnedItems, StringComparer.Ordinal) &&
                Equipped["Sword"] == value.Equipped["Sword"] && Equipped["Shield"] == value.Equipped["Shield"];

            internal JObject Token() => new JObject
            {
                ["collection"] = new JArray(Collection), ["ownedItems"] = new JArray(OwnedItems),
                ["equipped"] = new JObject { ["Sword"] = Equipped["Sword"] == null ? JValue.CreateNull() : new JValue(Equipped["Sword"]),
                    ["Shield"] = Equipped["Shield"] == null ? JValue.CreateNull() : new JValue(Equipped["Shield"]) }
            };

            internal static InventoryImage Parse(JToken token)
            {
                var obj = Shape(token, "collection", "ownedItems", "equipped");
                var equipped = Shape(obj["equipped"], "Sword", "Shield");
                return new InventoryImage(Names(obj["collection"]), Names(obj["ownedItems"]),
                    new Dictionary<string, string> { ["Sword"] = NullableName(equipped["Sword"]), ["Shield"] = NullableName(equipped["Shield"]) });
            }

            private static string[] Names(JToken token)
            {
                if (!(token is JArray array) || array.Any(v => v.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)v)))
                    throw Invalid();
                var names = array.Select(v => (string)v).ToArray();
                if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw Invalid();
                return names;
            }

            private static string NullableName(JToken token) => token?.Type == JTokenType.Null ? null : Text(token);
        }

        internal static bool IsReserved(string key) => key == Key || key == "__TamerAccountOwner" || key == "__TamerPendingJournal";

        // Scope the outer check to our reserved fields. Unknown legacy game/server keys are not schema errors.
        internal static string Extract(string json)
        {
            string transaction = null;
            bool ended = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw Invalid();
                while (reader.Read())
                {
                    if (ended || reader.TokenType == JsonToken.Comment) throw Invalid();
                    if (reader.Depth == 0 && reader.TokenType == JsonToken.EndObject) { ended = true; continue; }
                    if (reader.Depth != 1 || reader.TokenType != JsonToken.PropertyName) continue;
                    string name = (string)reader.Value;
                    if (!IsReserved(name)) continue;
                    if (!seen.Add(name) || !reader.Read() || reader.TokenType != JsonToken.String) throw Invalid();
                    if (name == Key) transaction = (string)reader.Value;
                }
                if (!ended) throw Invalid();
            }
            return transaction;
        }

        internal static EquipmentPurchaseRecord Parse(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw Invalid();
            JObject obj;
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw Invalid();
            }
            string phase = Text(obj["phase"]);
            bool pending = phase == "Materializing";
            if (!pending && phase != "Complete") throw Invalid();
            Shape(obj, pending ? new[] { "version", "owner", "sequence", "transactionId", "phase", "requestHash", "item", "payment", "inventory" }
                : new[] { "version", "owner", "sequence", "transactionId", "phase", "requestHash" });
            if (Text(obj["version"]) != "1") throw Invalid();
            var record = new EquipmentPurchaseRecord
            {
                Owner = Text(obj["owner"]), Sequence = Number(obj["sequence"], long.MaxValue - 1, true),
                TransactionId = Text(obj["transactionId"]), RequestHash = Text(obj["requestHash"]), Materializing = pending
            };
            if (!Guid.TryParseExact(record.TransactionId, "N", out var id) || id.ToString("N") != record.TransactionId || !IsHash(record.RequestHash)) throw Invalid();
            if (!pending) return record;
            var item = Shape(obj["item"], "id", "slot");
            var payment = Shape(obj["payment"], "pricePaid", "goldBefore", "goldAfter", "goldRevision");
            var inventory = Shape(obj["inventory"], "recordVersion", "ownerKey", "sourceSession", "beforeHash", "afterHash", "before", "after");
            record.ItemId = Text(item["id"]); record.Slot = Text(item["slot"]);
            if (record.Slot != "Sword" && record.Slot != "Shield") throw Invalid();
            record.Price = (int)Number(payment["pricePaid"], int.MaxValue, false);
            record.GoldBefore = (int)Number(payment["goldBefore"], int.MaxValue, false);
            record.GoldAfter = (int)Number(payment["goldAfter"], int.MaxValue, false);
            record.GoldRevision = Number(payment["goldRevision"], long.MaxValue - 1, true);
            record.SourceSession = Text(inventory["sourceSession"]);
            record.Before = InventoryImage.Parse(inventory["before"]); record.After = InventoryImage.Parse(inventory["after"]);
            if (record.GoldBefore < record.Price || record.GoldBefore - record.Price != record.GoldAfter ||
                Text(inventory["recordVersion"]) != "1" || Text(inventory["ownerKey"]) != PlayerInventoryStore.OwnerKey(record.Owner) ||
                Text(inventory["beforeHash"]) != Hash(record.Before.Token().ToString(Formatting.None)) ||
                Text(inventory["afterHash"]) != Hash(record.After.Token().ToString(Formatting.None)) ||
                record.RequestHash != Request(record.Owner, record.Sequence, record.TransactionId, record.ItemId, record.Slot, record.Price)) throw Invalid();
            record.ValidateDelta();
            return record;
        }

        internal static EquipmentPurchaseRecord Create(string owner, long sequence, string id, string item, string slot,
            int price, int goldBefore, int goldAfter, long revision, string sourceSession,
            PlayerInventorySnapshot before, PlayerInventorySnapshot after)
        {
            if (before.Owner != owner || after.Owner != owner) throw Invalid();
            var record = new EquipmentPurchaseRecord { Owner = owner, Sequence = sequence, TransactionId = id,
                RequestHash = Request(owner, sequence, id, item, slot, price), Materializing = true,
                ItemId = item, Slot = slot, Price = price, GoldBefore = goldBefore, GoldAfter = goldAfter,
                GoldRevision = revision, SourceSession = sourceSession, Before = new InventoryImage(before), After = new InventoryImage(after) };
            return Parse(record.Serialize()); // Apply exactly the same bounded schema to newly created records.
        }

        internal EquipmentPurchaseRecord Complete() => new EquipmentPurchaseRecord { Owner = Owner, Sequence = Sequence,
            TransactionId = TransactionId, RequestHash = RequestHash, Materializing = false };

        internal void ValidatePayment(string owner, IDictionary<string, string> data, PlayerDataChanges changes)
        {
            if (Owner != owner) throw Invalid();
            if (!Materializing) return;
            var pending = changes.Snapshot(); var revisions = changes.SnapshotRevisions();
            string after = GoldAfter.ToString(CultureInfo.InvariantCulture);
            if (!data.TryGetValue("Gold", out var gold) || gold != after ||
                !pending.TryGetValue("Gold", out var value) || value != after ||
                !revisions.TryGetValue("Gold", out var revision) || revision != GoldRevision) throw Invalid();
        }

        internal string Serialize()
        {
            var obj = new JObject { ["version"] = "1", ["owner"] = Owner,
                ["sequence"] = Sequence.ToString(CultureInfo.InvariantCulture), ["transactionId"] = TransactionId,
                ["phase"] = Materializing ? "Materializing" : "Complete", ["requestHash"] = RequestHash };
            if (Materializing)
            {
                obj["item"] = new JObject { ["id"] = ItemId, ["slot"] = Slot };
                obj["payment"] = new JObject { ["pricePaid"] = Price.ToString(CultureInfo.InvariantCulture),
                    ["goldBefore"] = GoldBefore.ToString(CultureInfo.InvariantCulture), ["goldAfter"] = GoldAfter.ToString(CultureInfo.InvariantCulture),
                    ["goldRevision"] = GoldRevision.ToString(CultureInfo.InvariantCulture) };
                obj["inventory"] = new JObject { ["recordVersion"] = "1", ["ownerKey"] = PlayerInventoryStore.OwnerKey(Owner),
                    ["sourceSession"] = SourceSession, ["beforeHash"] = Hash(Before.Token().ToString(Formatting.None)),
                    ["afterHash"] = Hash(After.Token().ToString(Formatting.None)), ["before"] = Before.Token(), ["after"] = After.Token() };
            }
            string json = obj.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw Invalid();
            return json;
        }

        private void ValidateDelta()
        {
            if (!Before.Collection.SequenceEqual(After.Collection, StringComparer.Ordinal) || Before.OwnedItems.Contains(ItemId) ||
                !Before.OwnedItems.Concat(new[] { ItemId }).SequenceEqual(After.OwnedItems, StringComparer.Ordinal) ||
                After.Equipped[Slot] != ItemId || Before.Equipped[Slot == "Sword" ? "Shield" : "Sword"] != After.Equipped[Slot == "Sword" ? "Shield" : "Sword"])
                throw Invalid();
        }

        internal static string Request(string owner, long sequence, string id, string item, string slot, int price) =>
            Hash(new JArray(owner, sequence.ToString(CultureInfo.InvariantCulture), id, item, slot,
                price.ToString(CultureInfo.InvariantCulture)).ToString(Formatting.None));

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        private static bool IsHash(string value) => value.Length == 64 && value.All(c => "0123456789abcdef".Contains(c));
        private static string Text(JToken token)
        {
            if (token?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)token)) throw Invalid();
            return (string)token;
        }
        private static long Number(JToken token, long maximum, bool positive)
        {
            string text = Text(token);
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long value) || value < (positive ? 1 : 0) ||
                value > maximum || text != value.ToString(CultureInfo.InvariantCulture)) throw Invalid();
            return value;
        }
        private static JObject Shape(JToken token, params string[] fields)
        {
            if (!(token is JObject obj) || obj.Count != fields.Length || obj.Properties().Any(p => !fields.Contains(p.Name, StringComparer.Ordinal))) throw Invalid();
            return obj;
        }
        private static InvalidDataException Invalid() => new InvalidDataException("Equipment purchase metadata is invalid; original preserved.");
    }
}
