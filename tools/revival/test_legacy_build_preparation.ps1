$ErrorActionPreference = 'Stop'
# Compile/run only the pure helper with synthetic paths/defines. No Unity or signing tools.
$source = Join-Path $PSScriptRoot '../../Assets/Scripts/Editor/LegacyBuildPreparation.cs'
$helperCode = Get-Content -LiteralPath $source -Raw
$checksCode = @'
public static class PreparationChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "tamer-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string marker = Path.Combine(root, "aab.txt"), completion = Path.Combine(root, "done.txt");
        string defines = "USER_BEFORE";
        Func<string> read = () => defines;
        Action<string> write = value => defines = value;
        Func<LegacyBuildPreparation> capture = () => LegacyBuildPreparation.Capture(
            marker, completion, defines, "RELEASE", marker, completion);
        try
        {
            // Pre-existing markers are never overwritten and no defines are changed.
            File.WriteAllText(marker, "legacy-owner");
            bool rejected = false;
            try { capture(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && File.ReadAllText(marker) == "legacy-owner" && defines == "USER_BEFORE", "existing marker");
            File.Delete(marker);

            var pending = capture();
            pending.WriteMarker(); pending.DefinesApplied = true; defines = pending.AppliedDefines;
            // Simulate persistence across reload by reconstructing only the serialized fields.
            var resumed = new LegacyBuildPreparation {
                MarkerPath = pending.MarkerPath, CompletionMarkerPath = pending.CompletionMarkerPath,
                Token = pending.Token, PreviousDefines = pending.PreviousDefines,
                AppliedDefines = pending.AppliedDefines, DefinesApplied = pending.DefinesApplied
            };
            resumed.RequireMarker();
            resumed.Finish(false, read, write); // failed second validation, before build starts
            Check(!File.Exists(marker) && defines == "USER_BEFORE", "reload/validation rollback");

            pending = capture(); pending.WriteMarker(); pending.DefinesApplied = true; defines = "RELEASE";
            pending.WriteCompletionMarker(); pending.Finish(false, read, write); // cancelled/failed build
            Check(!File.Exists(marker) && !File.Exists(completion) && defines == "USER_BEFORE", "cancellation cleanup");

            pending = capture(); pending.WriteMarker(); pending.DefinesApplied = true; defines = "OTHER_USER_EDIT";
            File.WriteAllText(marker, "replacement-owner"); File.WriteAllText(completion, "other-build");
            pending.Finish(false, read, write);
            Check(defines == "OTHER_USER_EDIT" && File.ReadAllText(marker) == "replacement-owner"
                && File.ReadAllText(completion) == "other-build", "foreign changes preserved");
            File.Delete(marker); File.Delete(completion);

            pending = capture(); pending.WriteMarker(); pending.DefinesApplied = true; defines = "RELEASE";
            pending.WriteCompletionMarker(); pending.Finish(true, read, write);
            Check(defines == "RELEASE" && !File.Exists(marker) && !File.Exists(completion), "success retains release defines");
            pending.Finish(true, read, write); // a second cleanup cannot re-arm or undo success
            Check(defines == "RELEASE", "idempotent completion");

            defines = "BEFORE_RACE"; pending = capture(); File.WriteAllText(marker, "race-owner");
            rejected = false;
            try { pending.WriteMarker(); } catch (IOException) { rejected = true; }
            pending.Finish(false, read, write);
            Check(rejected && File.ReadAllText(marker) == "race-owner" && defines == "BEFORE_RACE", "exclusive marker race");
            Console.WriteLine("PASS: 6 synthetic preparation checks; Unity/signing/build executions: 0");
        }
        finally { Directory.Delete(root, true); }
    }
}
'@
Add-Type -TypeDefinition ($helperCode + "`n" + $checksCode)
[PreparationChecks]::Run()
