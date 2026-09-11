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
            Init();
        }

        private void Start()
        {
            if (_ownsServices && !_shutdown && _soundM != null) _soundM.Init();
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
            DontDestroyOnLoad(gameObject);
            try
            {
#if TAMER_IAP_HARNESS
                RevivalIapIsolation.ValidateRuntime();
                _serverM = RevivalGameplayIsolation.CreateTestServer(_dataM, () => RevivalIapIsolation.Session?.AccountId);
                _IAPM.Dispose();
                _IAPM = RevivalIapIsolation.CreatePurchasing();
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

        /// <summary>
        /// 추후 다른 씬 특히 QA 전용 씬을 만들던지 할 때
        /// flow를 대비하여
        /// </summary>
        private void InitM()
        {
            DataM.InitializeData();
            PoolM.Init();
            PopupM.Init();
            GoogleAdMobM.Init();
        }
    }
}
