#if TAMER_PRIVACY_PRODUCT_UI_HARNESS && (!TAMER_REVIVAL_SMOKE || !TAMER_GAMEPLAY_HARNESS || !TAMER_DELETION_GAMEPLAY)
#error Privacy product UI requires all three isolated gameplay symbols.
#endif
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
using AD.Advertising;
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

    private sealed class FixtureInvariantException : InvalidOperationException
    {
        public readonly string Invariant;
        public FixtureInvariantException(string invariant) { Invariant = invariant; }
    }

    private static void RequireInvariant(bool condition, string invariant)
    {
        if (!condition) throw new FixtureInvariantException(invariant);
    }

    public static void Boot()
    {
        Require(!Application.isEditor && Debug.isDebugBuild &&
#if TAMER_PRIVACY_PRODUCT_UI_HARNESS
            Application.identifier == RevivalGameplayIsolation.PrivacyProductUiApplicationId &&
#else
            Application.identifier == RevivalGameplayIsolation.DeletionGameplayApplicationId &&
#endif
            string.IsNullOrEmpty(PlayFabSettings.staticPlayer.ClientSessionTicket));
        PlayFabSettings.TitleId = "OFFLINE";
        _directory = Path.Combine(Application.persistentDataPath, "RevivalDeletionGameplayV1");
        _firstProcess = !Directory.Exists(_directory);
#if TAMER_PRIVACY_PRODUCT_UI_HARNESS
        // This one-process UI fixture never consumes or resets a prepared/aborted run.
        Require(_firstProcess && Application.platform == RuntimePlatform.Android && !Application.isBatchMode);
#endif
        if (_firstProcess)
        {
            Require(!File.Exists(Path.Combine(Application.persistentDataPath, "PlayerData.json")) &&
                !LegacyKeys.Any(PlayerPrefs.HasKey) && !PlayerPrefs.HasKey(DataManager.DeletionLoginPauseKey) &&
                !PlayerPrefs.HasKey(GoogleAdMobManager.AgeChoicePreferenceKey));
            Directory.CreateDirectory(_directory);
            _state = new State { Phase = "prepared", InitialPid = Pid };
            for (int index = 0; index < LegacyKeys.Length; index++) PlayerPrefs.SetString(LegacyKeys[index], LegacyValues[index]);
            // Synthetic fixture only: do not submit a user's age or drive the age-choice UI.
            PlayerPrefs.SetString(GoogleAdMobManager.AgeChoicePreferenceKey, LocalAgeChoice.Encode(AgeChoice.Declined));
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
        Require(PlayerPrefs.GetString(GoogleAdMobManager.AgeChoicePreferenceKey, "") == LocalAgeChoice.Encode(AgeChoice.Declined));
        _ready = true;
        _driverDirectory = Path.Combine(Application.persistentDataPath, "DeletionDriverV1");
        Directory.CreateDirectory(_driverDirectory);
        var root = new GameObject("Offline deletion lifecycle verification");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<RevivalDeletionGameplayHarness>();
        // Install only local delegates. CloudScriptDeletionClient does not install its SDK factory in gameplay builds.
        DeletionPresenter.RuntimeFlowFactory = CreateFlow;
        DeletionRecoveryGuard.HasPendingSubmission = account => false;
        Mark("BOOT phase=" + (_firstProcess ? "initial-A" : "cold-pause") + " pid=" + Pid + " network=disabled syntheticAge=Declined ageVerification=false");
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
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var login = FindFirstObjectByType<AD.Login>();
        Require(login != null);
        var panel = (GameObject)typeof(AD.Login).GetField("_retry", fields).GetValue(login);
        var body = (TMPro.TMP_Text)typeof(AD.Login).GetField("_retryText", fields).GetValue(login);
        var loading = (GameObject)typeof(AD.Login).GetField("_loading", fields).GetValue(login);
        var button = panel.GetComponentInChildren<UnityEngine.UI.Button>(true);
        const string message = "Deletion request accepted. Sign in explicitly to continue.";
        Require(body != null && body.text == message && body.transform.parent == panel.transform &&
            body.name == "TMP_Contents" && panel.transform.Find("RetryMessage") == null &&
            loading != null && !loading.activeSelf && button != null &&
            !body.transform.IsChildOf(button.transform) &&
            button.GetComponentsInChildren<TMPro.TMP_Text>(true).Single().text == "Retry" &&
            panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Count(label => label.text == message) == 1 &&
            !panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(label => label.text.Contains("Please check your connection")));
        Canvas.ForceUpdateCanvases();
        body.ForceMeshUpdate();
        Require(!body.isTextOverflowing && Mathf.Approximately(body.fontSize, 30));
        Mark("LOGIN_RETRY_MESSAGE_PASS bodyCount=1 caption=Retry loadingHidden=true obsoleteNetworkText=absent fontSize=30 overflow=false programmaticObservation=true");
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
#if TAMER_PRIVACY_PRODUCT_UI_HARNESS
        // Opening the original panel is allowed; this variant cannot preview/submit deletion.
        return new DeletionFlow(new UnavailableDeletionGateway(), () => null);
#else
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
            // Main's normal scene transition has already refreshed data. Count only
            // reads after the held write/submission, not legitimate prior scene reads.
            _firstProcessReads = _reads;
            Mark("PRE_SUBMISSION_COUNTERS reads=" + _reads + " writes=" + _writes);
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
            Mark("POST_CLEANUP_COUNTERS readsBefore=" + _firstProcessReads + " readsAfter=" + _reads);
            RequireInvariant(!_oldWriteApplied, "old-write-callback-blocked");
            RequireInvariant(_staleLoginRejected, "old-login-callback-rejected");
            RequireInvariant(owner.AccountGeneration > generation, "account-generation-invalidated");
            RequireInvariant(!captured.Matches(owner.DeletionSession()), "captured-session-invalidated");
            RequireInvariant(!owner.IsServerDataReady && !owner.DeletionInProgress, "server-not-ready-no-pending-submission");
            RequireInvariant(string.IsNullOrEmpty(owner.PlayFabId) && owner.PlayFabPlayerData == null, "data-credentials-cleared");
            RequireInvariant(string.IsNullOrEmpty(PlayFabSettings.staticPlayer.PlayFabId) &&
                string.IsNullOrEmpty(PlayFabSettings.staticPlayer.ClientSessionTicket), "sdk-credentials-cleared");
            RequireInvariant(PlayerPrefs.GetInt(DataManager.DeletionLoginPauseKey, 0) == 1, "explicit-login-pause-saved");
            RequireInvariant(!owner.TryUpdateLocalData("Gold", "123"), "local-write-blocked");
            RequireInvariant(!File.Exists(Progress) && !File.Exists(Inventory(AccountA)), "owned-progress-inventory-removed");
            RequireInvariant(!File.Exists(OwnedBackup) && !File.Exists(OwnedArchive), "owned-backup-archive-removed");
            RequireInvariant(_reads == _firstProcessReads && !Managers.ServerM.IsInProgress, "post-submission-read-undispatched");
            RequireInvariant(Player.Instance != null && !Player.Instance.IsCurrentCombatSession(), "player-session-invalidated");
            RequirePreserved();
            _cleanupVerified = true;
            Mark("CLEANUP_VERIFIED ownedP=false ownedI=false ownedBackup=false ownedArchive=false foreignB=preserved legacy=preserved oldWrite=blocked queuedRead=undispatched oldLogin=stale-rejected playerSession=false");
            }
            catch (Exception error) { Fail("accepted-cleanup", error); throw; }
        }, owner.FinishCancelledDeletion,
            new FileDeletionRecoveryStore(Path.Combine(_directory, "DeletionJournal.json")),
            DeletionRecovery.Hash("offline-deletion-gameplay-v1", session.TitleId, session.AccountId, session.EntityId));
