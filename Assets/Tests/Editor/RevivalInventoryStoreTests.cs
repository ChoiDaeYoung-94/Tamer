using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RevivalInventoryStoreTests
{
    private string _root;
    private object _store;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args)
    {
        try { return (target as Type ?? target.GetType()).GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Invoke(target is Type ? null : target,args); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }
    private static Dictionary<string,string> Slots(string sword = null) => new Dictionary<string,string> { {"Sword",sword},{"Shield",null} };
    private object NewStore() => Activator.CreateInstance(Runtime("AD.PlayerInventoryStore"), _root, new[] { "Bat", "Crab" },
        new Dictionary<string,string> { {"SimpleSword","Sword"},{"MasterSword","Sword"},{"SimpleShield","Shield"} });
    private object Load(string owner) => Call(_store,"Load",owner);
    private object Save(string owner, string[] collection, string[] owned, string sword = null) => Call(_store,"Save",owner,collection,owned,Slots(sword));
    private string PathFor(string owner) => (string)Call(_store,"PathFor",owner);
    private static object Value(object snapshot,string name) => snapshot.GetType().GetProperty(name).GetValue(snapshot);
    private void DeleteBound(string owner, string session, Func<string,bool> matches)
    {
        try { _store.GetType().GetMethod("DeleteBound").Invoke(null,new object[]{_root,Path.GetFileNameWithoutExtension(PathFor(owner)),session,matches}); }
        catch(TargetInvocationException e) { throw e.InnerException ?? e; }
    }
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Field(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    [SetUp] public void Setup() { _root = Path.Combine(Path.GetTempPath(),"TamerInventory-"+Guid.NewGuid().ToString("N")); _store=NewStore(); }
    [TearDown] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }

    private sealed class EquipmentFixture : IDisposable
    {
        private readonly GameObject _go;
        private readonly FieldInfo _managerInstance = Runtime("AD.Managers").GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        private readonly FieldInfo _playerInstance = Runtime("Player").GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
        private readonly FieldInfo _shopInstance = Runtime("ShopMan").GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
        private readonly object _oldManager, _oldPlayer, _oldShop;
        private readonly Component _manager;
        internal Component Data;
        internal readonly string SavePath;
        internal string InventoryPath => Path.Combine(SavePath + ".inventory", (string)Call(Runtime("AD.PlayerInventoryStore"), "OwnerKey", "A") + ".json");
        internal Dictionary<string, string> Local => (Dictionary<string, string>)Field(Data, "LocalPlayerData");
        internal Dictionary<string, string> Stored => (Dictionary<string, string>)Call(Runtime("AD.DataManager"), "ParseData", File.ReadAllText(SavePath));
        internal bool Ready => (bool)Data.GetType().GetProperty("IsServerDataReady").GetValue(Data);
        internal readonly List<Dictionary<string, string>> Uploads = new List<Dictionary<string, string>>();
        private readonly Dictionary<string, string> _defaults = new Dictionary<string, string>
        { ["Gold"] = "100", ["AllyMonsters"] = "null", ["GooglePlay"] = "ProductNoAds,FutureProduct", ["futureGameKey"] = "retain" };

        internal EquipmentFixture(string root)
        {
            Directory.CreateDirectory(root); SavePath = Path.Combine(root, "EquipmentPlayerData.json");
            _oldManager = _managerInstance.GetValue(null); _oldPlayer = _playerInstance.GetValue(null); _oldShop = _shopInstance.GetValue(null);
            _go = new GameObject("Offline equipment transaction fixture"); _go.SetActive(false);
            _manager = _go.AddComponent(Runtime("AD.Managers")); _managerInstance.SetValue(null, _manager);
            _playerInstance.SetValue(null, null); _shopInstance.SetValue(null, null);
            Data = NewData(); Set(_manager, "_dataM", Data);
            Set(Data, "LocalPlayerData", new Dictionary<string, string>(_defaults)); Set(Data, "_localOwner", "A");
            Set(Data, "<PlayFabId>k__BackingField", "A"); Set(Data, "_accountGeneration", 1);
            Set(Data, "_inventorySession", new string('a', 32)); Set(Data, "<IsServerDataReady>k__BackingField", true);
            Call(Data, "SaveLocalData"); Call(Data, "ReadInventory");
            Call(Data, "WriteInventory", "A", 1, new[] { "Bat" }, new[] { "SimpleSword", "SimpleShield" },
                new Dictionary<string, string> { ["Sword"] = "SimpleSword", ["Shield"] = "SimpleShield" });
        }

        private Component NewData()
        {
            var data = _go.AddComponent(Runtime("AD.DataManager"));
            Set(data, "_playerDataPath", SavePath); Set(data, "_defaults", new Dictionary<string, string>(_defaults));
            Set(data, "MonsterData", new Dictionary<string, object> { ["Bat"] = null });
            Set(data, "ItemData", new Dictionary<string, object>
            {
                ["SimpleSword"] = new Dictionary<string, object> { ["Price"] = "10" },
                ["MasterSword"] = new Dictionary<string, object> { ["Price"] = "10" },
                ["SimpleShield"] = new Dictionary<string, object> { ["Price"] = "10" },
                ["MasterShield"] = new Dictionary<string, object> { ["Price"] = "10" }
            });
            var server = Activator.CreateInstance(Runtime("AD.ServerManager"), new object[]
            {
                (Func<string>)(() => (string)data.GetType().GetProperty("PlayFabId").GetValue(data)),
                (Func<bool>)(() => (bool)data.GetType().GetProperty("IsServerDataReady").GetValue(data)),
                (Action<string, Action<Dictionary<string,string>>, Action<int>>)((account, success, failure) => success(new Dictionary<string,string>(_defaults))),
                (Action<string, Dictionary<string,string>, Action, Action<int>>)((account, values, success, failure) => Uploads.Add(new Dictionary<string,string>(values))),
                (Action<TimeSpan, Action>)((delay, action) => { }),
                (Action<Dictionary<string,string>, bool>)((values, update) =>
                {
                    var type = data.GetType().GetField("PlayFabPlayerData").FieldType;
                    var records = (IDictionary)Activator.CreateInstance(type); var recordType = type.GetGenericArguments()[1];
                    foreach (var pair in values) { var record = Activator.CreateInstance(recordType); recordType.GetField("Value").SetValue(record, pair.Value); records.Add(pair.Key, record); }
                    Set(data, "PlayFabPlayerData", records); Call(data, "UpdateData");
                })
            });
            Set(data, "_server", server);
            return data;
        }
        internal object Quote() => Call(Data, "PrepareEquipmentPurchase", "MasterSword", 10);
        internal string Buy(object intent, Func<bool> current = null) => Call(Data, "TryCommitEquipmentPurchase", intent,
            int.Parse(Local["Gold"]), current ?? (() => true)).ToString();
        internal void Reauthenticate() => Call(Data, "BeginAccountSession", "A"); // No pre-load to hide the production reload.
        internal void Restart(string account = "A")
        {
            Call(Data, "SuspendAccountSession");
            Data = NewData(); Set(_manager, "_dataM", Data);
            // Fresh initialization loads once, as InitializeData does; recovery itself must use BeginAccountSession.
            Call(Data, "LoadStoredData"); Call(Data, "BeginAccountSession", account);
        }
        internal void FirstRead() => Call(Data, "UpdatePlayerData");
        internal object ReadInventory() => Call(Data, "ReadInventory");
        internal object PauseAfterProgressCommit()
        {
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
            object old = hook.GetValue(null), intent = Quote();
            try
            {
                hook.SetValue(null, (Action<string,string,string>)((p,t,s) => { throw new IOException("Synthetic pending purchase"); }));
                Assert.That(Buy(intent),Is.EqualTo("RecoveryRequired"));
            }
            finally { hook.SetValue(null,old); }
            return intent;
        }
        public void Dispose()
        {
            _managerInstance.SetValue(null, _oldManager); _playerInstance.SetValue(null, _oldPlayer); _shopInstance.SetValue(null, _oldShop);
            UnityEngine.Object.DestroyImmediate(_go);
        }
    }

    [TestCase("P-before")]
    [TestCase("P-after")]
    [TestCase("I-before")]
    [TestCase("I-after")]
    [TestCase("Complete-before")]
    [TestCase("Complete-after")]
    public void Revival_EquipmentTransaction_CutsRecoverThroughNewManagerWithoutRecharge(string cut)
    {
        using (var f = new EquipmentFixture(Path.Combine(_root, "transaction")))
        {
            var pHook = Runtime("AD.DataManager").GetField("JournalWriteCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
            var iHook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
            object oldP = pHook.GetValue(null), oldI = iHook.GetValue(null);
            string sourceSession = (string)Field(Call(Field(f.Data, "_inventoryStore"), "ReadEnvelope", "A"), "<Session>k__BackingField");
            int pReplace = 0, inventoryReplace = 0;
            try
            {
                pHook.SetValue(null, (Action<string,string,string>)((path, temp, stage) =>
                {
                    if (path != f.SavePath) return;
                    if (stage == "replaced") pReplace++;
                    if ((cut == "P-before" && pReplace == 0 && stage == "temporary-closed") ||
                        (cut == "P-after" && pReplace == 1 && stage == "replaced") ||
                        (cut == "Complete-before" && pReplace == 1 && stage == "temporary-closed") ||
                        (cut == "Complete-after" && pReplace == 2 && stage == "replaced")) throw new IOException("Synthetic P cut");
                }));
                iHook.SetValue(null, (Action<string,string,string>)((path, temp, stage) =>
                {
                    if (stage == "replaced") inventoryReplace++;
                    if ((cut == "I-before" && stage == "temporary-closed") || (cut == "I-after" && stage == "replaced"))
                        throw new IOException("Synthetic I cut");
                }));
                Assert.That(f.Buy(f.Quote()), Is.EqualTo("RecoveryRequired"));
            }
            finally { pHook.SetValue(null, oldP); iHook.SetValue(null, oldI); }
            Assert.That(f.Ready, Is.False);
            Assert.That(Call(f.Data, "TryUpdateLocalData", "Gold", "100"), Is.EqualTo(false));
            int extraMaterialization = 0;
            try
            {
                iHook.SetValue(null, (Action<string,string,string>)((path,temp,stage) => { if (stage == "replaced") extraMaterialization++; }));
                f.Restart();
            }
            finally { iHook.SetValue(null, oldI); }
            Assert.That(f.Ready, Is.False, "Private recovery may not publish public ready.");
            Assert.That(f.Stored["Gold"], Is.EqualTo(cut == "P-before" ? "100" : "90"));
            Assert.That(f.Stored["GooglePlay"], Is.EqualTo("ProductNoAds,FutureProduct"));
            if (cut != "P-before")
            {
                Assert.That(f.Stored["__TamerEquipmentTransaction"], Does.Contain("\"phase\":\"Complete\""));
                Assert.That(extraMaterialization, Is.EqualTo(inventoryReplace == 0 ? 1 : 0));
                Assert.That(File.ReadAllText(f.InventoryPath), Does.Contain(sourceSession), "Recovery must not rebind the source session first.");
            }
            f.FirstRead();
            Assert.That(f.Ready, Is.True, "First read/binding succeeds without any Player object.");
            var snapshot = f.ReadInventory();
            Assert.That(((IDictionary<string,string>)Value(snapshot,"Equipped"))["Sword"], Is.EqualTo(cut == "P-before" ? "SimpleSword" : "MasterSword"));
            Assert.That(f.Stored["futureGameKey"], Is.EqualTo("retain"));
        }
    }

    [Test]
    public void Revival_EquipmentTransaction_UnfinishedDuplicateCannotReportCompletedOrWriteAgain()
    {
        using (var f = new EquipmentFixture(Path.Combine(_root, "duplicate")))
        {
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
            object old = hook.GetValue(null); var intent = f.Quote();
            try
            {
                hook.SetValue(null, (Action<string,string,string>)((p,t,s) => { throw new IOException("Synthetic unresolved materialization"); }));
                Assert.That(f.Buy(intent), Is.EqualTo("RecoveryRequired"));
            }
            finally { hook.SetValue(null, old); }
            string progress = File.ReadAllText(f.SavePath), inventory = File.ReadAllText(f.InventoryPath);
            Assert.That(f.Buy(intent), Is.EqualTo("CommittedPendingMaterialization"));
            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(progress));
            Assert.That(File.ReadAllText(f.InventoryPath), Is.EqualTo(inventory));
            Assert.That(f.Ready, Is.False);
            Assert.That(Call(f.Data,"PrepareAllyPurchase","Bat",10),Is.Null);
            Assert.That(Call(f.Data,"TryGrantNoAds"),Is.EqualTo(false));
            Assert.That(()=>Call(f.Data,"SaveLocalData"),Throws.Exception);
            Assert.That(()=>Call(f.Data,"ReadInventory"),Throws.Exception);
            f.FirstRead(); Assert.That(f.Uploads,Is.Empty);
            f.Reauthenticate(); f.FirstRead();
            Assert.That(f.Stored["Gold"], Is.EqualTo("90"));
        }
    }

    [TestCase("missing")]
    [TestCase("session")]
    [TestCase("third-state")]
    [TestCase("owner")]
    public void Revival_EquipmentTransaction_ConflictingRecoveryPreservesSourcesAndStopsWrites(string change)
    {
        using (var f = new EquipmentFixture(Path.Combine(_root, "conflict")))
        {
            var hook = Runtime("AD.PlayerInventoryStore").GetField("InventoryWriteCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
            object old = hook.GetValue(null);
            try
            {
                hook.SetValue(null, (Action<string,string,string>)((p,t,s) => { throw new IOException("Synthetic I pause"); }));
                f.Buy(f.Quote());
            }
            finally { hook.SetValue(null, old); }
            string inventory = File.ReadAllText(f.InventoryPath);
            if (change == "missing") File.Delete(f.InventoryPath);
            if (change == "session") File.WriteAllText(f.InventoryPath, inventory.Replace(new string('a',32),new string('b',32)));
            if (change == "third-state") File.WriteAllText(f.InventoryPath, inventory.Replace("SimpleSword", "MasterSword"));
            if (change == "owner") File.WriteAllText(f.InventoryPath, inventory.Replace("\"Owner\":\"A\"", "\"Owner\":\"B\""));
            string progress = File.ReadAllText(f.SavePath);
            string source = change == "missing" ? null : File.ReadAllText(f.InventoryPath);
            Assert.That(() => f.Reauthenticate(), Throws.Exception);
            Assert.That(f.Ready, Is.False);
            Assert.That(Call(f.Data,"TryUpdateLocalData","Gold","100"),Is.EqualTo(false));
            Assert.That(File.ReadAllText(f.SavePath), Is.EqualTo(progress));
            Assert.That(change == "missing" ? null : File.ReadAllText(f.InventoryPath), Is.EqualTo(source));
        }
    }

    [Test]
    public void Revival_EquipmentTransaction_CompleteDuplicatePreservesLaterGoldAndDirectEquip()
    {
        using (var f = new EquipmentFixture(Path.Combine(_root, "completed")))
        {
            var intent = f.Quote(); var competing=f.Quote(); Assert.That(f.Buy(intent),Is.EqualTo("Completed"));
            Assert.That(f.Buy(competing),Is.EqualTo("Rejected"),"A different quote cannot reuse the completed sequence.");
            Call(f.Data,"TryUpdateLocalData","Gold","120");
            Call(f.Data,"WriteInventory","A",1,new[]{"Bat"},new[]{"SimpleSword","SimpleShield","MasterSword"},
                new Dictionary<string,string>{{"Sword","SimpleSword"},{"Shield","SimpleShield"}});
            string progress = File.ReadAllText(f.SavePath), inventory = File.ReadAllText(f.InventoryPath);
            Assert.That(f.Buy(intent),Is.EqualTo("AlreadyCommittedCompleted"));
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(progress));
            Assert.That(File.ReadAllText(f.InventoryPath),Is.EqualTo(inventory));
            Assert.That(Call(f.Data,"TryGrantNoAds"),Is.EqualTo(true),"No Ads persistence must not require a Player.");
            Assert.That(f.Uploads.All(p => !p.ContainsKey("__TamerEquipmentTransaction")),Is.True);
        }
    }

    [Test]
    public void Revival_EquipmentTransaction_CompletedCursorDoesNotRecreateLostInventoryAsEmpty()
    {
        using(var f=new EquipmentFixture(Path.Combine(_root,"lost-completed-inventory")))
        {
            Assert.That(f.Buy(f.Quote()),Is.EqualTo("Completed"));
            string progress=File.ReadAllText(f.SavePath);
            File.Delete(f.InventoryPath);
            Assert.That(()=>f.Restart(),Throws.Exception);
            Assert.That(f.Ready,Is.False);
            Assert.That(File.Exists(f.InventoryPath),Is.False);
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(progress));
        }
    }

    [TestCase("transaction-extra")]
    [TestCase("outer-type")]
    [TestCase("truncated")]
    [TestCase("extra-root")]
    public void Revival_EquipmentTransaction_UnreadableMetadataNeverWritesFallbackOrInventory(string change)
    {
        using (var f = new EquipmentFixture(Path.Combine(_root,"unreadable")))
        {
            Assert.That(f.Buy(f.Quote()),Is.EqualTo("Completed"));
            var stored = f.Stored.ToDictionary(p=>p.Key,p=>(object)p.Value);
            if(change=="transaction-extra")
            {
                string metadata=(string)stored["__TamerEquipmentTransaction"];
                stored["__TamerEquipmentTransaction"]=metadata.Insert(metadata.Length-1,",\"unexpected\":\"reject\"");
            }
            if(change=="outer-type") stored["__TamerEquipmentTransaction"]=new Dictionary<string,object>();
            string raw=(string)Call(Runtime("AD.Utility"),"SerializeToJson",stored);
            if(change=="truncated")raw=raw.Substring(0,raw.Length-1);
            if(change=="extra-root")raw+="{}";
            File.WriteAllText(f.SavePath,raw);
            string inventory=File.ReadAllText(f.InventoryPath);
            Assert.That(()=>f.Restart(),Throws.Exception);
            Assert.That(f.Data.GetType().GetProperty("HasKnownAccount").GetValue(f.Data),Is.EqualTo(true));
            Assert.That(Call(f.Data,"TryUpdateLocalData","Gold","999"),Is.EqualTo(false));
            Assert.That(()=>Call(f.Data,"SaveLocalData"),Throws.Exception);
            f.FirstRead();
            Assert.That(f.Uploads,Is.Empty);
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(raw));
            Assert.That(File.ReadAllText(f.InventoryPath),Is.EqualTo(inventory));
            Assert.That(Directory.Exists(Path.Combine(Path.GetDirectoryName(f.SavePath),"PlayerDataBackups")),Is.False);
        }
    }

    [Test]
    public void Revival_EquipmentTransaction_LegacyUnknownGameKeyAndPlayerlessNoAdsArePreserved()
    {
        using(var f=new EquipmentFixture(Path.Combine(_root,"legacy")))
        {
            f.Restart(); f.FirstRead();
            Assert.That(f.Ready,Is.True);
            Assert.That(f.Local["futureGameKey"],Is.EqualTo("retain"));
            Assert.That(f.Stored.ContainsKey("__TamerEquipmentTransaction"),Is.False);
            Assert.That(Call(f.Data,"TryGrantNoAds"),Is.EqualTo(true));
            Assert.That(f.Stored["GooglePlay"],Is.EqualTo("ProductNoAds,FutureProduct"));
        }
    }

    [Test]
    public void Revival_EquipmentTransaction_UnreadableProgressIsNotPublishedAsNewAccountDefaults()
    {
        using(var f=new EquipmentFixture(Path.Combine(_root,"locked-progress")))
        {
            string progress=File.ReadAllText(f.SavePath),inventory=File.ReadAllText(f.InventoryPath);
            using(var held=new FileStream(f.SavePath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                Assert.That(()=>f.Restart(),Throws.Exception);
                Assert.That(f.Ready,Is.False);
                Assert.That(f.Data.GetType().GetProperty("HasKnownAccount").GetValue(f.Data),Is.EqualTo(true));
                Assert.That(Field(f.Data,"LocalPlayerData"),Is.Null);
                Assert.That(Call(f.Data,"TryUpdateLocalData","Gold","999"),Is.EqualTo(false));
                Assert.That(()=>Call(f.Data,"SaveLocalData"),Throws.Exception);
                f.FirstRead();Assert.That(f.Uploads,Is.Empty);
            }
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(progress));
            Assert.That(File.ReadAllText(f.InventoryPath),Is.EqualTo(inventory));
        }
    }

    [Test]
    public void Revival_EquipmentTransaction_DeletionGuardAndOtherAccountPrecedeMaterialization()
    {
        using(var f=new EquipmentFixture(Path.Combine(_root,"deletion-guard")))
        {
            f.PauseAfterProgressCommit();
            string progress=File.ReadAllText(f.SavePath),inventory=File.ReadAllText(f.InventoryPath);
            Assert.That(()=>Call(f.Data,"BeginAccountSession","B"),Throws.Exception);
            var guard=Runtime("AD.Privacy.DeletionRecoveryGuard").GetProperty("HasPendingSubmission");
            object old=guard.GetValue(null);
            try
            {
                guard.SetValue(null,(Func<string,bool>)(account=>account=="A"));
                f.Reauthenticate();
                Assert.That(f.Data.GetType().GetProperty("DeletionInProgress").GetValue(f.Data),Is.EqualTo(true));
                Assert.That(f.Ready,Is.False);
                Assert.That(Call(f.Data,"TryGrantNoAds"),Is.EqualTo(false));
            }
            finally{guard.SetValue(null,old);}
            Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(progress));
            Assert.That(File.ReadAllText(f.InventoryPath),Is.EqualTo(inventory));
        }
    }

    [TestCase("generation","P")]
    [TestCase("deletion","P")]
    [TestCase("player","P")]
    [TestCase("generation","I")]
    [TestCase("deletion","I")]
    [TestCase("player","I")]
    public void Revival_EquipmentTransaction_PreReplaceFenceRejectsChangedContext(string change,string file)
    {
        using(var f=new EquipmentFixture(Path.Combine(_root,"fence")))
        {
            var intent=f.Quote(); string progress=File.ReadAllText(f.SavePath),inventory=File.ReadAllText(f.InventoryPath);
            var hook=Runtime(file=="P"?"AD.DataManager":"AD.PlayerInventoryStore").GetField(
                file=="P"?"JournalWriteCheckpoint":"InventoryWriteCheckpoint",BindingFlags.NonPublic|BindingFlags.Static);
            object old=hook.GetValue(null); bool current=true;
            try
            {
                hook.SetValue(null,(Action<string,string,string>)((p,t,s)=>
                {
                    if(p!=(file=="P"?f.SavePath:f.InventoryPath)||s!="temporary-closed")return;
                    if(change=="generation")Set(f.Data,"_accountGeneration",2);
                    if(change=="deletion")Set(f.Data,"<DeletionEpoch>k__BackingField",1);
                    if(change=="player")current=false;
                }));
                Assert.That(f.Buy(intent,()=>current),Is.EqualTo("RecoveryRequired"));
            }
            finally{hook.SetValue(null,old);}
            if(file=="P")Assert.That(File.ReadAllText(f.SavePath),Is.EqualTo(progress));
            else
            {
                Assert.That(f.Stored["Gold"],Is.EqualTo("90"));
                Assert.That(f.Stored["__TamerEquipmentTransaction"],Does.Contain("\"phase\":\"Materializing\""));
            }
            Assert.That(File.ReadAllText(f.InventoryPath),Is.EqualTo(inventory));
        }
    }

    [Test] public void Revival_InventoryStoreRestartsAndSwitchesWithoutImportOrReset()
    {
        Directory.CreateDirectory(_root);
        string legacy = Path.Combine(_root,"legacy-original.txt"); File.WriteAllText(legacy,"AllyMonsters=Crab;playerEquippedItems=MasterSword;LocalItem=MasterSword");
        Assert.That((IEnumerable)Value(Load("A"),"Collection"),Is.Empty);
        Save("A",new[]{"Bat"},new[]{"SimpleSword"},"SimpleSword");
        _store=NewStore();
        Assert.That((IEnumerable)Value(Load("A"),"Collection"),Is.EqualTo(new[]{"Bat"}));
        Assert.That((IEnumerable)Value(Load("B"),"OwnedItems"),Is.Empty);
        Save("B",new[]{"Crab"},Array.Empty<string>());
        Assert.That((IEnumerable)Value(Load("A"),"OwnedItems"),Is.EqualTo(new[]{"SimpleSword"}));
        Assert.That(File.ReadAllText(legacy),Is.EqualTo("AllyMonsters=Crab;playerEquippedItems=MasterSword;LocalItem=MasterSword"));
    }

    [Test] public void Revival_InventoryStoreRejectsUnknownOwnerAndNeverBuildsUserPath()
    {
        Assert.Throws<InvalidDataException>(()=>Load(null));
        Assert.Throws<InvalidDataException>(()=>Save("",Array.Empty<string>(),Array.Empty<string>()));
        Assert.That(Directory.Exists(_root),Is.False);
        Assert.That(Path.GetDirectoryName(PathFor("../../foreign")),Is.EqualTo(_root));
    }

    [Test] public void Revival_InventoryStoreRejectsForeignMalformedAndInvalidRecordsWithoutReplacing()
    {
        Save("A",new[]{"Bat"},new[]{"SimpleSword"},"SimpleSword");
        string path=PathFor("A"), original=File.ReadAllText(path);
        Assert.Throws<InvalidDataException>(()=>Save("A",new[]{"Unknown"},new[]{"SimpleSword"}));
        Assert.That(File.ReadAllText(path),Is.EqualTo(original));
        File.WriteAllText(PathFor("B"),original);
        Assert.Throws<InvalidDataException>(()=>Load("B"));
        Assert.That(File.ReadAllText(PathFor("B")),Is.EqualTo(original));
        File.WriteAllText(path,"{broken");
        Assert.That(()=>Save("A",Array.Empty<string>(),Array.Empty<string>()),Throws.Exception);
        Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));
    }

    [Test] public void Revival_InventoryStoreLockedReplacePreservesOriginalAndNewSessionReadsIt()
    {
        Save("A",new[]{"Bat"},new[]{"SimpleSword"},"SimpleSword");
        string path=PathFor("A"), original=File.ReadAllText(path);
        using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>Save("A",new[]{"Crab"},new[]{"MasterSword"},"MasterSword"));
        Assert.That(File.ReadAllText(path),Is.EqualTo(original));
        _store=NewStore();
        Assert.That((IEnumerable)Value(Load("A"),"Collection"),Is.EqualTo(new[]{"Bat"}));
        Save("A",new[]{"Crab"},new[]{"MasterSword"},"MasterSword");
        Assert.That(((IDictionary<string,string>)Value(Load("A"),"Equipped"))["Sword"],Is.EqualTo("MasterSword"));
    }

    [Test] public void Revival_InventorySessionRejectsUnreadyAndStaleCallbacksWithoutWriting()
    {
        var root=new GameObject("Inventory session guard"); root.SetActive(false);
        try
        {
            var type=Runtime("AD.DataManager"); var data=root.AddComponent(type);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            type.GetField("<PlayFabId>k__BackingField",flags).SetValue(data,"A");
            Assert.Throws<InvalidOperationException>(()=>Call(data,"WriteInventory","A",0,Array.Empty<string>(),Array.Empty<string>(),Slots()));
            type.GetField("<IsServerDataReady>k__BackingField",flags).SetValue(data,true);
            Assert.Throws<InvalidOperationException>(()=>Call(data,"WriteInventory","B",0,Array.Empty<string>(),Array.Empty<string>(),Slots()));
            Assert.Throws<InvalidOperationException>(()=>Call(data,"WriteInventory","A",-1,Array.Empty<string>(),Array.Empty<string>(),Slots()));
            type.GetField("<DeletionInProgress>k__BackingField",flags).SetValue(data,true);
            Assert.Throws<InvalidOperationException>(()=>Call(data,"WriteInventory","A",0,Array.Empty<string>(),Array.Empty<string>(),Slots()));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Assert.That(Directory.Exists(_root),Is.False);
    }

    [Test] public void Revival_InventoryTransitionClearsTargetsAndRejectsOldCollectionCallback()
    {
        var root=new GameObject("Inventory callback guard"); root.SetActive(false);
        var target=new GameObject("Old account target"); target.SetActive(false);
        try
        {
            var type=Runtime("Player"); var player=root.AddComponent(type);
            var monster=target.AddComponent(Runtime("Monster"));
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            Action<string,object> set=(name,value)=>type.GetField(name,flags).SetValue(player,value);
            set("_inventoryOwner","A"); set("_inventoryGeneration",1);
            set("_curTargetMonsterObject",target); set("_curTargetMonster",monster);
            set("_targetInventoryOwner","A"); set("_targetInventoryGeneration",1);
            set("_ableCaptureMonster",monster); set("_captureTrigger",target.AddComponent<BoxCollider>());
            Call(player,"ClearInventorySession");
            foreach(var field in new[]{"_curTargetMonsterObject","_curTargetMonster","_ableCaptureMonster","_captureTrigger"})
                Assert.That(type.GetField(field,flags).GetValue(player),Is.Null);
            set("_inventoryOwner","B"); set("_inventoryGeneration",2);
            // Even a retained/reintroduced old target cannot use the newly bound inventory identity.
            set("_curTargetMonsterObject",target); set("_curTargetMonster",monster);
            set("_targetInventoryOwner","A"); set("_targetInventoryGeneration",1);
            type.GetMethod("RecordDefeatedMonster",flags).Invoke(player,new object[]{target});
            Assert.That((IEnumerable)type.GetField("PlayerMonsterCollection").GetValue(player),Is.Empty);
            Assert.That(Directory.Exists(_root),Is.False); // No Managers/authentication/storage was required by the rejected callback.
        }
        finally { UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void Revival_InventoryDeletionOnlyRemovesBoundOwnerAndSession()
    {
        Call(_store,"BindSession","A",new string('a',32));
        Save("A",new[]{"Bat"},new[]{"SimpleSword"},"SimpleSword");
        Call(_store,"BindSession","B",new string('b',32));
        var other=File.ReadAllBytes(PathFor("B"));
        Assert.Throws<InvalidDataException>(()=>DeleteBound("A",new string('a',32),owner=>owner=="B"));
        Assert.That(File.Exists(PathFor("A")),Is.True);
        DeleteBound("A",new string('a',32),owner=>owner=="A");
        DeleteBound("A",new string('a',32),owner=>owner=="A");
        Assert.That(File.Exists(PathFor("A")),Is.False);
        Assert.That(File.ReadAllBytes(PathFor("B")),Is.EqualTo(other));
    }

    [Test] public void Revival_InventoryDeletionPreservesNewSessionAndUnboundLegacy()
    {
        Save("A",new[]{"Bat"},Array.Empty<string>());
        Assert.Throws<InvalidDataException>(()=>DeleteBound("A",null,owner=>true));
        Call(_store,"BindSession","A",new string('a',32));
        Call(NewStore(),"BindSession","A",new string('b',32));
        string fresh=File.ReadAllText(PathFor("A"));
        Assert.Throws<InvalidDataException>(()=>DeleteBound("A",new string('a',32),owner=>true));
        Assert.That(File.ReadAllText(PathFor("A")),Is.EqualTo(fresh));
        Assert.That((IEnumerable)Value(Load("A"),"Collection"),Is.EqualTo(new[]{"Bat"}));
    }

    [Test] public void Revival_InventoryDeletionAccessFailureIsNotAbsentFile()
    {
        Call(_store,"BindSession","A",new string('a',32));
        using(var held=new FileStream(PathFor("A"),FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>DeleteBound("A",new string('a',32),owner=>true));
        Assert.That(File.Exists(PathFor("A")),Is.True);
        DeleteBound("A",new string('a',32),owner=>true);
        Directory.CreateDirectory(PathFor("A"));
        Assert.Throws<UnauthorizedAccessException>(()=>DeleteBound("A",new string('a',32),owner=>true));
        Directory.Delete(PathFor("A")); Directory.Delete(_root); File.WriteAllText(_root,"preserve");
        try { Assert.Throws<IOException>(()=>DeleteBound("A",new string('a',32),owner=>true)); }
        finally { File.Delete(_root); }
    }
}
