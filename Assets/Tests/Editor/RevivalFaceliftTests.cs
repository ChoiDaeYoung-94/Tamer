using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class RevivalFaceliftTests
{
    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static void Prefab(string path, Action<GameObject> check)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { check(root); } finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void ShopCloseUsesOnlyTheExistingPopupCloseTarget()
    {
        Prefab("Assets/Prefabs/Main/ShopMan.prefab", root => {
            var popup = root.transform.Find("Canvas/Popup_Shop");
            var close = popup.Find("FaceliftClose");
            Assert.That(close, Is.Not.Null);
            var button = close.GetComponent(Find("UnityEngine.UI.Button"));
            var click = (UnityEventBase)button.GetType().GetProperty("onClick").GetValue(button);
            Assert.That(click.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(click.GetPersistentTarget(0), Is.EqualTo(popup.GetComponent(Find("AD.PopupObject"))));
            Assert.That(click.GetPersistentMethodName(0), Is.EqualTo("DisablePop"));
            Assert.That(((RectTransform)close).sizeDelta.x, Is.GreaterThanOrEqualTo(144));
            Assert.That(((RectTransform)close).sizeDelta.y, Is.GreaterThanOrEqualTo(144));
        });
    }

    [Test]
    public void SettingsTemplateStillUsesTheExistingKoreanFont()
    {
        Prefab("Assets/Prefabs/Manager/Manager.prefab", root => {
            var settings = root.transform.Find("Popup_settings");
            var text = settings.GetComponentInChildren(Find("TMPro.TMP_Text"), true);
            var font = (UnityEngine.Object)text.GetType().GetProperty("font").GetValue(text);
            Assert.That(AssetDatabase.GetAssetPath(font), Is.EqualTo("Assets/Fonts/DungGeunMo SDF.asset"));
        });
    }

    [Test]
    public void AdaptiveLayersReferenceTheBackgroundAndPaddedForeground()
    {
        var kind = (PlatformIconKind)Find("UnityEditor.Android.AndroidPlatformIconKind").GetField("Adaptive").GetValue(null);
        var icons = PlayerSettings.GetPlatformIcons(UnityEditor.Build.NamedBuildTarget.Android, kind);
        Assert.That(icons, Is.Not.Empty);
        foreach (var icon in icons) {
            Assert.That(AssetDatabase.GetAssetPath(icon.GetTexture(0)), Is.EqualTo("Assets/Art/Branding/app-icon-adaptive-background-20261029.png"));
            Assert.That(AssetDatabase.GetAssetPath(icon.GetTexture(1)), Is.EqualTo("Assets/Art/Branding/app-icon-adaptive-foreground-20261029.png"));
        }
    }

    [Test]
    public void AdaptiveForegroundLeavesTransparentMaskPadding()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try {
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes("Assets/Art/Branding/app-icon-adaptive-foreground-20261029.png")), Is.True);
            var pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a > 25) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            Assert.That(maxX, Is.GreaterThan(minX));
            Assert.That(minX, Is.GreaterThanOrEqualTo(texture.width / 6));
            Assert.That(minY, Is.GreaterThanOrEqualTo(texture.height / 6));
            Assert.That(maxX, Is.LessThan(texture.width * 5 / 6));
            Assert.That(maxY, Is.LessThan(texture.height * 5 / 6));
        } finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
}
