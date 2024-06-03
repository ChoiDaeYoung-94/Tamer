#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public static Managers Instance { get { return instance; } }

        [SerializeField] DataManager _dataM = null;
        public static DataManager DataM { get { return instance._dataM; } }

        PoolManager _poolM = new PoolManager();
        public static PoolManager PoolM { get { return instance._poolM; } }

        [SerializeField] PopupManager _popupM = null;
        public static PopupManager PopupM { get { return instance._popupM; } }

        ResourceManager _resourceM = new ResourceManager();
        public static ResourceManager ResourceM { get { return instance._resourceM; } }

        [SerializeField] SceneManager _sceneM = null;
        public static SceneManager SceneM { get { return instance._sceneM; } }

        ServerManager _serverM = new ServerManager();
        public static ServerManager ServerM { get { return instance._serverM; } }

        UpdateManager _updateM = new UpdateManager();
        public static UpdateManager UpdateM { get { return instance._updateM; } }

        GameManager _gameM = new GameManager();
        public static GameManager GameM { get { return instance._gameM; } }

        [SerializeField] GoogleAdMobManager _googleAdMobM = null;
        public static GoogleAdMobManager GoogleAdMobM { get { return instance._googleAdMobM; } }

        [Header("--- 미리 가지고 있어야 할 data ---")]
        [Tooltip("Pool에 사용할 GameObject")]
        public GameObject[] _go_poolGOs = null;
        [Tooltip("Pool에 사용할 UI")]
        public GameObject[] _go_poolUIs = null;

        private void Awake()
        {
            Init();
        }

        void Init()
        {
            instance = this;
            DontDestroyOnLoad(this);

            InitM();
        }

        private void OnDestroy()
        {
            instance = null;
        }

        private void Update()
        {
            _updateM.OnUpdate();
        }

        /// <summary>
        /// 추후 다른 씬 특히 QA 전용 씬을 만들던지 할 때
        /// flow를 대비하여
        /// </summary>
        private void InitM()
        {
            DataM.Init();
            PoolM.Init();
            PopupM.Init();
            GoogleAdMobM.Init();
        }

        /// <summary>
        /// 씬 전환 시 필요에 의하면 클리어
        /// </summary>
        public void Clear()
        {
            UpdateM.Clear();
            //PoolM.Clear();
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(Managers))]
        public class customEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox("초기 메니저 세팅", MessageType.Info);

                base.OnInspectorGUI();
            }
        }
#endif
    }
}
