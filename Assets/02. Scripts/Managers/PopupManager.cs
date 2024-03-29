using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AD
{
    /// <summary>
    /// Popup 관리
    /// </summary>
    public class PopupManager
    {
        [Tooltip("popup을 관리할 Stack, Enable - Push, Disable - Pop")]
        Stack<GameObject> _popupStack = new Stack<GameObject>();

        /// <summary>
        /// 게임씬, 로비씬 여부에 따라 
        /// 아무 팝업이 없이 뒤로 가기 버튼을 클릭시에
        /// 게임을 종료할지, 로비로 가겠냐는 팝업을 띄울지 정함
        /// </summary>
        bool isGameScene = false;

        /// <summary>
        /// 예외처리에 사용
        /// 로비씬 -> 오퍼월
        /// 게임씬에서 아이템을 선택하여서 사용 전인지에 대한 여부 판단
        /// </summary>
        bool isException = true;

        internal void Init()
        {
            AD.Managers.UpdateM._update -= Onupdate;
            AD.Managers.UpdateM._update += Onupdate;
        }

        void Onupdate()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    DisablePop(isEscape: true);
                }
            }
        }

        #region Popup 관련
        /// <summary>
        /// Popup이 Enable 될 때 Push
        /// </summary>
        /// <param name="pop"></param>
        public void EnablePop(GameObject pop)
        {
            _popupStack.Push(pop);
            AD.Debug.Log("PopupManager", _popupStack.Count + "_popupStack.Count -> push함");
        }

        /// <summary>
        /// Popup이 Disable 될 때 Pop하고 비활성화
        /// </summary>
        public void DisablePop(bool isEscape = false)
        {
            if (isException)
            {
                AD.Debug.Log("PopupManager", isException + " - isException");
                return;
            }

            // 스택에 팝업이 있을 경우
            if (_popupStack.Count > 0)
            {
                AD.Debug.Log("PopupManager", isEscape + " - isEscape");
                AD.Debug.Log("PopupManager", isGameScene + " - isGameScene");

                // 로비 씬
                if (!isGameScene)
                {
                    // 뒤로가기 버튼 클릭
                    if (isEscape)
                    {
                        GameObject popup = null;

                        popup = _popupStack.Peek();
                        popup.GetComponent<Button>().onClick.Invoke();
                    }
                    else // 직접 닫기
                    {
                        GameObject popup = null;

                        popup = _popupStack.Pop();
                        popup.SetActive(false);
                    }
                }
                else // 게임 씬
                {
                    if (isEscape)
                    {
                        GameObject popup = null;

                        popup = _popupStack.Peek();
                        popup.GetComponent<Button>().onClick.Invoke();
                    }
                    else
                    {
                        GameObject popup = null;

                        popup = _popupStack.Pop();
                        popup.SetActive(false);
                    }
                }
            }
            else // 팝업 없을 경우 -> 게임 종료 or 로비로 돌아가는 팝업
            {
                if (!isGameScene)
                {
                    GameObject quitPop = null; // 나가기 팝업 받아야 함
                    if (!quitPop.activeSelf)
                        quitPop.SetActive(true);
                }
                else
                {
                    GameObject goLobby = null; // 로비 가는 팝업 받아야 함
                    if (!goLobby.activeSelf)
                        goLobby.SetActive(true);
                }
            }
        }
        #endregion

        #region Functions
        /// <summary>
        /// 모든 팝업 닫기
        /// </summary>
        public void DisableAllPop()
        {
            foreach (GameObject popup in _popupStack)
                popup.SetActive(false);

            _popupStack.Clear();
        }

        /// <summary>
        /// 로비 씬 진입 시 초기화
        /// </summary>
        public void InitLobby()
        {
            _popupStack.Clear();
            isGameScene = false;
            isException = true;
        }

        /// <summary>
        /// 게임 씬 진입 시 초기화
        /// </summary>
        public void InitGame()
        {
            _popupStack.Clear();
            isGameScene = true;
            isException = true;
        }

        public void SetException()
        {
            isException = true;
        }

        public void ReleaseException()
        {
            isException = false;
        }
        #endregion
    }
}
