using UnityEngine;

using MiniJSON;

namespace AD
{
    /// <summary>
    /// 유틸리티 기능 제공
    /// </summary>
    public static class Utility
    {
        #region JSON handling

        /// <summary>
        /// 객체를 JSON 문자열로 변환
        /// </summary>
        public static string SerializeToJson(object obj)
        {
            return Json.Serialize(obj);
        }

        /// <summary>
        /// JSON 문자열을 객체로 변환
        /// </summary>
        public static object DeserializeFromJson(string jsonData)
        {
            return Json.Deserialize(jsonData);
        }

        #endregion

        #region Component handling

        /// <summary>
        /// GameObject에서 특정 컴포넌트를 가져오거나, 없으면 추가 후 반환
        /// </summary>
        public static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject == null)
            {
                AD.DebugLogger.LogError("Utility", "GetOrAddComponent: gameObject is null.");
                return null;
            }

            T component = gameObject.GetComponent<T>();
            return component ?? gameObject.AddComponent<T>();
        }

        #endregion

        #region Sorting

        /// <summary>
        /// 몬스터 정렬을 위한 보정값을 반환 (등차수열 공식 적용)
        /// </summary>
        public static int GetSortedMonsterCount(int n, int result)
        {
            if (n <= 0) return result;
            return result + ((n * (n - 1)) / 2);
        }

        #endregion
    }
}
