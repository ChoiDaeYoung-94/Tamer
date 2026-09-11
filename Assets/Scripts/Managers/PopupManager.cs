using System.Collections.Generic;
using AD.Advertising;
using TMPro;

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

        private readonly Stack<GameObject> _popupStack = new Stack<GameObject>();
        private readonly HashSet<GameObject> _exceptionOwners = new HashSet<GameObject>();
        private readonly HashSet<GameObject> _flowOwners = new HashSet<GameObject>();
        private UpdateManager _updateManager;

        private bool IsException => _isException || _exceptionOwners.Count > 0;
        private bool IsFlow => _isFlow || _flowOwners.Count > 0;

        /// <summary>
        /// 예외처리에 사용
        /// 로비씬 -> 오퍼월
        /// 게임씬에서 아이템을 선택하여서 사용 전인지에 대한 여부 판단
        /// Flow상 MainScene 진입 시 InitializeMain.cs에서 false
        /// </summary>
        private bool _isException = true;
        private bool _isFlow = false;
        private TMP_Text _healMessage;

        /// <summary>
        /// Managers - Awake() -> Init()
        /// </summary>
        public void Init()
        {
            if (_updateManager != null) _updateManager.OnUpdateEvent -= OnUpdate;
            _updateManager = AD.Managers.UpdateM;
            if (_updateManager != null) _updateManager.OnUpdateEvent += OnUpdate;

            SetPopup();
        }

        /// <summary>
        /// 열려 있는 모든 팝업을 비활성화하고 팝업 스택을 초기화
        /// </summary>
        public void SetPopup()
        {
            // Clear before OnDisable callbacks mutate registration.
            var popups = _popupStack.ToArray();
            _popupStack.Clear();
            foreach (GameObject popup in popups)
                if (popup != null) popup.SetActive(false);
        }

        private void OnUpdate()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && !IsFlow)
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
            if (popup == null || !popup.activeInHierarchy) return;
            RemovePopup(popup);
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

            if (IsException)
            {
                AD.DebugLogger.Log("PopupManager", $"{_isException} - 예외 처리 활성");
                return;
            }

            RemovePopup(null);
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

        public void PopupHeal()
        {
            if (_healMessage == null)
            {
                // Reuse the existing prompt without changing prefab GUIDs or button labels.
                foreach (var text in _popupHeal.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text == "Would you like to watch an ad to heal your HP?")
                    {
                        _healMessage = text;
                        break;
                    }
            }
            SetHealMessage(GoogleAdMobM.HasNoAds ? "Restore your HP?"
                : GoogleAdMobM.CanRequestAds ? "Would you like to watch an ad to heal your HP?"
                : "Ad healing is currently unavailable. You can keep playing.");
            _popupHeal.SetActive(true);
        }

        /// <summary>User close preserves the same exception gate as Android Back.</summary>
        public void RequestClosePopup(GameObject target)
        {
            if (Managers.Instance != null && Managers.SoundM != null) Managers.SoundM.UI_Click();
            if (IsException) return;
            ClosePopup(target);
        }

        /// <summary>Close a specific popup without consuming a newer popup above it.</summary>
        public void ClosePopup(GameObject target)
        {
            RemovePopup(target);
            if (target != null) target.SetActive(false);
        }

        // Registration removal must not close another popup or play a click sound.
        public void RemovePopup(GameObject target)
        {
            var openPopups = _popupStack.ToArray();
            _popupStack.Clear();
            for (int index = openPopups.Length - 1; index >= 0; index--)
            {
                var popup = openPopups[index];
                if (popup != null && popup != target && popup.activeInHierarchy)
                    _popupStack.Push(popup);
            }
        }

        public void RegisterBlocker(GameObject owner, bool flow)
        {
            if (owner != null) (flow ? _flowOwners : _exceptionOwners).Add(owner);
        }

        public void UnregisterPopup(GameObject owner)
        {
            RemovePopup(owner);
            _exceptionOwners.Remove(owner);
            _flowOwners.Remove(owner);
        }

        private void OnDestroy()
        {
            if (_updateManager != null) _updateManager.OnUpdateEvent -= OnUpdate;
        }

        private GoogleAdMobManager GoogleAdMobM => Managers.GoogleAdMobM;

        private void SetHealMessage(string message)
        {
            if (_healMessage != null) _healMessage.text = message;
        }

        public void PopupGameOver() => _popupGameOver.SetActive(true);

        public void PopupSetting()
        {
            DeletionSettingsEntry.Ensure(_popupSetting, this);
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
            var player = Player.Instance;
            if (player == null || GoogleAdMobM.IsInProgress) return;
            GoogleAdMobM.ShowRewardedAd(player, () =>
            {
                player.Heal();
                if (this != null) ClosePopup(_popupHeal);
            }, outcome =>
            {
                if (outcome == RewardedAdOutcome.Rewarded || this == null) return;
                SetHealMessage(outcome == RewardedAdOutcome.PolicyBlocked
                    ? "Ad healing is currently unavailable. You can keep playing."
                    : outcome == RewardedAdOutcome.Cancelled
                        ? "Ad closed. You can keep playing."
                        : "Ad is not ready. Please try again shortly.");
                _popupHeal.SetActive(true);
            });
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
