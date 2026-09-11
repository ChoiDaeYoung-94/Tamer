namespace AD.Advertising
{
    /// <summary>
    /// Keeps ad requests in explicitly permitted test environments during revival.
    /// </summary>
    public static class AdRequestPolicy
    {
        // Re-enabling production requires a reviewed change after Families verification.
        public const bool ProductionAdsEnabled = false;

        public static bool CanRequestTestAds(
            bool isEditor,
            bool isDevelopment,
            bool explicitTestAds,
            bool isAndroid,
            bool isIos,
            bool isBatchMode)
        {
#if TAMER_GAMEPLAY_HARNESS
            return false;
#else
            return !isBatchMode
                && (isEditor || isDevelopment || explicitTestAds)
                && (isEditor || isAndroid || isIos);
#endif
        }

        public static string TestRewardedAdUnit(bool isIos)
        {
            // Official Google Mobile Ads Unity 9.1.1 rewarded test units.
            return isIos
                ? "ca-app-pub-3940256099942544/1712485313"
                : "ca-app-pub-3940256099942544/5224354917";
        }
    }
}
