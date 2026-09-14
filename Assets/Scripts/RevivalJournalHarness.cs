#if UNITY_EDITOR || TAMER_JOURNAL_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using PlayFab.ClientModels;
using UnityEngine;

namespace AD
{
    /// <summary>Real DataManager file writes, synthetic request delegates, no SDK or Managers initialization.</summary>
    public sealed class RevivalJournalHarness : MonoBehaviour
    {
        public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.journal";
        private const string Owner = "synthetic-journal-owner-v1";
        private DataManager _data;
        private ServerManager _server;
        private string _path;
        private string _cloudGold = "10";
        private bool _restoredPending;
        public string Status { get; private set; } = "Not initialized";
        public string Gold => _data?.LocalPlayerData?["Gold"] ?? "unavailable";
        public int PendingCount => _data?.JournalHarnessPending().Count ?? -1;
        public string SavePath => _path;
        public int SyntheticWrites { get; private set; }

        public static string CreateSavePath(string root, string package)
        {
            if (package != ApplicationId || !Path.IsPathRooted(root))
                throw new InvalidOperationException("Dedicated package and absolute root required.");
            return Path.Combine(Path.GetFullPath(root), "JournalHarnessV1", "JournalPlayerData.json");
        }

        public void Initialize(string root, string package)
        {
            if (_data != null) throw new InvalidOperationException("Already initialized.");
            _path = CreateSavePath(root, package);
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            _data = gameObject.AddComponent<DataManager>();
            _server = new ServerManager(() => _data.PlayFabId, () => _data.IsServerDataReady,
                (account, success, failure) =>
                {
                    Require(account == Owner, "Unexpected synthetic account");
                    success(new Dictionary<string, string> { { "Gold", _cloudGold } });
                },
                (account, patch, success, failure) =>
                {
                    Require(account == Owner && patch.Count == 1 && patch.TryGetValue("Gold", out var value)
                        && value == "20", "Unexpected synthetic patch");
                    SyntheticWrites++;
                    _cloudGold = "20";
                    success();
                },
                (delay, callback) => { }, // Synchronous fake server; no timer or network.
                (snapshot, update) => Apply(snapshot));
            _data.InitializeJournalHarness(_path, _server);
            Status = File.Exists(_path) ? "Loaded disk only; choose the matching restart check" : "Fresh isolated save; prepare pending 20";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Apply(Dictionary<string, string> snapshot)
        {
            _data.PlayFabPlayerData = new Dictionary<string, UserDataRecord>();
            foreach (var entry in snapshot) _data.PlayFabPlayerData.Add(entry.Key, new UserDataRecord { Value = entry.Value });
            _data.UpdateData();
        }

        public void PreparePending()
        {
            Require(!File.Exists(_path), "Existing evidence preserved; prepare is only for a fresh installation");
            _data.BeginAccountSession(Owner);
            Apply(new Dictionary<string, string> { { "Gold", "10" } });
            Require(_data.TryUpdateLocalData("Gold", "20"), "Local mutation failed");
            Require(Gold == "20" && PendingCount == 1 && SyntheticWrites == 0, "Pending preparation failed");
            Status = "PASS prepared Gold=20 pending=1; force-stop then restart";
        }

        public void VerifyPendingRestart()
        {
            Require(File.Exists(_path) && Gold == "20" && PendingCount == 1, "Expected disk Gold=20 pending=1");
            _data.BeginAccountSession(Owner);
            Apply(new Dictionary<string, string> { { "Gold", "10" } });
            Require(Gold == "20" && PendingCount == 1 && SyntheticWrites == 0, "Old snapshot replaced pending data");
            _restoredPending = true;
            Status = "PASS restart + old server 10 => Gold=20 pending=1";
        }

        public void Acknowledge()
        {
            Require(_restoredPending && PendingCount == 1, "Verify pending restart first");
            _data.UpdatePlayerData(); // Real request queue invokes the real durable acknowledgement.
            Require(Gold == "20" && PendingCount == 0 && SyntheticWrites == 1 && !_server.HasFailed, "Acknowledgement failed");
            Status = "PASS synthetic ack Gold=20 pending=0; force-stop then restart";
        }

        public void VerifyAcknowledgedRestart()
        {
            Require(File.Exists(_path) && Gold == "20" && PendingCount == 0, "Expected acknowledged disk Gold=20 pending=0");
            _cloudGold = "20"; // Explicit synthetic acknowledged server snapshot, not a persisted server emulator.
            _data.BeginAccountSession(Owner);
            _data.UpdatePlayerData();
            Require(Gold == "20" && PendingCount == 0 && SyntheticWrites == 0 && !_server.HasFailed, "Acknowledged restart failed");
            Status = "PASS acknowledged restart Gold=20 pending=0 writes=0";
        }

        private void OnDestroy() { _data?.Shutdown(); _server?.Dispose(); }

#if TAMER_JOURNAL_HARNESS
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (!Debug.isDebugBuild || Application.identifier != ApplicationId)
                throw new InvalidOperationException("Dedicated development journal player required.");
            var root = new GameObject("Offline DataManager journal probe");
            DontDestroyOnLoad(root);
            var harness = root.AddComponent<RevivalJournalHarness>();
            try { harness.Initialize(Application.persistentDataPath, Application.identifier); }
            catch (Exception) { harness.Status = "FAIL initialization; original file preserved"; }
        }
#endif
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception error) { Status = "FAIL " + error.Message; }
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 900f, Screen.width / 900f, 1));
            GUILayout.BeginArea(new Rect(15, 25, 870, 1000), GUI.skin.box);
            GUILayout.Label("OFFLINE JOURNAL — real DataManager / synthetic server");
            GUILayout.Label(Status);
            GUILayout.Label("Gold=" + Gold + " pending=" + PendingCount + " synthetic writes=" + SyntheticWrites);
            GUILayout.Label("Only JournalHarnessV1/JournalPlayerData.json; no accounts or network");
            GUI.enabled = _data != null;
            if (GUILayout.Button("1 Prepare fresh pending 20", GUILayout.Height(95))) Run(PreparePending);
            if (GUILayout.Button("2 Verify pending after process restart (old server 10)", GUILayout.Height(95))) Run(VerifyPendingRestart);
            if (GUILayout.Button("3 Acknowledge synthetic upload", GUILayout.Height(95))) Run(Acknowledge);
            if (GUILayout.Button("4 Verify acknowledged process restart", GUILayout.Height(95))) Run(VerifyAcknowledgedRestart);
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
#endif
