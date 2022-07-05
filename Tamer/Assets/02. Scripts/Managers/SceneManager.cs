using UnityEngine;

namespace AD
{
    public class SceneManager
    {
        string _str_sceneName = string.Empty;

        public void NextScene(string sceneName)
        {
            _str_sceneName = sceneName;
            UnityEngine.SceneManagement.SceneManager.LoadScene("NextScene");
        }

        public void GoScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(_str_sceneName);
            Resources.UnloadUnusedAssets();
        }
    }
}