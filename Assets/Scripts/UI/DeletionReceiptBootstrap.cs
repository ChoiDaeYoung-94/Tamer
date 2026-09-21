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
                    if (record != null) { blocked = "Multiple pending requests were found. No account was selected. Records and account data are preserved."; break; }
                    if (candidate.Origin != _origin.AbsoluteUri || candidate.Title != _title ||
                        Path.GetFileName(path) != candidate.OwnerHash + ".json") throw new InvalidDataException();
                    record = candidate; store = candidateStore;
                }
            }
            catch (Exception) { blocked = "The saved request could not be read. No request was resent and your data is preserved."; }
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
            view.CloseButton.GetComponentInChildren<TMP_Text>().text = "Return to sign-in";
            if (blocked == null && (owner == null || record == null || !owner.CanApplyDeletionReceipt(record)))
                blocked = "A different account is active or the request binding is unclear. No account credentials or local data were changed.";
            if (blocked != null)
            {
                view.Message.text = blocked + " You may close this screen. Signing in to an existing account can check its own request; this does not confirm deletion or safe rejoining.";
                view.RefreshButton.gameObject.SetActive(false);
                return;
            }
            _gateway = HttpDeletionGateway.ForSessionConfirmation(origin, s => throw new InvalidOperationException());
            _client = new DeletionReceiptClient(_gateway, new AndroidDeletionReceiptKeys(), store);
            view.Message.text = "A previous deletion submission was found. Check its receipt without signing in. No deletion will be resent.";
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
                        ? "Your deletion request was accepted. Only matching local account progress may be cleared. This is not confirmation of final erasure."
                        : "The request was cancelled. No deletion was submitted.";
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
                        if (!_lifetime.IsCancellationRequested) _view.Message.text += " Local handling could not finish. The confirmed receipt is preserved; retry does not submit deletion.";
                    }
                }
                else _view.Message.text = "Acceptance is still unknown or not submitted. Your data is preserved. No request was resent. You may close this screen or check again later.";
            }
            catch (Exception)
            {
                if (!_lifetime.IsCancellationRequested) _view.Message.text = "This receipt cannot be checked now. Access may have expired, the protected key may be missing, or the connection may be unavailable. Acceptance remains unknown; data is preserved. Close this screen to stop, or sign in explicitly to an existing account to check its request. No new account or deletion is created by this recovery.";
            }
            finally { _busy = false; if (!_lifetime.IsCancellationRequested) _view.RefreshButton.interactable = true; }
        }
        private void OnDestroy() { _lifetime.Cancel(); _gateway?.Dispose(); _lifetime.Dispose(); }
    }
}
