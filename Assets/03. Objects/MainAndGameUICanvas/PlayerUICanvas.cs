#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

public class PlayerUICanvas : MonoBehaviour
{
    static PlayerUICanvas instance;
    public static PlayerUICanvas Instance { get { return instance; } }

    [Header("--- 세팅 panel_playerInfo ---")]
    [SerializeField] TMP_Text _TMP_playerNickName = null;
    [SerializeField] TMP_Text _TMP_captureCapacity = null;
    [SerializeField] TMP_Text _TMP_gold = null;
    [SerializeField] TMP_Text _TMP_HP = null;
    [SerializeField, Tooltip("Player HP UI")] Slider _Slider_HP = null;

    [Header("--- 세팅 In Popup_playerInfo ---")]
    [SerializeField, Tooltip("상단 패널 클릭 시 status popup")] GameObject _go_Popup_playerInfo = null;
    [SerializeField] TMP_Text _TMP_POPplayerNickName = null;
    [SerializeField] TMP_Text _TMP_POPcaptureCapacity = null;
    [SerializeField] TMP_Text _TMP_POPgold = null;
    [SerializeField] TMP_Text _TMP_POPpower = null;
    [SerializeField] TMP_Text _TMP_POPattackSpeed = null;
    [SerializeField] TMP_Text _TMP_POPmoveSpeed = null;

    [Header("--- 세팅 panel_playerBuff ---")]
    [SerializeField, Tooltip("AdMobBuff 남은 시간 표기하는 panel")] GameObject _go_panelAdMobBuff = null;
    [SerializeField, Tooltip("AdMobBuff 남은 시간 표기하는 TMP")] TMP_Text _TMP_remainingBuffTime = null;

    [Header("--- ETC ---")]
    [SerializeField, Tooltip("Game scene 진입 시")] GameObject _go_panel_gamesceneUI = null;
    [SerializeField, Tooltip("monster를 포획 가능 할 경우 포획 버튼 활성화")] GameObject _go_capture = null;

    [Header("--- 참고용 ---")]
    private bool _isBuff = false;
    private double _remainBuffTime = 0f;

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Player.cs, GameManager.cs 에서 호출
    /// </summary>
    public void StartInit()
    {
        ViewSettings();
        DataSettings();

        AD.Managers.UpdateM.OnUpdateEvent -= UpdateBuffPanel;
        AD.Managers.UpdateM.OnUpdateEvent += UpdateBuffPanel;
    }

    #region Functions

    #region Data Setting
    /// <summary>
    /// 뷰 세팅
    /// Main scene 진입 시, Main Game scene 전환 시
    /// </summary>
    public void ViewSettings()
    {
        _go_Popup_playerInfo.SetActive(false);
        _go_panel_gamesceneUI.SetActive(AD.Managers.GameM.IsGame);
    }

    /// <summary>
    /// 데이터 세팅
    /// </summary>
    private void DataSettings()
    {
        _TMP_playerNickName.text = $"{AD.Managers.DataM._dic_player["NickName"]}";

        UpdatePlayerInfo();
        UpdatePopPlayerInfo();
    }

    /// <summary>
    /// PlayerInfo Update
    /// </summary>
    public void UpdatePlayerInfo()
    {
        _TMP_captureCapacity.text = $"{Player.Instance.GetCurMonsterCount()} / {Player.Instance._maxCaptureCapacity}";
        _TMP_gold.text = $"Gold - {Player.Instance.Gold}";

        float maxHP = Player.Instance.ItemHp > Player.Instance.OrgHp ? Player.Instance.ItemHp : Player.Instance.OrgHp;
        _TMP_HP.text = $"{Player.Instance.Hp} / {maxHP}";

        _Slider_HP.maxValue = maxHP;
        _Slider_HP.value = Player.Instance.Hp;
    }

    /// <summary>
    /// PlayerInfo Popup Update
    /// </summary>
    private void UpdatePopPlayerInfo()
    {
        _TMP_POPplayerNickName.text = $"NickName - {AD.Managers.DataM._dic_player["NickName"]}";
        _TMP_POPcaptureCapacity.text = $"CaptureCapacity - {Player.Instance.GetCurMonsterCount()} / {Player.Instance._maxCaptureCapacity}";
        _TMP_POPgold.text = $"Gold - {Player.Instance.Gold}";

        _TMP_POPpower.text = _isBuff ? $"Power - {Player.Instance._buffPower}" : $"Power - {Player.Instance.Power}";
        _TMP_POPattackSpeed.text = _isBuff ? $"AttackSpeed - {Player.Instance._buffAttackSpeed}" : $"AttackSpeed - {Player.Instance.AttackSpeed}";
        _TMP_POPmoveSpeed.text = _isBuff ? $"MoveSpeed - {Player.Instance._buffMoveSpeed}" : $"MoveSpeed - {Player.Instance.MoveSpeed}";
    }
    #endregion

    /// <summary>
    /// panel_playerInfo 클릭 시
    /// </summary>
    public void OpenPopupPlayerInfo()
    {
        AD.Managers.SoundM.UI_Click();

        UpdatePopPlayerInfo();

        Time.timeScale = 0;
        _go_Popup_playerInfo.SetActive(true);
    }

    public void OpenSetting()
    {
        AD.Managers.SoundM.UI_Click();

        AD.Managers.PopupM.PopupSetting();
    }

    /// <summary>
    /// MiniMap 클릭 시
    /// </summary>
    public void OpenMap()
    {
        AD.Managers.SoundM.UI_Click();

        MiniMap.Instance.OpenMap();
    }

    /// <summary>
    /// Game scene에서 복귀버튼 클릭 시
    /// </summary>
    public void GoMainScene()
    {
        AD.Managers.SoundM.UI_Click();

        AD.Managers.PopupM.PopupGoLobby();
    }

    public void ClosePopupSetting() => Time.timeScale = 1;

    #region GoogleAdMob Buff
    /// <summary>
    /// BuffingMan.cs을 통해 변동되는 data로 작동
    /// buffpanel 관리 위함
    /// </summary>
    private void UpdateBuffPanel()
    {
        if (_isBuff)
        {
            _TMP_remainingBuffTime.text = AD.TimeUtility.FormatTimeString(_remainBuffTime, padZero: true, includeSeconds: true, useColon: true);

            _remainBuffTime -= Time.deltaTime;
            if (_remainBuffTime <= 0f)
                AD.Managers.GoogleAdMobM.ResetAdMob();
        }
    }

    /// <summary>
    /// AdMob 광고 시청 후 buff 시작 or Main 씬 진입 후 buff시간이 남아 있을 경우
    /// </summary>
    public void SetBuff(double remainTime)
    {
        _remainBuffTime = remainTime;
        _isBuff = true;

        UpdatePopPlayerInfo();

        _go_panelAdMobBuff.SetActive(true);
    }

    /// <summary>
    /// AdMob buff 종료 시
    /// </summary>
    public void EndBuff()
    {
        _go_panelAdMobBuff.SetActive(false);

        _isBuff = false;

        UpdatePopPlayerInfo();
    }
    #endregion

    public void EnableCapture() => _go_capture.SetActive(true);

    public void DisableCapture() => _go_capture.SetActive(false);

    public void OnClickCapture()
    {
        AD.Managers.SoundM.UI_Click();

        Player.Instance.Capture();
        _go_capture.SetActive(false);
    }

    #endregion

#if UNITY_EDITOR
    [CustomEditor(typeof(PlayerUICanvas))]
    public class customEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Main, Game scene에서 Player에 대한 정보, 세팅, minimap등 UI 제공", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}
