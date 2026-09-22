using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PlayFab.ClientModels;

public class RevivalDeletionTrialTests
{
    private static Type Harness => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("RevivalDeletionTrialHarness")).First(t => t != null);

    [Test]
    public void Revival_DeletionTrialLoginNeverCreatesAccount()
    {
        var request = (LoginWithCustomIDRequest)Harness.GetMethod("LoginRequest").Invoke(null,
            new object[] { "deletion-disposable-" + new string('a',48), "ABC123" });
        Assert.That(request.CreateAccount, Is.False);
        Assert.That(Harness.GetField("ApplicationId").GetRawConstantValue(), Is.EqualTo("com.AeDeong.MonsterTamer.deletiontrial"));
    }

    [TestCase("existing-pgs", "ABC123")]
    [TestCase("deletion-disposable-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "")]
    public void Revival_DeletionTrialRejectsUnboundInput(string custom, string expected)
    {
        Assert.Throws<TargetInvocationException>(() => Harness.GetMethod("LoginRequest").Invoke(null,new object[]{custom,expected}));
    }
}