#endif
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
            Managers.SceneM.NextScene(GameConstants.Scene.Main);
        }
        catch (Exception error) { Fail("prepare", error); }
    }

    private void Update()
    {
        if (!_ready) return;
        try
        {
#if TAMER_PRIVACY_PRODUCT_UI_HARNESS
            DrivePrivacyProductUiCommand();
#else
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
#endif
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
#if TAMER_PRIVACY_PRODUCT_UI_HARNESS
        // No legacy v4 command, including settings/privacy/request/confirm/exit/retry/shop,
        // may invoke a product listener in this native-input-only UI variant.
        throw new InvalidOperationException("Legacy driver is unavailable in privacy product UI.");
#else
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
            case "shop":
                Require(!_firstProcess && _mainObserved && _authenticated && _ready && _errors == 0);
                var playerCollider = Player.Instance.GetComponent<Collider>();
                Require(playerCollider != null && playerCollider.CompareTag("Player") && ShopMan.Instance != null);
                File.Move(input, Path.Combine(_driverDirectory, "shop.used"));
                Mark("PROGRAMMATIC_SHOP_TRIGGER originalHandler=true touchCoverage=false purchase=false");
                ShopMan.Instance.SendMessage("OnTriggerEnter", playerCollider, SendMessageOptions.RequireReceiver);
                ShopMan.Instance.OpenShop(0);
                return;
            default: throw new InvalidOperationException("Unknown fixture-only driver command.");
        }
        Require(button != null && button.gameObject.activeInHierarchy && button.IsInteractable());
        File.Move(input, Path.Combine(_driverDirectory, command + ".used"));
        Mark("PROGRAMMATIC_UGUI command=" + command + " fixtureOnly=true touchCoverage=false");
        button.onClick.Invoke();
#endif
    }

