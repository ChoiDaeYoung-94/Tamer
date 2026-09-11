using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Reflection keeps this asmdef independent of Assembly-CSharp. Every transport and clock is fake.
public class RevivalServerRequestTests
{
    private sealed class ReadCall
    {
        public string Account;
        public Action<Dictionary<string, string>> Success;
        public Action<int> Failure;
    }

    private sealed class WriteCall
    {
        public string Account;
        public Dictionary<string, string> Data;
        public Action Success;
        public Action<int> Failure;
    }

    private sealed class Timer
    {
        public double Due;
        public Action Run;
    }

    private sealed class Harness
    {
        public string Account = "test-account-a";
        public bool Ready = true;
        public bool RejectApply;
        public readonly List<ReadCall> Reads = new List<ReadCall>();
        public readonly List<WriteCall> Writes = new List<WriteCall>();
        public readonly List<Dictionary<string, string>> Applied = new List<Dictionary<string, string>>();
        public readonly List<bool> AppliedUpdates = new List<bool>();
        public Action BeforeApply;
        private readonly List<Timer> _timers = new List<Timer>();
        private double _now;
        private readonly object _manager;
        private readonly Type _type;
        public bool Busy => (bool)_type.GetProperty("IsInProgress").GetValue(_manager);
        public bool Failed => (bool)_type.GetProperty("HasFailed").GetValue(_manager);

        public Harness()
        {
            _type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AD.ServerManager"))
                .First(t => t != null);
            _manager = Activator.CreateInstance(_type, new object[]
            {
                (Func<string>)(() => Account), (Func<bool>)(() => Ready),
                (Action<string, Action<Dictionary<string, string>>, Action<int>>)((account, success, failure) =>
                    Reads.Add(new ReadCall { Account = account, Success = success, Failure = failure })),
                (Action<string, Dictionary<string, string>, Action, Action<int>>)((account, data, success, failure) =>
                    Writes.Add(new WriteCall { Account = account, Data = data, Success = success, Failure = failure })),
                (Action<TimeSpan, Action>)((delay, run) => _timers.Add(new Timer { Due = _now + delay.TotalSeconds, Run = run })),
                (Action<Dictionary<string, string>, bool>)((data, update) =>
                {
                    if (RejectApply) throw new InvalidOperationException("Fake malformed save.");
                    BeforeApply?.Invoke();
                    Applied.Add(data);
                    AppliedUpdates.Add(update);
                })
            });
        }

        private void Call(string name, params object[] arguments)
        {
            try { _type.GetMethod(name).Invoke(_manager, arguments); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }
        public void Set(Dictionary<string, string> data, bool read = false, bool update = false, Action written = null)
            => Call("SetData", data, read, update, written);
        public void Get(bool update = true) => Call("GetAllData", update);
        public void Delete(Dictionary<string, string> data) => Call("DeleteData", data, true);
        public void Cancel() => Call("CancelPendingRequests");
        public void Advance(double seconds)
        {
            double target = _now + seconds;
            int executions = 0;
            while (_timers.Any(t => t.Due <= target))
            {
                Assert.That(++executions, Is.LessThan(100), "Unbounded retry loop");
                var timer = _timers.Where(t => t.Due <= target).OrderBy(t => t.Due).First();
                _timers.Remove(timer);
                _now = timer.Due;
                timer.Run();
            }
            _now = target;
        }
    }

    private static Dictionary<string, string> Data(int count) => Enumerable.Range(0, count)
        .ToDictionary(i => "Key" + i, i => "Value" + i);

    [Test]
    public void Revival_Server_QueuedWritesUseIndependentSnapshotsAndTenKeyChunks()
    {
        var h = new Harness();
        var first = Data(25);
        h.Set(first);
        first["Key0"] = "changed";
        first.Remove("Key20");
        first.Add("AddedLater", "wrong");
        var second = new Dictionary<string, string> { { "Other", "queued-value" } };
        h.Set(second);
        second["Other"] = "changed";
        Assert.That(h.Writes.Count, Is.EqualTo(1));
        for (int i = 0; i < 4; i++)
        {
            Assert.That(h.Busy, Is.True);
            h.Writes[i].Success();
        }
        Assert.That(h.Writes.Select(w => w.Data.Count), Is.EqualTo(new[] { 10, 10, 5, 1 }));
        CollectionAssert.AreEquivalent(Data(25), h.Writes.Take(3).SelectMany(w => w.Data));
        Assert.That(h.Writes[3].Data["Other"], Is.EqualTo("queued-value"));
        Assert.That(h.Busy, Is.False);
        Assert.That(h.Failed, Is.False);
    }

