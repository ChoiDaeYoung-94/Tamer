using System;

namespace AD.Advertising
{
    public interface IAdConsentClient
    {
        bool CanRequestAds { get; }
        bool PrivacyOptionsRequired { get; }
        void Update(bool underAgeOfConsent, Action<bool> completed);
        void Gather(Action<bool> completed);
        void ShowPrivacyOptions(Action<bool> completed);
    }

    /// <summary>One owner, one consent operation. SDK callbacks are dispatched to the owner's thread.</summary>
    public sealed class AdConsentGate : IDisposable
    {
        private enum Stage { Idle, Updating, Gathering, Ready, Blocked, Privacy, Disposed }
        private readonly IAdConsentClient _client;
        private readonly Action<Action> _dispatch;
        private readonly Action<string> _trace;
        private readonly bool _underAgeOfConsent;
        private Stage _stage;
        private int _version;
        private Action<bool> _pendingCompletion;

        public AdConsentGate(IAdConsentClient client, bool underAgeOfConsent,
            Action<Action> dispatch, Action<string> trace = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _trace = trace ?? (_ => { });
            _underAgeOfConsent = underAgeOfConsent;
        }

        public bool IsUpdating => _stage == Stage.Updating;
        public bool IsBusy => _stage == Stage.Updating || _stage == Stage.Gathering || _stage == Stage.Privacy;
        public bool CanRequestAds => _stage == Stage.Ready && ReadCanRequestAds();
        public bool PrivacyOptionsRequired
        {
            get
            {
                if (_stage == Stage.Idle || _stage == Stage.Disposed) return false;
                try { return _client.PrivacyOptionsRequired; }
                catch (Exception) { return false; }
            }
        }

        private bool ReadCanRequestAds()
        {
            try { return _client.CanRequestAds; }
            catch (Exception) { return false; }
        }

        public bool Request(Action<bool> completed)
        {
            if (_stage == Stage.Disposed || IsBusy) return false;
            if (CanRequestAds) { completed(true); return true; }
            int version = ++_version;
            _pendingCompletion = completed;
            _stage = Stage.Updating;
            _trace("consent_update_call");
            try
            {
                // UMP treatment is separate from Mobile Ads' Child/Teen configuration.
                _client.Update(_underAgeOfConsent, success => _dispatch(() =>
                {
                    if (!Current(version, Stage.Updating)) return;
                    _trace(success ? "consent_update_ok" : "consent_update_failed");
                    if (!success) { Finish(false, completed); return; }
                    _stage = Stage.Gathering;
                    _trace("consent_form_call");
                    try
                    {
                        _client.Gather(gathered => _dispatch(() =>
                        {
                            if (!Current(version, Stage.Gathering)) return;
                            Finish(gathered && ReadCanRequestAds(), completed);
                        }));
                    }
                    catch (Exception) { if (Current(version, Stage.Gathering)) Finish(false, completed); }
                }));
            }
            catch (Exception) { if (Current(version, Stage.Updating)) Finish(false, completed); }
            return true;
        }

        public bool OpenPrivacyOptions(Action<bool> completed)
        {
            if (_stage == Stage.Disposed || IsBusy || !PrivacyOptionsRequired) return false;
            int version = ++_version;
            _pendingCompletion = completed;
            _stage = Stage.Privacy;
            _trace("privacy_options_call");
            try
            {
                _client.ShowPrivacyOptions(success => _dispatch(() =>
                {
                    if (!Current(version, Stage.Privacy)) return;
                    Finish(success && ReadCanRequestAds(), completed);
                }));
            }
            catch (Exception) { if (Current(version, Stage.Privacy)) Finish(false, completed); }
            return true;
        }

        private bool Current(int version, Stage stage) => _version == version && _stage == stage;

        private void Finish(bool allowed, Action<bool> completed)
        {
            _pendingCompletion = null;
            _stage = allowed ? Stage.Ready : Stage.Blocked;
            _trace(allowed ? "consent_allowed" : "consent_blocked");
            completed(allowed);
        }

        // Only the network update has a host deadline. Never time out an open form.
        public bool ExpireUpdate()
        {
            if (!IsUpdating) return false;
            ++_version;
            _trace("consent_update_timeout");
            Finish(false, _pendingCompletion);
            return true;
        }

        public void Dispose()
        {
            ++_version;
            _pendingCompletion = null;
            _stage = Stage.Disposed;
        }
    }
}
