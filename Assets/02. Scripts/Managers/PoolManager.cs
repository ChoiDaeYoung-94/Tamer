using System.Collections.Generic;
using UnityEngine;

namespace AD
{
    /// <summary>
    /// 풀 관리 클래스
    /// 다양한 GameObject/UI 풀을 생성 및 관리하며 재사용성을 높임
    /// </summary>
    public class PoolManager
    {
        #region Nested Pool Class

        /// <summary>
        /// 개별 풀을 관리
        /// 각 풀은 대상 프리팹을 기반으로 오브젝트를 미리 생성하여 스택으로 관리
        /// </summary>
        public class Pool
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
                return poolObj;
            }

            /// <summary>
            /// 사용한 풀 오브젝트를 비활성화한 후 스택에 반환
            /// </summary>
            public void PushToPool(PoolObject poolObj)
            {
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
                PoolObject poolObj = _poolStack.Count > 0 ? _poolStack.Pop() : CreatePoolObject();
                poolObj.gameObject.SetActive(true);

                if (parent == null)
                {
                    GameObject activePoolObj = GameObject.Find(AD.GameConstants.ActivePool);
                    parent = activePoolObj != null ? activePoolObj.transform : null;
                }
                poolObj.transform.SetParent(parent);

                return poolObj.gameObject;
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

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 각 타입별 루트를 생성하고 미리 지정된 풀 오브젝트들을 생성
        /// </summary>
        public void Init()
        {
            // 게임 오브젝트 풀 루트 생성
            RootGameObjects = new GameObject("Pool_GO").transform;
            Object.DontDestroyOnLoad(RootGameObjects.gameObject);

            // UI 풀 루트 생성
            RootUI = new GameObject("Pool_UI").transform;
            Object.DontDestroyOnLoad(RootUI.gameObject);

            // 플레이어 관련 풀 루트 생성
            RootPlayer = new GameObject("Pool_Player").transform;
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
            if (prefab == null)
            {
                AD.DebugLogger.LogError("PoolManager", "Prefab is null when creating pool.");
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

            if (!PoolDictionary.ContainsKey(prefab.name))
            {
                PoolDictionary.Add(prefab.name, pool);
            }
            else
            {
                AD.DebugLogger.LogError("PoolManager", $"Pool for {prefab.name} already exists.");
            }
        }

        /// <summary>
        /// 사용 완료한 풀 오브젝트를 해당 풀에 다시 반환
        /// 반환할 GameObject에 PoolObject 컴포넌트가 없으면 오브젝트를 파괴
        /// </summary>
        public void PushToPool(GameObject go)
        {
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
            if (RootGameObjects != null)
            {
                foreach (Transform child in RootGameObjects)
                {
                    Object.Destroy(child.gameObject);
                }
            }
            PoolDictionary.Clear();
        }
    }
}