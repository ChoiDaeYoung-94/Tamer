#if TAMER_PRIVACY_PRODUCT_UMP_HARNESS && (!TAMER_REVIVAL_SMOKE || !TAMER_GAMEPLAY_HARNESS || !TAMER_AD_TEST_HARNESS)
#error Privacy product UMP requires the three isolated base symbols.
#endif
#if TAMER_PRIVACY_PRODUCT_UMP_HARNESS && (TAMER_PRIVACY_COLD_HARNESS || TAMER_PRIVACY_COLD_RETRY_HARNESS || TAMER_PRIVACY_MANAGER_HARNESS || TAMER_DELETION_GAMEPLAY || TAMER_PRIVACY_PRODUCT_UI_HARNESS || TAMER_UMP_ONLY_HARNESS || TAMER_UMP_PUBLISHER_HARNESS || TAMER_PRIVACY_UI_HARNESS || TAMER_AD_SAMPLE_CLOSE_HARNESS || TAMER_TEST_ADS || TAMER_IAP_HARNESS || TAMER_IAP_STORE_TEST || TAMER_PGS_HARNESS || TAMER_DELETION_HARNESS || TAMER_RECEIPT_HARNESS || TAMER_SHOP_HARNESS || TAMER_AGE_CHOICE || TAMER_SESSION_HARNESS || TAMER_PLAYER_RESTORE || TAMER_INVENTORY_RESTORE || TAMER_GAMEPLAY_PHOTO || TAMER_PROGRESS_HARNESS || TAMER_JOURNAL_HARNESS || TAMER_GAMESAVE_HARNESS || TAMER_GAMESAVE_CLOUD || TAMER_DELETION_ONLINE_TRIAL || TAMER_SHOP_UI || TAMER_SHOP_AFTER_INVENTORY || TAMER_CHARACTER_UI || TAMER_PREPARATION || TAMER_IAP_REVIEWED_RECOVERY || TAMER_IAP_RECOVERY_RESUME_APPROVED || TAMER_IAP_TEST_CATALOG)
#error Privacy product UMP must not be combined with another fixture.
#endif
#if TAMER_PRIVACY_PRODUCT_UMP_HARNESS
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AD;
using AD.Advertising;
using AD.Privacy;
using GoogleMobileAds.Ump.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>Explicit synthetic Adult prime, then original UI warm reentry with the real SDK.
/// This does not test ordinary cold first-button entry or approve a production policy.</summary>
public sealed class RevivalPrivacyProductUmpHarness : MonoBehaviour
{
    // Use public effective policy checks; the standalone helper is absent in this variant.
    private static bool EffectiveReviewsDisabled => !AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed &&
        !AgeTreatmentPolicy.RegionalConsentReviewed && !AgeTreatmentPolicy.IsReviewed(AgeChoice.Under13) &&
        !AgeTreatmentPolicy.IsReviewed(AgeChoice.From13To15) && !AgeTreatmentPolicy.IsReviewed(AgeChoice.From16To17) &&
        !AgeTreatmentPolicy.IsReviewed(AgeChoice.Adult);
    public const string FixtureDirectory = "RevivalPrivacyProductUmpV2";
    public const string Account = "privacy-product-ump-synthetic-v1";
    private const string ColdPurchaseKey = "Tamer.Harness.PrivacyCold.Purchases";
    private const string ColdFixtureKey = "Tamer.Harness.PrivacyCold.FixtureId";
    private static readonly string[] ProtectedKeys = { GoogleAdMobManager.AgeChoicePreferenceKey,
        ColdPurchaseKey, ColdFixtureKey, "AllyMonsters", "playerEquippedItems", "LocalItem",
        DataManager.DeletionLoginPauseKey, "AD_LastGpgsId", "AD_CustomId", "AD_LoginMode", "AD_GoogleSignupPending" };
    private static readonly string Token = Guid.NewGuid().ToString("N");
    private static string _directory, _preferencesHash, _registrationInputPath;
    private static Dictionary<string, string> _preservedFiles;
    private static RevivalPrivacyProductUmpHarness _instance;
    private static bool _bootClaimed, _primeConsumed, _factoryConsumed;
    private static DataManager _data;
    private static ServerManager _server;
    private static GoogleAdMobManager _ads;
    private static AdConsentGate _gate;
    private static BoundedClient _client;
    private readonly ConcurrentQueue<Action> _observations = new ConcurrentQueue<Action>();
    private readonly List<object> _sdk = new List<object>();
    private string _registration, _registrationReceiptSha256, _failure = "", _lastState = "", _lastCommand = "";
    private bool _registrationInputZeroed, _registrationInputDeleted;
    private int _registrationInputBytes, _registrationMaskedLength;
    private int _commandSequence, _snapshotSequence, _privacyClicks, _ageClicks, _managedCalls;
    private int _sdkUpdates, _sdkUpdateCallbacks, _sdkShows, _sdkShowCallbacks;
    private long _sdkSequence;
    private bool _ready, _armed, _bound, _warmAllowed, _firstFailed, _finished;
    private volatile bool _freshUpdate;
    private bool _freshRequired, _lastShowSucceeded;
    private int _lastErrorCode = -1;
    private AD.Managers _managers;
    private Button _privacyButton, _ageButton;
    private TMPro.TMP_Text _status;
    private AD.DeletionView _view;
    private float _nextSnapshot;
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
    private static void Require(bool value, string stage)
    { if (!value) throw new InvalidOperationException("Privacy product UMP invariant: " + stage); }
    private static string Hash(byte[] value)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
    private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
    private static string PreferenceHash() => Hash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
        ProtectedKeys.Select(key => new { key, exists = PlayerPrefs.HasKey(key),
            text = PlayerPrefs.GetString(key, ""), integer = PlayerPrefs.GetInt(key, int.MinValue) }))));
    private static Dictionary<string, string> ExistingFiles() => Directory.GetFiles(Application.persistentDataPath,
        "*", SearchOption.AllDirectories).Where(path => !path.StartsWith(_directory + Path.DirectorySeparatorChar,
        StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal)
        .ToDictionary(path => path, HashFile, StringComparer.Ordinal);
    private static void WriteNew(string path, object value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "evidence-readback");
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    public static void Boot()
    {
        Require(!_bootClaimed, "boot-once");
        _bootClaimed = true;
        Require(!Application.isEditor && !Application.isBatchMode && Debug.isDebugBuild &&
            Application.platform == RuntimePlatform.Android &&
            Application.identifier == AgeTreatmentPolicy.NativePrivacyColdHarnessPackage &&
            string.IsNullOrEmpty(PlayFabSettings.staticPlayer.ClientSessionTicket), "exact-debug-context");
        Require(PlayerPrefs.GetString(GoogleAdMobManager.AgeChoicePreferenceKey, "") ==
            LocalAgeChoice.Encode(AgeChoice.Declined) &&
            PlayerPrefs.GetString(ColdPurchaseKey, "") == "ProductNoAds" &&
            !string.IsNullOrEmpty(PlayerPrefs.GetString(ColdFixtureKey, "")), "preserved-cold-inputs");
        Require(!File.Exists(Path.Combine(Application.persistentDataPath, "PlayerData.json")) &&
            !new[] { "AllyMonsters", "playerEquippedItems", "LocalItem" }.Any(PlayerPrefs.HasKey), "root-save-or-legacy-preserve-stop");
        _directory = Path.Combine(Application.persistentDataPath, FixtureDirectory);
        Require(!Directory.Exists(_directory) && !File.Exists(_directory), "new-fixture-never-overwrite");
        // ADB-pushed external files belong to shell and cannot be opened for
        // cleansing by the app. Receive only this transient copy in private files,
        // created as the app UID by the debug runner; preserve the source receipt.
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var files = activity.Call<AndroidJavaObject>("getFilesDir"))
        {
            string privateFilesRoot = files.Call<string>("getCanonicalPath");
            Require(privateFilesRoot == "/data/user/0/" + Application.identifier + "/files" ||
                privateFilesRoot == "/data/data/" + Application.identifier + "/files", "exact-private-user-zero-root");
            string inputDirectory = Path.Combine(privateFilesRoot, FixtureDirectory);
            Require(!Directory.Exists(inputDirectory) && !File.Exists(inputDirectory), "private-input-never-overwrite");
            Directory.CreateDirectory(inputDirectory);
            _registrationInputPath = Path.Combine(inputDirectory, "registration.input");
        }
        _preferencesHash = PreferenceHash();
        _preservedFiles = ExistingFiles();
        Directory.CreateDirectory(_directory);
        WriteNew(Path.Combine(_directory, "claim.json"), new { schema = 1, pid = Pid, token = Token,
            preferencesSha256 = _preferencesHash, preservedFiles = _preservedFiles,
            syntheticAdultPlanOnly = true, ordinaryColdFirstButtonTest = false });
        PlayFabSettings.TitleId = "OFFLINE";
        DeletionPresenter.RuntimeFlowFactory = () => new DeletionFlow(new UnavailableDeletionGateway(), () => null);
        var root = new GameObject("Privacy product UMP isolated fixture");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<RevivalPrivacyProductUmpHarness>();
    }
    public static string RequireSaveDirectory(string root)
    {
        Require(_bootClaimed && _instance != null && _directory == Path.Combine(root, FixtureDirectory), "dedicated-save-root");
        return _directory;
    }
    public static ServerManager CreateServer(DataManager owner)
    {
        Require(_bootClaimed && _data == null && owner != null && _server == null, "single-memory-owner");
        _data = owner;
        var seed = RevivalGameplayIsolation.Seed();
        seed["GooglePlay"] = "ProductNoAds";
        _server = RevivalGameplayIsolation.CreateTestServer(owner, () => Account, seed);
        return _server;
    }
    private IEnumerator Start()
    {
        yield return null;
        try
        {
            _managers = Managers.Instance;
            Require(_managers != null && RevivalGameplayIsolation.BlockedLogins == 1 &&
                ReferenceEquals(_data, Managers.DataM) && ReferenceEquals(_server, Managers.ServerM), "original-managers-memory-login");
            _ads = Managers.GoogleAdMobM;
            Require(_ads != null, "actual-ad-manager");
            _ads.HarnessEvent += ManagedEvent;
            _data.BeginAccountSession(Account);
            Managers.ServerM.GetAllData(update: true);
            Require(_data.IsServerDataReady && File.Exists(RevivalGameplayIsolation.SavePath) &&
                _ads.HasNoAds && _data.LocalPlayerData["GooglePlay"] == "ProductNoAds", "real-entitlement-getter");
            RequireContext(false);
            Managers.SceneM.NextScene(GameConstants.Scene.Main);
        }
        catch (Exception error) { Fail("prepare", error); }
    }
    private void ManagedEvent(string value, double at)
    { _managedCalls++; Fail("unexpected-managed-ad-call", new InvalidOperationException(value)); }
    private void RequireContext(bool main)
    {
        Require(_failure.Length == 0 && !_finished && ReferenceEquals(Managers.Instance, _managers) &&
            ReferenceEquals(Managers.DataM, _data) && ReferenceEquals(Managers.ServerM, _server) &&
            ReferenceEquals(Managers.GoogleAdMobM, _ads) && _data.PlayFabId == Account &&
            _data.IsServerDataReady && !_data.DeletionInProgress && _ads.HasNoAds && !_ads.CanRequestAds &&
            _data.LocalPlayerData["GooglePlay"] == "ProductNoAds" &&
            !AdRequestPolicy.ProductionAdsEnabled && EffectiveReviewsDisabled &&
            !PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial &&
            !Field<bool>(_ads, "_initialized") && !Field<bool>(_ads, "_initializing") && !Field<bool>(_ads, "_loading") &&
            Field<object>(_ads, "_consent") == null && Field<object>(_ads, "_rewardedAd") == null &&
            Field<object>(_ads, "_showingAd") == null && Field<object>(_ads, "_session") == null &&
            Field<object>(_ads, "_closingSession") == null && _managedCalls == 0 &&
            _ads.AgeSelection.Value == AgeChoice.Declined && !_ads.AgeSelection.IsEditing &&
            PreferenceHash() == _preferencesHash, "owner-policy-ad-age-preserved");
        Require(!main || (UnitySceneManager.GetActiveScene().name == "Main" && !Managers.SceneM.IsTransitioning &&
            Player.Instance != null && PlayerUICanvas.Instance != null && ShopMan.Instance != null), "original-main-stable");
        if (_gate != null) Require(ReferenceEquals(Field<AdConsentGate>(_ads, "_privacyConsent"), _gate), "same-retained-gate");
    }
    private void Update()
    {
        while (_observations.TryDequeue(out var observation))
            try { observation(); } catch (Exception error) { Fail("sdk-observation", error); }
        if (_failure.Length != 0 || _finished) return;
        try
        {
            if (!_ready)
            {
                if (UnitySceneManager.GetActiveScene().name != "Main" || Managers.SceneM.IsTransitioning ||
                    Player.Instance == null || ShopMan.Instance == null || PlayerUICanvas.Instance == null) return;
                RequireContext(true);
                _ready = true;
                WriteNew(Path.Combine(_directory, "ready.tmp"), new { schema = 1, pid = Pid, token = Token,
                    packageId = Application.identifier, versionCode = 29, commandSequence = 1,
                    registrationInput = "registration.input", registrationInputTransport = "run-as-private-files",
                    registrationInputRelativePath = "files/" + FixtureDirectory + "/registration.input",
                    ordinaryColdFirstButtonTest = false });
                File.Move(Path.Combine(_directory, "ready.tmp"), Path.Combine(_directory, "ready.json"));
                Snapshot("main-ready");
            }
            RequireContext(true);
            DriveCommand();
            ObserveUi();
        }
        catch (Exception error) { Fail("poll", error); }
    }
    private void BindOriginalUi()
    {
        if (_bound) return;
        _view = FindObjectsByType<AD.DeletionView>(FindObjectsSortMode.None).Single(view =>
            view.name == "AccountPrivacy" && view.gameObject.activeInHierarchy && view.GetComponent<AgePrivacyOptionsEntry>() != null);
        _privacyButton = _view.GetComponentsInChildren<Button>(true).Single(button => button.name == "AdPrivacyOptions");
        _ageButton = _view.GetComponentsInChildren<Button>(true).Single(button => button.name == "AgeChoice");
        _status = _view.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(text => text.name == "AdPrivacyStatus");
        Require(_view.GetComponent<AgePrivacyOptionsEntry>().isActiveAndEnabled && _privacyButton.IsInteractable() &&
            _ageButton.IsInteractable(), "original-ui-active");
        // Observe only. The original entry's persistent/runtime listeners perform all UI actions.
        _privacyButton.onClick.AddListener(ObservePrivacyClick);
        _ageButton.onClick.AddListener(ObserveAgeClick);
        _bound = true;
    }
    private void ObservePrivacyClick()
    {
        _privacyClicks++;
        try
        {
            Require(_warmAllowed && _firstFailed && _privacyClicks == 1 && _client.Shows == 2 &&
                _ads.IsConsentBusy && _ads.PrivacySettingsResult == AdPrivacyResult.Checking, "single-original-warm-click");
            _warmAllowed = false;
            Snapshot("original-warm-click");
        }
        catch (Exception error) { Fail("original-click", error); }
    }
    private void ObserveAgeClick() { _ageClicks++; Fail("unexpected-age-click", new InvalidOperationException()); }
    private void DriveCommand()
    {
        string input = Path.Combine(_directory, "command-" + Token + ".ready");
        if (!File.Exists(input)) return;
        Require(new FileInfo(input).Length <= 2048, "small-command");
        var command = JObject.Parse(File.ReadAllText(input), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var allowed = new[] { "schema", "pid", "token", "sequence", "action", "registrationReceiptSha256" };
        Require(command.Properties().All(property => allowed.Contains(property.Name)) && (int?)command["schema"] == 1 &&
            (int?)command["pid"] == Pid && (string)command["token"] == Token &&
            (int?)command["sequence"] == _commandSequence + 1, "command-token-pid-sequence");
        string action = (string)command["action"];
        Require(action == "arm" || action == "prime" || action == "observe" || action == "seal", "command-allowlist");
        // Consume before SDK construction or a call can reenter. Preserve the nonsecret receipt.
        File.Move(input, Path.Combine(_directory, "command-" + (++_commandSequence) + ".used.json"));
        _lastCommand = action;
        switch (action)
        {
            case "arm":
                Require(!_armed && !_primeConsumed && _registration == null, "arm-once");
                _registrationReceiptSha256 = (string)command["registrationReceiptSha256"];
                Require(System.Text.RegularExpressions.Regex.IsMatch(_registrationReceiptSha256 ?? "", "\\A[0-9a-f]{64}\\z"), "provenance-digest-required");
                string path = _registrationInputPath;
                Require(File.Exists(path) && new FileInfo(path).Length <= 34, "new-ipc-input-only");
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    _registrationInputBytes = checked((int)stream.Length);
                    var bytes = new byte[_registrationInputBytes];
                    Require(stream.Read(bytes, 0, bytes.Length) == bytes.Length, "registration-complete-read");
                    _registration = Encoding.ASCII.GetString(bytes).TrimEnd('\r', '\n');
                    Array.Clear(bytes, 0, bytes.Length);
                    Require(System.Text.RegularExpressions.Regex.IsMatch(_registration, "\\A[0-9a-fA-F]{32}\\z"), "registered-hash-format");
                    _registrationMaskedLength = _registration.Length;
                    stream.Position = 0; stream.Write(new byte[_registrationInputBytes], 0, _registrationInputBytes); stream.Flush(true);
                    stream.Position = 0;
                    var zeroReadback = new byte[_registrationInputBytes];
                    Require(stream.Read(zeroReadback, 0, zeroReadback.Length) == zeroReadback.Length &&
                        zeroReadback.All(value => value == 0), "new-ipc-zero-readback");
                    _registrationInputZeroed = true;
                }
                File.Delete(path); // Only this new IPC copy, never the existing registration source.
                _registrationInputDeleted = !File.Exists(path);
                Require(_registrationInputDeleted, "new-ipc-input-deleted");
                _armed = true;
                Snapshot("armed-registration-masked");
                break;
            case "prime":
                Prime(); break;
            case "observe": Snapshot("explicit-observation"); break;
            case "seal":
                Require(_primeConsumed && !_ads.IsConsentBusy && _sdkUpdateCallbacks == 1 &&
                    _sdkShows == _sdkShowCallbacks && _sdkUpdates == 1, "seal-after-real-callbacks-only");
                RequirePreservedFiles(); _warmAllowed = false; _finished = true; Snapshot("sealed"); break;
        }
    }
    private void Prime()
    {
        Require(_armed && !_primeConsumed && !_factoryConsumed && _registration != null &&
            Field<object>(_ads, "_privacyConsent") == null && !_ads.IsConsentBusy, "explicit-prime-once");
        BindOriginalUi();
        Require(AgeTreatmentPolicy.TryCreatePlan(AgeChoice.Adult, out var plan) && !plan.UmpUnderAgeOfConsent, "synthetic-adult-plan");
        _primeConsumed = true; // Static latch survives manager/fixture object recreation.
        var begin = typeof(GoogleAdMobManager).GetMethod("BeginPrivacyRefresh", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(AgeTreatmentPlan), typeof(Func<IAdConsentClient>), typeof(Action<string>) }, null);
        Require(begin != null, "private-begin-signature");
        Func<IAdConsentClient> factory = () =>
        {
            Require(!_factoryConsumed, "single-real-client-factory"); _factoryConsumed = true;
            string registered = _registration; _registration = null;
            _client = new BoundedClient(this, new GoogleUmpConsentClient(DebugGeography.EEA, registered,
                (value, at) => _observations.Enqueue(() => SdkObservation(value, at))));
            return _client;
        };
        try { begin.Invoke(_ads, new object[] { plan, factory, (Action<string>)(value => _observations.Enqueue(() => Snapshot("gate-" + value))) }); }
        catch (TargetInvocationException error) { Fail("private-prime-inner", error.InnerException ?? error); throw; }
        _gate = Field<AdConsentGate>(_ads, "_privacyConsent");
        Require(_factoryConsumed && _client != null && _client.Updates == 1 && _gate != null &&
            _gate.IsPrivacyOnly && _gate.IsPrivacyRefreshOwner, "real-owner-created");
        Snapshot("explicit-prime-started");
    }
    private sealed class BoundedClient : IAdConsentClient, IAdPrivacyStatusClient
    {
        private readonly RevivalPrivacyProductUmpHarness owner;
        private readonly GoogleUmpConsentClient real;
        public int Updates, Shows, Gathers;
        private void Guard(Action action, string stage)
        {
            try { action(); }
            catch (Exception error) { owner.Fail(stage, error); throw; }
        }
        public BoundedClient(RevivalPrivacyProductUmpHarness owner, GoogleUmpConsentClient real) { this.owner = owner; this.real = real; }
        public bool CanRequestAds => real.CanRequestAds;
        public bool PrivacyOptionsRequired => real.PrivacyOptionsRequired;
        public AdPrivacyRequirement PrivacyRequirement
        {
            get
            {
                var status = real.PrivacyRequirement; // Exactly the requested SDK read, no extra read.
                owner._freshRequired = status == AdPrivacyRequirement.Required;
                return status;
            }
        }
        public void Update(bool underAgeOfConsent, Action<bool> completed)
        {
            Guard(() => {
                owner.RequireContext(true);
                Require(!underAgeOfConsent && Interlocked.Increment(ref Updates) == 1, "update-cap-one");
            }, "update-boundary");
            real.Update(underAgeOfConsent, success =>
            {
                owner._freshUpdate = success; // Forward the unmodified real callback before gate dispatch.
                completed(success);
            });
        }
        public void Gather(Action<bool> completed)
        { Interlocked.Increment(ref Gathers); owner.Fail("forbidden-gather", new InvalidOperationException()); throw new InvalidOperationException("Gather excluded."); }
        public void ShowPrivacyOptions(Action<bool> completed)
        {
            Guard(() => {
                owner.RequireContext(true);
                int number = Interlocked.Increment(ref Shows);
                Require(number <= 2 && owner._freshUpdate && owner._freshRequired &&
                    (number == 1 || (owner._warmAllowed && owner._firstFailed && owner._privacyClicks == 0 &&
                    owner._view != null && owner._view.gameObject.activeInHierarchy)), "show-cap-and-real-warm-boundary");
            }, "show-boundary");
            real.ShowPrivacyOptions(completed);
        }
    }
    private void SdkObservation(string value, double at)
    {
        int marker = value.LastIndexOf(" sdk_sequence=", StringComparison.Ordinal);
        long sequence = 0;
        Require(marker > 0 && long.TryParse(value.Substring(marker + 14), out sequence) && sequence > _sdkSequence, "sdk-sequence-monotonic");
        _sdkSequence = sequence;
        _sdk.Add(new { value, receivedAt = at, pid = Pid });
        if (value.StartsWith("update_call ", StringComparison.Ordinal)) Require(++_sdkUpdates == 1, "actual-update-cap");
        else if (value.StartsWith("update_callback ", StringComparison.Ordinal))
        { Require(++_sdkUpdateCallbacks == 1 && _sdkUpdates == 1, "actual-update-callback"); _freshUpdate = value.Contains("ok=True"); }
        else if (value.StartsWith("status_read ", StringComparison.Ordinal))
        { Require(_freshUpdate, "fresh-status-after-real-update"); }
        else if (value.StartsWith("show_call ", StringComparison.Ordinal)) Require(++_sdkShows <= 2 && _freshRequired, "actual-show-cap");
        else if (value.StartsWith("show_callback ", StringComparison.Ordinal))
        {
            Require(++_sdkShowCallbacks <= 2 && _sdkShowCallbacks == _sdkShows, "actual-show-callback");
            _lastShowSucceeded = value.Contains("ok=True");
            int code = value.IndexOf(" code=", StringComparison.Ordinal);
            int end = value.IndexOf(' ', code + 6);
            Require(code >= 0 && int.TryParse(value.Substring(code + 6, end - code - 6), out _lastErrorCode), "actual-error-code");
        }
        else if (value.Contains("exception") || value.StartsWith("unexpected_gather", StringComparison.Ordinal))
        { Snapshot("actual-sdk-exception"); Fail("actual-sdk-exception", new InvalidOperationException(value)); return; }
        Snapshot("sdk-observation");
    }
    private void ObserveUi()
    {
        if (!_bound || !_primeConsumed) return;
        Require(_view != null && _view.gameObject.activeInHierarchy && _ageClicks == 0, "same-original-view");
        if (_sdkShows == 1 && _sdkShowCallbacks == 1 && !_ads.IsConsentBusy &&
            _ads.PrivacySettingsResult == AdPrivacyResult.FormFailed && _freshRequired && !_firstFailed)
        {
            if (!_privacyButton.IsInteractable() || !_ageButton.IsInteractable()) return;
            _firstFailed = true; _warmAllowed = true; Snapshot("first-real-form-failed-warm-ready");
        }
        if (Time.realtimeSinceStartup < _nextSnapshot) return;
        _nextSnapshot = Time.realtimeSinceStartup + .2f;
        string state = _ads.PrivacySettingsResult + "|" + _ads.IsConsentBusy + "|" + _status.text + "|" +
            _privacyButton.IsInteractable() + "|" + _ageButton.IsInteractable() + "|" + _sdkShows + "|" + _sdkShowCallbacks;
        if (_lastState == state) return;
        _lastState = state;
        Snapshot(_sdkShowCallbacks == 2 && !_ads.IsConsentBusy ? "second-real-terminal-ui" : "original-ui-state");
    }
    private static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    private static string RaycastTop(Button button)
    {
        if (EventSystem.current == null) return "no-event-system";
        Rect screen = ScreenRect((RectTransform)button.transform);
        var pointer = new PointerEventData(EventSystem.current) { position = screen.center };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0) return "none";
        return hits[0].gameObject.transform.IsChildOf(button.transform) || hits[0].gameObject == button.gameObject ? "original-button" : hits[0].gameObject.name;
    }
    private void RequirePreservedFiles()
    {
        Require(_preferencesHash == PreferenceHash(), "all-protected-preferences-unchanged");
        Require(ExistingFiles().Count == _preservedFiles.Count &&
            _preservedFiles.All(entry => File.Exists(entry.Key) && HashFile(entry.Key) == entry.Value), "all-existing-files-unchanged");
    }
    private void Snapshot(string stage)
    {
        bool ui = _bound && _status != null && _privacyButton != null && _ageButton != null;
        if (ui) { Canvas.ForceUpdateCanvases(); _status.ForceMeshUpdate(); }
        var rect = ui ? ScreenRect(_status.rectTransform) : new Rect();
        var privacyRect = ui ? ScreenRect((RectTransform)_privacyButton.transform) : new Rect();
        var ageRect = ui ? ScreenRect((RectTransform)_ageButton.transform) : new Rect();
        bool Fits(Rect value) => value.width > 0 && value.height > 0 && value.xMin >= -.5f && value.yMin >= -.5f &&
            value.xMax <= Screen.width + .5f && value.yMax <= Screen.height + .5f;
        var textSize = ui ? _status.GetPreferredValues(_status.text, _status.rectTransform.rect.width, float.PositiveInfinity) : Vector2.zero;
        var snapshot = new Dictionary<string, object>
        {
            ["schema"] = 1,
            ["stage"] = stage,
            ["sequence"] = ++_snapshotSequence,
            ["pid"] = Pid,
            ["token"] = Token,
            ["packageId"] = Application.identifier,
            ["versionCode"] = 29,
            ["commandSequence"] = _commandSequence,
            ["lastCommand"] = _lastCommand,
            ["syntheticAdultPlanOnly"] = true,
            ["ordinaryColdFirstButtonTest"] = false,
            ["realSdkUsed"] = _factoryConsumed,
            ["registrationMasked"] = _armed,
            ["registrationReceiptSha256"] = _registrationReceiptSha256,
            ["registrationInputZeroed"] = _registrationInputZeroed,
            ["registrationInputDeleted"] = _registrationInputDeleted,
            ["registrationInputBytes"] = _registrationInputBytes,
            ["registrationMaskedLength"] = _registrationMaskedLength,
            ["sourceAndApkBinding"] = "external-protected-runner-required",
            ["savedDeclinedUnchanged"] = PreferenceHash() == _preferencesHash,
            ["coldSyntheticNoAdsPreserved"] = PlayerPrefs.GetString(ColdPurchaseKey, "") == "ProductNoAds",
            ["actualManagersNoAds"] = _ads != null && _ads.HasNoAds,
            ["memoryAccount"] = _data != null && _data.PlayFabId == Account,
            ["memoryServer"] = _data != null && _server != null && _managers != null && ReferenceEquals(Managers.Instance, _managers) &&
                ReferenceEquals(_server, Managers.ServerM),
            ["actualMain"] = UnitySceneManager.GetActiveScene().name == "Main",
            ["managerId"] = _ads == null ? 0 : _ads.GetInstanceID(),
            ["managerBusy"] = _ads != null && _ads.IsConsentBusy,
            ["result"] = _ads == null ? "none" : _ads.PrivacySettingsResult.ToString(),
            ["sameRetainedOwner"] = _gate != null && ReferenceEquals(_gate, Field<AdConsentGate>(_ads, "_privacyConsent")),
            ["actualUpdates"] = _sdkUpdates,
            ["actualUpdateCallbacks"] = _sdkUpdateCallbacks,
            ["actualShows"] = _sdkShows,
            ["actualShowCallbacks"] = _sdkShowCallbacks,
            ["lastShowSucceeded"] = _lastShowSucceeded,
            ["actualErrorCode"] = _lastErrorCode,
            ["wrapperUpdateAttempts"] = _client == null ? 0 : _client.Updates,
            ["wrapperShowAttempts"] = _client == null ? 0 : _client.Shows,
            ["gathers"] = _client == null ? 0 : _client.Gathers,
            ["managedAdCalls"] = _managedCalls,
            ["privacyClickEvents"] = _privacyClicks,
            ["ageClickEvents"] = _ageClicks,
            ["warmAllowed"] = _warmAllowed,
            ["originalUi"] = ui,
            ["statusText"] = ui ? _status.text : null,
            ["overflow"] = ui && _status.isTextOverflowing,
            ["preferredHeight"] = textSize.y,
            ["statusHeight"] = ui ? _status.rectTransform.rect.height : 0,
            ["fullTextAndButtonsFit"] = ui && !_status.isTextOverflowing && textSize.y <= _status.rectTransform.rect.height + .5f &&
                Fits(rect) && Fits(privacyRect) && Fits(ageRect),
            ["statusScreenRect"] = new[] { rect.xMin, rect.yMin, rect.xMax, rect.yMax },
            ["privacyButtonScreenRect"] = new[] { privacyRect.xMin, privacyRect.yMin, privacyRect.xMax, privacyRect.yMax },
            ["ageButtonScreenRect"] = new[] { ageRect.xMin, ageRect.yMin, ageRect.xMax, ageRect.yMax },
            ["screenWidth"] = Screen.width,
            ["screenHeight"] = Screen.height,
            ["privacyActive"] = ui && _privacyButton.gameObject.activeInHierarchy,
            ["privacyInteractable"] = ui && _privacyButton.IsInteractable(),
            ["ageActive"] = ui && _ageButton.gameObject.activeInHierarchy,
            ["ageInteractable"] = ui && _ageButton.IsInteractable(),
            ["privacyRaycastTop"] = ui ? RaycastTop(_privacyButton) : null,
            ["ageRaycastTop"] = ui ? RaycastTop(_ageButton) : null,
            ["sdk"] = _sdk.ToArray(),
            ["productionAds"] = AdRequestPolicy.ProductionAdsEnabled,
            ["reviewsDisabled"] = EffectiveReviewsDisabled,
            ["privatePrivacyBinding"] = PrivatePrivacyReleaseContract.IsPrivatePrivacyTrial,
            ["sealedRun"] = _finished,
            ["failure"] = _failure,
            ["nativeTouchEvidence"] = "external-only",
            ["networkScope"] = "real-UMP; native-provider-presence-external-verification",
            ["deletionLifecycleRetested"] = false,
            ["iapStoreRetested"] = false,
        };
        string name = "state-" + _snapshotSequence.ToString("D4") + ".json";
        string path = Path.Combine(_directory, name); WriteNew(path, snapshot);
        Debug.Log("PRIVACY_PRODUCT_UMP_FILE sequence=" + _snapshotSequence + " pid=" + Pid + " file=" + name +
            " bytes=" + new FileInfo(path).Length + " sha256=" + HashFile(path));
    }
    private void Fail(string stage, Exception error)
    {
        if (_failure.Length != 0) return;
        _failure = stage + ":" + error.GetType().FullName; _warmAllowed = false; _registration = null;
        try
        {
            WriteNew(Path.Combine(_directory, "failure-" + Guid.NewGuid().ToString("N") + ".private.json"),
                new { schema = 1, pid = Pid, token = Token, stage, exceptionType = error.GetType().FullName,
                    exception = error.ToString(), sdk = _sdk.ToArray(), ordinaryColdFirstButtonTest = false });
            Snapshot("failed");
        }
        catch (Exception) { }
        Debug.LogError("PRIVACY_PRODUCT_UMP_FAILED stage=" + stage + " type=" + error.GetType().Name + " pid=" + Pid);
    }
    private void OnDestroy()
    {
        _registration = null;
        if (_ads != null) _ads.HarnessEvent -= ManagedEvent;
        if (_privacyButton != null) _privacyButton.onClick.RemoveListener(ObservePrivacyClick);
        if (_ageButton != null) _ageButton.onClick.RemoveListener(ObserveAgeClick);
    }
}
#endif
