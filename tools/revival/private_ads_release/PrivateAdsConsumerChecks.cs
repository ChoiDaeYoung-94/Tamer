// Approved source gates are compiled as tracked. Only blocked consumer contexts
// are invoked; inert counters fail if a JNI/resource boundary is reached.
using System;
using AD.Advertising;
namespace UnityEngine
{
    public enum RuntimePlatform { Android, WindowsEditor }
    public static class Application { public static bool isEditor, isBatchMode; public static RuntimePlatform platform = RuntimePlatform.Android; public static string identifier = "com.synthetic.contract"; }
    public static class Debug { public static bool isDebugBuild; }
    public sealed class TextAsset { public byte[] bytes; }
    public static class Resources { public static int Reads; public static T[] LoadAll<T>(string name) { Reads++; throw new Exception(); } }
    public class AndroidJavaObject : IDisposable
    {
        public static int Objects;
        public AndroidJavaObject(string name) { Objects++; throw new Exception(); }
        public T Call<T>(string name, params object[] args) { throw new Exception(); }
        public T Get<T>(string name) { throw new Exception(); }
        public void Dispose() { }
    }
    public class AndroidJavaClass : AndroidJavaObject
    { public AndroidJavaClass(string name) : base(name) { } public T GetStatic<T>(string name) { throw new Exception(); } }
}
internal static class PrivateAdsConsumerChecks
{
    private static void CheckBlocked(bool editor, bool development, bool batch, bool android,
        AgeChoice age, ref bool pure, ref bool consumer)
    {
        UnityEngine.Application.isEditor = editor;
        UnityEngine.Debug.isDebugBuild = development;
        UnityEngine.Application.isBatchMode = batch;
        UnityEngine.Application.platform = android
            ? UnityEngine.RuntimePlatform.Android : UnityEngine.RuntimePlatform.WindowsEditor;
        pure &= !PrivateAdsReleaseContract.CanInspectProductionContext(age);
        consumer &= !PrivateAdsReleaseContract.AllowsCurrentAndroidRelease("synthetic", age) &&
            !PrivateAdsReleaseContract.AllowsAdultRelease("com.synthetic.contract", "synthetic", "synthetic", age);
    }

    public static int Main()
    {
        // Inspecting this pure gate does not touch JNI/resources. Do not invoke
        // the consumer in this otherwise eligible simulated release context.
        bool approved = AdRequestPolicy.ProductionAdsEnabled &&
            AgeTreatmentPolicy.RegionalConsentReviewed && AgeTreatmentPolicy.IsReviewed(AgeChoice.Adult) &&
            PrivateAdsReleaseContract.CanInspectProductionContext(AgeChoice.Adult);
        bool pure = true, consumer = true;
        CheckBlocked(true, false, false, true, AgeChoice.Adult, ref pure, ref consumer);
        CheckBlocked(false, true, false, true, AgeChoice.Adult, ref pure, ref consumer);
        CheckBlocked(false, false, true, true, AgeChoice.Adult, ref pure, ref consumer);
        CheckBlocked(false, false, false, false, AgeChoice.Adult, ref pure, ref consumer);
        foreach (var age in new[] { AgeChoice.Unknown, AgeChoice.Declined, AgeChoice.Under13,
            AgeChoice.From13To15, AgeChoice.From16To17, (AgeChoice)999 })
            CheckBlocked(false, false, false, true, age, ref pure, ref consumer);
        bool build = !PrivateAdsReleaseContract.TryReadApprovedBuildContract(new byte[] { 123, 125 }, "synthetic", "synthetic", out var unit) && unit == null;
        bool native = UnityEngine.AndroidJavaObject.Objects == 0 && UnityEngine.Resources.Reads == 0;
        bool unknown = !PrivateAdsReleaseContract.CanInspectProductionContext(AgeChoice.Unknown) &&
            !PrivateAdsReleaseContract.CanInspectProductionContext(AgeChoice.Declined);
        Console.WriteLine("{\"approvedSourceAdultContextInspectable\":" + approved.ToString().ToLowerInvariant() +
            ",\"consumerFixtureContext\":\"blocked_environment_or_nonadult_only\",\"pureGateClosed\":" + pure.ToString().ToLowerInvariant() +
            ",\"consumerClosed\":" + consumer.ToString().ToLowerInvariant() + ",\"approvedBuildClosed\":" + build.ToString().ToLowerInvariant() +
            ",\"jniAndResourceReadsZero\":" + native.ToString().ToLowerInvariant() + ",\"unknownDeclinedClosed\":" + unknown.ToString().ToLowerInvariant() + "}");
        return approved && pure && consumer && build && native && unknown ? 0 : 1;
    }
}
