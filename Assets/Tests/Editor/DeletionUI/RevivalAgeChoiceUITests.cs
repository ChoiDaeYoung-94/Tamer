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
    public void Revival_AgeViewHasNeutralChoicesSavesDeclineAndRestoresPause()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var originalScene = SceneManager.GetActiveScene();
        Scene layoutScene = default;
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
            // Compare the same objects in a regular Editor scene, where uGUI's
            // ExecuteAlways layout behaviours participate in the normal layout pass.
            layoutScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(root, layoutScene);
            modalRect.ForceUpdateRectTransforms();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Debug.Log("AGE_LAYOUT_EDITOR modal=" + modalRect.rect.size + " content=" + contentRect.rect.size +
                " groupEnabled=" + group.enabled + " groupActive=" + group.isActiveAndEnabled +
                " objectActive=" + contentRect.gameObject.activeInHierarchy + " buttons=" +
                string.Join(",", buttons.Select(b => ((RectTransform)b.transform).rect.width.ToString())));
            Assert.That(modalRect.rect.width, Is.EqualTo(1080).Within(1));
            Assert.That(modalRect.rect.height, Is.EqualTo(1920).Within(1));
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(buttons.All(b => ((RectTransform)b.transform).rect.width > 800), Is.True);
            Assert.That(buttons.All(b => ((RectTransform)b.transform).rect.height > 0), Is.True);
            buttons.Single(b => b.name == "Declined").onClick.Invoke();
            Assert.That(stored, Is.EqualTo("1|declined"));
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(.5f));
            Call(presenter, "Open");
            Assert.That(canvas.gameObject.activeSelf, Is.True, "A saved refusal can be changed in settings.");
        }
        finally
        {
            if (presenter != null) Call(presenter, "OnDisable");
            if (ads != null) Call(ads, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(root);
            if (layoutScene.IsValid()) EditorSceneManager.CloseScene(layoutScene, true);
            if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
            EditorSceneManager.ClosePreviewScene(scene);
            Time.timeScale = time;
        }
    }
}
