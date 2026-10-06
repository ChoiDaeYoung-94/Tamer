using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RevivalAgeChoiceUITests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object owner, string name, params object[] args) =>
        owner.GetType().GetMethod(name, Flags).Invoke(owner, args);

    [Test]
    public void Revival_PrivacyEntryRestartUsesMemoryAgeAndNeverStartsSdk()
    {
        var managersType = Find("AD.Managers");
        var singleton = managersType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Privacy entry isolated fixture", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        float time = Time.timeScale;
        Component bridge = null;
        try
        {
            root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 1920);
            var inactive = new GameObject("Inactive Managers bridge");
            inactive.transform.SetParent(root.transform, false);
            inactive.SetActive(false);
            bridge = inactive.AddComponent(managersType);
            Assert.That(singleton.GetValue(null), Is.SameAs(previous), "Managers.Awake must not run.");
            Assert.That(managersType.GetField("_initialized", Flags).GetValue(bridge), Is.False);
            Assert.That(managersType.GetField("_ownsServices", Flags).GetValue(bridge), Is.False);
            Assert.That(managersType.GetField("_serverM", Flags).GetValue(bridge), Is.Null);
            Assert.That(managersType.GetField("_dataM", Flags).GetValue(bridge), Is.Null);

            string stored = "";
            var adsObject = new GameObject("Actual ad manager");
            adsObject.transform.SetParent(root.transform, false);
            var ads = adsObject.AddComponent(Find("AD.GoogleAdMobManager"));
            object selection = MemoryAge(ads, () => stored, value => stored = value);
            managersType.GetField("_googleAdMobM", Flags).SetValue(bridge, ads);
            singleton.SetValue(null, bridge);

            var popups = root.AddComponent(Find("AD.PopupManager"));
            var settings = new GameObject("Settings", typeof(RectTransform));
            settings.transform.SetParent(root.transform, false);
            var template = settings.AddComponent(Find("TMPro.TextMeshProUGUI"));
            var font = AssetDatabase.LoadAssetAtPath("Assets/Fonts/DungGeunMo SDF.asset", Find("TMPro.TMP_FontAsset"));
            Assert.That(font, Is.Not.Null);
            template.GetType().GetProperty("font").SetValue(template, font);
            var presenter = root.AddComponent(Find("AD.AgeChoicePresenter"));
            Call(presenter, "Bind", settings, popups, ads);
            var view = Find("AD.DeletionView");
            var createButton = view.GetMethod("Button", BindingFlags.Static | BindingFlags.NonPublic);
            var age = (Button)createButton.Invoke(null, new object[] { "Age", settings.transform, "연령대", font });
            var privacy = (Button)createButton.Invoke(null, new object[] { "Privacy", settings.transform, "개인정보", font });
            var label = view.GetMethod("Label", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { "PrivacyStatus", settings.transform, "", font, 28, 110f });
            var entry = settings.AddComponent(Find("AD.AgePrivacyOptionsEntry"));
            int externalCalls = 0;
            privacy.onClick.AddListener(() => externalCalls++);
            Call(entry, "Bind", age, privacy, label, presenter);
            Call(entry, "Bind", age, privacy, label, presenter);
            Assert.That(RuntimeListeners(privacy), Is.EqualTo(2), "One external and one owned listener.");

            privacy.onClick.Invoke(); // Unknown: real click dispatch and real age modal.
            Assert.That(Result(ads), Is.EqualTo("AgeRequired"));
            var modal = root.transform.Find("AgeChoiceCanvas");
            Assert.That(modal.gameObject.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Status(label), Does.Contain("연령대"));
            modal.GetComponentsInChildren<Button>().Single(b => b.name == "Adult").onClick.Invoke();
            Assert.That(stored, Is.EqualTo("1|18plus"));
            Assert.That(Result(ads), Is.EqualTo("AgeRequired"), "Age selection must not resume discovery.");
            Assert.That(modal.gameObject.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(time));
            privacy.onClick.Invoke();
            Assert.That(Result(ads), Is.EqualTo("Unavailable"));
            Assert.That(Status(label), Does.Contain("사용할 수 없습니다"));
            AssertNoSdk(ads);

            Select(selection, "Declined");
            privacy.onClick.Invoke();
            Assert.That(Result(ads), Is.EqualTo("AgeRequired"));
            Assert.That(modal.gameObject.activeSelf, Is.True);
            modal.GetComponentsInChildren<Button>().Single(b => b.name == "Declined").onClick.Invoke();
            Assert.That(Result(ads), Is.EqualTo("AgeRequired"));
            AssertNoSdk(ads);
            Select(selection, "Under13");
            privacy.onClick.Invoke();
            Assert.That(Result(ads), Is.EqualTo("Unavailable"));
            AssertNoSdk(ads);

            // Normal MonoBehaviours do not guarantee automatic lifecycle dispatch
            // in EditMode. Exercise the real handler explicitly, then remove its object.
            Call(ads, "OnDestroy");
            Assert.That(ads.GetType().GetField("_destroyed", Flags).GetValue(ads), Is.True);
            Assert.That(selection.GetType().GetField("Changed", Flags).GetValue(selection), Is.Null);
            UnityEngine.Object.DestroyImmediate(adsObject);
            Call(entry, "Refresh");
            Assert.That(privacy.gameObject.activeSelf, Is.True);
            Assert.That(privacy.interactable, Is.False);
            var recreated = new GameObject("Recreated actual ad manager");
            recreated.transform.SetParent(root.transform, false);
            ads = recreated.AddComponent(Find("AD.GoogleAdMobManager"));
            MemoryAge(ads, () => stored, value => stored = value);
            managersType.GetField("_googleAdMobM", Flags).SetValue(bridge, ads);
            Call(entry, "Refresh");
            Assert.That(privacy.gameObject.activeSelf && privacy.interactable, Is.True);
            privacy.onClick.Invoke();
            Assert.That(Result(ads), Is.EqualTo("Unavailable"));
            AssertNoSdk(ads);

            int beforeDestroy = externalCalls;
            Call(entry, "OnDestroy"); // Real handler, explicitly dispatched in EditMode.
            UnityEngine.Object.DestroyImmediate(entry);
            Assert.That(RuntimeListeners(privacy), Is.EqualTo(1));
            privacy.onClick.Invoke();
            Assert.That(externalCalls, Is.EqualTo(beforeDestroy + 1));
            Assert.That(Result(ads), Is.EqualTo("Unavailable"));
            Assert.That(inactive.activeInHierarchy, Is.False);
            Assert.That(managersType.GetField("_initialized", Flags).GetValue(bridge), Is.False);
        }
        finally
        {
            try
            {
                if (bridge != null) Call(bridge, "OnDestroy"); // Inert cleanup; never call Init.
                UnityEngine.Object.DestroyImmediate(root);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
            }
            finally
            {
                Time.timeScale = time;
                singleton.SetValue(null, previous);
                Assert.That(singleton.GetValue(null), Is.SameAs(previous));
            }
        }
    }

    private static object MemoryAge(Component ads, Func<string> load, Action<string> save)
    {
        var selection = Activator.CreateInstance(Find("AD.Advertising.LocalAgeChoice"), load, save);
        ads.GetType().GetField("_ageSelection", Flags).SetValue(ads, selection);
        selection.GetType().GetEvent("Changed").AddEventHandler(selection,
            Delegate.CreateDelegate(typeof(Action), ads, ads.GetType().GetMethod("InvalidateAgeContext", Flags)));
        return selection;
    }
    private static void Select(object selection, string choice) => Call(selection, "Select",
        Enum.Parse(Find("AD.Advertising.AgeChoice"), choice));
    private static string Result(Component ads) => ads.GetType().GetProperty("PrivacySettingsResult").GetValue(ads).ToString();
    private static string Status(object label) => (string)label.GetType().GetProperty("text").GetValue(label);
    private static int RuntimeListeners(Button button)
    {
        var calls = typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls", Flags).GetValue(button.onClick);
        return ((System.Collections.ICollection)calls.GetType().GetField("m_RuntimeCalls", Flags).GetValue(calls)).Count;
    }
    private static void AssertNoSdk(Component ads)
    {
        foreach (var name in new[] { "_consent", "_privacyConsent", "_rewardedAd", "_showingAd" })
            Assert.That(ads.GetType().GetField(name, Flags).GetValue(ads), Is.Null, name);
        foreach (var name in new[] { "_initialized", "_initializing", "_loading" })
            Assert.That(ads.GetType().GetField(name, Flags).GetValue(ads), Is.False, name);
    }

    [Test]
    public void Revival_AgeViewHasNeutralChoicesSavesDeclineAndRestoresPause()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Age UI fixture", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        // Reproduce PopupManager's real nested-Canvas hierarchy at a stable size.
        var parentCanvas = root.AddComponent<Canvas>();
        parentCanvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 1920);
        float time = Time.timeScale;
        Component ads = null, presenter = null;
        try
        {
            ads = root.AddComponent(Find("AD.GoogleAdMobManager"));
            var popups = root.AddComponent(Find("AD.PopupManager"));
            var settings = new GameObject("Settings", typeof(RectTransform));
            settings.transform.SetParent(root.transform, false);
            var template = settings.AddComponent(Find("TMPro.TextMeshProUGUI"));
            var font = AssetDatabase.LoadAssetAtPath("Assets/Fonts/DungGeunMo SDF.asset", Find("TMPro.TMP_FontAsset"));
            template.GetType().GetProperty("font").SetValue(template, font);
            string stored = null;
            var selection = Activator.CreateInstance(Find("AD.Advertising.LocalAgeChoice"),
                (Func<string>)(() => stored), (Action<string>)(s => stored = s));
            ads.GetType().GetField("_ageSelection", Flags).SetValue(ads, selection);
            presenter = root.AddComponent(Find("AD.AgeChoicePresenter"));
            Call(presenter, "Bind", settings, popups, ads);
            Time.timeScale = .5f;
            Call(presenter, "Open");
            var canvas = root.transform.Find("AgeChoiceCanvas").GetComponent<Canvas>();
            Assert.That(canvas.gameObject.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            var buttons = canvas.GetComponentsInChildren<Button>();
            Assert.That(buttons.Length, Is.EqualTo(5));
            Assert.That(buttons.Select(b => b.GetComponent<LayoutElement>().preferredHeight).Distinct().Count(), Is.EqualTo(1));
            Assert.That(buttons.All(b => b.navigation.mode == Navigation.Mode.None), Is.True);
            Assert.That(buttons.Select(b => b.targetGraphic.color).Distinct().Count(), Is.EqualTo(1));
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform.Find("Content"));
            var modalRect = (RectTransform)canvas.transform;
            var contentRect = (RectTransform)canvas.transform.Find("Content");
            var group = contentRect.GetComponent<VerticalLayoutGroup>();
            Debug.Log("AGE_LAYOUT_PREVIEW modal=" + modalRect.rect.size + " content=" + contentRect.rect.size +
                " groupEnabled=" + group.enabled + " groupActive=" + group.isActiveAndEnabled +
                " objectActive=" + contentRect.gameObject.activeInHierarchy + " buttons=" +
                string.Join(",", buttons.Select(b => ((RectTransform)b.transform).rect.width.ToString())));
            Assert.That(modalRect.rect.width, Is.EqualTo(1080).Within(1));
            Assert.That(modalRect.rect.height, Is.EqualTo(1920).Within(1));
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(canvas.sortingOrder, Is.EqualTo(30000));
            Assert.That(buttons.All(b => ((RectTransform)b.transform).rect.width > 800), Is.True);
            Assert.That(buttons.All(b => ((RectTransform)b.transform).rect.height > 0), Is.True);
            foreach (RectTransform child in contentRect)
                Assert.That(child.rect.width, Is.LessThanOrEqualTo(contentRect.rect.width + 1), child.name);
            buttons.Single(b => b.name == "Declined").onClick.Invoke();
            Assert.That(stored, Is.EqualTo("1|declined"));
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(.5f));
            Call(presenter, "Open");
            Assert.That(canvas.gameObject.activeSelf, Is.True, "A saved refusal can be changed in settings.");
            Assert.That(canvas.overrideSorting, Is.True, "Reopening must restore the overlay ordering.");
            Assert.That(canvas.sortingOrder, Is.EqualTo(30000));
        }
        finally
        {
            if (presenter != null) Call(presenter, "OnDisable");
            if (ads != null) Call(ads, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
            Time.timeScale = time;
        }
    }
}
