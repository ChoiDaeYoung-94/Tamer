using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AD.Purchasing;
using NUnit.Framework;
using UnityEngine;

public class RevivalIapAccountRecoveryTests
{
    private string _root;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD." + name)).First(t => t != null);
    private static object Call(Type type, object target, string method, params object[] args)
    {
        try { return type.GetMethod(method, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    private static object Property(object target, string name) => target.GetType().GetProperty(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    [SetUp] public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "RevivalIapRecovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }
    [TearDown] public void Teardown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true); // This unique synthetic test root only.
    }
    private string Prepare(string account, string slot) => (string)Call(Runtime("RevivalIapRecoveryProfile"), null,
        "Prepare", _root, "12B656", account, slot);

    [Test]
    public void Revival_Recovery_PreparePreservesOriginalBytesAndRejectsReuseOrForeignOwner()
    {
        string original = Path.Combine(_root, "IapTest", "12B656");
        Directory.CreateDirectory(Path.Combine(original, "PlayerDataBackups"));
        Directory.CreateDirectory(Path.Combine(original, "PlayerData.json.inventory"));
        var evidence = new Dictionary<string, byte[]>
        {
            [Path.Combine(original, "identity.txt")] = new byte[] { 1, 2, 3, 10 },
            [Path.Combine(original, "PlayerData.json")] = System.Text.Encoding.UTF8.GetBytes("synthetic-owner-B/journal/NoAds"),
            [Path.Combine(original, "PlayerDataBackups", "synthetic.json")] = new byte[] { 4, 5 },
            [Path.Combine(original, "PlayerData.json.inventory", "synthetic.json")] = new byte[] { 6, 7 }
        };
        foreach (var entry in evidence) File.WriteAllBytes(entry.Key, entry.Value);
        string slot = Guid.NewGuid().ToString("N"), path = Prepare("synthetic-A", slot);
        Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(path)), Is.Empty);
        Assert.Throws<IOException>(() => Prepare("synthetic-A", slot));
        Assert.Throws<InvalidDataException>(() => Call(Runtime("RevivalIapRecoveryProfile"), null,
            "ValidatePrepared", _root, "12B656", "synthetic-B", path));
        Call(Runtime("RevivalIapRecoveryProfile"), null, "ValidatePrepared", _root, "12B656", "synthetic-A", path);
        File.WriteAllText(path, "synthetic-existing-profile");
        Assert.Throws<InvalidDataException>(() => Call(Runtime("RevivalIapRecoveryProfile"), null,
            "ValidatePrepared", _root, "12B656", "synthetic-A", path));
        foreach (var entry in evidence) Assert.That(File.ReadAllBytes(entry.Key), Is.EqualTo(entry.Value));
        Assert.That(File.ReadAllText(path), Is.EqualTo("synthetic-existing-profile"));
    }

    [Test]
    public void Revival_Recovery_InterruptedReservationAndDeletionEvidenceBlockWithoutDeletion()
    {
        string slot = Guid.NewGuid().ToString("N"), path = Prepare("synthetic-A", slot);
        string directory = Path.GetDirectoryName(path), reservation = directory + ".reserved";
        Directory.Delete(directory); // Synthetic interruption BEFORE any profile data was written.
        Assert.Throws<IOException>(() => Prepare("synthetic-A", slot));
        Assert.That(File.Exists(reservation), Is.True);
        string deletion = Path.Combine(_root, "DeletionRecovery"); Directory.CreateDirectory(deletion);
        File.WriteAllText(Path.Combine(deletion, "unreadable.json"), "synthetic-unresolved");
        Assert.Throws<InvalidDataException>(() => Call(Runtime("RevivalIapRecoveryProfile"), null, "CheckNoDeletionRecovery", _root));
        Assert.That(File.ReadAllText(Path.Combine(deletion, "unreadable.json")), Is.EqualTo("synthetic-unresolved"));
    }

    [TestCase("synthetic-other", false)]
    [TestCase("synthetic-A", true)]
    public void Revival_Recovery_StagingRejectsForeignOrNewAccountAndLateCallbacks(string returned, bool created)
    {
        var type = Runtime("RevivalIapRecoveryStaging");
        var stage = Activator.CreateInstance(type, new object[] { "synthetic-A" });
        Assert.That(Call(type, stage, "AcceptLogin", returned, "synthetic-ticket", created), Is.False);
        Assert.That(Property(stage, "Session"), Is.Null);
        Assert.That(Call(type, stage, "AcceptLogin", "synthetic-A", "synthetic-ticket", false), Is.False);
    }

