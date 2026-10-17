using System;
using UnityEngine;

namespace AD
{
    /// <summary>
    /// Manager 스크립트 관리
    /// </summary>
    public class Managers : MonoBehaviour
    {
        /// <summary>
        /// Singleton - 객체 오직 1
        /// Manager관련 script 모두 등록
        /// </summary>
        static Managers instance;
        public static Managers Instance { get { return instance != null ? instance : null; } }

        [SerializeField] DataManager _dataM = null;
        public static DataManager DataM { get { return instance != null ? instance._dataM : null; } }

        PoolManager _poolM = new PoolManager();
        public static PoolManager PoolM { get { return instance != null ? instance._poolM : null; } }

        [SerializeField] PopupManager _popupM = null;
        public static PopupManager PopupM { get { return instance != null ? instance._popupM : null; } }

        ResourceManager _resourceM = new ResourceManager();
        public static ResourceManager ResourceM { get { return instance != null ? instance._resourceM : null; } }

        [SerializeField] SceneManager _sceneM = null;
        public static SceneManager SceneM { get { return instance != null ? instance._sceneM : null; } }

        ServerManager _serverM;
        public static ServerManager ServerM { get { return instance != null ? instance._serverM : null; } }

        [SerializeField] UpdateManager _updateM = null;
        public static UpdateManager UpdateM { get { return instance != null ? instance._updateM : null; } }

        GameManager _gameM = new GameManager();
        public static GameManager GameM { get { return instance != null ? instance._gameM : null; } }

        [SerializeField] GoogleAdMobManager _googleAdMobM = null;
        public static GoogleAdMobManager GoogleAdMobM { get { return instance != null ? instance._googleAdMobM : null; } }

        EquipmentManager _equipmentM = new EquipmentManager();
        public static EquipmentManager EquipmentM { get { return instance != null ? instance._equipmentM : null; } }

        IAPManager _IAPM = new IAPManager();
        public static IAPManager IAPM { get { return instance != null ? instance._IAPM : null; } }

        [SerializeField] SoundManager _soundM = null;
        public static SoundManager SoundM { get { return instance != null ? instance._soundM : null; } }

        [Header("--- 미리 가지고 있어야 할 data ---")]
        [Tooltip("Pool에 사용할 GameObject")]
        public GameObject[] _go_poolGOs = null;
        [Tooltip("Pool에 사용할 UI")]
        public GameObject[] _go_poolUIs = null;

        private bool _ownsServices;
        private bool _initialized;
        private bool _shutdown;

        private void Awake()
        {
#if TAMER_DELETION_HARNESS
            if (Application.identifier != RevivalDeletionTrialHarness.ApplicationId || !Debug.isDebugBuild)
                throw new InvalidOperationException("Isolated deletion trial required.");
            if (!TryClaimInstance()) throw new InvalidOperationException("Duplicate trial owner.");
            _dataM = gameObject.AddComponent<DataManager>();
            _serverM = new ServerManager(_dataM);
            _dataM.InitializeDeletionTrial();
            _initialized = true;
            DontDestroyOnLoad(gameObject);
#else
            Init();
#endif
        }

        private void Start()
        {
#if TAMER_IAP_HARNESS
            return;
#endif
            if (_ownsServices && !_shutdown && _dataM != null && !_dataM.ReceiptOnly && _soundM != null) _soundM.Init();
        }

        // A duplicate component must not replace a live owner or initialize its services.
        private bool TryClaimInstance()
        {
            if (_shutdown || (instance != null && instance != this)) return false;
            instance = this;
            _ownsServices = true;
            return true;
        }

        private void Init()
        {
            if (_initialized || _shutdown) return;
            if (!TryClaimInstance())
            {
                enabled = false;
                if (instance != null && instance.gameObject == gameObject) Destroy(this);
                else
                {
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }
                return;
            }
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            try
            {
#if TAMER_IAP_HARNESS
                RevivalIapIsolation.ValidateRuntime();
                _serverM = RevivalGameplayIsolation.CreateTestServer(_dataM, () => RevivalIapIsolation.Session?.AccountId);
                _IAPM.Dispose();
                _IAPM = RevivalIapIsolation.CreatePurchasing();
                _dataM.InitializeIapRecovery(RevivalIapIsolation.CreateSavePath(Application.persistentDataPath), _serverM, Application.persistentDataPath);
                _initialized = true;
                return; // Recovery-only boot: do not initialize gameplay, ads, pool or periodic writers.
#elif TAMER_GAMEPLAY_HARNESS
                _serverM = RevivalGameplayIsolation.CreateServer(_dataM);
#else
                _serverM = new ServerManager(_dataM);
#endif
                InitM();
                _initialized = true;
            }
            catch
            {
                gameObject.SetActive(false);
                Shutdown();
                Destroy(gameObject);
                throw;
            }
        }

        private void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            if (_ownsServices)
            {
                // Stop writes before invalidating transport callbacks. Preserve the saved file.
                if (_dataM != null) Cleanup(_dataM.Shutdown);
                if (_serverM != null) Cleanup(_serverM.Dispose);
            }
            Cleanup(_IAPM.Dispose);
            if (_ownsServices) Cleanup(_poolM.Dispose);
            if (instance == this) instance = null;
            _ownsServices = false;
        }

        private static void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void OnDestroy() => Shutdown();

#if UNITY_EDITOR || TAMER_IAP_HARNESS
        internal bool CanPrepareIapRecovery => _ownsServices && _initialized && !_shutdown &&
            _dataM != null && _dataM.CanPrepareIapRecovery;

        internal void CompleteIapRecovery(string path, AD.Purchasing.ReceiptSession session, bool existing)
        {
            if (!CanPrepareIapRecovery || session == null || !session.IsValid)
                throw new InvalidOperationException("Inactive fresh recovery host required.");
            var defaults = _dataM.IapRecoveryDefaults();
            if (existing)
            {
                if (path != RevivalIapRecoveryProfile.Selected(Application.persistentDataPath,
                    RevivalIapRecoveryProfile.TestTitle, session.AccountId)) throw new InvalidOperationException();
            }
            else RevivalIapRecoveryProfile.ValidatePrepared(Application.persistentDataPath,
                    RevivalIapRecoveryProfile.TestTitle, session.AccountId, path);
            var oldData = _dataM;
            oldData.Shutdown();
            _serverM.Dispose();
            // Previous data is never rebound or reused. Its original files stay in place.
            _dataM = gameObject.AddComponent<DataManager>();
            _serverM = RevivalGameplayIsolation.CreateTestServer(_dataM, () => session.AccountId, defaults);
            try
            {
                _dataM.InitializeIapRecovery(path, _serverM, Application.persistentDataPath);
                _dataM.BeginAccountSession(session.AccountId);
                _serverM.GetAllData(update: true);
                if (!_dataM.IsServerDataReady || _dataM.PlayFabId != session.AccountId)
                    throw new InvalidOperationException("Recovery profile could not bind.");
            }
            catch
            {
                _dataM.Shutdown(); _serverM.Dispose();
                throw;
            }
            finally { Destroy(oldData); }
        }
#endif

        /// <summary>
        /// 추후 다른 씬 특히 QA 전용 씬을 만들던지 할 때
        /// flow를 대비하여
        /// </summary>
        private void InitM()
        {
            DataM.InitializeData();
            if (DataM.ReceiptOnly) return; // Preserve receipt bootstrap ownership; no game services/ads/pool.
            PoolM.Init();
            PopupM.Init();
            GoogleAdMobM.Init();
        }
    }
}
