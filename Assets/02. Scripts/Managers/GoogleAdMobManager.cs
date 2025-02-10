using System;

using UnityEngine;

using GoogleMobileAds.Api;

namespace AD
{
    /// <summary>
    /// Google Mobile Ads SDK를 통해 보상형 광고를 관리
    /// Managers의 Awake() 에서 초기화되며, 광고 로드 및 표시, 보상 처리, 재로드 기능을 제공
    /// </summary>
    public class GoogleAdMobManager : MonoBehaviour
    {
        public bool IsInProgress = false;
        public bool IsReceived = false;

#if UNITY_ANDROID && Debug
        // GoogleAdMob에서 제공하는 TestID
        private string _adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_ANDROID && !Debug
        private string _adUnitId = "ca-app-pub-4045654268115042/5756715712";
#endif

        private RewardedAd _rewardedAd;

        /// <summary>
        /// Google Mobile Ads SDK를 초기화하고, 보상형 광고를 로드
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

        /// <summary>
        /// 광고 보상 여부를 체크
        /// 보상을 받았다면, 현재 활성 씬에 따라 보상 성공 처리를 호출
        /// </summary>
        private void CheckReward()
        {
            if (IsReceived)
            {
                IsReceived = !IsReceived;

                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                if (currentScene == "Main")
                    BuffingMan.Instance.OnAdSuccess();
                else if (currentScene == "Game")
                    Portal.Instance.RewardHeal();
            }
        }

        /// <summary>
        /// 보상형 광고를 로드
        /// 기존 광고가 있다면 제거한 후 새로 로드
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
                        AD.DebugLogger.LogError("GoogleAdMobManager", $"Rewarded ad failed to load an ad with error : {error}");
                        return;
                    }

                    AD.DebugLogger.Log("GoogleAdMobManager", $"Rewarded ad loaded with response : {ad.GetResponseInfo()}");

                    _rewardedAd = ad;

                    RegisterReloadHandler(_rewardedAd);
                });
        }

        /// <summary>
        /// 보상형 광고를 표시
        /// 플레이어가 'No Ads' 제품을 보유 중이면 바로 보상을 처리
        /// </summary>
        public void ShowRewardedAd()
        {
            IsInProgress = true;

            const string rewardMsg =
                "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

            if (AD.Managers.DataM.LocalPlayerData["GooglePlay"].Contains(AD.GameConstants.IAPItem.ProductNoAds.ToString()))
            {
                IsInProgress = false;
                IsReceived = true;

                return;
            }

            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                AD.Managers.SoundM.PauseBGM();
                _rewardedAd.Show((Reward reward) =>
                {
                    AD.DebugLogger.Log("GoogleAdMobManager", String.Format(rewardMsg, reward.Type, reward.Amount));

                    AD.Managers.SoundM.UnpauseBGM();

                    IsReceived = true;
                });
            }
            else
            {
                AD.Managers.SoundM.UnpauseBGM();

                BuffingMan.Instance.OnAdFailure();
                IsInProgress = false;
            }
        }

        /// <summary>
        /// 광고가 종료되거나 실패한 후 처리 로직을 등록
        /// 광고가 닫히거나 실패하면 다음 광고를 미리 로드
        /// </summary>
        private void RegisterReloadHandler(RewardedAd ad)
        {
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                IsInProgress = false;

                AD.DebugLogger.Log("GoogleAdMobManager", "Rewarded Ad full screen content closed.");

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedAd();
            };

            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                BuffingMan.Instance.OnAdFailure();
                IsInProgress = false;
                IsReceived = false;

                AD.DebugLogger.LogError("GoogleAdMobManager", $"Rewarded ad failed to open full screen content with error : {error}");

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
            if (BuffingMan.Instance != null)
                BuffingMan.Instance.SetAdmobState(true);
        }
        #endregion
    }
}