using UnityEngine;
using UnityEngine.UI;

using TMPro;

public class PlayerUICanvas : MonoBehaviour
{
    private static PlayerUICanvas _instance;
    public static PlayerUICanvas Instance { get { return _instance; } }

    [Header("--- 세팅 panel_playerInfo ---")]
    [SerializeField] private TMP_Text _playerNickNameText;
    [SerializeField] private TMP_Text _captureCapacityText;
    [SerializeField] private TMP_Text _goldText;
    [SerializeField] private TMP_Text _playerHpText;
    [SerializeField] private Slider _playerHpSlider;

    [Header("--- 세팅 In Popup_playerInfo ---")]
    [SerializeField] private GameObject _popupPlayerInfoPanel;
    [SerializeField] private TMP_Text _popupNickNameText;
    [SerializeField] private TMP_Text _popupCaptureCapacityText;
    [SerializeField] private TMP_Text _popupGoldText;
    [SerializeField] private TMP_Text _popupPowerText;
    [SerializeField] private TMP_Text _popupAttackSpeedText;
    [SerializeField] private TMP_Text _popupMoveSpeedText;

    [Header("--- 세팅 panel_playerBuff ---")]
    [SerializeField] private GameObject _buffPanel;
    [SerializeField] private TMP_Text _buffTimeText;

    [Header("--- ETC ---")]
    [SerializeField] private GameObject _gameSceneUIPanel;
    [SerializeField] private GameObject _captureButton;

    private bool _isBuffActive = false;
    private double _remainingBuffTime = 0f;

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    private void Awake()
    {
        _instance = this;
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
        _popupPlayerInfoPanel.SetActive(false);
        _gameSceneUIPanel.SetActive(AD.Managers.GameM.IsGame);
    }

    /// <summary>
    /// 데이터 세팅
    /// </summary>
    private void DataSettings()
    {
        _playerNickNameText.text = $"{AD.Managers.DataM.LocalPlayerData["NickName"]}";
        UpdatePlayerInfo();
        UpdatePopPlayerInfo();
    }

    /// <summary>
    /// PlayerInfo Update
    /// </summary>
    public void UpdatePlayerInfo()
    {
        _captureCapacityText.text = $"{Player.Instance.GetCurMonsterCount()} / {Player.Instance._maxCaptureCapacity}";
        _goldText.text = $"Gold - {Player.Instance.Gold}";

        float maxHP = Player.Instance.ItmeAdditionalHp > Player.Instance.OriginalHP ? Player.Instance.ItmeAdditionalHp : Player.Instance.OriginalHP;
        _playerHpText.text = $"{Player.Instance.Hp} / {maxHP}";

        _playerHpSlider.maxValue = maxHP;
        _playerHpSlider.value = Player.Instance.Hp;
    }

    /// <summary>
    /// PlayerInfo Popup Update
    /// </summary>
    private void UpdatePopPlayerInfo()
    {
        _popupNickNameText.text = $"NickName - {AD.Managers.DataM.LocalPlayerData["NickName"]}";
        _popupCaptureCapacityText.text = $"CaptureCapacity - {Player.Instance.GetCurMonsterCount()} / {Player.Instance._maxCaptureCapacity}";
        _popupGoldText.text = $"Gold - {Player.Instance.Gold}";

        _popupPowerText.text = GetPlayerStat(Player.Instance.Power, Player.Instance._buffPower, "Power");
        _popupAttackSpeedText.text = GetPlayerStat(Player.Instance.AttackSpeed, Player.Instance._buffAttackSpeed, "AttackSpeed");
        _popupMoveSpeedText.text = GetPlayerStat(Player.Instance.MoveSpeed, Player.Instance._buffMoveSpeed, "MoveSpeed");
    }

    private string GetPlayerStat(float normalStat, float buffStat, string stat)
    {
        return _isBuffActive ? $"{stat} - {buffStat}" : $"{stat} - {normalStat}";
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
        _popupPlayerInfoPanel.SetActive(true);
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
        if (_isBuffActive)
        {
            _buffTimeText.text = AD.TimeUtility.FormatTimeString(_remainingBuffTime, padZero: true, includeSeconds: true, useColon: true);

            _remainingBuffTime -= Time.deltaTime;
            if (_remainingBuffTime <= 0f)
                AD.Managers.GoogleAdMobM.ResetAdMob();
        }
    }

    /// <summary>
    /// AdMob 광고 시청 후 buff 시작 or Main 씬 진입 후 buff시간이 남아 있을 경우
    /// </summary>
    public void SetBuff(double remainTime)
    {
        _remainingBuffTime = remainTime;
        _isBuffActive = true;

        UpdatePopPlayerInfo();
        _buffPanel.SetActive(true);
    }

    /// <summary>
    /// AdMob buff 종료 시
    /// </summary>
    public void EndBuff()
    {
        _buffPanel.SetActive(false);
        _isBuffActive = false;
        UpdatePopPlayerInfo();
    }

    #endregion

    public void EnableCapture() => _captureButton.SetActive(true);

    public void DisableCapture() => _captureButton.SetActive(false);

    public void OnClickCapture()
    {
        AD.Managers.SoundM.UI_Click();

        Player.Instance.Capture();
        _captureButton.SetActive(false);
    }

    #endregion
}
