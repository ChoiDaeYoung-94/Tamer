/*

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TapjoyUnity;

namespace AD
{
    public class TapjoyManager : MonoBehaviour
    {
        [Header("--- 세팅 ---")]
        [SerializeField, Tooltip("Tapjoy Android SDK key")]
        string _str_androidKey = string.Empty;
        [SerializeField, Tooltip("Tapjoy IOS SDK key")]
        string _str_iosKey = string.Empty;
        [SerializeField, Tooltip("Tapjoy Placement")]
        string _str_placement = string.Empty;

        // 참고용
        TJPlacement _placement = null;
        Coroutine _co_timer = null;
        bool _isDismiss = false;
        int _balance = 0;

        internal void Init()
        {
#if UNITY_EDITOR

#else
        TapjoyInit();

        SetHandles();
#endif
        }

        private void OnDestroy()
        {
            ReleaseHandles();
        }

        #region Functions
        void TapjoyInit()
        {
            // Tapjoy 연결
#if UNITY_ANDROID
            if (!Tapjoy.IsConnected)
                Tapjoy.Connect(this._str_androidKey);
#elif UNITY_IOS
		if (!Tapjoy.IsConnected)
            Tapjoy.Connect(this._str_iosKey);
#endif

            Tapjoy.SetUserID(Server.Player.ID);

            this._placement = TJPlacement.CreatePlacement(this._str_placement);
            this._placement.RequestContent();
        }

        /// <summary>
        /// 비디오 콘텐츠를 보여주기 전에 앱의 자체 오디오를 음소거해야 함
        /// 그렇지 않으면, 비디오의 오디오와 앱 자체 오디오가 서로 오버랩 되거나 충돌할 수 있음
        /// </summary>
        internal void ShowOfferwall()
        {
            // + 음악 제거, 블럭
            Managers.PopupM.SetException();

            StartCoroutine(CoShowOfferwall());
        }

        IEnumerator CoShowOfferwall()
        {
            while (!Tapjoy.IsConnected)
            {
                TapjoyInit();
                yield return null;
            }

            this._placement.RequestContent();
            while (!this._placement.IsContentReady())
            {
                yield return null;
            }

            this._placement.ShowContent();
        }

        /// <summary>
        /// Tapjoy Offerwall 종료 후 3.5초 기달린 뒤 작업
        /// </summary>
        void OfferwallContentDismiss()
        {
            if (_co_timer == null)
                _co_timer = StartCoroutine(Timer(3.5f));
        }

        IEnumerator Timer(float time)
        {
            if (time > 0f)
                yield return new WaitForSeconds(time);

            Tapjoy.GetCurrencyBalance();

            _co_timer = null;
        }

        /// <summary>
        /// GetCurrencyBalance 후 가상 화폐의 변화를 측정 후 0 이상일 경우 바로 사용
        /// </summary>
        /// <param name="balance"></param>
        void CheckResult(int balance)
        {
            if (balance > 0)
            {
                Tapjoy.SpendCurrency(balance);
                this._balance = balance;
            }
            else
            {
                if (this._isDismiss)
                    ReleaseException();
            }
        }

        void ReleaseException()
        {
            this._isDismiss = false;

            // + 블록 제거
            Managers.PopupM.ReleaseException();
        }
        #endregion

        #region Tapjoy Handles & CallBacks
        /// <summary>
        /// Init시 필요한 Handle Actions
        /// </summary>
        void SetHandles()
        {
            // Tapjoy 연결 콜백 받기 위함
            Tapjoy.OnConnectSuccess += HandleConnectSuccess;
            Tapjoy.OnConnectFailure += OnConnectFailureHandler;

            // Tapjoy 화폐 잔액 불러온 뒤(getCurrencyBalance API) 잘 불러 왔는지에 대한 콜백 받기 위함
            Tapjoy.OnGetCurrencyBalanceResponse += HandleGetCurrencyBalanceResponse;
            Tapjoy.OnGetCurrencyBalanceResponseFailure += HandleGetCurrencyBalanceResponseFailure;

            // getCurrencyBalance API를 호출했을때 이전 잔액과 차이가 존재하면 호출
            Tapjoy.OnEarnedCurrency += HandleEarnedCurrency;

            // 가상화폐 지급 시 * Tapjoy.AwardCurrency(10);
            Tapjoy.OnAwardCurrencyResponse += HandleAwardCurrencyResponse;
            Tapjoy.OnAwardCurrencyResponseFailure += HandleAwardCurrencyResponseFailure;

            // 가상화폐 사용 시 * Tapjoy.SpendCurrency(10);
            Tapjoy.OnSpendCurrencyResponse += HandleSpendCurrencyResponse;
            Tapjoy.OnSpendCurrencyResponseFailure += HandleSpendCurrencyResponseFailure;

            // placement의 content요청에 대한 콜백
            TJPlacement.OnRequestSuccess += HandlePlacementRequestSuccess;
            TJPlacement.OnRequestFailure += HandlePlacementRequestFailure;

            // placement의 content가 준비 됐는지 확인
            TJPlacement.OnContentReady += HandlePlacementContentReady;

            // placement의 content가 열였는지 확인
            TJPlacement.OnContentShow += HandlePlacementContentShow;

            // placement의 content가 닫힐 때 호출될 대리자
            TJPlacement.OnContentDismiss += HandlePlacementContentDismiss;
        }

        void ReleaseHandles()
        {
            Tapjoy.OnConnectSuccess -= HandleConnectSuccess;
            Tapjoy.OnConnectFailure -= OnConnectFailureHandler;

            Tapjoy.OnGetCurrencyBalanceResponse -= HandleGetCurrencyBalanceResponse;
            Tapjoy.OnGetCurrencyBalanceResponseFailure -= HandleGetCurrencyBalanceResponseFailure;

            Tapjoy.OnEarnedCurrency -= HandleEarnedCurrency;

            Tapjoy.OnAwardCurrencyResponse -= HandleAwardCurrencyResponse;
            Tapjoy.OnAwardCurrencyResponseFailure -= HandleAwardCurrencyResponseFailure;

            Tapjoy.OnSpendCurrencyResponse -= HandleSpendCurrencyResponse;
            Tapjoy.OnSpendCurrencyResponseFailure -= HandleSpendCurrencyResponseFailure;

            TJPlacement.OnRequestSuccess -= HandlePlacementRequestSuccess;
            TJPlacement.OnRequestFailure -= HandlePlacementRequestFailure;

            TJPlacement.OnContentReady -= HandlePlacementContentReady;

            TJPlacement.OnContentShow -= HandlePlacementContentShow;

            TJPlacement.OnContentDismiss -= HandlePlacementContentDismiss;
        }

        void HandleConnectSuccess()
        {
            AD.Debug.Log("TapjoyManager", "TapJoy Connect Success");

            //Tapjoy 연결 후 Tapjoy.GetCurrencyBalance();를 통해 잔액 미리 확인
            // * Tapjoy에서 자주 확인해주길 권장함
            Tapjoy.GetCurrencyBalance();
        }

        void OnConnectFailureHandler()
        {
            AD.Debug.Log("TapjoyManager", "TapJoy Connect Failed -> reconnct");

            Tapjoy.Connect(this._str_androidKey);
        }

        void HandleGetCurrencyBalanceResponse(string currencyName, int balance)
        {
            AD.Debug.Log("TapjoyManager", "HandleGetCurrencyBalanceResponse: currencyName: " + currencyName + ", balance: " + balance);

            CheckResult(balance);
        }

        void HandleGetCurrencyBalanceResponseFailure(string error)
        {
            AD.Debug.Log("TapjoyManager", "HandleGetCurrencyBalanceResponseFailure: " + error);
        }

        void HandleEarnedCurrency(string currencyName, int amount)
        {
            AD.Debug.Log("TapjoyManager", "HandleEarnedCurrency: currencyName: " + currencyName + ", amount: " + amount);

            CheckResult(amount);
        }

        void HandleAwardCurrencyResponse(string currencyName, int balance)
        {
            AD.Debug.Log("TapjoyManager", "HandleAwardCurrencySucceeded: currencyName: " + currencyName + ", balance: " + balance);
        }

        void HandleAwardCurrencyResponseFailure(string error)
        {
            AD.Debug.Log("TapjoyManager", "HandleAwardCurrencyResponseFailure: " + error);
        }

        void HandleSpendCurrencyResponse(string currencyName, int balance)
        {
            AD.Debug.Log("TapjoyManager", "HandleSpendCurrencyResponse: currencyName: " + currencyName + ", balance: " + balance);

            AD.Debug.Log("TapjoyManager", $"cur balance - {balance}");
            if (balance == 0)
            {
                // balance를 잘 사용 했단 소리 -> 현존하는 재화 서버로 ++
            }


            ReleaseException();
        }

        void HandleSpendCurrencyResponseFailure(string error)
        {
            AD.Debug.Log("TapjoyManager", "HandleSpendCurrencyResponseFailure: " + error);
        }

        void HandlePlacementRequestSuccess(TJPlacement placement)
        {
            AD.Debug.Log("TapjoyManager", "HandlePlacementRequestSuccess: placementName: " + placement.GetName());
        }

        void HandlePlacementRequestFailure(TJPlacement placement, string error)
        {
            AD.Debug.Log("TapjoyManager", "HandlePlacementRequestFailure: " + error);

            this._placement.RequestContent();
        }

        // 콘텐츠를 실제로 표시할 수 있을 때 호출
        void HandlePlacementContentReady(TJPlacement placement)
        {
            AD.Debug.Log("TapjoyManager", "HandlePlacementContentReadySuccess: placementName: " + placement.GetName());
        }

        /// <summary>
        /// 플레이스먼트의 콘텐츠를 사용자에게 성공적으로 표시 한 후에는 콘텐츠를 다시 요청
        /// 즉, *p.RequestContent();*를 다시 호출하여 다음 콘텐츠 위해 플레이스먼트를 "다시 로드"해야합니다.
        /// 컨텐츠를 재요청 하지 않으면 *p.ShowContent();*를 다시 호출 할 수 없습니다. 
        /// 콘텐츠를 재요청하기 전에 콘텐츠를 다시 표시하려고하면 ShowContent가 실패합니다.
        /// </summary>
        /// <param name="placement"></param>
        void HandlePlacementContentShow(TJPlacement placement)
        {
            AD.Debug.Log("TapjoyManager", "HandlePlacementContentShow: placementName: " + placement.GetName());
            this._placement.RequestContent();
        }

        /// <summary>
        /// Content가 닫힐 때 호출될 대리자
        /// </summary>
        void HandlePlacementContentDismiss(TJPlacement placement)
        {
            Btn_Setting.Instance.PlayMusic();
            this._isDismiss = true;

            OfferwallContentDismiss();
        }
        #endregion

#if UNITY_EDITOR
        [CustomEditor(typeof(TapjoyManager))]
        public class customEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox("Tapjoy 관련 세팅 및 메서드", MessageType.Info);

                base.OnInspectorGUI();
            }
        }
#endif
    }
}

*/