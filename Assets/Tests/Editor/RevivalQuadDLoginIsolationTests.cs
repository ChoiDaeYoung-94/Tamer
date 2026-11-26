using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class RevivalQuadDLoginIsolationTests
{
    private static readonly string[] Defines = { "TAMER_REVIVAL_SMOKE", "TAMER_GAMEPLAY_HARNESS", "TAMER_SHOP_HARNESS",
        "TAMER_SHOP_UI", "TAMER_FACELIFT_UI_HARNESS", "TAMER_QUAD_D_LOGIN_HARNESS" };
    private const string Isolated = "com.AeDeong.MonsterTamer.revival.faceliftintrologin1029";
    private static Type Builder => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalFaceliftBuild")).First(t => t != null);
    private static bool Safe(string app, bool development, bool customKey, bool bundle, string[] symbols)
        => (bool)Builder.GetMethod("IsSafeIntroLoginConfiguration").Invoke(null, new object[] { app, development, customKey, bundle, symbols });

    [Test]
    public void CanonicalIntroPrecedesTheOriginalLoginAndGameplayScenes()
    {
        var scenes = (string[])Builder.GetField("IntroLoginScenes").GetValue(null);
        Assert.That(scenes, Is.EqualTo(new[] { "Assets/QuadD/Scenes/QuadDIntro.unity", "Assets/Scenes/Login.unity",
            "Assets/Scenes/SetCharacter.unity", "Assets/Scenes/Main.unity", "Assets/Scenes/Game.unity", "Assets/Scenes/NextScene.unity" }));
    }

    [Test]
    public void OperatingAndIncompleteIsolationConfigurationsAreRejected()
    {
        Assert.That(Safe(Isolated, true, false, false, Defines), Is.True);
        Assert.That(Safe("com.AeDeong.MonsterTamer", true, false, false, Defines), Is.False);
        Assert.That(Safe(Isolated, false, false, false, Defines), Is.False);
        Assert.That(Safe(Isolated, true, true, false, Defines), Is.False);
        Assert.That(Safe(Isolated, true, false, true, Defines), Is.False);
        foreach (string missing in Defines)
            Assert.That(Safe(Isolated, true, false, false, Defines.Where(d => d != missing).ToArray()), Is.False, missing);
        Assert.That(Safe(Isolated, true, false, false, Defines.Concat(new[] { "TAMER_CHARACTER_UI" }).ToArray()), Is.False);
    }

    [Test]
    public void RequestedAbiKeepsTheArm64DefaultAndAllowsOnlyTheExplicitIntroX86Profile()
    {
        var method = Builder.GetMethod("IsSafeRequestedArchitecture");
        Assert.That(method, Is.Not.Null);
        var abi = method.GetParameters()[3].ParameterType;
        object Arm64() => Enum.Parse(abi, "ARM64");
        object X86() => Enum.Parse(abi, "X86_64");
        bool Allows(bool character, bool intro, bool x86, object value) =>
            (bool)method.Invoke(null, new[] { (object)character, intro, x86, value });
        Assert.That(Allows(false, false, false, Arm64()), Is.True);
        Assert.That(Allows(true, false, false, Arm64()), Is.True);
        Assert.That(Allows(false, true, false, Arm64()), Is.True);
        Assert.That(Allows(false, true, true, X86()), Is.True);
        Assert.That(Allows(false, true, false, X86()), Is.False);
        Assert.That(Allows(false, true, true, Arm64()), Is.False);
        Assert.That(Allows(false, false, true, X86()), Is.False);
        Assert.That(Allows(true, false, true, X86()), Is.False);
        Assert.That(Allows(true, true, false, Arm64()), Is.False);
        Assert.That(Allows(true, true, true, X86()), Is.False);
        Assert.That(Allows(false, true, true, Enum.ToObject(abi, 0)), Is.False);
        var mixed = Convert.ToInt32(Arm64()) | Convert.ToInt32(X86());
        Assert.That(Allows(false, true, true, Enum.ToObject(abi, mixed)), Is.False);
        Assert.That(Allows(false, true, false, Enum.ToObject(abi, mixed)), Is.False);
        Assert.That(Allows(false, true, true, Enum.Parse(abi, "ARMv7")), Is.False);
    }

    [Test]
    public void X86EntryIsSeparateAndExistingSafeIntroIdentityAndDefinesRemainRequired()
    {
        var entry = Builder.GetMethod("RequestX86IntroLoginBuild");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.IsPublic && entry.IsStatic, Is.True);
        Assert.That(entry.GetParameters(), Is.Empty);
        // Never invoke a build request from an Editor NUnit test.
        Assert.That(Builder.GetMethod("RequestIntroLoginBuild"), Is.Not.Null);
        Assert.That(Safe(Isolated, true, false, false, Defines), Is.True);
        Assert.That(Safe("com.AeDeong.MonsterTamer", true, false, false, Defines), Is.False);
        Assert.That(Safe(Isolated, true, true, false, Defines), Is.False);
        Assert.That(Safe(Isolated, true, false, true, Defines), Is.False);
        Assert.That(Safe(Isolated, true, false, false, Defines.Concat(new[] { "TAMER_CHARACTER_UI" }).ToArray()), Is.False);
    }

    [Test]
    public void ReleaseIntroRequiresItsOwnIdentityEngineAndExactSymbols()
    {
        const string releaseId = "com.AeDeong.MonsterTamer.revival.faceliftintro1111release";
        string[] symbols = Defines.Concat(new[] { "TAMER_FACELIFT_RELEASE_INTRO" }).ToArray();
        bool Allows(string app, bool development, bool key, bool bundle, string[] values) =>
            (bool)Builder.GetMethod("IsSafeReleaseIntroLoginConfiguration").Invoke(null,
                new object[] { app, development, key, bundle, values });
        Assert.That(Allows(releaseId, false, false, false, symbols.Reverse().ToArray()), Is.True);
        foreach (string app in new[] { "com.AeDeong.MonsterTamer", Isolated, "com.AeDeong.MonsterTamer.revival.quaddintro1111release" })
            Assert.That(Allows(app, false, false, false, symbols), Is.False, app);
        Assert.That(Allows(releaseId, true, false, false, symbols), Is.False);
        Assert.That(Allows(releaseId, false, true, false, symbols), Is.False);
        Assert.That(Allows(releaseId, false, false, true, symbols), Is.False);
        Assert.That(Allows(releaseId, false, false, false, null), Is.False);
        foreach (string missing in symbols)
            Assert.That(Allows(releaseId, false, false, false, symbols.Where(x => x != missing).ToArray()), Is.False, missing);
        foreach (string extra in new[] { "TAMER_CHARACTER_UI", "TAMER_QUAD_D_SMOKE", "TAMER_FACELIFT_RELEASE_INTRO" })
            Assert.That(Allows(releaseId, false, false, false, symbols.Concat(new[] { extra }).ToArray()), Is.False, extra);
        Assert.That(Safe(Isolated, true, false, false, Defines), Is.True);
        Assert.That(Safe(Isolated, true, false, false, symbols), Is.False);
    }

    [Test]
    public void ReleaseIntroCannotRequestCharacterX86OrMixedArchitecture()
    {
        var entry = Builder.GetMethod("RequestReleaseIntroLoginBuild");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.IsPublic && entry.IsStatic, Is.True);
        Assert.That(entry.GetParameters(), Is.Empty);
        var method = Builder.GetMethod("IsSafeReleaseRequestedArchitecture");
        Type abi = method.GetParameters()[3].ParameterType;
        object Arm64() => Enum.Parse(abi, "ARM64");
        bool Allows(bool character, bool intro, bool x86, object value) =>
            (bool)method.Invoke(null, new[] { (object)character, intro, x86, value });
        Assert.That(Allows(false, true, false, Arm64()), Is.True);
        Assert.That(Allows(true, true, false, Arm64()), Is.False);
        Assert.That(Allows(false, false, false, Arm64()), Is.False);
        Assert.That(Allows(false, true, true, Arm64()), Is.False);
        foreach (object other in new[] { Enum.Parse(abi, "X86_64"), Enum.Parse(abi, "ARMv7"),
            Enum.ToObject(abi, 0), Enum.ToObject(abi, Convert.ToInt32(Arm64()) | Convert.ToInt32(Enum.Parse(abi, "X86_64"))) })
            Assert.That(Allows(false, true, false, other), Is.False);
        // Only inspect the public entry. Tests must never request a player build.
    }

    [Test]
    public void ReleaseRuntimeAndObservationRejectProductionEditorAndSingleSceneContexts()
    {
        Type isolation = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.RevivalGameplayIsolation")).First(t => t != null);
        const string id = "com.AeDeong.MonsterTamer.revival.faceliftintro1111release";
        bool Runtime(string app, bool editor, bool development, bool android) =>
            (bool)isolation.GetMethod("AllowsReleaseFaceliftContext").Invoke(null, new object[] { app, editor, development, android });
        bool Observe(string app, string scene, int count, bool login, bool editor, bool development, bool android) =>
            (bool)isolation.GetMethod("AllowsReleaseIntroObservation").Invoke(null, new object[] { app, scene, count, login, editor, development, android });
        Assert.That(Runtime(id, false, false, true), Is.True);
        Assert.That(Runtime("com.AeDeong.MonsterTamer", false, false, true), Is.False);
        Assert.That(Runtime(Isolated, false, false, true), Is.False);
        Assert.That(Runtime(id, true, false, true), Is.False);
        Assert.That(Runtime(id, false, true, true), Is.False);
        Assert.That(Runtime(id, false, false, false), Is.False);
        const string scene = "Assets/QuadD/Scenes/QuadDIntro.unity";
        Assert.That(Observe(id, scene, 6, true, false, false, true), Is.True);
        Assert.That(Observe(id, scene, 1, true, false, false, true), Is.False);
        Assert.That(Observe(id, scene, 6, false, false, false, true), Is.False);
        Assert.That(Observe(id, "Assets/Scenes/Login.unity", 6, true, false, false, true), Is.False);
        Assert.That(Observe(id, scene, 6, true, true, false, true), Is.False);
        Assert.That(Observe(id, scene, 6, true, false, true, true), Is.False);
        Assert.That(Observe(id, scene, 6, true, false, false, false), Is.False);
    }

    [Test]
    public void ReleaseManifestLabelsOnlyItsLauncherAndKeepsOfflineBackupRules()
    {
        const string original = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'>" +
            "<uses-permission android:name='android.permission.INTERNET'/>" +
            "<application android:label='keep' android:allowBackup='true'>" +
            "<activity android:name='com.unity3d.player.UnityPlayerActivity' android:label='old'/>" +
            "<activity android:name='other' android:label='keep-other'/>" +
            "<provider android:name='com.google.android.gms.ads.MobileAdsInitProvider'/></application></manifest>";
        var method = Builder.GetMethod("ReleaseIntroManifest");
        var xml = System.Xml.Linq.XDocument.Parse((string)method.Invoke(null, new object[] { original }));
        System.Xml.Linq.XNamespace android = "http://schemas.android.com/apk/res/android";
        var app = xml.Root.Element("application");
        Assert.That((string)app.Attribute(android + "label"), Is.EqualTo("keep"));
        Assert.That((string)app.Attribute(android + "allowBackup"), Is.EqualTo("false"));
        Assert.That((string)app.Elements("activity").Single(x => (string)x.Attribute(android + "name") ==
            "com.unity3d.player.UnityPlayerActivity").Attribute(android + "label"), Is.EqualTo("Tamer UI Release"));
        Assert.That((string)app.Elements("activity").Single(x => (string)x.Attribute(android + "name") ==
            "other").Attribute(android + "label"), Is.EqualTo("keep-other"));
        System.Xml.Linq.XNamespace tools = "http://schemas.android.com/tools";
        Assert.That((string)xml.Root.Elements("uses-permission").Single(x =>
            (string)x.Attribute(android + "name") == "android.permission.INTERNET").Attribute(tools + "node"), Is.EqualTo("remove"));
        Assert.That((string)app.Elements("provider").Single(x => (string)x.Attribute(android + "name") ==
            "com.google.android.gms.ads.MobileAdsInitProvider").Attribute(tools + "node"), Is.EqualTo("remove"));
        Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] {
            original.Replace("com.unity3d.player.UnityPlayerActivity", "missing") }));
    }

}
