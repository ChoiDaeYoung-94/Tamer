// New consumer-boundary checks only. No Unity or native implementation is loaded.
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
    public static int Main()
    {
        bool pure = !PrivateAdsReleaseContract.CanInspectProductionContext(AgeChoice.Adult);
        bool consumer = !PrivateAdsReleaseContract.AllowsCurrentAndroidRelease("synthetic", AgeChoice.Adult);
        bool build = !PrivateAdsReleaseContract.TryReadApprovedBuildContract(new byte[] { 123, 125 }, "synthetic", "synthetic", out var unit) && unit == null;
        bool native = UnityEngine.AndroidJavaObject.Objects == 0 && UnityEngine.Resources.Reads == 0;
        bool unknown = !PrivateAdsReleaseContract.CanInspectProductionContext(AgeChoice.Unknown) &&
            !PrivateAdsReleaseContract.CanInspectProductionContext(AgeChoice.Declined);
        Console.WriteLine("{\"pureGateClosed\":" + pure.ToString().ToLowerInvariant() +
            ",\"consumerClosed\":" + consumer.ToString().ToLowerInvariant() + ",\"approvedBuildClosed\":" + build.ToString().ToLowerInvariant() +
            ",\"jniAndResourceReadsZero\":" + native.ToString().ToLowerInvariant() + ",\"unknownDeclinedClosed\":" + unknown.ToString().ToLowerInvariant() + "}");
        return pure && consumer && build && native && unknown ? 0 : 1;
    }
}
