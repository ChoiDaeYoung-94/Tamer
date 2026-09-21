#if UNITY_EDITOR || TAMER_GAMESAVE_HARNESS || TAMER_PLAYER_RESTORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PlayFab.ClientModels;

namespace AD
{
    // Existing cloud fields only. PlayerPrefs collection/inventory/equipment are not cloud fields.
    public static class RevivalGameSaveSchema
    {
        public const string ApplicationId = "com.AeDeong.MonsterTamer.revival.gamesave";
        public const string CloudApplicationId = "com.AeDeong.MonsterTamer.revival.gamesavecloud";
        public const string TestTitle = "12B656";
        private static readonly string[] Fields = { "NickName", "Sex", "Tutorial", "Gold", "Power", "AttackSpeed", "MoveSpeed", "AllyMonsters" };
        public static List<string> ReadKeys() => new List<string>(Fields);
        public static Dictionary<string, string> Fixture(int step)
        {
            if (step != 1 && step != 2) throw new ArgumentOutOfRangeException(nameof(step));
            return new Dictionary<string, string>
            {
                ["NickName"] = step == 1 ? "GameplayFixtureA" : "GameplayFixtureB",
                ["Sex"] = step == 1 ? "Man" : "Woman", ["Tutorial"] = "done",
                ["Gold"] = step == 1 ? "120" : "235", ["Power"] = step == 1 ? "11" : "12",
                ["AttackSpeed"] = step == 1 ? "0.6" : "0.7", ["MoveSpeed"] = step == 1 ? "3.2" : "3.4",
                ["AllyMonsters"] = step == 1 ? "Bat" : "Bat,Magma"
            };
        }
        public static int SnapshotStep(Dictionary<string, string> snapshot)
        {
            if (snapshot == null) throw new InvalidDataException("Missing snapshot.");
            if (snapshot.Count == 0) return 0;
            foreach (int step in new[] { 1, 2 })
            {
                var fixture = Fixture(step);
                if (snapshot.Count == fixture.Count && fixture.All(e => snapshot.TryGetValue(e.Key, out var value) && value == e.Value)) return step;
            }
            throw new InvalidDataException("Unknown or incomplete test data; preserve without overwrite.");
        }
        public static void ValidatePatch(Dictionary<string, string> patch)
        {
            if (patch == null || patch.Count == 0) throw new InvalidDataException("Empty patch.");
            var first = Fixture(1); var second = Fixture(2);
            foreach (var pair in patch)
                if (!first.ContainsKey(pair.Key) || pair.Value == null ||
                    (first[pair.Key] != pair.Value && second[pair.Key] != pair.Value))
                    throw new InvalidDataException("Only synthetic gameplay fields may be written.");
        }
        public static void ValidatePackage(string package)
        {
            if (package != ApplicationId) throw new InvalidOperationException("Dedicated game-save package required.");
        }
        public static LoginWithCustomIDRequest CreateLoginRequest(string customId)
        {
            if (customId == null || !Regex.IsMatch(customId, "\\Agameplay-save-[a-f0-9]{32}\\z"))
                throw new ArgumentException("New separately provisioned gameplay identity required.");
            return new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = false };
        }
        public static string SavePath(string root, string package, string mode, string slot)
        {
            if (mode == "cloud") ValidateCloudTarget(package, TestTitle);
            else ValidatePackage(package);
            if (!Path.IsPathRooted(root) || (mode != "offline" && mode != "cloud") ||
                (slot != "primary" && slot != "restore1" && slot != "restore2"))
                throw new InvalidOperationException("Isolated root, mode and slot required.");
            return Path.Combine(Path.GetFullPath(root), "GameSaveHarnessV1", mode, slot, "GameSavePlayerData.json");
        }
        public static void ValidateCloudTarget(string package, string title)
        {
            if (package != CloudApplicationId || title != TestTitle)
                throw new InvalidOperationException("Exact cloud test package and title required.");
        }
    }
}
#endif
