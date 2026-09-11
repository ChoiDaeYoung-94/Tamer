using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;

public class RevivalIapIsolationTests
{
    private static Type Runtime(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    [TestCase("ABC", "DEF", "com.AeDeong.MonsterTamer.iaptest", "test-v1", true)]
    [TestCase("ABC", "abc", "com.AeDeong.MonsterTamer.iaptest", "test-v1", false)]
    [TestCase("ABC", "DEF", "com.AeDeong.MonsterTamer", "test-v1", false)]
    [TestCase("ABC", "DEF", "com.AeDeong.MonsterTamer.iaptest", "", false)]
    [TestCase("invalid", "DEF", "com.AeDeong.MonsterTamer.iaptest", "test-v1", false)]
    public void Revival_IapConfigurationRejectsUnsafeComposition(string test, string production, string package, string catalog, bool valid)
    {
        var type = Runtime("AD.RevivalIapIsolation");
        var configType = type.GetNestedType("Configuration");
        var config = Activator.CreateInstance(configType);
        configType.GetField("testTitle").SetValue(config, test);
        configType.GetField("productionTitle").SetValue(config, production);
        configType.GetField("catalog").SetValue(config, catalog);
        TestDelegate action = () => type.GetMethod("Validate").Invoke(null, new[] { config, package });
        if (valid) Assert.DoesNotThrow(action);
        else Assert.Throws<TargetInvocationException>(action);
    }

    [Test]
    public void Revival_IapManifestKeepsBillingAndNetworkWhileRemovingAds()
    {
        const string original = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><uses-permission android:name='android.permission.INTERNET'/><uses-permission android:name='com.android.vending.BILLING'/><application><activity android:name='kept'/></application></manifest>";
        var text = (string)Runtime("RevivalIapBuild").GetMethod("IsolatedManifest").Invoke(null, new object[] { original });
        var root = XDocument.Parse(text).Root;
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        foreach (string permission in new[] { "android.permission.INTERNET", "com.android.vending.BILLING" })
            Assert.That(root.Elements("uses-permission").Single(p => (string)p.Attribute(android + "name") == permission).Attribute(tools + "node"), Is.Null);
        Assert.That(root.Element("application").Element("provider").Attribute(tools + "node").Value, Is.EqualTo("remove"));
    }
}
