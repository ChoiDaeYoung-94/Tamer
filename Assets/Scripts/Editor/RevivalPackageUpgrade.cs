using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>Explicit, pinned SDK upgrade. Never runs on editor startup.</summary>
public static class RevivalPackageUpgrade
{
    static AddAndRemoveRequest request;
    static double deadline;

    [InitializeOnLoadMethod]
    static void ResumeAfterPackageReload()
    {
        // UPM may reload assemblies before AddAndRemove's poll callback runs.
        // Resume only for this explicit command, never for ordinary editor startup.
        if (!Environment.GetCommandLineArgs().Contains("RevivalPackageUpgrade.Install")) return;
        EditorApplication.delayCall += () =>
        {
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            if (!packages.Any(p => p.name == "com.unity.purchasing" && p.version == "5.4.3")) return;
            Directory.CreateDirectory("Logs/revival");
            File.WriteAllLines("Logs/revival/upm-resolved.txt", packages.Select(p => p.name + "@" + p.version));
            Debug.Log("REVIVAL_UPM_RESOLVED_AFTER_RELOAD");
            EditorApplication.Exit(0);
        };
    }

    public static void Install()
    {
        request = Client.AddAndRemove(new[] { "com.unity.purchasing@5.4.3" });
        deadline = EditorApplication.timeSinceStartup + 900;
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (!request.IsCompleted)
        {
            if (EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            Debug.LogError("REVIVAL_UPM_TIMEOUT");
            EditorApplication.Exit(2);
            return;
        }
        EditorApplication.update -= Poll;
        if (request.Status != StatusCode.Success)
        {
            Debug.LogError("REVIVAL_UPM_FAILED: " + request.Error?.message);
            EditorApplication.Exit(1);
            return;
        }
        Directory.CreateDirectory("Logs/revival");
        File.WriteAllLines("Logs/revival/upm-resolved.txt", request.Result.Select(p => p.name + "@" + p.version));
        Debug.Log("REVIVAL_UPM_RESOLVED");
        EditorApplication.Exit(0);
    }
}
