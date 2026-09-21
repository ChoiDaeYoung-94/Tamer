using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>Compile Android player scripts without packaging or running the game.</summary>
public static class RevivalAndroidScriptCompile
{
    [Serializable]
    private sealed class Evidence
    {
        public string unityVersion;
        public string target;
        public string options;
        public string outputDirectory;
        public string[] loginAssemblyDefines;
        public string[] assemblies;
    }

    public static void Compile()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Start this command with -buildTarget Android.");
        var loginAssembly = CompilationPipeline.GetAssemblies(AssembliesType.Player)
            .Single(a => a.sourceFiles.Any(p => p.Replace('\\', '/').EndsWith("/Login/Login.cs", StringComparison.Ordinal)));
        var defines = loginAssembly.defines.OrderBy(d => d).ToArray();
        if (!defines.Contains("UNITY_ANDROID") || defines.Contains("UNITY_EDITOR")
            || defines.Any(d => d.StartsWith("TAMER_", StringComparison.Ordinal) && d.EndsWith("_HARNESS", StringComparison.Ordinal)))
            throw new InvalidOperationException("Expected ordinary Android player compilation without Editor or harness defines.");

        var output = Path.GetFullPath(Path.Combine("Logs/revival/android-script-compile",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        var settings = new ScriptCompilationSettings
        {
            target = BuildTarget.Android,
            group = BuildTargetGroup.Android,
            options = ScriptCompilationOptions.None,
            extraScriptingDefines = Array.Empty<string>()
        };
        var result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
        if (result.assemblies == null || !result.assemblies.Any(p => Path.GetFileName(p) == loginAssembly.name + ".dll")
            || !File.Exists(Path.Combine(output, loginAssembly.name + ".dll")))
            throw new InvalidOperationException("Android script compilation produced no login assembly.");
        var evidence = new Evidence
        {
            unityVersion = Application.unityVersion,
            target = settings.target.ToString(),
            options = settings.options.ToString(),
            outputDirectory = output,
            loginAssemblyDefines = defines,
            assemblies = result.assemblies.ToArray()
        };
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(evidence, true));
        Debug.Log("Android player script compilation passed: " + output);
    }
}
