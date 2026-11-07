using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using UnityEngine;

namespace AD
{
    public sealed class DeletionPresenter : MonoBehaviour
    {
        private DeletionFlow _flow;
        private DeletionView _view;
        private Func<Task<bool>> _retry;
        private int _dirty;
        // CloudScriptDeletionClient installs the runtime factory; creating it does not submit a request.
        public static Func<DeletionFlow> RuntimeFlowFactory { private get; set; }

        // Call during application bootstrap, before login. No endpoint or title is inferred or enabled by default.
        public static void ConfigureSessionService(Uri httpsOrigin, string titleId, string recoveryDirectory = null)
        {
            ConfigureRuntimeService(httpsOrigin, titleId, recoveryDirectory, owner =>
                HttpDeletionGateway.ForSessionConfirmation(httpsOrigin, captured =>
                {
                    var player = PlayFab.PlayFabSettings.staticPlayer;
                    if (!captured.Matches(owner.DeletionSession()) || !ReferenceEquals(owner, Managers.DataM) ||
                        player.PlayFabId != captured.AccountId || player.EntityId != captured.EntityId ||
                        player.EntityType != "title_player_account" || PlayFab.PlayFabSettings.TitleId != titleId)
                        throw new InvalidOperationException();
                    return player.ClientSessionTicket;
                }));
        }

        public static void ConfigureService(Uri httpsOrigin, string titleId,
            Func<DeletionSession, CancellationToken, Task<DeletionAuthorization>> freshAuthentication,
            string recoveryDirectory = null)
        {
            if (freshAuthentication == null) throw new ArgumentNullException(nameof(freshAuthentication));
            ConfigureRuntimeService(httpsOrigin, titleId, recoveryDirectory,
                owner => new HttpDeletionGateway(httpsOrigin, freshAuthentication));
        }

        private static void ConfigureRuntimeService(Uri httpsOrigin, string titleId, string recoveryDirectory,
            Func<DataManager, HttpDeletionGateway> createGateway)
        {
            if (httpsOrigin == null || string.IsNullOrEmpty(titleId)) throw new ArgumentNullException();
            // Validate the origin now, before installing the login guard.
            using (HttpDeletionGateway.ForSessionConfirmation(httpsOrigin, s => null)) { }
            var directory = recoveryDirectory ?? Path.Combine(Application.persistentDataPath, "DeletionRecovery");
            DeletionReceiptBootstrap.Configure(httpsOrigin, titleId, directory);
            Func<string, FileDeletionRecoveryStore> store = account => new FileDeletionRecoveryStore(Path.Combine(directory,
                DeletionRecovery.Hash(httpsOrigin.AbsoluteUri, titleId, account) + ".json"));
            DeletionRecoveryGuard.HasPendingSubmission = account => store(account).Load()?.SubmissionStarted == true;
            RuntimeFlowFactory = () =>
            {
                var owner = Managers.DataM;
                var session = owner?.DeletionSession();
                if (session == null || !session.HasEntityBinding || session.TitleId != titleId)
                    return new DeletionFlow(new UnavailableDeletionGateway(), () => null);
                var gateway = createGateway(owner);
                var journal = store(session.AccountId);
                return new DeletionFlow(gateway, () => ReferenceEquals(owner, Managers.DataM) ? owner.DeletionSession() : null, owner.BeginDeletionSubmission,
                    owner.FinishAcceptedDeletion, owner.FinishCancelledDeletion, journal,
                    DeletionRecovery.Hash(httpsOrigin.AbsoluteUri, titleId, session.AccountId, session.EntityId),
                    new DeletionReceiptClient(gateway, new AndroidDeletionReceiptKeys(), journal), httpsOrigin.AbsoluteUri);
            };
        }

        public void Bind(DeletionView view)
        {
            _view = view;
            view.RequestButton.onClick.AddListener(() => Execute(() => _flow.RequestAsync()));
            view.ConfirmButton.onClick.AddListener(() => Execute(() => _flow.ConfirmAsync()));
            view.SessionConfirmButton.onClick.AddListener(() => Execute(() => _flow.ConfirmSessionAsync()));
            view.RefreshButton.onClick.AddListener(() => Execute(() => _flow.RefreshAsync()));
            view.CancelButton.onClick.AddListener(() => Execute(() => _flow.CancelAsync()));
            view.ReauthenticateButton.onClick.AddListener(() => Execute(() => _flow.ReauthenticateAsync()));
            view.RetryButton.onClick.AddListener(() => Execute(_retry));
        }

        // Synthetic tests own both the gateway and session; runtime uses the bootstrap factory.
        public void ConfigureSynthetic(DeletionFlow flow)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (flow == null || !flow.IsSynthetic) throw new ArgumentException("A synthetic flow is required.");
            Release();
            Attach(flow);
            Render();
#else
            throw new InvalidOperationException("Synthetic deletion is unavailable in this build.");
#endif
        }

