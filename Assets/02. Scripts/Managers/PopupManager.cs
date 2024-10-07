using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AD
{
    /// <summary>
    /// Popup 관리
    /// GameM.IsGame을 통해 게임씬, 로비씬 여부에 따라 
    /// 아무 팝업이 없이 뒤로 가기 버튼을 클릭시에
    /// 게임을 종료할지, 로비로 가겠냐는 팝업을 띄울지 정함
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        [Header("--- 세팅 ---")]
        [SerializeField, Tooltip("로비 팝업")]
        GameObject _go_popupLobby = null;
        [SerializeField, Tooltip("게임 종료 팝업")]
        GameObject _go_popupExit = null;
        [SerializeField, Tooltip("게임씬 힐 보상 팝업")]
        GameObject _go_popupHeal = null;
        [SerializeField, Tooltip("게임 오버 팝업")]
        GameObject _go_popupGameOver = null;
        [SerializeField, Tooltip("세팅 팝업")]
        GameObject _go_popupSetting = null;
        [SerializeField] GameObject _go_bgm = null;
        [SerializeField] GameObject _go_sfx = null;

        [Tooltip("popup을 관리할 Stack, Enable - Push, Disable - Pop")]
        Stack<GameObject> _popupStack = new Stack<GameObject>();

        /// <summary>
        /// 예외처리에 사용
        /// 로비씬 -> 오퍼월
        /// 게임씬에서 아이템을 선택하여서 사용 전인지에 대한 여부 판단
        /// Flow상 MainScene 진입 시 InitializeMain.cs에서 false
        /// </summary>
        bool isException = true;
        bool isFLow = false;

        /// <summary>
        /// Managers - Awake() -> Init()
        /// </summary>
        internal void Init()
        {
            AD.Managers.UpdateM._update -= Onupdate;
            AD.Managers.UpdateM._update += Onupdate;

            SetPopup();
        }

        /// <summary>
        /// 열여있는 팝업 닫고 초기화
        /// </summary>
        public void SetPopup()
        {
            if (_popupStack.Count > 0)
            {
                foreach (GameObject popup in _popupStack)
                    popup.SetActive(false);
            }

            _popupStack.Clear();
        }

        void Onupdate()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && !isFLow)
                {
                    DisablePop();
                }
            }
        }

        #region Functions
        /// <summary>
        /// Popup이 Enable 될 때 Push
        /// </summary>
        /// <param name="pop"></param>
        public void EnablePop(GameObject pop)
        {
            _popupStack.Push(pop);
            AD.Debug.Log("PopupManager", _popupStack.Count + "_popupStack.Count -> push");
        }

        /// <summary>
        /// Popup이 Disable 될 때 Pop하고 비활성화
        /// </summary>
        public void DisablePop()
        {
            AD.Managers.SoundM.UI_Click();

            if (isException)
            {
                AD.Debug.Log("PopupManager", isException + " - isException");
                return;
            }

            // 스택에 팝업이 있을 경우
            if (_popupStack.Count > 0)
            {
                GameObject popup = null;

                popup = _popupStack.Pop();
                AD.Debug.Log("PopupManager", _popupStack.Count + "_popupStack.Count -> pop");

                popup.SetActive(false);
            }
            else // 팝업 없을 경우 -> 게임 종료 or 로비로 돌아가는 팝업
            {
                if (!AD.Managers.GameM.IsGame)
                {
                    AD.Debug.Log("PopupManager", "lobby scene -> quit popup");

                    if (!_go_popupExit.activeSelf)
                        PopupExit();
                }
                else
                {
                    AD.Debug.Log("PopupManager", "game scene-> go lobby popup");

                    if (!_go_popupLobby.activeSelf)
                        PopupGoLobby();
                }
            }
        }

        internal void PopupGoLobby()
        {
            UnityEngine.Time.timeScale = 0;

            _go_popupLobby.SetActive(true);
        }

        internal void PopupExit()
        {
            UnityEngine.Time.timeScale = 0;

            _go_popupExit.SetActive(true);
        }

        internal void PopupHeal() => _go_popupHeal.SetActive(true);

        internal void PopupGameOver() => _go_popupGameOver.SetActive(true);

        internal void PopupSetting()
        {
            UnityEngine.Time.timeScale = 0;

            float bgm = PlayerPrefs.GetFloat("BGM", 1f);
            float sfx = PlayerPrefs.GetFloat("SFX", 1f);

            if (bgm == 1)
                _go_bgm.SetActive(false);
            else
                _go_bgm.SetActive(true);
            if (sfx == 1)
                _go_sfx.SetActive(false);
            else
                _go_sfx.SetActive(true);

            _go_popupSetting.SetActive(true);
        }

        public void ClosePopupSetting() => UnityEngine.Time.timeScale = 1;

        public void GoLobby()
        {
            UnityEngine.Time.timeScale = 1;

            AD.Managers.GameM.SwitchMainOrGameScene(AD.Define.Scenes.Main);
        }

        public void GameOver() => AD.Managers.GameM.GameOverGoLobby();

        public void ExitGame() => Application.Quit();

        public void Heal()
        {
            if (!AD.Managers.GoogleAdMobM.isInprogress)
                AD.Managers.GoogleAdMobM.ShowRewardedAd();
        }

        internal void SetException() => isException = true;

        internal void ReleaseException() => isException = false;

        internal void SetFLow() => isFLow = true;

        internal void ReleaseFLow() => isFLow = false;

        public void BGM()
        {
            float bgm = PlayerPrefs.GetFloat("BGM", 1f);

            if (bgm == 1)
            {
                _go_bgm.SetActive(true);
                bgm = 0;
            }
            else
            {
                _go_bgm.SetActive(false);
                bgm = 1;
            }

            AD.Managers.SoundM.SetBGMVolume(bgm);
            PlayerPrefs.SetFloat("BGM", bgm);
        }

        public void SFX()
        {
            float sfx = PlayerPrefs.GetFloat("SFX", 1f);

            if (sfx == 1)
            {
                _go_sfx.SetActive(true);
                sfx = 0;
            }
            else
            {
                _go_sfx.SetActive(false);
                sfx = 1;
            }

            AD.Managers.SoundM.SetSFXVolume(sfx);
            PlayerPrefs.SetFloat("SFX", sfx);
        }
        #endregion
    }
}
