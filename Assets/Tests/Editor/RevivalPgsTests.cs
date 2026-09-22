using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PlayFab;
using PlayFab.ClientModels;

public class RevivalPgsTests
{
    private static string OAuthFailure(string message, params string[] structured)
    {
        var error = new PlayFabError { Error = PlayFabErrorCode.GoogleOAuthError, ErrorMessage = message };
        if (structured != null && structured.Length > 0)
            error.ErrorDetails = new Dictionary<string, List<string>> { { "error", structured.ToList() } };
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalPgsHarness")).First(t => t != null);
        return (string)type.GetMethod("SafeOAuthAuthenticationFailure").Invoke(null, new object[] { error });
    }

    [Test]
    public void OAuthDiagnosticsUseExactStructuredFieldsAndRejectConflicts()
    {
        Assert.That(OAuthFailure(null, "invalid_client"), Is.EqualTo("Test authentication: GoogleOAuthError / invalid_client."));
        Assert.That(OAuthFailure("{\"error\":\"redirect_uri_mismatch\"}"), Is.EqualTo("Test authentication: GoogleOAuthError / redirect_uri_mismatch."));
        string unknown = "Test authentication: GoogleOAuthError / Unknown.";
        Assert.That(OAuthFailure("invalid_grant", "invalid_client"), Is.EqualTo(unknown));
        Assert.That(OAuthFailure(null, "invalid_client", "access_denied"), Is.EqualTo(unknown));
        Assert.That(OAuthFailure("{\"error\":\"future_error\",\"description\":\"invalid_client\"}"), Is.EqualTo(unknown));
        Assert.That(OAuthFailure("invalid_client", "invalid_client_extra"), Is.EqualTo(unknown));
        foreach (string input in new[]
        {
            "{\"error\":null,\"description\":\"invalid_client\"}",
            "{\"error\":\"invalid_client\",\"error\":false}",
            "{\"error\":\"invalid_client\",\"error\":\"invalid_grant\"}",
            "{\"error\":\"invalid_\\u0063lient\"}",
            "{\"error\":\"invalid_client\"",
            "{\"error\":false,\"description\":\"access_denied\"}"
        }) Assert.That(OAuthFailure(input), Is.EqualTo(unknown));
    }

    [Test]
    public void OAuthDiagnosticsNeverEchoSyntheticPayloadsOrTokenLookalikes()
    {
        string unknown = "Test authentication: GoogleOAuthError / Unknown.";
        foreach (string input in new[] { null, "", "future_error", "invalid_client_suffix", "prefix_invalid_grant", "https://synthetic.invalid/invalid_client", "code=access_denied", "invalid_client invalid_grant", new string('x', 4097) })
            Assert.That(OAuthFailure(input), Is.EqualTo(unknown));
        Assert.That(OAuthFailure("Synthetic secret=SYNTHETIC_PRIVATE_VALUE; OAuth error \"access_denied\""), Is.EqualTo("Test authentication: GoogleOAuthError / access_denied."));
        Assert.That(OAuthFailure("OAuth error (invalid_grant) synthetic-code=SYNTHETIC_CODE"), Is.EqualTo("Test authentication: GoogleOAuthError / invalid_grant."));
    }

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
    public void PreparationRequiresOptInAndKeepsVerificationNonCreating()
    {
        var c = Config();
        var method = ConfigType.GetMethod("CreatePreparationRequest");
        Assert.That(Assert.Throws<TargetInvocationException>(() => method.Invoke(c, new object[] { "synthetic-create" })).InnerException,
            Is.TypeOf<InvalidOperationException>());
        ConfigType.GetField("allowAccountPreparation").SetValue(c, true);
        var preparation = (LoginWithGooglePlayGamesServicesRequest)method.Invoke(c, new object[] { "synthetic-create" });
        var verification = (LoginWithGooglePlayGamesServicesRequest)ConfigType.GetMethod("CreateRequest").Invoke(c, new object[] { "synthetic-verify" });
        Assert.That(preparation.TitleId, Is.EqualTo("12B656"));
        Assert.That(preparation.CreateAccount, Is.True);
        Assert.That(verification.CreateAccount, Is.False);
        Assert.That(verification.ServerAuthCode, Is.EqualTo("synthetic-verify"));
        Assert.That(preparation.AuthenticationContext, Is.Not.SameAs(verification.AuthenticationContext));
        ConfigType.GetField("testTitle").SetValue(c, "67C9A");
        Assert.That(Assert.Throws<TargetInvocationException>(() => method.Invoke(c, new object[] { "synthetic-create" })).InnerException,
            Is.TypeOf<InvalidOperationException>());
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
