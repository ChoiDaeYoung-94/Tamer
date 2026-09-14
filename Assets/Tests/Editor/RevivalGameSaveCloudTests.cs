using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Internal;
using PlayFab.SharedModels;

public class RevivalGameSaveCloudTests
{
    private const string Package = "com.AeDeong.MonsterTamer.revival.gamesavecloud";
    private const string Account = "0123456789ABCDEF";
    private const string Custom = "gameplay-save-0123456789abcdef0123456789abcdef";
    private const string Ticket = "synthetic-cloud-ticket";
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD." + name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args)
    {
        try { return (target as Type ?? target.GetType()).GetMethod(method).Invoke(target is Type ? null : target, args); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }
    private static T Get<T>(object target, string property) => (T)target.GetType().GetProperty(property).GetValue(target);
    private sealed class Transport : ITransportPlugin
    {
        public bool IsInitialized => true;
        public readonly List<CallRequestContainer> Calls = new List<CallRequestContainer>();
        public void Initialize() => throw new InvalidOperationException("No real HTTP in regression");
        public void MakeApiCall(object request) => Calls.Add((CallRequestContainer)request);
        public void Update() { }
        public void OnDestroy() { }
        public int GetPendingMessages() => 0;
        public void SimpleGetCall(string u, Action<byte[]> s, Action<string> e) => throw new NotSupportedException();
        public void SimplePutCall(string u, byte[] p, Action<byte[]> s, Action<string> e) => throw new NotSupportedException();
        public void SimplePostCall(string u, byte[] p, Action<byte[]> s, Action<string> e) => throw new NotSupportedException();
    }
    private Transport _transport;
    private ITransportPlugin _original;
    private object _auth;
    private string _root;
    [SetUp] public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "GameSaveCloud-" + Guid.NewGuid().ToString("N"));
        _original = PluginManager.GetPlugin<ITransportPlugin>(PluginContract.PlayFab_Transport);
        _transport = new Transport(); PluginManager.SetPlugin(_transport, PluginContract.PlayFab_Transport);
        _auth = Activator.CreateInstance(TypeOf("RevivalGameSaveAuthentication"));
    }
    [TearDown] public void Cleanup()
    {
        (_auth as IDisposable)?.Dispose();
        PluginManager.SetPlugin(_original, PluginContract.PlayFab_Transport);
        if (Directory.Exists(_root)) Directory.Delete(_root, true); // Unique test-owned root only.
    }
    private void Login() => Call(_auth, "Login", Package, "12B656", Custom, Account);
    private static LoginResult Result(string id = Account) => new LoginResult { PlayFabId = id, SessionTicket = Ticket, NewlyCreated = false };
    private static void Reply(CallRequestContainer call, PlayFabResultCommon result)
    { call.ApiResult = result; call.InvokeSuccessCallback(); }
    private object Connect() => Call(_auth, "Connect", _root, Package, "12B656", "primary");

    [Test]
    public void Revival_GameSaveCloud_RealInstanceRequestsUseVerifiedLoginAndEightPrivateFields()
    {
        Assert.Throws<InvalidOperationException>(() => Connect());
        Assert.That(Directory.Exists(_root), Is.False);
        Login();
        var login = _transport.Calls.Single();
        Assert.That(login.settings.TitleId, Is.EqualTo("12B656"));
        Assert.That(((LoginWithCustomIDRequest)login.ApiRequest).CreateAccount, Is.False);
        Assert.That(((LoginWithCustomIDRequest)login.ApiRequest).CustomId, Is.EqualTo(Custom));
        Reply(login, Result());
        var session = Connect(); Call(session, "Read");
        var read = _transport.Calls.Last();
        Assert.That(read.ApiRequest, Is.TypeOf<GetUserDataRequest>());
        CollectionAssert.AreEquivalent((List<string>)Call(TypeOf("RevivalGameSaveSchema"), "ReadKeys"), ((GetUserDataRequest)read.ApiRequest).Keys);
        Assert.That(read.context.PlayFabId, Is.EqualTo(Account));
        Assert.That(read.context.ClientSessionTicket, Is.EqualTo(Ticket));
        Reply(read, new GetUserDataResult { Data = new Dictionary<string, UserDataRecord>() });
        Call(session, "Prepare", 1); Call(session, "Upload");
        var write = _transport.Calls.Last(); var request = (UpdateUserDataRequest)write.ApiRequest;
        var fixture = (Dictionary<string, string>)Call(TypeOf("RevivalGameSaveSchema"), "Fixture", 1);
        CollectionAssert.AreEquivalent(fixture, request.Data);
        Assert.That(request.Permission, Is.EqualTo(UserDataPermission.Private));
        Assert.That(request.KeysToRemove, Is.Null.Or.Empty);
        Assert.That(write.context, Is.SameAs(read.context));
        Assert.That(write.settings.TitleId, Is.EqualTo("12B656"));
        Reply(write, new UpdateUserDataResult());
        Reply(_transport.Calls.Last(), new GetUserDataResult { Data = fixture.ToDictionary(e => e.Key, e => new UserDataRecord { Value = e.Value }) });
        Call(session, "Verify", 1, false);
        foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            Assert.That(text, Does.Not.Contain(Custom).And.Not.Contain(Ticket));
        }
    }
    [TestCase("", Account, "12B656", Package)]
    [TestCase("progress-probe-0123456789abcdef0123456789abcdef", Account, "12B656", Package)]
    [TestCase(Custom, "", "12B656", Package)]
    [TestCase(Custom, "not-an-account", "12B656", Package)]
    [TestCase(Custom, Account, "67C9A", Package)]
    [TestCase(Custom, Account, "12B656", "com.AeDeong.MonsterTamer.revival.progress")]
    [TestCase(Custom, Account, "12B656", "com.AeDeong.MonsterTamer.revival.gamesave")]
    public void Revival_GameSaveCloud_InvalidIdentityTargetRejectsBeforeRequests(string custom, string expected, string title, string package)
    {
        Assert.That(() => Call(_auth, "Login", package, title, custom, expected), Throws.Exception);
        Assert.That(_transport.Calls, Is.Empty);
        Assert.That(Get<bool>(_auth, "IsAuthenticated"), Is.False);
        Assert.Throws<InvalidOperationException>(() => Connect());
        Assert.That(Directory.Exists(_root), Is.False);
    }
    [TestCase("mismatch")]
    [TestCase("missing-ticket")]
    [TestCase("newly-created")]
    public void Revival_GameSaveCloud_InvalidResponseCannotCreateBindingOrSave(string kind)
    {
        Login(); var result = Result();
        if (kind == "mismatch") result.PlayFabId = "FEDCBA9876543210";
        if (kind == "missing-ticket") result.SessionTicket = "";
        if (kind == "newly-created") result.NewlyCreated = true;
        Reply(_transport.Calls.Single(), result);
        Assert.That(Get<bool>(_auth, "IsAuthenticated"), Is.False);
        Assert.Throws<InvalidOperationException>(() => Connect());
        Assert.That(Directory.Exists(_root), Is.False);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void Revival_GameSaveCloud_LogoutOrReloginInvalidatesOldLoginSuccess(bool relogin)
    {
        Login(); var old = _transport.Calls.Single();
        if (relogin) Login(); else Call(_auth, "Disconnect");
        Reply(old, Result());
        Assert.That(Get<bool>(_auth, "IsAuthenticated"), Is.False);
        if (relogin)
        {
            Reply(_transport.Calls.Last(), Result());
            Assert.That(Get<bool>(_auth, "IsAuthenticated"), Is.True);
        }
    }
    [Test]
    public void Revival_GameSaveCloud_TimeoutRejectsLateSuccessAndOtherReceiptSessionApiIsAbsent()
    {
        ((IDisposable)_auth).Dispose();
        Action<LoginResult> success = null; Action timeout = null;
        var ctor = TypeOf("RevivalGameSaveAuthentication").GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        _auth = ctor.Invoke(new object[] {
            (Action<LoginWithCustomIDRequest, Action<LoginResult>, Action>)((r, ok, fail) => success = ok),
            (Action<TimeSpan, Action>)((delay, action) => { Assert.That(delay.TotalSeconds, Is.EqualTo(20)); timeout = action; }) });
        Login(); timeout(); success(Result());
        Assert.That(Get<bool>(_auth, "IsAuthenticated"), Is.False);
        Assert.Throws<InvalidOperationException>(() => Connect());
        Assert.That(TypeOf("RevivalGameSaveSession").GetMethod("Connect"), Is.Null);
        Assert.That(TypeOf("RevivalGameSaveAuthentication").GetMethods().SelectMany(m => m.GetParameters())
            .Any(p => p.ParameterType.Name.Contains("ReceiptSession")), Is.False);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void Revival_GameSaveCloud_LogoutOrReloginInvalidatesLateDataAcknowledgement(bool relogin)
    {
        Login(); Reply(_transport.Calls.Single(), Result());
        var session = Connect(); Call(session, "Read");
        Reply(_transport.Calls.Last(), new GetUserDataResult { Data = new Dictionary<string, UserDataRecord>() });
        Call(session, "Prepare", 1); Call(session, "Upload");
        var write = _transport.Calls.Last();
        string path = Get<string>(session, "SavePath"); string before = File.ReadAllText(path);
        if (relogin) Login(); else Call(_auth, "Disconnect");
        Reply(write, new UpdateUserDataResult());
        Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        int requests = _transport.Calls.Count;
        Call(session, "Read");
        Assert.That(_transport.Calls.Count, Is.EqualTo(requests));
        Assert.That(Get<bool>(session, "CanMutate"), Is.False);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void Revival_GameSaveCloud_LogoutOrReloginInvalidatesLateRead(bool relogin)
    {
        Login(); Reply(_transport.Calls.Single(), Result());
        var session = Connect(); Call(session, "Read");
        var read = _transport.Calls.Last(); string path = Get<string>(session, "SavePath");
        if (relogin) Login(); else Call(_auth, "Disconnect");
        Reply(read, new GetUserDataResult { Data = new Dictionary<string, UserDataRecord>() });
        Assert.That(File.Exists(path), Is.False);
        Assert.That(Get<bool>(session, "CanMutate"), Is.False);
    }
    [Test]
    public void Revival_GameSaveCloud_ManifestAddsOnlyInternetAndKeepsOfflineRemoval()
    {
        var builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalGameSaveBuild")).First(t => t != null);
        var document = System.Xml.Linq.XDocument.Parse((string)Call(builder, "CloudManifest", "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application/></manifest>"));
        System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
        System.Xml.Linq.XNamespace tools = "http://schemas.android.com/tools";
        foreach (var permission in document.Root.Elements("uses-permission"))
            if ((string)permission.Attribute(android + "name") == "android.permission.INTERNET") Assert.That(permission.Attribute(tools + "node"), Is.Null);
            else Assert.That((string)permission.Attribute(tools + "node"), Is.EqualTo("remove"));
    }
}
