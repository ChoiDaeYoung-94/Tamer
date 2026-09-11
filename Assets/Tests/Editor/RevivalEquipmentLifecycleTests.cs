using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RevivalEquipmentLifecycleTests
{
    private Type _playerType;
    private FieldInfo _singleton;
    private object _previous;
    private object _equipment;
    private Scene _scene;
    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    [SetUp]
    public void SetUp()
    {
        _playerType = Find("Player");
        _singleton = _playerType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        _previous = _singleton.GetValue(null);
        _scene = EditorSceneManager.NewPreviewScene();
        _equipment = Activator.CreateInstance(Find("AD.EquipmentManager"));
    }

    private Component CreatePlayer(string name, string equipped)
    {
        var root = new GameObject("Revival equipment " + name);
        root.SetActive(false); // Never run Player.Awake, game initialization, or PlayerPrefs.
        SceneManager.MoveGameObjectToScene(root, _scene);
        var player = root.AddComponent(_playerType);
        foreach (string field in new[] { "SimpleSword", "MasterSword", "Simpleshield", "Mastershield" })
        {
            var item = new GameObject(field);
            item.transform.SetParent(root.transform);
            item.SetActive(false);
            _playerType.GetField(field).SetValue(player, item);
        }
        _playerType.GetField("PlayerEquippedItems").SetValue(player, new List<string> { equipped });
        _playerType.GetField("EquippedItems").SetValue(player, equipped);
        return player;
    }

    private void Initialize(Component player)
    {
        _singleton.SetValue(null, player);
        _equipment.GetType().GetMethod("Init").Invoke(_equipment, null);
    }
    private IDictionary Map(string name) => (IDictionary)_equipment.GetType().GetField(name).GetValue(_equipment);

    [TearDown]
    public void TearDown()
    {
        try { EditorSceneManager.ClosePreviewScene(_scene); }
        finally { _singleton.SetValue(null, _previous); }
    }

    [Test]
    public void Revival_EquipmentRepeatInitRebindsNewPlayerWithoutChangingEquippedData()
    {
        var first = CreatePlayer("first", "SimpleSword");
        var second = CreatePlayer("second", "MasterShield");
        var mapping = Map("EquipmentMapping");
        var categories = Map("SegmentedEquipment");
        Initialize(first);
        Initialize(first);
        var oldSword = (GameObject)_playerType.GetField("SimpleSword").GetValue(first);
        Assert.That(oldSword.activeSelf, Is.True);
        Initialize(second);
        Assert.That(Map("EquipmentMapping"), Is.SameAs(mapping));
        Assert.That(Map("SegmentedEquipment"), Is.SameAs(categories));
        Assert.That(mapping.Count, Is.EqualTo(4));
        Assert.That(categories.Count, Is.EqualTo(2));
        Assert.That(mapping["SimpleSword"], Is.SameAs(_playerType.GetField("SimpleSword").GetValue(second)));
        Assert.That(((GameObject)mapping["MasterShield"]).activeSelf, Is.True);
        Assert.That(oldSword.activeSelf, Is.True, "Rebinding must not mutate the previous owner's objects.");
        Assert.That(_playerType.GetField("EquippedItems").GetValue(first), Is.EqualTo("SimpleSword"));
        Assert.That(_playerType.GetField("EquippedItems").GetValue(second), Is.EqualTo("MasterShield"));
        Assert.That((IEnumerable<string>)_playerType.GetField("PlayerEquippedItems").GetValue(second),
            Is.EqualTo(new[] { "MasterShield" }));
    }

    [Test]
    public void Revival_EquipmentMissingPlayerDoesNotDiscardExistingMapping()
    {
        Initialize(CreatePlayer("first", "SimpleSword"));
        var mapping = Map("EquipmentMapping");
        object sword = mapping["SimpleSword"];
        _singleton.SetValue(null, null);
        var error = Assert.Throws<TargetInvocationException>(() =>
            _equipment.GetType().GetMethod("Init").Invoke(_equipment, null));
        Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(mapping["SimpleSword"], Is.SameAs(sword));
        Assert.That(mapping.Count, Is.EqualTo(4));
    }
}
