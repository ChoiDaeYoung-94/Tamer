using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AD.Advertising
{
    /// <summary>Reviewed adult privacy discovery only; never authorizes advertising.</summary>
    public static class PrivatePrivacyReleaseContract
    {
        private const string ResourceName = "RevivalPrivatePrivacyRelease";
        private static readonly Binding ApprovedPrivacy = null;
        private static bool loaded;
        private static Contract cached;

        public static bool AllowsCurrentAndroidPrivacy(AgeChoice age)
        {
            // No resource, JNI or SDK access before immutable approval and context checks.
            if (ApprovedPrivacy == null || !AgeTreatmentPolicy.PrivacySdkEnvironmentReviewed ||
                !AgeTreatmentPolicy.IsReviewed(age) || age != AgeChoice.Adult ||
                !AgeTreatmentPolicy.IsPrivacySdkContext(Application.isEditor, Debug.isDebugBuild,
                    Application.isBatchMode, false, Application.platform == RuntimePlatform.Android,
                    Application.identifier)) return false;
            if (!loaded)
            {
                loaded = true; // A missing, duplicate or malformed resource stays closed.
                try
                {
                    var assets = Resources.LoadAll<TextAsset>(ResourceName);
                    if (assets.Length == 1) TryRead(assets[0].bytes, ApprovedPrivacy, out cached);
                }
                catch { cached = null; } // No private parser input or identifiers in logs.
            }
            return cached != null && cached.Matches(Application.identifier,
                PrivateAdsReleaseContract.ReadCurrentAndroidAppId());
        }

        // SourceHead is a reviewed baseline. The eventual artifact and merged
        // manifest/resource provenance require a separate build receipt.
        internal sealed class Binding
        {
            internal readonly string ResourceSha256, SourceHead, InventorySha256, CountryContractSha256,
                RegionalReviewSha256, AdultReviewSha256, PrivacyEnvironmentReviewSha256;
            internal Binding(string resourceSha256, string sourceHead, string inventorySha256,
                string countryContractSha256, string regionalReviewSha256, string adultReviewSha256,
                string privacyEnvironmentReviewSha256)
            {
                ResourceSha256 = resourceSha256; SourceHead = sourceHead; InventorySha256 = inventorySha256;
                CountryContractSha256 = countryContractSha256; RegionalReviewSha256 = regionalReviewSha256;
                AdultReviewSha256 = adultReviewSha256; PrivacyEnvironmentReviewSha256 = privacyEnvironmentReviewSha256;
            }
        }

        internal sealed class Contract
        {
            private readonly string packageId, appId;
            internal Contract(string package, string app) { packageId = package; appId = app; }
            internal bool Matches(string package, string app) => packageId == package && appId == app;
        }

        private static readonly string[] Fields = {
            "schema", "mode", "packageId", "androidAppId", "sourceHead", "inventorySha256",
            "countryContractSha256", "regionalReviewSha256", "adultReviewSha256", "privacyEnvironmentReviewSha256",
            "privacySdkEnvironmentApproved", "regionalReviewApproved", "adultConsentReviewed",
            "under13ConsentReviewed", "from13To15ConsentReviewed", "from16To17ConsentReviewed",
            "umpUnderAgeOfConsent", "productionAdsAuthorized" };

        internal static bool TryRead(byte[] bytes, Binding binding, out Contract contract)
        {
            contract = null;
            try
            {
                if (bytes == null || bytes.Length == 0 || bytes.Length > 16384 || binding == null ||
                    !PrivateAdsReleaseContract.Digest(binding.ResourceSha256) ||
                    PrivateAdsReleaseContract.Hash(bytes) != binding.ResourceSha256 ||
                    !Regex.IsMatch(binding.SourceHead ?? "", @"\A[0-9a-f]{40}\z") ||
                    new[] { binding.InventorySha256, binding.CountryContractSha256, binding.RegionalReviewSha256,
                        binding.AdultReviewSha256, binding.PrivacyEnvironmentReviewSha256 }
                        .Any(s => !PrivateAdsReleaseContract.Digest(s))) return false;
                var root = PrivateAdsReleaseContract.ReadFlatObject(bytes);
                if (root.Count != Fields.Length || !root.Properties().Select(p => p.Name).OrderBy(s => s, StringComparer.Ordinal)
                    .SequenceEqual(Fields.OrderBy(s => s, StringComparer.Ordinal)) ||
                    root["schema"].Type != JTokenType.Integer || (long)root["schema"] != 1 ||
                    PrivateAdsReleaseContract.String(root, "mode") != "privacy_only_adult") return false;
                foreach (var name in new[] { "privacySdkEnvironmentApproved", "regionalReviewApproved", "adultConsentReviewed" })
                    if (!PrivateAdsReleaseContract.Boolean(root, name, true)) return false;
                foreach (var name in new[] { "under13ConsentReviewed", "from13To15ConsentReviewed", "from16To17ConsentReviewed",
                    "umpUnderAgeOfConsent", "productionAdsAuthorized" })
                    if (!PrivateAdsReleaseContract.Boolean(root, name, false)) return false;
                if (PrivateAdsReleaseContract.String(root, "sourceHead") != binding.SourceHead ||
                    PrivateAdsReleaseContract.String(root, "inventorySha256") != binding.InventorySha256 ||
                    PrivateAdsReleaseContract.String(root, "countryContractSha256") != binding.CountryContractSha256 ||
                    PrivateAdsReleaseContract.String(root, "regionalReviewSha256") != binding.RegionalReviewSha256 ||
                    PrivateAdsReleaseContract.String(root, "adultReviewSha256") != binding.AdultReviewSha256 ||
                    PrivateAdsReleaseContract.String(root, "privacyEnvironmentReviewSha256") != binding.PrivacyEnvironmentReviewSha256)
                    return false;
                // Country hash binds the full declaration, including rest-of-world
                // and grouped locations. It does not determine the device's region.
                var package = PrivateAdsReleaseContract.String(root, "packageId");
                var app = PrivateAdsReleaseContract.String(root, "androidAppId");
                if (!Regex.IsMatch(package, @"\A[a-zA-Z][a-zA-Z0-9_]*(?:\.[a-zA-Z][a-zA-Z0-9_]*)+\z") ||
                    !Regex.IsMatch(app, @"\Aca-app-pub-[0-9]{16}~[0-9]{10}\z") ||
                    app.StartsWith("ca-app-pub-3940256099942544~", StringComparison.Ordinal)) return false;
                contract = new Contract(package, app);
                return true;
            }
            catch { contract = null; return false; }
        }
    }
}
