using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Pure synthetic records: no PlayerPrefs, Managers, file writes, or SDK requests.
public class RevivalInventorySnapshotTests
{
    private static readonly Dictionary<string, string> Catalog = new Dictionary<string, string>
    {
        { "SimpleSword", "Sword" }, { "MasterSword", "Sword" },
        { "SimpleShield", "Shield" }, { "MasterShield", "Shield" }
    };

    private static object Validate(string owner, string[] collection, string[] owned,
        Dictionary<string, string> equipped, string account = "synthetic-A")
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.PlayerInventorySnapshot"))
            .First(t => t != null);
        try
        {
            return type.GetMethod("Validate").Invoke(null,
                new object[] { account, owner, collection, owned, equipped, new[] { "Bat", "Magma" }, Catalog });
        }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static Dictionary<string, string> Slots(string sword = null, string shield = null) =>
        new Dictionary<string, string> { { "Sword", sword }, { "Shield", shield } };

    [TestCase(null)]
    [TestCase("")]
    [TestCase("synthetic-B")]
    [TestCase("Synthetic-A")]
    public void Revival_InventoryRejectsUnownedOrOtherAccount(string owner)
    {
        Assert.Throws<InvalidDataException>(() => Validate(owner, Array.Empty<string>(), Array.Empty<string>(), Slots()));
    }

    [Test]
    public void Revival_InventoryAcceptsEmptyAndDetachesValidatedValues()
    {
        Assert.NotNull(Validate("synthetic-A", Array.Empty<string>(), Array.Empty<string>(), Slots()));
        var collection = new[] { "Bat", "Magma" };
        var owned = new[] { "SimpleSword", "MasterShield" };
        var equipped = Slots("SimpleSword", "MasterShield");
        var result = Validate("synthetic-A", collection, owned, equipped);
        collection[0] = "Unknown";
        owned[0] = "Unknown";
        equipped["Sword"] = "Unknown";
        var type = result.GetType();
        Assert.That(type.GetProperty("Collection").GetValue(result), Is.EqualTo(new[] { "Bat", "Magma" }));
        Assert.That(type.GetProperty("OwnedItems").GetValue(result), Is.EqualTo(new[] { "SimpleSword", "MasterShield" }));
        var saved = (IDictionary<string, string>)type.GetProperty("Equipped").GetValue(result);
        Assert.That(saved["Sword"], Is.EqualTo("SimpleSword"));
        Assert.Throws<NotSupportedException>(() => saved["Sword"] = "MasterSword");
    }

    [TestCase("Bat", "Bat")]
    [TestCase("bat", "Magma")]
    [TestCase("Unknown", "Magma")]
    [TestCase("", "Magma")]
    public void Revival_InventoryRejectsInvalidCollection(string first, string second)
    {
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", new[] { first, second }, Array.Empty<string>(), Slots()));
    }

    [TestCase("SimpleSword", "SimpleSword")]
    [TestCase("Unknown", "MasterSword")]
    public void Revival_InventoryRejectsInvalidOwnedItems(string first, string second)
    {
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", Array.Empty<string>(), new[] { first, second }, Slots()));
    }

    [TestCase("MasterSword", null)] // Known but not owned.
    [TestCase("SimpleShield", null)] // Owned but wrong slot.
    [TestCase("", null)]
    [TestCase(null, "SimpleSword")]
    public void Revival_InventoryRejectsInvalidEquipment(string sword, string shield)
    {
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", Array.Empty<string>(),
            new[] { "SimpleSword", "SimpleShield" }, Slots(sword, shield)));
    }

    [Test]
    public void Revival_InventoryRejectsMissingDataAndUnknownSlots()
    {
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", null, Array.Empty<string>(), Slots()));
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", Array.Empty<string>(), null, Slots()));
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", Array.Empty<string>(), Array.Empty<string>(), null));
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", Array.Empty<string>(), Array.Empty<string>(),
            new Dictionary<string, string> { { "Sword", null }, { "Hat", null } }));
        Assert.Throws<InvalidDataException>(() => Validate("synthetic-A", Array.Empty<string>(), Array.Empty<string>(), Slots(), ""));
    }
}
