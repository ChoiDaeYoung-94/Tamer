using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class RevivalFaceliftSafeAreaTests
{
    private static Rect Normalize(Rect safe, float width, float height)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.FaceliftSafeArea")).First(t => t != null);
        return (Rect)type.GetMethod("Normalize").Invoke(null, new object[] { safe, width, height });
    }

    [Test]
    public void FullScreenPreservesOriginalAnchors()
        => Assert.That(Normalize(new Rect(0, 0, 1080, 1920), 1080, 1920), Is.EqualTo(new Rect(0, 0, 1, 1)));

    [Test]
    public void InsetsReserveBothNotchAndNavigationSpace()
    {
        Rect result = Normalize(new Rect(40, 80, 1000, 2200), 1080, 2400);
        Assert.That(result.xMin, Is.EqualTo(40f / 1080).Within(.00001));
        Assert.That(result.xMax, Is.EqualTo(1040f / 1080).Within(.00001));
        Assert.That(result.yMin, Is.EqualTo(80f / 2400).Within(.00001));
        Assert.That(result.yMax, Is.EqualTo(2280f / 2400).Within(.00001));
    }

    [Test]
    public void InvalidOrUnavailableMetricsCannotCollapseTheUi()
    {
        var full = new Rect(0, 0, 1, 1);
        Assert.That(Normalize(new Rect(0, 0, 1080, 1920), 0, 1920), Is.EqualTo(full));
        Assert.That(Normalize(new Rect(0, 0, 0, 0), 1080, 1920), Is.EqualTo(full));
        Assert.That(Normalize(new Rect(float.NaN, 0, 1080, 1920), 1080, 1920), Is.EqualTo(full));
        Assert.That(Normalize(new Rect(2000, 0, 100, 1920), 1080, 1920), Is.EqualTo(full));
    }

    [Test]
    public void OversizedPlatformBoundsAreClampedToTheDisplay()
        => Assert.That(Normalize(new Rect(-20, -30, 1150, 2000), 1080, 1920), Is.EqualTo(new Rect(0, 0, 1, 1)));
}
