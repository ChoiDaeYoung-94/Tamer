using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PlayFab;

public class RevivalDeletionIntakeTests
{
    [Test] public void Revival_CloudScriptDeletionRequiresMatchingSuccessfulEnvelope()
    {
        var type = DataType.Assembly.GetType("AD.CloudScriptDeletionClient");
        var method = type.GetMethod("IsAccepted");
        const string id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var result = new PlayFab.ClientModels.ExecuteCloudScriptResult
        {
            FunctionName = "requestCurrentPlayerDeletionV1", Revision = 7,
            FunctionResult = new Dictionary<string, object> { ["accepted"] = true, ["scope"] = "title",
                ["protocol"] = "tamer-title-deletion-v1", ["requestId"] = id }
        };
        bool Accepted() => (bool)method.Invoke(null, new object[] { result, id, 7 });
        Assert.True(Accepted());
        result.Error = new PlayFab.ClientModels.ScriptExecutionError { Error = "synthetic" }; Assert.False(Accepted()); result.Error = null;
        result.Revision = 8; Assert.False(Accepted()); result.Revision = 7;
        result.FunctionResultTooLarge = true; Assert.False(Accepted()); result.FunctionResultTooLarge = null;
        var body = (Dictionary<string, object>)result.FunctionResult;
        body["accepted"] = "true"; Assert.False(Accepted()); body["accepted"] = true;
        body["requestId"] = new string('b', 32); Assert.False(Accepted()); body["requestId"] = id;
        body["scope"] = "master"; Assert.False(Accepted()); body["scope"] = "title";
        result.FunctionResult = null; Assert.False(Accepted());
    }
    [TestCase(false,false)] [TestCase(true,false)] [TestCase(true,true)]
    public void Revival_InventoryDeletionAcceptedAndRestartRespectSession(bool restarted,bool newer)
    {
        var root=new GameObject("Inventory deletion fixture"); root.SetActive(false);
        string directory=Path.Combine(Path.GetTempPath(),"inventory-deletion-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"PlayerData.json");
        var credentials=new PlayFabAuthenticationContext(); credentials.CopyFrom(PlayFabSettings.staticPlayer);
        string title=PlayFabSettings.TitleId;
        const string pauseKey="AD_DeletionAcceptedNeedsLogin"; bool hadPause=PlayerPrefs.HasKey(pauseKey); int pause=PlayerPrefs.GetInt(pauseKey);
        try
        {
            PlayFabSettings.TitleId="TEST1"; PlayFabSettings.staticPlayer.ForgetAllCredentials();
            var data=root.AddComponent(DataType); Set(data,"_playerDataPath",path);
            Set(data,"<PlayFabId>k__BackingField","synthetic-a");
            string epoch=new string('a',32); Set(data,"_inventorySession",epoch);
            var storeType=DataType.Assembly.GetType("AD.PlayerInventoryStore");
            var store=Activator.CreateInstance(storeType,path+".inventory",new[]{"Bat"},new Dictionary<string,string>{{"SimpleSword","Sword"}});
            Call(store,"BindSession","synthetic-a",epoch); Call(store,"BindSession","other",new string('b',32));
            string own=(string)Call(store,"PathFor","synthetic-a"), other=(string)Call(store,"PathFor","other");
            File.WriteAllText(path,"{\"__TamerAccountOwner\":\"synthetic-a\",\"GooglePlay\":\"ProductNoAds\"}");
            if(restarted)
            {
                Set(data,"<PlayFabId>k__BackingField","");
                var recordType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AD.Privacy.DeletionRecovery")).First(t=>t!=null);
                var record=Activator.CreateInstance(recordType);
                void Field(string n,object v)=>recordType.GetField(n).SetValue(record,v);
                Field("Origin","https://example.invalid/"); Field("Title","TEST1");
                Field("OwnerHash",recordType.GetMethod("Hash").Invoke(null,new object[]{new[]{"https://example.invalid/","TEST1","synthetic-a"}}));
                Field("InventoryOwnerKey",Path.GetFileNameWithoutExtension(own)); Field("InventorySession",epoch);
                if(newer) Call(store,"BindSession","synthetic-a",new string('c',32));
                if(newer) Assert.Throws<TargetInvocationException>(()=>Call(data,"ApplyDeletionReceipt",record,true));
                else { Call(data,"ApplyDeletionReceipt",record,true); Call(data,"ApplyDeletionReceipt",record,true); }
            }
            else Call(data,"FinishAcceptedDeletion",Call(data,"DeletionSession"));
            Assert.That(File.Exists(own),Is.EqualTo(newer)); Assert.That(File.Exists(path),Is.EqualTo(newer));
            Assert.That(File.Exists(other),Is.True);
            if(!newer) Assert.That(File.ReadAllText(Directory.GetFiles(directory,"*.deletion-entitlement-*").Single()),Does.Contain("ProductNoAds"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); Directory.Delete(directory,true);
            PlayFabSettings.TitleId=title; PlayFabSettings.staticPlayer.CopyFrom(credentials);
            if(hadPause) PlayerPrefs.SetInt(pauseKey,pause); else PlayerPrefs.DeleteKey(pauseKey); PlayerPrefs.Save();
        }
    }
    private static Type DataType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD.DataManager")).First(t => t != null);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static object PrivateCall(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

    [TestCase(1)] [TestCase(2)]
    public void Revival_DeletionReceiptBootstrapOpensBeforeLoginAndDoesNotChooseMultipleAccounts(int count)
    {
        var root = new GameObject("Pre-login receipt fixture"); root.SetActive(false);
        string directory = Path.Combine(Path.GetTempPath(), "receipt-bootstrap-" + Guid.NewGuid().ToString("N"));
        var managersType = DataType.Assembly.GetType("AD.Managers");
        var instance = managersType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previousManager = instance.GetValue(null);
        var credentials = new PlayFabAuthenticationContext(); credentials.CopyFrom(PlayFabSettings.staticPlayer);
        string title = PlayFabSettings.TitleId;
        var bootstrap = DataType.Assembly.GetType("AD.DeletionReceiptBootstrap");
        var savedConfig = new[] { "_origin", "_title", "_directory" }.Select(n => bootstrap.GetField(n, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).ToArray();
        try
        {
            PlayFabSettings.staticPlayer.ForgetAllCredentials(); PlayFabSettings.TitleId = "TEST1";
            var recordType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Privacy.DeletionRecovery")).First(t => t != null);
            var storeType = recordType.Assembly.GetType("AD.Privacy.FileDeletionRecoveryStore");
            string Hash(params string[] parts) => (string)recordType.GetMethod("Hash").Invoke(null, new object[] { parts });
            for (int i = 0; i < count; i++)
            {
                var record = Activator.CreateInstance(recordType);
                void Field(string name, object value) => recordType.GetField(name).SetValue(record, value);
                string key = Guid.NewGuid().ToString("N"), owner = Hash("https://example.invalid/", "TEST1", "synthetic-" + i);
                Field("Origin", "https://example.invalid/"); Field("Title", "TEST1"); Field("OwnerHash", owner);
                Field("Binding", Hash("https://example.invalid/", "TEST1", "synthetic-" + i, "entity"));
                Field("ClientKey", key); Field("RequestId", "request-" + i); Field("Revision", "v1");
                Field("SubmissionStarted", true); Field("KeyAlias", "tamer.deletion.receipt." + key);
                Field("KeyCreated", true); Field("ReceiptRegistered", true); Field("ReceiptExpires", 2000d);
                var store = Activator.CreateInstance(storeType, Path.Combine(directory, owner + ".json"));
                Call(store, "Save", record);
            }
            bootstrap.GetMethod("Configure").Invoke(null, new object[] { new Uri("https://example.invalid/"), "TEST1", directory });
            var data = root.AddComponent(DataType); var managers = root.AddComponent(managersType);
            Set(managers, "_dataM", data); instance.SetValue(null, managers);
            var login = root.AddComponent(DataType.Assembly.GetType("AD.Login"));
            PrivateCall(login, "Start");
            Assert.That(login.GetType().GetField("_receiptRecoverySignIn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(login), Is.True);
            Assert.That(DataType.GetProperty("PlayFabId").GetValue(data), Is.Empty);
            Assert.That(PlayFabSettings.staticPlayer.ClientSessionTicket, Is.Null.Or.Empty);
            var view = root.GetComponentInChildren(DataType.Assembly.GetType("AD.DeletionView"), true);
            Assert.That(view, Is.Not.Null);
            var refresh = (UnityEngine.Component)view.GetType().GetProperty("RefreshButton").GetValue(view);
            Assert.That(refresh.gameObject.activeSelf, Is.EqualTo(count == 1), "Multiple records must not select or query one intent automatically");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); Directory.Delete(directory, true); instance.SetValue(null, previousManager);
            PlayFabSettings.TitleId = title; PlayFabSettings.staticPlayer.CopyFrom(credentials);
            bootstrap.GetMethod("Configure").Invoke(null, savedConfig);
        }
    }

    [TestCase("synthetic-owner", false, true)]
    [TestCase("foreign-owner", false, false)]
    [TestCase("", false, false)]
    [TestCase("synthetic-owner", true, false)]
    public void Revival_DeletionReceiptOfflineCleanupRequiresOwnerHashAndNeverTouchesAnotherActiveAccount(string diskOwner, bool otherActive, bool removed)
    {
        var root = new GameObject("Offline receipt owner fixture"); root.SetActive(false);
        string directory = Path.Combine(Path.GetTempPath(), "receipt-owner-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "PlayerData.json");
        string original = "{\"Gold\":\"123\",\"GooglePlay\":\"ProductNoAds\",\"__TamerAccountOwner\":\"" + diskOwner + "\"}";
        File.WriteAllText(path, original);
        var credentials = new PlayFabAuthenticationContext(); credentials.CopyFrom(PlayFabSettings.staticPlayer);
        string title = PlayFabSettings.TitleId;
        const string pauseKey = "AD_DeletionAcceptedNeedsLogin"; bool hadPause = PlayerPrefs.HasKey(pauseKey); int pause = PlayerPrefs.GetInt(pauseKey);
        try
        {
            PlayFabSettings.TitleId = "TEST1"; PlayFabSettings.staticPlayer.ForgetAllCredentials();
            if (otherActive) PlayFabSettings.staticPlayer.PlayFabId = "active-other";
            var data = root.AddComponent(DataType); Set(data, "_playerDataPath", path);
            var recordType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Privacy.DeletionRecovery")).First(t => t != null);
            var record = Activator.CreateInstance(recordType);
            recordType.GetField("Origin").SetValue(record, "https://example.invalid/"); recordType.GetField("Title").SetValue(record, "TEST1");
            var hash = recordType.GetMethod("Hash");
            recordType.GetField("OwnerHash").SetValue(record, hash.Invoke(null, new object[] { new[] { "https://example.invalid/", "TEST1", "synthetic-owner" } }));
            recordType.GetField("Binding").SetValue(record, hash.Invoke(null, new object[] { new[] { "https://example.invalid/", "TEST1", "synthetic-owner", "entity" } }));
            if (otherActive)
            {
                Assert.That(Call(data, "CanApplyDeletionReceipt", record), Is.False);
                Assert.Throws<TargetInvocationException>(() => Call(data, "ApplyDeletionReceipt", record, true));
                Assert.That(PlayFabSettings.staticPlayer.PlayFabId, Is.EqualTo("active-other"));
            }
            else Call(data, "ApplyDeletionReceipt", record, true);
            Assert.That(File.Exists(path), Is.EqualTo(!removed));
            if (!removed) Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            else Assert.That(File.ReadAllText(Directory.GetFiles(directory, "*.deletion-entitlement-*").Single()), Does.Contain("ProductNoAds").And.Not.Contain("Gold"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); Directory.Delete(directory, true);
            PlayFabSettings.TitleId = title; PlayFabSettings.staticPlayer.CopyFrom(credentials);
            if (hadPause) PlayerPrefs.SetInt(pauseKey, pause); else PlayerPrefs.DeleteKey(pauseKey); PlayerPrefs.Save();
        }
    }

    [TestCase(false)] [TestCase(true)] public void Revival_DeletionRuntimeCompositionRejectsReplacedManagerBeforeAnyHttp(bool provider)
    {
        var root = new GameObject("Deletion runtime composition fixture");
        root.SetActive(false);
        var managersType = DataType.Assembly.GetType("AD.Managers");
        var instance = managersType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previousManagers = instance.GetValue(null);
        var presenter = DataType.Assembly.GetType("AD.DeletionPresenter");
        var factory = presenter.GetProperty("RuntimeFlowFactory");
        var previousFactory = factory.GetValue(null);
        var guard = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Privacy.DeletionRecoveryGuard"))
            .First(t => t != null).GetProperty("HasPendingSubmission");
        var previousGuard = guard.GetValue(null);
        var credentials = new PlayFabAuthenticationContext(); credentials.CopyFrom(PlayFabSettings.staticPlayer);
        string title = PlayFabSettings.TitleId;
        IDisposable flow = null;
        try
        {
            var data = root.AddComponent(DataType);
            DataType.GetProperty("PlayFabId").SetValue(data, "synthetic-runtime");
            var managers = root.AddComponent(managersType);
            Set(managers, "_dataM", data); instance.SetValue(null, managers);
            PlayFabSettings.TitleId = "TEST1";
            PlayFabSettings.staticPlayer.PlayFabId = "synthetic-runtime";
            PlayFabSettings.staticPlayer.EntityId = "synthetic-entity";
            PlayFabSettings.staticPlayer.EntityType = "title_player_account";
            string recoveryDirectory = Path.Combine(Path.GetTempPath(), "tamer-unused-" + Guid.NewGuid().ToString("N"));
            if (provider)
            {
                var method = presenter.GetMethod("ConfigureService");
                var authType = method.GetParameters()[2].ParameterType;
                // This adapter must never run while constructing or inspecting the flow.
                var invoke = authType.GetMethod("Invoke");
                var args = invoke.GetParameters().Select(p => System.Linq.Expressions.Expression.Parameter(p.ParameterType)).ToArray();
                var body = System.Linq.Expressions.Expression.Throw(System.Linq.Expressions.Expression.New(typeof(InvalidOperationException)), invoke.ReturnType);
                var auth = System.Linq.Expressions.Expression.Lambda(authType, body, args).Compile();
                method.Invoke(null, new object[] { new Uri("https://example.invalid/"), "TEST1", auth, recoveryDirectory });
            }
            else presenter.GetMethod("ConfigureSessionService").Invoke(null, new object[] { new Uri("https://example.invalid/"), "TEST1", recoveryDirectory });
            flow = (IDisposable)((Delegate)factory.GetValue(null)).DynamicInvoke();
            Assert.That(flow.GetType().GetField("_receipts", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flow), Is.Not.Null);
            Assert.That(flow.GetType().GetField("_recovery", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flow), Is.Not.Null);
            Assert.That(flow.GetType().GetProperty("UsesSessionConfirmation").GetValue(flow), Is.EqualTo(!provider));
            Assert.That(PrivateCall(flow, "Current"), Is.True);
            Set(managers, "_dataM", root.AddComponent(DataType));
            Assert.That(PrivateCall(flow, "Current"), Is.False);
        }
        finally
        {
            flow?.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
            instance.SetValue(null, previousManagers); factory.SetValue(null, previousFactory);
            guard.SetValue(null, previousGuard);
            PlayFabSettings.TitleId = title; PlayFabSettings.staticPlayer.CopyFrom(credentials);
        }
    }

    [Test]
    public void Revival_DeletionPendingLoginOpensRecoveryWithoutUnlockingWritesOrOldLogin()
    {
        var root = new GameObject("Deletion restart isolated owner");
        root.SetActive(false);
        var managersType = DataType.Assembly.GetType("AD.Managers");
        var instance = managersType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previousManagers = instance.GetValue(null);
        var guard = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Privacy.DeletionRecoveryGuard"))
            .First(t => t != null).GetProperty("HasPendingSubmission");
        var previousGuard = guard.GetValue(null);
        var credentials = new PlayFabAuthenticationContext();
        credentials.CopyFrom(PlayFabSettings.staticPlayer);
        try
        {
            guard.SetValue(null, (Func<string, bool>)(account => account == "synthetic-pending"));
            var data = root.AddComponent(DataType);
            var managers = root.AddComponent(managersType);
            Set(managers, "_dataM", data); instance.SetValue(null, managers);
            var login = root.AddComponent(DataType.Assembly.GetType("AD.Login"));
            Set(login, "_dataOwner", data);
            PrivateCall(login, "CaptureLoginSession");
            var context = new PlayFabAuthenticationContext { PlayFabId = "synthetic-pending", EntityId = "synthetic-entity",
                EntityType = "title_player_account", ClientSessionTicket = "synthetic-ticket" };
            PrivateCall(login, "OnLoggedIn", "synthetic-pending", false, "CustomID", context, null);
            Assert.That(DataType.GetProperty("DeletionInProgress").GetValue(data), Is.True);
            Assert.That(DataType.GetProperty("IsServerDataReady").GetValue(data), Is.False);
            Assert.That(PrivateCall(login, "LoginCurrent"), Is.False);
            Assert.Throws<TargetInvocationException>(() => Call(data, "SaveLocalData"));
            var panel = (GameObject)login.GetType().GetField("_deletionRecoveryPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(login);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.activeSelf, Is.True);
            panel.SetActive(false);
            Call(login, "RetryConnection");
            Assert.That(panel.activeSelf, Is.True, "Retry must reopen recovery rather than attempt a new login");
            Assert.That(DataType.GetProperty("DeletionInProgress").GetValue(data), Is.True);
            Assert.That(PrivateCall(login, "LoginCurrent"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            instance.SetValue(null, previousManagers);
            guard.SetValue(null, previousGuard);
            PlayFabSettings.staticPlayer.CopyFrom(credentials);
        }
    }

    [Test]
    public void Revival_DeletionConfirmedCancellationRestoresSessionWithoutRevivingOldLogin()
    {
        var root = new GameObject("Deletion cancellation isolated owner");
        root.SetActive(false);
        try
        {
            var data = root.AddComponent(DataType);
            DataType.GetProperty("PlayFabId").SetValue(data, "synthetic-a");
            DataType.GetProperty("IsServerDataReady").SetValue(data, true);
            int epoch = (int)DataType.GetProperty("DeletionEpoch").GetValue(data);
            var session = Call(data, "DeletionSession");
            Call(data, "BeginDeletionSubmission", session);
            Assert.Throws<TargetInvocationException>(() => Call(data, "BeginAccountSession", "synthetic-a"));
            Call(data, "FinishCancelledDeletion", session);
            Assert.That(DataType.GetProperty("DeletionInProgress").GetValue(data), Is.False);
            Assert.That(DataType.GetProperty("IsServerDataReady").GetValue(data), Is.True);
            Assert.That(DataType.GetProperty("DeletionEpoch").GetValue(data), Is.Not.EqualTo(epoch));
            Assert.DoesNotThrow(() => Call(data, "BeginAccountSession", "synthetic-a"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase("synthetic-a", true)]
    [TestCase("synthetic-other", false)]
    [TestCase("", false)]
    public void Revival_DeletionIntakeCleanupRequiresDiskOwnerAndPreservesEntitlements(string diskOwner, bool removes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tamer-deletion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "PlayerData.json");
        var original = "{\"Gold\":\"123\",\"GooglePlay\":\"ProductNoAds\"" +
            (diskOwner == "" ? "" : ",\"__TamerAccountOwner\":\"" + diskOwner + "\"") + "}";
        File.WriteAllText(path, original);
        var root = new GameObject("Deletion intake isolated owner");
        root.SetActive(false);
        var credentials = new PlayFabAuthenticationContext();
        credentials.CopyFrom(PlayFabSettings.staticPlayer);
        const string key = "AD_DeletionAcceptedNeedsLogin";
        bool hadPause = PlayerPrefs.HasKey(key);
        int pause = PlayerPrefs.GetInt(key);
        var managersType = DataType.Assembly.GetType("AD.Managers");
        var instanceField = managersType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previousManagers = instanceField.GetValue(null);
        try
        {
            PlayFabSettings.staticPlayer.PlayFabId = "synthetic-a";
            var data = root.AddComponent(DataType);
            Set(data, "_playerDataPath", path);
            Set(data, "_localOwner", diskOwner);
            Set(data, "_defaults", new Dictionary<string, string> { ["Gold"] = "0", ["GooglePlay"] = "" });
            DataType.GetProperty("PlayFabId").SetValue(data, "synthetic-a");
            var managers = root.AddComponent(managersType);
            Set(managers, "_dataM", data);
            instanceField.SetValue(null, managers);
            var login = root.AddComponent(DataType.Assembly.GetType("AD.Login"));
            Set(login, "_dataOwner", data);
            PrivateCall(login, "CaptureLoginSession");
            Assert.That(PrivateCall(login, "LoginCurrent"), Is.True);
            var session = Call(data, "DeletionSession");
            Call(data, "BeginDeletionSubmission", session);
            Assert.That((bool)DataType.GetProperty("DeletionInProgress").GetValue(data), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo(original), "In-flight/unknown never cleans data");
            Assert.Throws<TargetInvocationException>(() => Call(data, "BeginAccountSession", "synthetic-other"));
            Call(data, "FinishAcceptedDeletion", session);
            Assert.That(DataType.GetProperty("PlayFabId").GetValue(data), Is.EqualTo(""));
            Assert.That((bool)DataType.GetProperty("DeletionInProgress").GetValue(data), Is.False);
            Assert.That(File.Exists(path), Is.EqualTo(!removes));
            var evidence = Directory.GetFiles(directory, "*.deletion-entitlement-*");
            Assert.That(evidence.Length, Is.EqualTo(removes ? 1 : 0));
            if (removes)
            {
                Assert.That(File.ReadAllText(evidence[0]), Does.Contain("ProductNoAds"));
                Assert.That(File.ReadAllText(evidence[0]), Does.Not.Contain("Gold"));
            }
            else Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            Assert.Throws<TargetInvocationException>(() => Call(data, "FinishAcceptedDeletion", session));
            var late = new PlayFabAuthenticationContext { PlayFabId = "synthetic-a", ClientSessionTicket = "synthetic-only" };
            PrivateCall(login, "OnLoggedIn", "synthetic-a", false, "late", late, null);
            Assert.That(DataType.GetProperty("PlayFabId").GetValue(data), Is.EqualTo(""));
            Assert.That(PlayFabSettings.staticPlayer.ClientSessionTicket, Is.Null.Or.Empty);
            Assert.That(PrivateCall(login, "LoginCurrent"), Is.False, "Profile and scene continuations must also reject this login");
            if (removes)
            {
                PrivateCall(login, "CaptureLoginSession"); // A newly initiated explicit login captures the new epoch.
                late.PlayFabId = "synthetic-new";
                PrivateCall(login, "OnLoggedIn", "synthetic-new", false, "explicit", late, null);
                Assert.That(DataType.GetProperty("PlayFabId").GetValue(data), Is.EqualTo("synthetic-new"));
                Assert.That(PrivateCall(login, "LoginCurrent"), Is.True);
            }
            if (diskOwner == "synthetic-other")
                Assert.Throws<TargetInvocationException>(() => Call(data, "BeginAccountSession", "synthetic-a"));
        }
        finally
        {
            instanceField.SetValue(null, previousManagers);
            UnityEngine.Object.DestroyImmediate(root);
            PlayFabSettings.staticPlayer.CopyFrom(credentials);
            if (hadPause) PlayerPrefs.SetInt(key, pause); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Directory.Delete(directory, true);
        }
    }
}
