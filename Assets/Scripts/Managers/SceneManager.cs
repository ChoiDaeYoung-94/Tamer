using System;
using System.Threading;

using UnityEngine;

using Cysharp.Threading.Tasks;

namespace AD
{
    /// <summary>
    /// Scene 관리
    /// NextScene이라는 중간 씬을 거쳐서 최종 target 씬으로 전환하는 역할
    /// </summary>
    public class SceneManager : MonoBehaviour
    {
        private AD.GameConstants.Scene _scene;
        private CancellationTokenSource _ctsGoScene;

        public void NextScene(AD.GameConstants.Scene scene)
        {
            AD.DebugLogger.Log("SceneManager", "NextScene으로 전환");

            AD.Managers.SoundM.PauseBGM();
            AD.Managers.PopupM.SetException();

            _scene = scene;
            UnityEngine.SceneManagement.SceneManager.LoadScene(AD.GameConstants.Scene.NextScene.ToString());
        }

        /// <summary>
        /// NextScene 씬에 도달 후 호출
        /// 서버 데이터 처리 및 씬 전환 작업
        /// </summary>
        public void GoScene()
        {
            AD.DebugLogger.Log("SceneManager", "GoScene() -> " + _scene.ToString() + "씬으로 전환");

            _ctsGoScene = new CancellationTokenSource();
            GoSceneAsync(_ctsGoScene.Token).Forget();
        }

        private async UniTask GoSceneAsync(CancellationToken cancellationToken)
        {
            AD.DebugLogger.Log("SceneManager", "GoSceneAsync() -> 데이터 처리 작업 진행");

            AD.Managers.DataM.UpdateLocalData(key: "null", value: "null", updateAll: true);
            AD.Managers.DataM.UpdatePlayerData();

            while (AD.Managers.ServerM.IsInProgress && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            AD.Managers.DataM.SaveLocalData();

            await LoadTargetSceneAsync(_scene, cancellationToken);

            AD.Managers.SoundM.UnpauseBGM();
        }

        private async UniTask LoadTargetSceneAsync(AD.GameConstants.Scene targetScene, CancellationToken cancellationToken)
        {
            AsyncOperation asyncOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetScene.ToString());
            asyncOp.allowSceneActivation = false;

            while (!asyncOp.isDone && !cancellationToken.IsCancellationRequested)
            {
                AD.DebugLogger.Log("SceneManager", $"{asyncOp.progress} - progress");

                if (asyncOp.progress >= 0.9f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: cancellationToken);
                    asyncOp.allowSceneActivation = true;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cancellationToken);
        }

        private void OnDestroy()
        {
            _ctsGoScene?.Cancel();
            _ctsGoScene?.Dispose();
        }
    }
}