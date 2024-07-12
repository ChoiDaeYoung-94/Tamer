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

        [Tooltip("popup을 관리할 Stack, Enable - Push, Disable - Pop")]
        Stack<GameObject> _popupStack = new Stack<GameObject>();

        /// <summary>
        /// 예외처리에 사용
        /// 로비씬 -> 오퍼월
        /// 게임씬에서 아이템을 선택하여서 사용 전인지에 대한 여부 판단
        /// Flow상 MainScene 진입 시 InitializeMain.cs에서 false
        /// </summary>
        bool isException = true;

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
                if (Input.GetKeyDown(KeyCode.Escape))
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
                        _go_popupExit.SetActive(true);
                }
                else
                {
                    AD.Debug.Log("PopupManager", "lobby  scene-> go lobby popup");

                    if (!_go_popupLobby.activeSelf)
                        _go_popupLobby.SetActive(true);
                }
            }
        }

        internal void PopupGoLobby() => _go_popupLobby.SetActive(true);

        internal void PopupExit() => _go_popupExit.SetActive(true);

        internal void PopupHeal() => _go_popupHeal.SetActive(true);

        public void GoLobby() => AD.Managers.GameM.SwitchMainOrGameScene(AD.Define.Scenes.Main);

        public void ExitGame() => Application.Quit();

        public void Heal()
        {
            if (!AD.Managers.GoogleAdMobM.isInprogress)
                AD.Managers.GoogleAdMobM.ShowRewardedAd();
        }

        internal void SetException() => isException = true;

        internal void ReleaseException() => isException = false;
        #endregion
    }
}
