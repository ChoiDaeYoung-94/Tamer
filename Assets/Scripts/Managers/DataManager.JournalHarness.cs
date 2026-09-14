#if UNITY_EDITOR || TAMER_JOURNAL_HARNESS
using System;
using System.Collections.Generic;
using System.IO;

namespace AD
{
    // Absent from normal players. The production initialization and save path remain unchanged.
    public partial class DataManager
    {
        internal void InitializeJournalHarness(string path, ServerManager server)
        {
            if (_initialized || _shutdown) throw new InvalidOperationException("Fresh harness manager required.");
            if (Path.GetFileName(path) != "JournalPlayerData.json"
                || Path.GetFileName(Path.GetDirectoryName(path)) != "JournalHarnessV1"
                || !Path.IsPathRooted(path)) throw new InvalidOperationException("Dedicated journal path required.");
            _playerDataPath = path;
            _defaults = new Dictionary<string, string> { { "Gold", "10" }, { "GooglePlay", "" } };
            MonsterData = new Dictionary<string, object>();
            _server = server ?? throw new ArgumentNullException(nameof(server));
            LoadStoredData();
            _initialized = true;
        }

        internal Dictionary<string, string> JournalHarnessPending() => _changes.Snapshot();
    }
}
#endif
