using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RevivalFaceliftInteractionTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    [TestCase("GoGameScene")]
    [TestCase("GoogleAdMob")]
    public void InvisibleMovementBoundaryDoesNotInterceptTheUiPrompt(string tag)
    {
        var root = new GameObject("Revival world prompt fixture");
        root.transform.position = new Vector3(15000, 15000, 15000);
        try {
            int uiLayer = LayerMask.NameToLayer("UI"); Assert.That(uiLayer, Is.GreaterThanOrEqualTo(0));
            var wall = new GameObject("Walls"); wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0, 0, 2); var boundary = wall.AddComponent<BoxCollider>();
            var sign = new GameObject("FaceliftClickTarget"); sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0, 0, 4); sign.tag = tag; sign.layer = uiLayer;
            var target = sign.AddComponent<BoxCollider>(); target.isTrigger = true;
            Physics.SyncTransforms(); var ray = new Ray(root.transform.position, Vector3.forward);
            Assert.That(Physics.Raycast(ray, out var original, 10), Is.True);
            Assert.That(original.collider, Is.SameAs(boundary), "Reproduce the real device's invisible boundary hit.");
            Assert.That(Select(ray), Is.SameAs(target));
        } finally { UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
    }

    [Test]
    public void OrdinaryWorldCollidersDoNotBecomeUiActions()
    {
        var root = new GameObject("Revival non-UI hit fixture");
        root.transform.position = new Vector3(15000, 15000, 15000); root.tag = "GoGameScene";
        try {
            root.AddComponent<BoxCollider>().isTrigger = true; Physics.SyncTransforms();
            Assert.That(Select(new Ray(root.transform.position - Vector3.forward * 2, Vector3.forward)), Is.Null);
        } finally { UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
    }

    private static Collider Select(Ray ray) => (Collider)Find("Player")
        .GetMethod("WorldPromptTarget", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { ray });

    [TestCase(true)]
    [TestCase(false)]
    public void DismissingResultReturnsCompletedPurchasesToListAndKeepsFailureDetails(bool completed)
    {
        WithPopups((shop, popup, info, result, newer) => {
            var label = new GameObject("Result label", typeof(RectTransform));
            label.transform.SetParent(result.transform, false);
            var text = label.AddComponent(Find("TMPro.TextMeshProUGUI"));
            shop.GetType().GetField("_afterBuyText", Members).SetValue(shop, text);
            shop.GetType().GetMethod("ShowPurchaseResult", Members).Invoke(shop, new object[] { "Observed result", completed });
            Assert.That((bool)shop.GetType().GetMethod("DismissPurchaseResult", Members).Invoke(shop, null), Is.True);
            Assert.That(result.activeSelf, Is.False);
            Assert.That(info.activeSelf, Is.EqualTo(!completed));
            Assert.That(newer.activeSelf, Is.True, "Closing these exact panels must preserve a newer popup.");
        });
    }

    [Test]
    public void BlockingOwnerPreservesTheCompletedResultForRetry()
    {
        WithPopups((shop, popup, info, result, newer) => {
            shop.GetType().GetField("_closeItemInfoOnResult", Members).SetValue(shop, true);
            popup.GetType().GetMethod("SetException").Invoke(popup, null);
            Assert.That((bool)shop.GetType().GetMethod("DismissPurchaseResult", Members).Invoke(shop, null), Is.False);
            Assert.That(result.activeSelf && info.activeSelf && newer.activeSelf, Is.True);
            Assert.That(shop.GetType().GetField("_closeItemInfoOnResult", Members).GetValue(shop), Is.True);
            popup.GetType().GetMethod("ReleaseException").Invoke(popup, null);
            Assert.That((bool)shop.GetType().GetMethod("DismissPurchaseResult", Members).Invoke(shop, null), Is.True);
            Assert.That(result.activeSelf || info.activeSelf, Is.False);
        });
    }

    private static void WithPopups(Action<Component, Component, GameObject, GameObject, GameObject> check)
    {
        var managersType = Find("AD.Managers");
        var singleton = managersType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = singleton.GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Revival result UI fixture"); SceneManager.MoveGameObjectToScene(root, scene);
        try {
            var popup = root.AddComponent(Find("AD.PopupManager"));
            var bridge = new GameObject("Inactive Managers"); bridge.transform.SetParent(root.transform, false); bridge.SetActive(false);
            var managers = bridge.AddComponent(managersType); managersType.GetField("_popupM", Members).SetValue(managers, popup);
            singleton.SetValue(null, managers); popup.GetType().GetMethod("ReleaseException").Invoke(popup, null);
            var shopOwner = new GameObject("Inactive shop"); shopOwner.transform.SetParent(root.transform, false); shopOwner.SetActive(false);
            var shop = shopOwner.AddComponent(Find("ShopMan"));
            GameObject Panel(string name) {
                var panel = new GameObject(name); panel.transform.SetParent(root.transform, false);
                popup.GetType().GetMethod("EnablePop").Invoke(popup, new object[] { panel }); return panel;
            }
            var info = Panel("Info"); var result = Panel("Result"); var newer = Panel("Newer popup");
            shop.GetType().GetField("_itemInfoPanel", Members).SetValue(shop, info);
            shop.GetType().GetField("_afterBuyPanel", Members).SetValue(shop, result);
            check(shop, popup, info, result, newer);
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
            singleton.SetValue(null, previous);
        }
    }
}
