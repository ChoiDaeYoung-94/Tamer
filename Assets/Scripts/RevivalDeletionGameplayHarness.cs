#if UNITY_EDITOR || TAMER_DELETION_GAMEPLAY
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AD;
using AD.Privacy;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>One accepted deletion followed by one cold-start explicit sign-in, in a dedicated offline app.</summary>
public sealed class RevivalDeletionGameplayHarness : MonoBehaviour
{
    public const string AccountA = "deletion-gameplay-fixture-A";
    public const string AccountB = "deletion-gameplay-fixture-B";
    public const string AccountC = "deletion-gameplay-fixture-C";
    private static readonly string[] LegacyKeys = { "AllyMonsters", "playerEquippedItems", "LocalItem" };
    private static readonly string[] LegacyValues = { "Bat,Magma", "SimpleSword", "SimpleSword,SimpleShield" };
    private static string _directory;
    private static State _state;
    private static bool _ready, _firstProcess, _paused, _retry, _authenticated;
    private static Func<Action> _captureInitialLogin;
    private static Action _oldLogin, _oldWrite;
    private static bool _holdWrite, _oldWriteApplied, _staleLoginRejected, _cleanupVerified;
    private static int _reads, _writes, _submissions, _errors;
    private static int _firstProcessReads;
    private static RevivalDeletionGameplayHarness _instance;
    private bool _mainObserved, _acceptedObserved;
    private string _lastShopUi;
    private DeletionView _observedView;
    private static string _driverDirectory;

    [Serializable]
    private sealed class State
    {
        public int Version = 1;
        public string Phase;
        public int InitialPid;
        public string ForeignInventoryHash;
        public string LegacyHash;
        public string CloudAHash;
        public string CloudCHash;
    }

    private static string Progress => Path.Combine(_directory, "PlayerData.json");
    private static string Marker => Path.Combine(_directory, "Phase.json");
    private static string Cloud(string account) => Path.Combine(_directory,
        account == AccountA ? "CloudA.json" : account == AccountC ? "CloudC.json" : throw new InvalidOperationException());
    private static string Inventory(string account) => Path.Combine(Progress + ".inventory", PlayerInventoryStore.OwnerKey(account) + ".json");
    private static string OwnedBackup => Path.Combine(_directory, "PlayerDataBackups", "PlayerData-11111111111111111111111111111111.json");
    private static string OwnedArchive => Progress + ".deletion-entitlement-22222222222222222222222222222222";
    private static int Pid
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var process = new AndroidJavaClass("android.os.Process")) return process.CallStatic<int>("myPid");
#else
            return System.Diagnostics.Process.GetCurrentProcess().Id;
