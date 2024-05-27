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

    [Header("--- ETC ---")]
    [SerializeField, Tooltip("Game scene 진입 시")] GameObject _go_panel_gamesceneUI = null;

    /// <summary>
    /// Main scene에서 생성 후 StartInit
    /// * 그 후 세팅을 위해 Game scene에서 StartInit 호출
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
        _go_Popup_playerInfo.SetActive(false);
        _go_panel_gamesceneUI.SetActive(AD.Managers.GameM.IsGame);

        Settings();
    }

    #region Functions
    /// <summary>
    /// 데이터 뷰 세팅
    /// </summary>
    private void Settings()
    {
        _TMP_playerNickName.text = $"{AD.Managers.DataM._dic_player["NickName"]}";

        UpdatePlayerInfo();
        UpdatePopPlayerInfo();
    }

    /// <summary>
    /// 추후 미니맵, 복귀 등 게임씬 작업 시 진행
    /// </summary>
    private void GameSettings()
    {

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

        _TMP_POPpower.text = $"Power - {Player.Instance.Power}";
        _TMP_POPattackSpeed.text = $"AttackSpeed - {Player.Instance.AttackSpeed}";
        _TMP_POPmoveSpeed.text = $"MoveSpeed - {Player.Instance.MoveSpeed}";
    }

    public void OpenPopupPlayerInfo()
    {
        _go_Popup_playerInfo.SetActive(true);
    }

    public void ClosePopupPlayerInfo()
    {
        AD.Managers.PopupM.DisablePop();
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
