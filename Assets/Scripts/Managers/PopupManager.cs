using System.Collections.Generic;

using UnityEngine;

namespace AD
{
    /// <summary>
    /// 팝업 관리 클래스
    /// 게임 씬과 로비 씬에서 뒤로 가기 버튼 클릭 시 적절한 팝업을 표시
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        [SerializeField] private GameObject _popupLobby = null;
        [SerializeField] private GameObject _popupExit = null;
        [SerializeField] private GameObject _popupHeal = null;
        [SerializeField] private GameObject _popupGameOver = null;
        [SerializeField] private GameObject _popupSetting = null;
        [SerializeField] private GameObject _bgmToggle = null;
        [SerializeField] private GameObject _sfxToggle = null;

        private Stack<GameObject> _popupStack = new Stack<GameObject>();

        /// <summary>
        /// 예외처리에 사용
        /// 로비씬 -> 오퍼월
        /// 게임씬에서 아이템을 선택하여서 사용 전인지에 대한 여부 판단
        /// Flow상 MainScene 진입 시 InitializeMain.cs에서 false
        /// </summary>
        private bool _isException = true;
        private bool _isFlow = false;

        /// <summary>
        /// Managers - Awake() -> Init()
        /// </summary>
        public void Init()
        {
            AD.Managers.UpdateM.OnUpdateEvent -= OnUpdate;
            AD.Managers.UpdateM.OnUpdateEvent += OnUpdate;

            SetPopup();
        }

        /// <summary>
        /// 열려 있는 모든 팝업을 비활성화하고 팝업 스택을 초기화
        /// </summary>
        public void SetPopup()
        {
            foreach (GameObject popup in _popupStack)
            {
                popup.SetActive(false);
            }
            _popupStack.Clear();
        }

        private void OnUpdate()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && !_isFlow)
                {
                    DisablePop();
                }
            }
        }

        #region Functions
        /// <summary>
        /// 팝업이 활성화될 때 스택에 추가
        /// </summary>
        public void EnablePop(GameObject popup)
        {
            _popupStack.Push(popup);
            AD.DebugLogger.Log("PopupManager", $"_popupStack.Count: {_popupStack.Count}, 팝업 스택에 푸시됨");
        }

        /// <summary>
        /// 팝업이 비활성화될 때 스택에서 제거
        /// 팝업이 없으면 씬 상태에 따라 종료 팝업 또는 로비 전환 팝업을 표시
        /// </summary>
        public void DisablePop()
        {
            AD.Managers.SoundM.UI_Click();

            if (_isException)
            {
                AD.DebugLogger.Log("PopupManager", $"{_isException} - 예외 처리 활성");
                return;
            }

            if (_popupStack.Count > 0)
            {
                GameObject popup = _popupStack.Pop();
                AD.DebugLogger.Log("PopupManager", $"_popupStack.Count: {_popupStack.Count} 팝업 스택에서 팝업 제거됨");
                popup.SetActive(false);
            }
            else
            {
                if (!AD.Managers.GameM.IsGame)
                {
                    AD.DebugLogger.Log("PopupManager", "lobby scene -> quit popup");

                    if (!_popupExit.activeSelf)
                        PopupExit();
                }
                else
                {
                    AD.DebugLogger.Log("PopupManager", "game scene-> go lobby popup");

                    if (!_popupLobby.activeSelf)
                        PopupGoLobby();
                }
            }
        }

        public void PopupGoLobby()
        {
            UnityEngine.Time.timeScale = 0;
            _popupLobby.SetActive(true);
        }

        public void PopupExit()
        {
            UnityEngine.Time.timeScale = 0;
            _popupExit.SetActive(true);
        }

        public void PopupHeal() => _popupHeal.SetActive(true);

        public void PopupGameOver() => _popupGameOver.SetActive(true);

        public void PopupSetting()
        {
            UnityEngine.Time.timeScale = 0;

            float bgm = PlayerPrefs.GetFloat("BGM", 1f);
            float sfx = PlayerPrefs.GetFloat("SFX", 1f);

            // 토글 오브젝트 활성화 여부: 기본 값 1이면 비활성화, 그 외 활성화
            _bgmToggle.SetActive(bgm != 1f);
            _sfxToggle.SetActive(sfx != 1f);

            _popupSetting.SetActive(true);
        }

        public void ClosePopupSetting() => UnityEngine.Time.timeScale = 1;

        public void GoLobby()
        {
            UnityEngine.Time.timeScale = 1;
            AD.Managers.GameM.SwitchMainOrGameScene();
        }

        public void GameOver() => AD.Managers.GameM.GameOverGoLobby();

        public void ExitGame() => Application.Quit();

        public void Heal()
        {
            if (!AD.Managers.GoogleAdMobM.IsInProgress)
                AD.Managers.GoogleAdMobM.ShowRewardedAd();
        }

        public void SetException() => _isException = true;

        public void ReleaseException() => _isException = false;

        public void SetFlow() => _isFlow = true;

        public void ReleaseFlow() => _isFlow = false;

        public void BGM()
        {
            float bgm = PlayerPrefs.GetFloat("BGM", 1f);

            if (bgm == 1f)
            {
                _bgmToggle.SetActive(true);
                bgm = 0f;
            }
            else
            {
                _bgmToggle.SetActive(false);
                bgm = 1f;
            }

            AD.Managers.SoundM.SetBGMVolume(bgm);
            PlayerPrefs.SetFloat("BGM", bgm);
        }

        public void SFX()
        {
            float sfx = PlayerPrefs.GetFloat("SFX", 1f);

            if (sfx == 1f)
            {
                _sfxToggle.SetActive(true);
                sfx = 0f;
            }
            else
            {
                _sfxToggle.SetActive(false);
                sfx = 1f;
            }

            AD.Managers.SoundM.SetSFXVolume(sfx);
            PlayerPrefs.SetFloat("SFX", sfx);
        }
        #endregion
    }
}
