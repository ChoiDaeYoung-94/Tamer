using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Integrates the real data and request managers with an in-memory server and a private temp save.
// No Managers singleton, Player object, PlayFab session, real clock, or network is used.
public class RevivalSaveQueueTests
{
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Invoke(object target, string name, params object[] arguments)
    {
        try
        {
            return (target as Type ?? target.GetType()).GetMethod(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Invoke(target is Type ? null : target, arguments);
        }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private sealed class WriteCall
    {
        public Dictionary<string, string> Values;
        public Action Success;
        public Action<int> Failure;
    }

    private sealed class ReadCall
    {
        public Action<Dictionary<string, string>> Success;
        public Action<int> Failure;
    }

    private sealed class SaveQueueHarness : IDisposable
    {
        private const string Account = "test-save-queue-account";
        private readonly string _directory;
        private readonly string _savePath;
        private readonly GameObject _object;
        private readonly Type _dataType;
        private readonly object _data;
        private readonly object _server;
        public readonly List<WriteCall> Writes = new List<WriteCall>();
        public readonly List<ReadCall> Reads = new List<ReadCall>();
        public readonly Dictionary<string, string> Cloud;

        public SaveQueueHarness()
        {
            _directory = Path.Combine(Path.GetTempPath(), "Tamer-RevivalSaveQueue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _savePath = Path.Combine(_directory, "PlayerData.json");
            var defaults = new Dictionary<string, string>
            {
                { "NickName", "QueueTest" }, { "Sex", "Man" }, { "Gold", "100" },
                { "Power", "10" }, { "AttackSpeed", "0.5" }, { "MoveSpeed", "3" },
                { "AllyMonsters", "null" }, { "GooglePlay", "" }
            };
            Cloud = new Dictionary<string, string>(defaults);
            _dataType = RuntimeType("AD.DataManager");
            _object = new GameObject("Revival isolated save queue") { hideFlags = HideFlags.HideAndDontSave };
            _data = _object.AddComponent(_dataType);
            SetField("_defaults", defaults);
            SetField("_playerDataPath", _savePath);
            SetField("_localOwner", Account);
            SetField("LocalPlayerData", new Dictionary<string, string>(defaults));
            SetField("MonsterData", new Dictionary<string, object>());
            _dataType.GetProperty("PlayFabId").GetSetMethod(true).Invoke(_data, new object[] { Account });
            _dataType.GetProperty("IsServerDataReady").GetSetMethod(true).Invoke(_data, new object[] { true });
            _server = Activator.CreateInstance(RuntimeType("AD.ServerManager"), new object[]
            {
                (Func<string>)(() => (string)_dataType.GetProperty("PlayFabId").GetValue(_data)),
                (Func<bool>)(() => (bool)_dataType.GetProperty("IsServerDataReady").GetValue(_data)),
                (Action<string, Action<Dictionary<string, string>>, Action<int>>)((account, success, failure) =>
                {
                    Assert.That(account, Is.EqualTo(Account));
                    Reads.Add(new ReadCall { Success = success, Failure = failure });
                }),
                (Action<string, Dictionary<string, string>, Action, Action<int>>)((account, values, success, failure) =>
                {
                    Assert.That(account, Is.EqualTo(Account));
                    Writes.Add(new WriteCall
                    {
                        Values = new Dictionary<string, string>(values), Success = success, Failure = failure
                    });
                }),
                // Tests release callbacks explicitly. Permanent failures require no retry timer.
                (Action<TimeSpan, Action>)((delay, callback) => { }),
                (Action<Dictionary<string, string>, bool>)((values, update) =>
                {
                    Assert.That(update, Is.True, "DataManager refreshes must hydrate the local snapshot.");
                    ApplyRead(values);
                })
            });
            SetField("_server", _server);
            Invoke(_data, "SaveLocalData");
        }

        private object Field(string name) => _dataType.GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(_data);
        private void SetField(string name, object value) => _dataType.GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(_data, value);
        public Dictionary<string, string> Local => (Dictionary<string, string>)Field("LocalPlayerData");
        public Dictionary<string, string> Pending => (Dictionary<string, string>)Invoke(Field("_changes"), "Snapshot");
        public Dictionary<string, string> Stored => (Dictionary<string, string>)Invoke(_dataType, "ParseData", File.ReadAllText(_savePath));
        public bool Busy => (bool)_server.GetType().GetProperty("IsInProgress").GetValue(_server);
        public bool Failed => (bool)_server.GetType().GetProperty("HasFailed").GetValue(_server);
        public void ChangeGold(string value) => Assert.That(Invoke(_data, "TryUpdateLocalData", "Gold", value), Is.EqualTo(true));
        public void Submit() => Invoke(_data, "UpdatePlayerData");

        private void ApplyRead(Dictionary<string, string> values)
        {
            var field = _dataType.GetField("PlayFabPlayerData");
            var records = (IDictionary)Activator.CreateInstance(field.FieldType);
            var recordType = field.FieldType.GetGenericArguments()[1];
            foreach (var pair in values)
            {
                var record = Activator.CreateInstance(recordType);
                recordType.GetField("Value").SetValue(record, pair.Value);
                records.Add(pair.Key, record);
            }
            field.SetValue(_data, records);
            Invoke(_data, "UpdateData");
        }

        public void SucceedWrite(int index)
        {
            foreach (var pair in Writes[index].Values)
            {
                if (pair.Value == null) Cloud.Remove(pair.Key);
                else Cloud[pair.Key] = pair.Value;
            }
            Writes[index].Success();
        }

        public void SucceedRead(int index) => Reads[index].Success(new Dictionary<string, string>(Cloud));

        public void AssertLatestGoldPending()
        {
            Assert.That(Local["Gold"], Is.EqualTo("100"), "An earlier acknowledgement must not erase a later local mutation.");
            Assert.That(Stored["Gold"], Is.EqualTo("100"), "A follow-up read must not persist an older queued value.");
            Assert.That(Stored["__TamerAccountOwner"], Is.EqualTo(Account));
            CollectionAssert.AreEquivalent(new Dictionary<string, string> { { "Gold", "100" } }, Pending);
        }

        public void Dispose()
        {
            Invoke(_server, "CancelPendingRequests");
            UnityEngine.Object.DestroyImmediate(_object);
            // This exact unique directory is created by the harness, never a project or live-save path.
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }

    private static void QueueFirstTwoWritesAndReturnToOriginalValue(SaveQueueHarness h)
    {
        // Establish a real pending 100 using public mutations; assigning an unchanged value is a no-op.
        h.ChangeGold("101");
        h.ChangeGold("100");
        h.Submit(); // A = 100, active.
        h.ChangeGold("90");
        h.Submit(); // B = 90, queued.
        h.ChangeGold("100"); // A different revision with A's value; no upload yet.
        Assert.That(h.Writes.Count, Is.EqualTo(1));
        h.AssertLatestGoldPending();
    }

    private static void ConfirmFirstTwoWrites(SaveQueueHarness h)
    {
        Assert.That(h.Writes[0].Values["Gold"], Is.EqualTo("100"));
        h.SucceedWrite(0);
        h.AssertLatestGoldPending();
        h.SucceedRead(0);
        h.AssertLatestGoldPending();
        Assert.That(h.Writes[1].Values["Gold"], Is.EqualTo("90"));
        h.SucceedWrite(1);
        h.SucceedRead(1);
        Assert.That(h.Cloud["Gold"], Is.EqualTo("90"));
        h.AssertLatestGoldPending();
    }

    [Test]
    public void Revival_SaveQueue_OlderEqualValueAcknowledgementCannotEraseUnsubmittedRevision()
    {
        using (var h = new SaveQueueHarness())
        {
            QueueFirstTwoWritesAndReturnToOriginalValue(h);
            ConfirmFirstTwoWrites(h);
            Assert.That(h.Writes.Count, Is.EqualTo(2));
            Assert.That(h.Reads.Count, Is.EqualTo(2));
            Assert.That(h.Busy, Is.False);
            Assert.That(h.Failed, Is.False);
        }
    }

    [Test]
    public void Revival_SaveQueue_FailedLatestQueuedRevisionRemainsDurableAndRetriesItsValue()
    {
        using (var h = new SaveQueueHarness())
        {
            QueueFirstTwoWritesAndReturnToOriginalValue(h);
            h.Submit(); // C = 100, queued before A's equal-valued acknowledgement.
            ConfirmFirstTwoWrites(h);
            Assert.That(h.Writes.Count, Is.EqualTo(3));
            Assert.That(h.Writes[2].Values["Gold"], Is.EqualTo("100"));
            h.Writes[2].Failure(403);
            Assert.That(h.Busy, Is.False);
            Assert.That(h.Failed, Is.True);
            h.AssertLatestGoldPending();

            h.Submit();
            Assert.That(h.Writes.Count, Is.EqualTo(4), "Failed C must create an upload, not only a read.");
            Assert.That(h.Writes[3].Values["Gold"], Is.EqualTo("100"));
            h.SucceedWrite(3);
            h.SucceedRead(2);
            Assert.That(h.Pending, Is.Empty);
            Assert.That(h.Cloud["Gold"], Is.EqualTo("100"));
            Assert.That(h.Local["Gold"], Is.EqualTo("100"));
            Assert.That(h.Stored["Gold"], Is.EqualTo("100"));
            Assert.That(h.Busy, Is.False);
            Assert.That(h.Failed, Is.False);
        }
    }

    [Test]
    public void Revival_SaveQueue_ConfirmedLatestWriteIsAcknowledgedEvenWhenItsRefreshFails()
    {
        using (var h = new SaveQueueHarness())
        {
            QueueFirstTwoWritesAndReturnToOriginalValue(h);
            h.Submit();
            ConfirmFirstTwoWrites(h);
            h.SucceedWrite(2);
            Assert.That(h.Pending, Is.Empty, "Only successful C may clear its current revision.");
            h.Reads[2].Failure(403);
            Assert.That(h.Failed, Is.True);
            Assert.That(h.Busy, Is.False);
            Assert.That(h.Stored["Gold"], Is.EqualTo("100"));

            h.Submit();
            Assert.That(h.Writes.Count, Is.EqualTo(3), "An acknowledged write does not need to be uploaded again.");
            Assert.That(h.Reads.Count, Is.EqualTo(4));
            h.SucceedRead(3);
            Assert.That(h.Local["Gold"], Is.EqualTo("100"));
            Assert.That(h.Stored["Gold"], Is.EqualTo("100"));
            Assert.That(h.Pending, Is.Empty);
            Assert.That(h.Busy, Is.False);
            Assert.That(h.Failed, Is.False);
        }
    }
}
