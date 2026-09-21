using System;
using System.Text;
using AD.Privacy;
using UnityEngine;

namespace AD
{
    /// <summary>App-UID AndroidKeyStore keys only. No key export, PlayerPrefs, disk fallback, or network.</summary>
    public sealed class AndroidDeletionReceiptKeys : IDeletionReceiptKeys
    {
        private static void ValidateAlias(string alias)
        {
            const string prefix = "tamer.deletion.receipt.";
            if (alias == null || !alias.StartsWith(prefix, StringComparison.Ordinal) || alias.Length != prefix.Length + 32)
                throw new InvalidOperationException("Receipt key is unavailable.");
            foreach (char c in alias.Substring(prefix.Length))
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) throw new InvalidOperationException();
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject Open()
        {
            using (var type = new AndroidJavaClass("java.security.KeyStore"))
            {
                var store = type.CallStatic<AndroidJavaObject>("getInstance", "AndroidKeyStore");
                store.Call("load", new object[] { null, null });
                return store;
            }
        }
#endif
        public void Create(string alias)
        {
            ValidateAlias(alias);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var store = Open())
            {
                if (store.Call<bool>("containsAlias", alias)) return;
                using (var generatorClass = new AndroidJavaClass("javax.crypto.KeyGenerator"))
                using (var generator = generatorClass.CallStatic<AndroidJavaObject>("getInstance", "HmacSHA256", "AndroidKeyStore"))
                using (var builder = new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder", alias, 4 | 8))
                {
                    builder.Call<AndroidJavaObject>("setKeySize", 256).Dispose();
                    builder.Call<AndroidJavaObject>("setUserAuthenticationRequired", false).Dispose();
                    using (var spec = builder.Call<AndroidJavaObject>("build")) generator.Call("init", spec);
                    using (var key = generator.Call<AndroidJavaObject>("generateKey"))
                        if (key.Call<byte[]>("getEncoded") != null) throw new InvalidOperationException("Exportable receipt keys are forbidden.");
                }
            }
#else
            throw new PlatformNotSupportedException("Protected receipt storage is unavailable.");
#endif
        }
        public string Sign(string alias, string message)
        {
            ValidateAlias(alias);
            if (message == null || message.Length > 4096) throw new InvalidOperationException();
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var store = Open())
            {
                if (!store.Call<bool>("containsAlias", alias)) throw new InvalidOperationException("Receipt key was lost.");
                using (var key = store.Call<AndroidJavaObject>("getKey", alias, null))
                using (var macClass = new AndroidJavaClass("javax.crypto.Mac"))
                using (var mac = macClass.CallStatic<AndroidJavaObject>("getInstance", "HmacSHA256"))
                {
                    if (key == null || key.Call<byte[]>("getEncoded") != null) throw new InvalidOperationException();
                    mac.Call("init", key);
                    var result = mac.Call<byte[]>("doFinal", Encoding.UTF8.GetBytes(message));
                    return BitConverter.ToString(result).Replace("-", "").ToLowerInvariant();
                }
            }
#else
            throw new PlatformNotSupportedException("Protected receipt storage is unavailable.");
#endif
        }
        public void Delete(string alias)
        {
            ValidateAlias(alias);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var store = Open()) store.Call("deleteEntry", alias);
#else
            throw new PlatformNotSupportedException("Protected receipt storage is unavailable.");
#endif
        }
    }
}
