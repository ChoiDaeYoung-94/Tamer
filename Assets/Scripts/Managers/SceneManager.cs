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
        public bool IsTransitioning { get; private set; }

        public void NextScene(AD.GameConstants.Scene scene)
        {
            if (IsTransitioning) return;
            IsTransitioning = true;
            AD.DebugLogger.Log("SceneManager", "NextScene으로 전환");

            try
            {
                AD.Managers.SoundM.PauseBGM();
                AD.Managers.PopupM.SetException();
                _scene = scene;
                UnityEngine.SceneManagement.SceneManager.LoadScene(AD.GameConstants.Scene.NextScene.ToString());
            }
            catch
            {
                IsTransitioning = false;
                throw;
            }
        }

        /// <summary>
        /// NextScene 씬에 도달 후 호출
        /// 서버 데이터 처리 및 씬 전환 작업
        /// </summary>
        public void GoScene()
        {
            if (_ctsGoScene != null) return;
            IsTransitioning = true;
            AD.DebugLogger.Log("SceneManager", "GoScene() -> " + _scene.ToString() + "씬으로 전환");

            _ctsGoScene = new CancellationTokenSource();
            GoSceneAsync(_scene, _ctsGoScene).Forget();
        }

        private async UniTask GoSceneAsync(AD.GameConstants.Scene targetScene, CancellationTokenSource source)
        {
            var cancellationToken = source.Token;
            try
            {
                AD.Managers.DataM.UpdateLocalData(key: "null", value: "null", updateAll: true);
                AD.Managers.DataM.UpdatePlayerData();
                await UniTask.WaitUntil(() => !AD.Managers.ServerM.IsInProgress,
                    cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                AD.Managers.DataM.SaveLocalData();
                await LoadTargetSceneAsync(targetScene, cancellationToken);
                if (this != null && AD.Managers.Instance != null)
                    AD.Managers.SoundM.UnpauseBGM();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Destruction cancels the owner, not Unity's native scene operation.
            }
            finally
            {
                if (_ctsGoScene == source)
                {
                    _ctsGoScene = null;
                    IsTransitioning = false;
                }
                source.Dispose();
            }
        }

        private async UniTask LoadTargetSceneAsync(AD.GameConstants.Scene targetScene, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Wait before starting native work. A cancelled operation must never leave
            // allowSceneActivation=false blocking Unity's subsequent scene operations.
            await UniTask.Delay(TimeSpan.FromSeconds(2), ignoreTimeScale: true,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetScene.ToString())
                .ToUniTask(cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cancellationToken);
        }

        private void OnDestroy()
        {
            _ctsGoScene?.Cancel();
            // The async owner disposes its source in finally.
        }
    }
}