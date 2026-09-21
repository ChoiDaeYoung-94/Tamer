using System;
using System.Collections.Generic;
using AD.Advertising;
using GoogleMobileAds.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace AD
{
    /// <summary>Owns one explicitly requested rewarded ad and its original recipient.</summary>
    public class GoogleAdMobManager : MonoBehaviour
    {
        private const float LoadTimeoutSeconds = 30f;
        private readonly object _callbackLock = new object();
        private readonly Queue<PendingCallback> _callbacks = new Queue<PendingCallback>();
        private sealed class PendingCallback
        {
            public Action Run;
            public Action Discard;
        }
        private RewardedAd _rewardedAd;
        private RewardedAd _showingAd;
        private RewardedAdSession _session;
        private RewardedAdSession _closingSession;
        private Action _resumeBgm;
        private AdConsentGate _consent;
        private bool _initialized;
        private bool _initializing;
        private bool _loading;
        private bool _subscribed;
        private volatile bool _destroyed;
        private int _loadVersion;
        private int _sceneVersion;
        private float _loadDeadline;
        private LocalAgeChoice _ageSelection;
        public const string AgeChoicePreferenceKey = "Tamer.Privacy.AgeChoice";
        public LocalAgeChoice AgeSelection
        {
            get
            {
                if (_ageSelection == null)
                {
                    _ageSelection = new LocalAgeChoice(
                        () => PlayerPrefs.GetString(AgeChoicePreferenceKey, ""),
                        value => { PlayerPrefs.SetString(AgeChoicePreferenceKey, value); PlayerPrefs.Save(); });
                    _ageSelection.Changed += InvalidateAgeContext;
                }
                return _ageSelection;
            }
        }
        public bool CanChangeAge => !_destroyed && !IsInProgress && !IsConsentBusy;

        private void InvalidateAgeContext()
        {
            ++_loadVersion;
            ++_sceneVersion; // Also invalidate a closed impression's delayed reward receipt.
            _session?.Invalidate(); // Keep native fullscreen ownership until its real close.
            _consent?.Dispose();
            _consent = null;
            _loading = _initializing = _initialized = false;
            DestroyLoadedAd();
            if (_subscribed) BeginConsent(false);
        }

#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
        private SoundManager _harnessSound;
        public event Action<string, double> HarnessEvent;
        public void ConfigureHarnessAudio(SoundManager sound) => _harnessSound = sound;
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("TAMER_AD_TEST_HARNESS")]
        private void TraceHarness(string name)
        {
#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
            double timestamp = (double)System.Diagnostics.Stopwatch.GetTimestamp() /
                System.Diagnostics.Stopwatch.Frequency;
            Enqueue(() => HarnessEvent?.Invoke(name, timestamp));
#endif
        }

        public bool IsInProgress => _session != null;
        public bool IsConsentBusy => _consent != null && _consent.IsBusy;
        public bool PrivacyOptionsRequired => AgeSelection.HasAge &&
            _consent != null && _consent.PrivacyOptionsRequired;

        public void ShowPrivacyOptions()
        {
            if (_destroyed || !AgeSelection.HasAge || IsInProgress || _loading || _initializing || _consent == null) return;
            if (!PrivacyOptionsRequired || IsConsentBusy) return;
            ++_loadVersion;
            ++_sceneVersion; // Withdrawn choices also invalidate delayed reward receipts.
            DestroyLoadedAd();
            _consent.OpenPrivacyOptions(_ => { }); // Never auto-load after a privacy choice.
        }

        // This project has no iOS AdMob app ID/native validation. Keep device tests Android-only.
        public bool CanRequestAds =>
            AgeSelection.HasAge && AgeTreatmentPolicy.IsReviewed(AgeSelection.Value) &&
            (Application.isEditor || Application.platform == RuntimePlatform.Android) &&
            AdRequestPolicy.CanRequestTestAds(
            Application.isEditor, Debug.isDebugBuild,
#if TAMER_TEST_ADS
            true,
#else
            false,
#endif
            Application.platform == RuntimePlatform.Android,
            Application.platform == RuntimePlatform.IPhonePlayer,
            Application.isBatchMode);

        public bool HasNoAds
        {
            get
            {
                if (Managers.Instance == null || Managers.DataM == null ||
                    Managers.DataM.LocalPlayerData == null ||
                    !Managers.DataM.LocalPlayerData.TryGetValue("GooglePlay", out string purchases))
                    return false;
                return AdEntitlement.HasNoAds(purchases);
            }
        }

        public void Init()
        {
            if (_subscribed || _destroyed) return;
            _subscribed = true;
            UnitySceneManager.activeSceneChanged += OnSceneChanged;
            // Each app-owned manager starts with a fresh gate, never saved consent.
            // Regional review currently blocks this before any SDK operation.
            // Consent completion alone never initializes Mobile Ads or loads an ad.
            BeginConsent(false);
        }

        private void Update()
        {
            while (TryDequeue(out var callback))
            {
                try
                {
                    if (_destroyed) callback.Discard?.Invoke();
                    else callback.Run();
                }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            // Drain a same-frame reward/close batch before releasing fullscreen state.
            // Android's separate callback threads may still deliver an earned reward
            // in a later frame; that receipt is independent of presentation cleanup.
            var closing = _closingSession;
            _closingSession = null;
            if (closing != null)
            {
                try { CompleteSession(closing, RewardedAdOutcome.Cancelled); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            if ((_loading || _initializing) && (!IsConsentBusy || _consent.IsUpdating) &&
                Time.realtimeSinceStartup >= _loadDeadline)
            {
                // Expire only UMP's network update, not a visible consent form.
                _consent?.ExpireUpdate();
                // Invalidate late loads; never treat a visible native ad as closed.
                _loadVersion++;
                _loading = _initializing = false;
            }
        }

        private bool TryDequeue(out PendingCallback callback)
        {
            lock (_callbackLock)
            {
                callback = _callbacks.Count > 0 ? _callbacks.Dequeue() : null;
                return callback != null;
            }
        }

        private void Enqueue(Action callback, Action discarded = null)
        {
            lock (_callbackLock)
            {
                if (!_destroyed)
                {
                    _callbacks.Enqueue(new PendingCallback { Run = callback, Discard = discarded });
                    return;
                }
            }
            discarded?.Invoke();
        }

        /// <summary>On-demand sample ads only. Production requests remain blocked.</summary>
        public void LoadRewardedAd()
        {
            if (_destroyed || !CanRequestAds || IsInProgress || _loading || _initializing || IsConsentBusy) return;
            Init();
            BeginConsent(true);
        }

        private void BeginConsent(bool loadAfterConsent)
        {
            if (_destroyed || !CanRequestAds || IsInProgress || _loading || _initializing || IsConsentBusy ||
                !AgeTreatmentPolicy.TryCreatePlan(AgeSelection.Value, out var plan)) return;
            int version = ++_loadVersion;
            _loadDeadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            _initializing = true;
            if (_consent == null)
            {
                try
                {
                    // Apply protection before UMP and Mobile Ads initialization, including age changes.
                    MobileAds.SetRequestConfiguration(CreateRequestConfiguration(plan));
                    TraceHarness("request_flags_set");
                    _consent = new AdConsentGate(new GoogleUmpConsentClient(), plan.UmpUnderAgeOfConsent,
                        callback => Enqueue(callback), name => TraceHarness(name));
                }
                catch (Exception)
                {
                    _initializing = false;
                    return;
                }
            }
            _consent.Request(allowed =>
            {
                if (_destroyed || version != _loadVersion) return;
                if (!allowed || !loadAfterConsent || !CanRequestAds) { _initializing = false; return; }
                _loadDeadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
                if (_initialized)
                {
                    _initializing = false;
                    LoadSampleAd(version);
                }
                else InitializeSampleSdk(version);
            });
        }

        // Pure configuration construction; no SDK call and no deprecated TFCD/TFUA tags.
        private static RequestConfiguration CreateRequestConfiguration(AgeTreatmentPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            AgeRestrictedTreatment treatment;
            switch (plan.Advertising)
            {
                case AdAgeTreatment.Child: treatment = AgeRestrictedTreatment.Child; break;
                case AdAgeTreatment.Teen: treatment = AgeRestrictedTreatment.Teen; break;
                case AdAgeTreatment.Unspecified: treatment = AgeRestrictedTreatment.Unspecified; break;
                default: throw new ArgumentOutOfRangeException(nameof(plan));
            }
            return new RequestConfiguration { AgeRestrictedTreatment = treatment, MaxAdContentRating = MaxAdContentRating.G };
        }

        private void InitializeSampleSdk(int version)
        {
            try
            {
                TraceHarness("initialize_call");
                MobileAds.Initialize(status =>
                {
                    TraceHarness("initialize_callback");
                    Enqueue(() =>
                    {
                        if (version != _loadVersion) return;
                        _initializing = false;
                        _initialized = status != null;
                        if (_initialized && _consent.CanRequestAds) LoadSampleAd(version);
                    });
                });
            }
            catch (Exception)
            {
                _initializing = false;
                DebugLogger.LogError("GoogleAdMobManager", "Test ad initialization failed.");
            }
        }

        private void LoadSampleAd(int version)
        {
            _loading = true;
            _loadDeadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            DestroyLoadedAd();
            try
            {
                TraceHarness("load_call");
                RewardedAd.Load(AdRequestPolicy.TestRewardedAdUnit(
                    Application.platform == RuntimePlatform.IPhonePlayer), new AdRequest(), (ad, error) =>
                {
                    TraceHarness(error == null && ad != null ? "load_callback_ok" : "load_callback_failed");
                    Enqueue(() =>
                    {
                        if (version != _loadVersion)
                        {
                            ad?.Destroy();
                            return;
                        }
                        _loading = false;
                        if (error != null || ad == null)
                        {
                            ad?.Destroy();
                            DebugLogger.LogError("GoogleAdMobManager", "Test rewarded ad unavailable; retry manually.");
                            return;
                        }
                        _rewardedAd = ad;
                    }, () => ad?.Destroy());
                });
            }
            catch (Exception)
            {
                _loading = false;
                DebugLogger.LogError("GoogleAdMobManager", "Test ad load failed.");
            }
        }

        /// <summary>The caller supplies the reward, never the scene active at callback time.</summary>
        public bool ShowRewardedAd(MonoBehaviour owner, Action reward, Action<RewardedAdOutcome> finished)
        {
            if (_destroyed || IsInProgress || owner == null || reward == null || finished == null) return false;
            Init();
            var session = CreateSession(owner, reward, finished);
            _session = session;

            if (HasNoAds)
            {
                session.EarnReward();
                CompleteSession(session, RewardedAdOutcome.Cancelled);
                return true;
            }
            if (!CanRequestAds)
            {
                CompleteSession(session, RewardedAdOutcome.PolicyBlocked);
                return true;
            }
            if (_consent == null || !_consent.CanRequestAds)
            {
                DestroyLoadedAd();
                CompleteSession(session, RewardedAdOutcome.Unavailable);
                LoadRewardedAd();
                return true;
            }
            bool canShow;
            try { canShow = _rewardedAd != null && _rewardedAd.CanShowAd(); }
            catch (Exception)
            {
                try { DestroyLoadedAd(); }
                finally { CompleteSession(session, RewardedAdOutcome.Failed); }
                return true;
            }
            if (!canShow)
            {
                CompleteSession(session, RewardedAdOutcome.Unavailable);
                LoadRewardedAd();
                return true;
            }

            var ad = _rewardedAd;
            _rewardedAd = null;
            _showingAd = ad;
#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
            ad.OnAdFullScreenContentOpened += () => TraceHarness("opened_callback");
#endif
            ad.OnAdFullScreenContentClosed += () =>
            {
                TraceHarness("closed_callback");
                Enqueue(() => QueueClose(session));
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                TraceHarness("failed_callback");
                Enqueue(() => CompleteSession(session, RewardedAdOutcome.Failed));
            };
            try
            {
                var sound = Managers.Instance != null ? Managers.SoundM : null;
#if UNITY_EDITOR || TAMER_AD_TEST_HARNESS
                if (_harnessSound != null) sound = _harnessSound;
#endif
                if (sound != null) _resumeBgm = sound.PauseBGMForAd();
                TraceHarness("show_call");
                ad.Show(rewardInfo =>
                {
                    TraceHarness("earned_callback");
                    Enqueue(session.EarnReward);
                });
            }
            catch (Exception)
            {
                CompleteSession(session, RewardedAdOutcome.Failed);
            }
            return true;
        }

        private void CompleteSession(RewardedAdSession session, RewardedAdOutcome outcome)
        {
            // Stale callbacks cannot clear a newer session or resume its music.
            if (_session != session)
            {
                if (outcome == RewardedAdOutcome.Failed) session.Invalidate();
                return;
            }
            _session = null;
            _closingSession = null;
            var shownAd = _showingAd;
            _showingAd = null;
            var resume = _resumeBgm;
            _resumeBgm = null;
            try { shownAd?.Destroy(); }
            finally
            {
                try { resume?.Invoke(); }
                finally { session.Complete(outcome); }
            }
            // A later explicit interaction can load again; no automatic retry loop.
        }

        private void QueueClose(RewardedAdSession session)
        {
            if (_session == session) _closingSession = session;
        }

        private RewardedAdSession CreateSession(MonoBehaviour owner, Action reward,
            Action<RewardedAdOutcome> finished)
        {
            int sceneHandle = UnitySceneManager.GetActiveScene().handle;
            int sceneVersion = _sceneVersion;
            return new RewardedAdSession(
                () => !_destroyed && owner != null && _sceneVersion == sceneVersion &&
                    UnitySceneManager.GetActiveScene().handle == sceneHandle,
                reward, finished);
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            // Invalidate the receipt but keep the fullscreen lock until real close/failure.
            // Destroy() is not a reliable way to dismiss native fullscreen UI.
            _sceneVersion++;
            _session?.Invalidate();
        }

        private void DestroyLoadedAd()
        {
            var ad = _rewardedAd;
            _rewardedAd = null;
            ad?.Destroy();
        }

        private void OnDestroy()
        {
            if (_ageSelection != null) _ageSelection.Changed -= InvalidateAgeContext;
            _consent?.Dispose();
            lock (_callbackLock) _destroyed = true;
            _loadVersion++;
            UnitySceneManager.activeSceneChanged -= OnSceneChanged;
            try
            {
                if (_session != null) CompleteSession(_session, RewardedAdOutcome.Failed);
            }
            catch (Exception exception) { Debug.LogException(exception); }

            // Each cleanup step must run even if another step throws. Keep the
            // callback loop outside finally handlers on Unity's Mono runtime.
            try { DestroyLoadedAd(); }
            catch (Exception exception) { Debug.LogException(exception); }

            while (TryDequeue(out var callback))
            {
                try { callback.Discard?.Invoke(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        public void ResetAdMob()
        {
            Managers.DataM.UpdateLocalData(key: "GoogleAdMob", value: "null");
            if (PlayerUICanvas.Instance != null) PlayerUICanvas.Instance.EndBuff();
            if (Player.Instance != null) Player.Instance.EndBuff();
            if (BuffingMan.Instance != null) BuffingMan.Instance.SetAdmobState(true);
        }
    }
}
