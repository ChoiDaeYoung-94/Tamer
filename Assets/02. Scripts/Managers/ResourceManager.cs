using UnityEngine;

namespace AD
{
    /// <summary>
    /// Resources 관리
    /// </summary>
    public class ResourceManager
    {
        public T Load<T>(string where, string path) where T : Object
        {
            T resource = Resources.Load<T>(path);
            if (resource == null)
                AD.DebugLogger.LogLoadError(where, path);

            return resource;
        }

        public GameObject InstantiatePrefab(string where, string path, Transform parent = null)
        {
            GameObject prefab = Load<GameObject>(where, "Prefabs/" + path);
            if (prefab == null)
            {
                AD.DebugLogger.LogInstantiateError(where, path);
                return null;
            }

            return Object.Instantiate(prefab, parent);
        }
    }
}