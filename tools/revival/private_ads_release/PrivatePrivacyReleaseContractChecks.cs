// In-memory synthetic approval and inert Unity/JNI stubs; no SDK or actual resource.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AD.Advertising;
using Newtonsoft.Json;

namespace UnityEngine
{
    public enum RuntimePlatform { Android }
    public static class Application
    {
        public static bool isEditor, isBatchMode;
        public static RuntimePlatform platform = RuntimePlatform.Android;
        public static string identifier = "com.AeDeong.MonsterTamer";
    }
    public static class Debug { public static bool isDebugBuild; }
    public sealed class TextAsset { public byte[] bytes; }
    public static class Resources
    {
        public static int Reads;
        public static T[] LoadAll<T>(string name) { Reads++; throw new Exception(); }
    }
    public class AndroidJavaObject : IDisposable
    {
        public static int Reads;
        public T Call<T>(string name, params object[] args) { Reads++; throw new Exception(); }
        public T Get<T>(string name) { Reads++; throw new Exception(); }
        public void Dispose() { }
    }
    public class AndroidJavaClass : AndroidJavaObject
    {
        public AndroidJavaClass(string name) { Reads++; throw new Exception(); }
        public T GetStatic<T>(string name) { Reads++; throw new Exception(); }
    }
}

internal static class PrivatePrivacyReleaseContractChecks
{
    private static readonly string Head = new string('b', 40), Reference = new string('a', 64);
    private static readonly string[] References = { "inventorySha256", "countryContractSha256", "regionalReviewSha256",
        "adultReviewSha256", "privacyEnvironmentReviewSha256" };
    private static readonly string[] TrueFlags = { "privacySdkEnvironmentApproved", "regionalReviewApproved", "adultConsentReviewed" };
    private static readonly string[] FalseFlags = { "under13ConsentReviewed", "from13To15ConsentReviewed", "from16To17ConsentReviewed",
        "umpUnderAgeOfConsent", "productionAdsAuthorized" };
    private static readonly Dictionary<string, bool> Results = new Dictionary<string, bool>();
    private static Dictionary<string, object> Fixture()
    {
        var value = new Dictionary<string, object> { { "schema", 1 }, { "mode", "privacy_only_adult" },
            { "packageId", "com.synthetic.contract" }, { "androidAppId", "ca-app-pub-1111111111111111~2222222222" },
            { "sourceHead", Head } };
        foreach (var name in References) value[name] = Reference;
        foreach (var name in TrueFlags) value[name] = true;
        foreach (var name in FalseFlags) value[name] = false;
        return value;
    }
    private static byte[] Bytes(Dictionary<string, object> value) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
    private static PrivatePrivacyReleaseContract.Binding Binding(byte[] bytes) => new PrivatePrivacyReleaseContract.Binding(
        PrivateAdsReleaseContract.Hash(bytes), Head, Reference, Reference, Reference, Reference, Reference);
    private static bool Read(byte[] bytes) => PrivatePrivacyReleaseContract.TryRead(bytes, Binding(bytes), out _);
    private static bool Read(Dictionary<string, object> value) => Read(Bytes(value));
    private static bool Read(string text) => Read(Encoding.UTF8.GetBytes(text));
    private static void Check(string name, Func<bool> test) { try { Results[name] = test(); } catch { Results[name] = false; } }
    public static int Main()
    {
        var original = Bytes(Fixture());
        Check("synthetic_privacy_approval_independent_of_ad_master", () => !AdRequestPolicy.ProductionAdsEnabled &&
            PrivatePrivacyReleaseContract.TryRead(original, Binding(original), out var c) &&
            c.Matches("com.synthetic.contract", "ca-app-pub-1111111111111111~2222222222") &&
            !c.Matches("com.other", "ca-app-pub-1111111111111111~2222222222") && !c.Matches("com.synthetic.contract", "wrong"));
        Check("null_binding_and_hash_tampering_closed", () => !PrivatePrivacyReleaseContract.TryRead(original, null, out _) &&
            !PrivatePrivacyReleaseContract.TryRead(Encoding.UTF8.GetBytes("{}"), Binding(original), out _));
        Check("all_exact_fields_and_types", () => Fixture().Keys.All(key => {
            var missing = Fixture(); missing.Remove(key); var wrong = Fixture(); wrong[key] = null;
            return !Read(missing) && !Read(wrong); }) && !Read(JsonConvert.SerializeObject(Fixture()).Replace("\"schema\":1", "\"schema\":true")));
        Check("extra_field_closed", () => { var value = Fixture(); value["productionRewardedAdUnit"] = "synthetic"; return !Read(value); });
        Check("approval_flags_and_minor_tfua_ad_authority_closed", () => TrueFlags.Concat(FalseFlags).All(key => {
            var opposite = Fixture(); opposite[key] = !(bool)opposite[key];
            var wrong = Fixture(); wrong[key] = "false"; return !Read(opposite) && !Read(wrong); }));
        Check("baseline_and_every_reference_exact", () => References.Concat(new[] { "sourceHead" }).All(key => {
            var value = Fixture(); value[key] = new string('c', key == "sourceHead" ? 40 : 64); return !Read(value); }));
        Check("strict_json_duplicate_utf8_size", () => {
            var text = Encoding.UTF8.GetString(original);
            return !Read(text.Replace("\"schema\":1", "\"schema\":1,\"sche\\u006da\":1")) &&
                !Read("/*comment*/" + text) && !Read(text + "{}") && !Read(text.Substring(0, text.Length - 1) + ",}") &&
                !Read(text.Replace("\"schema\":1", "\"schema\":1.0")) && !Read(new byte[] { 255 }) && !Read(new byte[16385]); });
        Check("sample_app_and_release_mode_rejected", () => {
            var sample = Fixture(); sample["androidAppId"] = "ca-app-pub-3940256099942544~3347511713";
            var release = Fixture(); release["mode"] = "adult_only_release"; return !Read(sample) && !Read(release); });
        Check("adult_tfua_matches_existing_age_candidate", () => AgeTreatmentPolicy.TryCreatePlan(AgeChoice.Adult, out var plan) && !plan.UmpUnderAgeOfConsent);
        Check("all_ages_disabled_before_resource_jni", () => Enum.GetValues(typeof(AgeChoice)).Cast<AgeChoice>()
            .All(age => !PrivatePrivacyReleaseContract.AllowsCurrentAndroidPrivacy(age)) &&
            UnityEngine.Resources.Reads == 0 && UnityEngine.AndroidJavaObject.Reads == 0);
        Check("immutable_binding_null", () => {
            var field = typeof(PrivatePrivacyReleaseContract).GetField("ApprovedPrivacy", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return field.IsInitOnly && field.GetValue(null) == null; });
        Check("existing_ad_release_reader_regression", () => {
            var value = Fixture(); value["mode"] = "adult_only_release";
            value.Remove("privacySdkEnvironmentApproved"); value.Remove("privacyEnvironmentReviewSha256");
            value.Remove("umpUnderAgeOfConsent"); value.Remove("productionAdsAuthorized");
            value["activationReviewSha256"] = Reference; value["productionActivationApproved"] = true;
            value["productionRewardedAdUnit"] = "ca-app-pub-1111111111111111/3333333333"; value["countryCodes"] = "KR;US";
            var bytes = Bytes(value);
            var binding = new PrivateAdsReleaseContract.Binding(PrivateAdsReleaseContract.Hash(bytes), Head,
                Reference, Reference, Reference, Reference, Reference);
            return PrivateAdsReleaseContract.TryRead(bytes, binding, out var c) && c.MatchesApplication("com.synthetic.contract", "ca-app-pub-1111111111111111~2222222222") &&
                !PrivatePrivacyReleaseContract.TryRead(bytes, Binding(bytes), out _);
        });
        Console.WriteLine(JsonConvert.SerializeObject(new { checks = Results, failed = Results.Count(p => !p.Value), sdkCalls = 0,
            unityBuilds = 0, runtimePrivacyVerified = false, binaryVerified = false }));
        return Results.All(p => p.Value) ? 0 : 1;
    }
}
