using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AD.Advertising
{
    /// <summary>
    /// Immutable adult release approval reader. A source-reviewed binding is required;
    /// a resource, age choice or consent result alone cannot authorize production ads.
    /// </summary>
    public static class PrivateAdsReleaseContract
    {
        private const string ResourceName = "RevivalPrivateAdsRelease";
        // No release has been authorized. A future reviewed source change must bind
        // exact approved bytes and their documentary references; no setter/override.
        private static readonly Binding ApprovedRelease = null;
        private static bool loaded;
        private static Contract cached;
        private static bool manifestRead;
        private static string manifestAppId;

        // Pure gate runs before resource or JNI access. It does not grant UMP
        // consent, entitlement or a reward, and cannot be overridden by JSON.
        public static bool CanInspectProductionContext(AgeChoice age) =>
            AdRequestPolicy.ProductionAdsEnabled && AgeTreatmentPolicy.IsReviewed(age) &&
            ApprovedRelease != null && RuntimeEligible(Application.isEditor, Debug.isDebugBuild,
                Application.isBatchMode, Application.platform == RuntimePlatform.Android, age);

        public static bool AllowsCurrentAndroidRelease(string rewardedUnit, AgeChoice age)
        {
#if !UNITY_ANDROID || UNITY_EDITOR || DEVELOPMENT_BUILD || TAMER_TEST_ADS || TAMER_REVIVAL_SMOKE || TAMER_AD_TEST_HARNESS || TAMER_AD_SAMPLE_CLOSE_HARNESS || TAMER_UMP_PUBLISHER_HARNESS || TAMER_UMP_ONLY_HARNESS || TAMER_PRIVACY_MANAGER_HARNESS || TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS || TAMER_IAP_STORE_TEST || TAMER_SESSION_HARNESS || TAMER_GAMESAVE_HARNESS || TAMER_PGS_HARNESS || TAMER_PROGRESS_HARNESS || TAMER_JOURNAL_HARNESS || TAMER_DELETION_HARNESS || TAMER_RECEIPT_HARNESS
            return false;
#else
            if (!CanInspectProductionContext(age)) return false;
            if (!manifestRead)
            {
                manifestRead = true;
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
                    using (var info = manager.Call<AndroidJavaObject>("getApplicationInfo", Application.identifier, 128))
                    using (var metadata = info.Get<AndroidJavaObject>("metaData"))
                        manifestAppId = metadata.Call<string>("getString", "com.google.android.gms.ads.APPLICATION_ID");
                }
                catch { manifestAppId = null; }
            }
            return AllowsAdultRelease(Application.identifier, manifestAppId, rewardedUnit, age);
#endif
        }

        // Build-source validation only. Runtime environment and UMP/entitlement
        // eligibility remain separate; no binding is accepted from environment.
        public static bool TryReadApprovedBuildContract(byte[] bytes, string packageId,
            string androidAppId, out string rewardedUnit)
        {
            rewardedUnit = null;
            if (!AdRequestPolicy.ProductionAdsEnabled || !AgeTreatmentPolicy.RegionalConsentReviewed ||
                !AgeTreatmentPolicy.IsReviewed(AgeChoice.Adult) ||
                AgeTreatmentPolicy.IsReviewed(AgeChoice.Under13) || AgeTreatmentPolicy.IsReviewed(AgeChoice.From13To15) ||
                AgeTreatmentPolicy.IsReviewed(AgeChoice.From16To17) ||
                !TryRead(bytes, ApprovedRelease, out var contract) || !contract.MatchesApplication(packageId, androidAppId)) return false;
            rewardedUnit = contract.RewardedUnit;
            return true;
        }

        public static bool AllowsAdultRelease(string packageId, string androidAppId,
            string rewardedUnit, AgeChoice age)
        {
#if !UNITY_ANDROID || UNITY_EDITOR || DEVELOPMENT_BUILD || TAMER_TEST_ADS || TAMER_REVIVAL_SMOKE || TAMER_AD_TEST_HARNESS || TAMER_AD_SAMPLE_CLOSE_HARNESS || TAMER_UMP_PUBLISHER_HARNESS || TAMER_UMP_ONLY_HARNESS || TAMER_PRIVACY_MANAGER_HARNESS || TAMER_GAMEPLAY_HARNESS || TAMER_IAP_HARNESS || TAMER_IAP_STORE_TEST
            return false;
#elif TAMER_SESSION_HARNESS || TAMER_GAMESAVE_HARNESS || TAMER_PGS_HARNESS || TAMER_PROGRESS_HARNESS || TAMER_JOURNAL_HARNESS || TAMER_DELETION_HARNESS || TAMER_RECEIPT_HARNESS
            return false;
#else
            if (!AdRequestPolicy.ProductionAdsEnabled || ApprovedRelease == null ||
                !RuntimeEligible(Application.isEditor, Debug.isDebugBuild,
                    Application.isBatchMode, Application.platform == RuntimePlatform.Android, age)) return false;
            if (!loaded)
            {
                loaded = true; // Missing or malformed data stays closed for this process.
                try
                {
                    var assets = Resources.LoadAll<TextAsset>(ResourceName);
                    if (assets.Length == 1) TryRead(assets[0].bytes, ApprovedRelease, out cached);
                }
                catch { cached = null; } // Never log identifiers or parser input.
            }
            return cached != null && cached.Matches(packageId, androidAppId, rewardedUnit);
#endif
        }

        internal static bool RuntimeEligible(bool editor, bool development, bool batch,
            bool android, AgeChoice age) => !editor && !development && !batch && android && age == AgeChoice.Adult;

        // Hashes identify independently reviewed private records. This is not a
        // digital signature or a claim that those records prove policy compliance.
        internal sealed class Binding
        {
            // SourceHead identifies the reviewed source baseline, not the eventual
            // build commit. Actual build provenance belongs in the artifact receipt.
            internal readonly string ResourceSha256, SourceHead, InventorySha256, CountryContractSha256,
                ActivationReviewSha256, RegionalReviewSha256, AdultReviewSha256;
            internal Binding(string resourceSha256, string sourceHead, string inventorySha256,
                string countryContractSha256, string activationReviewSha256,
                string regionalReviewSha256, string adultReviewSha256)
            {
                ResourceSha256 = resourceSha256; SourceHead = sourceHead; InventorySha256 = inventorySha256;
                CountryContractSha256 = countryContractSha256; ActivationReviewSha256 = activationReviewSha256;
                RegionalReviewSha256 = regionalReviewSha256; AdultReviewSha256 = adultReviewSha256;
            }
        }

        internal sealed class Contract
        {
            private readonly string packageId, appId, rewardedUnit;
            internal Contract(string package, string app, string unit)
            { packageId = package; appId = app; rewardedUnit = unit; }
            internal bool Matches(string package, string app, string unit) =>
                packageId == package && appId == app && rewardedUnit == unit;
            internal bool MatchesApplication(string package, string app) => packageId == package && appId == app;
            internal string RewardedUnit => rewardedUnit;
        }

        private static readonly string[] Fields = {
            "schema", "mode", "packageId", "androidAppId", "productionRewardedAdUnit", "countryCodes",
            "sourceHead", "inventorySha256", "countryContractSha256", "activationReviewSha256",
            "regionalReviewSha256", "adultReviewSha256", "productionActivationApproved",
            "regionalReviewApproved", "adultConsentReviewed", "under13ConsentReviewed",
            "from13To15ConsentReviewed", "from16To17ConsentReviewed" };

        internal static bool TryRead(byte[] bytes, Binding binding, out Contract contract)
        {
            contract = null;
            try
            {
                if (bytes == null || bytes.Length == 0 || bytes.Length > 16384 || binding == null ||
                    !Digest(binding.ResourceSha256) || Hash(bytes) != binding.ResourceSha256 ||
                    !Regex.IsMatch(binding.SourceHead ?? "", @"\A[0-9a-f]{40}\z") ||
                    new[] { binding.InventorySha256, binding.CountryContractSha256, binding.ActivationReviewSha256,
                        binding.RegionalReviewSha256, binding.AdultReviewSha256 }.Any(s => !Digest(s))) return false;
                var text = new UTF8Encoding(false, true).GetString(bytes);
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
                new FlatJson(text).Validate();
                JObject root;
                using (var input = new StringReader(text))
                using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 2 })
                {
                    root = JObject.Load(reader, new JsonLoadSettings {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) return false;
                }
                if (root.Count != Fields.Length || !root.Properties().Select(p => p.Name).OrderBy(s => s, StringComparer.Ordinal)
                    .SequenceEqual(Fields.OrderBy(s => s, StringComparer.Ordinal)) ||
                    root["schema"].Type != JTokenType.Integer || (long)root["schema"] != 1 ||
                    String(root, "mode") != "adult_only_release") return false;
                foreach (var name in new[] { "productionActivationApproved", "regionalReviewApproved", "adultConsentReviewed" })
                    if (!Boolean(root, name, true)) return false;
                foreach (var name in new[] { "under13ConsentReviewed", "from13To15ConsentReviewed", "from16To17ConsentReviewed" })
                    if (!Boolean(root, name, false)) return false;
                if (String(root, "sourceHead") != binding.SourceHead || String(root, "inventorySha256") != binding.InventorySha256 ||
                    String(root, "countryContractSha256") != binding.CountryContractSha256 ||
                    String(root, "activationReviewSha256") != binding.ActivationReviewSha256 ||
                    String(root, "regionalReviewSha256") != binding.RegionalReviewSha256 ||
                    String(root, "adultReviewSha256") != binding.AdultReviewSha256) return false;
                var countries = String(root, "countryCodes").Split(';');
                if (countries.Length == 0 || countries.Any(s => !Regex.IsMatch(s, @"\A[A-Z]{2}\z")) ||
                    !countries.SequenceEqual(countries.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal))) return false;
                // This checks the reviewed distribution declaration; it is not device
                // geolocation or regional consent. Existing UMP remains necessary.
                var package = String(root, "packageId");
                var app = String(root, "androidAppId");
                var unit = String(root, "productionRewardedAdUnit");
                if (!Regex.IsMatch(package, @"\A[a-zA-Z][a-zA-Z0-9_]*(?:\.[a-zA-Z][a-zA-Z0-9_]*)+\z") ||
                    !Regex.IsMatch(app, @"\Aca-app-pub-[0-9]{16}~[0-9]{10}\z") ||
                    !Regex.IsMatch(unit, @"\Aca-app-pub-[0-9]{16}/[0-9]{10}\z") ||
                    app.StartsWith("ca-app-pub-3940256099942544~", StringComparison.Ordinal) ||
                    app.Split('~')[0] != unit.Split('/')[0]) return false;
                contract = new Contract(package, app, unit);
                return true;
            }
            catch { contract = null; return false; }
        }

        private static string String(JObject root, string name)
        {
            var value = root[name];
            if (value == null || value.Type != JTokenType.String || ((string)value).Length == 0)
                throw new InvalidDataException();
            return (string)value;
        }
        private static bool Boolean(JObject root, string name, bool expected) =>
            root[name] != null && root[name].Type == JTokenType.Boolean && (bool)root[name] == expected;
        private static bool Digest(string value) => Regex.IsMatch(value ?? "", @"\A[0-9a-f]{64}\z");
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        // Only the versioned flat object grammar is accepted. Newtonsoft's comments,
        // trailing commas, constructors and permissive numeric syntax are rejected.
        private sealed class FlatJson
        {
            private readonly string text;
            private int position;
            internal FlatJson(string value) { text = value; }
            private char Peek => position < text.Length ? text[position] : '\0';
            private void Space() { while (Peek == ' ' || Peek == '\t' || Peek == '\r' || Peek == '\n') position++; }
            private bool Take(char value) { if (Peek != value) return false; position++; return true; }
            private void Need(char value) { if (!Take(value)) throw new InvalidDataException(); }
            internal void Validate()
            {
                Space(); Need('{'); Space();
                if (!Take('}'))
                    while (true)
                    {
                        StringToken(); Space(); Need(':'); Space();
                        if (Peek == '"') StringToken();
                        else if (Peek == 't') Literal("true");
                        else if (Peek == 'f') Literal("false");
                        else if (Peek == '1') position++; // The only numeric value in schema 1.
                        else throw new InvalidDataException();
                        Space(); if (Take('}')) break;
                        Need(','); Space();
                    }
                Space(); if (position != text.Length) throw new InvalidDataException();
            }
            private void Literal(string value)
            { foreach (var ch in value) Need(ch); }
            private void StringToken()
            {
                Need('"');
                while (position < text.Length)
                {
                    var ch = text[position++];
                    if (ch == '"') return;
                    if (ch < 0x20) throw new InvalidDataException();
                    if (ch != '\\') continue;
                    if (position == text.Length) throw new InvalidDataException();
                    ch = text[position++];
                    if (ch == 'u')
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (!((Peek >= '0' && Peek <= '9') || (Peek >= 'a' && Peek <= 'f') || (Peek >= 'A' && Peek <= 'F')))
                                throw new InvalidDataException();
                            position++;
                        }
                    }
                    else if ("\"\\/bfnrt".IndexOf(ch) < 0) throw new InvalidDataException();
                }
                throw new InvalidDataException();
            }
        }
    }
}