    [Test]
    public void Revival_Recovery_StagingRequiresSameSessionInventoryAndRejectsCancelOrDuplicate()
    {
        var type = Runtime("RevivalIapRecoveryStaging");
        var stage = Activator.CreateInstance(type, new object[] { "synthetic-A" });
        Assert.That(Call(type, stage, "AcceptLogin", "synthetic-A", "synthetic-ticket", false), Is.True);
        var session = (ReceiptSession)Property(stage, "Session");
        Assert.That(Call(type, stage, "AcceptInventory", new ReceiptSession("synthetic-A", "stale-ticket"), true), Is.False);
        Assert.That(Call(type, stage, "AcceptInventory", session, true), Is.True);
        Assert.That(Call(type, stage, "AcceptInventory", session, true), Is.False);
        Call(type, stage, "Cancel");
        Assert.That(Property(stage, "Session"), Is.Null);
        Assert.That(Call(type, stage, "AcceptInventory", session, true), Is.False);
        var missing = Activator.CreateInstance(type, new object[] { "synthetic-A" });
        Call(type, missing, "AcceptLogin", "synthetic-A", "synthetic-ticket", false);
        Assert.That(Call(type, missing, "AcceptInventory", new ReceiptSession("synthetic-A", "synthetic-ticket"), false), Is.False);
        Assert.That(Property(missing, "Session"), Is.Null);
    }

    [Test]
    public void Revival_Recovery_GateRequiresApprovalAndSurvivesManagerOrAppReplacement()
    {
        var type = Runtime("RevivalIapRecoveryGate");
        var gate = (IIapExecutionGate)Activator.CreateInstance(type);
        Assert.That(gate.CanAccessStore || gate.AllowPurchases || gate.AllowAutomaticRetry || gate.TryBeginVerification(), Is.False);
        Assert.That(Call(type, gate, "Open", _root, false, true), Is.False);
        Assert.That(Call(type, gate, "Open", _root, true, false), Is.False);
        Assert.That(Call(type, gate, "Open", _root, true, true), Is.True);
        // Crash after reservation, before receipt verification: a new gate still cannot open.
        var restarted = (IIapExecutionGate)Activator.CreateInstance(type);
        Assert.That(Call(type, restarted, "Open", _root, true, true), Is.False);
        Assert.That(gate.TryBeginVerification(), Is.True);
        Assert.That(gate.TryBeginVerification() || gate.CanAccessStore, Is.False);
        gate.Close();
        Assert.That(Call(type, gate, "Open", _root, true, true), Is.False);
        Assert.That(File.Exists(Path.Combine(_root, "recovery-attempt-v1.lock")), Is.True);
    }

