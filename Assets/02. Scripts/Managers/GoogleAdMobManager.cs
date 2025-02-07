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
        [SerializeField, Tooltip("비동기 AD 실행 확인")]
        public bool isInprogress = false;
        [SerializeField, Tooltip("보상 받은 여부 확인")]
        public bool isReceive = false;

#if UNITY_ANDROID && Debug
        // GoogleAdMob에서 제공하는 TestID
        private string _adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_ANDROID && !Debug
        private string _adUnitId = "ca-app-pub-4045654268115042/5756715712";
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

            AD.Managers.UpdateM.OnUpdateEvent -= CheckReward;
            AD.Managers.UpdateM.OnUpdateEvent += CheckReward;
        }

        private void CheckReward()
        {
            if (isReceive)
            {
                isReceive = !isReceive;

                string temp_sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                if (temp_sceneName == "Main")
                    BuffingMan.Instance.OnAdSuccess();
                else if (temp_sceneName == "Game")
                    Portal.Instance.RewardHeal();
            }
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

            AD.DebugLogger.Log("GoogleAdMobManager", "Loading the rewarded ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            RewardedAd.Load(_adUnitId, adRequest,
                (RewardedAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        AD.DebugLogger.LogError("GoogleAdMobManager", "Rewarded ad failed to load an ad " + "with error : " + error);
                        return;
                    }

                    AD.DebugLogger.Log("GoogleAdMobManager", "Rewarded ad loaded with response : " + ad.GetResponseInfo());

                    _rewardedAd = ad;

                    RegisterReloadHandler(_rewardedAd);
                });
        }

        /// <summary>
        /// Show reward ad.
        /// </summary>
        public void ShowRewardedAd()
        {
            isInprogress = true;

            const string rewardMsg =
                "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

            if (AD.Managers.DataM.LocalPlayerData["GooglePlay"].Contains(AD.GameConstants.IAPItem.ProductNoAds.ToString()))
            {
                isInprogress = false;
                isReceive = true;

                return;
            }

            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                AD.Managers.SoundM.PauseBGM();
                _rewardedAd.Show((Reward reward) =>
                {
                    AD.DebugLogger.Log("GoogleAdMobManager", String.Format(rewardMsg, reward.Type, reward.Amount));

                    AD.Managers.SoundM.UnpauseBGM();

                    isReceive = true;
                });
            }
            else
            {
                AD.Managers.SoundM.UnpauseBGM();

                BuffingMan.Instance.OnAdFailure();
                isInprogress = false;
            }
        }

        /// <summary>
        /// 광고 본 후 처리
        /// 보상 받기, 다음 보상형 광고 미리 로드
        /// </summary>
        /// <param name="ad"></param>
        private void RegisterReloadHandler(RewardedAd ad)
        {
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                isInprogress = false;

                AD.DebugLogger.Log("GoogleAdMobManager", "Rewarded Ad full screen content closed.");

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedAd();
            };

            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                BuffingMan.Instance.OnAdFailure();
                isInprogress = false;
                isReceive = false;

                AD.DebugLogger.LogError("GoogleAdMobManager", "Rewarded ad failed to open full screen content " +
                               "with error : " + error);

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedAd();
            };
        }

        #region Functions
        /// <summary>
        /// 보상형 광고 데이터 리셋 시 사용
        /// GoogleAdMob data의 경우 local에만 저장하면 됨
        /// -> Player data 갱신 시 GoogleAdMob에 대한 내용을 따로 추가하지 않았기 때문에
        /// 해당 데이터는 로컬을 우선시하게 됨 즉 서버를 통해 Player data를 갱신하게 되더라도
        /// 서버에 있는 GoogleAdMob가 우선이 아니라 로컬에 있는 GoogleAdMob를 우선시 하게 됨
        /// </summary>
        public void ResetAdMob()
        {
            AD.Managers.DataM.UpdateLocalData(key: "GoogleAdMob", value: "null");

            PlayerUICanvas.Instance.EndBuff();
            Player.Instance.EndBuff();
            if (BuffingMan.Instance)
                BuffingMan.Instance.ableAdMob();
        }
        #endregion
    }
}