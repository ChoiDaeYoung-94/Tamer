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
#if TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS
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

        public static bool CanRequestProductionAds(bool isEditor, bool isDevelopment,
            bool explicitTestAds, bool isAndroid, bool isBatchMode)
        {
#if TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS || TAMER_AD_TEST_HARNESS
            return false;
#else
            return ProductionAdsEnabled && isAndroid && !isEditor && !isDevelopment &&
                !explicitTestAds && !isBatchMode;
#endif
        }

        public static bool TrySelectRewardedAdUnit(bool testEnvironment, bool productionEnvironment,
            bool isIos, string configuredProductionUnit, out string adUnit)
        {
            adUnit = null;
            // Ambiguous environments fail closed instead of choosing either inventory.
            if (testEnvironment == productionEnvironment) return false;
            if (testEnvironment)
            {
                adUnit = TestRewardedAdUnit(isIos);
                return true;
            }
            if (isIos || string.IsNullOrEmpty(configuredProductionUnit) ||
                !System.Text.RegularExpressions.Regex.IsMatch(configuredProductionUnit,
                    "\\Aca-app-pub-[0-9]{16}/[0-9]{10}\\z") ||
                configuredProductionUnit.StartsWith("ca-app-pub-3940256099942544/",
                    System.StringComparison.Ordinal)) return false;
            adUnit = configuredProductionUnit;
            return true;
        }
    }
}
