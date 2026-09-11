using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RevivalBaselineTests
{
    [OneTimeSetUp] public void InitializeBatchFixture()
    {
        // The standalone test runner creates a transient dirty scene. It owns this
        // batch process; live Pipeline tests must instead preserve the user's setup.
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene("Assets/Tests/Scenes/RevivalSmoke.unity", OpenSceneMode.Single);
    }

    static void RestoreSetup(SceneSetup[] setup)
    {
        if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
    static void Invoke(string method)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RevivalBuild")).First(t => t != null);
        try { type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }

    [Test] public void Revival_BaselineSettingsAndMarkersAreSafe() => Invoke("ValidateBaseline");

    [Test] public void Revival_DirtyAdditiveSceneIsPreservedWhenOperationsReject()
    {
        Invoke("RequireSavedScenes");
        var active = SceneManager.GetActiveScene();
        var unsaved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var sentinel = new GameObject("Unsaved user change");
        SceneManager.MoveGameObjectToScene(sentinel, unsaved);
        EditorSceneManager.MarkSceneDirty(unsaved);
        SceneManager.SetActiveScene(active);
        try
        {
            foreach (var method in new[] { "PrepareSmokeScene", "VerifySmokeScene", "RequireSavedScenes" })
            {
                Assert.Throws<InvalidOperationException>(() => Invoke(method));
                Assert.That(unsaved.isLoaded && unsaved.isDirty && sentinel != null, Is.True);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            }
        }
        finally { EditorSceneManager.CloseScene(unsaved, true); }
    }

    [Test] public void Revival_SmokeSceneSurvivesSaveAndReopenWithoutGameplay()
    {
        Invoke("RequireSavedScenes");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try { Invoke("PrepareSmokeScene"); Invoke("VerifySmokeScene"); }
        finally { RestoreSetup(setup); }
    }

    [Test] public void Revival_EnabledGameScenesHaveNoMissingScripts()
    {
        Invoke("RequireSavedScenes");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                            Is.Zero, entry.path + ": " + transform.name);
            }
        }
        finally { RestoreSetup(setup); }
    }
}
