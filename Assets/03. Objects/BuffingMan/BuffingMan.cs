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
    private string _str_bufTime = string.Empty;

    private void Awake()
    {
        instance = this;

        Init();
    }

    private void Init()
    {
        _TM_admob.text = _str_normalState;
        _go_ablePortal.SetActive(true);
        _go_unablePortal.SetActive(false);

        _str_bufTime = AD.Managers.DataM._dic_player["GoogleAdMob"];
        if (_str_bufTime != "null")
        {

        }
    }

    #region Functions
    /// <summary>
    /// 보상형 광고 성공 후 리워드 적용
    /// 플레이어 버프
    /// Time으로 시간 계산 및 데이터 정리
    /// </summary>
    public void OnAdSuccess()
    {
        _go_admob.SetActive(false);
        _go_ablePortal.SetActive(false);
        _go_unablePortal.SetActive(true);

        AD.Managers.DataM._dic_player["GoogleAdMob"] = _str_bufTime = DateTime.Now.ToString();
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
            if (_str_bufTime == "null")
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