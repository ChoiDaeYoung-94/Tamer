namespace AD
{
    /// <summary>
    /// 자체 Debug
    /// - Define symbol에 Debug이 있을 때 작동
    /// - BuildScript를 참고하면 release build 시 Define symbol에 Debug가 제외 됨
    /// </summary>
    public class Debug
    {
        /// <summary>
        /// System.Diagnostics.Conditional
        /// > https://learn.microsoft.com/ko-kr/dotnet/api/system.diagnostics.conditionalattribute?view=net-7.0
        /// </summary>
        /// <param name="where"></param>
        /// <param name="contents"></param>
        [System.Diagnostics.Conditional("Debug")]
        public static void Log(string where, string contents)
        {
            UnityEngine.MonoBehaviour.print($"Log - {where} \n<color=cyan>{contents}</color>");
        }

        [System.Diagnostics.Conditional("Debug")]
        public static void LogWarning(string where, string contents)
        {
            UnityEngine.Debug.LogWarning($"<color=yellow>LogWarning</color> - {where} \n<color=cyan>{contents}</color>");
        }

        [System.Diagnostics.Conditional("Debug")]
        public static void GetData(string where, string contents)
        {
            UnityEngine.MonoBehaviour.print($"<color=red>Check</color> - {where} \nFailed to GetData : {contents}");
        }

        [System.Diagnostics.Conditional("Debug")]
        public static void Parse(string where, string contents)
        {
            UnityEngine.MonoBehaviour.print($"<color=red>Check</color> - {where} \nFailed to Parse : {contents}");
        }

        [System.Diagnostics.Conditional("Debug")]
        public static void Contain(string where, string contents)
        {
            UnityEngine.MonoBehaviour.print($"<color=red>Check</color> - {where} \nThere is no \"{contents}\"");
        }

        [System.Diagnostics.Conditional("Debug")]
        public static void Load(string where, string path)
        {
            UnityEngine.MonoBehaviour.print($"<color=red>Check</color> - {where} \nFailed to Load - path : {path}");
        }

        [System.Diagnostics.Conditional("Debug")]
        public static void Instantiate(string where, string path)
        {
            UnityEngine.MonoBehaviour.print($"<color=red>Check</color> - {where} \nFailed to Instantiate - path : {path}");
        }
    }
}