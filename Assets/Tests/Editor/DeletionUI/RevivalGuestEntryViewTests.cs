using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RevivalGuestEntryViewTests
{
    [Test]
    public void Revival_LoginPrefabReferencesTheExistingKoreanFontForFirstEntry()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/LoginCanvas.prefab");
        Assert.That(prefab, Is.Not.Null);
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.Login")).First(t => t != null);
        var login = prefab.GetComponent(type);
        Assert.That(login, Is.Not.Null);
        var font = type.GetField("_firstEntryFont", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(login) as UnityEngine.Object;
        Assert.That(font, Is.Not.Null, "The English loading font must not be the only input to the Korean choice UI.");
        Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(font)),
            Is.EqualTo("6b62d0bbc19501141b40b8d32a134954"));
    }

    [Test]
    public void Revival_GuestViewUsesEqualExplicitChoicesAndRebindsOnlyOwnedListeners()
    {
        var root = new GameObject("GuestViewTest", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.GraphicRaycaster));
        root.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var panel = new GameObject("Choice", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.InitialAccountChoiceView"))
                .First(t => t != null);
            var view = panel.AddComponent(type);
            int old = 0, google = 0, guest = 0;
            type.GetMethod("Build").Invoke(view, new object[] { null, new Action(() => old++), new Action(() => old++) });
            type.GetMethod("SetChoices").Invoke(view, new object[] { new Action(() => google++), new Action(() => guest++) });
            var googleButton = (UnityEngine.UI.Button)type.GetProperty("GoogleButton").GetValue(view);
            var guestButton = (UnityEngine.UI.Button)type.GetProperty("GuestButton").GetValue(view);
            Assert.That(googleButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight,
                Is.EqualTo(guestButton.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight));
            Assert.That(googleButton.GetComponent<UnityEngine.UI.Image>().color,
                Is.EqualTo(guestButton.GetComponent<UnityEngine.UI.Image>().color));
            Assert.That(googleButton.interactable && guestButton.interactable, Is.True);
            googleButton.onClick.Invoke(); guestButton.onClick.Invoke();
            Assert.That(new[] { old, google, guest }, Is.EqualTo(new[] { 0, 1, 1 }));
            type.GetMethod("SetStatus").Invoke(view, new object[] { "Synthetic authentication failure" });
            Assert.That(panel.transform.Find("Content/Status").gameObject.activeSelf, Is.True);
            type.GetMethod("SetStatus").Invoke(view, new object[] { null });
            Assert.That(panel.transform.Find("Content/Status").gameObject.activeSelf, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
