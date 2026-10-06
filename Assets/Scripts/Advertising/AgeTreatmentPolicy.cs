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
        // Privacy-only SDK calls require a separate reviewed environment.
        public static bool PrivacySdkEnvironmentReviewed => false;

        // Matches the existing Android applicationIdentifier; this is only a
        // context exclusion, not App ID/artifact binding or regional approval.
        public static bool IsPrivacySdkContext(bool isEditor, bool isDevelopment,
            bool isBatchMode, bool explicitTest, bool isAndroid, string packageId)
        {
#if DEVELOPMENT_BUILD || TAMER_TEST_ADS || TAMER_REVIVAL_SMOKE || TAMER_AD_TEST_HARNESS || TAMER_AD_SAMPLE_CLOSE_HARNESS || TAMER_UMP_PUBLISHER_HARNESS || TAMER_UMP_ONLY_HARNESS || TAMER_PRIVACY_UI_HARNESS || TAMER_PRIVACY_MANAGER_HARNESS || TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS || TAMER_IAP_STORE_TEST || TAMER_SESSION_HARNESS || TAMER_GAMESAVE_HARNESS || TAMER_PGS_HARNESS || TAMER_PROGRESS_HARNESS || TAMER_JOURNAL_HARNESS || TAMER_DELETION_HARNESS || TAMER_RECEIPT_HARNESS
            return false;
#else
            return isAndroid && !isEditor && !isDevelopment && !isBatchMode && !explicitTest &&
                string.Equals(packageId, "com.AeDeong.MonsterTamer", System.StringComparison.Ordinal);
#endif
        }

        public static bool TryCreatePrivacyPlan(AgeChoice choice, bool editing,
            bool environmentReviewed, bool ageReviewed, out AgeTreatmentPlan plan)
        {
            plan = null;
            return !editing && environmentReviewed && ageReviewed && TryCreatePlan(choice, out plan);
        }
        // Each cohort needs its own reviewed release decision. Opening the master
        // gate must never implicitly approve every age after an adult-only check.
        private const bool Under13ConsentReviewed = false;
        private const bool From13To15ConsentReviewed = false;
        private const bool From16To17ConsentReviewed = false;
        private const bool AdultConsentReviewed = false;

        public const string NativePrivacyHarnessPackage = "com.AeDeong.MonsterTamer.revival.privacymanager";
#if TAMER_PRIVACY_MANAGER_HARNESS
        public static bool NativePrivacyHarnessReviewsDisabled => !PrivacySdkEnvironmentReviewed &&
            !RegionalConsentReviewed && !Under13ConsentReviewed && !From13To15ConsentReviewed &&
            !From16To17ConsentReviewed && !AdultConsentReviewed;

        public static bool NativePrivacyHarnessContextAllowed(bool editor, bool android, bool development,
            bool batch, string package, string scene, bool hasManagers) =>
            !editor && android && development && !batch && !hasManagers &&
            package == NativePrivacyHarnessPackage && scene == "Assets/Tests/Scenes/RevivalAdHarness.unity";
#endif

        public static bool IsReviewed(AgeChoice choice)
        {
            if (!RegionalConsentReviewed) return false;
            switch (choice)
            {
                case AgeChoice.Under13: return Under13ConsentReviewed;
                case AgeChoice.From13To15: return From13To15ConsentReviewed;
                case AgeChoice.From16To17: return From16To17ConsentReviewed;
                case AgeChoice.Adult: return AdultConsentReviewed;
                default: return false;
            }
        }

        // An age-only candidate, not permission to advertise. The saved Play target
        // includes ages 9+ (adult added, review submission pending); all release
        // and regional gates remain closed, including the separate adult review.
        // The SDK controls the native X; no five-second close guarantee exists here.
        public static bool AllowsFullscreenRewarded(AgeChoice choice) =>
            choice == AgeChoice.Adult;

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
