using UnityEngine;

using MiniJSON;

namespace AD
{
    /// <summary>
    /// 애매한 기능 묶기 위함
    /// </summary>
    public class Utils : MonoBehaviour
    {
        #region MiniJson
        public static string ObjectToJson(object obj)
        {
            return Json.Serialize(obj);
        }

        public static object JsonToObject(string JsonData)
        {
            return Json.Deserialize(JsonData);
        }
        #endregion

        #region etc
        /// <summary>
        /// 컴포넌트를 Get할건데 없으면 Add하고 Get
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <returns></returns>
        public static T GetComponent_<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();

            return component;
        }

        public static int Plus(int n, int result)
        {
            while (--n != 0)
            {
                result += n;
            }

            return result;
        }
        #endregion
    }
}