    [Test]
    public void Revival_Server_TransientFailureRetriesOnlyFailedSnapshotChunk()
    {
        var h = new Harness();
        int written = 0;
        h.Set(Data(21), written: () => written++);
        h.Set(new Dictionary<string, string> { { "Later", "value" } });
        h.Writes[0].Success();
        h.Writes[1].Failure(503);
        h.Writes[1].Failure(503);
        h.Advance(1);
        Assert.That(h.Writes.Count, Is.EqualTo(3));
        CollectionAssert.AreEquivalent(h.Writes[1].Data, h.Writes[2].Data);
        h.Writes[1].Success(); // A late success from the failed attempt cannot advance the cursor.
        Assert.That(h.Writes.Count, Is.EqualTo(3));
        h.Writes[2].Success();
        h.Writes[3].Success();
        Assert.That(written, Is.EqualTo(1));
        Assert.That(h.Busy, Is.True);
        h.Writes[4].Success();
        h.Advance(60);
        Assert.That(h.Writes.Count, Is.EqualTo(5));
        Assert.That(h.Failed, Is.False);
    }

    [Test]
    public void Revival_Server_PermanentFailureStopsQueuedWritesAndNextGroupCanRecover()
    {
        var h = new Harness();
        h.Set(Data(1));
        h.Set(Data(2));
        h.Writes[0].Failure(403);
        h.Writes[0].Success();
        h.Advance(60);
        Assert.That(h.Writes.Count, Is.EqualTo(1));
        Assert.That(h.Busy, Is.False);
        Assert.That(h.Failed, Is.True);
        h.Get();
        Assert.That(h.Failed, Is.False);
        h.Reads[0].Success(Data(1));
        Assert.That(h.Applied.Count, Is.EqualTo(1));
        Assert.That(h.Busy, Is.False);
    }

    [Test]
    public void Revival_Server_MissingCallbacksTimeOutAfterThreeAttempts()
    {
        var h = new Harness();
        h.Set(Data(11));
        h.Get();
        h.Advance(60);
        Assert.That(h.Writes.Count, Is.EqualTo(3));
        Assert.That(h.Reads, Is.Empty);
        Assert.That(h.Busy, Is.False);
        Assert.That(h.Failed, Is.True);
        foreach (var write in h.Writes) write.Success();
        Assert.That(h.Writes.Count, Is.EqualTo(3));
    }

    [Test]
    public void Revival_Server_CancellationDiscardsRetryAndOldAccountCallbacks()
    {
        var h = new Harness();
        h.Set(Data(1));
        h.Writes[0].Failure(429);
        h.Cancel();
        Assert.That(h.Failed, Is.True);
        h.Account = "test-account-b";
        h.Get();
        h.Writes[0].Success();
        h.Advance(2);
        Assert.That(h.Writes.Count, Is.EqualTo(1));
        Assert.That(h.Busy, Is.True);
        Assert.That(h.Reads[0].Account, Is.EqualTo("test-account-b"));
        h.Reads[0].Success(Data(1));
        Assert.That(h.Applied.Count, Is.EqualTo(1));
        Assert.That(h.Busy, Is.False);
        Assert.That(h.Failed, Is.False);
    }

    [Test]
    public void Revival_Server_AccountChangeWithoutExplicitCancelCannotApplyRead()
    {
        var h = new Harness();
        h.Get();
        h.Account = "test-account-b";
        h.Reads[0].Success(Data(1));
        Assert.That(h.Applied, Is.Empty);
        Assert.That(h.Failed, Is.True);
        Assert.That(h.Busy, Is.False);
    }

