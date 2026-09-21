using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AD.Privacy
{
    public interface IDeletionReceiptKeys
    {
        void Create(string alias);
        string Sign(string alias, string message);
        void Delete(string alias);
    }

    [DataContract]
    public sealed class DeletionReceipt
    {
        [DataMember(IsRequired = true)] public string requestId;
        [DataMember(IsRequired = true)] public string clientKey;
        [DataMember(IsRequired = true)] public string ownerHash;
        [DataMember(IsRequired = true)] public string binding;
        [DataMember(IsRequired = true)] public double expiresAt;
        [DataMember(IsRequired = true)] public string policyRevision;
        [DataMember(IsRequired = true)] public string scope;
        [DataMember(IsRequired = true)] public string state;
        [DataMember(IsRequired = true)] public string submissionState;

        public DeletionSnapshot Validate(DeletionRecovery record, bool registration = false)
        {
            if (requestId != record.RequestId || clientKey != record.ClientKey || ownerHash != record.OwnerHash ||
                binding != record.Binding || policyRevision != record.Revision || scope != "title" ||
                double.IsNaN(expiresAt) || double.IsInfinity(expiresAt) || expiresAt <= 0 ||
                !registration && expiresAt != record.ReceiptExpires)
                throw new InvalidOperationException("Receipt does not match this intent.");
            DeletionState result;
            if (state == "processing" && submissionState == "accepted") result = DeletionState.Accepted;
            else if (state == "processing" && submissionState == "submission_unknown") result = DeletionState.SubmissionUnknown;
            else if (state == "cancelled" && submissionState == "not_submitted") result = DeletionState.Cancelled;
            else if (state == "awaiting_confirmation" && submissionState == "not_submitted") result = DeletionState.AwaitingConfirmation;
            else if (state == "queued" && (submissionState == "not_submitted" || submissionState == "ready")) result = DeletionState.Queued;
            else if (state == "processing" && submissionState == "ready") result = DeletionState.Processing;
            else throw new InvalidOperationException("Receipt state is unavailable.");
            return new DeletionSnapshot(requestId, policyRevision, scope, result);
        }
    }

    public interface IDeletionReceiptGateway
    {
        Task<DeletionReceipt> RegisterReceiptAsync(DeletionAuthorization authorization, string requestId, string verifier, CancellationToken token);
        Task<DeletionReceipt> ReadReceiptAsync(string requestId, string capability, CancellationToken token);
        Task AcknowledgeReceiptAsync(string requestId, string capability, CancellationToken token);
    }

    public sealed class DeletionReceiptClient
    {
        private readonly IDeletionReceiptGateway _gateway;
        private readonly IDeletionReceiptKeys _keys;
        private readonly IDeletionRecoveryStore _store;
        public DeletionReceiptClient(IDeletionReceiptGateway gateway, IDeletionReceiptKeys keys, IDeletionRecoveryStore store)
        { _gateway = gateway; _keys = keys; _store = store; }

        public static string AccessMessage(DeletionRecovery r) => r.InventoryOwnerKey == null
            ? DeletionRecovery.Hash("tamer.deletion.receipt.access.v1", r.Origin, r.Title, r.OwnerHash, r.Binding, r.ClientKey, r.RequestId, r.Revision)
            : DeletionRecovery.Hash("tamer.deletion.receipt.access.v2", r.Origin, r.Title, r.OwnerHash, r.Binding, r.ClientKey,
                r.RequestId, r.Revision, r.InventoryOwnerKey, r.InventorySession);
        private static string SealMessage(DeletionRecovery r) => DeletionRecovery.Hash("tamer.deletion.receipt.local-terminal.v1",
            AccessMessage(r), r.TerminalState, r.ReceiptExpires.ToString("R", CultureInfo.InvariantCulture), r.CleanupApplied ? "applied" : "pending");
        public static string Verifier(string capability)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.ASCII.GetBytes(capability)))
                .Replace("-", "").ToLowerInvariant();
        }

        public async Task RegisterAsync(DeletionAuthorization auth, DeletionRecovery record, CancellationToken token)
        {
            if (record.SubmissionStarted) throw new InvalidOperationException();
            if (record.KeyAlias == null) { record.KeyAlias = "tamer.deletion.receipt." + record.ClientKey; _store.Save(record); }
            if (!record.KeyCreated)
            {
                _keys.Create(record.KeyAlias);
                record.KeyCreated = true;
                _store.Save(record); // Must precede registration: a missing key after this point is never recreated.
            }
            string capability = _keys.Sign(record.KeyAlias, AccessMessage(record));
            var response = await _gateway.RegisterReceiptAsync(auth, record.RequestId, Verifier(capability), token);
            response.Validate(record, registration: true);
            record.ReceiptExpires = response.expiresAt;
            record.ReceiptRegistered = true;
            _store.Save(record);
            var durable = _store.Load();
            if (durable == null || !durable.ReceiptRegistered || AccessMessage(durable) != AccessMessage(record) ||
                durable.KeyAlias != record.KeyAlias || durable.ReceiptExpires != record.ReceiptExpires ||
                _keys.Sign(durable.KeyAlias, AccessMessage(durable)) != capability)
                throw new InvalidOperationException("Durable receipt recovery is required before submission.");
        }

        public async Task<DeletionSnapshot> ReadAsync(DeletionRecovery record, CancellationToken token)
        {
            record.Validate();
            if (!record.ReceiptRegistered) throw new InvalidOperationException();
            if (record.TerminalState != null)
            {
                if (_keys.Sign(record.KeyAlias, SealMessage(record)) != record.TerminalSeal)
                    throw new InvalidOperationException("Local receipt is unavailable.");
                return new DeletionSnapshot(record.RequestId, record.Revision, "title",
                    record.TerminalState == "accepted" ? DeletionState.Accepted : DeletionState.Cancelled);
            }
            var receipt = await _gateway.ReadReceiptAsync(record.RequestId, _keys.Sign(record.KeyAlias, AccessMessage(record)), token);
            var snapshot = receipt.Validate(record);
            if (snapshot.State == DeletionState.Accepted || snapshot.State == DeletionState.Cancelled)
            {
                RememberTerminal(record, snapshot.State);
            }
            return snapshot;
        }

        public void RememberTerminal(DeletionRecovery record, DeletionState state)
        {
            if (!record.ReceiptRegistered || state != DeletionState.Accepted && state != DeletionState.Cancelled)
                throw new InvalidOperationException();
            record.TerminalState = state == DeletionState.Accepted ? "accepted" : "cancelled";
            record.TerminalSeal = _keys.Sign(record.KeyAlias, SealMessage(record));
            _store.Save(record); // Authenticated terminal evidence is durable before cleanup or acknowledgement.
        }

        public async Task FinishAsync(DeletionRecovery record, Action applyLocal, CancellationToken token)
        {
            if (record.TerminalState == null || _keys.Sign(record.KeyAlias, SealMessage(record)) != record.TerminalSeal)
                throw new InvalidOperationException();
            if (!record.CleanupApplied)
            {
                applyLocal();
                record.CleanupApplied = true;
                record.TerminalSeal = _keys.Sign(record.KeyAlias, SealMessage(record));
                _store.Save(record);
            }
            // Ack response loss is recoverable from the sealed terminal record and idempotent server acknowledgement.
            try { await _gateway.AcknowledgeReceiptAsync(record.RequestId, _keys.Sign(record.KeyAlias, AccessMessage(record)), token); }
            catch (Exception) { /* A sealed, applied terminal receipt does not become unknown when its ack is lost/expired. */ }
            _store.Clear(); // Never delete a key while its only remaining recovery record still needs it.
            _keys.Delete(record.KeyAlias); // Alias is unique to this intent; never enumerate/delete other keys.
        }
    }
}
