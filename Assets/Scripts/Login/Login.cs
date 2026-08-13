using System;
using System.Threading;
using System.Collections.Generic;

using UnityEngine;

using PlayFab;
using PlayFab.ClientModels;

using Cysharp.Threading.Tasks;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace AD
{
    /// <summary>
    /// 로그인 관리 클래스 (PlayFab, Google Play)
    ///
    /// 어떤 상황에서도 게임 진입이 막히지 않도록 다단계 fallback을 사용한다.
    /// 1) Google Play Games 자동 로그인
    /// 2) Google Play Games 수동 로그인 (계정 선택 UI 노출)
    /// 3) 이전에 성공했던 Google Play id 캐시로 PlayFab 로그인 (기존 계정 유지)
    /// 4) Android 기기 id 로그인 (CreateAccount)
    /// 5) 단말에 저장된 Custom id 로그인 (CreateAccount)
    ///
    /// 모든 단계에 timeout과 재시도가 걸려 있으며, 최종 실패 시에도 멈추지 않고
    /// 재시도 패널을 노출한다. (무한 로딩 = 스토어 정책 위반이므로 반드시 회피)
    /// </summary>
    public class Login : MonoBehaviour
    {
        [Header("--- UI Elements ---")]
        [SerializeField] private GameObject _loading;
        [SerializeField] private TMPro.TMP_Text _loadingText;
        [SerializeField] private GameObject _retry;
        [Tooltip("재시도 패널에 실패 사유를 표시 (선택 사항, 연결하지 않아도 동작)")]
        [SerializeField] private TMPro.TMP_Text _retryText;
        [SerializeField] private GameObject _nicknamePanel;
        [SerializeField] private TMPro.TMP_Text _nicknameInput;
        [SerializeField] private GameObject _nicknameRulePanel;
        [SerializeField] private GameObject _nicknameConflictPanel;

        #region Constants

        private const string LogTag = "[Tamer/Login]";

        private const string EmailDomain = "@AeDeong.com";
        private const string PlayFabPassword = "AeDeong";
        private const string TestAccountId = "testAccount";

        // 이전에 로그인에 성공했던 Google Play id -> GPGS 인증이 실패해도 같은 계정으로 진입하기 위해 보관
        private const string PrefsKeyGpgsId = "AD_LastGpgsId";
        // GPGS를 전혀 사용할 수 없는 환경에서 사용하는 단말 고유 id
        private const string PrefsKeyCustomId = "AD_CustomId";
        // 이 단말이 어떤 방식으로 계정을 만들었는지 (계정이 갈라지는 것을 방지)
        private const string PrefsKeyLoginMode = "AD_LoginMode";

        private const string LoginModeGpgs = "gpgs";
        private const string LoginModeDevice = "device";

        private static readonly TimeSpan GpgsTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan ServerSyncTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan NetworkWaitTimeout = TimeSpan.FromSeconds(5);

        private const int MaxApiAttempts = 3;

        #endregion

        private CancellationTokenSource _cts;
        private bool _isLoginRunning;
        // 닉네임 등록이 필요한 신규 계정인지 (프로필 조회 실패 시 판단 근거로 사용)
        private bool _isNewAccount;

        #region Unity Lifecycle

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                PlayGamesPlatform.DebugLogEnabled = true;
                PlayGamesPlatform.Activate();
            }
            catch (Exception e)
            {
                // Activate 실패해도 아래 fallback으로 진입 가능해야 한다
                LogStep($"PlayGamesPlatform.Activate 실패 -> {e.Message}");
            }
#endif
        }

        private void Start()
        {
            _cts = new CancellationTokenSource();
            StartLogin();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region Login Flow

        private void StartLogin()
        {
            if (_isLoginRunning)
            {
                LogStep("이미 로그인 진행 중 -> 중복 요청 무시");
                return;
            }

            if (_cts == null || _cts.IsCancellationRequested)
                return;

            _isLoginRunning = true;
            RunLoginAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// 재시도 버튼에서 호출 (prefab 연결 유지)
        /// </summary>
        public void RetryConnection()
        {
            if (_isLoginRunning)
                return;

            LogStep("사용자 재시도 요청");
            StartLogin();
        }

        private async UniTask RunLoginAsync(CancellationToken token)
        {
            ShowLoading("LogIn...");

            try
            {
                if (!await WaitForNetworkAsync(token))
                {
                    ShowRetry("Please check your network connection.");
                    return;
                }

                bool loggedIn;
#if UNITY_EDITOR
                loggedIn = await LoginWithTestAccountAsync(token);
#elif UNITY_ANDROID
                loggedIn = await LoginOnAndroidAsync(token);
#else
                loggedIn = await LoginWithDeviceAsync(token);
#endif

                if (token.IsCancellationRequested)
                    return;

                if (!loggedIn)
                {
                    ShowRetry("Sign-in failed. Please try again.");
                    return;
                }

                await ResolveProfileAsync(token);
            }
            catch (OperationCanceledException)
            {
                // scene 이동 / 오브젝트 파괴 -> 정상 종료
            }
            catch (Exception e)
            {
                // 예외로 인해 로딩 화면에 갇히는 상황을 막는다
                LogStep($"로그인 처리 중 예외 -> {e}");
                ShowRetry("Sign-in failed. Please try again.");
            }
            finally
            {
                _isLoginRunning = false;
            }
        }

        /// <summary>
        /// 네트워크 연결을 잠시 기다린다. 부팅 직후에는 아직 연결이 잡히지 않았을 수 있다.
        /// </summary>
        private async UniTask<bool> WaitForNetworkAsync(CancellationToken token)
        {
            if (IsInternetAvailable())
                return true;

            LogStep("네트워크 미연결 -> 연결 대기");
            return await WaitUntilAsync(IsInternetAvailable, NetworkWaitTimeout, token);
        }

        private static bool IsInternetAvailable() => Application.internetReachability != NetworkReachability.NotReachable;

        #endregion

        #region Platform Login

        /// <summary>
        /// Android 로그인. GPGS가 실패해도 반드시 진입할 수 있도록 단계적으로 fallback 한다.
        /// </summary>
        private async UniTask<bool> LoginOnAndroidAsync(CancellationToken token)
        {
            string cachedGpgsId = PlayerPrefs.GetString(PrefsKeyGpgsId, string.Empty);

            // 이미 기기 계정으로 플레이 중인 단말은 계속 기기 계정을 사용한다.
            // (나중에 Google Play를 쓸 수 있게 되어도 계정이 갈라지면 진행 상황이 끊긴다)
            if (string.IsNullOrEmpty(cachedGpgsId)
                && PlayerPrefs.GetString(PrefsKeyLoginMode, string.Empty) == LoginModeDevice)
            {
                LogStep("기기 계정으로 사용 중인 단말 -> 기기 계정 로그인 유지");
                return await LoginWithDeviceAsync(token);
            }

            string gpgsId = await AuthenticateGooglePlayAsync(token);

            if (!string.IsNullOrEmpty(gpgsId))
            {
                // 다음 실행에서 GPGS 인증이 실패하더라도 같은 계정으로 진입하기 위해 저장
                PlayerPrefs.SetString(PrefsKeyGpgsId, gpgsId);
                PlayerPrefs.Save();

                // 계정이 특정된 상태이므로 실패해도 다른 계정으로 진입시키지 않는다.
                // (여기서 기기 계정으로 넘어가면 빈 계정으로 들어가 진행 상황이 사라진 것처럼 보인다)
                return await LoginOrRegisterWithEmailAsync(gpgsId, token);
            }

            if (!string.IsNullOrEmpty(cachedGpgsId))
            {
                // GPGS 인증에 실패해도 PlayFab 자격 증명은 id 문자열만 있으면 되므로
                // 캐시된 id로 기존 계정에 그대로 로그인한다 (진행 상황 보존)
                LogStep("GPGS 인증 실패 -> 캐시된 Google Play id로 로그인 시도");
                return await LoginOrRegisterWithEmailAsync(cachedGpgsId, token);
            }

            if (token.IsCancellationRequested)
                return false;

            // 이미 플레이한 기록이 있는 단말이라면 기기 계정을 새로 만들지 않는다.
            // 기존 계정 대신 빈 계정으로 진입하면 진행 상황이 사라진 것처럼 보이므로,
            // 차라리 재시도를 유도하는 편이 낫다. (이 버전 이전 유저는 id 캐시가 없다)
            if (HasLocalProgress())
            {
                LogStep("기존 플레이 기록 있음 -> 기기 계정 생성 대신 재시도 유도");
                return false;
            }

            // Google Play 계정을 한 번도 쓴 적이 없는 단말 -> 기기 계정으로라도 진입시킨다
            LogStep("사용 가능한 Google Play 계정 없음 -> 기기 계정으로 로그인");
            return await LoginWithDeviceAsync(token);
        }

        /// <summary>
        /// 이 단말에서 케릭터를 만들어 플레이한 적이 있는지 (Sex는 케릭터 생성 시 저장된다)
        /// </summary>
        private static bool HasLocalProgress()
        {
            var local = AD.Managers.DataM.LocalPlayerData;
            if (local == null)
                return false;

            return local.TryGetValue("Sex", out string sex) && !string.IsNullOrEmpty(sex) && sex != "null";
        }

        /// <summary>
        /// GPGS 인증 후 사용자 id를 반환. 실패 시 null.
        /// </summary>
        private async UniTask<string> AuthenticateGooglePlayAsync(CancellationToken token)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ShowLoading("LogIn...");
            SignInStatus status = await RequestGpgsSignInAsync(manual: false, token);

            if (status != SignInStatus.Success && !token.IsCancellationRequested)
            {
                // 자동 로그인 실패 -> 계정 선택 UI를 띄워 사용자가 직접 로그인하도록 한다
                LogStep($"GPGS 자동 로그인 실패({status}) -> 수동 로그인 시도");
                ShowLoading("Sign in to Google Play...");
                status = await RequestGpgsSignInAsync(manual: true, token);
            }

            if (status != SignInStatus.Success)
            {
                LogStep($"GPGS 로그인 최종 실패 -> {status}");
                return null;
            }

            string id = Social.localUser != null ? Social.localUser.id : null;
            if (string.IsNullOrEmpty(id) || id == "0")
            {
                // 인증은 성공했지만 id를 얻지 못한 경우 -> 잘못된 계정으로 진입하면 안 되므로 실패 처리
                LogStep("GPGS 로그인 성공했지만 사용자 id가 비어 있음");
                return null;
            }

            LogStep("GPGS 로그인 성공");
            return id;
#else
            await UniTask.CompletedTask;
            return null;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// GPGS 인증 콜백을 timeout과 함께 대기.
        /// 콜백이 끝내 호출되지 않는 단말이 있어 반드시 timeout이 필요하다.
        /// </summary>
        private async UniTask<SignInStatus> RequestGpgsSignInAsync(bool manual, CancellationToken token)
        {
            SignInStatus status = SignInStatus.InternalError;
            bool done = false;

            try
            {
                Action<SignInStatus> callback = result =>
                {
                    status = result;
                    done = true;
                };

                if (manual)
                    PlayGamesPlatform.Instance.ManuallyAuthenticate(callback);
                else
                    PlayGamesPlatform.Instance.Authenticate(callback);
            }
            catch (Exception e)
            {
                LogStep($"GPGS Authenticate 호출 예외 -> {e.Message}");
                return SignInStatus.InternalError;
            }

            if (!await WaitUntilAsync(() => done, GpgsTimeout, token))
            {
                LogStep($"GPGS {(manual ? "수동" : "자동")} 로그인 timeout({GpgsTimeout.TotalSeconds}s)");
                return SignInStatus.InternalError;
            }

            return status;
        }
#endif

        /// <summary>
        /// GPGS를 사용할 수 없는 환경의 최종 fallback.
        /// 기기 id -> Custom id 순으로 시도하며, 두 경우 모두 계정이 없으면 생성한다.
        /// </summary>
        private async UniTask<bool> LoginWithDeviceAsync(CancellationToken token)
        {
            ShowLoading("LogIn...");

#if UNITY_ANDROID && !UNITY_EDITOR
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            if (!string.IsNullOrEmpty(deviceId) && deviceId != SystemInfo.unsupportedIdentifier)
            {
                var device = await CallWithRetryAsync<LoginResult>(
                    (onOk, onError) => PlayFabClientAPI.LoginWithAndroidDeviceID(new LoginWithAndroidDeviceIDRequest
                    {
                        AndroidDeviceId = deviceId,
                        OS = SystemInfo.operatingSystem,
                        AndroidDevice = SystemInfo.deviceModel,
                        CreateAccount = true
                    }, onOk, onError),
                    "LoginWithAndroidDeviceID", token);

                if (device.IsSuccess)
                {
                    OnLoggedIn(device.Result.PlayFabId, device.Result.NewlyCreated, "AndroidDeviceID", LoginModeDevice);
                    return true;
                }
            }
#endif

            if (token.IsCancellationRequested)
                return false;

            string customId = GetOrCreateCustomId();
            var custom = await CallWithRetryAsync<LoginResult>(
                (onOk, onError) => PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest
                {
                    CustomId = customId,
                    CreateAccount = true
                }, onOk, onError),
                "LoginWithCustomID", token);

            if (custom.IsSuccess)
            {
                OnLoggedIn(custom.Result.PlayFabId, custom.Result.NewlyCreated, "CustomID", LoginModeDevice);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 단말에 고정된 Custom id를 반환 (없으면 생성 후 저장)
        /// </summary>
        private static string GetOrCreateCustomId()
        {
            string customId = PlayerPrefs.GetString(PrefsKeyCustomId, string.Empty);
            if (string.IsNullOrEmpty(customId))
            {
                customId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PrefsKeyCustomId, customId);
                PlayerPrefs.Save();
            }

            return customId;
        }

        #endregion

        #region PlayFab Login

        /// <summary>
        /// 기존 계정 로그인 -> 계정이 없을 때만 신규 등록.
        /// 일시적 오류(네트워크/서버)로 등록 흐름을 타지 않도록 오류 종류를 구분한다.
        /// </summary>
        private async UniTask<bool> LoginOrRegisterWithEmailAsync(string userId, CancellationToken token)
        {
            string email = $"{userId}{EmailDomain}";

            var login = await LoginWithEmailAsync(email, token);
            if (login.IsSuccess)
            {
                OnLoggedIn(login.Result.PlayFabId, isNewAccount: false, "EmailAddress", LoginModeGpgs);
                return true;
            }

            if (login.Error == null || !IsAccountMissing(login.Error))
            {
                // 계정이 없어서가 아니라 통신/서버 문제 -> 신규 등록하면 안 된다
                LogStep($"LoginWithEmailAddress 실패(등록 대상 아님) -> {Describe(login)}");
                return false;
            }

            LogStep("계정 없음 -> 신규 등록 시도");
            var register = await CallWithRetryAsync<RegisterPlayFabUserResult>(
                (onOk, onError) => PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
                {
                    Email = email,
                    Password = PlayFabPassword,
                    RequireBothUsernameAndEmail = false
                }, onOk, onError),
                "RegisterPlayFabUser", token);

            if (register.IsSuccess)
            {
                OnLoggedIn(register.Result.PlayFabId, isNewAccount: true, "Register", LoginModeGpgs);
                return true;
            }

            // 이미 존재하는 계정 -> 기존 로그인이 일시적으로 실패했던 것이므로 다시 로그인
            if (register.Error != null && register.Error.Error == PlayFabErrorCode.EmailAddressNotAvailable)
            {
                LogStep("이미 존재하는 계정 -> 로그인 재시도");
                var retry = await LoginWithEmailAsync(email, token);
                if (retry.IsSuccess)
                {
                    OnLoggedIn(retry.Result.PlayFabId, isNewAccount: false, "EmailAddress(retry)", LoginModeGpgs);
                    return true;
                }
            }

            LogStep($"RegisterPlayFabUser 실패 -> {Describe(register)}");
            return false;
        }

        private UniTask<ApiResult<LoginResult>> LoginWithEmailAsync(string email, CancellationToken token)
        {
            return CallWithRetryAsync<LoginResult>(
                (onOk, onError) => PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest
                {
                    Email = email,
                    Password = PlayFabPassword
                }, onOk, onError),
                "LoginWithEmailAddress", token);
        }

        private void OnLoggedIn(string playFabId, bool isNewAccount, string method, string loginMode = null)
        {
            AD.Managers.DataM.PlayFabId = playFabId;
            _isNewAccount = isNewAccount;

            // 다음 실행에서 같은 방식의 계정으로 접속하도록 기록
            if (!string.IsNullOrEmpty(loginMode))
            {
                PlayerPrefs.SetString(PrefsKeyLoginMode, loginMode);
                PlayerPrefs.Save();
            }

            LogStep($"PlayFab 로그인 성공 (method: {method}, newAccount: {isNewAccount})");
            ShowLoading("Success!!");
        }

        #region Test account (Editor only)

        private async UniTask<bool> LoginWithTestAccountAsync(CancellationToken token)
        {
            string email = $"{TestAccountId}{EmailDomain}";

            var login = await CallWithRetryAsync<LoginResult>(
                (onOk, onError) => PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest
                {
                    Email = email,
                    Password = "TestAccount"
                }, onOk, onError),
                "LoginWithEmailAddress(Test)", token);

            if (login.IsSuccess)
            {
                OnLoggedIn(login.Result.PlayFabId, isNewAccount: false, "TestAccount");
                return true;
            }

            var register = await CallWithRetryAsync<RegisterPlayFabUserResult>(
                (onOk, onError) => PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
                {
                    Email = email,
                    Password = "TestAccount",
                    RequireBothUsernameAndEmail = false
                }, onOk, onError),
                "RegisterPlayFabUser(Test)", token);

            if (register.IsSuccess)
            {
                OnLoggedIn(register.Result.PlayFabId, isNewAccount: true, "TestAccount(Register)");
                return true;
            }

            LogStep($"테스트 계정 로그인 실패 -> {Describe(register)}");
            return false;
        }

        #endregion

        #endregion

        #region Profile

        /// <summary>
        /// 닉네임 등록 여부를 판단.
        /// 프로필 조회에 실패하더라도 로그인 자체는 끝났으므로 진입을 막지 않는다.
        /// </summary>
        private async UniTask ResolveProfileAsync(CancellationToken token)
        {
            ShowLoading("Check Data...");

            var profile = await CallWithRetryAsync<GetPlayerProfileResult>(
                (onOk, onError) => PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest
                {
                    PlayFabId = AD.Managers.DataM.PlayFabId,
                    ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
                }, onOk, onError),
                "GetPlayerProfile", token);

            if (token.IsCancellationRequested)
                return;

            bool needsNickname;
            if (profile.IsSuccess)
            {
                needsNickname = profile.Result.PlayerProfile == null
                    || string.IsNullOrEmpty(profile.Result.PlayerProfile.DisplayName);
            }
            else
            {
                // 조회 실패 -> 방금 만든 계정이면 닉네임이 없고, 기존 계정이면 있다고 본다
                LogStep($"GetPlayerProfile 실패 -> 신규 계정 여부({_isNewAccount})로 판단");
                needsNickname = _isNewAccount;
            }

            if (needsNickname)
            {
                ShowNicknamePanel();
                return;
            }

            GoNext();
        }

        #endregion

        #region Nickname management

        public void CheckNickName()
        {
            string nickname = _nicknameInput != null ? _nicknameInput.text : string.Empty;
            nickname = nickname.Trim();

            if (string.IsNullOrEmpty(nickname) || nickname.Contains(" ") || nickname.Length < 3 || nickname.Length > 20)
            {
                _nicknameRulePanel.SetActive(true);
                return;
            }

            UpdateDisplayNameAsync(nickname, _cts != null ? _cts.Token : CancellationToken.None).Forget();
        }

        private async UniTask UpdateDisplayNameAsync(string name, CancellationToken token)
        {
            var update = await CallWithRetryAsync<UpdateUserTitleDisplayNameResult>(
                (onOk, onError) => PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest
                {
                    DisplayName = name
                }, onOk, onError),
                "UpdateUserTitleDisplayName", token);

            if (token.IsCancellationRequested)
                return;

            if (!update.IsSuccess)
            {
                // 중복/규칙 위반이 아닌 통신 오류까지 "중복"으로 안내하지 않는다
                LogStep($"UpdateUserTitleDisplayName 실패 -> {Describe(update)}");

                if (update.Error != null && IsTransient(update.Error))
                    ShowRetry("Network error. Please try again.");
                else
                    _nicknameConflictPanel.SetActive(true);

                return;
            }

            _nicknamePanel.SetActive(false);
            _nicknameRulePanel.SetActive(false);
            _nicknameConflictPanel.SetActive(false);

            ShowLoading("Save NickName...");
            AD.Managers.ServerM.SetData(new Dictionary<string, string> { { "NickName", name } }, false, false);

            // 저장이 끝나기를 기다리되, 끝나지 않아도 진입은 막지 않는다
            if (!await WaitUntilAsync(() => !AD.Managers.ServerM.IsInProgress, ServerSyncTimeout, token))
            {
                if (token.IsCancellationRequested)
                    return;

                LogStep("닉네임 저장 timeout -> 그대로 진행");
                AD.Managers.ServerM.SetInProgress(false);
            }

            GoNext();
        }

        #endregion

        #region Scene transition

        private void GoNext()
        {
            ShowLoading("Check Data...");
            AD.Managers.DataM.UpdatePlayerData();

            InitPlayerDataAsync(_cts != null ? _cts.Token : CancellationToken.None).Forget();
        }

        private async UniTask InitPlayerDataAsync(CancellationToken token)
        {
            if (!await WaitUntilAsync(() => !AD.Managers.ServerM.IsInProgress, ServerSyncTimeout, token))
            {
                if (token.IsCancellationRequested)
                    return;

                // 서버 동기화가 끝나지 않아도 로컬 데이터로 진입시킨다 (무한 로딩 방지)
                LogStep("서버 데이터 동기화 timeout -> 로컬 데이터로 진입");
                AD.Managers.ServerM.SetInProgress(false);
            }

            if (token.IsCancellationRequested)
                return;

            string sex = "null";
            var localData = AD.Managers.DataM.LocalPlayerData;
            if (localData != null && localData.TryGetValue("Sex", out string value))
                sex = value;

            LogStep($"Scene 이동 (Sex: {sex})");
            AD.Managers.SceneM.NextScene(sex != "null"
                ? AD.GameConstants.Scene.Main
                : AD.GameConstants.Scene.SetCharacter);
        }

        #endregion

        #region PlayFab call helper

        /// <summary>
        /// PlayFab 호출 결과 (timeout 포함)
        /// </summary>
        private struct ApiResult<T> where T : class
        {
            public T Result;
            public PlayFabError Error;
            public bool IsTimeout;

            public bool IsSuccess => Result != null && Error == null && !IsTimeout;
        }

        /// <summary>
        /// 일시적 오류일 때 backoff를 두고 재시도한다.
        /// 영구적 오류(계정 없음 등)는 즉시 반환하여 호출 측이 분기하도록 한다.
        /// </summary>
        private async UniTask<ApiResult<T>> CallWithRetryAsync<T>(
            Action<Action<T>, Action<PlayFabError>> invoke, string label, CancellationToken token) where T : class
        {
            ApiResult<T> result = default;

            for (int attempt = 0; attempt < MaxApiAttempts; attempt++)
            {
                if (token.IsCancellationRequested)
                    return result;

                result = await CallAsync(invoke, label, token);

                if (result.IsSuccess)
                    return result;

                bool retryable = result.IsTimeout || (result.Error != null && IsTransient(result.Error));
                if (!retryable)
                    return result;

                if (attempt == MaxApiAttempts - 1)
                    break;

                float delay = Mathf.Pow(2f, attempt); // 1s, 2s
                LogStep($"{label} 일시적 실패 -> {delay}s 후 재시도 ({attempt + 1}/{MaxApiAttempts})");

                if (!await DelayAsync(TimeSpan.FromSeconds(delay), token))
                    return result;
            }

            LogStep($"{label} 최종 실패 -> {Describe(result)}");
            return result;
        }

        private async UniTask<ApiResult<T>> CallAsync<T>(
            Action<Action<T>, Action<PlayFabError>> invoke, string label, CancellationToken token) where T : class
        {
            T apiResult = null;
            PlayFabError apiError = null;
            bool done = false;

            try
            {
                invoke(
                    value =>
                    {
                        apiResult = value;
                        done = true;
                    },
                    error =>
                    {
                        apiError = error;
                        done = true;
                    });
            }
            catch (Exception e)
            {
                LogStep($"{label} 호출 예외 -> {e.Message}");
                return new ApiResult<T> { IsTimeout = true };
            }

            if (!await WaitUntilAsync(() => done, ApiTimeout, token))
            {
                if (!token.IsCancellationRequested)
                    LogStep($"{label} timeout({ApiTimeout.TotalSeconds}s)");

                return new ApiResult<T> { IsTimeout = true };
            }

            return new ApiResult<T> { Result = apiResult, Error = apiError };
        }

        /// <summary>
        /// 네트워크/서버 문제처럼 재시도로 해결될 수 있는 오류인지 판단
        /// </summary>
        private static bool IsTransient(PlayFabError error)
        {
            switch (error.Error)
            {
                case PlayFabErrorCode.ConnectionError:
                case PlayFabErrorCode.InternalServerError:
                case PlayFabErrorCode.ServiceUnavailable:
                case PlayFabErrorCode.DownstreamServiceUnavailable:
                    return true;
            }

            // 5xx, 429(rate limit), 408(timeout), 0(응답 없음)
            return error.HttpCode >= 500 || error.HttpCode == 429 || error.HttpCode == 408 || error.HttpCode == 0;
        }

        /// <summary>
        /// 계정이 존재하지 않아서 실패한 것인지 판단 (신규 등록 여부 결정)
        /// </summary>
        private static bool IsAccountMissing(PlayFabError error)
        {
            switch (error.Error)
            {
                case PlayFabErrorCode.AccountNotFound:
                case PlayFabErrorCode.InvalidEmailOrPassword:
                case PlayFabErrorCode.InvalidEmailAddress:
                case PlayFabErrorCode.InvalidParams:
                    return true;
                default:
                    return false;
            }
        }

        private static string Describe<T>(ApiResult<T> result) where T : class
        {
            if (result.IsTimeout)
                return "timeout";

            if (result.Error != null)
                return $"{result.Error.Error}({result.Error.HttpCode}) {result.Error.ErrorMessage}";

            return "unknown";
        }

        #endregion

        #region Utility

        /// <summary>
        /// predicate가 참이 되거나 timeout이 지날 때까지 대기.
        /// 취소/timeout 시 false를 반환하며 예외를 던지지 않는다.
        /// </summary>
        private static async UniTask<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, CancellationToken token)
        {
            float deadline = Time.realtimeSinceStartup + (float)timeout.TotalSeconds;

            while (!predicate())
            {
                if (token.IsCancellationRequested || Time.realtimeSinceStartup >= deadline)
                    return false;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            return true;
        }

        /// <summary>
        /// 취소되면 false를 반환하는 대기 (예외 없음)
        /// </summary>
        private static async UniTask<bool> DelayAsync(TimeSpan duration, CancellationToken token)
        {
            float deadline = Time.realtimeSinceStartup + (float)duration.TotalSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (token.IsCancellationRequested)
                    return false;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            return !token.IsCancellationRequested;
        }

        /// <summary>
        /// release 빌드에서도 adb logcat으로 확인할 수 있는 로그.
        /// AD.DebugLogger는 Conditional("Debug")라 release 빌드에서 제거되므로 별도로 사용한다.
        /// </summary>
        private static void LogStep(string message) => Debug.Log($"{LogTag} {message}");

        #endregion

        #region UI

        private void ShowLoading(string message)
        {
            if (_retry != null) _retry.SetActive(false);
            if (_loading != null) _loading.SetActive(true);
            if (_loadingText != null) _loadingText.text = message;
        }

        private void ShowRetry(string message)
        {
            LogStep($"재시도 패널 노출 -> {message}");

            if (_loading != null) _loading.SetActive(false);
            if (_nicknamePanel != null) _nicknamePanel.SetActive(false);
            if (_retry != null) _retry.SetActive(true);
            if (_retryText != null) _retryText.text = message;
        }

        private void ShowNicknamePanel()
        {
            if (_loadingText != null) _loadingText.text = "Set NickName...";
            if (_nicknamePanel != null) _nicknamePanel.SetActive(true);
        }

        public void ClickedOK() => AD.Managers.SoundM.UI_Ok();

        #endregion
    }
}
