using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Reflection avoids an Assembly-CSharp dependency. No Managers, PlayerPrefs, SDK calls, or live saves.
public class RevivalDataSyncTests
{
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    private static object Invoke(object target, string method, params object[] args)
    {
        try
        {
            return (target as Type ?? target.GetType()).GetMethod(method,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Invoke(target is Type ? null : target, args);
        }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static Dictionary<string, string> Defaults() => new Dictionary<string, string>
    {
        { "NickName", "null" }, { "Sex", "null" }, { "Gold", "0" },
        { "Power", "10" }, { "AllyMonsters", "null" }, { "GooglePlay", "" }
    };

    private static Dictionary<string, string> Merge(Dictionary<string, string> defaults,
        Dictionary<string, string> local, Dictionary<string, string> server,
        Dictionary<string, string> pending) => (Dictionary<string, string>)Invoke(
            RuntimeType("AD.PlayerDataSyncPolicy"), "Merge", defaults, local, server, pending);

    private sealed class SaveHarness : IDisposable
    {
        private readonly GameObject _object;
        private readonly object _manager;
        private readonly Type _type;
        public readonly string DirectoryPath;
        public readonly string SavePath;
        public const string Original = "{\"Gold\":\"900\",\"GooglePlay\":\"ProductNoAds\",\"LegacyOnly\":\"keep\"}";

        public SaveHarness()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "Tamer-RevivalDataSync-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            SavePath = Path.Combine(DirectoryPath, "PlayerData.json");
            File.WriteAllText(SavePath, Original);
            _type = RuntimeType("AD.DataManager");
            _object = new GameObject("Revival isolated data test") { hideFlags = HideFlags.HideAndDontSave };
            _manager = _object.AddComponent(_type);
            SetField("_defaults", Defaults());
            SetField("_playerDataPath", SavePath);
            SetField("LocalPlayerData", new Dictionary<string, string>
            {
                { "Gold", "900" }, { "GooglePlay", "ProductNoAds" }, { "LegacyOnly", "keep" }
            });
            SetProperty("PlayFabId", "test-account-a");
        }

        public Dictionary<string, string> Local => (Dictionary<string, string>)GetField("LocalPlayerData");
        public bool Ready => (bool)_type.GetProperty("IsServerDataReady").GetValue(_manager);
        public object Changes => GetField("_changes");
        public object GetField(string name) => _type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(_manager);
        public void SetField(string name, object value) => _type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(_manager, value);
        public void SetProperty(string name, object value) => _type.GetProperty(name)
            .GetSetMethod(true).Invoke(_manager, new[] { value });
        public void Server(Dictionary<string, string> data, bool nullRecord = false)
        {
            var field = _type.GetField("PlayFabPlayerData");
            var records = (IDictionary)Activator.CreateInstance(field.FieldType);
            var recordType = field.FieldType.GetGenericArguments()[1];
            foreach (var pair in data)
            {
                object record = null;
                if (!nullRecord)
                {
                    record = Activator.CreateInstance(recordType);
                    recordType.GetField("Value").SetValue(record, pair.Value);
                }
                records.Add(pair.Key, record);
            }
            field.SetValue(_manager, records);
        }
        public void Sync() => Invoke(_manager, "UpdateData");
        public bool Update(string key, string value) => (bool)Invoke(_manager, "TryUpdateLocalData", key, value);
        public Dictionary<string, string> Pending() => (Dictionary<string, string>)Invoke(Changes, "Snapshot");
        public Dictionary<string, string> Stored() => (Dictionary<string, string>)Invoke(_type, "ParseData", File.ReadAllText(SavePath));
        public string[] Backups() => Directory.Exists(Path.Combine(DirectoryPath, "PlayerDataBackups"))
            ? Directory.GetFiles(Path.Combine(DirectoryPath, "PlayerDataBackups")) : Array.Empty<string>();
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(_object);
            // The directory is uniquely created by this harness under the OS temporary directory.
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }

    [Test]
    public void Revival_Data_ServerSnapshotWinsAndMissingKeysUseDefaultsRegardlessOfCounts()
    {
        var defaults = Defaults();
        var local = new Dictionary<string, string> { { "Gold", "900" }, { "LocalOnly", "stale" } };
        var server = new Dictionary<string, string> { { "Gold", "50" }, { "FutureServerKey", "retain" } };
        var merged = Merge(defaults, local, server, new Dictionary<string, string>());
        Assert.That(merged["Gold"], Is.EqualTo("50"));
        Assert.That(merged["Power"], Is.EqualTo("10"));
        Assert.That(merged["FutureServerKey"], Is.EqualTo("retain"));
        Assert.That(merged.ContainsKey("LocalOnly"), Is.False);
        Assert.That(local["Gold"], Is.EqualTo("900"));
        Assert.That(defaults["Gold"], Is.EqualTo("0"));
        Assert.That(server.Count, Is.EqualTo(2));
    }

    [Test]
    public void Revival_Data_OnlyExplicitPendingChangesOverrideCloudValues()
    {
        var local = new Dictionary<string, string> { { "Gold", "900" }, { "Power", "999" } };
        var server = new Dictionary<string, string> { { "Gold", "50" }, { "Power", "20" } };
        var pending = new Dictionary<string, string> { { "Gold", "30" }, { "GoogleAdMob", "pending-time" } };
        var merged = Merge(Defaults(), local, server, pending);
        Assert.That(merged["Gold"], Is.EqualTo("30"));
        Assert.That(merged["Power"], Is.EqualTo("20"));
        Assert.That(merged["GoogleAdMob"], Is.EqualTo("pending-time"));
        Assert.That(pending.Count, Is.EqualTo(2));
    }

    [Test]
    public void Revival_Data_PendingEntitlementSurvivesEvenWhenItIsAbsentFromEarlierSnapshots()
    {
        var merged = Merge(Defaults(), new Dictionary<string, string> { { "GooglePlay", "LocalProduct" } },
            new Dictionary<string, string> { { "GooglePlay", "ServerProduct" } },
            new Dictionary<string, string> { { "GooglePlay", "ProductNoAds" } });
        Assert.That(merged["GooglePlay"], Is.EqualTo("ServerProduct,LocalProduct,ProductNoAds"));
    }

    [TestCase(null, null, "")]
    [TestCase("null, ProductNoAds,ProductNoAds", "ProductNoAds", "ProductNoAds")]
    [TestCase("OtherProduct,ProductNoAds", "ProductNoAds,FutureProduct", "OtherProduct,ProductNoAds,FutureProduct")]
    [TestCase("ProductNoAdsTrial", "ProductNoAds", "ProductNoAdsTrial,ProductNoAds")]
    [TestCase(" productnoads ,,null ", "", "productnoads")]
    public void Revival_Data_EntitlementsUseExactDistinctCsvTokens(string first, string second, string expected)
    {
        Assert.That(Invoke(RuntimeType("AD.PlayerDataSyncPolicy"), "UnionEntitlements", first, second), Is.EqualTo(expected));
    }

    [TestCase("", "test-account-a", true)]
    [TestCase("test-account-a", "test-account-a", true)]
    [TestCase("test-account-a", "test-account-b", false)]
    [TestCase("test-account-a", "TEST-ACCOUNT-A", false)]
    [TestCase("", "", false)]
    [TestCase(null, null, false)]
    public void Revival_Data_AccountBindingRequiresAnExactExistingOwner(string owner, string account, bool expected)
    {
        Assert.That(Invoke(RuntimeType("AD.PlayerDataSyncPolicy"), "CanBindAccount", owner, account), Is.EqualTo(expected));
    }

    [Test]
    public void Revival_Data_AcknowledgementRetainsChangesMadeWhileUploadWasInFlight()
    {
        var changes = Activator.CreateInstance(RuntimeType("AD.PlayerDataChanges"));
        Invoke(changes, "Track", "Gold", "10");
        Invoke(changes, "Track", "AllyMonsters", "Slime");
        var submitted = (Dictionary<string, string>)Invoke(changes, "Snapshot");
        Invoke(changes, "Track", "Gold", "15");
        Invoke(changes, "Track", "GooglePlay", "ProductNoAds");
        Invoke(changes, "Acknowledge", submitted);
        var pending = (Dictionary<string, string>)Invoke(changes, "Snapshot");
        CollectionAssert.AreEquivalent(new Dictionary<string, string> { { "Gold", "15" }, { "GooglePlay", "ProductNoAds" } }, pending);
        submitted["Gold"] = "mutated copy";
        Assert.That(((Dictionary<string, string>)Invoke(changes, "Snapshot"))["Gold"], Is.EqualTo("15"));
        Invoke(changes, "Clear");
        Assert.That((Dictionary<string, string>)Invoke(changes, "Snapshot"), Is.Empty);
    }

    [Test]
    public void Revival_Data_HydrationPreservesOriginalBackupBindsOwnerAndKeepsNoAds()
    {
        using (var h = new SaveHarness())
        {
            h.Server(new Dictionary<string, string> { { "Gold", "50" }, { "FutureServerKey", "retain" }, { "GooglePlay", "" } });
            h.Sync();
            Assert.That(h.Ready, Is.True);
            Assert.That(h.Local["Gold"], Is.EqualTo("50"));
            Assert.That(h.Local["Power"], Is.EqualTo("10"));
            Assert.That(h.Local["GooglePlay"], Is.EqualTo("ProductNoAds"));
            Assert.That(h.Local["FutureServerKey"], Is.EqualTo("retain"));
            Assert.That(h.Local.ContainsKey("LegacyOnly"), Is.False);
            Assert.That(h.Stored()["__TamerAccountOwner"], Is.EqualTo("test-account-a"));
            Assert.That(h.Stored()["Gold"], Is.EqualTo("50"));
            Assert.That(h.Local.ContainsKey("__TamerAccountOwner"), Is.False, "Local metadata must not become cloud data.");
            Assert.That(h.GetField("_localOwner"), Is.EqualTo("test-account-a"));
            Assert.That(h.Backups().Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(h.Backups().Single()), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Pending(), Is.Empty, "Hydration must not turn legacy data into an upload patch.");
            h.Sync();
            Assert.That(h.Backups().Length, Is.EqualTo(1), "A later read must not replace the original backup.");
        }
    }

    [Test]
    public void Revival_Data_MissingSnapshotCannotReplaceOrBindLegacySave()
    {
        using (var h = new SaveHarness())
        {
            var previous = h.Local;
            Assert.Throws<InvalidOperationException>(() => h.Sync());
            Assert.That(h.Local, Is.SameAs(previous));
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Stored().ContainsKey("__TamerAccountOwner"), Is.False);
            Assert.That(h.Backups(), Is.Empty);
            Assert.That(h.Ready, Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_Data_IncompleteCloudRecordsPreserveLegacySave(bool nullRecord)
    {
        using (var h = new SaveHarness())
        {
            h.Server(new Dictionary<string, string> { { "Gold", null } }, nullRecord);
            var previous = h.Local;
            Assert.Throws<InvalidDataException>(() => h.Sync());
            Assert.That(h.Local, Is.SameAs(previous));
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Stored().ContainsKey("__TamerAccountOwner"), Is.False);
            Assert.That(h.Backups(), Is.Empty);
        }
    }

    [Test]
    public void Revival_Data_OwnerMismatchCannotTransferProgressOrNoAds()
    {
        using (var h = new SaveHarness())
        {
            h.SetField("_localOwner", "test-account-b");
            const string ownedOriginal = "{\"__TamerAccountOwner\":\"test-account-b\",\"Gold\":\"900\"}";
            File.WriteAllText(h.SavePath, ownedOriginal);
            h.Server(new Dictionary<string, string> { { "Gold", "50" } });
            Assert.Throws<InvalidOperationException>(() => h.Sync());
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(ownedOriginal));
            Assert.That(h.Stored()["__TamerAccountOwner"], Is.EqualTo("test-account-b"));
            Assert.That(h.Local["GooglePlay"], Is.EqualTo("ProductNoAds"));
            Assert.That(h.Backups(), Is.Empty);
            Assert.That(h.Ready, Is.False);
        }
    }

    [TestCase("Gold", "not-a-number")]
    [TestCase("Gold", "2147483648")]
    [TestCase("Power", "NaN")]
    [TestCase("AttackSpeed", "Infinity")]
    [TestCase("MoveSpeed", "null")]
    public void Revival_Data_InvalidGameplayValuesCannotReplaceLocalSave(string key, string value)
    {
        using (var h = new SaveHarness())
        {
            h.Server(new Dictionary<string, string> { { key, value } });
            Assert.Throws<InvalidDataException>(() => h.Sync());
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Stored().ContainsKey("__TamerAccountOwner"), Is.False);
            Assert.That(h.Ready, Is.False);
        }
    }

