using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuffingMan : MonoBehaviour
{
    static BuffingMan instance;
    public static BuffingMan Instance { get { return instance; } }

    [Header("--- 세팅 ---")]
    [SerializeField, Tooltip("Admob 광고 가능 포탈")] GameObject _go_ablePortal = null;
    [SerializeField, Tooltip("Admob 광고 불가능 포탈")] GameObject _go_unablePortal = null;
    [SerializeField, Tooltip("Admob 광고 오브젝트")] GameObject _go_admob = null;
    [SerializeField, Tooltip("Admob 광고 실패 시 표기 위함")] TextMesh _TM_admob = null;

    [Header("--- 참고용 ---")]
    private string _str_normalState = "클릭하여 광고 보상을 통해\n플레이어의 능력치를 증가시켜주세요!\n버프는 10분간 적용됩니다.";
    private string _str_failState = "광고가 로드되지 않았습니다.\n잠시 후 다시 시도해 주세요!";

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
        string str_temp = AD.Managers.DataM.LocalPlayerData["GoogleAdMob"];
        if (str_temp != "null")
        {
            double remainTime = AD.TimeUtility.GetAdBuffRemainingTime(str_temp);
            if (remainTime >= 0f)
            {
                PlayerUICanvas.Instance.SetBuff(remainTime);

                if (!Player.Instance.isBuffing)
                    Player.Instance.SetBuff();

                UnableAdMob();
                return;
            }
            else
                AD.Managers.GoogleAdMobM.ResetAdMob();
        }

        ableAdMob();
    }

    /// <summary>
    /// AdMob 광고 시청이 가능할 경우
    /// </summary>
    public void ableAdMob()
    {
        _TM_admob.text = _str_normalState;
        _go_ablePortal.SetActive(true);
        _go_unablePortal.SetActive(false);
    }

    /// <summary>
    /// AdMob 광고 시청이 불가능할 경우
    /// </summary>
    private void UnableAdMob()
    {
        _TM_admob.text = _str_failState;
        _go_ablePortal.SetActive(false);
        _go_unablePortal.SetActive(true);
    }

    /// <summary>
    /// 보상형 광고 성공 후 리워드 적용
    /// 플레이어 버프
    /// Time으로 시간 계산 및 데이터 정리
    /// </summary>
    public void OnAdSuccess()
    {
        _go_admob.SetActive(false);
        UnableAdMob();

        AD.Managers.DataM.UpdateLocalData(key: "GoogleAdMob", value: DateTime.Now.ToString());

        CheckAdMob();
    }

    public void OnAdFailure()
    {
        _TM_admob.text = _str_failState;
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            if (AD.Managers.DataM.LocalPlayerData["GoogleAdMob"] == "null")
            {
                _TM_admob.text = _str_normalState;
                _go_admob.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_admob.SetActive(false);
    }
}