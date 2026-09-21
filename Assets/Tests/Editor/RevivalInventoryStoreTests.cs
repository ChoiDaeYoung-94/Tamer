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
        try { return target.GetType().GetMethod(method).Invoke(target,args); }
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
    [SetUp] public void Setup() { _root = Path.Combine(Path.GetTempPath(),"TamerInventory-"+Guid.NewGuid().ToString("N")); _store=NewStore(); }
    [TearDown] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }

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
