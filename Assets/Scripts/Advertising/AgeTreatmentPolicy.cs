namespace AD.Advertising
{
    public enum AdAgeTreatment { Unspecified, Child, Teen }

    public sealed class AgeTreatmentPlan
    {
        public AdAgeTreatment Advertising { get; }
        public bool UmpUnderAgeOfConsent { get; }
        internal AgeTreatmentPlan(AdAgeTreatment advertising, bool umpUnderAgeOfConsent)
        {
            Advertising = advertising;
            UmpUnderAgeOfConsent = umpUnderAgeOfConsent;
        }
    }

    /// <summary>Saved self-reported age is not consent or a reviewed regional treatment.</summary>
    public static class AgeTreatmentPolicy
    {
        // A source-reviewed release contract, never a PlayerPrefs/locale/debug override.
        // Supported regions, age treatment and published messages remain unreviewed.
        public static bool RegionalConsentReviewed => false;
        public static bool IsReviewed(AgeChoice choice) =>
            RegionalConsentReviewed && TryCreatePlan(choice, out _);

        // Proposed protections only. A plan is neither regional approval nor consent.
        public static bool TryCreatePlan(AgeChoice choice, out AgeTreatmentPlan plan)
        {
            switch (choice)
            {
                case AgeChoice.Under13:
                case AgeChoice.From13To15:
                    plan = new AgeTreatmentPlan(AdAgeTreatment.Child, true);
                    return true;
                case AgeChoice.From16To17:
                    plan = new AgeTreatmentPlan(AdAgeTreatment.Teen, false);
                    return true;
                case AgeChoice.Adult:
                    plan = new AgeTreatmentPlan(AdAgeTreatment.Unspecified, false);
                    return true;
                default:
                    plan = null;
                    return false;
            }
        }
    }
}
