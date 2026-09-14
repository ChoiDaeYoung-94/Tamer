#if UNITY_EDITOR || TAMER_GAMESAVE_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AD
{
    public partial class DataManager
    {
        internal void InitializeGameSaveHarness(string path, ServerManager server)
        {
            if (_initialized || _shutdown || !Path.IsPathRooted(path) || Path.GetFileName(path) != "GameSavePlayerData.json")
                throw new InvalidOperationException("Fresh isolated manager required.");
            _playerDataPath = path;
            _defaults = ParseData(Resources.Load<TextAsset>("Data/PlayerData").text);
            MonsterData = Utility.DeserializeFromJson(Resources.Load<TextAsset>("Data/MonstersData").text) as Dictionary<string, object>;
            ItemData = Utility.DeserializeFromJson(Resources.Load<TextAsset>("Data/ItemsData").text) as Dictionary<string, object>;
            _server = server ?? throw new ArgumentNullException(nameof(server));
            LoadStoredData();
            _initialized = true;
        }
        internal Dictionary<string, string> GameSavePending() => _changes.Snapshot();
    }
}
#endif
