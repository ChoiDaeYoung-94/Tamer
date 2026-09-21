using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

/// <summary>Account selection and callback tests without PlayFab, GPGS, PlayerPrefs or scenes.</summary>
public class RevivalLoginContinuityTests
{
    [Test]
    public void Revival_UnreadableLocalSaveCannotAuthorizeNewAccountCreation()
    {
        bool existingOrUnknown = (bool)Policy("HasLocalProgress", (object)null);
        Assert.That(existingOrUnknown, Is.True);
        Assert.That(Policy("CanCreateAccount", "", existingOrUnknown), Is.False);
    }

    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD." + name)).First(t => t != null);

    private static object Call(object target, string method, params object[] args)
    {
        var type = target as Type ?? target.GetType();
        try
        {
            return type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance).Invoke(target is Type ? null : target, args);
        }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }

    private static object New(string type) => Activator.CreateInstance(RuntimeType(type), true);
    private static object Policy(string method, params object[] args) => Call(RuntimeType("LoginContinuityPolicy"), method, args);

    [TestCase("old-player", "old-player", "old-player")]
    [TestCase("old-player", null, null)]
    [TestCase("old-player", "", null)]
    [TestCase(null, null, null)]
    [TestCase("old-player", "other-player", null)]
    [TestCase("old-player", "OLD-PLAYER", null)]
    [TestCase(null, "first-player", "first-player")]
    public void Revival_LoginNeverReplacesCachedGoogleIdentity(string cached, string current, string expected)
        => Assert.That(Policy("SelectGoogleId", cached, current), Is.EqualTo(expected));

    [TestCase("custom", true, true, "custom")]
    [TestCase("custom", false, true, null)]
    [TestCase("android", true, true, "android")]
    [TestCase("android", true, false, null)]
    [TestCase("device", false, true, "android")]
    [TestCase("device", true, true, null)]
    [TestCase("device", true, false, null)]
    [TestCase("device", false, false, null)]
    [TestCase("gpgs", true, true, null)]
    [TestCase("unknown-future-mode", false, true, null)]
    [TestCase("", true, true, "custom")]
    [TestCase("", false, true, "android")]
    [TestCase("", false, false, "custom")]
    [TestCase("custom-pending", true, true, "custom")]
    [TestCase("android-pending", true, true, "android")]
    public void Revival_LoginKeepsDeviceMethodAndRejectsAmbiguousLegacySelection(
        string savedMode, bool customId, bool androidId, string expected)
        => Assert.That(Policy("SelectDeviceMode", savedMode, customId, androidId), Is.EqualTo(expected));

    [TestCase("", false, true)]
    [TestCase("", true, false)]
    [TestCase("device", false, false)]
    [TestCase("android", false, false)]
    [TestCase("custom", false, false)]
    [TestCase("gpgs", false, false)]
    [TestCase("gpgs-pending", false, false)]
    [TestCase("custom-pending", false, true)]
    [TestCase("android-pending", false, true)]
    [TestCase("gpgs-pending", true, false)]
    [TestCase("custom-pending", true, false)]
    public void Revival_LoginCreationRequiresAnUnplayedFirstSelection(string mode, bool progress, bool expected)
        => Assert.That(Policy("CanCreateAccount", mode, progress), Is.EqualTo(expected));

    [TestCase(false, false, "", "", true)]
    [TestCase(false, true, "", "", false)]
    [TestCase(false, false, "gpgs", "", false)]
    [TestCase(false, false, "gpgs-pending", "", false)]
    [TestCase(false, false, "", "saved-google-id", false)]
    [TestCase(true, true, "gpgs", "saved-google-id", true)]
    public void Revival_NativeGoogleRequiresKnownOwnerOrCleanFirstLogin(
        bool owner, bool progress, string mode, string cachedId, bool expected)
        => Assert.That(Policy("CanAttemptNativeGoogle", owner, progress, mode, cachedId), Is.EqualTo(expected));

    [TestCase("original", "original", true)]
    [TestCase("original", "different", false)]
    [TestCase("original", "ORIGINAL", false)]
    [TestCase(null, "linked-account", true)]
    [TestCase(null, null, false)]
    [TestCase("original", "", false)]
    public void Revival_NativeGoogleRejectsReturnedAccountMismatch(string known, string returned, bool expected)
        => Assert.That(Policy("MatchesKnownPlayFabAccount", known, returned), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Revival_NativeGoogleRejectsMissingFreshCode(string code)
        => Assert.Throws<ArgumentException>(() => Call(RuntimeType("Login"), "CreateGoogleLoginRequest", code));

    [Test]
    public void Revival_NativeGoogleNeverCreatesAccountsOrUsesSharedAuthenticationContext()
    {
        var first = Call(RuntimeType("Login"), "CreateGoogleLoginRequest", "synthetic-code-one");
        var second = Call(RuntimeType("Login"), "CreateGoogleLoginRequest", "synthetic-code-two");
        var type = first.GetType();
        Assert.That(type.GetField("CreateAccount").GetValue(first), Is.EqualTo(false));
        Assert.That(type.GetField("ServerAuthCode").GetValue(first), Is.EqualTo("synthetic-code-one"));
        var context = type.GetField("AuthenticationContext");
        Assert.That(context.GetValue(first), Is.Not.Null);
        Assert.That(context.GetValue(first), Is.Not.SameAs(context.GetValue(second)));
    }

    [TestCase("Sex", "null", false)]
    [TestCase("GooglePlay", "", false)]
    [TestCase("GooglePlay", "test-entitlement", true)]
    [TestCase("NickName", "test-nickname", true)]
    [TestCase("Gold", "0", false)]
    [TestCase("Gold", "100", true)]
    public void Revival_LoginRecognizesProgressBeforeCharacterCreation(string key, string value, bool expected)
        => Assert.That(Policy("HasLocalProgress", new Dictionary<string, string> { { key, value } }), Is.EqualTo(expected));

    [Test]
    public void Revival_LoginSerializesNicknameAndEntersSceneOnlyOnce()
    {
        var gate = New("LoginOperationGate");
        Assert.That(Call(gate, "TryBeginNickname"), Is.False, "No authenticated profile yet.");
        Assert.That(Call(gate, "TryBeginLogin"), Is.True);
        Assert.That(Call(gate, "TryBeginLogin"), Is.False);
        Call(gate, "AwaitNickname");
        Call(gate, "EndOperation");
        Assert.That(Call(gate, "TryBeginLogin"), Is.False, "Retry must not bypass nickname entry.");
        Assert.That(Call(gate, "TryBeginNickname"), Is.True);
        Assert.That(Call(gate, "TryBeginNickname"), Is.False);
        Assert.That(Call(gate, "TryBeginLogin"), Is.False);
        Assert.That(Call(gate, "TryEnterScene"), Is.True);
        Assert.That(Call(gate, "TryEnterScene"), Is.False);
        Call(gate, "EndOperation");
        Assert.That(Call(gate, "TryBeginLogin"), Is.False);
        Assert.That(Call(gate, "TryBeginNickname"), Is.False);
    }

    [Test]
    public void Revival_LoginRetryWaitsForFailedOperationToUnwind()
    {
        var gate = New("LoginOperationGate");
        Call(gate, "TryBeginLogin");
        Call(gate, "AwaitNickname");
        Call(gate, "ResetForRetry");
        Assert.That(Call(gate, "TryBeginLogin"), Is.False);
        Call(gate, "EndOperation");
        Assert.That(Call(gate, "TryBeginLogin"), Is.True);
    }

    [Test]
    public void Revival_LoginLateAndDuplicateCallbacksCannotCompleteANewAttempt()
    {
        var expired = New("LoginCallbackGate");
        Call(expired, "Expire");
        var current = New("LoginCallbackGate");
        Assert.That(Call(expired, "TryComplete", false), Is.False, "Late timeout response.");
        Assert.That(Call(current, "TryComplete", false), Is.True);
        Assert.That(Call(current, "TryComplete", false), Is.False, "Duplicate callback.");
        var cancelled = New("LoginCallbackGate");
        Assert.That(Call(cancelled, "TryComplete", true), Is.False);
    }

    [Test]
    public void Revival_LoginCancellationWinsBeforeAnAlreadyCompletedPredicate()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            bool predicateCalled = false;
            Func<bool> predicate = () => { predicateCalled = true; return true; };
            var pending = Call(RuntimeType("Login"), "WaitUntilAsync", predicate,
                TimeSpan.FromSeconds(20), cancellation.Token);
            var awaiter = pending.GetType().GetMethod("GetAwaiter").Invoke(pending, null);
            Assert.That(awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null), Is.False);
            Assert.That(predicateCalled, Is.False);
        }
    }

}
