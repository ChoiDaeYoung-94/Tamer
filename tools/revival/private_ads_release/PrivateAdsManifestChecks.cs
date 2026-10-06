// Isolated positive fixture. Real tracked approvals remain false/null.
using System;
using AD.Advertising;
namespace UnityEngine
{
    public enum RuntimePlatform { Android, WindowsEditor }
    public static class Application { public static bool isEditor, isBatchMode; public static RuntimePlatform platform = RuntimePlatform.Android; public static string identifier = "com.synthetic.contract"; }
    public static class Debug { public static bool isDebugBuild; }
    public sealed class TextAsset { public byte[] bytes; }
    public static class Resources
    {
        public static byte[] Fixture;
        public static int Reads;
        public static T[] LoadAll<T>(string name) { Reads++; return new T[] { (T)(object)new TextAsset { bytes = Fixture } }; }
    }
    public class AndroidJavaObject : IDisposable
    {
        public T Call<T>(string name, params object[] args)
        { return typeof(T) == typeof(string) ? (T)(object)AndroidJavaClass.AppId : (T)(object)new AndroidJavaObject(); }
        public T Get<T>(string name) { return (T)(object)new AndroidJavaObject(); }
        public void Dispose() { }
    }
    public class AndroidJavaClass : AndroidJavaObject
    {
        public static int Reads;
        public static bool Throw;
        public static string AppId = "ca-app-pub-1111111111111111~2222222222";
        public AndroidJavaClass(string name) { Reads++; if (Throw) throw new Exception("Synthetic JNI failure"); }
        public T GetStatic<T>(string name) { return (T)(object)new AndroidJavaObject(); }
    }
}
internal static class PrivateAdsManifestChecks
{
    public static int Main(string[] args)
    {
        UnityEngine.Resources.Fixture = Convert.FromBase64String(SyntheticApprovedBytes.Base64);
        var scenario = args[0];
        const string unit = "ca-app-pub-1111111111111111/3333333333";
        if (scenario == "manifestMismatch") UnityEngine.AndroidJavaClass.AppId = "ca-app-pub-4444444444444444~2222222222";
        if (scenario == "throw") UnityEngine.AndroidJavaClass.Throw = true;
        bool first = PrivateAdsReleaseContract.AllowsCurrentAndroidRelease(unit, AgeChoice.Adult);
        bool expected = scenario != "manifestMismatch" && scenario != "throw";
        bool pass = first == expected;
        // On exception even a now-working JNI provider must never be retried.
        UnityEngine.AndroidJavaClass.Throw = false;
        UnityEngine.AndroidJavaClass.AppId = "ca-app-pub-1111111111111111~2222222222";
        bool second = PrivateAdsReleaseContract.AllowsCurrentAndroidRelease(unit, AgeChoice.Adult);
        pass &= second == expected && UnityEngine.AndroidJavaClass.Reads == 1;
        if (scenario == "context")
        {
            pass &= !PrivateAdsReleaseContract.AllowsCurrentAndroidRelease("ca-app-pub-1111111111111111/9999999999", AgeChoice.Adult);
            UnityEngine.Application.identifier = "com.synthetic.other";
            pass &= !PrivateAdsReleaseContract.AllowsCurrentAndroidRelease(unit, AgeChoice.Adult);
            pass &= !PrivateAdsReleaseContract.AllowsCurrentAndroidRelease(unit, AgeChoice.Declined);
            pass &= !PrivateAdsReleaseContract.AllowsCurrentAndroidRelease(unit, AgeChoice.Under13);
        }
        pass &= UnityEngine.AndroidJavaClass.Reads == 1 && UnityEngine.Resources.Reads == 1;
        Console.WriteLine("{\"scenario\":\"" + scenario + "\",\"passed\":" + pass.ToString().ToLowerInvariant() +
            ",\"fakeJniReads\":" + UnityEngine.AndroidJavaClass.Reads + ",\"fakeResourceReads\":" + UnityEngine.Resources.Reads + "}");
        return pass ? 0 : 1;
    }
}
