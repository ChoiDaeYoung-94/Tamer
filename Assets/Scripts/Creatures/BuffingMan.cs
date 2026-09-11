using System;
using AD.Advertising;

using UnityEngine;

public class BuffingMan : MonoBehaviour
{
    static BuffingMan instance;
    public static BuffingMan Instance { get { return instance; } }

    [SerializeField] private GameObject _ablePortal;
    [SerializeField] private GameObject _unablePortal;
    [SerializeField] private GameObject _admobObject;
    [SerializeField] private TextMesh _admobTextMesh;

    private const string _normalAdMessage = "클릭하여 광고 보상을 통해\n플레이어의 능력치를 증가시켜주세요!\n버프는 10분간 적용됩니다.";
    private const string _failedAdMessage = "광고가 로드되지 않았습니다.\n잠시 후 다시 시도해 주세요!";
    private const string _pausedAdMessage = "광고 보상은 현재 준비 중입니다.\n광고 없이 게임을 계속할 수 있습니다.";
    private bool _rewardAvailable;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    public void StartInit()
    {
        CheckAdMob();
    }

    #region Functions
    /// <summary>
    /// AdMob data check 후 버프 진행여부 결정
    /// </summary>
    private void CheckAdMob()
    {
        string adData = AD.Managers.DataM.LocalPlayerData["GoogleAdMob"];
        if (adData != "null")
        {
            double remainingTime = AD.TimeUtility.GetAdBuffRemainingTime(adData);
            if (remainingTime >= 0f)
            {
                PlayerUICanvas.Instance.SetBuff(remainingTime);

                if (!Player.Instance.IsBuffing)
                    Player.Instance.SetBuff();

                SetAdmobState(false);
                return;
            }
            else
            {
                AD.Managers.GoogleAdMobM.ResetAdMob();
            }
        }

        SetAdmobState(true);
    }

    /// <summary>
    /// 광고 상태 설정 (true: 광고 가능, false: 광고 불가능)
    /// </summary>
    public void SetAdmobState(bool isAvailable)
    {
        _rewardAvailable = isAvailable;
        bool enabled = isAvailable && (AD.Managers.GoogleAdMobM.HasNoAds || AD.Managers.GoogleAdMobM.CanRequestAds);
        _admobTextMesh.text = enabled ? RewardPrompt() : _pausedAdMessage;
        _ablePortal.SetActive(enabled);
        _unablePortal.SetActive(!enabled);
    }

    private string RewardPrompt() => AD.Managers.GoogleAdMobM.HasNoAds
        ? "클릭하여 플레이어의 능력치를 증가시켜주세요!\n버프는 10분간 적용됩니다."
        : _normalAdMessage;

    public void RequestAdReward()
    {
        if (!_rewardAvailable || AD.Managers.GoogleAdMobM.IsInProgress) return;
        AD.Managers.GoogleAdMobM.ShowRewardedAd(this, OnAdSuccess, outcome =>
        {
            if (outcome == RewardedAdOutcome.Rewarded) return;
            SetAdmobState(true);
            _admobTextMesh.text = outcome == RewardedAdOutcome.PolicyBlocked ? _pausedAdMessage
                : outcome == RewardedAdOutcome.Cancelled ? RewardPrompt() : _failedAdMessage;
        });
    }

    /// <summary>
    /// 보상형 광고 성공 후 리워드 적용
    /// 플레이어 버프
    /// Time으로 시간 계산 및 데이터 정리
    /// </summary>
    public void OnAdSuccess()
    {
        _admobObject.SetActive(false);
        SetAdmobState(false);

        AD.Managers.DataM.UpdateLocalData(key: "GoogleAdMob", value: DateTime.Now.ToString());

        CheckAdMob();
    }

    public void OnAdFailure()
    {
        SetAdmobState(true);
        _admobTextMesh.text = _failedAdMessage;
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player") && AD.Managers.DataM.LocalPlayerData["GoogleAdMob"] == "null")
        {
            SetAdmobState(true);
            _admobObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
            _admobObject.SetActive(false);
    }
}
