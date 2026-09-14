using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RevivalJournalBoundaryTests
{
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args)
    {
        try { return (target as Type ?? target.GetType()).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Invoke(target is Type ? null : target, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }
    private const string Package = "com.AeDeong.MonsterTamer.revival.journal";

    [TestCase(false)]
    [TestCase(true)]
    public void Revival_JournalBoundary_RealWriteCheckpointsExposeConsistentOldAndNewFiles(bool ack)
    {
        string root = Path.Combine(Path.GetTempPath(), "JournalBoundary-" + Guid.NewGuid().ToString("N"));
        var go = new GameObject("Boundary fixture");
        var hook = TypeOf("AD.DataManager").GetField("JournalWriteCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            var h = go.AddComponent(TypeOf("AD.RevivalJournalHarness"));
            Call(h, "Initialize", root, Package);
            Call(h, "PreparePending");
            Call(h, "VerifyPendingRestart");
            var path = (string)h.GetType().GetProperty("SavePath").GetValue(h);
            var oldBytes = File.ReadAllText(path);
            var observed = new List<string>();
            string newBytes = null;
            hook.SetValue(null, (Action<string, string, string>)((target, temp, stage) =>
            {
                if (target != path) return;
                observed.Add(stage);
                if (stage == "temporary-closed")
                {
                    Assert.That(File.ReadAllText(target), Is.EqualTo(oldBytes));
                    newBytes = File.ReadAllText(temp);
                    var data = (Dictionary<string, string>)Call(TypeOf("AD.DataManager"), "ParseData", newBytes);
                    Assert.That(data["Gold"], Is.EqualTo(ack ? "20" : "30"));
                    var parsed = Call(TypeOf("AD.PlayerDataChanges"), "Deserialize", data["__TamerPendingJournal"], data["__TamerAccountOwner"], data);
                    Assert.That(((Dictionary<string, string>)Call(parsed, "Snapshot")).Count, Is.EqualTo(ack ? 0 : 1));
                }
                else
                {
                    Assert.That(File.Exists(temp), Is.False);
                    Assert.That(File.ReadAllText(target), Is.EqualTo(newBytes));
                    oldBytes = newBytes;
                }
            }));
            if (ack) Call(h, "Acknowledge");
            else
            {
                var data = h.GetType().GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(h);
                Assert.That(Call(data, "TryUpdateLocalData", "Gold", "30"), Is.EqualTo(true));
            }
            // Ack also performs a follow-up hydration write; its target remains consistent.
            CollectionAssert.AreEqual(ack ? new[] { "temporary-closed", "replaced", "temporary-closed", "replaced" }
                : new[] { "temporary-closed", "replaced" }, observed);
        }
        finally
        {
            hook.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(go);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public void Revival_JournalBoundary_MalformedFixtureLoadPreservesOriginalAndOrphanTemp()
    {
        string root = Path.Combine(Path.GetTempPath(), "JournalMalformed-" + Guid.NewGuid().ToString("N"));
        var go = new GameObject("Malformed synthetic fixture");
        try
        {
            var type = TypeOf("AD.RevivalJournalHarness");
            var path = (string)Call(type, "CreateSavePath", root, Package);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "null");
            File.WriteAllText(path + ".tmp-synthetic", "incomplete synthetic temp");
            var h = go.AddComponent(type);
            Assert.Throws<InvalidDataException>(() => Call(h, "Initialize", root, Package));
            Assert.That(File.ReadAllText(path), Is.EqualTo("null"));
            Assert.That(File.ReadAllText(path + ".tmp-synthetic"), Is.EqualTo("incomplete synthetic temp"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
