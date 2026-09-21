using System;
using AD.Advertising;
using GoogleMobileAds.Ump.Api;

namespace AD
{
    /// <summary>SDK adapter only; it is never constructed by startup or a blocked release request.</summary>
    internal sealed class GoogleUmpConsentClient : IAdConsentClient
    {
#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
        private readonly ConsentDebugSettings _debugSettings;
        public GoogleUmpConsentClient() { }
        internal GoogleUmpConsentClient(DebugGeography geography, string testDeviceHash)
        {
            _debugSettings = CreateDebugSettings(geography, testDeviceHash);
        }

        internal static ConsentDebugSettings CreateDebugSettings(DebugGeography geography, string testDeviceHash)
        {
            if (geography != DebugGeography.EEA && geography != DebugGeography.RegulatedUSState &&
                geography != DebugGeography.Other)
                throw new ArgumentOutOfRangeException(nameof(geography));
            if (string.IsNullOrEmpty(testDeviceHash) || testDeviceHash.Length != 32 ||
                !System.Text.RegularExpressions.Regex.IsMatch(testDeviceHash, "\\A[0-9a-fA-F]{32}\\z"))
                throw new ArgumentException("A local UMP test-device hash is required.", nameof(testDeviceHash));
            return new ConsentDebugSettings { DebugGeography = geography,
                TestDeviceHashedIds = new System.Collections.Generic.List<string> { testDeviceHash } };
        }
#endif
        public bool CanRequestAds => ConsentInformation.CanRequestAds();
        public bool PrivacyOptionsRequired => ConsentInformation.PrivacyOptionsRequirementStatus ==
            PrivacyOptionsRequirementStatus.Required;

        public void Update(bool underAgeOfConsent, Action<bool> completed)
        {
            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = underAgeOfConsent };
#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
            parameters.ConsentDebugSettings = _debugSettings;
#endif
            ConsentInformation.Update(parameters,
                error => completed(error == null));
        }

        public void Gather(Action<bool> completed) =>
            ConsentForm.LoadAndShowConsentFormIfRequired(error => completed(error == null));

        public void ShowPrivacyOptions(Action<bool> completed) =>
            ConsentForm.ShowPrivacyOptionsForm(error => completed(error == null));
    }
}
