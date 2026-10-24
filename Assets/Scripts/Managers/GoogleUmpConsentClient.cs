using System;
using AD.Advertising;
using GoogleMobileAds.Ump.Api;

namespace AD
{
    /// <summary>SDK adapter only; it is never constructed by startup or a blocked release request.</summary>
    internal sealed class GoogleUmpConsentClient : IAdConsentClient, IAdPrivacyStatusClient
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
#if TAMER_PRIVACY_COLD_HARNESS || TAMER_PRIVACY_PRODUCT_UMP_HARNESS
        private readonly Action<string, double> _observer;
        private static long _observationSequence;
        internal GoogleUmpConsentClient(Action<string, double> observer) { _observer = observer; }
        internal GoogleUmpConsentClient(DebugGeography geography, string testDeviceHash,
            Action<string, double> observer) : this(geography, testDeviceHash) { _observer = observer; }
        private void Observe(string value)
        {
            double receivedAt = (double)System.Diagnostics.Stopwatch.GetTimestamp() /
                System.Diagnostics.Stopwatch.Frequency;
            // Observation must never change the real SDK callback/result.
            long sequence = System.Threading.Interlocked.Increment(ref _observationSequence);
            try { _observer?.Invoke(value + " sdk_sequence=" + sequence, receivedAt); } catch (Exception) { }
        }
#endif
        public bool CanRequestAds => ConsentInformation.CanRequestAds();
        public bool PrivacyOptionsRequired => ConsentInformation.PrivacyOptionsRequirementStatus ==
            PrivacyOptionsRequirementStatus.Required;
#if TAMER_PRIVACY_COLD_HARNESS || TAMER_PRIVACY_PRODUCT_UMP_HARNESS
        public AdPrivacyRequirement PrivacyRequirement
        {
            get
            {
                try
                {
                    var status = ConsentInformation.PrivacyOptionsRequirementStatus;
                    Observe("status_read value=" + status);
                    return status == PrivacyOptionsRequirementStatus.Required ? AdPrivacyRequirement.Required
                        : status == PrivacyOptionsRequirementStatus.NotRequired ? AdPrivacyRequirement.NotRequired
                        : AdPrivacyRequirement.Unknown;
                }
                catch (Exception error) { Observe("status_exception type=" + error.GetType().Name); throw; }
            }
        }
#else
        public AdPrivacyRequirement PrivacyRequirement =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required
                ? AdPrivacyRequirement.Required
                : ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.NotRequired
                    ? AdPrivacyRequirement.NotRequired : AdPrivacyRequirement.Unknown;

#endif

        public void Update(bool underAgeOfConsent, Action<bool> completed)
        {
            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = underAgeOfConsent };
#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
            if (_debugSettings != null) parameters.ConsentDebugSettings = _debugSettings;
#endif
#if TAMER_PRIVACY_COLD_HARNESS || TAMER_PRIVACY_PRODUCT_UMP_HARNESS
            Observe("update_call tfua=" + underAgeOfConsent);
            try
            {
                ConsentInformation.Update(parameters, error => {
                    Observe("update_callback ok=" + (error == null) + " code=" + (error == null ? 0 : error.ErrorCode));
                    completed(error == null);
                });
            }
            catch (Exception error) { Observe("update_exception type=" + error.GetType().Name); throw; }
#else
            ConsentInformation.Update(parameters,
                error => completed(error == null));
#endif
        }

#if TAMER_PRIVACY_COLD_HARNESS || TAMER_PRIVACY_PRODUCT_UMP_HARNESS
        public void Gather(Action<bool> completed)
        {
            Observe("unexpected_gather");
            throw new InvalidOperationException("Gather is excluded from the cold privacy fixture.");
        }
        public void ShowPrivacyOptions(Action<bool> completed)
        {
            Observe("show_call");
            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => {
                    Observe("show_callback ok=" + (error == null) + " code=" + (error == null ? 0 : error.ErrorCode));
                    completed(error == null);
                });
            }
            catch (Exception error) { Observe("show_exception type=" + error.GetType().Name); throw; }
        }
#else
        public void Gather(Action<bool> completed) =>
            ConsentForm.LoadAndShowConsentFormIfRequired(error => completed(error == null));

        public void ShowPrivacyOptions(Action<bool> completed) =>
            ConsentForm.ShowPrivacyOptionsForm(error => completed(error == null));
#endif
    }
}
