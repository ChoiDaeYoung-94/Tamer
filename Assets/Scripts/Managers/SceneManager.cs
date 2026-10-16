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
        private SceneRequest _request;
        private bool _requestConsumed;
        public bool IsTransitioning { get; private set; }

        private sealed class SceneRequest
        {
            public readonly AD.GameConstants.Scene Target;
            public readonly SceneServices Services;
            public SceneRequest(AD.GameConstants.Scene target)
            { Target = target; Services = new SceneServices(Managers.Instance); }
        }

        private sealed class SceneServices
        {
            private readonly Managers _owner;
            public readonly DataManager Data;
            public readonly ServerManager Server;
            public readonly SoundManager Sound;
            private readonly string _account;
            private readonly int _generation, _deletionEpoch;
            private readonly bool _requiresReady;

            public SceneServices(Managers owner)
            {
                _owner = owner;
                if (owner == null || Managers.Instance != owner) return;
                Data = Managers.DataM;
                Server = Managers.ServerM;
                Sound = Managers.SoundM;
                if (Data != null)
                {
                    _account = Data.PlayFabId; _generation = Data.AccountGeneration; _deletionEpoch = Data.DeletionEpoch;
                    // Initial hydration may start unready; an established gameplay session may not become unready.
                    _requiresReady = Data.IsServerDataReady;
                }
            }

            public bool IsCurrent => _owner != null && Managers.Instance == _owner &&
                Data != null && Sound != null && Server != null &&
                ReferenceEquals(Managers.DataM, Data) && ReferenceEquals(Managers.ServerM, Server) &&
                ReferenceEquals(Managers.SoundM, Sound) && Data.PlayFabId == _account &&
                Data.AccountGeneration == _generation && Data.DeletionEpoch == _deletionEpoch &&
                !Data.DeletionInProgress && (!_requiresReady || Data.IsServerDataReady);
        }

        public void NextScene(AD.GameConstants.Scene scene)
        {
            var request = PrepareSceneRequest(scene);
            if (request == null) return;
            AD.DebugLogger.Log("SceneManager", "NextScene으로 전환");

            try
            {
                if (!OwnsSceneRequest(request)) { CompleteSceneRequest(request); return; }
                request.Services.Sound.PauseBGM();
                if (!OwnsSceneRequest(request)) { CompleteSceneRequest(request); return; }
                AD.Managers.PopupM.SetException();
                if (!OwnsSceneRequest(request)) { CompleteSceneRequest(request); return; }
                UnityEngine.SceneManagement.SceneManager.LoadScene(AD.GameConstants.Scene.NextScene.ToString());
            }
            catch
            {
                CompleteSceneRequest(request);
                throw;
            }
        }

        // Shared by the explicit request entry and the never-requested legacy GoScene entry.
        private SceneRequest PrepareSceneRequest(AD.GameConstants.Scene scene)
        {
            if (IsTransitioning || _ctsGoScene != null) return null;
            var request = new SceneRequest(scene);
            _request = request; _requestConsumed = false; _scene = scene;
            IsTransitioning = true;
            return request;
        }

        private bool OwnsSceneRequest(SceneRequest request) => this != null &&
            ReferenceEquals(_request, request) && request.Services.IsCurrent;

        private SceneRequest TryConsumeSceneRequest()
        {
            if (_ctsGoScene != null) return null;
            // Retain completed/invalidated requests as tombstones. A late GoScene may not
            // reinterpret their target as a new session's direct-entry request.
            var request = _request ?? PrepareSceneRequest(_scene);
            if (request == null || _requestConsumed) return null;
            _requestConsumed = true;
            if (!OwnsSceneRequest(request)) { CompleteSceneRequest(request); return null; }
            return request;
        }

        private void CompleteSceneRequest(SceneRequest request)
        {
            if (!ReferenceEquals(_request, request)) return;
            _requestConsumed = true;
            IsTransitioning = false;
        }

        /// <summary>
        /// NextScene 씬에 도달 후 호출
        /// 서버 데이터 처리 및 씬 전환 작업
        /// </summary>
        public void GoScene()
        {
            var request = TryConsumeSceneRequest();
            if (request == null) return;
            AD.DebugLogger.Log("SceneManager", "GoScene() -> " + request.Target.ToString() + "씬으로 전환");

            _ctsGoScene = new CancellationTokenSource();
            GoSceneAsync(request, _ctsGoScene).Forget();
        }

        private async UniTask GoSceneAsync(SceneRequest request, CancellationTokenSource source)
        {
            var cancellationToken = source.Token;
            try
            {
                var services = request.Services;
                if (!OwnsSceneRequest(request)) return;
                services.Data.UpdateLocalData(key: "null", value: "null", updateAll: true);
                if (!OwnsSceneRequest(request)) return;
                services.Data.UpdatePlayerData();
                if (!await WaitForServerAsync(services, cancellationToken)) return;
                if (!OwnsSceneRequest(request)) return;
                services.Data.SaveLocalData();
                await LoadTargetSceneAsync(request.Target, cancellationToken, () => OwnsSceneRequest(request));
                if (OwnsSceneRequest(request))
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
                    CompleteSceneRequest(request);
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
