// _go_console never used warning
#pragma warning disable 0414

using UnityEngine;
using UnityEngine.SceneManagement;

public class RuntimeInitialize : MonoBehaviour
{
    [SerializeField] GameObject _go_console = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void FirstLoad()
    {
#if TAMER_REVIVAL_SMOKE
        return;
#endif
#if UNITY_EDITOR
        var scene = SceneManager.GetActiveScene();
        if (!CanKeepEditorScene(scene.name, scene.path))
            SceneManager.LoadScene("Login");
#endif
    }

#if UNITY_EDITOR
    private static bool CanKeepEditorScene(string name, string path)
    {
        return name == "Login" || name == "Test" ||
            path == "Assets/Tests/Scenes/RevivalSmoke.unity" ||
            path == "Assets/Tests/Scenes/RevivalAdHarness.unity" ||
            path == "Assets/QuadD/Scenes/QuadDIntro.unity";
    }
#endif

    private void Awake()
    {
#if DEVELOPMENT_BUILD
        this._go_console.SetActive(true);
#endif
    }
}
