using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public class RevivalUploadSigningTests
{
    private const string Marker = "Build/AOSSettingAAB.txt";
    private const string BuildInfo = "BuildInfo/buildinfo.txt";
    private static readonly string[] EnvironmentNames = {
        "TAMER_UPLOAD_KEYSTORE_PATH", "TAMER_UPLOAD_KEY_ALIAS", "TAMER_UPLOAD_CERT_SHA256",
        "TAMER_KEYSTORE_PASS", "TAMER_KEYALIAS_PASS"
    };
    private string[] savedEnvironment;

    [SetUp]
    public void SaveEnvironment()
    {
        savedEnvironment = EnvironmentNames.Select(Environment.GetEnvironmentVariable).ToArray();
    }

    [TearDown]
    public void RestoreEnvironment()
    {
        for (int index = 0; index < EnvironmentNames.Length; index++)
            Environment.SetEnvironmentVariable(EnvironmentNames[index], savedEnvironment[index]);
    }

    private static void InvokeAabMenu()
    {
        var method = FindBuildScript().GetMethod("SetAOS", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        try { method.Invoke(null, new object[] { Marker }); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static BuildFailedException RejectWithoutChangingProject(Action configure)
    {
        Assert.That(File.Exists(Marker), Is.False, "기존 빌드 표시 파일이 있으면 검사를 시작하지 않습니다.");
        byte[] beforeVersion = File.Exists(BuildInfo) ? File.ReadAllBytes(BuildInfo) : null;
        bool beforeCustom = PlayerSettings.Android.useCustomKeystore;
        string beforeKeystore = PlayerSettings.Android.keystoreName;
        string beforeAlias = PlayerSettings.Android.keyaliasName;
        string beforeStorePassword = PlayerSettings.Android.keystorePass;
        string beforeAliasPassword = PlayerSettings.Android.keyaliasPass;

        configure();
        BuildFailedException error = Assert.Throws<BuildFailedException>(InvokeAabMenu);

        Assert.That(File.Exists(Marker), Is.False);
        byte[] afterVersion = File.Exists(BuildInfo) ? File.ReadAllBytes(BuildInfo) : null;
        Assert.That((beforeVersion == null && afterVersion == null)
            || (beforeVersion != null && afterVersion != null && beforeVersion.SequenceEqual(afterVersion)), Is.True);
        Assert.That(PlayerSettings.Android.useCustomKeystore == beforeCustom, Is.True);
        Assert.That(string.Equals(PlayerSettings.Android.keystoreName, beforeKeystore), Is.True);
        Assert.That(string.Equals(PlayerSettings.Android.keyaliasName, beforeAlias), Is.True);
        // 실패 메시지에 이전 Editor 비밀번호가 출력되지 않도록 bool만 비교한다.
        Assert.That(string.Equals(PlayerSettings.Android.keystorePass, beforeStorePassword), Is.True);
        Assert.That(string.Equals(PlayerSettings.Android.keyaliasPass, beforeAliasPassword), Is.True);
        return error;
    }

    [Test]
    public void Revival_AabMenuRejectsMissingUploadInputsBeforeChangingSettings()
    {
        var error = RejectWithoutChangingProject(() => {
            foreach (string name in EnvironmentNames)
                Environment.SetEnvironmentVariable(name, null);
        });
        Assert.That(error.Message.Contains("모두 필요"), Is.True);
    }

    [Test]
    public void Revival_AabMenuRejectsGitTrackedKeyPathBeforeChangingSettings()
    {
        var error = RejectWithoutChangingProject(() => {
            Environment.SetEnvironmentVariable(EnvironmentNames[0],
                Path.Combine(Path.GetDirectoryName(Application.dataPath), "Assets/Scripts/Editor/BuildScript.cs"));
            SetOtherInputs();
        });
        Assert.That(error.Message.Contains("Git 작업 폴더 밖"), Is.True);
    }

    [Test]
    public void Revival_AabMenuRejectsInvalidExternalKeystoreBeforeChangingSettings()
    {
        string outsideFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(outsideFile, "not a keystore");
            var error = RejectWithoutChangingProject(() => {
                Environment.SetEnvironmentVariable(EnvironmentNames[0], outsideFile);
                SetOtherInputs();
            });
            Assert.That(error.Message.Contains("공개 인증서"), Is.True);
        }
        finally { File.Delete(outsideFile); }
    }

    private static void SetOtherInputs()
    {
        Environment.SetEnvironmentVariable(EnvironmentNames[1], "synthetic-test");
        Environment.SetEnvironmentVariable(EnvironmentNames[2], new string('0', 64));
        byte[] random = new byte[24];
        using (var generator = RandomNumberGenerator.Create())
            generator.GetBytes(random);
        string temporaryPassword = Convert.ToBase64String(random);
        Environment.SetEnvironmentVariable(EnvironmentNames[3], temporaryPassword);
        Environment.SetEnvironmentVariable(EnvironmentNames[4], temporaryPassword);
    }

    [Test]
    public void Revival_AabPreflightMatchesOnlyTheSyntheticCertificate()
    {
        string folder = Path.Combine(Path.GetTempPath(), "TamerUploadSigningTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string keystore = Path.Combine(folder, "synthetic.jks");
        string certificate = Path.Combine(folder, "synthetic.der");
        try
        {
            SetOtherInputs();
            Environment.SetEnvironmentVariable(EnvironmentNames[0], keystore);
            string jdkRoot = UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath;
            if (string.IsNullOrEmpty(jdkRoot))
                jdkRoot = Path.Combine(EditorApplication.applicationContentsPath,
                    "PlaybackEngines", "AndroidPlayer", "OpenJDK");
            string keytool = Path.Combine(jdkRoot, "bin",
                Application.platform == RuntimePlatform.WindowsEditor ? "keytool.exe" : "keytool");
            Assert.That(File.Exists(keytool), Is.True);

            RunKeytool(keytool, "-genkeypair -keystore \"" + keystore + "\" -storetype JKS"
                + " -storepass:env TAMER_KEYSTORE_PASS -keypass:env TAMER_KEYALIAS_PASS"
                + " -alias synthetic-test -keyalg RSA -keysize 2048 -validity 2"
                + " -dname \"CN=Local Synthetic Test, O=Tamer, C=KR\"");
            RunKeytool(keytool, "-exportcert -keystore \"" + keystore + "\""
                + " -storepass:env TAMER_KEYSTORE_PASS -alias synthetic-test -file \"" + certificate + "\"");
            using (var parsed = new X509Certificate2(File.ReadAllBytes(certificate)))
            using (var sha256 = SHA256.Create())
            {
                string digest = BitConverter.ToString(sha256.ComputeHash(parsed.RawData)).Replace("-", "");
                Environment.SetEnvironmentVariable(EnvironmentNames[2], digest);
            }

            var method = FindBuildScript().GetMethod("ValidateReleaseUploadSigning",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That(InvokePreflight(method) != null, Is.True);

            Environment.SetEnvironmentVariable(EnvironmentNames[2], new string('0', 64));
            var mismatch = Assert.Throws<BuildFailedException>(() => InvokePreflight(method));
            Assert.That(mismatch.Message.Contains("기대 SHA-256"), Is.True);
        }
        finally
        {
            if (File.Exists(certificate)) File.Delete(certificate);
            if (File.Exists(keystore)) File.Delete(keystore);
            Directory.Delete(folder, false);
        }
    }

    private static object InvokePreflight(MethodInfo method)
    {
        try { return method.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static Type FindBuildScript()
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("BuildScript"))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null);
        return type;
    }

    private static void RunKeytool(string tool, string arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo {
            FileName = tool, Arguments = arguments, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        using (var process = new System.Diagnostics.Process { StartInfo = start })
        {
            Assert.That(process.Start(), Is.True);
            Task output = process.StandardOutput.ReadToEndAsync();
            Task errors = process.StandardError.ReadToEndAsync();
            bool finished = process.WaitForExit(30000);
            if (!finished) process.Kill();
            Assert.That(finished, Is.True);
            Assert.That(Task.WaitAll(new[] { output, errors }, 5000), Is.True);
            Assert.That(process.ExitCode == 0, Is.True);
        }
    }
}
