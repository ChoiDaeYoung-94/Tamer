using System;
using AD.Advertising;
using GoogleMobileAds.Ump.Api;

namespace AD
{
    /// <summary>SDK adapter only; it is never constructed by startup or a blocked release request.</summary>
    internal sealed class GoogleUmpConsentClient : IAdConsentClient
    {
        public bool CanRequestAds => ConsentInformation.CanRequestAds();
        public bool PrivacyOptionsRequired => ConsentInformation.PrivacyOptionsRequirementStatus ==
            PrivacyOptionsRequirementStatus.Required;

        public void Update(bool underAgeOfConsent, Action<bool> completed) =>
            ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = underAgeOfConsent },
                error => completed(error == null));

        public void Gather(Action<bool> completed) =>
            ConsentForm.LoadAndShowConsentFormIfRequired(error => completed(error == null));

        public void ShowPrivacyOptions(Action<bool> completed) =>
            ConsentForm.ShowPrivacyOptionsForm(error => completed(error == null));
    }
}
