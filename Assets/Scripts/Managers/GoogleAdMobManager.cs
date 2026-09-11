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
        private bool _initialized;
        private bool _initializing;
        private bool _loading;
        private bool _subscribed;
        private volatile bool _destroyed;
        private int _loadVersion;
        private int _sceneVersion;
        private float _loadDeadline;

        public bool IsInProgress => _session != null;

        // This project has no iOS AdMob app ID/native validation. Keep device tests Android-only.
        public bool CanRequestAds =>
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
            // Login/startup never initializes the SDK or requests an ad.
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
            if ((_loading || _initializing) && Time.realtimeSinceStartup >= _loadDeadline)
            {
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
            if (_destroyed || !CanRequestAds || IsInProgress || _loading || _initializing) return;
            Init();
            int version = ++_loadVersion;
            _loadDeadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            if (!_initialized)
            {
                _initializing = true;
                try
                {
                    // Conservative test-request treatment, not a claim about the user's
                    // age or a replacement for the unverified Console declaration.
                    MobileAds.SetRequestConfiguration(new RequestConfiguration
                    {
                        TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                        TagForUnderAgeOfConsent = TagForUnderAgeOfConsent.True,
                        MaxAdContentRating = MaxAdContentRating.G
                    });
                    MobileAds.Initialize(status => Enqueue(() =>
                    {
                        if (version != _loadVersion) return;
                        _initializing = false;
                        _initialized = status != null;
                        if (_initialized) LoadSampleAd(version);
                    }));
                }
                catch (Exception)
                {
                    _initializing = false;
                    DebugLogger.LogError("GoogleAdMobManager", "Test ad initialization failed.");
                }
                return;
            }
            LoadSampleAd(version);
        }

        private void LoadSampleAd(int version)
        {
            _loading = true;
            _loadDeadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            DestroyLoadedAd();
            try
            {
                RewardedAd.Load(AdRequestPolicy.TestRewardedAdUnit(
                    Application.platform == RuntimePlatform.IPhonePlayer), new AdRequest(), (ad, error) =>
                {
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
            ad.OnAdFullScreenContentClosed += () => Enqueue(() =>
                QueueClose(session));
            ad.OnAdFullScreenContentFailed += error => Enqueue(() =>
                CompleteSession(session, RewardedAdOutcome.Failed));
            try
            {
                if (Managers.Instance != null && Managers.SoundM != null)
                    _resumeBgm = Managers.SoundM.PauseBGMForAd();
                ad.Show(rewardInfo => Enqueue(session.EarnReward));
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
