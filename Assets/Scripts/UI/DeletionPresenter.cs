using System;
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
        // Explicit application bootstrap only. Null is the default, so builds cannot contact a deletion service accidentally.
        public static Func<DeletionFlow> RuntimeFlowFactory { private get; set; }

        public static void ConfigureService(Uri httpsOrigin,
            Func<DeletionSession, CancellationToken, Task<DeletionAuthorization>> freshAuthentication)
        {
            if (httpsOrigin == null || freshAuthentication == null) throw new ArgumentNullException();
            RuntimeFlowFactory = () =>
            {
                var owner = Managers.DataM;
                if (owner == null) return new DeletionFlow(new UnavailableDeletionGateway(), () => null);
                return new DeletionFlow(new HttpDeletionGateway(httpsOrigin, freshAuthentication),
                    owner.DeletionSession, owner.BeginDeletionSubmission, owner.FinishAcceptedDeletion,
                    owner.FinishCancelledDeletion);
            };
        }

        public void Bind(DeletionView view)
        {
            _view = view;
            view.RequestButton.onClick.AddListener(() => Execute(() => _flow.RequestAsync()));
            view.ConfirmButton.onClick.AddListener(() => Execute(() => _flow.ConfirmAsync()));
            view.RefreshButton.onClick.AddListener(() => Execute(() => _flow.RefreshAsync()));
            view.CancelButton.onClick.AddListener(() => Execute(() => _flow.CancelAsync()));
            view.ReauthenticateButton.onClick.AddListener(() => Execute(() => _flow.ReauthenticateAsync()));
            view.RetryButton.onClick.AddListener(() => Execute(_retry));
        }

        // Tests own both the gateway and session; production has no adapter composition.
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
            _retry = action; // Preserve cancel/confirm intent after failure.
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
                    text = "Verify your identity before reviewing a deletion request. Nothing is deleted by opening this screen."; break;
                case DeletionState.Authenticating:
                    text = "Identity verification is required. Waiting for verification to finish."; break;
                case DeletionState.AwaitingConfirmation:
                    text = "Review the deletion scope before confirming.\n" +
                        (_flow.Request.Scope == "master" ? "Scope: account and linked titles." : "Scope: this game's account data.") +
                        "\nThe request has not been confirmed."; break;
                case DeletionState.Queued:
                    text = "Deletion request received. Deletion is not complete."; break;
                case DeletionState.Processing:
                    text = "Deletion request is being processed. Completion has not been confirmed."; break;
                case DeletionState.Accepted:
                    text = "Your deletion request was accepted. You have been signed out." +
                        (_flow.AcceptedCleanupFailed ? " Some local data could not be cleared. Do not submit another deletion request." : ""); break;
                case DeletionState.SubmissionUnknown:
                    text = "We could not confirm whether the deletion request was accepted. Your local data has not been cleared. Check this request; do not submit another."; break;
                case DeletionState.Completed:
                    text = _flow.IsSynthetic && !string.IsNullOrEmpty(_flow.Request?.CompletionEvidence)
                        ? "Test deletion completed. No real account data was deleted."
                        : "Deletion completion is not verified."; break;
                case DeletionState.Cancelled:
                    text = "The deletion request was cancelled."; break;
                case DeletionState.RetryableFailure:
                    text = "The last action could not be verified. Retry it, check request status, or verify your identity again. Deletion is not confirmed."; break;
                case DeletionState.SessionChanged:
                    text = "The account or session changed. Close this screen and start again."; break;
                default:
                    text = "Account deletion is currently unavailable in this app. No deletion request can be submitted here."; break;
            }
            if (_flow.IsSynthetic) text = "TEST ONLY — no real account changes\n\n" + text;
            if (_flow.IsBusy && state != DeletionState.Authenticating) text += "\nWaiting for the current action...";
            _view.Message.text = text;
            Set(_view.RequestButton, state == DeletionState.Unavailable || state == DeletionState.Idle, ready && state == DeletionState.Idle);
            Set(_view.ConfirmButton, state == DeletionState.AwaitingConfirmation, ready);
            Set(_view.RefreshButton, state == DeletionState.Queued || state == DeletionState.Processing || state == DeletionState.SubmissionUnknown ||
                state == DeletionState.RetryableFailure && _flow.Request != null, ready);
            Set(_view.CancelButton, state == DeletionState.AwaitingConfirmation || state == DeletionState.Queued, ready);
            Set(_view.RetryButton, state == DeletionState.RetryableFailure && _retry != null, ready);
            Set(_view.ReauthenticateButton, state == DeletionState.RetryableFailure, ready);
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
