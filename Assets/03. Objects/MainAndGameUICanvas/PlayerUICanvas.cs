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
    internal void StartInit()
    {
        ViewSettings();
        DataSettings();

        AD.Managers.UpdateM._update -= UpdateBuffPanel;
        AD.Managers.UpdateM._update += UpdateBuffPanel;
    }

    #region Functions
    /// <summary>
    /// 뷰 세팅
    /// Main scene 진입 시, Main Game scene 전환 시
    /// </summary>
    internal void ViewSettings()
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
    internal void UpdatePlayerInfo()
    {
        _TMP_captureCapacity.text = $"{AD.Managers.DataM._dic_player["CurCaptureCapacity"]} / {AD.Managers.DataM._dic_player["MaxCaptureCapacity"]}";
        _TMP_gold.text = $"Gold - {Player.Instance.Gold}";
        _TMP_HP.text = $"{Player.Instance.Hp} / {Player.Instance.OrgHp}";

        _Slider_HP.maxValue = Player.Instance.OrgHp;
        _Slider_HP.value = Player.Instance.Hp;

        if (_go_Popup_playerInfo.activeSelf)
            UpdatePopPlayerInfo();
    }

    /// <summary>
    /// PlayerInfo Popup Update
    /// </summary>
    internal void UpdatePopPlayerInfo()
    {
        _TMP_POPplayerNickName.text = $"NickName - {AD.Managers.DataM._dic_player["NickName"]}";
        _TMP_POPcaptureCapacity.text = $"CaptureCapacity - {AD.Managers.DataM._dic_player["CurCaptureCapacity"]} / {AD.Managers.DataM._dic_player["MaxCaptureCapacity"]}";
        _TMP_POPgold.text = $"Gold - {Player.Instance.Gold}";

        _TMP_POPpower.text = _isBuff ? $"Power - {Player.Instance._bufPower}" : $"Power - {Player.Instance.Power}";
        _TMP_POPattackSpeed.text = _isBuff ? $"AttackSpeed - {Player.Instance._bufAttackSpeed}" : $"AttackSpeed - {Player.Instance.AttackSpeed}";
        _TMP_POPmoveSpeed.text = _isBuff ? $"MoveSpeed - {Player.Instance._bufMoveSpeed}" : $"MoveSpeed - {Player.Instance.MoveSpeed}";
    }

    /// <summary>
    /// panel_playerInfo 클릭 시
    /// </summary>
    public void OpenPopupPlayerInfo()
    {
        UpdatePopPlayerInfo();

        _go_Popup_playerInfo.SetActive(true);
    }

    /// <summary>
    /// MiniMap 클릭 시
    /// </summary>
    public void OpenMap()
    {
        MiniMap.Instance.OpenMap();
    }

    /// <summary>
    /// Game scene에서 복귀버튼 클릭 시
    /// </summary>
    public void GoMainScene()
    {
        AD.Managers.PopupM.PopupGoLobby();
    }

    #region GoogleAdMob Buff
    /// <summary>
    /// BuffingMan.cs을 통해 변동되는 data로 작동
    /// buffpanel 관리 위함
    /// </summary>
    private void UpdateBuffPanel()
    {
        if (_isBuff)
        {
            _TMP_remainingBuffTime.text = AD.Time.TimeToString(_remainBuffTime, plusZero: true, plusSecond: true, colon: true);

            _remainBuffTime -= Time.deltaTime;
            if (_remainBuffTime <= 0f)
                AD.Managers.GoogleAdMobM.ResetAdMob();
        }
    }

    /// <summary>
    /// AdMob 광고 시청 후 buff 시작 or Main 씬 진입 후 buff시간이 남아 있을 경우
    /// </summary>
    internal void SetBuff(double remainTime)
    {
        _remainBuffTime = remainTime;
        _isBuff = true;

        UpdatePopPlayerInfo();

        _go_panelAdMobBuff.SetActive(true);
    }

    /// <summary>
    /// AdMob buff 종료 시
    /// </summary>
    internal void EndBuff()
    {
        _go_panelAdMobBuff.SetActive(false);

        _isBuff = false;

        UpdatePopPlayerInfo();
    }
    #endregion

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
