using System;
using System.Collections.Generic;
using AD.Advertising;
internal static class PrivateAdsPrivacyAgeChecks
{
    private sealed class Client : IAdConsentClient
    {
        public bool CanRequestAds { get; set; } = true;
        public bool PrivacyOptionsRequired { get; set; } = true;
        public int Updates, Gathers, Privacy;
        public Action<bool> Updated, Gathered, Opened;
        public void Update(bool underAge, Action<bool> completed) { Updates++; Updated = completed; }
        public void Gather(Action<bool> completed) { Gathers++; Gathered = completed; }
        public void ShowPrivacyOptions(Action<bool> completed) { Privacy++; Opened = completed; }
    }
    private static AdConsentGate Gate(Client client) => new AdConsentGate(client, false, callback => callback());
    private static void Ready(AdConsentGate gate, Client client)
    { gate.Request(_ => { }); client.Updated(true); client.Gathered(true); }
    private static readonly Dictionary<string, bool> results = new Dictionary<string, bool>();
    private static void Check(string name, Func<bool> test)
    { try { results[name] = test(); } catch { results[name] = false; } }
    public static int Main()
    {
        Check("ready_suspend_retains_existing_entry_and_blocks_ads", () => {
            var client = new Client(); var gate = Gate(client); Ready(gate, client); gate.SuspendForPrivacy();
            return gate.IsPrivacyOnly && gate.PrivacyOptionsRequired && !gate.CanRequestAds && !gate.Request(_ => { }) && client.Updates == 1; });
        Check("updating_suspend_late_callback_cannot_gather", () => {
            var client = new Client(); var gate = Gate(client); int calls = 0; gate.Request(_ => calls++);
            gate.SuspendForPrivacy(); client.Updated(true);
            return !gate.IsBusy && !gate.CanRequestAds && calls == 0 && client.Gathers == 0 && client.Privacy == 0; });
        Check("gathering_suspend_retains_native_busy_until_completion", () => {
            var client = new Client(); var gate = Gate(client); int calls = 0; gate.Request(_ => calls++); client.Updated(true);
            gate.SuspendForPrivacy(); bool busy = gate.IsBusy && !gate.OpenPrivacyOptions(_ => { }) && !gate.ExpireUpdate();
            client.Gathered(true);
            return busy && !gate.IsBusy && !gate.CanRequestAds && gate.PrivacyOptionsRequired && calls == 0 && client.Privacy == 0; });
        Check("privacy_suspend_retains_native_busy_and_discards_previous_completion", () => {
            var client = new Client(); var gate = Gate(client); Ready(gate, client); int calls = 0;
            gate.OpenPrivacyOptions(_ => calls++); gate.SuspendForPrivacy();
            bool busy = gate.IsBusy && !gate.OpenPrivacyOptions(_ => calls++) && !gate.ExpireUpdate();
            client.Opened(true);
            return busy && !gate.IsBusy && !gate.CanRequestAds && calls == 0 && client.Privacy == 1; });
        Check("explicit_privacy_after_suspend_never_restarts_ad_or_update", () => {
            var client = new Client(); var gate = Gate(client); Ready(gate, client); gate.SuspendForPrivacy();
            int calls = 0; bool allowed = true;
            bool opened = gate.OpenPrivacyOptions(value => { calls++; allowed = value; }); client.Opened(true);
            return opened && calls == 1 && !allowed && !gate.CanRequestAds && client.Updates == 1 && client.Gathers == 1 && client.Privacy == 1; });
        Check("idle_suspend_creates_no_required_entry_or_sdk_operation", () => {
            var client = new Client(); var gate = Gate(client); gate.SuspendForPrivacy();
            return !gate.PrivacyOptionsRequired && !gate.Request(_ => { }) && !gate.OpenPrivacyOptions(_ => { }) && client.Updates + client.Gathers + client.Privacy == 0; });
        Check("dispose_suspended_owner_invalidates_late_form", () => {
            var client = new Client(); var gate = Gate(client); Ready(gate, client); int calls = 0;
            gate.OpenPrivacyOptions(_ => calls++); gate.SuspendForPrivacy(); gate.Dispose(); client.Opened(true);
            return calls == 0 && !gate.PrivacyOptionsRequired && !gate.CanRequestAds; });
        Check("localage_edit_select_and_save_failure_emit_invalidation_before_choice", () => {
            var changes = new List<bool>(); var age = new LocalAgeChoice(() => "1|18plus", _ => { });
            age.Changed += () => changes.Add(age.HasAge); bool selected = age.Select(AgeChoice.Declined);
            var failed = new LocalAgeChoice(() => "1|18plus", _ => { throw new Exception(); }); int failedChanges = 0;
            failed.Changed += () => failedChanges++; bool rejected = !failed.Select(AgeChoice.Under13);
            return selected && changes.Count == 2 && !changes[0] && !changes[1] && rejected && failed.IsEditing && failedChanges == 1; });
        Check("required_fallback_survives_unready_adult_reentry_and_double_age_edit", () => {
            var firstClient = new Client(); var retained = Gate(firstClient); Ready(retained, firstClient); retained.SuspendForPrivacy();
            var original = retained;
            var freshClient = new Client { PrivacyOptionsRequired = false }; var active = Gate(freshClient);
            active.Request(_ => { });
            AdConsentGate.SuspendAndRetainPrivacy(ref active, ref retained);
            AdConsentGate.SuspendAndRetainPrivacy(ref active, ref retained);
            freshClient.Updated(true);
            bool same = active == null && ReferenceEquals(retained, original) && retained.PrivacyOptionsRequired;
            bool open = retained.OpenPrivacyOptions(_ => { }); firstClient.Opened(true);
            return same && open && freshClient.Updates == 1 && freshClient.Gathers == 0 && firstClient.Privacy == 1 && !retained.CanRequestAds; });
        Check("required_fallback_and_unready_native_form_keep_both_owners_until_real_close", () => {
            var firstClient = new Client(); var retained = Gate(firstClient); Ready(retained, firstClient); retained.SuspendForPrivacy();
            var original = retained; var freshClient = new Client { PrivacyOptionsRequired = false }; var active = Gate(freshClient);
            active.Request(_ => { }); freshClient.Updated(true);
            AdConsentGate.SuspendAndRetainPrivacy(ref active, ref retained);
            bool owners = active != null && active.IsBusy && ReferenceEquals(retained, original);
            freshClient.Gathered(true); AdConsentGate.SuspendAndRetainPrivacy(ref active, ref retained);
            return owners && active == null && retained.PrivacyOptionsRequired && !retained.CanRequestAds; });
        int failures = 0; foreach (var result in results) if (!result.Value) failures++;
        Console.WriteLine("{\"checks\":{" + string.Join(",", Format()) + "},\"failed\":" + failures + "}");
        return failures == 0 ? 0 : 1;
    }
    private static IEnumerable<string> Format()
    { foreach (var result in results) yield return "\"" + result.Key + "\":" + result.Value.ToString().ToLowerInvariant(); }
}
