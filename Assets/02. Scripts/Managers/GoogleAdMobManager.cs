using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using GoogleMobileAds;
using GoogleMobileAds.Api;

namespace AD
{
    public class GoogleAdMobManager : MonoBehaviour
    {
#if UNITY_ANDROID && Debug
        private string _adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_ANDROID && !Debug
        private string _adUnitId = "ca-app-pub-4045654268115042~2814857605";
#endif

        private RewardedAd _rewardedAd;

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 필요한 데이터 미리 받아 둠
        /// </summary>
        public void Init()
        {
            // Initialize the Google Mobile Ads SDK.
            MobileAds.Initialize((InitializationStatus initStatus) =>
            {
                // This callback is called once the MobileAds SDK is initialized.
                LoadRewardedAd();
            });
        }

        /// <summary>
        /// Loads the rewarded ad.
        /// </summary>
        public void LoadRewardedAd()
        {
            // Clean up the old ad before loading a new one.
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }

            UnityEngine.Debug.Log("Loading the rewarded ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest.Builder().Build();

            // send the request to load the ad.
            RewardedAd.Load(_adUnitId, adRequest,
                (RewardedAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        UnityEngine.Debug.LogError("Rewarded ad failed to load an ad " + "with error : " + error);
                        return;
                    }

                    UnityEngine.Debug.Log("Rewarded ad loaded with response : " + ad.GetResponseInfo());

                    _rewardedAd = ad;
                });
        }

        /// <summary>
        /// Show reward ad.
        /// </summary>
        public void ShowRewardedAd()
        {
            const string rewardMsg =
                "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                _rewardedAd.Show((Reward reward) =>
                {
                    // TODO: Reward the user.
                    UnityEngine.Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));
                });
            }
        }

        /// <summary>
        /// 다음 보상형 광고 미리 로드
        /// </summary>
        /// <param name="ad"></param>
        private void RegisterReloadHandler(RewardedAd ad)
        {
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                UnityEngine.Debug.Log("Rewarded Ad full screen content closed.");

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedAd();
            };

            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                UnityEngine.Debug.LogError("Rewarded ad failed to open full screen content " +
                               "with error : " + error);

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedAd();
            };
        }
    }
}