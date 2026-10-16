using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AD
{
    internal enum EquipmentPurchaseResult
    {
        Rejected, CommittedPendingMaterialization, Completed, AlreadyCommittedCompleted, RecoveryRequired
    }

    public partial class DataManager
    {
        private EquipmentPurchaseRecord _equipmentRecord;
        private bool _equipmentReadBlocked;
        private bool _equipmentReloadRequired;
        private object _equipmentWriter;
        private bool EquipmentBlocksWrites => _equipmentReadBlocked || _equipmentReloadRequired ||
            _equipmentWriter != null || (_equipmentRecord != null && _equipmentRecord.Materializing);

        internal sealed class EquipmentPurchaseIntent
        {
            internal readonly DataManager Owner;
            internal readonly string Account, Session, Item, Slot, Id, RequestHash;
            internal readonly int Generation, DeletionEpoch, Price;
            internal readonly long Sequence;
            internal bool Attempted;
            internal EquipmentPurchaseIntent(DataManager owner, string item, string slot, int price, long sequence)
            {
                Owner = owner; Account = owner.PlayFabId; Generation = owner._accountGeneration;
                DeletionEpoch = owner.DeletionEpoch; Session = owner._inventorySession;
                Item = item; Slot = slot; Price = price; Sequence = sequence; Id = Guid.NewGuid().ToString("N");
                RequestHash = EquipmentPurchaseRecord.Request(Account, Sequence, Id, Item, Slot, Price);
            }
        }

        private static Dictionary<string, string> EquipmentSlots() => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SimpleSword"] = "Sword", ["MasterSword"] = "Sword", ["SimpleShield"] = "Shield", ["MasterShield"] = "Shield"
        };

        private void EnsureInventoryStore()
        {
            if (_inventoryStore != null) return;
            var slots = EquipmentSlots();
            if (ItemData == null || MonsterData == null || slots.Keys.Any(key => !ItemData.ContainsKey(key)))
                throw new InvalidDataException("Inventory catalogs are unavailable.");
            _inventoryStore = new PlayerInventoryStore(_playerDataPath + ".inventory", MonsterData.Keys, slots);
        }

        // No Player condition here: normal first server reads and No Ads persistence need no Player.
        private bool AuthenticatedEquipmentOwner(string account, int generation, int deletionEpoch) =>
            !_shutdown && !_equipmentReadBlocked && !DeletionInProgress && !_deletionSignedOut &&
            PlayFabId == account && _localOwner == account && !string.IsNullOrEmpty(account) &&
            _accountGeneration == generation && DeletionEpoch == deletionEpoch;

        private void BindInventoryForCurrentSession()
        {
            if (EquipmentBlocksWrites || _allyPurchaseWriting || _allyPurchaseReloadRequired)
                throw new InvalidOperationException("Save recovery must finish before inventory binding.");
            EnsureInventoryStore();
            string account = PlayFabId, session = _inventorySession;
            int generation = _accountGeneration, epoch = DeletionEpoch;
            _inventoryStore.BindSessionCurrent(account, session, () => !EquipmentBlocksWrites && !_allyPurchaseWriting &&
                !_allyPurchaseReloadRequired && _inventorySession == session && AuthenticatedEquipmentOwner(account, generation, epoch));
            _inventoryBoundSession = session;
        }

        internal EquipmentPurchaseIntent PrepareEquipmentPurchase(string item, int price)
        {
            if (!CanPurchaseAlly() || !TryEquipmentPrice(item, price, out var slot)) return null;
            long previous = _equipmentRecord?.Sequence ?? 0;
            if (previous >= long.MaxValue - 1) return null;
            var inventory = ReadInventory();
            var envelope = _inventoryStore.ReadEnvelope(PlayFabId);
            if (envelope == null || envelope.Session != _inventorySession || inventory.OwnedItems.Contains(item)) return null;
            return new EquipmentPurchaseIntent(this, item, slot, price, previous + 1);
        }

        private bool TryEquipmentPrice(string item, int price, out string slot)
        {
            slot = null;
            return !string.IsNullOrEmpty(item) && price >= 0 && EquipmentSlots().TryGetValue(item, out slot) &&
                ItemData != null && ItemData.TryGetValue(item, out var raw) && raw is Dictionary<string, object> catalog &&
                catalog.TryGetValue("Price", out var value) &&
                int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int current) && current == price;
        }

        private bool MatchesEquipmentIntent(EquipmentPurchaseIntent intent) => _equipmentRecord != null &&
            _equipmentRecord.Owner == intent.Account && _equipmentRecord.Sequence == intent.Sequence &&
            _equipmentRecord.TransactionId == intent.Id && _equipmentRecord.RequestHash == intent.RequestHash;

        internal EquipmentPurchaseResult TryCommitEquipmentPurchase(EquipmentPurchaseIntent intent, int expectedGold, Func<bool> currentPlayer)
        {
            if (intent == null || !ReferenceEquals(intent.Owner, this)) return EquipmentPurchaseResult.Rejected;
            // A durable but unfinished duplicate never opens the success/presentation path.
            if (MatchesEquipmentIntent(intent) && _equipmentRecord.Materializing)
                return EquipmentPurchaseResult.CommittedPendingMaterialization;
            bool current = AuthenticatedEquipmentOwner(intent.Account, intent.Generation, intent.DeletionEpoch) &&
                _inventorySession == intent.Session && currentPlayer != null && currentPlayer();
            if (!current) return EquipmentPurchaseResult.Rejected;
            if (MatchesEquipmentIntent(intent) && !_equipmentRecord.Materializing && !EquipmentBlocksWrites && IsServerDataReady)
                return EquipmentPurchaseResult.AlreadyCommittedCompleted;
            if (intent.Attempted) return EquipmentBlocksWrites ? EquipmentPurchaseResult.RecoveryRequired : EquipmentPurchaseResult.Rejected;
            intent.Attempted = true;
            if (!CanPurchaseAlly() || !TryEquipmentPrice(intent.Item, intent.Price, out var slot) || slot != intent.Slot ||
                intent.Sequence != (_equipmentRecord?.Sequence ?? 0) + 1 ||
                !LocalPlayerData.TryGetValue("Gold", out var rawGold) ||
                !int.TryParse(rawGold, NumberStyles.Integer, CultureInfo.InvariantCulture, out int gold) ||
                gold != expectedGold || gold < intent.Price) return EquipmentPurchaseResult.Rejected;

            object token = new object();
            _equipmentWriter = token;
            Func<bool> fence = () => ReferenceEquals(_equipmentWriter, token) && !_equipmentReloadRequired &&
                IsServerDataReady && !_allyPurchaseWriting && !_allyPurchaseReloadRequired &&
                AuthenticatedEquipmentOwner(intent.Account, intent.Generation, intent.DeletionEpoch) &&
                _inventorySession == intent.Session && currentPlayer();
            try
            {
                EnsureInventoryStore();
                var source = _inventoryStore.ReadEnvelope(intent.Account);
                if (source == null || source.Session != intent.Session || source.Snapshot.OwnedItems.Contains(intent.Item))
                    throw new InvalidDataException("Equipment purchase source changed.");
                var equipped = new Dictionary<string, string>(source.Snapshot.Equipped) { [intent.Slot] = intent.Item };
                var after = PlayerInventorySnapshot.Validate(intent.Account, intent.Account, source.Snapshot.Collection,
                    source.Snapshot.OwnedItems.Concat(new[] { intent.Item }), equipped, MonsterData.Keys, EquipmentSlots());
                var local = new Dictionary<string, string>(LocalPlayerData) { ["Gold"] = (gold - intent.Price).ToString(CultureInfo.InvariantCulture) };
                var changes = _changes.Clone();
                changes.Track("Gold", local["Gold"]);
                var pending = EquipmentPurchaseRecord.Create(intent.Account, intent.Sequence, intent.Id, intent.Item,
                    intent.Slot, intent.Price, gold, gold - intent.Price, changes.SnapshotRevisions()["Gold"], source.Session, source.Snapshot, after);
                WritePlayerData(local, _localOwner, changes, fence, pending); // P: unique purchase commit.
                if (!fence()) throw new InvalidOperationException("Equipment purchase session changed.");
                LocalPlayerData = local; _changes = changes; _equipmentRecord = pending;
                MaterializeEquipment(pending, fence); // I, then P Complete. Never recharges or Tracks.
                return EquipmentPurchaseResult.Completed;
            }
            catch (Exception)
            {
                StopEquipmentSession(intent.Account, null);
                return EquipmentPurchaseResult.RecoveryRequired;
            }
            finally { if (ReferenceEquals(_equipmentWriter, token)) _equipmentWriter = null; }
        }

        private void MaterializeEquipment(EquipmentPurchaseRecord record, Func<bool> fence)
        {
            record.ValidatePayment(_localOwner, LocalPlayerData, _changes);
            EnsureInventoryStore();
            _inventoryStore.ApplyCommittedPurchase(record, fence);
            if (!fence()) throw new InvalidOperationException("Equipment recovery session changed.");
            var completed = record.Complete();
            WritePlayerData(LocalPlayerData, _localOwner, _changes, fence, completed);
            if (!fence()) throw new InvalidOperationException("Equipment completion session changed.");
            _equipmentRecord = completed;
        }

        // Called after successful authentication and deletion guard, before static ticket adoption,
        // first server merge, inventory rebind or Player creation. Public ready must remain false.
        private void RecoverEquipmentPurchase()
        {
            if (_equipmentReadBlocked) throw new InvalidDataException("Save recovery is required.");
            if (_equipmentRecord == null) { _equipmentReloadRequired = false; return; }
            if (!_equipmentRecord.Materializing)
            {
                // A completed purchase proves an inventory file existed. Missing I is not a first install.
                try
                {
                    EnsureInventoryStore();
                    if (_inventoryStore.ReadEnvelope(PlayFabId) == null) throw new InvalidDataException();
                    _equipmentReloadRequired = false;
                }
                catch (Exception)
                {
                    StopEquipmentSession(PlayFabId, null);
                    throw new InvalidDataException("Completed purchase inventory is unavailable; progress preserved.");
                }
                return;
            }
            string account = PlayFabId;
            int generation = _accountGeneration, epoch = DeletionEpoch;
            object token = new object(); _equipmentWriter = token;
            Func<bool> fence = () => ReferenceEquals(_equipmentWriter, token) && ReferenceEquals(Managers.DataM, this) &&
                !_allyPurchaseWriting && AuthenticatedEquipmentOwner(account, generation, epoch);
            try
            {
                if (!fence()) throw new InvalidOperationException("Equipment recovery owner changed.");
                MaterializeEquipment(_equipmentRecord, fence);
                _equipmentReloadRequired = false;
            }
            catch (Exception)
            {
                StopEquipmentSession(account, null);
                throw new InvalidDataException("Equipment recovery is incomplete; originals preserved.");
            }
            finally { if (ReferenceEquals(_equipmentWriter, token)) _equipmentWriter = null; }
        }

        internal void StopEquipmentPresentation(EquipmentPurchaseIntent intent)
        {
            if (intent != null && ReferenceEquals(intent.Owner, this) && _accountGeneration == intent.Generation &&
                PlayFabId == intent.Account && DeletionEpoch == intent.DeletionEpoch)
                StopEquipmentSession(intent.Account, intent.Generation);
        }

        private void StopEquipmentSession(string account, int? generation)
        {
            if (_localOwner != account || (generation.HasValue && _accountGeneration != generation.Value)) return;
            _equipmentReloadRequired = true; IsServerDataReady = false;
            _server?.CancelPendingRequests();
            try { SuspendAccountSession(); }
            catch (Exception) { UnityEngine.Debug.LogWarning("[Tamer/Data] Equipment save preserved; restart to recover."); }
        }

        private void ClearEquipmentState()
        {
            _equipmentRecord = null; _equipmentReadBlocked = false; _equipmentReloadRequired = false;
        }
    }
}
