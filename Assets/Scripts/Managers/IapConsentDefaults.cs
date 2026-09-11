using UnityEngine;
using UnityEngine.UnityConsent;

namespace AD
{
    /// <summary>
    /// No opt-in is collected by the current application. Default optional Ads
    /// and Analytics purposes to denied before a store can collect player data.
    /// Unity diagnostic collection has a separate project setting.
    /// </summary>
    public static class IapConsentDefaults
    {
        public static ConsentState WithSafeDefaults(ConsentState current)
        {
            if (current.AdsIntent == ConsentStatus.Unspecified)
                current.AdsIntent = ConsentStatus.Denied;
            if (current.AnalyticsIntent == ConsentStatus.Unspecified)
                current.AnalyticsIntent = ConsentStatus.Denied;
            return current;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            EndUserConsent.SetConsentState(WithSafeDefaults(EndUserConsent.GetConsentState()));
        }
    }
}
