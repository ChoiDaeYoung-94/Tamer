namespace AD
{
    /// <summary>
    /// 자체 Debug Logger
    /// - Define symbol에 DEBUG가 있을 때 작동
    /// - BuildScript를 참고하면 release build 시 Define symbol에 DEBUG가 제외됨
    /// System.Diagnostics.Conditional
    /// > https://learn.microsoft.com/ko-kr/dotnet/api/system.diagnostics.conditionalattribute?view=net-7.0
    /// </summary>
    public static class DebugLogger
    {
        /// <summary>
        /// 로그 메시지를 출력
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void Log(string where, string message)
        {
            PrintLog("Log", where, message, "cyan");
        }

        /// <summary>
        /// 경고 메시지를 출력
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void LogWarning(string where, string message)
        {
            UnityEngine.Debug.LogWarning($"<color=yellow>LogWarning</color> - {where}\n<color=cyan>{message}</color>");
        }

        /// <summary>
        /// 오류 메시지를 출력
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void LogError(string where, string message)
        {
            UnityEngine.Debug.LogError($"<color=red>LogError</color> - {where}\n<color=cyan>{message}</color>");
        }

        /// <summary>
        /// 데이터 가져오기 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void LogDataError(string where, string message)
        {
            PrintLog("Data Error", where, $"Failed to get data: {message}", "red");
        }

        /// <summary>
        /// 파싱 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void LogParseError(string where, string message)
        {
            PrintLog("Parse Error", where, $"Failed to parse: {message}", "red");
        }

        /// <summary>
        /// 특정 값이 존재하지 않을 때 로그
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void LogNotFound(string where, string item)
        {
            PrintLog("Not Found", where, $"There is no \"{item}\"", "red");
        }

        /// <summary>
        /// 로드 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void LogLoadError(string where, string path)
        {
            PrintLog("Load Error", where, $"Failed to load - path: {path}", "red");
        }

        /// <summary>
        /// 인스턴스화 실패 로그
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        public static void LogInstantiateError(string where, string path)
        {
            PrintLog("Instantiate Error", where, $"Failed to instantiate - path: {path}", "red");
        }

        /// <summary>
        /// 내부적으로 로그를 출력하는 공통 메서드
        /// </summary>
        [System.Diagnostics.Conditional("Debug")]
        private static void PrintLog(string type, string where, string message, string color)
        {
            UnityEngine.Debug.Log($"<color={color}>{type}</color> - {where}\n{message}");
        }
    }
}
