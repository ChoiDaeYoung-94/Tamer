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

        private sealed class SceneServices
        {
            private readonly Managers _owner;
            public readonly DataManager Data;
            public readonly ServerManager Server;
            public readonly SoundManager Sound;

            public SceneServices(Managers owner)
            {
                _owner = owner;
                if (owner == null || Managers.Instance != owner) return;
                Data = Managers.DataM;
                Server = Managers.ServerM;
                Sound = Managers.SoundM;
            }

            public bool IsCurrent => _owner != null && Managers.Instance == _owner &&
                Data != null && Sound != null && Server != null &&
                ReferenceEquals(Managers.DataM, Data) && ReferenceEquals(Managers.ServerM, Server) &&
                ReferenceEquals(Managers.SoundM, Sound);
        }

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
                var services = new SceneServices(Managers.Instance);
                if (!services.IsCurrent) return;
                services.Data.UpdateLocalData(key: "null", value: "null", updateAll: true);
                if (!services.IsCurrent) return;
                services.Data.UpdatePlayerData();
                if (!await WaitForServerAsync(services, cancellationToken)) return;
                services.Data.SaveLocalData();
                await LoadTargetSceneAsync(targetScene, cancellationToken, () => services.IsCurrent);
                if (this != null && services.IsCurrent)
                    services.Sound.UnpauseBGM();
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

        private async UniTask<bool> WaitForServerAsync(SceneServices services, CancellationToken cancellationToken)
        {
            await UniTask.WaitUntil(() => !services.IsCurrent || !services.Server.IsInProgress,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return this != null && services.IsCurrent;
        }

        private async UniTask LoadTargetSceneAsync(AD.GameConstants.Scene targetScene, CancellationToken cancellationToken,
            Func<bool> ownsServices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ownsServices()) return;
            // Wait before starting native work. A cancelled operation must never leave
            // allowSceneActivation=false blocking Unity's subsequent scene operations.
            await UniTask.Delay(TimeSpan.FromSeconds(2), ignoreTimeScale: true,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ownsServices()) return;
            await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetScene.ToString())
                .ToUniTask(cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ownsServices()) return;
            await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cancellationToken);
        }

        private void OnDestroy()
        {
            _ctsGoScene?.Cancel();
            // The async owner disposes its source in finally.
        }
    }
}