#if TAMER_PRIVACY_PRODUCT_UI_HARNESS
    private enum PrivacyUiStage { Idle, Updating, Failing, Failed, Reopening, Busy, StalePending, Stale, Settling, Settled }
    private PrivacyUiStage _privacyUiStage;
    private PreparingPrivacyProductUiClient _privacyUiClient;
    private GoogleAdMobManager _privacyUiAds;
    private DataManager _privacyUiOwner;
    private DeletionView _privacyUiView;
    private UnityEngine.UI.Button _privacyUiAge, _privacyUiButton;
    private TMPro.TMP_Text _privacyUiStatus;
    private int _privacyUiFactoryCalls, _privacyUiClicks, _privacyUiAgeClicks, _privacyUiFrames, _privacyUiBusyFrame;
    private const string PrivacyFormFailedText = "개인정보 창을 열지 못했습니다. 준비 중이거나 연결에 문제가 있을 수 있습니다. 잠시 후 같은 버튼을 다시 눌러 주세요.";
    private const string PrivacyCheckingText = "개인정보 설정을 확인하고 있습니다.";
    private const string PrivacyClosedText = "개인정보 선택 창을 닫았습니다. 선택 내용을 변경하려면 다시 확인해 주세요.";

    // The only adapter owned by this variant. These callbacks are synthetic, never UMP callbacks.
    private sealed class PreparingPrivacyProductUiClient : IAdConsentClient, IAdPrivacyStatusClient
    {
        public int Updates, Shows, Gathers, UpdateDeliveries, FirstCloseDeliveries, SecondCloseDeliveries;
        public Action<bool> Updated;
        public readonly List<Action<bool>> Closed = new List<Action<bool>>();
        public bool CanRequestAds => true; // The real privacy-only gate must still block ads.
        public bool PrivacyOptionsRequired => true;
        public AdPrivacyRequirement PrivacyRequirement => AdPrivacyRequirement.Required;
        public void Update(bool underAge, Action<bool> completed)
        {
            RequireInvariant(!underAge && ++Updates == 1 && Updated == null && completed != null, "privacy-update-once");
            Updated = completed;
        }
        public void Gather(Action<bool> completed)
        {
            Gathers++;
            throw new FixtureInvariantException("privacy-gather-forbidden");
        }
        public void ShowPrivacyOptions(Action<bool> completed)
        {
            RequireInvariant(++Shows <= 2 && completed != null, "privacy-show-limit");
            Closed.Add(completed);
        }
    }

    private void RequirePrivacyProductUiContext()
    {
        RequireInvariant(_ready && _errors == 0 && _firstProcess && _mainObserved && !_cleanupVerified &&
            !_acceptedObserved && _submissions == 0 && _state != null && _state.Phase == "prepared" &&
            _state.InitialPid == Pid && !Application.isEditor && !Application.isBatchMode && Debug.isDebugBuild &&
            Application.platform == RuntimePlatform.Android &&
            Application.identifier == RevivalGameplayIsolation.PrivacyProductUiApplicationId &&
            UnitySceneManager.GetActiveScene().name == "Main" && !Managers.SceneM.IsTransitioning,
            "privacy-first-main-exact-package");
        var ads = Managers.GoogleAdMobM;
        var owner = Managers.DataM;
        RequireInvariant(ads != null && owner != null && owner.PlayFabId == AccountA && owner.IsServerDataReady &&
            !owner.DeletionInProgress && !ads.HasNoAds && !ads.CanRequestAds &&
            ads.AgeSelection.Value == AgeChoice.Declined && !ads.AgeSelection.HasAge && !ads.AgeSelection.IsEditing &&
            PlayerPrefs.GetString(GoogleAdMobManager.AgeChoicePreferenceKey, "") == LocalAgeChoice.Encode(AgeChoice.Declined) &&
            owner.LocalPlayerData != null && owner.LocalPlayerData.TryGetValue("GooglePlay", out string purchase) &&
            purchase == "" && Managers.IAPM.Status == IAPStatus.Unavailable,
            "privacy-saved-declined-noads-false");
        RequireInvariant(!PrivacyManagerField<bool>(ads, "_initialized") && !PrivacyManagerField<bool>(ads, "_initializing") &&
            !PrivacyManagerField<bool>(ads, "_loading") && PrivacyManagerField<object>(ads, "_consent") == null &&
            PrivacyManagerField<object>(ads, "_rewardedAd") == null && PrivacyManagerField<object>(ads, "_showingAd") == null &&
            PrivacyManagerField<object>(ads, "_session") == null && PrivacyManagerField<object>(ads, "_closingSession") == null,
            "privacy-no-managed-ad-owner");
        RequirePreserved();
        if (_privacyUiClient != null)
        {
            RequireInvariant(ReferenceEquals(ads, _privacyUiAds) && ReferenceEquals(owner, _privacyUiOwner) &&
                _privacyUiFactoryCalls == 1 && _privacyUiClient.Updates == 1 && _privacyUiClient.Gathers == 0 &&
                _privacyUiClient.UpdateDeliveries == 1 && _privacyUiClient.Shows >= 0 && _privacyUiClient.Shows <= 2 &&
                _privacyUiClicks <= 1 && _privacyUiAgeClicks == 0, "privacy-one-fake-owner");
            var privacyOwner = PrivacyManagerField<AdConsentGate>(ads, "_privacyConsent");
            RequireInvariant(privacyOwner != null && privacyOwner.IsPrivacyRefreshOwner && privacyOwner.IsPrivacyOnly &&
                (_privacyUiStage == PrivacyUiStage.Updating || ads.PrivacyOptionsRequired), "privacy-retained-required-owner");
        }
    }

    private static T PrivacyManagerField<T>(GoogleAdMobManager ads, string name)
    {
        var field = typeof(GoogleAdMobManager).GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        RequireInvariant(field != null, "privacy-manager-field-present");
        return (T)field.GetValue(ads);
    }

    private void BindOriginalPrivacyProductUi()
    {
        _privacyUiView = FindObjectsByType<DeletionView>(FindObjectsSortMode.None).Single(view =>
            view.name == "AccountPrivacy" && view.gameObject.activeInHierarchy &&
            view.GetComponent<AgePrivacyOptionsEntry>() != null && view.GetComponent<DeletionPresenter>() != null);
        _privacyUiAge = _privacyUiView.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "AgeChoice");
        _privacyUiButton = _privacyUiView.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "AdPrivacyOptions");
        _privacyUiStatus = _privacyUiView.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(text => text.name == "AdPrivacyStatus");
        RequireInvariant(_privacyUiView.isActiveAndEnabled && !_privacyUiView.ExitOnClose &&
            _privacyUiView.GetComponent<AgePrivacyOptionsEntry>().isActiveAndEnabled &&
            _privacyUiAge.gameObject.activeInHierarchy && _privacyUiAge.IsInteractable() &&
            _privacyUiButton.gameObject.activeInHierarchy && _privacyUiButton.IsInteractable() &&
            _privacyUiStatus.gameObject.activeInHierarchy, "privacy-original-panel-open");
        // Observation only: the driver never invokes onClick, CheckPrivacy or RequestPrivacySettings.
        _privacyUiButton.onClick.AddListener(ObserveOriginalPrivacyProductUiClick);
        _privacyUiAge.onClick.AddListener(() => { _privacyUiAgeClicks++; });
    }

    private void ObserveOriginalPrivacyProductUiClick()
    {
        _privacyUiClicks++;
        RequireInvariant(_privacyUiStage == PrivacyUiStage.Failed && _privacyUiClicks == 1 &&
            _privacyUiClient.Updates == 1 && _privacyUiClient.Shows == 2,
            "privacy-original-listener-reopens-owner");
        _privacyUiStage = PrivacyUiStage.Reopening;
        _privacyUiFrames = 0;
    }

    private void DrivePrivacyProductUiCommand()
    {
        string input = Path.Combine(_driverDirectory, "command.txt");
        if (!File.Exists(input)) return;
        RequirePrivacyProductUiContext();
        RequireInvariant(new FileInfo(input).Length <= 32, "privacy-command-small");
        string command = File.ReadAllText(input).Trim();
        switch (command)
        {
            case "privacy-prime":
                RequireInvariant(_privacyUiStage == PrivacyUiStage.Idle && _privacyUiClient == null, "privacy-prime-once");
                BindOriginalPrivacyProductUi();
                _privacyUiAds = Managers.GoogleAdMobM;
                _privacyUiOwner = Managers.DataM;
                RequireInvariant(PrivacyManagerField<object>(_privacyUiAds, "_privacyConsent") == null,
                    "privacy-unprimed-owner");
                RequireInvariant(AgeTreatmentPolicy.TryCreatePlan(AgeChoice.Adult, out var plan), "privacy-adapter-plan");
                // Constant method name and explicit parameter types keep IL2CPP reflection analyzable.
                var begin = typeof(GoogleAdMobManager).GetMethod("BeginPrivacyRefresh",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(AgeTreatmentPlan), typeof(Func<IAdConsentClient>), typeof(Action<string>) }, null);
                RequireInvariant(begin != null, "privacy-private-refresh-method-present");
                File.Move(input, Path.Combine(_driverDirectory, command + ".used"));
                _privacyUiClient = new PreparingPrivacyProductUiClient();
                Func<IAdConsentClient> factory = () => { _privacyUiFactoryCalls++; return _privacyUiClient; };
                _privacyUiStage = PrivacyUiStage.Updating;
                _privacyUiFrames = 0;
                try { begin.Invoke(_privacyUiAds, new object[] { plan, factory, null }); }
                catch (System.Reflection.TargetInvocationException error)
                {
                    Mark("PRIVACY_PRODUCT_UI_REFLECTION_FAILURE innerType=" +
                        (error.InnerException == null ? "none" : error.InnerException.GetType().Name));
                    throw;
                }
                RequireInvariant(_privacyUiFactoryCalls == 1 && _privacyUiClient.Updates == 1 &&
                    _privacyUiClient.Updated != null, "privacy-fake-update-created");
                _privacyUiClient.UpdateDeliveries++;
                _privacyUiClient.Updated(true);
                break;
            case "privacy-stale":
                RequireInvariant(_privacyUiStage == PrivacyUiStage.Busy && Time.frameCount - _privacyUiBusyFrame >= 3 &&
                    _privacyUiClicks == 1 && _privacyUiClient.Shows == 2 && _privacyUiClient.Closed.Count == 2 &&
                    _privacyUiClient.FirstCloseDeliveries == 1 && _privacyUiClient.SecondCloseDeliveries == 0,
                    "privacy-stale-after-native-reentry");
                RequirePrivacyProductUiBusy();
                File.Move(input, Path.Combine(_driverDirectory, command + ".used"));
                _privacyUiStage = PrivacyUiStage.StalePending;
                _privacyUiFrames = 0;
                _privacyUiClient.FirstCloseDeliveries++;
                _privacyUiClient.Closed[0](true);
                break;
            case "privacy-settle":
                RequireInvariant(_privacyUiStage == PrivacyUiStage.Stale && _privacyUiClicks == 1 &&
                    _privacyUiClient.Shows == 2 && _privacyUiClient.FirstCloseDeliveries == 2 &&
                    _privacyUiClient.SecondCloseDeliveries == 0, "privacy-settle-after-stale");
                RequirePrivacyProductUiBusy();
                File.Move(input, Path.Combine(_driverDirectory, command + ".used"));
                _privacyUiStage = PrivacyUiStage.Settling;
                _privacyUiFrames = 0;
                _privacyUiClient.SecondCloseDeliveries++;
                _privacyUiClient.Closed[1](true);
                break;
            default:
                throw new FixtureInvariantException("privacy-command-not-allowlisted");
        }
        Mark("PRIVACY_PRODUCT_UI_COMMAND command=" + command + " syntheticAdapter=true productButtonInvocations=0 nativeTouchEvidence=external");
    }

    private void LateUpdate()
    {
        if (!_ready || _privacyUiStage == PrivacyUiStage.Idle) return;
        try
        {
            RequirePrivacyProductUiContext();
            RequireInvariant(_privacyUiView != null && _privacyUiView.gameObject.activeInHierarchy &&
                _privacyUiView.GetComponent<AgePrivacyOptionsEntry>().isActiveAndEnabled,
                "privacy-original-panel-remains-open");
            _privacyUiFrames++;
            switch (_privacyUiStage)
            {
                case PrivacyUiStage.Updating:
                    RequireInvariant(_privacyUiClient.Shows <= 1, "privacy-no-extra-first-show");
                    if (_privacyUiClient.Shows == 1)
                    {
                        RequireInvariant(_privacyUiClient.Closed.Count == 1 && _privacyUiClient.FirstCloseDeliveries == 0,
                            "privacy-first-show-callback-present");
                        _privacyUiStage = PrivacyUiStage.Failing;
                        _privacyUiFrames = 0;
                        _privacyUiClient.FirstCloseDeliveries++;
                        _privacyUiClient.Closed[0](false);
                    }
                    else RequireInvariant(_privacyUiFrames <= 30, "privacy-update-dispatch-timeout");
                    break;
                case PrivacyUiStage.Failing:
                    if (_privacyUiAds.PrivacySettingsResult != AdPrivacyResult.FormFailed)
                    { RequireInvariant(_privacyUiFrames <= 30, "privacy-first-failure-dispatch-timeout"); break; }
                    // All product Update methods have run before this LateUpdate; allow layout to settle.
                    if (!PrivacyProductUiMatches(PrivacyFormFailedText, false, true))
                    { RequireInvariant(_privacyUiFrames <= 30, "privacy-formfailed-ui-timeout"); break; }
                    RequireInvariant(_privacyUiClient.Shows == 1 && _privacyUiClicks == 0, "privacy-formfailed-no-auto-retry");
                    _privacyUiStage = PrivacyUiStage.Failed;
                    LogPrivacyProductUiSnapshot("FormFailed");
                    break;
                case PrivacyUiStage.Failed:
                    RequireInvariant(_privacyUiAds.PrivacySettingsResult == AdPrivacyResult.FormFailed &&
                        PrivacyProductUiMatches(PrivacyFormFailedText, false, true) && _privacyUiClient.Shows == 1 &&
                        _privacyUiClicks == 0, "privacy-no-frame-driven-retry");
                    break;
                case PrivacyUiStage.Reopening:
                    if (!PrivacyProductUiMatches(PrivacyCheckingText, true, false))
                    { RequireInvariant(_privacyUiFrames <= 30, "privacy-reentry-ui-timeout"); break; }
                    RequireInvariant(_privacyUiAds.PrivacySettingsResult == AdPrivacyResult.Checking &&
                        _privacyUiClient.Shows == 2 && _privacyUiClicks == 1, "privacy-original-reentry-counts");
                    _privacyUiStage = PrivacyUiStage.Busy;
                    _privacyUiBusyFrame = Time.frameCount;
                    LogPrivacyProductUiSnapshot("CheckingAfterOriginalButtonEvent");
                    break;
                case PrivacyUiStage.Busy:
                case PrivacyUiStage.Stale:
                    RequirePrivacyProductUiBusy();
                    break;
                case PrivacyUiStage.StalePending:
                    RequirePrivacyProductUiBusy();
                    // Waiting two completed frames proves the stale callback passed the real manager queue.
                    if (_privacyUiFrames < 2) break;
                    _privacyUiStage = PrivacyUiStage.Stale;
                    LogPrivacyProductUiSnapshot("StaleFirstCallbackIgnored");
                    break;
                case PrivacyUiStage.Settling:
                    if (_privacyUiAds.PrivacySettingsResult != AdPrivacyResult.OptionsClosed ||
                        !PrivacyProductUiMatches(PrivacyClosedText, false, true))
                    { RequireInvariant(_privacyUiFrames <= 30, "privacy-settle-ui-timeout"); break; }
                    RequireInvariant(_privacyUiClient.Shows == 2 && _privacyUiClicks == 1 &&
                        _privacyUiClient.FirstCloseDeliveries == 2 && _privacyUiClient.SecondCloseDeliveries == 1,
                        "privacy-final-callback-counts");
                    _privacyUiStage = PrivacyUiStage.Settled;
                    LogPrivacyProductUiSnapshot("OptionsClosed");
                    break;
                case PrivacyUiStage.Settled:
                    RequireInvariant(_privacyUiAds.PrivacySettingsResult == AdPrivacyResult.OptionsClosed &&
                        PrivacyProductUiMatches(PrivacyClosedText, false, true) && _privacyUiClient.Shows == 2 &&
                        _privacyUiClicks == 1, "privacy-terminal-ui-stable");
                    break;
            }
        }
        catch (Exception error)
        {
            Mark("PRIVACY_PRODUCT_UI_OBSERVE_FAILURE " + JsonConvert.SerializeObject(new
            {
                stage = _privacyUiStage.ToString(), exceptionType = error.GetType().Name,
                result = _privacyUiAds == null ? "no-manager" : _privacyUiAds.PrivacySettingsResult.ToString(),
                busy = _privacyUiAds != null && _privacyUiAds.IsConsentBusy,
                updates = _privacyUiClient == null ? 0 : _privacyUiClient.Updates,
                shows = _privacyUiClient == null ? 0 : _privacyUiClient.Shows,
                originalPrivacyClickEvents = _privacyUiClicks,
                statusText = _privacyUiStatus == null ? "no-status" : _privacyUiStatus.text,
                syntheticAdapter = true, realUmpSdkRetested = false
            }));
            Fail("privacy-product-ui-observe", error);
        }
    }

    private bool PrivacyProductUiMatches(string text, bool busy, bool interactable) =>
        _privacyUiAds.IsConsentBusy == busy && _privacyUiStatus.gameObject.activeInHierarchy &&
        _privacyUiStatus.text == text && _privacyUiAge.gameObject.activeInHierarchy &&
        _privacyUiAge.IsInteractable() == interactable && _privacyUiButton.gameObject.activeInHierarchy &&
        _privacyUiButton.IsInteractable() == interactable;

    private void RequirePrivacyProductUiBusy() => RequireInvariant(
        _privacyUiAds.PrivacySettingsResult == AdPrivacyResult.Checking &&
        PrivacyProductUiMatches(PrivacyCheckingText, true, false) && _privacyUiClient.Shows == 2 &&
        _privacyUiClicks == 1 && _privacyUiClient.SecondCloseDeliveries == 0, "privacy-second-owner-still-busy");

    private static Rect PrivacyScreenRect(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        RequireInvariant(canvas != null, "privacy-product-canvas-present");
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RequireInvariant(canvas.renderMode == RenderMode.ScreenSpaceOverlay || camera != null, "privacy-product-canvas-camera-present");
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
            points.Max(point => point.x), points.Max(point => point.y));
    }

    private static string PrivacyRaycastTop(UnityEngine.UI.Button button, Rect screen)
    {
        var events = UnityEngine.EventSystems.EventSystem.current;
        RequireInvariant(events != null, "privacy-eventsystem-present");
        var pointer = new UnityEngine.EventSystems.PointerEventData(events) { position = screen.center };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(pointer, hits);
        var top = hits.Count == 0 ? null : hits[0].gameObject;
        bool targetsButton = top != null && (top == button.gameObject || top.transform.IsChildOf(button.transform));
        return targetsButton ? "original-button" : top == null ? "none" : "other-product-object";
    }

    private void LogPrivacyProductUiSnapshot(string stage)
    {
        Canvas.ForceUpdateCanvases();
        _privacyUiStatus.ForceMeshUpdate();
        var textRect = _privacyUiStatus.rectTransform.rect;
        var preferred = _privacyUiStatus.GetPreferredValues(_privacyUiStatus.text, textRect.width, float.PositiveInfinity);
        var statusScreen = PrivacyScreenRect(_privacyUiStatus.rectTransform);
        var buttonScreen = PrivacyScreenRect((RectTransform)_privacyUiButton.transform);
        var ageScreen = PrivacyScreenRect((RectTransform)_privacyUiAge.transform);
        bool fitsScreen(Rect value) => value.width > 0 && value.height > 0 && value.xMin >= -.5f &&
            value.yMin >= -.5f && value.xMax <= Screen.width + .5f && value.yMax <= Screen.height + .5f;
        bool fits = !_privacyUiStatus.isTextOverflowing && preferred.y <= textRect.height + .5f &&
            fitsScreen(statusScreen) && fitsScreen(buttonScreen) && fitsScreen(ageScreen);
        var privacyHit = PrivacyRaycastTop(_privacyUiButton, buttonScreen);
        var ageHit = PrivacyRaycastTop(_privacyUiAge, ageScreen);
        RequireInvariant(fits, "privacy-full-text-and-buttons-fit");
        RequireInvariant(!_privacyUiButton.IsInteractable() || privacyHit == "original-button", "privacy-original-button-raycast");
        RequireInvariant(!_privacyUiAge.IsInteractable() || ageHit == "original-button", "privacy-age-button-raycast");
        int sequence = stage == "FormFailed" ? 1 : stage == "CheckingAfterOriginalButtonEvent" ? 2 :
            stage == "StaleFirstCallbackIgnored" ? 3 : stage == "OptionsClosed" ? 4 : 0;
        RequireInvariant(sequence != 0, "privacy-snapshot-stage-allowlisted");
        var snapshot = new
        {
            schema = 1, sequence,
            stage, pid = Pid, syntheticAdapter = true, realUmpSdkRetested = false,
            originalMain = true, originalAccountPrivacyView = true, nativeTouchEvidence = "external-log-and-screen",
            factoryCalls = _privacyUiFactoryCalls, updates = _privacyUiClient.Updates, shows = _privacyUiClient.Shows,
            gathers = _privacyUiClient.Gathers, updateDeliveries = _privacyUiClient.UpdateDeliveries,
            firstCloseDeliveries = _privacyUiClient.FirstCloseDeliveries, secondCloseDeliveries = _privacyUiClient.SecondCloseDeliveries,
            originalPrivacyClickEvents = _privacyUiClicks, ageClickEvents = _privacyUiAgeClicks,
            managerBusy = _privacyUiAds.IsConsentBusy, result = _privacyUiAds.PrivacySettingsResult.ToString(),
            statusText = _privacyUiStatus.text, overflow = _privacyUiStatus.isTextOverflowing, fullTextAndButtonsFit = fits,
            preferredWidth = preferred.x, preferredHeight = preferred.y, textRectWidth = textRect.width, textRectHeight = textRect.height,
            screenWidth = Screen.width, screenHeight = Screen.height,
            statusScreenRect = new[] { statusScreen.xMin, statusScreen.yMin, statusScreen.xMax, statusScreen.yMax },
            privacyButtonScreenRect = new[] { buttonScreen.xMin, buttonScreen.yMin, buttonScreen.xMax, buttonScreen.yMax },
            ageButtonScreenRect = new[] { ageScreen.xMin, ageScreen.yMin, ageScreen.xMax, ageScreen.yMax },
            privacyActive = _privacyUiButton.gameObject.activeInHierarchy, privacyInteractable = _privacyUiButton.IsInteractable(),
            ageActive = _privacyUiAge.gameObject.activeInHierarchy, ageInteractable = _privacyUiAge.IsInteractable(),
            privacyRaycastTop = privacyHit, ageRaycastTop = ageHit,
            savedDeclinedUnchanged = true, viewOwnerNoAds = _privacyUiAds.HasNoAds, googlePlayEmpty = true,
            initialized = false, initializing = false, loading = false, rewarded = "null", showing = "null", consent = "null",
            canRequestAds = _privacyUiAds.CanRequestAds, iap = Managers.IAPM.Status.ToString(), submissions = _submissions, errors = _errors,
            foreignBSha256 = HashFile(Inventory(AccountB)), legacySha256 = LegacyHash(),
            adapterNativeSdkCalls = 0, adapterNetworkCalls = 0,
            networkPermissionEvidence = "verify-final-apk", realSdkFactoryUsed = false, deletionLifecycleRetested = false
        };
        // Android's Unity log payload can truncate at 1,024 bytes. Keep the full
        // observation in this one-process fixture; publish only its short index.
        string name = "privacy-ui-state-" + sequence + ".json";
        string path = Path.Combine(_driverDirectory, name);
        WriteNew(path, snapshot);
        byte[] expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot));
        byte[] actual = File.ReadAllBytes(path);
        RequireInvariant(actual.SequenceEqual(expected), "privacy-snapshot-readback");
        Mark("PRIVACY_PRODUCT_UI_FILE schema=1 sequence=" + sequence + " stage=" + stage +
            " pid=" + Pid + " file=" + name + " bytes=" + actual.Length + " sha256=" + Hash(actual));
    }
#endif

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
            string itemName = (string)typeof(Item).GetField("_itemName", fields).GetValue(item);
            string price = (string)typeof(Item).GetField("_itemPrice", fields).GetValue(item);
            Require(icon != null && icon.activeInHierarchy && text != null &&
                text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(price) &&
                text.text == itemName + "\nPrice - " + price + "G");
            names.Add(itemName);
        }
        string observation = string.Join(",", names.OrderBy(name => name, StringComparer.Ordinal));
        if (observation == _lastShopUi) return;
        _lastShopUi = observation;
        RequirePreserved();
        Require(_ready && _errors == 0);
        Mark("C_SHOP_VISIBLE items=" + items.Length + " allLocked=true allUnequipped=true priceLabelsMatched=true foreignB=preserved legacy=preserved programmaticOpen=true");
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
            var invariant = error as FixtureInvariantException;
            Debug.LogError("DELETION_GAMEPLAY_FAIL stage=" + stage + " type=" + error.GetType().Name +
                (invariant == null ? "" : " invariant=" + invariant.Invariant) + " evidence=preserved");
    }
    private static void Mark(string message) => Debug.Log("DELETION_GAMEPLAY " + message);
}
#endif
