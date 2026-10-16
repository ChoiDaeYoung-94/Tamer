using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using AD.Privacy;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class RevivalDeletionReceiptTests
{
    private string _directory;
    private FaultStore _store;
    private ReceiptTestKeys _keys;
    private Gateway _gateway;
    private DeletionRecovery _record;
    private DeletionReceiptClient Client() => new DeletionReceiptClient(_gateway, _keys, _store);
    private sealed class FaultStore : IDeletionRecoveryStore
    {
        public FileDeletionRecoveryStore Inner;
        public bool FailRegisteredSave;
        public DeletionRecovery Load() => Inner.Load();
        public void Save(DeletionRecovery r) { if (FailRegisteredSave && r.ReceiptRegistered) throw new IOException(); Inner.Save(r); }
        public void Clear() { Inner.Clear(); }
    }
    private sealed class Gateway : IDeletionReceiptGateway
    {
        public DeletionRecovery Record;
        public string Verifier;
        public bool Accepted, LoseRegistration, Acked, Mismatch;
        public int Reads, Registers, Acks;
        public Action AfterAck;
        public Task<DeletionReceipt> RegisterReceiptAsync(DeletionAuthorization auth, string id, string verifier, CancellationToken t)
        {
            Registers++;
            Assert.That(auth.Proof, Is.EqualTo("synthetic-proof"));
            if (Verifier != null) Assert.That(verifier, Is.EqualTo(Verifier));
            Verifier = verifier;
            if (LoseRegistration) { LoseRegistration = false; throw new IOException(); }
            return Task.FromResult(Response(false));
        }
        public Task<DeletionReceipt> ReadReceiptAsync(string id, string cap, CancellationToken t)
        {
            Reads++;
            Assert.That(DeletionReceiptClient.Verifier(cap), Is.EqualTo(Verifier));
            Assert.That(Acked, Is.False);
            return Task.FromResult(Response(true));
        }
        public Task AcknowledgeReceiptAsync(string id, string cap, CancellationToken t)
        { Acks++; Assert.That(DeletionReceiptClient.Verifier(cap), Is.EqualTo(Verifier)); Acked = true; AfterAck?.Invoke(); return Task.CompletedTask; }
        private DeletionReceipt Response(bool reading) => new DeletionReceipt { requestId = Record.RequestId, clientKey = Record.ClientKey,
            ownerHash = Mismatch ? DeletionRecovery.Hash("foreign-owner") : Record.OwnerHash, binding = Record.Binding,
            expiresAt = 2000, policyRevision = Record.Revision, scope = "title",
            state = reading ? "processing" : "awaiting_confirmation", submissionState = reading ? (Accepted ? "accepted" : "submission_unknown") : "not_submitted" };
    }
    [SetUp] public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "receipt-fixture-" + Guid.NewGuid().ToString("N"));
        _store = new FaultStore { Inner = new FileDeletionRecoveryStore(Path.Combine(_directory, "pending.json")) };
        _keys = new ReceiptTestKeys();
        _record = new DeletionRecovery { Origin = "https://example.invalid/", Title = "ABC12", ClientKey = new string('a', 32),
            OwnerHash = DeletionRecovery.Hash("https://example.invalid/", "ABC12", "synthetic-a"),
            Binding = DeletionRecovery.Hash("https://example.invalid/", "ABC12", "synthetic-a", "entity-a"), RequestId = "request-one", Revision = "v1" };
        _gateway = new Gateway { Record = _record };
    }
    [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private Task Register() => Client().RegisterAsync(new DeletionAuthorization("synthetic-a", "synthetic-proof"), _record, CancellationToken.None);

    private const BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Invoke(object target, string method, params object[] args)
    {
        try { return (target as Type ?? target.GetType()).GetMethod(method, Hidden).Invoke(target is Type ? null : target, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    private static object Field(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    private static string Json(IDictionary<string, object> values) => (string)Invoke(Runtime("AD.Utility"), "SerializeToJson", values);

    // Uses the actual Managers.Init -> DataManager.InitializeData and Login.Start -> receipt bootstrap.
    // Only the save path and receipt client are synthetic; no manual ApplyDeletionReceipt shortcut.
    private sealed class ReceiptOnlyHost : IDisposable
    {
        private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
        private readonly GameObject _root;
        private readonly FieldInfo _singleton = Runtime("AD.Managers").GetField("instance", Hidden);
        private readonly object _previousManager;
        private readonly Dictionary<FieldInfo, object> _previousStatics = new Dictionary<FieldInfo, object>();
        private readonly PropertyInfo _title = Runtime("PlayFab.PlayFabSettings").GetProperty("TitleId", Hidden);
        private readonly object _previousTitle, _auth, _previousAuth;
        private readonly bool _hadPause;
        private readonly int _pause;
        private readonly HashSet<int> _previousEvents;
        internal Component Manager, Data, Login;
        internal readonly string Path;
        internal string InventoryPath(string owner) => System.IO.Path.Combine(Path + ".inventory",
            (string)Invoke(Runtime("AD.PlayerInventoryStore"), "OwnerKey", owner) + ".json");
        internal ReceiptOnlyHost(string directory, string raw)
        {
            string saveRoot = System.IO.Path.Combine(directory,"TamerReceiptOnly-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveRoot); Path = System.IO.Path.Combine(saveRoot,"ReceiptOnlyPlayerData.json");
            File.WriteAllText(Path, raw);
            _previousManager = _singleton.GetValue(null); _singleton.SetValue(null,null);
            foreach(var pair in new[]{new[]{"Player","_instance"},new[]{"ShopMan","_instance"}})
            {var field=Runtime(pair[0]).GetField(pair[1],Hidden);_previousStatics.Add(field,field.GetValue(null));field.SetValue(null,null);}
            foreach(string name in new[]{"_origin","_title","_directory"})
            {var field=Runtime("AD.DeletionReceiptBootstrap").GetField(name,Hidden);_previousStatics.Add(field,field.GetValue(null));}
            _previousTitle=_title.GetValue(null);_title.SetValue(null,"ABC12");
            _hadPause=PlayerPrefs.HasKey("AD_DeletionAcceptedNeedsLogin");_pause=PlayerPrefs.GetInt("AD_DeletionAcceptedNeedsLogin");
            var settings=Runtime("PlayFab.PlayFabSettings");
            _auth=settings.GetProperty("staticPlayer",Hidden)?.GetValue(null)??settings.GetField("staticPlayer",Hidden).GetValue(null);
            _previousAuth=Activator.CreateInstance(_auth.GetType());Invoke(_previousAuth,"CopyFrom",_auth);Invoke(_auth,"ForgetAllCredentials");
            _previousEvents=new HashSet<int>(Resources.FindObjectsOfTypeAll(Runtime("UnityEngine.EventSystems.EventSystem")).Select(o=>o.GetInstanceID()));
            _root=new GameObject("Receipt-only initialization fixture");_root.SetActive(false);SceneManager.MoveGameObjectToScene(_root,_scene);
            Manager=_root.AddComponent(Runtime("AD.Managers"));Data=_root.AddComponent(Runtime("AD.DataManager"));
            Set(Manager,"_dataM",Data);Set(Data,"ReceiptInitializationPathForTests",Path);
            // Missing popup/ads/pool scene inputs would fail if InitM erroneously continued.
            Set(Manager,"_soundM",_root.AddComponent(Runtime("AD.SoundManager")));
            Login=_root.AddComponent(Runtime("AD.Login"));
            var status=new GameObject("Existing receipt-only status panel");status.transform.SetParent(_root.transform);status.SetActive(false);
            var retry=new GameObject("Existing retry panel");retry.transform.SetParent(_root.transform);retry.SetActive(false);
            Set(Login,"_loading",status);Set(Login,"_retry",retry);
        }
        internal void Initialize() { Invoke(Manager,"Init");Invoke(Manager,"Start"); }
        internal Component OpenReceipt(string directory)
        {
            StartLoginEntry(directory);
            return _root.GetComponentsInChildren(Runtime("AD.DeletionReceiptPresenter"),true).Single();
        }
        internal void StartLoginEntry(string directory)
        {
            Invoke(Runtime("AD.DeletionReceiptBootstrap"),"Configure",new Uri("https://example.invalid/"),"ABC12",directory);
            Invoke(Login,"Start");
        }
        internal void InitializeRejectedDataOnly()
        {
            Invoke(Manager,"TryClaimInstance");
            var server=Activator.CreateInstance(Runtime("AD.ServerManager"),Data);
            Set(Manager,"_serverM",server);
            Invoke(Data,"InitializeData");
        }
        internal void AssertBlocked()
        {
            Assert.That(_singleton.GetValue(null),Is.SameAs(Manager));
            Assert.That(Field(Manager,"_shutdown"),Is.EqualTo(false));
            Assert.That(Data.GetType().GetProperty("ReceiptOnly",Hidden).GetValue(Data),Is.EqualTo(true));
            Assert.That(Data.GetType().GetProperty("IsServerDataReady").GetValue(Data),Is.EqualTo(false));
            Assert.That(Data.GetType().GetProperty("HasKnownAccount").GetValue(Data),Is.EqualTo(true));
            Assert.That(Field(Data,"LocalPlayerData"),Is.Null);
            Assert.That(Field(Data,"MonsterData"),Is.Null);Assert.That(Field(Data,"ItemData"),Is.Null);
            Assert.That(Field(Data,"_ctsLocalDataUpdate"),Is.Null);
            Assert.That(Field(Login,"_nativeGoogleActivated"),Is.EqualTo(false));
            Invoke(Login,"RetryConnection");Invoke(Login,"StartLogin");
            Assert.That(((GameObject)Field(Login,"_loading")).activeSelf,Is.True);
            Assert.That(((GameObject)Field(Login,"_retry")).activeSelf,Is.False);
            Assert.That(Field(Login,"_loginCaptured"),Is.EqualTo(false));
            Assert.That(Invoke(Data,"TryUpdateLocalData","Gold","999"),Is.EqualTo(false));
            Assert.That(Invoke(Data,"TryGrantNoAds"),Is.EqualTo(false));
            Assert.That(()=>Invoke(Data,"SaveLocalData"),Throws.Exception);
            Assert.That(()=>Invoke(Data,"ReadInventory"),Throws.Exception);
            Assert.That(()=>Invoke(Data,"BeginAccountSession","synthetic-a"),Throws.Exception);
            Invoke(Data,"UpdatePlayerData");Invoke(Data,"UpdateData");
            Assert.That(Field(Manager,"_IAPM").GetType().GetProperty("Status").GetValue(Field(Manager,"_IAPM")).ToString(),Is.EqualTo("NotInitialized"));
        }
        public void Dispose()
        {
            Invoke(Manager,"Shutdown");EditorSceneManager.ClosePreviewScene(_scene);
            foreach(var item in Resources.FindObjectsOfTypeAll(Runtime("UnityEngine.EventSystems.EventSystem")))
                if(!_previousEvents.Contains(item.GetInstanceID())&&item.name=="ReceiptEventSystem")UnityEngine.Object.DestroyImmediate(((Component)item).gameObject);
            foreach(var pair in _previousStatics)pair.Key.SetValue(null,pair.Value);
            _singleton.SetValue(null,_previousManager);_title.SetValue(null,_previousTitle);Invoke(_auth,"CopyFrom",_previousAuth);
            if(_hadPause)PlayerPrefs.SetInt("AD_DeletionAcceptedNeedsLogin",_pause);else PlayerPrefs.DeleteKey("AD_DeletionAcceptedNeedsLogin");
            PlayerPrefs.Save();
        }
    }

    private string Progress(string owner, string metadata, string journal=null)
    {
        var data=new Dictionary<string,object>{{"__TamerAccountOwner",owner},{"Gold","90"},{"GooglePlay","ProductNoAds"},
            {"__TamerEquipmentTransaction",metadata},{"futureGameKey","retain"}};
        if(journal!=null)data["__TamerPendingJournal"]=journal;
        return Json(data);
    }
    private string EquipmentMetadata(string phase)
    {
        const string owner="synthetic-a";
        var slots=new Dictionary<string,string>{{"SimpleSword","Sword"}};
        object Snapshot(bool after)=>Invoke(Runtime("AD.PlayerInventorySnapshot"),"Validate",owner,owner,Array.Empty<string>(),
            after?new[]{"SimpleSword"}:Array.Empty<string>(),new Dictionary<string,string>{{"Sword",after?"SimpleSword":null},{"Shield",null}},Array.Empty<string>(),slots);
        var record=Invoke(Runtime("AD.EquipmentPurchaseRecord"),"Create",owner,1L,new string('c',32),"SimpleSword","Sword",10,100,90,1L,
            _record.InventorySession,Snapshot(false),Snapshot(true));
        if(phase=="Complete")record=Invoke(record,"Complete");
        return (string)Invoke(record,"Serialize");
    }
    private void WriteInventory(ReceiptOnlyHost host,string owner,bool after,bool newer=false)
    {
        string path=host.InventoryPath(owner);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        File.WriteAllText(path,Json(new Dictionary<string,object>{{"Version",1},{"Owner",owner},
            {"Session",newer?new string('d',32):_record.InventorySession},{"Collection",Array.Empty<string>()},
            {"OwnedItems",after?new[]{"SimpleSword"}:Array.Empty<string>()},
            {"Equipped",new Dictionary<string,string>{{"Sword",after?"SimpleSword":null},{"Shield",null}}}}));
    }
    private async Task PrepareAcceptedForBootstrap()
    {
        _record.InventoryOwnerKey=(string)Invoke(Runtime("AD.PlayerInventoryStore"),"OwnerKey","synthetic-a");
        _record.InventorySession=new string('b',32);
        _store.Inner=new FileDeletionRecoveryStore(System.IO.Path.Combine(_directory,"receipts",_record.OwnerHash+".json"));
        await Register();_record.SubmissionStarted=true;_store.Save(_record);
        _gateway.Accepted=true;await Client().ReadAsync(_record,CancellationToken.None);
    }

    [TestCase("invalid-inner")]
    [TestCase("Materializing-before")]
    [TestCase("Materializing-after")]
    [TestCase("Complete")]
    public async Task Revival_ReceiptOnly_ActualInitAndLoginBootstrapCanFinishAcceptedBWithoutNormalServices(string phase)
    {
        await PrepareAcceptedForBootstrap();
        const string broken="{\"version\":\"unsupported\"}";
        using(var host=new ReceiptOnlyHost(_directory,Progress("synthetic-a",broken)))
        {
            host.Initialize();host.AssertBlocked();
            if(phase!="invalid-inner")
            {
                var changes=Activator.CreateInstance(Runtime("AD.PlayerDataChanges"));Invoke(changes,"Track","Gold","90");
                File.WriteAllText(host.Path,Progress("synthetic-a",EquipmentMetadata(phase=="Complete"?"Complete":"Materializing"),
                    (string)Invoke(changes,"Serialize","synthetic-a")));
            }
            WriteInventory(host,"synthetic-a",phase=="Materializing-after"||phase=="Complete");
            WriteInventory(host,"other-owner",false);string foreign=File.ReadAllText(host.InventoryPath("other-owner"));
            var presenter=host.OpenReceipt(System.IO.Path.Combine(_directory,"receipts"));
            Assert.That(Field(presenter,"_owner"),Is.SameAs(host.Data));Assert.That(Field(presenter,"_client"),Is.Not.Null);
            Set(presenter,"_client",Client()); // Uses real terminal seal validation and FinishAsync, synthetic key/gateway only.
            int reads=_gateway.Reads,registers=_gateway.Registers;
            Invoke(presenter,"Check");
            Assert.That(Field(presenter,"_busy"),Is.EqualTo(false),"Synthetic completed tasks must finish the existing handler synchronously.");
            Assert.That(File.Exists(host.Path),Is.False);Assert.That(File.Exists(host.InventoryPath("synthetic-a")),Is.False);
            Assert.That(File.ReadAllText(host.InventoryPath("other-owner")),Is.EqualTo(foreign));
            Assert.That(_store.Load(),Is.Null);Assert.That(_keys.Keys,Is.Empty);
            Assert.That(_gateway.Reads,Is.EqualTo(reads));Assert.That(_gateway.Registers,Is.EqualTo(registers));
            host.AssertBlocked(); // Even successful cleanup cannot silently initialize a game on this host.
        }
    }

    [TestCase("other-owner")]
    [TestCase("newer-inventory")]
    [TestCase("invalid-terminal")]
    public async Task Revival_ReceiptOnly_BootstrapPreservesUnmatchedOrUnverifiedAcceptedData(string mismatch)
    {
        await PrepareAcceptedForBootstrap();
        string owner=mismatch=="other-owner"?"other-owner":"synthetic-a";
        using(var host=new ReceiptOnlyHost(_directory,Progress(owner,"{\"version\":\"unsupported\"}")))
        {
            host.Initialize();host.AssertBlocked();WriteInventory(host,"synthetic-a",false,mismatch=="newer-inventory");
            if(mismatch=="invalid-terminal")
            {var bad=_store.Load();bad.TerminalSeal=new string('0',64);_store.Save(bad);}
            string progress=File.ReadAllText(host.Path),inventory=File.ReadAllText(host.InventoryPath("synthetic-a"));
            var presenter=host.OpenReceipt(System.IO.Path.Combine(_directory,"receipts"));
            if(mismatch=="other-owner")Assert.That(Field(presenter,"_client"),Is.Null);
            else{Set(presenter,"_client",Client());Invoke(presenter,"Check");}
            Assert.That(File.ReadAllText(host.Path),Is.EqualTo(progress));Assert.That(File.ReadAllText(host.InventoryPath("synthetic-a")),Is.EqualTo(inventory));
            Assert.That(_store.Load().CleanupApplied,Is.False);Assert.That(_keys.Keys.Count,Is.EqualTo(1));
            host.AssertBlocked();
        }
    }

    [Test]
    public void Revival_ReceiptOnly_ActualLoginWithoutReceiptCannotFallThroughToAuthenticationOrSignup()
    {
        using(var host=new ReceiptOnlyHost(_directory,Progress("synthetic-a","{\"version\":\"unsupported\"}")))
        {
            string before=File.ReadAllText(host.Path);
            host.Initialize();host.StartLoginEntry(System.IO.Path.Combine(_directory,"no-receipts"));
            host.AssertBlocked();
            Assert.That(Field(host.Login,"_signupCandidateIdentity"),Is.Null);
            Assert.That(Field(host.Login,"_signupPanel"),Is.Null);
            Assert.That(File.ReadAllText(host.Path),Is.EqualTo(before));
            Assert.That(Directory.Exists(host.Path+".inventory"),Is.False);
        }
    }

    [Test]
    public async Task Revival_ReceiptOnly_CancelledTerminalClosesOnlyReceiptAndNeverEnablesNormalLogin()
    {
        await PrepareAcceptedForBootstrap();
        Client().RememberTerminal(_record,DeletionState.Cancelled);
        using(var host=new ReceiptOnlyHost(_directory,Progress("synthetic-a","{\"version\":\"unsupported\"}")))
        {
            host.Initialize();WriteInventory(host,"synthetic-a",false);
            string progress=File.ReadAllText(host.Path),inventory=File.ReadAllText(host.InventoryPath("synthetic-a"));
            var presenter=host.OpenReceipt(System.IO.Path.Combine(_directory,"receipts"));
            Set(presenter,"_client",Client());Invoke(presenter,"Check");
            Assert.That(_store.Load(),Is.Null);Assert.That(_keys.Keys,Is.Empty);
            Assert.That(File.ReadAllText(host.Path),Is.EqualTo(progress));Assert.That(File.ReadAllText(host.InventoryPath("synthetic-a")),Is.EqualTo(inventory));
            host.AssertBlocked();
        }
    }

    [TestCase("duplicate-owner")]
    [TestCase("owner-type")]
    [TestCase("owner-null")]
    public void Revival_ReceiptOnly_AmbiguousOuterOwnerCannotBePromotedToCleanupHost(string change)
    {
        string raw=Progress("synthetic-a","{\"version\":\"unsupported\"}");
        if(change=="duplicate-owner")raw=raw.Insert(1,"\"__TamerAccountOwner\":\"other-owner\",");
        if(change=="owner-type")raw=raw.Replace("\"__TamerAccountOwner\":\"synthetic-a\"","\"__TamerAccountOwner\":{}");
        if(change=="owner-null")raw=raw.Replace("\"__TamerAccountOwner\":\"synthetic-a\"","\"__TamerAccountOwner\":\"null\"");
        using(var host=new ReceiptOnlyHost(_directory,raw))
        {
            // Reject classification before Managers would destroy the failed normal host.
            Assert.That(()=>host.InitializeRejectedDataOnly(),Throws.Exception);
            Assert.That(host.Data.GetType().GetProperty("ReceiptOnly",Hidden).GetValue(host.Data),Is.EqualTo(false));
            Assert.That(Field(host.Data,"_equipmentReadBlocked"),Is.EqualTo(true));
            Assert.That(Field(host.Data,"LocalPlayerData"),Is.Null);
            Assert.That(File.ReadAllText(host.Path),Is.EqualTo(raw));
            Assert.That(Directory.Exists(host.Path+".inventory"),Is.False);
        }
    }

    [Test] public async Task Revival_InventoryDeletionFailureRetainsReceiptAndKeyForRestart()
    {
        var inventoryType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AD.PlayerInventoryStore")).First(t=>t!=null);
        _record.InventoryOwnerKey=(string)inventoryType.GetMethod("OwnerKey").Invoke(null,new object[]{"synthetic-a"});
        _record.InventorySession=new string('b',32);
        await Register();
        string directory=Path.Combine(_directory,"inventory"); Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,_record.InventoryOwnerKey+".json");
        File.WriteAllText(path,"{\"Version\":1,\"Owner\":\"synthetic-a\",\"Session\":\""+_record.InventorySession+"\",\"Collection\":[],\"OwnedItems\":[],\"Equipped\":{\"Sword\":null,\"Shield\":null}}");
        Action cleanup=()=>
        {
            try { inventoryType.GetMethod("DeleteBound").Invoke(null,new object[]{directory,_record.InventoryOwnerKey,_record.InventorySession,new Func<string,bool>(owner=>owner=="synthetic-a")}); }
            catch(TargetInvocationException e) { throw e.InnerException ?? e; }
        };
        _gateway.Accepted=true; await Client().ReadAsync(_record,CancellationToken.None);
        using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.ThrowsAsync<IOException>(async()=>await Client().FinishAsync(_record,cleanup,CancellationToken.None));
        Assert.That(_store.Load().CleanupApplied,Is.False); Assert.That(_keys.Keys.Count,Is.EqualTo(1)); Assert.That(_gateway.Acked,Is.False);
        var restarted=_store.Load(); int reads=_gateway.Reads;
        Assert.That((await Client().ReadAsync(restarted,CancellationToken.None)).State,Is.EqualTo(DeletionState.Accepted));
        await Client().FinishAsync(restarted,cleanup,CancellationToken.None);
        Assert.That(_gateway.Reads,Is.EqualTo(reads)); Assert.That(File.Exists(path),Is.False);
        Assert.That(_store.Load(),Is.Null); Assert.That(_keys.Keys,Is.Empty);
    }

    [Test] public async Task Revival_InventoryDeletionMetadataIsBoundToReceiptCapabilityAndSeal()
    {
        _record.InventoryOwnerKey=new string('b',64); _record.InventorySession=new string('c',32);
        await Register(); _gateway.Accepted=true;
        var tampered=_store.Load(); tampered.InventorySession=new string('d',32);
        Assert.ThrowsAsync<AssertionException>(async()=>await Client().ReadAsync(tampered,CancellationToken.None));
        await Client().ReadAsync(_record,CancellationToken.None);
        var terminal=_store.Load(); terminal.InventorySession=new string('d',32);
        Assert.ThrowsAsync<InvalidOperationException>(async()=>await Client().ReadAsync(terminal,CancellationToken.None));
        Assert.That(_store.Load().CleanupApplied,Is.False); Assert.That(_keys.Keys.Count,Is.EqualTo(1));
    }

    [Test] public void Revival_DeletionReceiptClearDoesNotHideDirectoryAccessFailureAsAbsentFile()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "pending.json"));
        Assert.Throws<UnauthorizedAccessException>(() => _store.Clear());
        Assert.That(Directory.Exists(Path.Combine(_directory, "pending.json")), Is.True);
    }

    [Test] public async Task Revival_DeletionReceiptAcceptedLostSessionRestartNeedsNoTicketAndNeverResubmits()
    {
        await Register();
        _record.SubmissionStarted = true; _store.Save(_record);
        _gateway.Accepted = true; // Server accepted; response and all login credentials are gone.
        var restarted = _store.Load();
        var client = Client(); // New process-equivalent client, same protected key store and metadata only.
        Assert.That((await client.ReadAsync(restarted, CancellationToken.None)).State, Is.EqualTo(DeletionState.Accepted));
        int applied = 0;
        await client.FinishAsync(restarted, () => applied++, CancellationToken.None);
        Assert.That(applied, Is.EqualTo(1));
        Assert.That(_gateway.Reads, Is.EqualTo(1)); Assert.That(_gateway.Registers, Is.EqualTo(1));
        Assert.That(_store.Load(), Is.Null); Assert.That(_keys.Keys, Is.Empty);
    }
    [Test] public async Task Revival_DeletionReceiptLostRegistrationReusesProtectedKeyAndDurableMetadata()
    {
        _gateway.LoseRegistration = true;
        Assert.ThrowsAsync<IOException>(async () => await Register());
        _record = _store.Load(); _gateway.Record = _record;
        Assert.That(_record.KeyCreated, Is.True); Assert.That(_record.ReceiptRegistered, Is.False);
        await Register();
        Assert.That(_keys.Keys.Count, Is.EqualTo(1)); Assert.That(_gateway.Registers, Is.EqualTo(2));
        Assert.That(_store.Load().ReceiptRegistered, Is.True);
        string capability = _keys.Sign(_record.KeyAlias, DeletionReceiptClient.AccessMessage(_record));
        StringAssert.DoesNotContain(capability, File.ReadAllText(Path.Combine(_directory, "pending.json")));
    }
    [Test] public async Task Revival_DeletionReceiptAckThenLocalFailureRestartsFromSealedTerminalWithoutCleanupTwice()
    {
        await Register(); _gateway.Accepted = true;
        await Client().ReadAsync(_record, CancellationToken.None);
        string otherAlias = "tamer.deletion.receipt." + new string('b', 32); _keys.Create(otherAlias);
        int applied = 0;
        FileStream deletionLock = null;
        _gateway.AfterAck = () => deletionLock = new FileStream(Path.Combine(_directory, "pending.json"),
            FileMode.Open, FileAccess.Read, FileShare.Read); // Real Windows sharing denial, after the terminal save.
        try
        {
            Assert.ThrowsAsync<IOException>(async () => await Client().FinishAsync(_record, () => applied++, CancellationToken.None));
            Assert.That(_keys.Keys.ContainsKey(_record.KeyAlias), Is.True, "Failed journal removal must preserve its verification key");
            Assert.That(_store.Load().CleanupApplied, Is.True);
        }
        finally { deletionLock?.Dispose(); _gateway.AfterAck = null; }
        Assert.That(_gateway.Acked, Is.True);
        var restarted = _store.Load();
        Assert.That((await Client().ReadAsync(restarted, CancellationToken.None)).State, Is.EqualTo(DeletionState.Accepted));
        await Client().FinishAsync(restarted, () => applied++, CancellationToken.None);
        Assert.That(applied, Is.EqualTo(1)); Assert.That(_gateway.Reads, Is.EqualTo(1)); Assert.That(_gateway.Acks, Is.EqualTo(2));
        Assert.That(_keys.Keys.ContainsKey(otherAlias), Is.True); Assert.That(_keys.Keys.Count, Is.EqualTo(1));
        Assert.That(_store.Load(), Is.Null);
        Assert.DoesNotThrow(() => _store.Clear(), "An already absent journal remains idempotent");
    }
    [Test] public async Task Revival_DeletionReceiptMissingKeyOrWrongOwnerNeverBecomesAccepted()
    {
        await Register(); _gateway.Accepted = true; _gateway.Mismatch = true;
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Client().ReadAsync(_record, CancellationToken.None));
        Assert.That(_record.TerminalState, Is.Null);
        _gateway.Mismatch = false; _keys.Delete(_record.KeyAlias);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Client().ReadAsync(_record, CancellationToken.None));
        Assert.That(_gateway.Reads, Is.EqualTo(1)); Assert.That(_gateway.Acks, Is.Zero); Assert.That(_keys.Keys, Is.Empty);
    }
    [Test] public void Revival_DeletionReceiptRegistrationNotDurableCannotBeUsedForSubmission()
    {
        _store.FailRegisteredSave = true;
        Assert.ThrowsAsync<IOException>(async () => await Register());
        Assert.That(_store.Load().ReceiptRegistered, Is.False);
        Assert.That(_gateway.Reads, Is.Zero); Assert.That(_gateway.Acks, Is.Zero);
    }
}
