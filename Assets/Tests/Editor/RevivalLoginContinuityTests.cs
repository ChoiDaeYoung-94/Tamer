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
        Assert.That(Policy("CanCreateGoogleAccount", "", existingOrUnknown, ""), Is.False);
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
    [TestCase("old-player", null, "old-player")]
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
    [TestCase("gpgs-pending", false, true)]
    [TestCase("custom-pending", false, true)]
    [TestCase("android-pending", false, true)]
    [TestCase("gpgs-pending", true, false)]
    [TestCase("custom-pending", true, false)]
    public void Revival_LoginCreationRequiresAnUnplayedFirstSelection(string mode, bool progress, bool expected)
        => Assert.That(Policy("CanCreateAccount", mode, progress), Is.EqualTo(expected));

    [TestCase("", false, false, true)]
    [TestCase("", false, true, false)]
    [TestCase("", true, false, false)]
    [TestCase("gpgs", false, true, false)]
    [TestCase("gpgs-pending", false, true, true)]
    [TestCase("gpgs-pending", true, true, false)]
    [TestCase("android-pending", false, false, false)]
    public void Revival_LoginGoogleCreationSeparatesFreshSelectionFromSavedIdentity(
        string mode, bool progress, bool cachedId, bool expected)
        => Assert.That(Policy("CanCreateGoogleAccount", mode, progress, cachedId), Is.EqualTo(expected));

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

    [TestCase("AccountNotFound", true, true)]
    [TestCase("AccountNotFound", false, false)]
    [TestCase("InvalidEmailOrPassword", true, true)]
    [TestCase("InvalidEmailOrPassword", false, false)]
    [TestCase("InvalidEmailAddress", true, false)]
    [TestCase("InvalidParams", true, false)]
    [TestCase("ConnectionError", true, false)]
    public void Revival_LoginRegistrationRequiresFreshSelectionAndAnEligibleResponse(
        string errorName, bool allowCreate, bool expected)
    {
        var errorType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PlayFab.PlayFabError"))
            .First(t => t != null);
        var error = Activator.CreateInstance(errorType);
        var codeField = errorType.GetField("Error");
        codeField.SetValue(error, Enum.Parse(codeField.FieldType, errorName));
        Assert.That(Call(RuntimeType("Login"), "IsRegistrationCandidate", error, allowCreate), Is.EqualTo(expected));
    }
}
