using System;
using System.IO;
using System.Threading;
using AD.Privacy;
using TMPro;
using UnityEngine;

namespace AD
{
    public static class DeletionReceiptBootstrap
    {
        private static Uri _origin;
        private static string _title, _directory;
        public static void Configure(Uri origin, string title, string directory)
        { _origin = origin; _title = title; _directory = directory; }

        // Runs before login. Never selects one account from multiple pending records.
        public static bool TryOpen(Transform parent, TMP_FontAsset font, Action close)
        {
            if (_origin == null || !Directory.Exists(_directory)) return false;
            DeletionRecovery record = null;
            FileDeletionRecoveryStore store = null;
            string blocked = null;
            try
            {
                foreach (string path in Directory.GetFiles(_directory, "*.json"))
                {
                    var candidateStore = new FileDeletionRecoveryStore(path);
                    var candidate = candidateStore.Load();
                    if (candidate == null || !candidate.SubmissionStarted) continue;
                    if (record != null) { blocked = "대기 중인 요청이 여러 개 있어 계정을 선택하지 않았습니다. 요청 기록과 계정 데이터는 보존합니다."; break; }
                    if (candidate.Origin != _origin.AbsoluteUri || candidate.Title != _title ||
                        Path.GetFileName(path) != candidate.OwnerHash + ".json") throw new InvalidDataException();
                    record = candidate; store = candidateStore;
                }
            }
            catch (Exception) { blocked = "저장된 요청을 읽지 못했습니다. 요청을 다시 전송하지 않았으며 데이터는 보존합니다."; }
            if (record == null && blocked == null) return false;
            var root = new GameObject("DeletionReceiptRecovery", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("ReceiptEventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule)).transform.SetParent(root.transform, false);
            var panel = DeletionView.Rect("Receipt", root.transform);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            var view = panel.gameObject.AddComponent<DeletionView>();
            view.Build(font, () => { UnityEngine.Object.Destroy(root); close(); });
            var presenter = panel.gameObject.AddComponent<DeletionReceiptPresenter>();
            presenter.Initialize(view, record, store, _origin, Managers.DataM, blocked);
            return true;
        }
    }

    public sealed class DeletionReceiptPresenter : MonoBehaviour
    {
        private DeletionView _view;
        private DeletionRecovery _record;
        private DeletionReceiptClient _client;
        private HttpDeletionGateway _gateway;
        private DataManager _owner;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _busy;
        public void Initialize(DeletionView view, DeletionRecovery record, IDeletionRecoveryStore store, Uri origin,
            DataManager owner, string blocked)
        {
            _view = view; _record = record; _owner = owner;
            foreach (var button in new[] { view.RequestButton, view.ConfirmButton, view.SessionConfirmButton, view.CancelButton,
                view.RetryButton, view.ReauthenticateButton }) button.gameObject.SetActive(false);
            bool receiptOnly = owner != null && owner.ReceiptOnly;
            view.CloseButton.GetComponentInChildren<TMP_Text>().text = receiptOnly ? "복구 화면 닫기" : "로그인 화면으로";
            if (blocked == null && (owner == null || record == null || !owner.CanApplyDeletionReceipt(record)))
                blocked = "다른 계정이 사용 중이거나 요청의 계정을 확인할 수 없습니다. 계정 인증 정보와 기기 데이터는 변경하지 않았습니다.";
            if (blocked != null)
            {
                view.Message.text = blocked + (receiptOnly
                    ? " 화면을 닫아도 됩니다. 이 복구 세션에서는 로그인할 수 없습니다. 기기 처리를 마친 뒤 앱을 다시 시작해 주세요. 삭제 완료나 안전한 재가입이 확인된 것은 아닙니다."
                    : " 화면을 닫아도 됩니다. 기존 계정으로 로그인하면 해당 계정의 요청을 확인할 수 있습니다. 삭제 완료나 안전한 재가입이 확인된 것은 아닙니다.");
                view.RefreshButton.gameObject.SetActive(false);
                return;
            }
            _gateway = HttpDeletionGateway.ForSessionConfirmation(origin, s => throw new InvalidOperationException());
            _client = new DeletionReceiptClient(_gateway, new AndroidDeletionReceiptKeys(), store);
            view.Message.text = "이전 삭제 요청 전송 기록이 있습니다. 로그인하지 않고 접수 기록을 확인할 수 있습니다. 삭제 요청을 다시 전송하지 않습니다.";
            view.RefreshButton.onClick.AddListener(Check);
        }

        private async void Check()
        {
            if (_busy || _client == null || _lifetime.IsCancellationRequested) return;
            _busy = true; _view.RefreshButton.interactable = false;
            try
            {
                if (!ReferenceEquals(_owner, Managers.DataM) || !_owner.CanApplyDeletionReceipt(_record)) throw new InvalidOperationException();
                var snapshot = await _client.ReadAsync(_record, _lifetime.Token);
                if (_lifetime.IsCancellationRequested) return;
                if (!ReferenceEquals(_owner, Managers.DataM) || !_owner.CanApplyDeletionReceipt(_record)) throw new InvalidOperationException();
                if (snapshot.State == DeletionState.Accepted || snapshot.State == DeletionState.Cancelled)
                {
                    _view.Message.text = snapshot.State == DeletionState.Accepted
                        ? "삭제 요청이 접수되었습니다. 일치하는 계정의 기기 진행도만 정리할 수 있습니다. 최종 삭제 완료가 확인된 것은 아닙니다."
                        : "요청이 취소되었습니다. 삭제 요청은 전송되지 않았습니다.";
                    try
                    {
                        await _client.FinishAsync(_record, () =>
                        {
                            if (!ReferenceEquals(_owner, Managers.DataM)) throw new InvalidOperationException();
                            _owner.ApplyDeletionReceipt(_record, snapshot.State == DeletionState.Accepted);
                        }, _lifetime.Token);
                        if (!_lifetime.IsCancellationRequested) _view.RefreshButton.gameObject.SetActive(false);
                    }
                    catch (Exception)
                    {
                        if (!_lifetime.IsCancellationRequested) _view.Message.text += " 기기 처리를 마치지 못했습니다. 확인된 접수 기록은 보존하며 다시 시도해도 삭제 요청을 전송하지 않습니다.";
                    }
                }
                else _view.Message.text = "접수 여부가 아직 확인되지 않았거나 전송되지 않은 요청입니다. 데이터는 보존하고 요청은 다시 전송하지 않았습니다. 화면을 닫거나 나중에 다시 확인해 주세요.";
            }
            catch (Exception)
            {
                if (!_lifetime.IsCancellationRequested)
                {
                    _view.Message.text = "지금은 접수 기록을 확인할 수 없습니다. 접근 기한이 지났거나 기기의 확인 정보 또는 연결을 사용할 수 없을 수 있습니다. 접수 여부는 미확인이며 데이터는 보존합니다. "
                        + (_owner != null && _owner.ReceiptOnly
                            ? "화면을 닫아 중단할 수 있습니다. 이 복구 세션에서는 로그인할 수 없습니다. 기기 처리를 마친 뒤 앱을 다시 시작해 주세요. "
                            : "화면을 닫아 중단하거나 기존 계정으로 직접 로그인해 해당 요청을 확인할 수 있습니다. ")
                        + "이 복구 과정에서 새 계정이나 삭제 요청을 만들지 않습니다.";
                }
            }
            finally { _busy = false; if (!_lifetime.IsCancellationRequested) _view.RefreshButton.interactable = true; }
        }
        private void OnDestroy() { _lifetime.Cancel(); _gateway?.Dispose(); _lifetime.Dispose(); }
    }
}
