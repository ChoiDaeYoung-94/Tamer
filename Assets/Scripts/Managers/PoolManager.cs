using System.Collections.Generic;
using UnityEngine;

namespace AD
{
    /// <summary>
    /// 풀 관리 클래스
    /// 다양한 GameObject/UI 풀을 생성 및 관리하며 재사용성을 높임
    /// </summary>
    public class PoolManager : System.IDisposable
    {
        #region Nested Pool Class

        /// <summary>
        /// 개별 풀을 관리
        /// 각 풀은 대상 프리팹을 기반으로 오브젝트를 미리 생성하여 스택으로 관리
        /// </summary>
        public class Pool : System.IDisposable
        {
            /// <summary>
            /// 풀에서 생성할 대상 프리팹
            /// </summary>
            public GameObject TargetPrefab { get; private set; }

            /// <summary>
            /// 풀 오브젝트들을 보관할 root Transform
            /// </summary>
            public Transform Root { get; set; }

            /// <summary>
            /// 이 풀이 게임 오브젝트용 풀인지 UI용 풀인지를 결정
            /// true이면 게임 오브젝트, false이면 UI (Canvas 추가)
            /// </summary>
            public bool IsGameObjectPool = false;

            // 풀 오브젝트를 저장하는 스택
            private Stack<PoolObject> _poolStack = new Stack<PoolObject>();
            private HashSet<PoolObject> _storedObjects = new HashSet<PoolObject>();
            private readonly HashSet<PoolObject> _createdObjects = new HashSet<PoolObject>();
            private bool _disposed;

            /// <summary>
            /// Pool 생성 시 Init
            /// 대상 프리팹을 설정하고, 지정된 수만큼 오브젝트를 생성하여 스택에 추가
            /// </summary>
            public void Init(GameObject prefab, int count)
            {
                TargetPrefab = prefab;

                // 풀 루트 생성
                GameObject rootObject = new GameObject(prefab.name);
                if (!IsGameObjectPool)
                {
                    // UI 풀인 경우 캔버스를 추가하여 UI 요소가 올바르게 렌더링되도록 함
                    rootObject.AddComponent<Canvas>();
                }
                Root = rootObject.transform;

                // 지정된 개수만큼 오브젝트 생성 및 스택에 Push
                for (int i = 0; i < count; i++)
                {
                    PoolObject poolObj = CreatePoolObject();
                    PushToPool(poolObj);
                }
            }

            /// <summary>
            /// 대상 프리팹을 기반으로 새로운 풀 오브젝트를 생성
            /// </summary>
            private PoolObject CreatePoolObject()
            {
                GameObject newObj = Object.Instantiate(TargetPrefab);
                newObj.name = TargetPrefab.name;
                PoolObject poolObj = newObj.GetOrAddComponent<PoolObject>();
                _createdObjects.Add(poolObj);
                return poolObj;
            }

            /// <summary>
            /// 사용한 풀 오브젝트를 비활성화한 후 스택에 반환
            /// </summary>
            public void PushToPool(PoolObject poolObj)
            {
                if (_disposed || poolObj == null) return;
                // Register before parenting/deactivation, which can re-enter through OnDisable.
                if (!_storedObjects.Add(poolObj))
                    return;

                poolObj.transform.SetParent(Root);
                poolObj.gameObject.SetActive(false);

                _poolStack.Push(poolObj);
            }

            /// <summary>
            /// 풀에서 오브젝트를 하나 꺼내 활성화한 후 지정된 부모 하위로 이동
            /// 스택이 비어있으면 새로운 오브젝트를 생성
            /// </summary>
            public GameObject PopFromPool(Transform parent)
            {
                if (_disposed) return null;
                while (_poolStack.Count > 0 && _poolStack.Peek() == null)
                    _storedObjects.Remove(_poolStack.Pop());
                PoolObject poolObj = _poolStack.Count > 0 ? _poolStack.Pop() : CreatePoolObject();
                _storedObjects.Remove(poolObj);
                poolObj.gameObject.SetActive(true);

                if (parent == null)
                {
                    parent = MonsterGenerator.Instance != null ? MonsterGenerator.Instance.transform : null;
                }
                poolObj.transform.SetParent(parent);

                return poolObj.gameObject;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                var objects = new List<PoolObject>(_createdObjects);
                _createdObjects.Clear();
                _storedObjects.Clear();
                _poolStack.Clear();
                foreach (var item in objects)
                    if (item != null) DestroyOwned(item.gameObject);
                if (Root != null) DestroyOwned(Root.gameObject);
                Root = null;
                TargetPrefab = null;
            }
        }
        #endregion

        /// <summary>
        /// 풀들을 관리하는 Dictionary, 키는 대상 프리팹의 이름으로 사용
        /// </summary>
        public Dictionary<string, Pool> PoolDictionary = new Dictionary<string, Pool>();

        /// <summary>
        /// 게임 오브젝트 풀의 루트 Transform.
        /// </summary>
        public Transform RootGameObjects;

