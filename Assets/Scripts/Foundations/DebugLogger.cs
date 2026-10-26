namespace AD
{
    /// <summary>
    /// 자체 Debug Logger
    /// - UNITY_EDITOR 또는 DEVELOPMENT_BUILD가 정의된 경우에만 호출과 인수 평가를 포함
    /// - 일반 release에서는 호출과 인수 평가를 제외하며 Debug/DEBUG 단독 정의로 활성화되지 않음
    /// System.Diagnostics.Conditional
    /// > https://learn.microsoft.com/ko-kr/dotnet/api/system.diagnostics.conditionalattribute?view=net-7.0
    /// </summary>
    public static class DebugLogger
    {
        /// <summary>
        /// 로그 메시지를 출력
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string where, string message)
        {
            PrintLog("Log", where, message, "cyan");
        }

        /// <summary>
        /// 경고 메시지를 출력
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string where, string message)
        {
            UnityEngine.Debug.LogWarning($"<color=yellow>LogWarning</color> - {where}\n<color=cyan>{message}</color>");
        }

        /// <summary>
        /// 오류 메시지를 출력
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogError(string where, string message)
        {
            UnityEngine.Debug.LogError($"<color=red>LogError</color> - {where}\n<color=cyan>{message}</color>");
        }

        /// <summary>
        /// 데이터 가져오기 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogDataError(string where, string message)
        {
            PrintLog("Data Error", where, $"Failed to get data: {message}", "red");
        }

        /// <summary>
        /// 파싱 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogParseError(string where, string message)
        {
            PrintLog("Parse Error", where, $"Failed to parse: {message}", "red");
        }

        /// <summary>
        /// 특정 값이 존재하지 않을 때 로그
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogNotFound(string where, string item)
        {
            PrintLog("Not Found", where, $"There is no \"{item}\"", "red");
        }

        /// <summary>
        /// 로드 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogLoadError(string where, string path)
        {
            PrintLog("Load Error", where, $"Failed to load - path: {path}", "red");
        }

        /// <summary>
        /// 인스턴스화 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogInstantiateError(string where, string path)
        {
            PrintLog("Instantiate Error", where, $"Failed to instantiate - path: {path}", "red");
        }

        /// <summary>
        /// 내부적으로 로그를 출력하는 공통 메서드
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void PrintLog(string type, string where, string message, string color)
        {
            UnityEngine.Debug.Log($"<color={color}>{type}</color> - {where}\n{message}");
        }
    }
}
