using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class BuildScript : MonoBehaviour, IPostprocessBuildWithReport
{
    // AOS build시 필요한 data
    private const string PRODUCT_NAME = "MonsterTamer";
    private const string IDENTIFIER = "com.AeDeong.MonsterTamer";
    private const string KEYSTORE_NAME = "src/AeDeong.keystore";
    private const string KEYALIAS_NAME = "aedeong";

    // keystore 비밀번호는 저장소에 두지 않고 환경 변수로 전달받는다.
    // 환경 변수가 없으면 에디터(Player Settings)에 입력된 값을 그대로 사용한다.
    private const string ENV_KEYSTORE_PASS = "TAMER_KEYSTORE_PASS";
    private const string ENV_KEYALIAS_PASS = "TAMER_KEYALIAS_PASS";
    private static string[] DEFINESYMBOLS_APK = { "DEBUG" };
    private static string[] DEFINESYMBOLS_AAB = { "" };

    // version 자동화 관련
    private const string VERSION = "1.0.";
    private const string DAY_CALCULATEVERSION = "01/21/2023 00:00:00";
    private const string BUILDINFO_PATH = "BuildInfo/buildinfo.txt";
    private const string BUILDINFO_FINISHVERSIONSETTING = "BuildInfo/finishversionsetting.txt";
    private static string[] _str_buildInfo = null;

    // build 추출물 경로
    private const string AOS_BUILD_PATH = "Build/AOS";

    // build 구분
    private const string CHECK_AOS_SETTING_APK = "Build/AOSSettingAPK.txt";
    private const string CHECK_AOS_SETTING_AAB = "Build/AOSSettingAAB.txt";

    // build 완료 후 에디터 종료 위함
    private const string CHECK_BUILD = "Build/checkedBuilding.txt";

    [MenuItem("Build/AOS/APK")]
    static void BuildAOSAPK() => SetAOS(form: CHECK_AOS_SETTING_APK);
    [MenuItem("Build/AOS/AAB")]
    static void BuildAOSAAB() => SetAOS(form: CHECK_AOS_SETTING_AAB);

    #region AOS
    /// <summary>
    /// AOS build Setting
    /// </summary>
    /// <param name="form"></param>
    static void SetAOS(string form)
    {
        if (!Directory.Exists("Build"))
            Directory.CreateDirectory("Build");

        StreamWriter file = File.CreateText(form);
        file.Close();

        bool isAAB = form.Equals(CHECK_AOS_SETTING_AAB) ? true : false;

        if (isAAB)
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, DEFINESYMBOLS_AAB);
        else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, DEFINESYMBOLS_APK);

        CheckCI();
    }

    /// <summary>
    /// AOS Build
    /// </summary>
    /// <param name="isAAB"></param>
    static void BuildAOS(bool isAAB)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        EditorUserBuildSettings.buildAppBundle = isAAB;
        if (isAAB)
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        // Github action에서 apk, aab를 모두 빌드 할 경우 apk와 aab의 version을 맞추기 위함
        if (File.Exists(BUILDINFO_FINISHVERSIONSETTING))
            _str_buildInfo = GetVersion();
        else
            _str_buildInfo = SetVersion(isAAB: isAAB);

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, IDENTIFIER);
        PlayerSettings.bundleVersion = $"{VERSION}{_str_buildInfo[0]}";
        PlayerSettings.productName = PRODUCT_NAME;

        PlayerSettings.Android.bundleVersionCode = Convert.ToInt32(_str_buildInfo[2]);

        PlayerSettings.Android.keystoreName = KEYSTORE_NAME;
        PlayerSettings.Android.keyaliasName = KEYALIAS_NAME;
        ApplyKeystorePassword();

        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_4_6);

        string filePath = CHECK_BUILD;
        StreamWriter file = File.CreateText(filePath);
        file.Close();

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();

        string extension = isAAB == true ? ".aab" : ".apk";
        buildPlayerOptions.locationPathName = AOS_BUILD_PATH + "/" + $"{VERSION}{_str_buildInfo[0]}.{_str_buildInfo[1]}" + extension;


        if (isAAB)
        {
            buildPlayerOptions.options = BuildOptions.CompressWithLz4HC;
            buildPlayerOptions.options &= ~BuildOptions.Development;
        }
        else
            buildPlayerOptions.options = BuildOptions.CompressWithLz4 | BuildOptions.Development;

        buildPlayerOptions.scenes = GetScenes();
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.targetGroup = BuildTargetGroup.Android;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Debug.Log("AOSBuild succeeded: " + summary.totalSize + " bytes");

        if (summary.result == BuildResult.Failed)
            Debug.Log("AOSBuild failed");
    }

    /// <summary>
    /// keystore 비밀번호 적용
    /// 저장소에 비밀번호를 남기지 않기 위해 환경 변수에서 읽고,
    /// 환경 변수가 없으면 Player Settings에 입력된 값을 그대로 사용한다.
    /// (예전처럼 빈 문자열로 덮어쓰면 서명 단계에서 Cannot recover key로 실패한다)
    /// </summary>
    static void ApplyKeystorePassword()
    {
        string keystorePass = Environment.GetEnvironmentVariable(ENV_KEYSTORE_PASS);
        if (!string.IsNullOrEmpty(keystorePass))
            PlayerSettings.Android.keystorePass = keystorePass;

        string keyaliasPass = Environment.GetEnvironmentVariable(ENV_KEYALIAS_PASS);
        if (!string.IsNullOrEmpty(keyaliasPass))
            PlayerSettings.Android.keyaliasPass = keyaliasPass;

        // 비밀번호 없이 진행하면 IL2CPP까지 다 돌린 뒤 마지막 서명 단계에서 실패한다.
        // 시간 낭비를 막기 위해 빌드 시작 전에 중단시킨다.
        if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass)
            || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
        {
            throw new BuildFailedException(
                $"keystore 비밀번호가 설정되지 않았습니다. 환경 변수 {ENV_KEYSTORE_PASS}, {ENV_KEYALIAS_PASS}를 설정하거나 " +
                "Player Settings > Publishing Settings에 비밀번호를 입력한 뒤 다시 빌드해 주세요.");
        }
    }
    #endregion

    /// <summary>
    /// version 자동화 관련
    /// </summary>
    /// <param name="form"></param>
    /// <returns></returns>
    static string[] SetVersion(bool isAAB)
    {
        if (!File.Exists(BUILDINFO_PATH))
            Debug.LogError("빌드 버전 정보가 존재하지 않습니다.");

        string[] buildInfo = File.ReadAllText(BUILDINFO_PATH).Split(',');
        if (buildInfo.Length != 3)
            Debug.LogError("빌드 버전 정보의 형식이 잘못되었습니다.\n" +
                                "weekNumber,buildNumber,bundleVersionCode");

        TimeSpan timeSpan = DateTime.Now - Convert.ToDateTime(DAY_CALCULATEVERSION);
        int weekNumber = timeSpan.Days / 7;

        int buildNumber = 0;
        if (Convert.ToInt32(buildInfo[0]) == weekNumber)
            buildNumber = Convert.ToInt32(buildInfo[1]) + 1;

        int bundleVersionCode = Convert.ToInt32(buildInfo[2]);
        if (isAAB)
            ++bundleVersionCode;

        File.WriteAllText(path: BUILDINFO_PATH, contents: string.Format("{0},{1},{2}", weekNumber, buildNumber, bundleVersionCode));
        buildInfo = File.ReadAllText(BUILDINFO_PATH).Split(',');

        if (isAAB)
        {
            StreamWriter file = File.CreateText(BUILDINFO_FINISHVERSIONSETTING);
            file.Close();
        }

        return buildInfo;
    }

    /// <summary>
    /// version 자동화 관련
    /// </summary>
    /// <returns></returns>
    static string[] GetVersion() => File.ReadAllText(BUILDINFO_PATH).Split(',');

    /// <summary>
    /// https://docs.unity3d.com/ScriptReference/EditorBuildSettingsScene.html
    /// </summary>
    /// <returns></returns>
    private static string[] GetScenes()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        List<string> sceneList = new List<string>();

        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.enabled)
                sceneList.Add(scene.path);
        }

        return sceneList.ToArray();
    }

    static IEnumerator CheckCompiling()
    {
        while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            yield return null;

        EditorApplication.delayCall += () =>
        {
            if (File.Exists(CHECK_AOS_SETTING_APK))
            {
                File.Delete(CHECK_AOS_SETTING_APK);
                BuildAOS(isAAB: false);
            }

            if (File.Exists(CHECK_AOS_SETTING_AAB))
            {
                File.Delete(CHECK_AOS_SETTING_AAB);
                BuildAOS(isAAB: true);
            }
        };
    }

    /// <summary>
    /// https://docs.unity3d.com/ScriptReference/Callbacks.DidReloadScripts.html
    /// Build setting 후 compile이 필요한 경우를 대비
    /// 후 build
    /// </summary>
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void CheckCI()
    {
        if (File.Exists(CHECK_AOS_SETTING_APK) || File.Exists(CHECK_AOS_SETTING_AAB))
            EditorCoroutine.StartCoroutine(CheckCompiling());
    }

    /// <summary>
    /// https://docs.unity3d.com/ScriptReference/Build.IPostprocessBuildWithReport.OnPostprocessBuild.html
    /// </summary>
    public int callbackOrder { get { return 0; } }
    public void OnPostprocessBuild(BuildReport report)
    {
        if (File.Exists(CHECK_BUILD))
        {
            File.Delete(CHECK_BUILD);

            EditorApplication.delayCall += () => { EditorApplication.Exit(0); };
        }
    }
}

/// <summary>
/// https://docs.unity3d.com/kr/2022.2/Manual/com.unity.editorcoroutines.html
/// </summary>
class EditorCoroutine
{
    private IEnumerator iEnumerator = null;

    private EditorCoroutine(IEnumerator iEnumerator)
    {
        this.iEnumerator = iEnumerator;
    }

    public static EditorCoroutine StartCoroutine(IEnumerator iEnumerator)
    {
        EditorCoroutine editorCoroutine = new EditorCoroutine(iEnumerator);
        editorCoroutine.Start();

        return editorCoroutine;
    }

    private void Start()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    public void Stop() => EditorApplication.update -= Update;

    private void Update()
    {
        if (!iEnumerator.MoveNext())
            Stop();
    }
}