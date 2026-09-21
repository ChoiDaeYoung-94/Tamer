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
    private static Type DataType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD.DataManager")).First(t => t != null);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static object PrivateCall(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

    [Test] public void Revival_DeletionRuntimeCompositionRejectsReplacedManagerBeforeAnyHttp()
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
            presenter.GetMethod("ConfigureSessionService").Invoke(null, new object[] { new Uri("https://example.invalid/"), "TEST1",
                Path.Combine(Path.GetTempPath(), "tamer-unused-" + Guid.NewGuid().ToString("N")) });
            flow = (IDisposable)((Delegate)factory.GetValue(null)).DynamicInvoke();
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
