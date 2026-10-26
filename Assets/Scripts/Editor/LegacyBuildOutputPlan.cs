using System;
using System.IO;

// Unity 의존성이 없는 legacy 메뉴 출력 계획. 호출자가 실행을 직렬화해야 한다.
public sealed class LegacyBuildOutputPlan
{
    private readonly string versionPath, finishPath, sourceVersion;
    private readonly bool isAab;
    private readonly string[] version;
    public string OutputPath { get; private set; }
    public bool ReusesVersion { get; private set; }

    private LegacyBuildOutputPlan(string versionPath, string finishPath, string outputPath,
        string sourceVersion, string[] version, bool reusesVersion, bool isAab)
    {
        this.versionPath = versionPath;
        this.finishPath = finishPath;
        this.sourceVersion = sourceVersion;
        this.version = version;
        this.isAab = isAab;
        OutputPath = outputPath;
        ReusesVersion = reusesVersion;
    }

    public static LegacyBuildOutputPlan Capture(string versionPath, string finishPath,
        string outputDirectory, string versionPrefix, bool isAab, DateTime now, DateTime epoch)
    {
        versionPath = Path.GetFullPath(versionPath);
        finishPath = Path.GetFullPath(finishPath);
        bool reuse = RegularFileOrMissing(finishPath);
        string source = File.ReadAllText(versionPath);
        string[] values = source.Split(',');
        if (values.Length != 3)
            throw new InvalidOperationException("빌드 버전 정보는 weekNumber,buildNumber,bundleVersionCode 형식이어야 합니다.");
        int previousWeek = Convert.ToInt32(values[0]);
        int previousBuild = Convert.ToInt32(values[1]);
        int previousCode = Convert.ToInt32(values[2]);
        if (!reuse)
        {
            int week = (now - epoch).Days / 7;
            int build = previousWeek == week ? unchecked(previousBuild + 1) : 0;
            int code = isAab ? unchecked(previousCode + 1) : previousCode;
            values = new[] { week.ToString(), build.ToString(), code.ToString() };
        }
        // finish가 있으면 기존 GetVersion처럼 원래 숫자 표기(예: 01)를 재사용한다.
        string output = Path.GetFullPath(Path.Combine(outputDirectory,
            versionPrefix + values[0] + "." + values[1] + (isAab ? ".aab" : ".apk")));
        var plan = new LegacyBuildOutputPlan(versionPath, finishPath, output, source, values, reuse, isAab);
        plan.RequireOutputAbsent();
        return plan;
    }

    public string[] ApplyVersion()
    {
        // 계획 이후 다른 요청이 버전 입력을 바꿨다면 그 변경을 덮어쓰지 않는다.
        if (File.ReadAllText(versionPath) != sourceVersion || RegularFileOrMissing(finishPath) != ReusesVersion)
            throw new InvalidOperationException("빌드 버전 입력이 바뀌어 기존 계획을 적용할 수 없습니다.");
        RequireOutputAbsent();
        if (!ReusesVersion)
        {
            File.WriteAllText(versionPath, string.Join(",", version));
            if (isAab)
                using (var marker = new FileStream(finishPath, FileMode.CreateNew, FileAccess.Write)) { }
        }
        return (string[])version.Clone();
    }

    public void RequireOutputAbsent()
    {
        string current = OutputPath;
        bool destination = true;
        while (!string.IsNullOrEmpty(current))
        {
            FileAttributes? attributes = AttributesOrMissing(current);
            if (attributes.HasValue)
            {
                if (destination)
                    throw new InvalidOperationException("기존 빌드 출력 파일 또는 디렉터리가 있어 새 요청을 거절합니다.");
                if ((attributes.Value & FileAttributes.Directory) == 0
                    || (attributes.Value & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("빌드 출력의 상위 경로가 일반 디렉터리가 아닙니다.");
            }
            destination = false;
            current = Path.GetDirectoryName(current);
        }
    }

    private static bool RegularFileOrMissing(string path)
    {
        FileAttributes? attributes = AttributesOrMissing(path);
        if (!attributes.HasValue) return false;
        if ((attributes.Value & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new InvalidOperationException("빌드 버전 완료 표시가 일반 파일이 아닙니다.");
        return true;
    }

    private static FileAttributes? AttributesOrMissing(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        // 접근 거절·잘못된 경로·기타 I/O 오류를 '없음'으로 처리하지 않는다.
    }
}