#endif
        }
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Deletion gameplay fixture mismatch; preserve evidence.");
    }

    public static void Boot()
    {
        Require(!Application.isEditor && Debug.isDebugBuild &&
            Application.identifier == RevivalGameplayIsolation.DeletionGameplayApplicationId &&
            string.IsNullOrEmpty(PlayFabSettings.staticPlayer.ClientSessionTicket));
        PlayFabSettings.TitleId = "OFFLINE";
        _directory = Path.Combine(Application.persistentDataPath, "RevivalDeletionGameplayV1");
        _firstProcess = !Directory.Exists(_directory);
        if (_firstProcess)
        {
            Require(!File.Exists(Path.Combine(Application.persistentDataPath, "PlayerData.json")) &&
                !LegacyKeys.Any(PlayerPrefs.HasKey) && !PlayerPrefs.HasKey(DataManager.DeletionLoginPauseKey));
            Directory.CreateDirectory(_directory);
            _state = new State { Phase = "prepared", InitialPid = Pid };
            for (int index = 0; index < LegacyKeys.Length; index++) PlayerPrefs.SetString(LegacyKeys[index], LegacyValues[index]);
            PlayerPrefs.Save();
            WriteNew(Cloud(AccountA), Snapshot(AccountA));
            WriteNew(Cloud(AccountC), Snapshot(AccountC));
            _state.LegacyHash = LegacyHash();
            _state.CloudAHash = HashFile(Cloud(AccountA));
            _state.CloudCHash = HashFile(Cloud(AccountC));
            WriteNew(Marker, _state); // A partial/aborted phase1 is never silently seeded again.
        }
        else
        {
            Require((File.GetAttributes(_directory) & FileAttributes.ReparsePoint) == 0 && File.Exists(Marker));
            _state = JsonConvert.DeserializeObject<State>(File.ReadAllText(Marker));
            Require(_state != null && _state.Version == 1 && _state.Phase == "accepted" &&
                _state.InitialPid != Pid && PlayerPrefs.GetInt(DataManager.DeletionLoginPauseKey, 0) == 1 &&
                !File.Exists(Progress) && !File.Exists(Inventory(AccountA)) && !File.Exists(Inventory(AccountC)) &&
                !File.Exists(OwnedBackup) && !File.Exists(OwnedArchive));
            RequirePreserved();
            RequireAcceptedDisk();
            Require(HashFile(Cloud(AccountA)) == _state.CloudAHash && HashFile(Cloud(AccountC)) == _state.CloudCHash);
            _state.Phase = "cold-started";
            Replace(Marker, _state); // Consume this second process before waiting for explicit Retry.
        }
        _ready = true;
        _driverDirectory = Path.Combine(Application.persistentDataPath, "DeletionDriverV1");
        Directory.CreateDirectory(_driverDirectory);
        var root = new GameObject("Offline deletion lifecycle verification");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<RevivalDeletionGameplayHarness>();
        // Install only local delegates. CloudScriptDeletionClient does not install its SDK factory in gameplay builds.
        DeletionPresenter.RuntimeFlowFactory = CreateFlow;
        DeletionRecoveryGuard.HasPendingSubmission = account => false;
        Mark("BOOT phase=" + (_firstProcess ? "initial-A" : "cold-pause") + " pid=" + Pid + " network=disabled");
    }

    public static string RequireSaveDirectory(string root)
    {
        Require(_ready && _directory == Path.Combine(root, "RevivalDeletionGameplayV1"));
        return _directory;
    }

    public static PlayFabAuthenticationContext Identity(string account) =>
        new PlayFabAuthenticationContext(null, null, account, "offline-entity-" + account, "title_player_account");

    public static bool HoldInitialLogin(Func<Action> capture)
    {
        Require(_ready);
        if (!_firstProcess) return false;
        Require(_captureInitialLogin == null);
        _captureInitialLogin = capture;
        Mark("INITIAL_LOGIN_HELD no-native-auth no-auto-submit");
        return true;
    }

    public static void ObserveOriginalPause(bool retryVisible)
    {
        Require(_ready && !_firstProcess && retryVisible && !_authenticated && _reads == 0 && _writes == 0 &&
            string.IsNullOrEmpty(Managers.DataM.PlayFabId) && !Managers.DataM.IsServerDataReady);
        _paused = true;
        Mark("ORIGINAL_LOGIN_PAUSE retryVisible=true automaticAuth=0 reads=0 writes=0 pid=" + Pid);
    }

    public static void ObserveExplicitRetry()
    {
        Require(_ready && !_firstProcess && _paused && !_retry && !_authenticated);
        _retry = true;
        Mark("ORIGINAL_RETRY_CLICKED");
    }

    public static string AuthenticateExplicitly()
    {
        Require(_ready && !_firstProcess && _paused && _retry && !_authenticated && _reads == 0 &&
            string.IsNullOrEmpty(Managers.DataM.PlayFabId) && !File.Exists(Progress) && !File.Exists(Inventory(AccountC)));
        RequirePreserved();
        Require(HashFile(Cloud(AccountC)) == _state.CloudCHash);
        _authenticated = true;
        Mark("SYNTHETIC_C_AUTH explicit=true nativeCalls=0 sdkCalls=0");
        return AccountC;
    }

    private static Dictionary<string, string> Snapshot(string account)
    {
        var data = RevivalGameplayIsolation.Seed();
        data["NickName"] = account == AccountA ? "DeleteFixtureA" : "FreshFixtureC";
        data["Gold"] = account == AccountA ? "1000" : "41";
        return data;
    }

    public static ServerManager CreateServer(DataManager owner)
    {
        Require(_ready && owner != null);
        return new ServerManager(() => owner.PlayFabId, () => owner.IsServerDataReady,
            (account, success, failure) =>
            {
                RequireAccount(account); _reads++;
                success(JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Cloud(account))));
            },
            (account, patch, success, failure) =>
            {
                RequireAccount(account); _writes++;
                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Cloud(account)));
                foreach (var entry in patch) if (entry.Value == null) data.Remove(entry.Key); else data[entry.Key] = entry.Value;
                Replace(Cloud(account), data);
                if (_holdWrite) { Require(_oldWrite == null); _oldWrite = success; }
                else success();
            }, (delay, callback) => { },
            (snapshot, update) =>
            {
                owner.PlayFabPlayerData = snapshot.ToDictionary(pair => pair.Key, pair => new UserDataRecord { Value = pair.Value });
                if (update) owner.UpdateData();
            });
    }

    private static void RequireAccount(string account) => Require(_ready &&
        ((_firstProcess && account == AccountA && !_cleanupVerified) ||
         (!_firstProcess && account == AccountC && _authenticated)));

    private static DeletionFlow CreateFlow()
    {
        var owner = Managers.DataM;
        Require(_ready && _firstProcess && _instance._mainObserved && owner != null && owner.PlayFabId == AccountA);
        var session = owner.DeletionSession();
        Require(session != null && session.HasEntityBinding);
        var gateway = new CloudScriptDeletionGateway((captured, id, token) =>
        {
            Require(captured.Matches(owner.DeletionSession()) && owner.DeletionInProgress && ++_submissions == 1);
            Mark("FIXTURE_ACCEPTED_RESPONSE submissions=1 externalCalls=0");
            return Task.FromResult(true);
        }, synthetic: false, prepare: (captured, token) =>
        {
            Require(captured.Matches(owner.DeletionSession()) && _submissions == 0);
            Mark("PRODUCT_REQUEST_PREVIEW no-submission");
            return Task.FromResult(true);
        });
        return new DeletionFlow(gateway, owner.DeletionSession, captured =>
        {
            Require(_oldLogin != null && _oldWrite == null && _submissions == 0 && owner.IsServerDataReady);
            _holdWrite = true;
            // The real ServerManager owns this late write completion and queued read.
            Managers.ServerM.SetData(new Dictionary<string, string> { ["Gold"] = "999" },
                getAllData: true, update: true, onWritten: () => _oldWriteApplied = true);
            Require(_oldWrite != null && Managers.ServerM.IsInProgress);
            owner.BeginDeletionSubmission(captured);
        }, captured =>
        {
            try
            {
            int generation = owner.AccountGeneration;
            owner.FinishAcceptedDeletion(captured);
            _oldWrite(); _oldLogin();
            Require(!_oldWriteApplied && _staleLoginRejected && owner.AccountGeneration > generation &&
                !captured.Matches(owner.DeletionSession()) && !owner.IsServerDataReady &&
                !owner.DeletionInProgress && string.IsNullOrEmpty(owner.PlayFabId) && owner.PlayFabPlayerData == null &&
                string.IsNullOrEmpty(PlayFabSettings.staticPlayer.PlayFabId) &&
                string.IsNullOrEmpty(PlayFabSettings.staticPlayer.ClientSessionTicket) &&
                PlayerPrefs.GetInt(DataManager.DeletionLoginPauseKey, 0) == 1 &&
                !owner.TryUpdateLocalData("Gold", "123") && !File.Exists(Progress) &&
                !File.Exists(Inventory(AccountA)) && !File.Exists(OwnedBackup) && !File.Exists(OwnedArchive) &&
                _reads == _firstProcessReads && !Managers.ServerM.IsInProgress &&
                Player.Instance != null && !Player.Instance.IsCurrentCombatSession());
            RequirePreserved();
            _cleanupVerified = true;
            Mark("CLEANUP_VERIFIED ownedP=false ownedI=false ownedBackup=false ownedArchive=false foreignB=preserved legacy=preserved oldWrite=blocked queuedRead=undispatched oldLogin=stale-rejected playerSession=false");
            }
            catch (Exception error) { Fail("accepted-cleanup", error); throw; }
        }, owner.FinishCancelledDeletion,
            new FileDeletionRecoveryStore(Path.Combine(_directory, "DeletionJournal.json")),
            DeletionRecovery.Hash("offline-deletion-gameplay-v1", session.TitleId, session.AccountId, session.EntityId));
    }

    private void Awake() => Application.logMessageReceived += OnLog;
    private void OnDestroy() => Application.logMessageReceived -= OnLog;
    private void OnLog(string message, string trace, LogType type)
    {
        if (message.Contains("Session: StaleRejected")) _staleLoginRejected = true;
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
            !RevivalGameplayIsolation.IsOfflinePlayerConnectionDiagnostic(Application.identifier, Debug.isDebugBuild,
                Application.isEditor, message, type))
        {
            _errors++;
            _ready = false;
            Mark("ERROR_LATCHED type=" + type + " evidence=preserved");
        }
    }

    private IEnumerator Start()
    {
        yield return null;
        try
        {
            Require(_ready && Managers.Instance != null && Managers.PopupM != null && Managers.SoundM != null &&
                Managers.GoogleAdMobM != null && !Managers.GoogleAdMobM.CanRequestAds);
            if (!_firstProcess) { Require(_paused && _reads == 0 && _writes == 0); yield break; }
            Require(_captureInitialLogin != null);
            Managers.DataM.BeginAccountSession(AccountA);
            PlayFabSettings.staticPlayer.CopyFrom(Identity(AccountA));
            Managers.ServerM.GetAllData(update: true);
            Require(Managers.DataM.IsServerDataReady && File.Exists(Progress) && !File.Exists(Inventory(AccountB)));
            Managers.DataM.WriteInventory(AccountA, Managers.DataM.AccountGeneration, new[] { "Bat" },
                new[] { "SimpleSword" }, new Dictionary<string, string> { ["Sword"] = "SimpleSword", ["Shield"] = null });
            var foreign = new PlayerInventoryStore(Progress + ".inventory", new[] { "Bat" },
                new Dictionary<string, string> { ["SimpleSword"] = "Sword" });
            foreign.BindSession(AccountB, "foreign-fixture-session");
            foreign.Save(AccountB, new[] { "Bat" }, new[] { "SimpleSword" },
                new Dictionary<string, string> { ["Sword"] = "SimpleSword", ["Shield"] = null });
            Directory.CreateDirectory(Path.GetDirectoryName(OwnedBackup));
            using (var output = new FileStream(OwnedBackup, FileMode.CreateNew))
            { var bytes = File.ReadAllBytes(Progress); output.Write(bytes, 0, bytes.Length); output.Flush(true); }
            WriteNew(OwnedArchive, new Dictionary<string, string> { ["__TamerAccountOwner"] = AccountA, ["GooglePlay"] = "" });
            _state.ForeignInventoryHash = HashFile(Inventory(AccountB));
            Replace(Marker, _state);
            _oldLogin = _captureInitialLogin();
            _firstProcessReads = _reads;
            Managers.SceneM.NextScene(GameConstants.Scene.Main);
        }
        catch (Exception error) { Fail("prepare", error); }
    }

    private void Update()
    {
        if (!_ready) return;
        try
        {
            DriveFixtureButton();
            var view = FindFirstObjectByType<DeletionView>();
            if (view != null && view != _observedView)
            {
                _observedView = view;
                view.RequestButton.onClick.AddListener(() => Mark("PRODUCT_REQUEST_CLICKED"));
                view.ConfirmButton.onClick.AddListener(() => Mark("PRODUCT_CONFIRM_CLICKED"));
                view.CloseButton.onClick.AddListener(() => Mark(view.ExitOnClose ? "PRODUCT_EXIT_CLICKED pid=" + Pid : "PRODUCT_PRIVACY_BACK_CLICKED"));
                Mark("PRODUCT_PRIVACY_VIEW originalButtons=true");
            }
            if (_cleanupVerified && !_acceptedObserved && view != null && view.ExitOnClose)
            {
                var blockers = typeof(PopupManager).GetField("_flowOwners", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).GetValue(Managers.PopupM) as HashSet<GameObject>;
                Require(_errors == 0 && blockers != null && blockers.Contains(view.gameObject) && !view.ConfirmButton.gameObject.activeSelf &&
                    !view.RequestButton.gameObject.activeSelf && view.CloseButton.interactable &&
                    view.Message.text.Contains("Your deletion request was accepted."));
                _state.Phase = "accepted";
                _state.CloudAHash = HashFile(Cloud(AccountA));
                RequireAcceptedDisk();
                Replace(Marker, _state);
                _acceptedObserved = true;
                Mark("PHASE1_ACCEPTED_UI_PASS originalExit=true cleanup=verified errors=0; use Exit game, then start a new process");
            }
            if (!_firstProcess && _mainObserved) ObserveVisibleShop();
            if (_mainObserved || UnitySceneManager.GetActiveScene().name != "Main" ||
                Managers.SceneM.IsTransitioning || Player.Instance == null || ShopMan.Instance == null || PlayerUICanvas.Instance == null) return;
            Require(_errors == 0 && !Managers.GoogleAdMobM.CanRequestAds && Managers.IAPM.Status == IAPStatus.Unavailable);
            RequirePreserved();
            var inventory = Managers.DataM.ReadInventory();
            if (_firstProcess)
            {
                Require(inventory.Owner == AccountA && inventory.Collection.Count == 1 && inventory.OwnedItems.Count == 1 &&
                    Player.Instance.PlayerEquippedItems.SequenceEqual(new[] { "SimpleSword" }) && ShopMan.Instance.CurrentItemsList.SequenceEqual(new[] { "SimpleSword" }));
                Mark("PHASE1_MAIN_READY owner=A playerEquipped=1 shopOwned=1; open Settings / Account & privacy manually");
            }
            else
            {
                const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic;
                var gold = typeof(PlayerUICanvas).GetField("_goldText", fields).GetValue(PlayerUICanvas.Instance) as TMPro.TMP_Text;
                var nickname = typeof(PlayerUICanvas).GetField("_playerNickNameText", fields).GetValue(PlayerUICanvas.Instance) as TMPro.TMP_Text;
                Require(_paused && _retry && _authenticated && Managers.DataM.PlayFabId == AccountC &&
                    Managers.DataM.IsServerDataReady && PlayerPrefs.GetInt(DataManager.DeletionLoginPauseKey, 0) == 0 &&
                    inventory.Owner == AccountC && inventory.Collection.Count == 0 && inventory.OwnedItems.Count == 0 &&
                    inventory.Equipped.Values.All(value => value == null) &&
                    Player.Instance.PlayerMonsterCollection.Count == 0 && Player.Instance.PlayerEquippedItems.Count == 0 &&
                    Player.Instance.GetCurMonsterCount() == 0 && Player.Instance.Gold == 41 &&
                    ShopMan.Instance.CurrentItemsList.Count == 0 && !Player.Instance.SimpleSword.activeSelf &&
                    !Player.Instance.MasterSword.activeSelf && !Player.Instance.Simpleshield.activeSelf && !Player.Instance.Mastershield.activeSelf &&
                    File.Exists(Inventory(AccountC)) && !File.Exists(Inventory(AccountA)) && _writes == 0 && _submissions == 0 &&
                    gold != null && gold.text == "Gold - 41" && nickname != null && nickname.text == "FreshFixtureC");
                Mark("PHASE2_C_PLAYER_SHOP_SNAPSHOT_PASS owner=C currentInventory=empty playerCollection=0 equipped=0 allies=0 shopOwned=0 models=off gold=41 HUD=matched foreignB=preserved legacy=preserved automaticAuth=0 explicitRetry=1 networkCalls=0 errors=0; open original shop manually for visible-item observation");
            }
            _mainObserved = true;
        }
        catch (Exception error) { Fail("observe", error); }
    }

    // A developer-only, package-private test driver. This invokes the original uGUI
    // listeners; it is programmatic coverage, not a claim of native touch or authentication.
    // The separate app has only local fixtures and no Internet permission.
    private void DriveFixtureButton()
    {
        string input = Path.Combine(_driverDirectory, "command.txt");
        if (!File.Exists(input)) return;
        Require(_ready && _errors == 0 && File.ReadAllBytes(input).Length <= 32);
        string command = File.ReadAllText(input).Trim();
        UnityEngine.UI.Button button;
        var view = FindFirstObjectByType<DeletionView>();
        switch (command)
        {
            case "settings":
                Require(_firstProcess && _mainObserved && !_cleanupVerified);
                button = OriginalButton("OpenSetting");
                break;
            case "privacy":
                Require(_firstProcess && _mainObserved && !_cleanupVerified && view == null);
                button = FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                    .Single(value => value.name == "AccountPrivacyEntry" && value.gameObject.activeInHierarchy);
                break;
            case "request":
                Require(_firstProcess && view != null && !_cleanupVerified && _submissions == 0);
                button = view.RequestButton;
                break;
            case "confirm":
                Require(_firstProcess && view != null && !_cleanupVerified && _submissions == 0);
                button = view.ConfirmButton;
                break;
            case "exit":
                Require(_firstProcess && _acceptedObserved && view != null && view.ExitOnClose);
                button = view.CloseButton;
                break;
            case "retry":
                Require(!_firstProcess && _paused && !_retry && !_authenticated);
                button = OriginalButton("RetryConnection");
                break;
            default: throw new InvalidOperationException("Unknown fixture-only driver command.");
        }
        Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable());
        File.Move(input, Path.Combine(_driverDirectory, command + ".used"));
        Mark("PROGRAMMATIC_UGUI command=" + command + " fixtureOnly=true touchCoverage=false");
        button.onClick.Invoke();
    }

    private static UnityEngine.UI.Button OriginalButton(string method) =>
        FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(button =>
            button.gameObject.activeInHierarchy && Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(index => button.onClick.GetPersistentMethodName(index) == method));

    private void ObserveVisibleShop()
    {
        Require(_ready && _errors == 0);
        if (ShopMan.Instance == null || Managers.DataM.PlayFabId != AccountC) return;
        var items = ShopMan.Instance.ItemList.Where(item => item != null && item.gameObject.activeInHierarchy).ToArray();
        if (items.Length == 0) return;
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var names = new List<string>();
        foreach (var item in items)
        {
            Require(!(bool)typeof(Item).GetField("_isUnlocked", fields).GetValue(item) &&
                !(bool)typeof(Item).GetField("_isEquipped", fields).GetValue(item));
            var icon = (GameObject)typeof(Item).GetField("_lockIcon", fields).GetValue(item);
            var text = (TMPro.TMP_Text)typeof(Item).GetField("_itemInfoText", fields).GetValue(item);
            Require(icon != null && icon.activeInHierarchy && text != null &&
                !string.IsNullOrWhiteSpace(text.text) && !text.text.Contains("Equipped"));
            names.Add((string)typeof(Item).GetField("_itemName", fields).GetValue(item));
        }
        string observation = string.Join(",", names.OrderBy(name => name, StringComparer.Ordinal));
        if (observation == _lastShopUi) return;
        _lastShopUi = observation;
        RequirePreserved();
        Require(_ready && _errors == 0);
        Mark("C_SHOP_VISIBLE items=" + items.Length + " allLocked=true allUnequipped=true foreignB=preserved legacy=preserved");
    }

    private static string LegacyHash() => Hash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
        LegacyKeys.Select(key => { Require(PlayerPrefs.HasKey(key)); return PlayerPrefs.GetString(key); }).ToArray())));
    private static void RequirePreserved() => Require(_state != null && LegacyHash() == _state.LegacyHash &&
        !string.IsNullOrEmpty(_state.ForeignInventoryHash) && HashFile(Inventory(AccountB)) == _state.ForeignInventoryHash);
    private static void RequireAcceptedDisk()
    {
        var expected = new[] { Marker, Cloud(AccountA), Cloud(AccountC), Inventory(AccountB) };
        Require(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)
            .SequenceEqual(expected.OrderBy(path => path, StringComparer.Ordinal), StringComparer.Ordinal));
        Require(Directory.GetDirectories(_directory, "*", SearchOption.AllDirectories).All(path =>
            path == Progress + ".inventory" || path == Path.GetDirectoryName(OwnedBackup)));
    }
    private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
    private static string Hash(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static void WriteNew(string path, object data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
    }
    private static void Replace(string path, object data)
    {
        string temporary = path + ".next-" + Guid.NewGuid().ToString("N");
        WriteNew(temporary, data);
        File.Replace(temporary, path, null); // Failure preserves the original and any temporary evidence.
    }
    private static void Fail(string stage, Exception error)
    {
        _ready = false;
        Debug.LogError("DELETION_GAMEPLAY_FAIL stage=" + stage + " type=" + error.GetType().Name + " evidence=preserved");
    }
    private static void Mark(string message) => Debug.Log("DELETION_GAMEPLAY " + message);
}
#endif
