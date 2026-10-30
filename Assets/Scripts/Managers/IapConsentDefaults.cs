using UnityEngine;
using UnityEngine.UnityConsent;

namespace AD
{
    /// <summary>
    /// This application collects no opt-in for IAP advertising identifiers.
    /// Deny that optional purpose even if a prior/native state was granted.
    /// Default unspecified Analytics to denied; preserve explicit Analytics choices.
    /// Unity diagnostic collection has a separate project setting.
    /// </summary>
    public static class IapConsentDefaults
    {
        public static ConsentState WithSafeDefaults(ConsentState current)
        {
            // AdMob's age/UMP flow is not consent for Unity IAP advertising data.
            // IAP can query GAID when AdsIntent is granted and remote settings
            // enable it, including while this application's age is still unknown.
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
