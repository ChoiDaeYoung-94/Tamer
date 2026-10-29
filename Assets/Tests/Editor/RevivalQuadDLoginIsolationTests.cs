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
}
