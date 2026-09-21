#if UNITY_EDITOR || TAMER_RECEIPT_HARNESS
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AD;
using AD.Privacy;
using UnityEngine;

public sealed class RevivalDeletionReceiptHarness : MonoBehaviour
{
    public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.receipt";
    private const string ScenarioDirectory = "ReceiptHarnessInventoryV2";
    private const string OldSession = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string NewSession = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private string _status = "Offline receipt key verification";
#if TAMER_RECEIPT_HARNESS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor || Application.identifier != ApplicationId) throw new InvalidOperationException();
        var root = new GameObject("Offline protected receipt verification");
        DontDestroyOnLoad(root); root.AddComponent<RevivalDeletionReceiptHarness>();
    }
#endif
    private async void Start()
    {
        try
        {
            if (Application.isEditor || Application.identifier != ApplicationId) throw new InvalidOperationException();
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                if (activity.Call<int>("checkSelfPermission", "android.permission.INTERNET") == 0)
                    throw new InvalidOperationException("Offline permission check failed.");
#endif
            // A separate scenario directory preserves every file from the original v1 device verification.
            string directory = Path.Combine(Application.persistentDataPath, ScenarioDirectory);
            Directory.CreateDirectory(directory);
            string done = Path.Combine(directory, "complete.txt");
            if (File.Exists(done)) { Mark("COMPLETE_ALREADY_RECORDED"); return; }
            var store = new FileDeletionRecoveryStore(Path.Combine(directory, "pending.json"));
            var keys = new AndroidDeletionReceiptKeys();
            var record = store.Load();
            bool restart = record != null;
            if (!restart) record = new DeletionRecovery { Origin = "https://example.invalid/", Title = "TEST1",
                ClientKey = Guid.NewGuid().ToString("N"), RequestId = "synthetic-receipt", Revision = "v1",
                OwnerHash = DeletionRecovery.Hash("https://example.invalid/", "TEST1", "synthetic-account"),
                Binding = DeletionRecovery.Hash("https://example.invalid/", "TEST1", "synthetic-account", "synthetic-entity"),
                InventoryOwnerKey = PlayerInventoryStore.OwnerKey("synthetic-account"), InventorySession = OldSession };
            string inventoryDirectory = Path.Combine(directory, "inventory");
            var inventory = new PlayerInventoryStore(inventoryDirectory, new[] { "Bat" },
                new Dictionary<string, string> { { "SimpleSword", "Sword" } });
            var gateway = new FixedGateway(record, Path.Combine(directory, "server-verifier.txt"));
            var client = new DeletionReceiptClient(gateway, keys, store);
            if (!restart)
            {
                if (Directory.GetFileSystemEntries(directory).Length != 0)
                    throw new InvalidOperationException("Existing scenario files are preserved; do not reseed.");
                inventory.BindSession("synthetic-account", OldSession);
                inventory.Save("synthetic-account", new[] { "Bat" }, new[] { "SimpleSword" },
                    new Dictionary<string, string> { { "Sword", "SimpleSword" }, { "Shield", null } });
                inventory.BindSession("synthetic-other", OldSession);
                inventory.BindSession("synthetic-new-session", OldSession);
                inventory.BindSession("synthetic-new-session", NewSession);
                File.WriteAllText(Path.Combine(directory, "preserved-other.txt"), File.ReadAllText(inventory.PathFor("synthetic-other")));
                File.WriteAllText(Path.Combine(directory, "preserved-new-session.txt"), File.ReadAllText(inventory.PathFor("synthetic-new-session")));
                await client.RegisterAsync(new DeletionAuthorization("synthetic-account", "synthetic-proof"), record, CancellationToken.None);
                record.SubmissionStarted = true; store.Save(record);
                Mark("PHASE1_V2_REGISTERED_NONEXPORTABLE_KEY inventory-fixtures-ready=true restart-required internet=false");
                return; // Simulated accepted response loss. No ticket/proof/capability is persisted.
            }
            var receipt = await client.ReadAsync(record, CancellationToken.None);
            if (receipt.State != DeletionState.Accepted) throw new InvalidOperationException();
            bool staleRejected = false, foreignRejected = false;
            try { PlayerInventoryStore.DeleteBound(inventoryDirectory, PlayerInventoryStore.OwnerKey("synthetic-new-session"), OldSession, owner => owner == "synthetic-new-session"); }
            catch (InvalidDataException) { staleRejected = true; }
            try { PlayerInventoryStore.DeleteBound(inventoryDirectory, PlayerInventoryStore.OwnerKey("synthetic-other"), OldSession, owner => owner == "synthetic-account"); }
            catch (InvalidDataException) { foreignRejected = true; }
            if (!staleRejected || !foreignRejected) throw new InvalidOperationException("Inventory preservation check failed.");
            await client.FinishAsync(record, () => PlayerInventoryStore.DeleteBound(inventoryDirectory,
                record.InventoryOwnerKey, record.InventorySession,
                owner => DeletionRecovery.Hash(record.Origin, record.Title, owner) == record.OwnerHash), CancellationToken.None);
            if (File.Exists(inventory.PathFor("synthetic-account")) ||
                File.ReadAllText(inventory.PathFor("synthetic-other")) != File.ReadAllText(Path.Combine(directory, "preserved-other.txt")) ||
                File.ReadAllText(inventory.PathFor("synthetic-new-session")) != File.ReadAllText(Path.Combine(directory, "preserved-new-session.txt")))
                throw new InvalidOperationException("Inventory result mismatch.");
            bool removed;
            try { keys.Sign(record.KeyAlias, DeletionReceiptClient.AccessMessage(record)); removed = false; }
            catch (Exception) { removed = true; }
            if (!removed || store.Load() != null || gateway.Registers != 0 || gateway.Reads != 1 || gateway.Acks != 1)
                throw new InvalidOperationException();
            File.WriteAllText(done, "v2-accepted-inventory-removed-other-and-new-session-preserved");
            Mark("PHASE2_V2_ACCEPTED_AFTER_PROCESS_RESTART inventory-removed=true other-owner-preserved=true new-session-preserved=true key-removed=true reads=1 new-registrations=0 server-deletes=0 internet=false");
        }
        catch (Exception error) { Mark("FAIL type=" + error.GetType().Name); }
    }
    private void Mark(string value) { _status = value; Debug.Log("RECEIPT_HARNESS " + value); }
    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 900f, Screen.width / 900f, 1));
        GUI.Label(new Rect(20, 20, 860, 500), "OFFLINE SYNTHETIC RECEIPT TEST\n" + _status);
    }
    private sealed class FixedGateway : IDeletionReceiptGateway
    {
        private readonly DeletionRecovery _record;
        private readonly string _verifierPath;
        public int Registers, Reads, Acks;
        public FixedGateway(DeletionRecovery record, string path) { _record = record; _verifierPath = path; }
        public Task<DeletionReceipt> RegisterReceiptAsync(DeletionAuthorization auth, string id, string verifier, CancellationToken token)
        { Registers++; File.WriteAllText(_verifierPath, verifier); return Task.FromResult(Reply(false)); }
        public Task<DeletionReceipt> ReadReceiptAsync(string id, string capability, CancellationToken token)
        { Reads++; Verify(id, capability); return Task.FromResult(Reply(true)); }
        public Task AcknowledgeReceiptAsync(string id, string capability, CancellationToken token)
        { Acks++; Verify(id, capability); return Task.CompletedTask; }
        private void Verify(string id, string capability)
        {
            if (id != _record.RequestId || DeletionReceiptClient.Verifier(capability) != File.ReadAllText(_verifierPath))
                throw new InvalidOperationException();
        }
        private DeletionReceipt Reply(bool accepted) => new DeletionReceipt { requestId = _record.RequestId, clientKey = _record.ClientKey,
            ownerHash = _record.OwnerHash, binding = _record.Binding, policyRevision = _record.Revision, scope = "title",
            expiresAt = 2000000000, state = accepted ? "processing" : "awaiting_confirmation",
            submissionState = accepted ? "accepted" : "not_submitted" };
    }
}
#endif
