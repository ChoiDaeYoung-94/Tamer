using System;
using System.Collections.Generic;
using AD.Advertising;

// Runs only the actual pure policy/gate with synthetic clients, never Unity or UMP.
internal static class PrivateAdsPrivacyRestartChecks
{
    private class Client : IAdConsentClient
    {
        public bool CanRequestAds => true; // A cached grant must never grant this owner ads.
        public bool PrivacyOptionsRequired => true; // Deliberately stale.
        public int Updates, Shows, Gathers;
        public bool Tfua;
        public Action<bool> Updated, Closed;
        public void Update(bool tfua, Action<bool> done) { Updates++; Tfua = tfua; Updated = done; }
        public void Gather(Action<bool> done) { Gathers++; throw new Exception("Gather forbidden"); }
        public void ShowPrivacyOptions(Action<bool> done) { Shows++; Closed = done; }
    }
    private sealed class StatusClient : Client, IAdPrivacyStatusClient
    {
        public AdPrivacyRequirement Status = AdPrivacyRequirement.Required;
        public bool ThrowStatus;
        public AdPrivacyRequirement PrivacyRequirement => ThrowStatus ? throw new Exception("Status unavailable") : Status;
    }
    private static void Require(bool condition) { if (!condition) throw new Exception("Invariant failed"); }
    private static AdConsentGate Gate(Client client, bool tfua = false) => new AdConsentGate(client, tfua, a => a());
    private static int checks;
    private static void Check(string name, Action test)
    {
        test(); checks++; Console.WriteLine("PASS " + name);
    }
    public static int Main(string[] args)
    {
#if TAMER_PRIVACY_MANAGER_HARNESS
        if (args.Length == 1 && args[0] == "--native-context-only")
        {
            string package = AgeTreatmentPolicy.NativePrivacyHarnessPackage;
            const string scene = "Assets/Tests/Scenes/RevivalAdHarness.unity";
            Require(AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(false, true, true, false, package, scene, false));
            Require(!AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(true, true, true, false, package, scene, false));
            Require(!AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(false, false, true, false, package, scene, false));
            Require(!AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(false, true, false, false, package, scene, false));
            Require(!AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(false, true, true, true, package, scene, false));
            Require(!AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(false, true, true, false, package, scene, true));
            foreach (var other in new[] { null, "", "com.AeDeong.MonsterTamer", package + " ", package.ToUpperInvariant() })
                Require(!AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(false, true, true, false, other, scene, false));
            Require(!AgeTreatmentPolicy.NativePrivacyHarnessContextAllowed(false, true, true, false, package, "Assets/Tests/Scenes/RevivalSmoke.unity", false));
            Require(AgeTreatmentPolicy.NativePrivacyHarnessReviewsDisabled);
            Require(!AgeTreatmentPolicy.IsPrivacySdkContext(false, false, false, false, true, "com.AeDeong.MonsterTamer"));
            Console.WriteLine("PASS native_privacy_context_boundaries_reviews_false_release_context_blocked");
            return 0;
        }
#endif
        if (args.Length == 1 && args[0] == "--context-only")
        {
            const string package = "com.AeDeong.MonsterTamer";
            Require(AgeTreatmentPolicy.IsPrivacySdkContext(false, false, false, false, true, package));
            Require(!AgeTreatmentPolicy.IsPrivacySdkContext(true, false, false, false, true, package));
            Require(!AgeTreatmentPolicy.IsPrivacySdkContext(false, true, false, false, true, package));
            Require(!AgeTreatmentPolicy.IsPrivacySdkContext(false, false, true, false, true, package));
            Require(!AgeTreatmentPolicy.IsPrivacySdkContext(false, false, false, true, true, package));
            Require(!AgeTreatmentPolicy.IsPrivacySdkContext(false, false, false, false, false, package));
            foreach (var other in new[] { null, "", package + ".revival.privacyui", package.ToLowerInvariant(), package + " " })
                Require(!AgeTreatmentPolicy.IsPrivacySdkContext(false, false, false, false, true, other));
            Require(!AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed && !AgeTreatmentPolicy.RegionalConsentReviewed);
            foreach (AgeChoice age in Enum.GetValues(typeof(AgeChoice))) Require(!AgeTreatmentPolicy.IsReviewed(age));
            Console.WriteLine("PASS privacy_context_boundaries_and_reviews_remain_false");
            return 0;
        }
        Check("production_false_and_no_age_never_start_discovery", () =>
        {
            foreach (AgeChoice age in Enum.GetValues(typeof(AgeChoice)))
            {
                Require(!AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed && !AgeTreatmentPolicy.IsReviewed(age));
                Require(!AgeTreatmentPolicy.TryCreatePrivacyPlan(age, false, false, true, out _));
                Require(!AgeTreatmentPolicy.TryCreatePrivacyPlan(age, false, true, false, out _));
                Require(!AgeTreatmentPolicy.TryCreatePrivacyPlan(age, true, true, true, out _));
            }
            Require(!AgeTreatmentPolicy.TryCreatePrivacyPlan(AgeChoice.Unknown, false, true, true, out _));
            Require(!AgeTreatmentPolicy.TryCreatePrivacyPlan(AgeChoice.Declined, false, true, true, out _));
        });
        Check("reviewed_synthetic_ages_keep_current_tfua_without_gather_or_ads", () =>
        {
            foreach (var age in new[] { AgeChoice.Under13, AgeChoice.From13To15, AgeChoice.From16To17, AgeChoice.Adult })
            {
                Require(AgeTreatmentPolicy.TryCreatePrivacyPlan(age, false, true, true, out var plan));
                var c = new StatusClient(); using (var g = Gate(c, plan.UmpUnderAgeOfConsent))
                {
                    int completions = 0; g.RefreshPrivacyOptions(r => { Require(r == AdPrivacyResult.OptionsClosed); completions++; });
                    Require(c.Shows == 0 && !g.PrivacyOptionsRequired && !g.CanRequestAds);
                    c.Updated(true); Require(c.Shows == 1 && g.IsBusy && !g.ExpireUpdate());
                    c.Closed(true); c.Closed(true); c.Updated(true);
                    Require(completions == 1 && c.Shows == 1 && c.Updates == 1 && c.Gathers == 0 && !g.CanRequestAds);
                    Require(c.Tfua == (age == AgeChoice.Under13 || age == AgeChoice.From13To15));
                    Require(!g.Request(_ => { }));
                }
            }
        });
        Check("failed_update_does_not_use_cached_required_or_auto_retry", () =>
        {
            var c = new StatusClient(); using (var g = Gate(c))
            {
                var result = AdPrivacyResult.None; g.RefreshPrivacyOptions(r => result = r);
                c.Updated(false); c.Updated(true);
                Require(result == AdPrivacyResult.UpdateFailed && c.Shows == 0 && c.Updates == 1);
                Require(!g.PrivacyOptionsRequired && !g.OpenPrivacyOptions(_ => { }) && !g.CanRequestAds);
            }
        });
        Check("unknown_missing_throwing_status_do_not_fall_back_to_cached_bool", () =>
        {
            foreach (Client c in new Client[] { new Client(), new StatusClient { Status = AdPrivacyRequirement.Unknown }, new StatusClient { ThrowStatus = true } })
                using (var g = Gate(c))
                {
                    var result = AdPrivacyResult.None; g.RefreshPrivacyOptions(r => result = r); c.Updated(true);
                    Require(result == AdPrivacyResult.Unknown && !g.PrivacyOptionsRequired && !g.OpenPrivacyOptions(_ => { }) && c.Shows == 0);
                }
        });
        Check("not_required_is_not_withdrawal_or_form_success", () =>
        {
            var c = new StatusClient { Status = AdPrivacyRequirement.NotRequired }; using (var g = Gate(c))
            {
                var result = AdPrivacyResult.None; g.RefreshPrivacyOptions(r => result = r); c.Updated(true);
                Require(result == AdPrivacyResult.NotRequired && c.Shows == 0 && !g.PrivacyOptionsRequired && !g.CanRequestAds);
            }
        });
        Check("update_timeout_rejects_late_callback_then_manual_request_gets_new_version", () =>
        {
            var c = new StatusClient(); using (var g = Gate(c))
            {
                int calls = 0; g.RefreshPrivacyOptions(r => { Require(r == AdPrivacyResult.UpdateFailed); calls++; });
                var old = c.Updated; Require(g.ExpireUpdate()); old(true); Require(c.Shows == 0 && calls == 1);
                g.RefreshPrivacyOptions(_ => calls++); old(true); Require(c.Shows == 0);
                c.Updated(true); c.Closed(false); Require(c.Shows == 1 && calls == 2 && !g.CanRequestAds);
            }
        });
        Check("age_change_during_update_discards_stale_required_and_completion", () =>
        {
            var c = new StatusClient(); using (var g = Gate(c))
            {
                int calls = 0; g.RefreshPrivacyOptions(_ => calls++); g.SuspendForPrivacy(); c.Updated(true);
                Require(!g.IsBusy && !g.PrivacyOptionsRequired && c.Shows == 0 && calls == 0 && !g.CanRequestAds);
            }
        });
        Check("age_change_during_form_keeps_busy_until_real_callback", () =>
        {
            var c = new StatusClient(); using (var g = Gate(c))
            {
                int calls = 0; g.RefreshPrivacyOptions(_ => calls++); c.Updated(true); g.SuspendForPrivacy();
                Require(g.IsBusy && !g.ExpireUpdate() && !g.RefreshPrivacyOptions(_ => { }) && !g.OpenPrivacyOptions(_ => { }));
                Require(c.Shows == 1); c.Closed(true); Require(!g.IsBusy && calls == 0 && !g.CanRequestAds);
            }
        });
        Check("dispose_and_recreation_do_not_restore_required_or_grant", () =>
        {
            var c = new StatusClient(); var g = Gate(c); int calls = 0;
            g.RefreshPrivacyOptions(_ => calls++); var old = c.Updated; g.Dispose(); old(true);
            using (var fresh = Gate(c)) Require(!fresh.PrivacyOptionsRequired && !fresh.CanRequestAds && !fresh.IsBusy);
            Require(c.Shows == 0 && calls == 0);
        });
        Check("native_form_error_settles_once_without_automatic_retry", () =>
        {
            var c = new StatusClient(); using (var g = Gate(c))
            {
                int calls = 0; g.RefreshPrivacyOptions(r => { Require(r == AdPrivacyResult.FormFailed); calls++; });
                c.Updated(true); c.Closed(false); c.Closed(true);
                Require(calls == 1 && c.Shows == 1 && c.Updates == 1 && c.Gathers == 0 && !g.IsBusy && !g.CanRequestAds);
            }
        });
        Check("warm_privacy_result_distinguishes_form_failure_from_ad_permission", () =>
        {
            foreach (bool success in new[] { false, true })
            {
                var c = new StatusClient(); using (var g = Gate(c))
                {
                    g.Request(_ => { }); c.Updated(false); g.SuspendForPrivacy();
                    var result = AdPrivacyResult.None; int calls = 0;
                    Require(g.OpenPrivacyOptions(allowed => { Require(!allowed); calls++; }, r => result = r));
                    c.Closed(success); c.Closed(!success);
                    Require(calls == 1 && result == (success ? AdPrivacyResult.OptionsClosed : AdPrivacyResult.FormFailed));
                    Require(!g.CanRequestAds && c.Shows == 1 && c.Gathers == 0);
                }
            }
        });
        Console.WriteLine("PASS actual privacy restart checks: " + checks);
        return 0;
    }
}
