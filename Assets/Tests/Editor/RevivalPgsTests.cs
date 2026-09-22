using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PlayFab;
using PlayFab.ClientModels;

public class RevivalPgsTests
{
    private static string Failure(PlayFabErrorCode? code)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalPgsHarness")).First(t => t != null);
        return (string)type.GetMethod("SafeAuthenticationFailure").Invoke(null, new object[] { code });
    }

    [Test]
    public void AuthenticationDiagnosticsDistinguishAccountAndOAuthFailures()
    {
        Assert.That(Failure(PlayFabErrorCode.AccountNotFound), Is.EqualTo("Test authentication: AccountNotFound. No account was created."));
        Assert.That(Failure(PlayFabErrorCode.GoogleOAuthError), Is.EqualTo("Test authentication: GoogleOAuthError."));
        Assert.That(Failure(PlayFabErrorCode.InvalidGooglePlayGamesServerAuthCode), Is.EqualTo("Test authentication: InvalidGooglePlayGamesServerAuthCode."));
    }

    [Test]
    public void AuthenticationDiagnosticsFailClosedForMissingAndUnlistedCodes()
    {
        string fallback = "Test authentication failed. Diagnostic unavailable. No account was created.";
        Assert.That(Failure(null), Is.EqualTo(fallback));
        Assert.That(Failure((PlayFabErrorCode)int.MaxValue), Is.EqualTo(fallback));
        Assert.That(Failure(PlayFabErrorCode.Success), Is.EqualTo(fallback));
        Assert.That(Failure(PlayFabErrorCode.InvalidPassword), Is.EqualTo(fallback));
        foreach (PlayFabErrorCode code in Enum.GetValues(typeof(PlayFabErrorCode)))
            Assert.That(Failure(code), Does.StartWith("Test authentication").And.Not.Contains("\n").And.Not.Contains("\r"));
    }

    private static Type ConfigType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.RevivalPgsTestConfiguration")).First(t => t != null);
    private static object Config(string title = "12B656", string web = "123-synthetic.apps.googleusercontent.com", string game = "123")
    {
        var c = Activator.CreateInstance(ConfigType);
        ConfigType.GetField("testTitle").SetValue(c, title);
        ConfigType.GetField("webClientId").SetValue(c, web);
        ConfigType.GetField("gameId").SetValue(c, game);
        return c;
    }
    private static void Validate(object c, string package = "com.AeDeong.MonsterTamer.revival.pgs")
    {
        try { ConfigType.GetMethod("Validate").Invoke(c, new object[] { package }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    [TestCase("67C9A")]
    [TestCase("")]
    [TestCase(null)]
    public void RejectsNonTestTitle(string title) => Assert.Throws<InvalidOperationException>(() => Validate(Config(title)));
    [TestCase("com.AeDeong.MonsterTamer")]
    [TestCase("com.AeDeong.MonsterTamer.iaptest")]
    [TestCase("com.AeDeong.MonsterTamer.revival.pgs.other")]
    public void RejectsOtherPackage(string package) => Assert.Throws<InvalidOperationException>(() => Validate(Config(), package));
    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-web-client")]
    public void RejectsMissingWebConfiguration(string web) => Assert.Throws<InvalidOperationException>(() => Validate(Config(web:web)));
    [TestCase(null)]
    [TestCase("not-a-game")]
    public void RejectsMissingGameConfiguration(string game) => Assert.Throws<InvalidOperationException>(() => Validate(Config(game:game)));
    [Test]
    public void RequestIsTestOnlyAndDoesNotCreateAccounts()
    {
        var c = Config(); Validate(c);
        var first = (LoginWithGooglePlayGamesServicesRequest)ConfigType.GetMethod("CreateRequest").Invoke(c, new object[] { "synthetic-one" });
        var second = (LoginWithGooglePlayGamesServicesRequest)ConfigType.GetMethod("CreateRequest").Invoke(c, new object[] { "synthetic-two" });
        Assert.That(first.TitleId, Is.EqualTo("12B656"));
        Assert.That(first.CreateAccount, Is.False);
        Assert.That(first.AuthenticationContext, Is.Not.SameAs(second.AuthenticationContext));
        Assert.That(first.ServerAuthCode, Is.EqualTo("synthetic-one"));
    }
    [Test]
    public void EmptyCodeNeverCreatesRequest()
    {
        var error = Assert.Throws<TargetInvocationException>(() => ConfigType.GetMethod("CreateRequest").Invoke(Config(), new object[] { " " }));
        Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
    }
    [Test]
    public void ManifestRemovesBillingAndAdsStartup()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalPgsBuild")).First(t => t != null);
        string xml = (string)type.GetMethod("IsolatedManifest").Invoke(null, new object[] { "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><uses-permission android:name='com.android.vending.BILLING'/><application /></manifest>" });
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
        System.Xml.Linq.XNamespace tools = "http://schemas.android.com/tools";
        Assert.That((string)doc.Root.Element("application").Attribute(android + "allowBackup"), Is.EqualTo("false"));
        Assert.That(doc.Root.Elements("uses-permission").Where(e => (string)e.Attribute(android+"name") == "com.android.vending.BILLING").All(e => (string)e.Attribute(tools+"node") == "remove"), Is.True);
        Assert.That(doc.Root.Element("application").Elements("provider").Any(e => (string)e.Attribute(android+"name") == "com.google.android.gms.ads.MobileAdsInitProvider" && (string)e.Attribute(tools+"node") == "remove"), Is.True);
    }
}
