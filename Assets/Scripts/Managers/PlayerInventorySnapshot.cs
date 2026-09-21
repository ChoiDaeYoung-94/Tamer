using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace AD
{
    /// <summary>
    /// Validated, detached inventory values. This is not an ownership proof, persistence format,
    /// acquisition authority, or cloud transport. The caller must supply an authenticated account.
    /// </summary>
    public sealed class PlayerInventorySnapshot
    {
        public string Owner { get; }
        public ReadOnlyCollection<string> Collection { get; }
        public ReadOnlyCollection<string> OwnedItems { get; }
        public ReadOnlyDictionary<string, string> Equipped { get; }

        private PlayerInventorySnapshot(string owner, List<string> collection, List<string> owned,
            Dictionary<string, string> equipped)
        {
            Owner = owner;
            Collection = collection.AsReadOnly();
            OwnedItems = owned.AsReadOnly();
            Equipped = new ReadOnlyDictionary<string, string>(equipped);
        }

        public static PlayerInventorySnapshot Validate(string authenticatedAccount, string storedOwner,
            IEnumerable<string> collection, IEnumerable<string> ownedItems,
            IDictionary<string, string> equipped, IEnumerable<string> knownMonsters,
            IDictionary<string, string> itemSlots)
        {
            // An ownerless legacy record must never become the current account's record here.
            if (string.IsNullOrWhiteSpace(authenticatedAccount) ||
                !string.Equals(authenticatedAccount, storedOwner, StringComparison.Ordinal))
                throw new InvalidDataException("Inventory requires the authenticated account owner.");
            if (knownMonsters == null || itemSlots == null)
                throw new ArgumentNullException("Inventory catalogs are required.");

            var monsters = new HashSet<string>(knownMonsters, StringComparer.Ordinal);
            var items = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in itemSlots)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || !IsSlot(item.Value))
                    throw new InvalidDataException("Invalid inventory catalog.");
                items.Add(item.Key, item.Value);
            }
            var collected = ValidateNames(collection, monsters);
            var owned = ValidateNames(ownedItems, new HashSet<string>(items.Keys, StringComparer.Ordinal));
            var ownedSet = new HashSet<string>(owned, StringComparer.Ordinal);
            if (equipped == null || equipped.Count != 2)
                throw new InvalidDataException("Inventory requires both equipment slots.");
            var slots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in equipped)
            {
                if (!IsSlot(entry.Key) || (entry.Value != null &&
                    (!ownedSet.Contains(entry.Value) || !items.TryGetValue(entry.Value, out var slot) || slot != entry.Key)))
                    throw new InvalidDataException("Equipment must be owned and match its slot.");
                slots.Add(entry.Key, entry.Value);
            }
            return new PlayerInventorySnapshot(storedOwner, collected, owned, slots);
        }

        private static List<string> ValidateNames(IEnumerable<string> values, HashSet<string> known)
        {
            if (values == null) throw new InvalidDataException("Inventory list is missing.");
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !known.Contains(value) || !seen.Add(value))
                    throw new InvalidDataException("Unknown or duplicate inventory entry.");
                result.Add(value);
            }
            return result;
        }

        private static bool IsSlot(string slot) => slot == "Sword" || slot == "Shield";
    }
}