        /// <summary>
        /// UI 풀의 루트 Transform.
        /// </summary>
        public Transform RootUI;

        /// <summary>
        /// 플레이어가 사용하는 오브젝트의 루트 Transform.
        /// </summary>
        public Transform RootPlayer;
        private readonly List<GameObject> _ownedRoots = new List<GameObject>();
        private bool _initialized;
        private bool _disposed;
        private bool _clearing;

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 각 타입별 루트를 생성하고 미리 지정된 풀 오브젝트들을 생성
        /// </summary>
        public void Init()
        {
            if (_disposed || _initialized) return;
            _initialized = true;
            // 게임 오브젝트 풀 루트 생성
            RootGameObjects = new GameObject("Pool_GO").transform;
            _ownedRoots.Add(RootGameObjects.gameObject);
            Object.DontDestroyOnLoad(RootGameObjects.gameObject);

            // UI 풀 루트 생성
            RootUI = new GameObject("Pool_UI").transform;
            _ownedRoots.Add(RootUI.gameObject);
            Object.DontDestroyOnLoad(RootUI.gameObject);

            // 플레이어 관련 풀 루트 생성
            RootPlayer = new GameObject("Pool_Player").transform;
            _ownedRoots.Add(RootPlayer.gameObject);
            Object.DontDestroyOnLoad(RootPlayer.gameObject);

            // Managers.Instance._go_poolGOs 배열에 있는 모든 GameObject에 대해 풀 생성 (기본 20개)
            for (int i = 0; i < Managers.Instance._go_poolGOs.Length; i++)
            {
                CreatePool(Managers.Instance._go_poolGOs[i], isGameObjectPool: true, count: 20);
            }

            // Managers.Instance._go_poolUIs 배열에 있는 모든 GameObject에 대해 풀 생성 (기본 50개)
            for (int i = 0; i < Managers.Instance._go_poolUIs.Length; i++)
            {
                CreatePool(Managers.Instance._go_poolUIs[i], isGameObjectPool: false, count: 50);
            }
        }

        /// <summary>
        /// 특정 프리팹에 대해 풀을 생성하고 등록
        /// </summary>
        public void CreatePool(GameObject prefab, bool isGameObjectPool = true, int count = 20)
        {
            if (_disposed || _clearing) return;
            if (prefab == null)
            {
                AD.DebugLogger.LogError("PoolManager", "Prefab is null when creating pool.");
                return;
            }

            if (PoolDictionary.ContainsKey(prefab.name))
            {
                AD.DebugLogger.LogError("PoolManager", $"Pool for {prefab.name} already exists.");
                return;
            }

            Pool pool = new Pool
            {
                IsGameObjectPool = isGameObjectPool
            };
            pool.Init(prefab, count);

            // 각 풀의 루트는 해당 풀의 타입에 맞는 상위 루트로 설정
            Transform rootParent = isGameObjectPool ? RootGameObjects : RootUI;
            pool.Root.SetParent(rootParent);

            PoolDictionary.Add(prefab.name, pool);
        }

        /// <summary>
        /// 사용 완료한 풀 오브젝트를 해당 풀에 다시 반환
        /// 반환할 GameObject에 PoolObject 컴포넌트가 없으면 오브젝트를 파괴
        /// </summary>
        public void PushToPool(GameObject go)
        {
            if (_disposed || _clearing) return;
            if (go == null)
                return;

            PoolObject poolObj = go.GetComponent<PoolObject>();
            if (poolObj == null)
            {
                Object.Destroy(go);
                return;
            }

            // 생성 시 사용한 프리팹 이름을 키로 사용
            if (!PoolDictionary.ContainsKey(go.name))
            {
                Object.Destroy(go);
                return;
            }

            PoolDictionary[go.name].PushToPool(poolObj);
        }

        /// <summary>
        /// 지정한 풀 이름에 해당하는 풀에서 오브젝트를 하나 꺼냄
        /// </summary>
        public GameObject PopFromPool(string poolName, Transform parent = null)
        {
            if (_disposed || _clearing) return null;
            if (!PoolDictionary.ContainsKey(poolName))
            {
                AD.DebugLogger.LogNotFound("PoolManager", $"{poolName} not found in pool dictionary.");
                return null;
            }

            return PoolDictionary[poolName].PopFromPool(parent);
        }

        /// <summary>
        /// 모든 게임 오브젝트 풀의 자식들을 제거하고, 풀 딕셔너리를 초기화
        /// </summary>
        public void Clear()
        {
            if (_clearing) return;
            _clearing = true;
            var pools = new List<Pool>(PoolDictionary.Values);
            PoolDictionary.Clear();
            try { foreach (var pool in pools) pool.Dispose(); }
            finally { _clearing = false; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
            foreach (var root in _ownedRoots)
                if (root != null) DestroyOwned(root);
            _ownedRoots.Clear();
            RootGameObjects = RootUI = RootPlayer = null;
        }

        private static void DestroyOwned(GameObject target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
