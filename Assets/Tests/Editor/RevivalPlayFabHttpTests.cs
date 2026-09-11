using System;
using System.Reflection;
using NUnit.Framework;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Internal;
using UnityEngine;

public class RevivalPlayFabHttpTests
{
    private sealed class Transport : ITransportPlugin
    {
        public bool IsInitialized => true;
        public object Request;
        public void Initialize() => throw new InvalidOperationException("No live transport in tests");
        public void MakeApiCall(object request) => Request = request;
        public void Update() { }
        public void OnDestroy() { }
        public int GetPendingMessages() => 0;
        public void SimpleGetCall(string u, Action<byte[]> s, Action<string> e) => throw new NotSupportedException();
        public void SimplePutCall(string u, byte[] p, Action<byte[]> s, Action<string> e) => throw new NotSupportedException();
        public void SimplePostCall(string u, byte[] p, Action<byte[]> s, Action<string> e) => throw new NotSupportedException();
    }
    private FieldInfo _settingsField;
    private object _originalSettings;
    private PlayFabSharedSettings _temporarySettings;
    private ITransportPlugin _originalTransport;
    private Transport _transport;

    [SetUp]
    public void SetUp()
    {
        _settingsField = typeof(PlayFabSettings).GetField("_playFabShared", BindingFlags.NonPublic | BindingFlags.Static);
        _originalSettings = _settingsField.GetValue(null);
        _temporarySettings = ScriptableObject.CreateInstance<PlayFabSharedSettings>();
        _temporarySettings.TitleId = "";
        _settingsField.SetValue(null, _temporarySettings);
        _originalTransport = PluginManager.GetPlugin<ITransportPlugin>(PluginContract.PlayFab_Transport);
        _transport = new Transport();
        PluginManager.SetPlugin(_transport, PluginContract.PlayFab_Transport);
    }
    [TearDown]
    public void TearDown()
    {
        PluginManager.SetPlugin(_originalTransport, PluginContract.PlayFab_Transport);
        _settingsField.SetValue(null, _originalSettings);
        UnityEngine.Object.DestroyImmediate(_temporarySettings);
    }
    [Test]
    public void Revival_InstanceTitleWorksWithEmptyStaticTitle()
    {
        var settings = new PlayFabApiSettings { TitleId = "ABC" };
        var context = new PlayFabAuthenticationContext();
        var client = new PlayFabClientInstanceAPI(settings, context);
        client.LoginWithCustomID(new LoginWithCustomIDRequest { CustomId = "synthetic" }, r => { }, e => { });
        Assert.That(_transport.Request, Is.Not.Null);
        Assert.That(PlayFabSettings.TitleId, Is.Empty);
        var request = (CallRequestContainer)_transport.Request;
        Assert.That(request.settings, Is.SameAs(settings));
        Assert.That(request.context, Is.SameAs(context));
    }
    [Test]
    public void Revival_EmptyInstanceAndStaticTitlesFailBeforeTransport()
    {
        var client = new PlayFabClientInstanceAPI(new PlayFabApiSettings());
        Assert.Throws<PlayFabException>(() => client.LoginWithCustomID(
            new LoginWithCustomIDRequest { CustomId = "synthetic" }, r => { }, e => { }));
        Assert.That(_transport.Request, Is.Null);
    }
    [Test]
    public void Revival_DefaultHttpInitializationStillUsesStaticTitle()
    {
        Assert.Throws<PlayFabException>(() => PlayFabHttp.InitializeHttp());
        _temporarySettings.TitleId = "DEF";
        Assert.DoesNotThrow(() => PlayFabHttp.InitializeHttp());
    }
}