    [Test]
    public void Revival_Recovery_RealFreshDataRejectsForeignOwnerAndStartsWithEmptyEntitlementJournal()
    {
        string path = Prepare("synthetic-A", Guid.NewGuid().ToString("N"));
        var dataType = Runtime("DataManager");
        var serverType = Runtime("ServerManager");
        var go = new GameObject("Synthetic IAP recovery data");
        object server = null;
        try
        {
            var data = go.AddComponent(dataType);
            server = Activator.CreateInstance(serverType, new object[] { data }); // No request is issued.
            Call(dataType, data, "InitializeIapRecovery", path, server, _root);
            var local = (Dictionary<string, string>)dataType.GetField("LocalPlayerData").GetValue(data);
            Assert.That(local["Gold"], Is.EqualTo("0"));
            Assert.That(local["GooglePlay"], Is.Empty);
            Assert.That(local.ContainsKey("__TamerPendingJournal"), Is.False);
            File.WriteAllText(path, "{\"Gold\":\"0\",\"GooglePlay\":\"ProductNoAds\",\"__TamerAccountOwner\":\"synthetic-B\"}");
            Call(dataType, data, "Shutdown");
            UnityEngine.Object.DestroyImmediate(data);
            data = go.AddComponent(dataType);
            ((IDisposable)server).Dispose();
            server = Activator.CreateInstance(serverType, new object[] { data });
            Call(dataType, data, "InitializeIapRecovery", path, server, _root);
            string original = File.ReadAllText(path);
            Assert.Throws<InvalidOperationException>(() => Call(dataType, data, "BeginAccountSession", "synthetic-A"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
        }
        finally { (server as IDisposable)?.Dispose(); UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void Revival_Recovery_SelectionCommitsAfterReadyAndRequiresSameReauthenticatedOwner()
    {
        var type = Runtime("RevivalIapRecoveryProfile");
        string path = Prepare("synthetic-A", Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "synthetic-durable-owner-A");
        Assert.Throws<FileNotFoundException>(() => Call(type, null, "Selected", _root, "12B656", "synthetic-A"));
        Call(type, null, "MarkReady", _root, "12B656", path);
        Assert.That(Call(type, null, "Selected", _root, "12B656", "synthetic-A"), Is.EqualTo(path));
        Assert.Throws<InvalidDataException>(() => Call(type, null, "Selected", _root, "12B656", "synthetic-B"));
        Assert.Throws<IOException>(() => Prepare("synthetic-A", Guid.NewGuid().ToString("N")));
        Assert.That(File.ReadAllText(path), Is.EqualTo("synthetic-durable-owner-A"));
        Assert.That(File.Exists(Path.Combine(_root, "IapTest", "12B656", "recovery-profile-v1.selection.pending")), Is.False);
    }

    [Test]
    public void Revival_Recovery_LoginRequestCannotCreateAndDefaultBuildRemainsLocked()
    {
        var type = Runtime("RevivalIapIsolation");
        var request = (PlayFab.ClientModels.LoginWithCustomIDRequest)Call(type, null, "RecoveryLoginRequest",
            "iap-test-00000000000000000000000000000000");
        Assert.That(request.CreateAccount, Is.False);
        Assert.That(type.GetProperty("ResumeBuildApproved").GetValue(null), Is.False);
        Assert.Throws<ArgumentException>(() => Call(type, null, "RecoveryLoginRequest", "invalid-synthetic-input"));
    }

    private sealed class NoNetworkVerifier : IReceiptVerifier
    {
        public System.Threading.Tasks.Task<bool> VerifyAsync(string receipt, ReceiptSession session,
            System.Threading.CancellationToken token) => throw new InvalidOperationException("Unexpected synthetic network path");
    }
    [TestCase("test-catalog", NoAdsPurchaseFulfillment.ProductId, 1, true)]
    [TestCase("other-catalog", NoAdsPurchaseFulfillment.ProductId, 1, false)]
    [TestCase("test-catalog", "other-product", 1, false)]
    [TestCase("test-catalog", NoAdsPurchaseFulfillment.ProductId, -1, false)]
    [TestCase("test-catalog", NoAdsPurchaseFulfillment.ProductId, 0, false)]
    public void Revival_Recovery_PreflightRequiresExactCatalogSkuAndUnexpiredInventory(string catalog, string sku, int days, bool valid)
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new[] { new PlayFab.ClientModels.ItemInstance { ItemId = sku, CatalogVersion = catalog, Expiration = now.AddDays(days) } };
        Assert.That(Call(Runtime("RevivalIapIsolation"), null, "RecoveryInventoryOwned", items, "test-catalog", now), Is.EqualTo(valid));
    }

    [Test]
    public void Revival_Recovery_AuthenticationGenerationOrOwnerChangeRejectsCapturedSession()
    {
        var type = Runtime("RevivalIapRecoveryBinding");
        var owner = new object();
        var session = new ReceiptSession("synthetic-A", "synthetic-ticket");
        var binding = Activator.CreateInstance(type, new object[] { owner, 7, session });
        Assert.That(Call(type, binding, "Current", owner, 7, true, "synthetic-A"), Is.SameAs(session));
        Assert.That(Call(type, binding, "Current", owner, 8, true, "synthetic-A"), Is.Null,
            "Reauthentication on the same DataManager must invalidate its old receipt callback.");
        Assert.That(Call(type, binding, "Current", new object(), 7, true, "synthetic-A"), Is.Null);
        Assert.That(Call(type, binding, "Current", owner, 7, false, "synthetic-A"), Is.Null);
        Assert.That(Call(type, binding, "Current", owner, 7, true, "synthetic-B"), Is.Null);
    }

    [Test]
    public void Revival_Recovery_RealIapManagerRejectsStorePurchaseRestoreRetryAndAccountChange()
    {
        int sessionReads = 0;
        var gateType = Runtime("RevivalIapRecoveryGate");
        var gate = (IIapExecutionGate)Activator.CreateInstance(gateType);
        var type = Runtime("IAPManager");
        var manager = Activator.CreateInstance(type, new object[] { new NoNetworkVerifier(),
            (Func<ReceiptSession>)(() => { sessionReads++; return null; }), 1, gate });
        try
        {
            Call(type, manager, "InitializePurchasing");
            Call(type, manager, "RestorePurchases");
            Call(type, manager, "BuyProductID", NoAdsPurchaseFulfillment.ProductId);
            Call(type, manager, "RetryPendingPurchases");
            Assert.That(sessionReads, Is.Zero);
            Assert.That(type.GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager), Is.Null);
            Assert.That(Call(gateType, gate, "Open", _root, true, true), Is.True);
            Call(type, manager, "OnAuthAccountChanged");
            Assert.That(gate.CanAccessStore || gate.TryBeginVerification(), Is.False);
            var replacement = (IIapExecutionGate)Activator.CreateInstance(gateType);
            Assert.That(Call(gateType, replacement, "Open", _root, true, true), Is.False);
        }
        finally { ((IDisposable)manager).Dispose(); }
    }
}
