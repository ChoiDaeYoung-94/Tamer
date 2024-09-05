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
        Coroutine _co_UpdateData = null;
        Coroutine _co_GoScene = null;

        public void NextScene(AD.Define.Scenes scene)
        {
            AD.Debug.Log("SceneManager", "NextScene으로 전환");

            AD.Managers.PopupM.SetException();

            _scene = scene;
            UnityEngine.SceneManagement.SceneManager.LoadScene(AD.Define.Scenes.NextScene.ToString());
        }

        /// <summary>
        /// NextScene씬에 도달 후 호출
        /// PlayerData 정리 후 씬 이동
        /// </summary>
        public void GoScene()
        {
            AD.Debug.Log("SceneManager", "GoScene() -> " + _scene.ToString() + "씬으로 전환");

            _co_UpdateData = StartCoroutine(Co_UpdateData());
        }

        IEnumerator Co_UpdateData()
        {
            AD.Debug.Log("SceneManager", "Co_UpdateData() -> 데이터 처리 작업 진행");

            AD.Managers.DataM.UpdateLocalData(key: "null", value: "null", all: true);
            AD.Managers.DataM.UpdatePlayerData();
            AD.Managers.DataM.SaveLocalData();

            while (AD.Managers.ServerM.isInprogress)
                yield return null;

            if (_co_UpdateData != null)
            {
                StopCoroutine(_co_UpdateData);
                _co_UpdateData = null;

                _co_GoScene = StartCoroutine(Co_GoScene());
            }
        }


        IEnumerator Co_GoScene()
        {
            AsyncOperation ao = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(_scene.ToString());

            ao.allowSceneActivation = false;

            while (!ao.isDone)
            {
                AD.Debug.Log("SceneManager", $"{ao.progress} - progress");

                if (ao.progress >= 0.9f)
                {
                    yield return new WaitForSeconds(2f);

                    ao.allowSceneActivation = true;
                }

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