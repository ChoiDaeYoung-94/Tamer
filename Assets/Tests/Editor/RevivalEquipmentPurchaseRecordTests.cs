using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Pure bounded metadata tests. No Managers, Player, SDK, Editor instance, or live files.
public class RevivalEquipmentPurchaseRecordTests
{
    private const string Key = "__TamerEquipmentTransaction";
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args)
    {
        try { return (target as Type ?? target.GetType()).GetMethod(name, Members).Invoke(target is Type ? null : target, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    private static object Snapshot(bool after) => Call(Runtime("AD.PlayerInventorySnapshot"), "Validate", "account-A", "account-A",
        new[] { "Bat" }, after ? new[] { "SimpleSword", "SimpleShield", "MasterSword" } : new[] { "SimpleSword", "SimpleShield" },
        new Dictionary<string, string> { ["Sword"] = after ? "MasterSword" : "SimpleSword", ["Shield"] = "SimpleShield" },
        new[] { "Bat" }, new Dictionary<string, string> { ["SimpleSword"] = "Sword", ["MasterSword"] = "Sword", ["SimpleShield"] = "Shield" });
    private static object Record() => Call(Runtime("AD.EquipmentPurchaseRecord"), "Create", "account-A", 1L,
        new string('a', 32), "MasterSword", "Sword", 10, 100, 90, 1L, new string('b', 32), Snapshot(false), Snapshot(true));
    private static string Serialize(object record) => (string)Call(record, "Serialize");
    private static string Outer(object metadata, string unknown = "keep") => (string)Call(Runtime("AD.Utility"), "SerializeToJson",
        new Dictionary<string, object> { ["__TamerAccountOwner"] = "account-A", [Key] = metadata, ["futureGameKey"] = unknown });

    [Test]
    public void Revival_EquipmentRecord_RoundTripsPendingAndCompletedCursorWithoutHistoricalEffects()
    {
        string pending = Serialize(Record());
        Assert.That(Serialize(Call(Runtime("AD.EquipmentPurchaseRecord"), "Parse", pending)), Is.EqualTo(pending));
        string completed = Serialize(Call(Record(), "Complete"));
        Assert.That(completed, Does.Contain("\"phase\":\"Complete\""));
        Assert.That(completed, Does.Not.Contain("goldAfter"));
        Assert.That(completed, Does.Not.Contain("inventory"));
        Assert.That(Serialize(Call(Runtime("AD.EquipmentPurchaseRecord"), "Parse", completed)), Is.EqualTo(completed));
    }

    [TestCase("extra-root")]
    [TestCase("extra-payment")]
    [TestCase("duplicate")]
    [TestCase("size")]
    [TestCase("number-type")]
    [TestCase("revision-format")]
    [TestCase("version")]
    public void Revival_EquipmentRecord_RejectsTransactionSchemaViolations(string change)
    {
        string json = Serialize(Record());
        if (change == "extra-root") json = json.Insert(json.Length - 1, ",\"extra\":\"reject\"");
        if (change == "extra-payment") json = json.Replace("\"goldBefore\":", "\"extra\":\"reject\",\"goldBefore\":");
        if (change == "duplicate") json = json.Insert(1, "\"version\":\"1\",");
        if (change == "size") json = new string(' ', 1024 * 1024) + json;
        if (change == "number-type") json = json.Replace("\"sequence\":\"1\"", "\"sequence\":1");
        if (change == "revision-format") json = json.Replace("\"goldRevision\":\"1\"", "\"goldRevision\":\"01\"");
        if (change == "version") json = json.Replace("\"version\":\"1\"", "\"version\":\"2\"");
        Assert.That(() => Call(Runtime("AD.EquipmentPurchaseRecord"), "Parse", json), Throws.Exception);
    }

    [Test]
    public void Revival_EquipmentRecord_PreservesUnknownOuterGameKeysOutsideTransactionSizeLimit()
    {
        string metadata = Serialize(Record());
        string outer = Outer(metadata, new string('x', 1024 * 1024 + 1));
        Assert.That(Call(Runtime("AD.EquipmentPurchaseRecord"), "Extract", outer), Is.EqualTo(metadata));
        var values = (Dictionary<string, string>)Call(Runtime("AD.DataManager"), "ParseData", outer);
        Assert.That(values["futureGameKey"].Length, Is.EqualTo(1024 * 1024 + 1));
        Assert.That(Call(Runtime("AD.EquipmentPurchaseRecord"), "Extract", "{\"legacyUnknown\":\"retain\"}"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_EquipmentRecord_RejectsOuterReservedDuplicateOrNonString(bool duplicate)
    {
        string json = duplicate ? Outer(Serialize(Record())).Insert(1, "\"__TamerEquipmentTransaction\":\"duplicate\",") : Outer(new Dictionary<string, object>());
        Assert.That(() => Call(Runtime("AD.EquipmentPurchaseRecord"), "Extract", json), Throws.Exception);
    }

    [Test]
    public void Revival_EquipmentRecord_CannotBecomePendingServerData()
    {
        var changes = Activator.CreateInstance(Runtime("AD.PlayerDataChanges"));
        Assert.That(() => Call(changes, "Track", Key, Serialize(Record())), Throws.Exception);
    }

    [TestCase("{\"legacyUnknown\":\"retain\"")]
    [TestCase("{\"legacyUnknown\":{\"nested\":\"retain\"")]
    [TestCase("{\"legacyUnknown\":\"retain\"}{}")]
    [TestCase("{\"legacyUnknown\":\"retain\"} true")]
    public void Revival_EquipmentRecord_RejectsTruncatedOuterOrAdditionalRootToken(string json)
    {
        Assert.That(() => Call(Runtime("AD.EquipmentPurchaseRecord"), "Extract", json), Throws.Exception);
    }
}