    [Test]
    public void Revival_Server_CancelledReadCannotCompleteNewAttemptForSameAccount()
    {
        var h = new Harness();
        h.Get();
        h.Cancel();
        h.Get();
        h.Reads[0].Success(Data(5));
        h.Reads[0].Failure(503);
        Assert.That(h.Applied, Is.Empty);
        Assert.That(h.Busy, Is.True);
        h.Reads[1].Success(Data(1));
        h.Advance(60);
        Assert.That(h.Reads.Count, Is.EqualTo(2));
        Assert.That(h.Applied.Single().Count, Is.EqualTo(1));
        Assert.That(h.Failed, Is.False);
    }

    [Test]
    public void Revival_Server_ReadRetriesAreBoundedAndKeepQueuedWritesBlocked()
    {
        var h = new Harness();
        h.Get();
        h.Set(Data(1));
        h.Reads[0].Failure(0);
        h.Advance(1);
        h.Reads[1].Failure(500);
        h.Advance(2);
        h.Reads[2].Failure(429);
        h.Advance(60);
        Assert.That(h.Reads.Count, Is.EqualTo(3));
        Assert.That(h.Writes, Is.Empty);
        Assert.That(h.Failed, Is.True);
        Assert.That(h.Busy, Is.False);
    }

    [Test]
    public void Revival_Server_WriteAcknowledgementPrecedesReadAndOccursOnlyOnce()
    {
        var h = new Harness();
        int written = 0;
        h.BeforeApply = () => Assert.That(written, Is.EqualTo(1));
        h.Set(Data(11), read: true, update: true, written: () => written++);
        h.Writes[0].Success();
        h.Writes[1].Success();
        h.Writes[1].Success();
        Assert.That(h.Busy, Is.True);
        Assert.That(h.Reads.Count, Is.EqualTo(1));
        h.Reads[0].Success(Data(2));
        h.Reads[0].Success(Data(3));
        Assert.That(written, Is.EqualTo(1));
        Assert.That(h.Applied.Count, Is.EqualTo(1));
        Assert.That(h.AppliedUpdates.Single(), Is.True);
        Assert.That(h.Busy, Is.False);
        Assert.That(h.Failed, Is.False);
    }

    [Test]
    public void Revival_Server_UnsafeReadApplicationFailsAndStopsQueue()
    {
        var h = new Harness { RejectApply = true };
        h.Get();
        h.Set(Data(1));
        h.Reads[0].Success(Data(1));
        Assert.That(h.Failed, Is.True);
        Assert.That(h.Busy, Is.False);
        Assert.That(h.Writes, Is.Empty);
        Assert.That(h.Applied, Is.Empty);
    }

    [Test]
    public void Revival_Server_NullResponseIsNotTreatedAsEmptyNewAccount()
    {
        var h = new Harness();
        h.Get();
        h.Reads[0].Success(null);
        Assert.That(h.Failed, Is.True);
        Assert.That(h.Busy, Is.False);
        Assert.That(h.Applied, Is.Empty);
    }

    [Test]
    public void Revival_Server_WritesRequireAccountAndSuccessfulSyncButReadsCanInitialize()
    {
        var h = new Harness { Ready = false };
        h.Set(Data(1));
        Assert.That(h.Failed, Is.True);
        Assert.That(h.Writes, Is.Empty);
        h.Get();
        h.Reads[0].Success(new Dictionary<string, string>());
        Assert.That(h.Failed, Is.False);
        Assert.That(h.Applied.Single(), Is.Empty);
        h.Account = "";
        h.Get();
        Assert.That(h.Reads.Count, Is.EqualTo(1));
        Assert.That(h.Failed, Is.True);
    }

    [Test]
    public void Revival_Server_DeleteUsesNullValuesAndSameChunkQueue()
    {
        var h = new Harness();
        h.Delete(Data(12));
        h.Writes[0].Success();
        h.Writes[1].Success();
        Assert.That(h.Writes.Select(w => w.Data.Count), Is.EqualTo(new[] { 10, 2 }));
        Assert.That(h.Writes.SelectMany(w => w.Data.Values).All(v => v == null), Is.True);
        Assert.That(h.Reads.Count, Is.EqualTo(1));
        h.Reads[0].Success(Data(1));
        Assert.That(h.Busy, Is.False);
    }
}
