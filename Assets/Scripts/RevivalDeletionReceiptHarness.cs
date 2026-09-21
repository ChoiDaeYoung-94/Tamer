#if UNITY_EDITOR || TAMER_RECEIPT_HARNESS
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AD;
using AD.Privacy;
using UnityEngine;

public sealed class RevivalDeletionReceiptHarness : MonoBehaviour
{
    public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.receipt";
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
            string directory = Path.Combine(Application.persistentDataPath, "ReceiptHarness");
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
                Binding = DeletionRecovery.Hash("https://example.invalid/", "TEST1", "synthetic-account", "synthetic-entity") };
            var gateway = new FixedGateway(record, Path.Combine(directory, "server-verifier.txt"));
            var client = new DeletionReceiptClient(gateway, keys, store);
            if (!restart)
            {
                await client.RegisterAsync(new DeletionAuthorization("synthetic-account", "synthetic-proof"), record, CancellationToken.None);
                record.SubmissionStarted = true; store.Save(record);
                Mark("PHASE1_REGISTERED_NONEXPORTABLE_KEY restart-required internet=false");
                return; // Simulated accepted response loss. No ticket/proof/capability is persisted.
            }
            var receipt = await client.ReadAsync(record, CancellationToken.None);
            if (receipt.State != DeletionState.Accepted) throw new InvalidOperationException();
            await client.FinishAsync(record, () => File.WriteAllText(done, "accepted-local-fixture-applied"), CancellationToken.None);
            bool removed;
            try { keys.Sign(record.KeyAlias, DeletionReceiptClient.AccessMessage(record)); removed = false; }
            catch (Exception) { removed = true; }
            if (!removed || store.Load() != null || gateway.Registers != 0 || gateway.Reads != 1 || gateway.Acks != 1)
                throw new InvalidOperationException();
            Mark("PHASE2_ACCEPTED_AFTER_PROCESS_RESTART key-removed=true reads=1 new-registrations=0 deletes=0 internet=false");
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