        private void OnEnable()
        {
            if (_flow == null)
            {
                DeletionFlow flow = null;
                try { flow = RuntimeFlowFactory?.Invoke(); } catch (Exception) { }
                Attach(flow ?? new DeletionFlow(new UnavailableDeletionGateway(), () => null));
            }
            Render();
        }

        private void Attach(DeletionFlow flow)
        {
            _flow = flow;
            _flow.Changed += MarkDirty;
            MarkDirty();
        }

        private void MarkDirty() => Interlocked.Exchange(ref _dirty, 1);
        private void Update() { if (Interlocked.Exchange(ref _dirty, 0) != 0) Render(); }

        private async void Execute(Func<Task<bool>> action)
        {
            var owner = _flow;
            if (owner == null || owner.IsBusy || !owner.IsAvailable || action == null) return;
            _retry = owner.UsesSessionConfirmation ? () => owner.ReauthenticateAsync() : action;
            try { await action(); }
            catch (Exception) { /* No raw account/proof/error data is displayed or logged. */ }
            if (ReferenceEquals(_flow, owner)) MarkDirty();
        }

        public void Render()
        {
            if (_flow == null || _view == null) return;
            var state = _flow.State;
            bool ready = _flow.IsAvailable && !_flow.IsBusy;
            string text;
            switch (state)
            {
                case DeletionState.Idle:
                    text = _flow.UsesSessionConfirmation || _flow.UsesCloudScript
                        ? "현재 로그인한 게임 계정의 삭제 요청을 확인할 수 있습니다. 이 화면을 여는 것만으로는 삭제되지 않습니다."
                        : "삭제 요청을 확인하려면 본인 확인이 필요합니다. 이 화면을 여는 것만으로는 삭제되지 않습니다."; break;
                case DeletionState.Authenticating:
                    text = _flow.UsesSessionConfirmation || _flow.UsesCloudScript ? "현재 게임 계정으로 삭제를 요청할 수 있는지 확인하고 있습니다."
                        : "본인 확인이 필요합니다. 확인이 끝날 때까지 기다려 주세요."; break;
                case DeletionState.AwaitingSessionConfirmation:
                    text = "현재 로그인한 게임 계정으로 삭제 요청을 진행할지 확인해 주세요. 현재 세션을 확인하는 단계이며 새 Google 로그인은 아닙니다. 이 단계에서는 삭제 요청을 전송하지 않습니다."; break;
                case DeletionState.RecoveryRequired:
                    text = _flow.UsesCloudScript ? "저장된 삭제 요청 미리보기가 있습니다. 확정 전에 내용을 다시 확인해 주세요. 아직 전송된 요청은 없습니다."
                        : "이전 삭제 요청이 있습니다. 다시 로그인하지 않고 접수 기록을 확인할 수 있습니다. 전송 여부가 불확실한 요청은 상태만 확인하며 다시 전송하지 않습니다."; break;
                case DeletionState.RecoveryUnavailable:
                    text = "접수 기록을 확인하지 못했습니다. 접근 기한이 지났거나 기기의 보호 키를 사용할 수 없을 수 있습니다. 접수 여부는 미확인 상태이며 데이터는 보존하고 요청은 다시 전송하지 않았습니다. 화면을 닫거나 연결을 다시 확인해 주세요. 유효한 기존 계정 세션으로 이 요청을 확인할 수도 있습니다."; break;
                case DeletionState.UnsupportedAccount:
                    text = "현재 삭제 서비스에서 지원하지 않는 계정 유형입니다. 이 확인으로 삭제 요청을 전송하지 않았습니다. 기존 대기 요청과 기기 데이터는 보존합니다."; break;
                case DeletionState.AwaitingConfirmation:
                    text = "확정 전에 삭제 범위를 확인해 주세요.\n" +
                        (_flow.Request.Scope == "master" ? "범위: 계정 및 연결된 게임." : "범위: 이 게임의 계정 데이터.") +
                        "\n아직 삭제 요청을 확정하지 않았습니다."; break;
                case DeletionState.Queued:
                    text = "삭제 요청이 접수되었습니다. 삭제는 아직 완료되지 않았습니다."; break;
                case DeletionState.Processing:
                    text = "삭제 요청을 처리하고 있습니다. 삭제 완료는 아직 확인되지 않았습니다."; break;
                case DeletionState.Accepted:
                    text = "삭제 요청이 접수되었습니다. 삭제 완료는 아직 확인되지 않았습니다." + (_flow.AcceptedCleanupFailed
                        ? " 기기에서 로그아웃 또는 정리를 마치지 못했습니다. 삭제 요청을 다시 보내지 마세요."
                        : " 게임에서 로그아웃되었습니다."); break;
                case DeletionState.SubmissionUnknown:
                    text = _flow.UsesCloudScript
                        ? "삭제 요청의 접수 여부를 확인하지 못했습니다. 요청을 다시 전송하지 않습니다. 기기 데이터는 보존하며 저장은 일시 중지됩니다. " + CloudScriptDeletionClient.SupportEmail + "로 문의해 주세요. 이 문제를 해결하려고 새 계정을 만들지 마세요."
                        : "삭제 요청의 접수 여부를 확인하지 못했습니다. 기기 데이터는 삭제하지 않았습니다. 기존 요청을 확인하고 새 요청은 보내지 마세요."; break;
                case DeletionState.Completed:
                    text = _flow.IsSynthetic && !string.IsNullOrEmpty(_flow.Request?.CompletionEvidence)
                        ? "시험용 삭제가 완료되었습니다. 실제 계정 데이터는 삭제하지 않았습니다."
                        : "삭제 완료는 확인되지 않았습니다."; break;
                case DeletionState.Cancelled:
                    text = "삭제 요청이 취소되었습니다."; break;
                case DeletionState.RetryableFailure:
                    text = "마지막 작업의 결과를 확인하지 못했습니다. 다시 시도하거나 요청 상태 또는 본인 확인을 다시 진행해 주세요. 삭제는 확인되지 않았습니다."; break;
                case DeletionState.SessionChanged:
                    text = _flow.CloudScriptSubmissionStarted
                        ? "삭제 요청 전송 중 세션이 변경되었습니다. 접수 여부는 미확인이며 기기 정리나 재전송은 하지 않았습니다. " + CloudScriptDeletionClient.SupportEmail + "로 문의해 주세요."
                        : "계정 또는 세션이 변경되었습니다. 화면을 닫고 다시 시작해 주세요."; break;
                default:
                    text = "현재 앱에서 계정 삭제를 요청할 수 없습니다. 이 화면에서는 삭제 요청을 전송하지 않습니다."; break;
            }
            if (_flow.IsSynthetic) text = "시험용 — 실제 계정 변경 없음\n\n" + text;
            if (_flow.IsBusy && state != DeletionState.Authenticating) text += "\n현재 작업이 끝나기를 기다리고 있습니다...";
            _view.Message.text = text;
            bool accepted = state == DeletionState.Accepted && !_flow.IsSynthetic;
            _view.ExitOnClose = accepted;
            var closeCaption = _view.CloseButton.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (closeCaption != null) closeCaption.text = accepted ? "게임 종료" : "설정으로 돌아가기";
            var popupManager = Managers.PopupM;
            if (accepted && popupManager != null) popupManager.RegisterBlocker(_view.gameObject, true);
            Set(_view.RequestButton, state == DeletionState.Unavailable || state == DeletionState.Idle, ready && state == DeletionState.Idle);
            Set(_view.ConfirmButton, state == DeletionState.AwaitingConfirmation && _flow.CanConfirmDeletion, ready);
            Set(_view.SessionConfirmButton, state == DeletionState.AwaitingSessionConfirmation, ready);
            Set(_view.RefreshButton, !_flow.UsesCloudScript && (state == DeletionState.Queued || state == DeletionState.Processing || state == DeletionState.SubmissionUnknown ||
                state == DeletionState.RecoveryRequired || state == DeletionState.RecoveryUnavailable ||
                state == DeletionState.RetryableFailure && _flow.Request != null), ready && (!_flow.NeedsAuthorization || _flow.CanRecoverReceipt));
            Set(_view.CancelButton, state == DeletionState.AwaitingConfirmation || state == DeletionState.Queued, ready && !_flow.NeedsAuthorization);
            Set(_view.RetryButton, state == DeletionState.RetryableFailure && _retry != null, ready);
            Set(_view.ReauthenticateButton, !_flow.CloudScriptSubmissionStarted && (state == DeletionState.RetryableFailure || state == DeletionState.RecoveryRequired ||
                state == DeletionState.RecoveryUnavailable || state == DeletionState.SubmissionUnknown || state == DeletionState.AwaitingSessionConfirmation), ready);
            Set(_view.ContactButton, true, !_flow.IsBusy);
        }

        private static void Set(UnityEngine.UI.Button button, bool visible, bool enabled)
        {
            button.gameObject.SetActive(visible);
            button.interactable = enabled;
        }

        private void Release()
        {
            if (_flow != null) { _flow.Changed -= MarkDirty; _flow.Dispose(); }
            _flow = null;
            _retry = null;
        }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
