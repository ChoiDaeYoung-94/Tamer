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
    public void ProximityTriggerCannotInterceptTheVisibleSign(string tag)
    {
        var root = new GameObject("Revival sign click fixture");
        root.transform.position = new Vector3(15000, 15000, 15000);
        try {
            var proximity = new GameObject("NPC proximity"); proximity.transform.SetParent(root.transform, false);
            proximity.transform.localPosition = new Vector3(0, 0, 2);
            var radius = proximity.AddComponent<SphereCollider>(); radius.radius = 1; radius.isTrigger = true;
            var sign = new GameObject("Original sign target"); sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0, 0, 4); sign.tag = tag;
            var target = sign.AddComponent<BoxCollider>(); target.isTrigger = true;
            Physics.SyncTransforms();
            var ray = new Ray(root.transform.position, Vector3.forward);
            Assert.That(Physics.Raycast(ray, out var original, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide), Is.True);
            Assert.That(original.collider, Is.SameAs(radius), "Reproduce the unfiltered click hitting the proximity volume.");
            Assert.That(Select(ray), Is.SameAs(target));
        } finally { UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
    }

    [Test]
    public void SolidOcclusionStillStopsClicksBeforeTheSign()
    {
        var root = new GameObject("Revival occluded sign fixture");
        root.transform.position = new Vector3(15000, 15000, 15000);
        try {
            var sign = new GameObject("Sign"); sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0, 0, 4); sign.tag = "GoGameScene";
            sign.AddComponent<BoxCollider>().isTrigger = true;
            var wall = new GameObject("Solid wall"); wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0, 0, 2);
            var collider = wall.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Assert.That(Select(new Ray(root.transform.position, Vector3.forward)), Is.SameAs(collider));
            Assert.That(Select(new Ray(root.transform.position + Vector3.right * 3, Vector3.forward)), Is.Null);
        } finally { UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
    }

    private static Collider Select(Ray ray) => (Collider)Find("Player")
        .GetMethod("WorldActionCollider", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { ray });

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
