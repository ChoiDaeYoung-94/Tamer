using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class RevivalGameSaveTests
{
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD." + name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args)
    {
        try { return (target as Type ?? target.GetType()).GetMethod(name).Invoke(target is Type ? null : target, args); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    private string _root;
    private readonly List<IDisposable> _sessions = new List<IDisposable>();
    private const string Package = "com.AeDeong.MonsterTamer.revival.gamesave";
    private static Type Schema => TypeOf("RevivalGameSaveSchema");
    [SetUp] public void Setup() => _root = Path.Combine(Path.GetTempPath(), "GameSaveRegression-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void Cleanup()
    {
        foreach (var session in _sessions) session.Dispose();
        _sessions.Clear();
        if (Directory.Exists(_root)) Directory.Delete(_root, true); // Unique test-owned root only.
    }
    private object Open(string slot = "primary")
    {
        var session = Call(TypeOf("RevivalGameSaveSession"), "Offline", _root, Package, slot);
        _sessions.Add((IDisposable)session);
        return session;
    }
    [Test]
    public void Revival_GameSave_TwoFixturesSurvivePendingRestartAndFreshReadOnlyRestore()
    {
        var session = Open();
        Assert.Throws<InvalidOperationException>(() => Call(session, "Prepare", 1));
        Call(session, "Read");
        Assert.That(Get<int>(session, "CloudStep"), Is.Zero);
        Assert.That(Get<int>(session, "Writes"), Is.Zero);
        for (int step = 1; step <= 2; step++)
        {
            Call(session, "Prepare", step);
            Assert.That(Get<int>(session, "PendingCount"), Is.EqualTo(step == 1 ? 8 : 7));
            ((IDisposable)session).Dispose();
            session = Open();
            Call(session, "Read");
            Call(session, "Verify", step, true);
            Assert.That(Get<int>(session, "Writes"), Is.Zero);
            Call(session, "Upload");
            Call(session, "Verify", step, false);
            Assert.That(Get<int>(session, "Writes"), Is.EqualTo(1));
            string path = Get<string>(session, "SavePath");
            string original = File.ReadAllText(path);
            var restore = Open("restore" + step);
            Assert.That(File.Exists(Get<string>(restore, "SavePath")), Is.False);
            Call(restore, "Read");
            Call(restore, "Verify", step, false);
            Assert.That(Get<int>(restore, "Writes"), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => Call(restore, "Prepare", 2));
            Assert.Throws<InvalidOperationException>(() => Call(restore, "Upload"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            Assert.Throws<InvalidOperationException>(() => Open("restore" + step));
            ((IDisposable)session).Dispose();
            session = Open(); Call(session, "Read"); Call(session, "Verify", step, false);
            Assert.That(Get<int>(session, "Writes"), Is.Zero);
            var values = Get<Dictionary<string, string>>(session, "Values");
            Assert.That(values.Count, Is.EqualTo(10));
            Assert.That(values["GooglePlay"], Is.Empty);
            Assert.That(values["GoogleAdMob"], Is.EqualTo("null"));
        }
    }
    [TestCase("GooglePlay", "ProductNoAds")]
    [TestCase("GoogleAdMob", "null")]
    [TestCase("__TamerPendingJournal", "null")]
    [TestCase("__TamerAccountOwner", "owner")]
    [TestCase("LocalItem", "SimpleSword")]
    [TestCase("Gold", null)]
    [TestCase("Gold", "9999")]
    public void Revival_GameSave_RejectsOutOfScopePatch(string key, string value)
    {
        Assert.Throws<InvalidDataException>(() => Call(Schema, "ValidatePatch", new Dictionary<string, string> { [key] = value }));
    }
    [TestCase("progress-probe-0123456789abcdef0123456789abcdef")]
    [TestCase("gameplay-save-0123456789abcdef0123456789abcdef\n")]
    [TestCase("")]
    public void Revival_GameSave_RejectsOtherOrMalformedIdentity(string value)
        => Assert.Throws<ArgumentException>(() => Call(Schema, "CreateLoginRequest", value));
    [Test]
    public void Revival_GameSave_OnlyProvisionedIdentityAndExplicitEightReadKeys()
    {
        var request = Call(Schema, "CreateLoginRequest", "gameplay-save-0123456789abcdef0123456789abcdef");
        Assert.That(request.GetType().GetField("CreateAccount").GetValue(request), Is.EqualTo(false));
        CollectionAssert.AreEquivalent(new[] { "NickName", "Sex", "Tutorial", "Gold", "Power", "AttackSpeed", "MoveSpeed", "AllyMonsters" },
            (List<string>)Call(Schema, "ReadKeys"));
    }
    [Test]
    public void Revival_GameSave_MalformedSyntheticServerPreservesPendingAndBlocksWrite()
    {
        var session = Open(); Call(session, "Read"); Call(session, "Prepare", 1);
        string path = Get<string>(session, "SavePath"); string bytes = File.ReadAllText(path);
        ((IDisposable)session).Dispose();
        string cloud = Path.Combine(_root, "GameSaveHarnessV1", "offline", "SyntheticServer.json");
        File.WriteAllText(cloud, "{\"Gold\":\"9999\"}");
        session = Open(); Call(session, "Read");
        Assert.That(Get<bool>(session, "HasFailed"), Is.True);
        Assert.Throws<InvalidOperationException>(() => Call(session, "Upload"));
        Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
        Assert.That(File.ReadAllText(cloud), Is.EqualTo("{\"Gold\":\"9999\"}"));
        Assert.That(Get<int>(session, "Writes"), Is.Zero);
    }
    [Test]
    public void Revival_GameSave_LateWriteAcknowledgementAfterSessionChangeCannotClearJournal()
    {
        bool current = true;
        Action written = null;
        var snapshot = (Dictionary<string, string>)Call(Schema, "Fixture", 1);
        string path = (string)Call(Schema, "SavePath", _root, Package, "offline", "primary");
        var session = Activator.CreateInstance(TypeOf("RevivalGameSaveSession"), path, "synthetic-stale-owner",
            (Func<bool>)(() => current), false,
            (Action<string, Action<Dictionary<string, string>>, Action<int>>)((account, ok, fail) => ok(snapshot)),
            (Action<string, Dictionary<string, string>, Action, Action<int>>)((account, patch, ok, fail) => written = ok));
        _sessions.Add((IDisposable)session);
        Call(session, "Read"); Call(session, "Prepare", 2); Call(session, "Upload");
        Assert.That(written, Is.Not.Null);
        string bytes = File.ReadAllText(path);
        current = false;
        written();
        Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
        Assert.That(Get<int>(session, "PendingCount"), Is.EqualTo(7));
        Assert.That(Get<bool>(session, "CanMutate"), Is.False);
    }
    [Test]
    public void Revival_GameSave_UnexpectedReadKeyRejectsEntireSnapshotBeforeLocalWrite()
    {
        var snapshot = (Dictionary<string, string>)Call(Schema, "Fixture", 1);
        snapshot.Add("GooglePlay", "ProductNoAds");
        string path = (string)Call(Schema, "SavePath", _root, Package, "offline", "primary");
        var session = Activator.CreateInstance(TypeOf("RevivalGameSaveSession"), path, "synthetic-read-owner",
            (Func<bool>)(() => true), false,
            (Action<string, Action<Dictionary<string, string>>, Action<int>>)((account, ok, fail) => ok(snapshot)),
            (Action<string, Dictionary<string, string>, Action, Action<int>>)((account, patch, ok, fail) => Assert.Fail("Unexpected write")));
        _sessions.Add((IDisposable)session);
        Call(session, "Read");
        Assert.That(Get<bool>(session, "HasFailed"), Is.True);
        Assert.That(File.Exists(path), Is.False);
        Assert.That(Get<int>(session, "Writes"), Is.Zero);
    }
    [Test]
    public void Revival_GameSave_SourceHooksExcludedAndOfflineManifestBlocksNetwork()
    {
        foreach (string file in new[] { "RevivalGameSaveSchema", "RevivalGameSaveSession", "RevivalGameSaveHarness", "Managers/DataManager.GameSaveHarness" })
            Assert.That(File.ReadAllText("Assets/Scripts/" + file + ".cs").Trim(), Does.StartWith("#if UNITY_EDITOR || TAMER_GAMESAVE_HARNESS"));
        var build = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalGameSaveBuild")).First(t => t != null);
        string manifest = (string)Call(build, "GameSaveManifest", "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application /></manifest>");
        var document = System.Xml.Linq.XDocument.Parse(manifest);
        System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
        System.Xml.Linq.XNamespace tools = "http://schemas.android.com/tools";
        foreach (string key in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE", "com.android.vending.BILLING", "com.google.android.gms.permission.AD_ID" })
            Assert.That(document.Root.Elements("uses-permission").Single(e => (string)e.Attribute(android + "name") == key).Attribute(tools + "node").Value, Is.EqualTo("remove"));
    }
}
