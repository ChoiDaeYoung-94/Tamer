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
    /// 저장된 계정 방식과 식별자를 유지한다. 계정 선택이 모호하거나 서버 동기화가
    /// 실패하면 다른 계정 또는 로컬 진행도로 진입하지 않고 재시도를 노출한다.
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
        private const string LoginModeAndroid = "android";
        private const string LoginModeCustom = "custom";

        private static readonly TimeSpan GpgsTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan ServerSyncTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan NetworkWaitTimeout = TimeSpan.FromSeconds(5);

        private const int MaxApiAttempts = 3;

        #endregion

        private CancellationTokenSource _cts;
        private DataManager _dataOwner;
        private readonly LoginOperationGate _operations = new LoginOperationGate();
        private string _selectedGpgsId;

        #region Unity Lifecycle

        private void Awake()
        {
#if TAMER_GAMEPLAY_HARNESS
            return;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                PlayGamesPlatform.DebugLogEnabled = false;
                PlayGamesPlatform.Activate();
            }
            catch (Exception e)
            {
                // Activate 실패해도 아래 fallback으로 진입 가능해야 한다
                LogStep($"PlayGamesPlatform.Activate 실패 -> {e.GetType().Name}");
            }
#endif
        }

        private void Start()
        {
#if TAMER_GAMEPLAY_HARNESS
            RevivalGameplayIsolation.BlockLogin();
            return;
#endif
            _dataOwner = AD.Managers.DataM;
            _cts = new CancellationTokenSource();
            StartLogin();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            if (_dataOwner != null && !_operations.HasEnteredScene)
                _dataOwner.SuspendAccountSession();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region Login Flow

        private void StartLogin()
        {
            if (_cts == null || _cts.IsCancellationRequested || !_operations.TryBeginLogin())
            {
                LogStep("이미 로그인 진행 중 -> 중복 요청 무시");
                return;
            }

            AD.Managers.DataM.SuspendAccountSession();
            RunLoginAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// 재시도 버튼에서 호출 (prefab 연결 유지)
        /// </summary>
        public void RetryConnection()
        {
            if (_operations.IsRunning)
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

                // A successful initial read is required before any nickname/progress write.
                AD.Managers.DataM.UpdatePlayerData();
                if (!await WaitForServerAsync(token))
                    return;

                await ResolveProfileAsync(token);
            }
            catch (OperationCanceledException)
            {
                // scene 이동 / 오브젝트 파괴 -> 정상 종료
            }
            catch (Exception e)
            {
                // 예외로 인해 로딩 화면에 갇히는 상황을 막는다
                LogStep($"로그인 처리 중 예외 -> {e.GetType().Name}");
                ShowRetry("Sign-in failed. Please try again.");
            }
            finally
            {
                _operations.EndOperation();
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

        /// <summary>Keep the selected identity; failures never select a different account.</summary>
        private async UniTask<bool> LoginOnAndroidAsync(CancellationToken token)
        {
            string cachedGpgsId = PlayerPrefs.GetString(PrefsKeyGpgsId, string.Empty);
            string mode = PlayerPrefs.GetString(PrefsKeyLoginMode, string.Empty);
            if (mode == "device" && !string.IsNullOrEmpty(cachedGpgsId))
                return false; // Conflicting legacy selection has no reliable winner.
            if (LoginContinuityPolicy.IsDeviceMode(mode))
                return await LoginWithDeviceAsync(token);
            if (!string.IsNullOrEmpty(mode) && mode != LoginModeGpgs && mode != "gpgs-pending")
                return false;

            string authenticatedId = await AuthenticateGooglePlayAsync(token);
            if (token.IsCancellationRequested)
                return false;

            string selectedId = LoginContinuityPolicy.SelectGoogleId(cachedGpgsId, authenticatedId);
            if (!string.IsNullOrEmpty(cachedGpgsId) && !string.IsNullOrEmpty(authenticatedId)
                && selectedId == null)
            {
                LogStep("Google Play account differs from the saved account; retry with the original account.");
                return false;
            }

            if (!string.IsNullOrEmpty(selectedId))
            {
                bool allowCreate = LoginContinuityPolicy.CanCreateGoogleAccount(mode, HasLocalProgress(),
                    !string.IsNullOrEmpty(cachedGpgsId));
                // Pin a first selection before network I/O; a timeout must retry this same identity.
                _selectedGpgsId = selectedId;
                if (string.IsNullOrEmpty(cachedGpgsId) && allowCreate)
                {
                    PlayerPrefs.SetString(PrefsKeyGpgsId, selectedId);
                    PlayerPrefs.SetString(PrefsKeyLoginMode, "gpgs-pending");
                    PlayerPrefs.Save();
                }
                return await LoginOrRegisterWithEmailAsync(selectedId, allowCreate, token);
            }

            if (!string.IsNullOrEmpty(mode) || HasLocalProgress())
                return false;

            return await LoginWithDeviceAsync(token);
        }

        /// <summary>
        /// 캐릭터 생성 전 계정/닉네임/구매 기록도 신규 계정 생성 방지 근거로 사용한다.
        /// </summary>
        private static bool HasLocalProgress()
        {
            return AD.Managers.DataM.HasKnownAccount
                || LoginContinuityPolicy.HasLocalProgress(AD.Managers.DataM.LocalPlayerData);
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
            if (token.IsCancellationRequested) return SignInStatus.InternalError;
            SignInStatus status = SignInStatus.InternalError;
            var pending = new LoginCallbackGate();
            try
            {
                Action<SignInStatus> callback = result =>
                {
                    if (!pending.TryComplete(token.IsCancellationRequested)) return;
                    status = result;
                };
                if (manual) PlayGamesPlatform.Instance.ManuallyAuthenticate(callback);
                else PlayGamesPlatform.Instance.Authenticate(callback);
                if (!await WaitUntilAsync(() => pending.IsCompleted, GpgsTimeout, token))
                    return SignInStatus.InternalError;
                return status;
            }
            catch (Exception e)
            {
                LogStep($"GPGS request exception: {e.GetType().Name}");
                return SignInStatus.InternalError;
            }
            finally { pending.Expire(); }
        }
#endif

        /// <summary>Choose one device identity. Never fall through after a request fails.</summary>
        private async UniTask<bool> LoginWithDeviceAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return false;
            ShowLoading("LogIn...");
            string mode = PlayerPrefs.GetString(PrefsKeyLoginMode, string.Empty);
            string customId = PlayerPrefs.GetString(PrefsKeyCustomId, string.Empty);
            string deviceId = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            deviceId = SystemInfo.deviceUniqueIdentifier;
            if (deviceId == SystemInfo.unsupportedIdentifier) deviceId = null;
#endif
            string selectedMode = LoginContinuityPolicy.SelectDeviceMode(mode,
                !string.IsNullOrEmpty(customId), !string.IsNullOrEmpty(deviceId));
            bool allowCreate = LoginContinuityPolicy.CanCreateAccount(mode, HasLocalProgress());
            if (selectedMode == null || (string.IsNullOrEmpty(mode) && !allowCreate))
                return false;

            if (string.IsNullOrEmpty(mode))
            {
                if (selectedMode == LoginModeCustom) customId = GetOrCreateCustomId();
                PlayerPrefs.SetString(PrefsKeyLoginMode, selectedMode + "-pending");
                PlayerPrefs.Save();
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (selectedMode == LoginModeAndroid)
            {
                var device = await CallWithRetryAsync<LoginResult>(
                    (onOk, onError) => PlayFabClientAPI.LoginWithAndroidDeviceID(new LoginWithAndroidDeviceIDRequest
                    {
                        AuthenticationContext = new PlayFabAuthenticationContext(),
                        AndroidDeviceId = deviceId,
                        OS = SystemInfo.operatingSystem,
                        AndroidDevice = SystemInfo.deviceModel,
                        CreateAccount = allowCreate
                    }, onOk, onError), "LoginWithAndroidDeviceID", token);
                if (!device.IsSuccess || token.IsCancellationRequested) return false;
                OnLoggedIn(device.Result.PlayFabId, device.Result.NewlyCreated,
                    "AndroidDeviceID", device.Result.AuthenticationContext, LoginModeAndroid);
                return true;
            }
#endif
            if (selectedMode != LoginModeCustom || string.IsNullOrEmpty(customId)) return false;
            var custom = await CallWithRetryAsync<LoginResult>(
                (onOk, onError) => PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest
                {
                    AuthenticationContext = new PlayFabAuthenticationContext(),
                    CustomId = customId,
                    CreateAccount = allowCreate
                }, onOk, onError), "LoginWithCustomID", token);
            if (!custom.IsSuccess || token.IsCancellationRequested) return false;
            OnLoggedIn(custom.Result.PlayFabId, custom.Result.NewlyCreated,
                "CustomID", custom.Result.AuthenticationContext, LoginModeCustom);
            return true;
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
        /// 기존 계정 로그인 후, 신규 생성이 허용된 첫 식별자만 같은 이메일로 등록을 시도한다.
        /// 이메일 로그인은 미등록 이메일과 잘못된 암호를 별도 오류로 구분하지 않는다.
        /// </summary>
        private async UniTask<bool> LoginOrRegisterWithEmailAsync(string userId, bool allowCreate, CancellationToken token)
        {
            string email = $"{userId}{EmailDomain}";

            var login = await LoginWithEmailAsync(email, token);
            if (login.IsSuccess && !token.IsCancellationRequested)
            {
                OnLoggedIn(login.Result.PlayFabId, false, "EmailAddress", login.Result.AuthenticationContext, LoginModeGpgs);
                return true;
            }

            if (token.IsCancellationRequested || !IsRegistrationCandidate(login.Error, allowCreate))
            {
                // Existing identity, transient failure or invalid input cannot create an account.
                LogStep($"LoginWithEmailAddress 실패(등록 대상 아님) -> {Describe(login)}");
                return false;
            }

            LogStep("첫 계정 선택 -> 동일 이메일 등록 시도");
            var register = await CallWithRetryAsync<RegisterPlayFabUserResult>(
                (onOk, onError) => PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
                {
                    AuthenticationContext = new PlayFabAuthenticationContext(),
                    Email = email,
                    Password = PlayFabPassword,
                    RequireBothUsernameAndEmail = false
                }, onOk, onError),
                "RegisterPlayFabUser", token);

            if (register.IsSuccess && !token.IsCancellationRequested)
            {
                OnLoggedIn(register.Result.PlayFabId, true, "Register", register.Result.AuthenticationContext, LoginModeGpgs);
                return true;
            }

            // 이미 존재하는 계정 -> 기존 로그인이 일시적으로 실패했던 것이므로 다시 로그인
            if (register.Error != null && register.Error.Error == PlayFabErrorCode.EmailAddressNotAvailable)
            {
                LogStep("이미 존재하는 계정 -> 로그인 재시도");
                var retry = await LoginWithEmailAsync(email, token);
                if (retry.IsSuccess && !token.IsCancellationRequested)
                {
                    OnLoggedIn(retry.Result.PlayFabId, false, "EmailAddress(retry)", retry.Result.AuthenticationContext, LoginModeGpgs);
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
                    AuthenticationContext = new PlayFabAuthenticationContext(),
                    Email = email,
                    Password = PlayFabPassword
                }, onOk, onError),
                "LoginWithEmailAddress", token);
        }

        private void OnLoggedIn(string playFabId, bool isNewAccount, string method, PlayFabAuthenticationContext context, string loginMode = null)
        {
            AD.Managers.DataM.BeginAccountSession(playFabId);
            PlayFabSettings.staticPlayer.CopyFrom(context);

            // 다음 실행에서 같은 방식의 계정으로 접속하도록 기록
            if (!string.IsNullOrEmpty(loginMode))
            {
                if (loginMode == LoginModeGpgs) PlayerPrefs.SetString(PrefsKeyGpgsId, _selectedGpgsId);
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
                    AuthenticationContext = new PlayFabAuthenticationContext(),
                    Email = email,
                    Password = "TestAccount"
                }, onOk, onError),
                "LoginWithEmailAddress(Test)", token);

            if (login.IsSuccess && !token.IsCancellationRequested)
            {
                OnLoggedIn(login.Result.PlayFabId, false, "TestAccount", login.Result.AuthenticationContext);
                return true;
            }

            if (token.IsCancellationRequested || !IsRegistrationCandidate(login.Error, !HasLocalProgress()))
                return false;

            var register = await CallWithRetryAsync<RegisterPlayFabUserResult>(
                (onOk, onError) => PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
                {
                    AuthenticationContext = new PlayFabAuthenticationContext(),
                    Email = email,
                    Password = "TestAccount",
                    RequireBothUsernameAndEmail = false
                }, onOk, onError),
                "RegisterPlayFabUser(Test)", token);

            if (register.IsSuccess && !token.IsCancellationRequested)
            {
                OnLoggedIn(register.Result.PlayFabId, true, "TestAccount(Register)", register.Result.AuthenticationContext);
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
        /// 프로필 조회 실패는 재시도하며 닉네임 상태를 추측하지 않는다.
        /// </summary>
        private async UniTask ResolveProfileAsync(CancellationToken token)
        {
            ShowLoading("Check Data...");

            var profile = await CallWithRetryAsync<GetPlayerProfileResult>(
                (onOk, onError) => PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest
                {
                    AuthenticationContext = CopySessionContext(),
                    PlayFabId = AD.Managers.DataM.PlayFabId,
                    ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
                }, onOk, onError),
                "GetPlayerProfile", token);

            if (token.IsCancellationRequested)
                return;

            if (!profile.IsSuccess)
            {
                ShowRetry("Could not load your profile. Please try again.");
                return;
            }

            bool needsNickname = profile.Result.PlayerProfile == null
                || string.IsNullOrEmpty(profile.Result.PlayerProfile.DisplayName);
            if (needsNickname)
            {
                _operations.AwaitNickname();
                ShowNicknamePanel();
                return;
            }

            await GoNextAsync(token);
        }

        #endregion

        #region Nickname management

        public void CheckNickName()
        {
            if (_cts == null || _cts.IsCancellationRequested || !_operations.TryBeginNickname())
                return;

            string nickname = _nicknameInput != null ? _nicknameInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(nickname) || nickname.Contains(" ") || nickname.Length < 3 || nickname.Length > 20)
            {
                if (_nicknameRulePanel != null) _nicknameRulePanel.SetActive(true);
                _operations.EndOperation();
                return;
            }

            UpdateDisplayNameAsync(nickname, _cts.Token).Forget();
        }

        private async UniTask UpdateDisplayNameAsync(string name, CancellationToken token)
        {
            try
            {
                var update = await CallWithRetryAsync<UpdateUserTitleDisplayNameResult>(
                    (onOk, onError) => PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest
                    {
                        AuthenticationContext = CopySessionContext(),
                        DisplayName = name
                    }, onOk, onError), "UpdateUserTitleDisplayName", token);

                if (token.IsCancellationRequested) return;
                if (!update.IsSuccess)
                {
                    if (update.IsTimeout || (update.Error != null && IsTransient(update.Error)))
                        ShowRetry("Network error. Please try again.");
                    else if (_nicknameConflictPanel != null)
                        _nicknameConflictPanel.SetActive(true);
                    return;
                }

                if (_nicknamePanel != null) _nicknamePanel.SetActive(false);
                if (_nicknameRulePanel != null) _nicknameRulePanel.SetActive(false);
                if (_nicknameConflictPanel != null) _nicknameConflictPanel.SetActive(false);
                ShowLoading("Save NickName...");
                AD.Managers.ServerM.SetData(new Dictionary<string, string> { { "NickName", name } }, false, false);
                if (!await WaitForServerAsync(token)) return;
                await GoNextAsync(token);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!token.IsCancellationRequested) ShowRetry("Could not save your profile. Please try again.");
            }
            finally { _operations.EndOperation(); }
        }

        #endregion

        #region Scene transition

        private async UniTask GoNextAsync(CancellationToken token)
        {
            ShowLoading("Check Data...");
            AD.Managers.DataM.UpdatePlayerData();
            if (!await WaitForServerAsync(token) || !_operations.TryEnterScene()) return;

            string sex = "null";
            var localData = AD.Managers.DataM.LocalPlayerData;
            if (localData != null && localData.TryGetValue("Sex", out string value)) sex = value;
            AD.Managers.SceneM.NextScene(!string.IsNullOrEmpty(sex) && sex != "null"
                ? AD.GameConstants.Scene.Main
                : AD.GameConstants.Scene.SetCharacter);
        }

        private async UniTask<bool> WaitForServerAsync(CancellationToken token)
        {
            var server = AD.Managers.ServerM;
            if (server == null || token.IsCancellationRequested) return false;
            bool completed = await WaitUntilAsync(() => !server.IsInProgress, ServerSyncTimeout, token);
            if (!completed || token.IsCancellationRequested)
                server.CancelPendingRequests();
            if (token.IsCancellationRequested) return false;
            if (!completed || server.HasFailed)
            {
                ShowRetry("Could not load your saved progress. Please try again.");
                return false;
            }
            return true;
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
            if (token.IsCancellationRequested) return new ApiResult<T> { IsTimeout = true };
            T apiResult = null;
            PlayFabError apiError = null;
            var callback = new LoginCallbackGate();
            try
            {
                invoke(value =>
                    {
                        if (!callback.TryComplete(token.IsCancellationRequested)) return;
                        apiResult = value;
                    }, error =>
                    {
                        if (!callback.TryComplete(token.IsCancellationRequested)) return;
                        apiError = error;
                    });
                if (!await WaitUntilAsync(() => callback.IsCompleted, ApiTimeout, token))
                    return new ApiResult<T> { IsTimeout = true };
                return new ApiResult<T> { Result = apiResult, Error = apiError };
            }
            catch (Exception e)
            {
                LogStep($"{label} request exception: {e.GetType().Name}");
                return new ApiResult<T> { IsTimeout = true };
            }
            finally { callback.Expire(); }
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
        /// Fresh pinned identities may try registration with the same generated email.
        /// InvalidEmailOrPassword is not proof that an account is missing; an existing
        /// email cannot be recreated and the caller retries login on EmailAddressNotAvailable.
        /// </summary>
        private static bool IsRegistrationCandidate(PlayFabError error, bool allowCreate)
        {
            if (!allowCreate || error == null) return false;
            switch (error.Error)
            {
                case PlayFabErrorCode.AccountNotFound:
                case PlayFabErrorCode.InvalidEmailOrPassword:
                    return true;
                default:
                    return false;
            }
        }

        private static PlayFabAuthenticationContext CopySessionContext()
        {
            var context = new PlayFabAuthenticationContext();
            context.CopyFrom(PlayFabSettings.staticPlayer);
            return context;
        }

        private static string Describe<T>(ApiResult<T> result) where T : class
        {
            if (result.IsTimeout)
                return "timeout";

            if (result.Error != null)
                return $"{result.Error.Error}({result.Error.HttpCode})";

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

            while (!token.IsCancellationRequested)
            {
                if (predicate()) return true;
                if (Time.realtimeSinceStartup >= deadline) return false;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            return false;
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
            AD.Managers.DataM.SuspendAccountSession();
            _operations.ResetForRetry();
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

    internal static class LoginContinuityPolicy
    {
        public static bool HasLocalProgress(Dictionary<string, string> local)
        {
            // A failed/uninitialized local load is unknown history, never permission to create an account.
            if (local == null) return true;
            foreach (var key in new[] { "Sex", "NickName", "Tutorial", "AllyMonsters", "GooglePlay" })
                if (local.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) && value != "null")
                    return true;
            return local.TryGetValue("Gold", out var gold) && !string.IsNullOrEmpty(gold)
                && gold != "0" && gold != "null";
        }

        public static bool IsDeviceMode(string mode) => mode == "device" || mode == "android"
            || mode == "custom" || mode == "android-pending" || mode == "custom-pending";

        public static string SelectGoogleId(string cachedId, string authenticatedId)
        {
            if (!string.IsNullOrEmpty(cachedId) && !string.IsNullOrEmpty(authenticatedId)
                && !string.Equals(cachedId, authenticatedId, StringComparison.Ordinal)) return null;
            return string.IsNullOrEmpty(cachedId) ? authenticatedId : cachedId;
        }

        public static bool CanCreateAccount(string mode, bool hasLocalProgress) => !hasLocalProgress
            && (string.IsNullOrEmpty(mode) || mode == "gpgs-pending"
                || mode == "android-pending" || mode == "custom-pending");

        public static bool CanCreateGoogleAccount(string mode, bool hasLocalProgress, bool hasCachedId)
            => !hasLocalProgress && (string.IsNullOrEmpty(mode) || mode == "gpgs-pending")
                && (!hasCachedId || mode == "gpgs-pending");

        public static string SelectDeviceMode(string mode, bool hasCustomId, bool hasAndroidId)
        {
            if (mode == "custom" || mode == "custom-pending") return hasCustomId ? "custom" : null;
            if (mode == "android" || mode == "android-pending") return hasAndroidId ? "android" : null;
            // Old versions recorded both implementations as "device". If a CustomID also
            // exists, it is impossible to prove which login last succeeded; do not guess.
            if (mode == "device") return !hasCustomId && hasAndroidId ? "android" : null;
            if (!string.IsNullOrEmpty(mode)) return null;
            return hasCustomId || !hasAndroidId ? "custom" : "android";
        }
    }

    internal sealed class LoginOperationGate
    {
        public bool IsRunning { get; private set; }
        public bool HasEnteredScene { get; private set; }
        private bool _awaitingNickname;

        public bool TryBeginLogin()
        {
            if (IsRunning || HasEnteredScene || _awaitingNickname) return false;
            IsRunning = true;
            return true;
        }

        public bool TryBeginNickname()
        {
            if (IsRunning || HasEnteredScene || !_awaitingNickname) return false;
            IsRunning = true;
            return true;
        }

        public void AwaitNickname() => _awaitingNickname = true;
        public void EndOperation() => IsRunning = false;
        public void ResetForRetry()
        {
            _awaitingNickname = false;
            HasEnteredScene = false;
        }

        public bool TryEnterScene()
        {
            if (!IsRunning || HasEnteredScene) return false;
            HasEnteredScene = true;
            return true;
        }
    }

    internal sealed class LoginCallbackGate
    {
        private bool _active = true;
        public bool IsCompleted { get; private set; }
        public bool TryComplete(bool cancelled)
        {
            if (!_active || IsCompleted || cancelled) return false;
            IsCompleted = true;
            return true;
        }
        public void Expire() => _active = false;
    }

}
