using System;
using System.IO;

// SessionState serializes this snapshot before a define-triggered domain reload.
[Serializable]
public sealed class LegacyBuildPreparation
{
    public const string MarkerPrefix = "TAMER_PREPARATION:";
    public string MarkerPath, CompletionMarkerPath, Token, PreviousDefines, AppliedDefines;
    public bool DefinesApplied, BuildStarted;

    public static LegacyBuildPreparation Capture(string marker, string completion,
        string previousDefines, string appliedDefines, params string[] blockers)
    {
        foreach (string path in blockers)
            if (File.Exists(path))
                throw new InvalidOperationException("기존 빌드 표시가 있어 새 요청을 준비할 수 없습니다.");
        return new LegacyBuildPreparation {
            MarkerPath = marker, CompletionMarkerPath = completion,
            Token = MarkerPrefix + Guid.NewGuid().ToString("N"),
            PreviousDefines = previousDefines, AppliedDefines = appliedDefines
        };
    }

    public void WriteMarker() => WriteNew(MarkerPath);
    public void WriteCompletionMarker() => WriteNew(CompletionMarkerPath);
    public bool OwnsCompletionMarker => Owns(CompletionMarkerPath);
    public static bool ShouldExitEditor(LegacyBuildPreparation preparation, bool succeeded)
        => preparation != null && succeeded && preparation.OwnsCompletionMarker;

    private void WriteNew(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream))
            writer.Write(Token);
    }

    public void RequireMarker()
    {
        if (!Owns(MarkerPath))
            throw new InvalidOperationException("빌드 준비 표시의 소유권을 확인할 수 없습니다.");
    }

    private bool Owns(string path) => File.Exists(path) && File.ReadAllText(path) == Token;

    public void Finish(bool succeeded, Func<string> readDefines, Action<string> writeDefines)
    {
        // Never delete a pre-existing/replaced marker or undo somebody else's edits.
        if (Owns(MarkerPath)) File.Delete(MarkerPath);
        if (Owns(CompletionMarkerPath)) File.Delete(CompletionMarkerPath);
        if (!succeeded && DefinesApplied && readDefines() == AppliedDefines)
            writeDefines(PreviousDefines);
    }

}
