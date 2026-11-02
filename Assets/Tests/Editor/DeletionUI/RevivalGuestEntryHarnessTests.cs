using System;
using System.Linq;
using NUnit.Framework;

public class RevivalGuestEntryHarnessTests
{
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    [TestCase("com.AeDeong.MonsterTamer.revival.guestentry1102", false, true, true, true)]
    [TestCase("com.AeDeong.MonsterTamer", false, true, true, false)]
    [TestCase("com.AeDeong.MonsterTamer.revival.gameplay", false, true, true, false)]
    [TestCase("com.AeDeong.MonsterTamer.revival.guestentry1102", true, true, true, false)]
    [TestCase("com.AeDeong.MonsterTamer.revival.guestentry1102", false, false, true, false)]
    [TestCase("com.AeDeong.MonsterTamer.revival.guestentry1102", false, true, false, false)]
    public void Revival_GuestUiPrefsRequireTheDedicatedAndroidDevelopmentSandbox(
        string package, bool editor, bool development, bool android, bool expected)
    {
        Assert.That(Runtime("RevivalGuestEntryUiHarness").GetMethod("AllowsContext")
            .Invoke(null, new object[] { package, editor, development, android }), Is.EqualTo(expected));
    }

    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("SOME_UNRELATED_FEATURE", true)]
    [TestCase("TAMER_GUEST_ENTRY_UI_HARNESS", false)]
    [TestCase("SOME_UNRELATED_FEATURE; TAMER_GAMEPLAY_HARNESS", false)]
    [TestCase("TAMER_REVIVAL_SMOKE", false)]
    public void Revival_GuestUiSymbolsCannotLeakIntoGlobalPlayerSettings(string defines, bool expected)
    {
        Assert.That(Runtime("RevivalGuestEntryUiBuild").GetMethod("AllowsGlobalDefines")
            .Invoke(null, new object[] { defines }), Is.EqualTo(expected));
    }
}
