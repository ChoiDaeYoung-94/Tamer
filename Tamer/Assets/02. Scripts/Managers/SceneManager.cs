using System.Collections;

using UnityEngine;

namespace AD
{
    public class SceneManager : MonoBehaviour
    {
        string _str_sceneName = string.Empty;
        Coroutine _co_GoScene = null;

        float progress = 0f;
        public float Progress { get { return progress; } } 

        public void NextScene(string sceneName)
        {
            this._str_sceneName = sceneName;
            UnityEngine.SceneManagement.SceneManager.LoadScene("NextScene");
        }

        public void GoScene()
        {
            _co_GoScene = StartCoroutine(Co_GoScene());
        }

        IEnumerator Co_GoScene()
        {
            AsyncOperation ao = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(this._str_sceneName);

            while (!ao.isDone)
            {
                AD.Debug.Log("SceneManager", $"{ao.progress} - progress");
                this.progress = ao.progress;
                yield return null;
            }

            if (_co_GoScene != null)
            {
                StopCoroutine(_co_GoScene);
                _co_GoScene = null;

                this._str_sceneName = string.Empty;

                Resources.UnloadUnusedAssets();
            }
        }
    }
}