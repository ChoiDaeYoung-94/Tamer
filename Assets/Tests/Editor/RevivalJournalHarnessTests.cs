using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RevivalJournalHarnessTests
{
    private static Type HarnessType => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("AD.RevivalJournalHarness")).First(t => t != null);
    private static object Call(object target, string name, params object[] args)
    {
        try { return HarnessType.GetMethod(name).Invoke(target, args); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }
    private const string Package = "com.AeDeong.MonsterTamer.revival.journal";

    [Test]
    public void Revival_Journal_RealManagerPersistsPendingAndAckAcrossFreshInstances()
    {
        var root = Path.Combine(Path.GetTempPath(), "RevivalJournal-" + Guid.NewGuid().ToString("N"));
        GameObject go = null;
        try
        {
            Func<object> create = () =>
            {
                go = new GameObject("Synthetic journal test");
                var component = go.AddComponent(HarnessType);
                Call(component, "Initialize", root, Package);
                return component;
            };
            var first = create();
            Call(first, "PreparePending");
            string path = (string)HarnessType.GetProperty("SavePath").GetValue(first);
            string pending = File.ReadAllText(path);
            Assert.Throws<InvalidOperationException>(() => Call(first, "PreparePending"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(pending));
            UnityEngine.Object.DestroyImmediate(go);
            var second = create();
            Call(second, "VerifyPendingRestart");
            Call(second, "Acknowledge");
            Assert.That(HarnessType.GetProperty("PendingCount").GetValue(second), Is.EqualTo(0));
            UnityEngine.Object.DestroyImmediate(go);
            var third = create();
            Call(third, "VerifyAcknowledgedRestart");
            Assert.That(HarnessType.GetProperty("Status").GetValue(third), Does.StartWith("PASS"));
            Assert.That(HarnessType.GetProperty("Gold").GetValue(third), Is.EqualTo("20"));
            Assert.That(HarnessType.GetProperty("SyntheticWrites").GetValue(third), Is.EqualTo(0));
        }
        finally
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
            if (Directory.Exists(root)) Directory.Delete(root, true); // Unique test-owned temp directory only.
        }
    }

    [TestCase("com.AeDeong.MonsterTamer")]
    [TestCase("com.AeDeong.MonsterTamer.revival.progress")]
    public void Revival_Journal_RejectsNonDedicatedPackage(string package)
    {
        Assert.Throws<InvalidOperationException>(() => Call(null, "CreateSavePath", Path.GetTempPath(), package));
    }

    [Test]
    public void Revival_Journal_ManifestRemovesNetworkBillingAndAdIdentifiers()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalJournalBuild")).First(t => t != null);
        var result = (string)type.GetMethod("JournalManifest").Invoke(null,
            new object[] { "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application /></manifest>" });
        var document = System.Xml.Linq.XDocument.Parse(result);
        System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
        System.Xml.Linq.XNamespace tools = "http://schemas.android.com/tools";
        foreach (string permission in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE", "com.android.vending.BILLING", "com.google.android.gms.permission.AD_ID" })
            Assert.That(document.Root.Elements("uses-permission").Single(e => (string)e.Attribute(android + "name") == permission)
                .Attribute(tools + "node").Value, Is.EqualTo("remove"));
    }

    [Test]
    public void Revival_Journal_TestInjectionIsExcludedFromNormalPlayerSources()
    {
        foreach (var file in new[] { "Assets/Scripts/RevivalJournalHarness.cs", "Assets/Scripts/Managers/DataManager.JournalHarness.cs" })
        {
            var source = File.ReadAllText(file).Trim();
            Assert.That(source, Does.StartWith("#if UNITY_EDITOR || TAMER_JOURNAL_HARNESS"));
            Assert.That(source, Does.EndWith("#endif"));
        }
        Assert.That(File.ReadAllText("Assets/Scripts/Managers/DataManager.cs"), Does.Contain("Path.Combine(Application.persistentDataPath, \"PlayerData.json\")"));
    }
}
