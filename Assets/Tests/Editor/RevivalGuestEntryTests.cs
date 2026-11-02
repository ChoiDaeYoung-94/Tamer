using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>First-entry effects using only fake settings and identities; no PlayerPrefs or SDK calls.</summary>
public class RevivalGuestEntryTests
{
    private const string Id = "tamer-guest-v1-00112233445566778899aabbccddeeff";
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD." + name)).First(t => t != null);
    private static object New(string name) => Activator.CreateInstance(Runtime(name), true);
    private static object Call(object target, string name, params object[] args)
    {
        var type = target as Type ?? target.GetType();
        try { return type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Invoke(target is Type ? null : target, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    private static long Version(object gate) => (long)gate.GetType().GetProperty("Version").GetValue(gate);

    [TestCase("fresh", true)]
    [TestCase("known-owner", false)]
    [TestCase("local-progress-or-purchase", false)]
    [TestCase("saved-google", false)]
    [TestCase("google-id", false)]
    [TestCase("orphan-custom-id", false)]
    [TestCase("deletion", false)]
    [TestCase("deletion-pause", false)]
    [TestCase("receipt-only", false)]
    [TestCase("google-create-pending", false)]
    [TestCase("private-privacy-trial", false)]
    public void Revival_GuestEntryIsOnlyOfferedToUnplayedUnambiguousUsers(string context, bool expected)
    {
        Assert.That(Call(Runtime("LoginGuestEntryPolicy"), "CanOffer",
            context == "known-owner", context == "local-progress-or-purchase",
            context == "saved-google" ? "gpgs" : "", context == "google-id" ? "old-google" : "",
            context == "orphan-custom-id" ? "old-custom" : "", context == "deletion",
            context == "deletion-pause", context == "receipt-only", context == "google-create-pending",
            context != "private-privacy-trial"), Is.EqualTo(expected));
    }

    [Test]
    public void Revival_GuestChoiceIsSingleUse()
    {
        var gate = New("LoginGuestEntryGate"); var owner = new object();
        Assert.That(Call(gate, "Offer", owner, 4, 7, true), Is.True);
        long version = Version(gate);
        Assert.That(Call(gate, "TryChoose", owner, 4, 7, version, true), Is.True);
        Assert.That(Call(gate, "TryChoose", owner, 4, 7, version, true), Is.False);
    }

    [TestCase("owner")]
    [TestCase("generation")]
    [TestCase("deletion-epoch")]
    [TestCase("eligibility")]
    public void Revival_GuestChoiceRejectsContextChangedAfterDisplay(string changed)
    {
        var gate = New("LoginGuestEntryGate"); var owner = new object();
        Call(gate, "Offer", owner, 4, 7, true);
        Assert.That(Call(gate, "TryChoose", changed == "owner" ? new object() : owner,
            changed == "generation" ? 5 : 4, changed == "deletion-epoch" ? 8 : 7,
            Version(gate), changed != "eligibility"), Is.False);
    }

    [Test]
    public void Revival_OldChoiceCallbackCannotConsumeANewerOfferInTheSameGeneration()
    {
        var gate = New("LoginGuestEntryGate"); var owner = new object();
        Call(gate, "Offer", owner, 4, 7, true); long old = Version(gate);
        Call(gate, "Offer", owner, 4, 7, true); long current = Version(gate);
        Assert.That(Call(gate, "TryChoose", owner, 4, 7, old, true), Is.False);
        Assert.That(Call(gate, "TryChoose", owner, 4, 7, current, true), Is.True);
    }

    private sealed class Settings
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public readonly List<string> Events = new List<string>();
        public bool ThrowOnMode, ThrowOnFlush;
        public string Read(string key) => Values.TryGetValue(key, out var value) ? value : "";
        public void Write(string key, string value)
        {
            Events.Add(key);
            if (ThrowOnMode && key == "AD_LoginMode") throw new InvalidOperationException("synthetic settings failure");
            Values[key] = value;
        }
        public void Flush()
        {
            Events.Add("flush");
            if (ThrowOnFlush) throw new InvalidOperationException("synthetic flush failure");
        }
    }

    private static bool Prepare(object flow, Settings store, Func<string> generate = null, Func<bool> current = null) =>
        (bool)Call(flow, "TryPrepare", new Func<string, string>(store.Read),
            new Action<string, string>(store.Write), new Action(store.Flush),
            generate ?? (() => Id), current ?? (() => true));

    [Test]
    public void Revival_GuestIdentityIsFlushedBeforeCallerCanAuthenticateAndNotGeneratedAgain()
    {
        var flow = New("LoginGuestIdentityPreparation"); var store = new Settings(); int generated = 0;
        Func<string> generate = () => { generated++; store.Events.Add("generate"); return Id; };
        Assert.That(Prepare(flow, store, generate), Is.True);
        Assert.That(store.Events, Is.EqualTo(new[] { "generate", "AD_CustomId", "AD_LoginMode", "flush" }));
        Assert.That(store.Read("AD_CustomId"), Is.EqualTo(Id));
        Assert.That(store.Read("AD_LoginMode"), Is.EqualTo("custom-pending"));
        Assert.That(Prepare(flow, store, generate), Is.False);
        Assert.That(generated, Is.EqualTo(1));
        Assert.That(Call(Runtime("LoginContinuityPolicy"), "SelectDeviceMode", "custom-pending", true, true), Is.EqualTo("custom"));
    }

    [TestCase("AD_CustomId")]
    [TestCase("AD_LoginMode")]
    [TestCase("AD_LastGpgsId")]
    [TestCase("AD_GoogleSignupPending")]
    public void Revival_GuestSetupNeverOverwritesAnExistingIdentityOrPendingCreation(string key)
    {
        var flow = New("LoginGuestIdentityPreparation"); var store = new Settings();
        store.Values[key] = "existing-data";
        Assert.That(Prepare(flow, store, () => throw new Exception("must not generate")), Is.False);
        Assert.That(store.Read(key), Is.EqualTo("existing-data"));
        Assert.That(store.Events, Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Revival_UncertainGuestWriteBlocksAllFurtherPreparationUntilFreshProcess(bool failDuringWrite)
    {
        var flow = New("LoginGuestIdentityPreparation");
        var store = new Settings { ThrowOnMode = failDuringWrite, ThrowOnFlush = !failDuringWrite };
        Assert.That(Prepare(flow, store), Is.False);
        Assert.That(flow.GetType().GetProperty("HasUncertainWrites").GetValue(flow), Is.True);
        Assert.That(Call(flow, "IsBlockedUntilRestart", Id), Is.True);
        Assert.That(Prepare(flow, new Settings(), () => throw new Exception("must not generate again")), Is.False);
    }

    [Test]
    public void Revival_ChangingContextDuringPersistenceCannotAuthorizeAuthentication()
    {
        var flow = New("LoginGuestIdentityPreparation"); var store = new Settings(); int checks = 0;
        Assert.That(Prepare(flow, store, current: () => ++checks < 3), Is.False);
        Assert.That(store.Events.Last(), Is.EqualTo("flush"));
        Assert.That(flow.GetType().GetProperty("HasUncertainWrites").GetValue(flow), Is.True);
    }

    [Test]
    public void Revival_InvalidOrEmptyNewGuestIdentityNeverWritesSettings()
    {
        var flow = New("LoginGuestIdentityPreparation"); var store = new Settings();
        Assert.That(Prepare(flow, store, () => "tamer-guest-v1-00000000000000000000000000000000"), Is.False);
        Assert.That(store.Events, Is.Empty);
    }

    [TestCase("custom-pending", Id, true)]
    [TestCase("custom", Id, true)]
    [TestCase("", Id, false)]
    [TestCase("gpgs", Id, false)]
    [TestCase("custom", "tamer-guest-v1-broken", false)]
    [TestCase("device", "legacy-custom-value", true)]
    [TestCase("gpgs", "", true)]
    public void Revival_OnlyNewGuestRecordsGainStrictPairValidation(string mode, string id, bool expected)
        => Assert.That(Call(Runtime("LoginGuestIdentityPreparation"), "IsConsistent", mode, id), Is.EqualTo(expected));
}
