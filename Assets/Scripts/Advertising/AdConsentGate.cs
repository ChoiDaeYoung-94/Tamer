using System;

namespace AD.Advertising
{
    public enum AdPrivacyRequirement { Unknown, NotRequired, Required }
    public enum AdPrivacyResult
    { None, Checking, AgeRequired, Unavailable, Busy, UpdateFailed, Unknown, NotRequired, FormFailed, OptionsClosed, Cancelled }

    public interface IAdPrivacyStatusClient
    {
        AdPrivacyRequirement PrivacyRequirement { get; }
    }

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
        private bool _privacyOnly;
        private bool _privacyRefreshOwner, _freshPrivacyStatus;
        private Action<AdPrivacyResult> _privacyCompletion;

        public AdConsentGate(IAdConsentClient client, bool underAgeOfConsent,
            Action<Action> dispatch, Action<string> trace = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _trace = trace ?? (_ => { });
            _underAgeOfConsent = underAgeOfConsent;
        }

        public bool IsUpdating => _stage == Stage.Updating;
        public bool IsPrivacyOnly => _privacyOnly;
        public bool IsPrivacyRefreshOwner => _privacyRefreshOwner;
        public bool IsBusy => _stage == Stage.Updating || _stage == Stage.Gathering || _stage == Stage.Privacy;
        public bool CanRequestAds => !_privacyOnly && _stage == Stage.Ready && ReadCanRequestAds();
        public bool PrivacyOptionsRequired
        {
            get
            {
                if (_stage == Stage.Idle || _stage == Stage.Disposed) return false;
                if (_privacyRefreshOwner && !_freshPrivacyStatus) return false;
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
            if (_privacyOnly || _stage == Stage.Disposed || IsBusy) return false;
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

        public bool OpenPrivacyOptions(Action<bool> completed, Action<AdPrivacyResult> privacyCompleted = null)
        {
            if (_stage == Stage.Disposed || IsBusy || !PrivacyOptionsRequired) return false;
            int version = ++_version;
            bool formSuccess = false;
            Action<bool> completion = allowed =>
            {
                completed(allowed);
                privacyCompleted?.Invoke(formSuccess ? AdPrivacyResult.OptionsClosed : AdPrivacyResult.FormFailed);
            };
            _pendingCompletion = completion;
            _stage = Stage.Privacy;
            _trace("privacy_options_call");
            try
            {
                _client.ShowPrivacyOptions(success => _dispatch(() =>
                {
                    if (!Current(version, Stage.Privacy)) return;
                    formSuccess = success;
                    Finish(success && ReadCanRequestAds(), completion);
                }));
            }
            catch (Exception) { if (Current(version, Stage.Privacy)) Finish(false, completion); }
            return true;
        }

        // Explicit privacy discovery only. Never Gather or restore ad eligibility.
        // Failed/expired updates must not expose the SDK's previous-session Required.
        public bool RefreshPrivacyOptions(Action<AdPrivacyResult> completed)
        {
            if (_stage == Stage.Disposed || IsBusy) return false;
            SuspendForPrivacy();
            _privacyRefreshOwner = true;
            _freshPrivacyStatus = false;
            _privacyCompletion = completed;
            int version = ++_version;
            _stage = Stage.Updating;
            try
            {
                _client.Update(_underAgeOfConsent, success => _dispatch(() =>
                {
                    if (!Current(version, Stage.Updating)) return;
                    if (!success) { FinishPrivacyRefresh(AdPrivacyResult.UpdateFailed); return; }
                    AdPrivacyRequirement status;
                    try { status = (_client as IAdPrivacyStatusClient)?.PrivacyRequirement ?? AdPrivacyRequirement.Unknown; }
                    catch (Exception) { status = AdPrivacyRequirement.Unknown; }
                    _freshPrivacyStatus = status == AdPrivacyRequirement.Required;
                    if (status != AdPrivacyRequirement.Required)
                    {
                        FinishPrivacyRefresh(status == AdPrivacyRequirement.NotRequired
                            ? AdPrivacyResult.NotRequired : AdPrivacyResult.Unknown);
                        return;
                    }
                    _stage = Stage.Privacy;
                    try
                    {
                        _client.ShowPrivacyOptions(shown => _dispatch(() =>
                        {
                            if (Current(version, Stage.Privacy)) FinishPrivacyRefresh(shown
                                ? AdPrivacyResult.OptionsClosed : AdPrivacyResult.FormFailed);
                        }));
                    }
                    catch (Exception) { if (Current(version, Stage.Privacy)) FinishPrivacyRefresh(AdPrivacyResult.FormFailed); }
                }));
            }
            catch (Exception) { if (Current(version, Stage.Updating)) FinishPrivacyRefresh(AdPrivacyResult.UpdateFailed); }
            return true;
        }

        private void FinishPrivacyRefresh(AdPrivacyResult result)
        {
            var completed = _privacyCompletion;
            _privacyCompletion = null;
            _stage = Stage.Blocked;
            completed?.Invoke(result);
        }

        private bool Current(int version, Stage stage) => _version == version && _stage == stage;

        private void Finish(bool allowed, Action<bool> completed)
        {
            // Suspension discards the prior owner's completion, but a native form
            // still owns its busy state until this real callback settles it.
            bool deliver = _pendingCompletion != null;
            _pendingCompletion = null;
            allowed = allowed && !_privacyOnly;
            _stage = allowed ? Stage.Ready : Stage.Blocked;
            _trace(allowed ? "consent_allowed" : "consent_blocked");
            if (deliver) completed(allowed);
        }

        // Preserve only an existing privacy entry. Never starts Update/Gather or
        // restores ad eligibility. Gathering/Privacy cannot be timed out/closed by
        // an age change; their real callbacks settle native form ownership.
        public void SuspendForPrivacy()
        {
            if (_stage == Stage.Disposed) return;
            _privacyOnly = true;
            _pendingCompletion = null;
            _privacyCompletion = null;
            if (_stage == Stage.Updating)
            {
                ++_version;
                _stage = Stage.Blocked;
                if (_privacyRefreshOwner) _freshPrivacyStatus = false;
            }
            else if (_stage == Stage.Ready)
                _stage = Stage.Blocked;
        }

        // Share the exact owner transfer rule with the manager and pure checks.
        // An unready replacement must not erase an earlier Required entry.
        public static void SuspendAndRetainPrivacy(ref AdConsentGate active, ref AdConsentGate retained)
        {
            if (active == null) return;
            active.SuspendForPrivacy();
            if (ReferenceEquals(active, retained)) { active = null; return; }
            if (retained != null && (retained.IsBusy ||
                (retained.PrivacyOptionsRequired && !active.PrivacyOptionsRequired)))
            {
                // Native form owners settle themselves, even on direct age edits.
                if (!active.IsBusy) { active.Dispose(); active = null; }
                return;
            }
            retained?.Dispose();
            retained = active;
            active = null;
        }

        // Only the network update has a host deadline. Never time out an open form.
        public bool ExpireUpdate()
        {
            if (!IsUpdating) return false;
            ++_version;
            _trace("consent_update_timeout");
            if (_privacyRefreshOwner) { FinishPrivacyRefresh(AdPrivacyResult.UpdateFailed); return true; }
            Finish(false, _pendingCompletion);
            return true;
        }

        public void Dispose()
        {
            ++_version;
            _pendingCompletion = null;
            _privacyCompletion = null;
            _stage = Stage.Disposed;
        }
    }
}
