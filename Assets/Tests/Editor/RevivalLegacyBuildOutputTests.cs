using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class RevivalLegacyBuildOutputTests
{
    private string root, info, finish, outputs;
    private readonly DateTime epoch = new DateTime(2023, 1, 21);

    [SetUp]
    public void PrepareSyntheticFiles()
    {
        root = Path.Combine(Path.GetTempPath(), "tamer-output-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        info = Path.Combine(root, "buildinfo.txt");
        finish = Path.Combine(root, "finish.txt");
        outputs = Path.Combine(root, "outputs");
        File.WriteAllText(info, "1,2,26");
    }

    [TearDown]
    public void RemoveOnlyOwnedSyntheticDirectory()
    {
        if (root == null || !Directory.Exists(root)) return;
        string absolute = Path.GetFullPath(root);
        Assert.That(Path.GetDirectoryName(absolute), Is.EqualTo(
            Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).IgnoreCase);
        Assert.That(Path.GetFileName(absolute), Does.StartWith("tamer-output-tests-"));
        Assert.That((File.GetAttributes(absolute) & FileAttributes.ReparsePoint) == 0, Is.True);
        Directory.Delete(absolute, true);
    }

    // 테스트 asmdef는 기본 Editor assembly를 직접 참조하지 않으므로 실제 로드된 helper를 찾는다.
    private object Capture(bool aab, DateTime? now = null)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("LegacyBuildOutputPlan")).FirstOrDefault(found => found != null);
        Assert.That(type, Is.Not.Null, "현재 Editor에 새 helper가 컴파일되어야 합니다.");
        return Invoke(type.GetMethod("Capture"), null,
            info, finish, outputs, "1.0.", aab, now ?? epoch.AddDays(8), epoch);
    }

    private static object Invoke(MethodInfo method, object target, params object[] arguments)
    {
        Assert.That(method, Is.Not.Null);
        try { return method.Invoke(target, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static string Output(object plan) => (string)plan.GetType().GetProperty("OutputPath").GetValue(plan);
    private static string[] Apply(object plan) => (string[])Invoke(plan.GetType().GetMethod("ApplyVersion"), plan);
    private static void RequireAbsent(object plan) => Invoke(plan.GetType().GetMethod("RequireOutputAbsent"), plan);

    [TestCase(true)]
    [TestCase(false)]
    public void ExistingOutputRejectsBeforeVersionWrite(bool aab)
    {
        string output = Output(Capture(aab));
        Directory.CreateDirectory(outputs);
        byte[] sentinel = { 0, 255, 128, 13, 10 };
        File.WriteAllBytes(output, sentinel);
        Assert.Throws<InvalidOperationException>(() => Capture(aab));
        CollectionAssert.AreEqual(sentinel, File.ReadAllBytes(output));
        Assert.That(File.ReadAllText(info), Is.EqualTo("1,2,26"));
        Assert.That(File.Exists(finish), Is.False);
    }

    [Test]
    public void LateOutputRejectsWithoutApplyingCapturedVersion()
    {
        var plan = Capture(true);
        Directory.CreateDirectory(outputs);
        File.WriteAllText(Output(plan), "other-output");
        Assert.Throws<InvalidOperationException>(() => Apply(plan));
        Assert.That(File.ReadAllText(info), Is.EqualTo("1,2,26"));
        Assert.That(File.Exists(finish), Is.False);
        Assert.That(File.ReadAllText(Output(plan)), Is.EqualTo("other-output"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ApplyUsesPlannedVersionAndKeepsApkCode(bool aab)
    {
        var plan = Capture(aab);
        CollectionAssert.AreEqual(new[] { "1", "3", aab ? "27" : "26" }, Apply(plan));
        Assert.That(Path.GetFileName(Output(plan)), Is.EqualTo("1.0.1.3" + (aab ? ".aab" : ".apk")));
        Assert.That(File.Exists(finish), Is.EqualTo(aab));
        Assert.That(File.Exists(Output(plan)), Is.False, "합성 helper 시험은 BuildPipeline을 호출하지 않습니다.");
    }

    [Test]
    public void ExecutionSnapshotAfterWeekBoundaryUsesOneVersion()
    {
        var preview = Capture(true, epoch.AddDays(14).AddTicks(-1));
        var execution = Capture(true, epoch.AddDays(14));
        Assert.That(Output(execution), Is.Not.EqualTo(Output(preview)));
        CollectionAssert.AreEqual(new[] { "2", "0", "27" }, Apply(execution));
        Assert.That(Path.GetFileName(Output(execution)), Is.EqualTo("1.0.2.0.aab"));
    }

    [Test]
    public void FinishMarkerReusesOriginalVersionBytes()
    {
        File.WriteAllText(info, "01,02,026");
        byte[] marker = { 255, 0, 128, 1 };
        File.WriteAllBytes(finish, marker);
        var plan = Capture(true, epoch.AddDays(70));
        CollectionAssert.AreEqual(new[] { "01", "02", "026" }, Apply(plan));
        Assert.That(File.ReadAllText(info), Is.EqualTo("01,02,026"));
        CollectionAssert.AreEqual(marker, File.ReadAllBytes(finish));
        Assert.That(Path.GetFileName(Output(plan)), Is.EqualTo("1.0.01.02.aab"));
    }

    [Test]
    public void FinalGuardPreservesLateOutputWithoutClaimingVersionRollback()
    {
        var plan = Capture(true);
        Apply(plan);
        Directory.CreateDirectory(outputs);
        File.WriteAllText(Output(plan), "late-output");
        Assert.Throws<InvalidOperationException>(() => RequireAbsent(plan));
        Assert.That(File.ReadAllText(Output(plan)), Is.EqualTo("late-output"));
        Assert.That(File.ReadAllText(info), Is.EqualTo("1,3,27"));
    }
}
