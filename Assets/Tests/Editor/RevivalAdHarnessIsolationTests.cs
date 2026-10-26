using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;

/// <summary>Pure build-context and synthetic manifest checks; no SDK, scene or player build is started.</summary>
public class RevivalAdHarnessIsolationTests
{
    private const string SamplePackage = "com.AeDeong.MonsterTamer.revival.ads";
    private const string SampleApp = "ca-app-pub-3940256099942544~3347511713";
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type Builder
    {
        get
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("RevivalAdHarnessBuild"))
                .FirstOrDefault(found => found != null);
            Assert.That(type, Is.Not.Null);
            return type;
        }
    }

    private static object Call(string method, params object[] arguments)
    {
        var member = Builder.GetMethod(method, StaticMembers);
        Assert.That(member, Is.Not.Null);
        try { return member.Invoke(null, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    [TestCase(SamplePackage, SampleApp, true)]
    [TestCase("com.AeDeong.MonsterTamer", SampleApp, false)]
    [TestCase(SamplePackage, "ca-app-pub-1234567890123456~1234567890", false)]
    [TestCase(null, SampleApp, false)]
    [TestCase(SamplePackage, null, false)]
    public void Revival_PgsIsolationAddsOnlyExactSampleAndPreservesLegacyUmpFlags(
        string package, string appId, bool sampleIdentity)
    {
        // All 128 flag combinations retain the old UMP truth table plus one exact sample context.
        for (int mask = 0; mask < 128; mask++)
        {
            bool development = (mask & 1) != 0, umpOnly = (mask & 2) != 0;
            bool publisher = (mask & 4) != 0, privacyAge = (mask & 8) != 0;
            bool nativeManager = (mask & 16) != 0, cold = (mask & 32) != 0, retry = (mask & 64) != 0;
            bool oldUmpAllowed = development && umpOnly && publisher && privacyAge;
            bool exactSampleAllowed = sampleIdentity && mask == 1;
            Assert.That(Call("PgsIsolationContextAllowed", development, umpOnly, publisher, privacyAge,
                nativeManager, cold, retry, package, appId), Is.EqualTo(oldUmpAllowed || exactSampleAllowed),
                "Unexpected context for flag mask " + mask);
        }
    }

    [Test]
    public void Revival_IsolatedManifestRemovesOnlyPgsStartupTargets()
    {
        const string original = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'>" +
            "<uses-permission android:name='android.permission.INTERNET'/><application android:label='synthetic'>" +
            "<provider android:name='synthetic.KeptProvider' android:exported='false'/>" +
            "<meta-data android:name='synthetic.kept' android:value='retained'/>" +
            "</application></manifest>";
        var result = XDocument.Parse((string)Call("IsolatedUmpManifest", original));
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XNamespace tools = "http://schemas.android.com/tools";
        var app = result.Root.Element("application");
        var removals = app.Elements().Where(node => (string)node.Attribute(tools + "node") == "remove").ToArray();
        Assert.That(removals.Select(node => node.Name.LocalName + ":" + (string)node.Attribute(android + "name")),
            Is.EquivalentTo(new[] {
                "provider:com.google.android.gms.games.provider.PlayGamesInitProvider",
                "meta-data:com.google.android.gms.games.APP_ID"
            }));
        Assert.That(removals.All(node => node.Attributes().Count() == 2 && !node.HasElements), Is.True);
        Assert.That(app.Elements().Count(), Is.EqualTo(4));
        Assert.That((string)app.Attribute(android + "label"), Is.EqualTo("synthetic"));
        Assert.That((string)app.Elements("provider").Single(node =>
            (string)node.Attribute(android + "name") == "synthetic.KeptProvider").Attribute(android + "exported"),
            Is.EqualTo("false"));
        Assert.That((string)app.Elements("meta-data").Single(node =>
            (string)node.Attribute(android + "name") == "synthetic.kept").Attribute(android + "value"),
            Is.EqualTo("retained"));
        Assert.That((string)result.Root.Element("uses-permission").Attribute(android + "name"),
            Is.EqualTo("android.permission.INTERNET"));
    }

    [TestCase("provider", "com.google.android.gms.games.provider.PlayGamesInitProvider")]
    [TestCase("meta-data", "com.google.android.gms.games.APP_ID")]
    public void Revival_IsolatedManifestRejectsExistingPgsIsolationTargets(string element, string name)
    {
        string original = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application><" +
            element + " android:name='" + name + "'/></application></manifest>";
        var error = Assert.Catch(() => Call("IsolatedUmpManifest", original));
        Assert.That(error.GetType().Name, Is.EqualTo("BuildFailedException"));
    }
}
