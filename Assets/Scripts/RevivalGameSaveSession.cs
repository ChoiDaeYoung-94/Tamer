#if UNITY_EDITOR || TAMER_GAMESAVE_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlayFab.ClientModels;
using UnityEngine;

namespace AD
{
    public sealed class RevivalGameSaveSession : IDisposable
    {
        private readonly GameObject _root;
        private readonly DataManager _data;
        private readonly ServerManager _server;
        private readonly Func<bool> _current;
        private readonly bool _readOnly;
        private bool _disposed;
        private bool _readReady;
        private int _cloudStep = -1;
        public string SavePath { get; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public int CloudStep => _current() && !_disposed ? _cloudStep : -1;
        public bool IsBusy => _server.IsInProgress;
        public bool HasFailed => _server.HasFailed;
        public int PendingCount => _data.GameSavePending().Count;
        public Dictionary<string, string> Values => new Dictionary<string, string>(_data.LocalPlayerData);
        public bool CanMutate => !_disposed && _current() && !_readOnly && _readReady && !IsBusy && !HasFailed;

        public RevivalGameSaveSession(string path, string account, Func<bool> current, bool readOnly,
            Action<string, Action<Dictionary<string, string>>, Action<int>> read,
            Action<string, Dictionary<string, string>, Action, Action<int>> write)
        {
            if (string.IsNullOrWhiteSpace(account)) throw new ArgumentException("Bound account required.");
            _current = current ?? throw new ArgumentNullException(nameof(current));
            _readOnly = readOnly;
            SavePath = path;
            if (readOnly && (File.Exists(path) || Directory.Exists(Path.GetDirectoryName(path))))
                throw new InvalidOperationException("Fresh restore slot required; existing evidence preserved.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _root = new GameObject("Isolated game-save data");
            _data = _root.AddComponent<DataManager>();
            _server = new ServerManager(() => !_disposed && _current() ? account : null,
                () => !_disposed && _current() && !_readOnly && _readReady && _data.IsServerDataReady,
                (owner, ok, fail) => { Reads++; read(owner, ok, fail); },
                (owner, patch, ok, fail) =>
                {
                    RevivalGameSaveSchema.ValidatePatch(patch);
                    Writes++; write(owner, patch, ok, fail);
                },
                (snapshot, update) =>
                {
                    int step = RevivalGameSaveSchema.SnapshotStep(snapshot);
                    _data.PlayFabPlayerData = snapshot.ToDictionary(e => e.Key, e => new UserDataRecord { Value = e.Value });
                    _data.UpdateData();
                    if (_data.LocalPlayerData["GooglePlay"] != "" || _data.LocalPlayerData["GoogleAdMob"] != "null")
                        throw new InvalidDataException("Excluded fields must remain synthetic defaults.");
                    _cloudStep = step;
                    _readReady = true;
                });
            try
            {
                _data.InitializeGameSaveHarness(path, _server);
                // Reject polluted local saves before any network request or overwrite.
                var values = _data.LocalPlayerData;
                if (values.Count != 10 || values["GooglePlay"] != "" || values["GoogleAdMob"] != "null")
                    throw new InvalidDataException("Unexpected local schema; preserve without overwrite.");
                var pending = _data.GameSavePending();
                if (pending.Count != 0) RevivalGameSaveSchema.ValidatePatch(pending);
                _data.BeginAccountSession(account);
            }
            catch { Dispose(); throw; }
        }

        public void Read()
        {
            if (_disposed || !_current() || IsBusy) return;
            _readReady = false; _cloudStep = -1;
            _server.GetAllData(update: true);
        }
        public void Prepare(int step)
        {
            if (!CanMutate || PendingCount != 0 || CloudStep != step - 1)
                throw new InvalidOperationException("Read the preceding fixture and settle pending work first.");
            foreach (var entry in RevivalGameSaveSchema.Fixture(step))
                if (!_data.TryUpdateLocalData(entry.Key, entry.Value)) throw new IOException("Local fixture write failed.");
        }
        public void Upload()
        {
            if (!CanMutate || PendingCount == 0) throw new InvalidOperationException("Read before uploading pending fixture.");
            RevivalGameSaveSchema.ValidatePatch(_data.GameSavePending());
            _cloudStep = -1;
            _data.UpdatePlayerData();
        }
        public void Verify(int step, bool pending)
        {
            if (_disposed || !_current() || !_readReady || IsBusy || HasFailed)
                throw new InvalidOperationException("Completed successful read required.");
            foreach (var entry in RevivalGameSaveSchema.Fixture(step))
                if (!_data.LocalPlayerData.TryGetValue(entry.Key, out var value) || value != entry.Value)
                    throw new InvalidDataException("Fixture did not restore completely.");
            if (pending ? PendingCount == 0 : PendingCount != 0 || CloudStep != step)
                throw new InvalidDataException("Journal or cloud state differs.");
            if (_readOnly && Writes != 0) throw new InvalidOperationException("Restore must be read-only.");
        }
        public static RevivalGameSaveSession Offline(string root, string package, string slot)
        {
            string path = RevivalGameSaveSchema.SavePath(root, package, "offline", slot);
            string cloud = Path.Combine(root, "GameSaveHarnessV1", "offline", "SyntheticServer.json");
            Func<Dictionary<string, string>> snapshot = () =>
            {
                if (!File.Exists(cloud)) return new Dictionary<string, string>();
                var parsed = Utility.DeserializeFromJson(File.ReadAllText(cloud)) as Dictionary<string, object>;
                if (parsed == null) throw new InvalidDataException("Invalid synthetic server; preserved.");
                var data = parsed.ToDictionary(e => e.Key, e => e.Value as string);
                RevivalGameSaveSchema.SnapshotStep(data);
                return data;
            };
            return new RevivalGameSaveSession(path, "synthetic-gamesave-owner-v1", () => true, slot != "primary",
                (account, ok, fail) => ok(snapshot()),
                (account, patch, ok, fail) =>
                {
                    var data = snapshot();
                    foreach (var entry in patch) data[entry.Key] = entry.Value;
                    RevivalGameSaveSchema.SnapshotStep(data);
                    File.WriteAllText(cloud, Utility.SerializeToJson(data));
                    ok();
                });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _data?.Shutdown(); _server?.Dispose();
            if (_root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }
        }
    }
}
#endif
