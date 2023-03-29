using System.Collections;

using UnityEngine;

namespace AD
{
    /// <summary>
    /// Scene 관리
    /// </summary>
    public class SceneManager : MonoBehaviour
    {
        AD.Define.Scenes _scene;
        Coroutine _co_GoScene = null;

        float progress = 0f;
        public float Progress { get { return progress; } }

        public void NextScene(AD.Define.Scenes scene)
        {
            _scene = scene;
            UnityEngine.SceneManagement.SceneManager.LoadScene(AD.Define.Scenes.NextScene.ToString());
        }

        /// <summary>
        /// NextScene씬에 도달 후 호출
        /// </summary>
        public void GoScene()
        {
            _co_GoScene = StartCoroutine(Co_GoScene());
        }

        IEnumerator Co_GoScene()
        {
            AsyncOperation ao = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(_scene.ToString());

            while (!ao.isDone)
            {
                AD.Debug.Log("SceneManager", $"{ao.progress} - progress");
                progress = ao.progress;
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