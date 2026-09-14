using System;
using System.Collections.Generic;
using System.Linq;
using AD.Purchasing;
using NUnit.Framework;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Internal;

public class RevivalProgressProbeTests
{
    private sealed class Transport : ITransportPlugin
    {
        public bool IsInitialized => true;
        public readonly List<CallRequestContainer> Calls = new List<CallRequestContainer>();
        public void Initialize() => throw new InvalidOperationException("No live HTTP allowed");
        public void MakeApiCall(object request) => Calls.Add((CallRequestContainer)request);
        public void Update() { }
        public void OnDestroy() { }
        public int GetPendingMessages() => 0;
        public void SimpleGetCall(string u,Action<byte[]> s,Action<string> e) => throw new NotSupportedException();
        public void SimplePutCall(string u,byte[] p,Action<byte[]> s,Action<string> e) => throw new NotSupportedException();
        public void SimplePostCall(string u,byte[] p,Action<byte[]> s,Action<string> e) => throw new NotSupportedException();
    }

    [Test]
    public void Revival_Progress_RealSdkRequestsKeepDedicatedTitleKeyPrivateAndReadAfterWrite()
    {
        var original=PluginManager.GetPlugin<ITransportPlugin>(PluginContract.PlayFab_Transport);
        var transport=new Transport();
        PluginManager.SetPlugin(transport,PluginContract.PlayFab_Transport);
        object probe=null;
        try {
            var session=new ReceiptSession("synthetic-a","synthetic-ticket");
            probe=Probe.GetMethod("Connect").Invoke(null,new object[]{"12B656","com.AeDeong.MonsterTamer.iaptest",(Func<ReceiptSession>)(()=>session)});
            Probe.GetMethod("Read").Invoke(probe,null);
            var read=transport.Calls.Single();
            Assert.That(read.settings.TitleId,Is.EqualTo("12B656"));
            Assert.That(((GetUserDataRequest)read.ApiRequest).Keys,Is.EqualTo(new[]{Key}));
            Assert.That(read.context.PlayFabId,Is.EqualTo(session.AccountId));
            read.ApiResult=new GetUserDataResult{Data=new Dictionary<string,UserDataRecord>()}; read.InvokeSuccessCallback();
            Probe.GetMethod("Save").Invoke(probe,new object[]{1});
            var write=transport.Calls.Last();
            Assert.That(write.ApiRequest,Is.TypeOf<UpdateUserDataRequest>());
            var patch=(UpdateUserDataRequest)write.ApiRequest;
            Assert.That(patch.Data,Is.EqualTo(new Dictionary<string,string>{{Key,"v1:1"}}));
            Assert.That(patch.Permission,Is.EqualTo(UserDataPermission.Private));
            Assert.That(patch.KeysToRemove,Is.Null.Or.Empty);
            Assert.That(write.settings.TitleId,Is.EqualTo("12B656"));
            Assert.That(transport.Calls.Count,Is.EqualTo(2));
            write.ApiResult=new UpdateUserDataResult(); write.InvokeSuccessCallback();
            var readBack=transport.Calls.Last();
            Assert.That(readBack.ApiRequest,Is.TypeOf<GetUserDataRequest>());
            readBack.ApiResult=new GetUserDataResult{Data=new Dictionary<string,UserDataRecord>{{Key,new UserDataRecord{Value="v1:1"}}}};
            readBack.InvokeSuccessCallback();
            Assert.That(Probe.GetProperty("RestoredStep").GetValue(probe),Is.EqualTo(1));
        }
        finally { (probe as IDisposable)?.Dispose(); PluginManager.SetPlugin(original,PluginContract.PlayFab_Transport); }
    }
    private static Type Probe => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.RevivalProgressProbe")).First(t => t != null);
    private const string Key = "RevivalProgressProbeV1";
    private sealed class Harness : IDisposable
    {
        public ReceiptSession Session = new ReceiptSession("synthetic-a", "synthetic-ticket");
        public Dictionary<string, string> Cloud = new Dictionary<string, string> { ["GooglePlay"] = "ProductNoAds", ["Gold"] = "00042", ["__TamerAccountOwner"] = "synthetic-a" };
        public int Writes;
        public bool DelayRead;
        public Action<Dictionary<string, string>> Pending;
        public object Instance;
        public Harness() { Connect(); }
        public void Connect()
        {
            (Instance as IDisposable)?.Dispose();
            Instance = Activator.CreateInstance(Probe, new object[] {
                (Func<ReceiptSession>)(() => Session),
                (Action<string, Action<Dictionary<string,string>>, Action<int>>)((a,ok,fail) => {
                    if (DelayRead) Pending=ok;
                    else ok(new Dictionary<string,string>(Cloud));
                }),
                (Action<string, Dictionary<string,string>, Action, Action<int>>)((a,p,ok,fail) => {
                    Writes++;
                    foreach(var entry in p) Cloud[entry.Key]=entry.Value;
                    ok();
                }) });
        }
        public void Call(string method, params object[] args) => Probe.GetMethod(method).Invoke(Instance,args);
        public object Value(string name) => Probe.GetProperty(name).GetValue(Instance);
        public void Dispose() => (Instance as IDisposable)?.Dispose();
    }
    [Test]
    public void Revival_Progress_ReconnectReadsCloudWithoutChangingGameOrEntitlementKeys()
    {
        using(var h=new Harness()) {
            h.Call("Save",1); Assert.That(h.Writes,Is.Zero);
            h.Call("Read"); h.Call("Save",2);
            Assert.That(h.Value("RestoredStep"),Is.EqualTo(2));
            h.Connect(); Assert.That(h.Value("RestoredStep"),Is.Null);
            h.Call("Read"); Assert.That(h.Value("RestoredStep"),Is.EqualTo(2));
            Assert.That(h.Cloud["GooglePlay"],Is.EqualTo("ProductNoAds"));
            Assert.That(h.Cloud["Gold"],Is.EqualTo("00042"));
            Assert.That(h.Cloud["__TamerAccountOwner"],Is.EqualTo("synthetic-a"));
            Assert.That(h.Cloud.Count,Is.EqualTo(4));
        }
    }
    [TestCase("GooglePlay","ProductNoAds")]
    [TestCase("Gold","123")]
    [TestCase(Key,null)]
    [TestCase(Key,"v1:-1")]
    [TestCase(Key,"v1:10000")]
    public void Revival_Progress_RejectsNonProbeOrMalformedPatch(string key,string value)
    {
        Assert.Throws<System.Reflection.TargetInvocationException>(()=>Probe.GetMethod("ValidatePatch").Invoke(null,
            new object[]{new Dictionary<string,string>{{key,value}}}));
    }
    [Test]
    public void Revival_Progress_MalformedReadCannotEnableOverwrite()
    {
        using(var h=new Harness()) {
            h.Cloud[Key]="legacy-unknown"; h.Call("Read"); h.Call("Save",1);
            Assert.That(h.Writes,Is.Zero); Assert.That(h.Value("CanSave"),Is.False);
            Assert.That(h.Cloud[Key],Is.EqualTo("legacy-unknown"));
        }
    }
    [TestCase(false)]
    [TestCase(true)]
    public void Revival_Progress_LateReadCannotCrossAccountOrSessionGeneration(bool sameAccount)
    {
        using(var h=new Harness()) {
            h.DelayRead=true; h.Call("Read");
            h.Session=new ReceiptSession(sameAccount?"synthetic-a":"synthetic-b","synthetic-ticket");
            h.Pending(new Dictionary<string,string>{{Key,"v1:2"}});
            Assert.That(h.Value("RestoredStep"),Is.Null); h.Call("Save",1); Assert.That(h.Writes,Is.Zero);
        }
    }
    [Test]
    public void Revival_Progress_DisposedProbeIgnoresLateRead()
    {
        using(var h=new Harness()) {
            h.DelayRead=true; h.Call("Read"); h.Dispose();
            h.Pending(new Dictionary<string,string>{{Key,"v1:2"}});
            Assert.That(h.Value("RestoredStep"),Is.Null);
        }
    }
    [TestCase("67C9A","com.AeDeong.MonsterTamer.iaptest")]
    [TestCase("12B656","com.AeDeong.MonsterTamer")]
    [TestCase("ABC","com.AeDeong.MonsterTamer.iaptest")]
    public void Revival_Progress_RealAdapterRejectsUnapprovedTitleOrPackageBeforeNetwork(string title,string package)
    {
        Assert.Throws<System.Reflection.TargetInvocationException>(()=>Probe.GetMethod("Connect").Invoke(null,
            new object[]{title,package,(Func<ReceiptSession>)(()=>new ReceiptSession("synthetic-a","synthetic-ticket"))}));
    }
}
