// Pure executable test fixture outside Assets. Unity APIs are inert stubs;
// no Unity assemblies, Editor, SDK, resources, private inventory or signing load.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AD.Advertising;
using Newtonsoft.Json;

namespace UnityEngine
{
    public enum RuntimePlatform { Android, WindowsEditor }
    public static class Application
    {
        public static bool isEditor = false, isBatchMode = false;
        public static RuntimePlatform platform = RuntimePlatform.Android;
    }
    public static class Debug { public static bool isDebugBuild = false; }
    public sealed class TextAsset { public byte[] bytes; }
    public static class Resources
    {
        public static int Reads;
        public static T[] LoadAll<T>(string name) { Reads++; throw new Exception("Stub resource must not load"); }
    }
}

internal static class PrivateAdsReleaseContractChecks
{
    private static readonly string[] ReviewFields = { "inventorySha256", "countryContractSha256",
        "activationReviewSha256", "regionalReviewSha256", "adultReviewSha256" };
    private static readonly string HashReference = new string('a', 64), Head = new string('b', 40);
    private static readonly Dictionary<string, bool> results = new Dictionary<string, bool>();
    private static Dictionary<string, object> Fixture() => new Dictionary<string, object> {
        { "schema", 1 }, { "mode", "adult_only_release" }, { "packageId", "com.synthetic.contract" },
        { "androidAppId", "ca-app-pub-1111111111111111~2222222222" },
        { "productionRewardedAdUnit", "ca-app-pub-1111111111111111/3333333333" },
        { "countryCodes", "KR;US" }, { "sourceHead", Head }, { "inventorySha256", HashReference },
        { "countryContractSha256", HashReference }, { "activationReviewSha256", HashReference },
        { "regionalReviewSha256", HashReference }, { "adultReviewSha256", HashReference },
        { "productionActivationApproved", true }, { "regionalReviewApproved", true }, { "adultConsentReviewed", true },
        { "under13ConsentReviewed", false }, { "from13To15ConsentReviewed", false }, { "from16To17ConsentReviewed", false } };
    private static string Hash(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static PrivateAdsReleaseContract.Binding Binding(byte[] bytes) =>
        new PrivateAdsReleaseContract.Binding(Hash(bytes), Head, HashReference, HashReference, HashReference, HashReference, HashReference);
    private static bool Read(byte[] bytes) => PrivateAdsReleaseContract.TryRead(bytes, Binding(bytes), out _);
    private static bool Read(string json) => Read(Encoding.UTF8.GetBytes(json));
    private static bool Read(Dictionary<string, object> value) => Read(JsonConvert.SerializeObject(value));
    private static void Check(string name, Func<bool> test)
    { try { results[name] = test(); } catch { results[name] = false; } }

    public static int Main()
    {
        var original = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Fixture()));
        Check("synthetic_exact_approved_contract", () => PrivateAdsReleaseContract.TryRead(original, Binding(original), out var c) &&
            c.Matches("com.synthetic.contract", "ca-app-pub-1111111111111111~2222222222", "ca-app-pub-1111111111111111/3333333333"));
        Check("missing_approval_binding", () => !PrivateAdsReleaseContract.TryRead(original, null, out _));
        Check("empty_null_oversize_utf8_rejected", () => !PrivateAdsReleaseContract.TryRead(null, Binding(original), out _) &&
            !Read(new byte[0]) && !Read(new byte[16385]) && !Read(new byte[] { 0xff, 0xfe }));
        Check("resource_digest_mismatch", () => !PrivateAdsReleaseContract.TryRead(Encoding.UTF8.GetBytes("{}"), Binding(original), out _));
        Check("inventory_context_mismatch", () => PrivateAdsReleaseContract.TryRead(original, Binding(original), out var c) &&
            !c.Matches("com.other", "ca-app-pub-1111111111111111~2222222222", "ca-app-pub-1111111111111111/3333333333") &&
            !c.Matches("com.synthetic.contract", "wrong", "ca-app-pub-1111111111111111/3333333333") &&
            !c.Matches("com.synthetic.contract", "ca-app-pub-1111111111111111~2222222222", "wrong"));
        Check("documentary_reference_mismatch", () => ReviewFields.All(field => {
            var value = Fixture(); value[field] = new string('c', 64); return !Read(value); }));
        Check("source_provenance_mismatch", () => { var value = Fixture(); value["sourceHead"] = new string('c', 40); return !Read(value); });
        Check("malformed_binding_hash_rejected", () => !PrivateAdsReleaseContract.TryRead(original,
            new PrivateAdsReleaseContract.Binding(Hash(original), Head, "not-a-hash", HashReference, HashReference, HashReference, HashReference), out _));
        Check("missing_or_extra_field", () => {
            var missing = Fixture(); missing.Remove("activationReviewSha256");
            var extra = Fixture(); extra["checkout"] = "synthetic unwanted field"; return !Read(missing) && !Read(extra); });
        Check("duplicate_decoded_field", () => !Read(Encoding.UTF8.GetString(original).Replace("\"schema\":1", "\"schema\":1,\"sche\\u006da\":1")));
        Check("strict_json_syntax", () => {
            var text = Encoding.UTF8.GetString(original);
            return !Read(text.Substring(0, text.Length - 1) + ",}") && !Read("/*extension*/" + text) &&
                !Read(text + "{}") && !Read(text.Replace("\"schema\":1", "\"schema\":1.0")) &&
                !Read(text.Replace("\"schema\":1", "\"schema\":true")); });
        Check("approval_flags_strict", () => new[] { "productionActivationApproved", "regionalReviewApproved", "adultConsentReviewed" }.All(field => {
            var value = Fixture(); value[field] = false;
            var wrongType = Fixture(); wrongType[field] = "true"; return !Read(value) && !Read(wrongType); }));
        Check("minor_approval_always_rejected", () => new[] { "under13ConsentReviewed", "from13To15ConsentReviewed", "from16To17ConsentReviewed" }.All(field => {
            var value = Fixture(); value[field] = true; return !Read(value); }));
        Check("disabled_candidate_is_not_release_contract", () => { var value = Fixture(); value["mode"] = "disabled_candidate"; return !Read(value); });
        Check("reviewed_countries_canonical", () => new[] { "US;KR", "KR;KR", "kr", "KR;", "", "KR\nUS" }.All(c => {
            var value = Fixture(); value["countryCodes"] = c; return !Read(value); }));
        Check("sample_or_mixed_publisher_rejected", () => {
            var sample = Fixture(); sample["androidAppId"] = "ca-app-pub-3940256099942544~3347511713";
            var mixed = Fixture(); mixed["productionRewardedAdUnit"] = "ca-app-pub-4444444444444444/3333333333"; return !Read(sample) && !Read(mixed); });
        Check("adult_only_unknown_declined_and_invalid_enum", () =>
            Enum.GetValues(typeof(AgeChoice)).Cast<AgeChoice>().All(age =>
                PrivateAdsReleaseContract.RuntimeEligible(false, false, false, true, age) == (age == AgeChoice.Adult)) &&
            !PrivateAdsReleaseContract.RuntimeEligible(false, false, false, true, (AgeChoice)99));
        Check("runtime_environment_closed", () =>
            !PrivateAdsReleaseContract.RuntimeEligible(true, false, false, true, AgeChoice.Adult) &&
            !PrivateAdsReleaseContract.RuntimeEligible(false, true, false, true, AgeChoice.Adult) &&
            !PrivateAdsReleaseContract.RuntimeEligible(false, false, true, true, AgeChoice.Adult) &&
            !PrivateAdsReleaseContract.RuntimeEligible(false, false, false, false, AgeChoice.Adult));
        Check("current_public_path_false_without_resource_read", () =>
            !PrivateAdsReleaseContract.AllowsAdultRelease("com.synthetic.contract", "synthetic", "synthetic", AgeChoice.Adult) &&
            UnityEngine.Resources.Reads == 0 && !AdRequestPolicy.ProductionAdsEnabled);
        Check("compiled_binding_null_and_readonly", () => {
            var field = typeof(PrivateAdsReleaseContract).GetField("ApprovedRelease", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return field != null && field.IsInitOnly && field.GetValue(null) == null; });
        Console.WriteLine(JsonConvert.SerializeObject(new { checks = results, failed = results.Count(p => !p.Value),
            actualUnityBuilds = 0, sdkCalls = 0, privateApprovalRecordsModified = 0, binaryVerified = false }));
        return results.All(p => p.Value) ? 0 : 1;
    }
}
