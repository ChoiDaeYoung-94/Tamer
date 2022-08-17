using System.Collections;

using UnityEngine;

namespace AD
{
    public class SceneManager : MonoBehaviour
    {
        AD.Define.Scenes _scene;
        Coroutine _co_GoScene = null;

        float progress = 0f;
        public float Progress { get { return progress; } }

        public void NextScene(AD.Define.Scenes scene)
        {
            this._scene = scene;
            UnityEngine.SceneManagement.SceneManager.LoadScene(AD.Define.Scenes.NextScene.ToString());
        }

        public void GoScene()
        {
            _co_GoScene = StartCoroutine(Co_GoScene());
        }

        IEnumerator Co_GoScene()
        {
            AsyncOperation ao = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(this._scene.ToString());

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

                Resources.UnloadUnusedAssets();
            }
        }
    }
}