// External read-only run_script entry; do not stage this file into Assets.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor.Build;
using UnityEngine;

public static class PrivateAdsRegistrationProbe
{
    public static object Inspect()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch inspection required.");
        var types = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
            .Select(a => a.GetType("PrivateProductionAdsBuild", false)).Where(t => t != null).ToArray();
        if (types.Length != 1) throw new InvalidOperationException("Registration type count rejected.");
        var type = types[0];
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ScriptAssemblies"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var location = Path.GetFullPath(type.Assembly.Location);
        var comparison = Application.platform == RuntimePlatform.WindowsEditor
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!location.StartsWith(directory, comparison) || !File.Exists(location) ||
            !typeof(IPreprocessBuildWithReport).IsAssignableFrom(type) ||
            !typeof(IProcessSceneWithReport).IsAssignableFrom(type) ||
            !typeof(IPostprocessBuildWithReport).IsAssignableFrom(type))
            throw new InvalidOperationException("Normal Editor assembly registration rejected.");
        var names = new[] { "OnPreprocessBuild", "OnProcessScene", "OnPostprocessBuild" };
        var methods = names.Select(name => type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance)).ToArray();
        if (methods.Any(method => method == null || method.GetMethodBody() == null))
            throw new InvalidOperationException("Callback method metadata rejected.");
        using (var sha = SHA256.Create())
            return new { normalEditorAssembly = true, interfaceCount = 3,
                methodIlSha256 = methods.Select(method => BitConverter.ToString(
                    sha.ComputeHash(method.GetMethodBody().GetILAsByteArray())).Replace("-", "").ToLowerInvariant()).ToArray(),
                callbackExecuted = false, buildExecuted = false, binaryVerified = false };
    }
}
