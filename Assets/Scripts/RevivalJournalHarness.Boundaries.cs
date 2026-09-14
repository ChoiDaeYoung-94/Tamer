#if UNITY_EDITOR || TAMER_JOURNAL_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AD
{
    public sealed partial class RevivalJournalHarness
    {
        private static readonly string[] BoundaryCases = { "mutation-before", "mutation-after", "ack-before", "ack-after" };

        private string BoundaryRoot(string name)
        {
            if (Array.IndexOf(BoundaryCases, name) < 0) throw new ArgumentException("Unknown boundary case.");
            return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_path)), "AtomicBoundariesV1", name);
        }

        public void RunBoundaryCase(string name)
        {
            var root = BoundaryRoot(name);
            Require(!Directory.Exists(root), "Existing case evidence preserved; case cannot be repeated");
            var go = new GameObject("Isolated boundary case");
            go.transform.SetParent(transform);
            var child = go.AddComponent<RevivalJournalHarness>();
            try
            {
                child.Initialize(root, ApplicationId);
                child.PreparePending();
                child.VerifyPendingRestart();
                string exactPath = Path.GetFullPath(child.SavePath);
                string checkpoint = name.EndsWith("before", StringComparison.Ordinal) ? "temporary-closed" : "replaced";
                // Fixture preparation is complete before the exact-path one-shot hook is armed.
                DataManager.JournalWriteCheckpoint = (path, temporary, stage) =>
                {
                    if (Path.GetFullPath(path) != exactPath || stage != checkpoint) return;
                    DataManager.JournalWriteCheckpoint = null;
                    string processId = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
                    File.WriteAllText(Path.Combine(root, "checkpoint.json"), Utility.SerializeToJson(new Dictionary<string, string>
                    {
                        { "case", name }, { "checkpoint", stage }, { "file", exactPath },
                        { "temporary", Path.GetFullPath(temporary) }, { "pid", processId }, { "termination", "awaiting-external-force-stop" }
                    }));
                    System.Threading.Thread.Sleep(4000);
                    File.WriteAllText(Path.Combine(root, "timeout.txt"), "External termination did not arrive within four seconds.");
                    // Reaching this line is never evidence of a real process interruption.
                    throw new InvalidOperationException("Checkpoint did not terminate the Android process");
                };
                if (name.StartsWith("mutation", StringComparison.Ordinal)) child._data.TryUpdateLocalData("Gold", "30");
                else child.Acknowledge();
                throw new InvalidOperationException("Boundary checkpoint was not reached");
            }
            finally
            {
                DataManager.JournalWriteCheckpoint = null;
                Destroy(go);
            }
        }

        public void VerifyBoundaryCases()
        {
            foreach (var name in BoundaryCases)
            {
                var root = BoundaryRoot(name);
                var markerPath = Path.Combine(root, "checkpoint.json");
                Require(File.Exists(markerPath), "Missing checkpoint for " + name);
                var marker = Utility.DeserializeFromJson(File.ReadAllText(markerPath)) as Dictionary<string, object>;
                string expectedStage = name.EndsWith("before", StringComparison.Ordinal) ? "temporary-closed" : "replaced";
                Require(marker != null && marker.TryGetValue("case", out var recordedCase) && (string)recordedCase == name
                    && marker.TryGetValue("checkpoint", out var recordedStage) && (string)recordedStage == expectedStage,
                    "Invalid checkpoint marker for " + name);
                string path = CreateSavePath(root, ApplicationId);
                var original = File.ReadAllBytes(path);
                var go = new GameObject("Boundary restart verification");
                var child = go.AddComponent<RevivalJournalHarness>();
                try
                {
                    child.Initialize(root, ApplicationId); // Real DataManager.LoadStoredData, no hydration or repair writes.
                    string expectedGold = name == "mutation-after" ? "30" : "20";
                    int expectedPending = name == "ack-after" ? 0 : 1;
                    Require(child.Gold == expectedGold && child.PendingCount == expectedPending, "Inconsistent recovered state for " + name);
                    if (expectedPending == 1)
                        Require(child._data.JournalHarnessPending()["Gold"] == expectedGold, "Pending value mismatch");
                    var saved = Utility.DeserializeFromJson(File.ReadAllText(path)) as Dictionary<string, object>;
                    var journal = Utility.DeserializeFromJson((string)saved["__TamerPendingJournal"]) as Dictionary<string, object>;
                    Require((string)journal["revision"] == (name == "mutation-after" ? "2" : "1"), "Revision mismatch");
                    Require(Convert.ToBase64String(original) == Convert.ToBase64String(File.ReadAllBytes(path)), "Loading changed evidence");
                }
                finally { Destroy(go); }
            }
            Status = "PASS four checkpoint files reloaded consistently; verify host PID evidence separately";
        }
    }
}
#endif
