using System;

namespace AD
{
    /// <summary>
    /// 시간 관련 유틸리티 클래스
    /// </summary>
    public static class TimeUtility
    {
        #region 날짜 관련 메서드

        /// <summary>
        /// 다음 날 00:00 반환
        /// </summary>
        public static DateTime GetTomorrowDateTime()
        {
            DateTime tomorrow = DateTime.Now.Date.AddDays(1);
            return tomorrow;
        }

        /// <summary>
        /// 다음 날 (00:00)까지 남은 초 반환
        /// </summary>
        public static double GetSecondsUntilTomorrow()
        {
            return (GetTomorrowDateTime() - DateTime.Now).TotalSeconds;
        }

        /// <summary>
        /// 특정 날짜에서 days 만큼 추가
        /// </summary>
        public static DateTime AddDaysToDateTime(DateTime date, int days)
        {
            return date.AddDays(days);
        }

        /// <summary>
        /// 특정 날짜까지 남은 초 반환
        /// </summary>
        public static double GetSecondsUntilDateTime(DateTime targetDate)
        {
            return (targetDate - DateTime.Now).TotalSeconds;
        }

        /// <summary>
        /// 다음 정각 (1시간 후) 반환
        /// </summary>
        public static DateTime GetNextHourDateTime()
        {
            DateTime nextHour = DateTime.Now.AddHours(1);
            return new DateTime(nextHour.Year, nextHour.Month, nextHour.Day, nextHour.Hour, 0, 0);
        }

        /// <summary>
        /// 다음 정각까지 남은 초 반환
        /// </summary>
        public static double GetSecondsUntilNextHour()
        {
            return (GetNextHourDateTime() - DateTime.Now).TotalSeconds;
        }

        #endregion

        #region 시간 형식 변환

        /// <summary>
        /// 초 단위의 시간을 문자열로 변환
        /// </summary>
        public static string FormatTimeString(object time, bool padZero = false, bool includeSeconds = false, bool useColon = false)
        {
            if (!double.TryParse(time.ToString(), out double totalSeconds))
            {
                DebugLogger.LogParseError("TimeUtility", time.ToString());
                return "Invalid Time";
            }

            int hours = (int)(totalSeconds / 3600);
            int minutes = (int)((totalSeconds % 3600) / 60);
            int seconds = (int)(totalSeconds % 60);

            if (padZero)
            {
                return useColon
                    ? $"{hours:D2}:{minutes:D2}{(includeSeconds ? $":{seconds:D2}" : "")}"
                    : $"{hours}시간 {minutes}분{(includeSeconds ? $" {seconds}초" : "")}";
            }
            else
            {
                return useColon
                    ? $"{(hours > 0 ? $"{hours}:" : "")}{minutes}{(includeSeconds ? $":{seconds}" : "")}"
                    : $"{(hours > 0 ? $"{hours}시간 " : "")}{minutes}분{(includeSeconds ? $" {seconds}초" : "")}";
            }
        }

        #endregion

        #region 출석 보상 체크

        /// <summary>
        /// 일일 출석 보상 체크 (마지막 로그인 기준 하루가 지났는지)
        /// </summary>
        public static bool IsDailyRewardAvailable(DateTime now, DateTime lastLogin)
        {
            return now.Date > lastLogin.Date;
        }

        /// <summary>
        /// 특정 날짜와 비교하여 갱신이 필요한지 체크
        /// </summary>
        public static bool IsUpdateRequired(DateTime now, DateTime target)
        {
            return (now - target).TotalDays >= 0;
        }

        /// <summary>
        /// 광고 보상 갱신 체크 (마지막 광고 시청 후 1시간이 지났는지)
        /// </summary>
        public static bool IsAdRewardAvailable(DateTime now, DateTime lastAdTime)
        {
            return (now - lastAdTime).TotalHours >= 1;
        }

        #endregion

        #region 광고 보상 버프

        /// <summary>
        /// Google AdMob 보상 광고 시청 후 버프 남은 시간 반환 (10분 기준)
        /// </summary>
        public static double GetAdBuffRemainingTime(string lastAdTime)
        {
            if (!DateTime.TryParse(lastAdTime, out DateTime parsedTime))
            {
                DebugLogger.LogParseError("TimeUtility", lastAdTime);
                return 0;
            }

            return (parsedTime.AddMinutes(10) - DateTime.Now).TotalSeconds;
        }

        #endregion
    }
}
