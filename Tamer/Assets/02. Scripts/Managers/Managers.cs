#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace AD
{
    public class Managers : MonoBehaviour
    {
        /// <summary>
        /// Singleton - 객체 오직 1
        /// Manager관련 script 모두 등록
        /// </summary>
        static Managers instance;
        public static Managers Instance { get { return instance; } }

        DataManager _dataM = new DataManager();
        public static DataManager DataM { get { return instance._dataM; } }

        PoolManager _poolM = new PoolManager();
        public static PoolManager PoolM { get { return instance._poolM; } }

        PopupManager _popupM = new PopupManager();
        public static PopupManager PopupM { get { return instance._popupM; } }

        ResourceManager _resourceM = new ResourceManager();
        public static ResourceManager ResourceM { get { return instance._resourceM; } }

        [SerializeField] SceneManager _sceneM = null;
        public static SceneManager SceneM { get { return instance._sceneM; } }

        ServerManager _serverM = new ServerManager();
        public static ServerManager ServerM { get { return instance._serverM; } }

        UpdateManager _updateM = new UpdateManager();
        public static UpdateManager UpdateM { get { return instance._updateM; } }

        private void Awake()
        {
            Init();
        }

        void Init()
        {
            if (instance == null)
            {
                GameObject go = GameObject.Find("Manager");
                if (go == null)
                {
                    go = new GameObject { name = "Manager" };
                    go.AddComponent<Managers>();
                }

                DontDestroyOnLoad(go);
                instance = go.GetComponent<Managers>();

                InitM();
            }
            else
                Destroy(gameObject);
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
        public void InitM()
        {
            DataM.Init();
            PoolM.Init();
            PopupM.Init();
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
