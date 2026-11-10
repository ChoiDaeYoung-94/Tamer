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

}