    [Test]
    public void Revival_Data_DurableMutationWorksBeforePlayerExistsAndTracksOnlyChangedKeys()
    {
        using (var h = new SaveHarness())
        {
            h.SetProperty("IsServerDataReady", true);
            Assert.That(h.Update("Gold", "75"), Is.True);
            Assert.That(h.Update("GooglePlay", "FutureProduct,ProductNoAds"), Is.True);
            Assert.That(h.Update("GooglePlay", ""), Is.True, "An empty restore must not revoke a durable grant.");
            Assert.That(h.Local["GooglePlay"], Is.EqualTo("ProductNoAds,FutureProduct"));
            Assert.That(File.ReadAllText(h.SavePath), Does.Contain("75").And.Contain("ProductNoAds,FutureProduct"));
            CollectionAssert.AreEquivalent(new Dictionary<string, string>
                { { "Gold", "75" }, { "GooglePlay", "ProductNoAds,FutureProduct" } }, h.Pending());
            Assert.That(h.Backups(), Is.Empty, "This test does not hydrate or call any server.");
        }
    }

    [Test]
    public void Revival_Data_MutationBeforeHydrationLeavesOriginalAndPendingUntouched()
    {
        using (var h = new SaveHarness())
        {
            Assert.That(h.Update("Gold", "75"), Is.False);
            Assert.That(h.Update("GooglePlay", "OtherProduct"), Is.False);
            Assert.That(h.Local["Gold"], Is.EqualTo("900"));
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Pending(), Is.Empty);
        }
    }

    [TestCase("Gold")]
    [TestCase("NewKey")]
    public void Revival_Data_LocalWriteFailureRollsBackMemoryAndDoesNotTrackUncommittedValue(string key)
    {
        using (var h = new SaveHarness())
        {
            h.SetProperty("IsServerDataReady", true);
            h.SetField("_playerDataPath", Path.Combine(h.DirectoryPath, "missing-parent", "PlayerData.json"));
            Assert.That(h.Update(key, "uncommitted"), Is.False);
            Assert.That(h.Local["Gold"], Is.EqualTo("900"));
            Assert.That(h.Local.ContainsKey("NewKey"), Is.False);
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Pending(), Is.Empty);
            Assert.That(Directory.GetFiles(h.DirectoryPath, "*.tmp-*"), Is.Empty);
        }
    }

    [Test]
    public void Revival_Data_FailedHydrationDoesNotCommitOwnershipOrMemory()
    {
        using (var h = new SaveHarness())
        {
            // A directory as the destination makes the body write fail on every supported OS.
            string blockedPath = Path.Combine(h.DirectoryPath, "blocked-save");
            Directory.CreateDirectory(blockedPath);
            h.SetField("_playerDataPath", blockedPath);
            h.Server(new Dictionary<string, string> { { "Gold", "50" } });
            var previous = h.Local;
            var error = Assert.Catch<Exception>(() => h.Sync());
            Assert.That(error, Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
            Assert.That(h.Local, Is.SameAs(previous));
            Assert.That(h.GetField("_localOwner"), Is.EqualTo(""));
            Assert.That(h.Ready, Is.False);
            Assert.That(Directory.GetFiles(h.DirectoryPath, "blocked-save*"), Is.Empty,
                "An unsuccessful hydration cannot leave owner metadata or temporary files.");
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
        }
    }

    [Test]
    public void Revival_Data_ReservedOwnerKeyCannotBeUploadedOrProvidedByCloud()
    {
        using (var h = new SaveHarness())
        {
            h.SetProperty("IsServerDataReady", true);
            Assert.That(h.Update("__TamerAccountOwner", "test-account-b"), Is.False);
            Assert.That(h.Pending(), Is.Empty);
            h.Server(new Dictionary<string, string> { { "__TamerAccountOwner", "test-account-b" } });
            Assert.Throws<InvalidDataException>(() => h.Sync());
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Local.ContainsKey("__TamerAccountOwner"), Is.False);
            Assert.That(h.Backups(), Is.Empty);
        }
    }

    [TestCase("[]")]
    [TestCase("null")]
    public void Revival_Data_InvalidLocalObjectIsRejectedWithoutRewritingTheSource(string contents)
    {
        using (var h = new SaveHarness())
        {
            File.WriteAllText(h.SavePath, contents);
            Assert.Throws<InvalidDataException>(() => h.Stored());
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(contents));
            Assert.That(h.GetField("_localOwner"), Is.EqualTo(""));
            Assert.That(h.Ready, Is.False);
            Assert.That(h.Pending(), Is.Empty);
        }
    }

    [Test]
    public void Revival_Data_CloudRefreshRetainsDurableChangesStillWaitingForAcknowledgement()
    {
        using (var h = new SaveHarness())
        {
            h.Server(new Dictionary<string, string> { { "Gold", "50" }, { "Power", "20" } });
            h.Sync();
            Assert.That(h.Update("Gold", "35"), Is.True);
            h.Server(new Dictionary<string, string> { { "Gold", "60" }, { "Power", "25" } });
            h.Sync();
            Assert.That(h.Local["Gold"], Is.EqualTo("35"));
            Assert.That(h.Local["Power"], Is.EqualTo("25"));
            Assert.That(h.Stored()["Gold"], Is.EqualTo("35"));
            Assert.That(h.Stored()["__TamerAccountOwner"], Is.EqualTo("test-account-a"));
            CollectionAssert.AreEquivalent(new Dictionary<string, string> { { "Gold", "35" } }, h.Pending());
        }
    }

    [Test]
    public void Revival_Data_AtomicReplacementFailurePreservesExistingOriginalAndOwnership()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor)
            Assert.Ignore("This case uses Windows file sharing to reject atomic replacement of an existing save.");
        using (var h = new SaveHarness())
        {
            h.Server(new Dictionary<string, string> { { "Gold", "50" } });
            var previous = h.Local;
            using (var locked = new FileStream(h.SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var error = Assert.Catch<Exception>(() => h.Sync());
                Assert.That(error, Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
            }
            Assert.That(File.ReadAllText(h.SavePath), Is.EqualTo(SaveHarness.Original));
            Assert.That(h.Local, Is.SameAs(previous));
            Assert.That(h.GetField("_localOwner"), Is.EqualTo(""));
            Assert.That(h.Ready, Is.False);
            Assert.That(h.Stored().ContainsKey("__TamerAccountOwner"), Is.False);
            Assert.That(File.ReadAllText(h.Backups().Single()), Is.EqualTo(SaveHarness.Original));
            Assert.That(Directory.GetFiles(h.DirectoryPath, "*.tmp-*"), Is.Empty);
        }
    }
}
