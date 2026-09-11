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
        if (SceneManager.GetActiveScene().path == "Assets/Tests/Scenes/RevivalSmoke.unity")
            return;
        if (SceneManager.GetActiveScene().name.CompareTo("Test") == 0)
            return;

        if (SceneManager.GetActiveScene().name.CompareTo("Login") != 0)
            SceneManager.LoadScene("Login");
#endif
    }

    private void Awake()
    {
#if DEVELOPMENT_BUILD
        this._go_console.SetActive(true);
#endif
    }
}
