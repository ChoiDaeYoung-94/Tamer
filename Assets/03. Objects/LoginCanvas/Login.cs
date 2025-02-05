using System;
using System.Threading;
using System.Collections.Generic;

using UnityEngine;

using PlayFab;
using PlayFab.ClientModels;

using Cysharp.Threading.Tasks;
using UniRx;

#if UNITY_ANDROID
using GooglePlayGames;
#endif

namespace AD
{
    /// <summary>
    /// 로그인 관리 클래스 (PlayFab, Google Play)
    /// </summary>
    public class Login : MonoBehaviour
    {
        [Header("--- UI Elements ---")]
        [SerializeField] private GameObject _loading;
        [SerializeField] private TMPro.TMP_Text _loadingText;
        [SerializeField] private GameObject _retry;
        [SerializeField] private GameObject _nicknamePanel;
        [SerializeField] private TMPro.TMP_Text _nicknameInput;
        [SerializeField] private GameObject _nicknameRulePanel;
        [SerializeField] private GameObject _nicknameConflictPanel;

        private IDisposable _nicknameCheckSubscription;

        private void Awake()
        {
#if UNITY_ANDROID
            PlayGamesPlatform.DebugLogEnabled = true;
            PlayGamesPlatform.Activate();
#endif
        }

        private void Start()
        {
            _loadingText.text = "LogIn...";

            TryStartLogin();
        }

        #region Connection Check

        private void TryStartLogin()
        {
            if (!IsInternetAvailable())
            {
                ShowRetryPanel();
                return;
            }

            StartLogin();
        }

        private bool IsInternetAvailable() => Application.internetReachability != NetworkReachability.NotReachable;

        private void ShowRetryPanel()
        {
            _loading.SetActive(false);
            _retry.SetActive(true);
        }

        public void RetryConnection()
        {
            _retry.SetActive(false);
            _loading.SetActive(true);

            TryStartLogin();
        }

        #endregion

        #region Login Process

        private void StartLogin()
        {
            _retry.SetActive(false);
            _loading.SetActive(true);

#if UNITY_EDITOR
            LoginWithTestAccount();
#elif UNITY_ANDROID
            LoginWithGoogle();
#endif
        }

        #region Login with test account

        private void LoginWithTestAccount()
        {
            var request = new LoginWithEmailAddressRequest { Email = "testAccount@AeDeong.com", Password = "TestAccount" };
            PlayFabClientAPI.LoginWithEmailAddress(request,
                (success) =>
                {
                    AD.Managers.DataM.StrID = success.PlayFabId;
                    GoNext();
                },
                (failed) => SignUpWithTestAccount());
        }

        private void SignUpWithTestAccount()
        {
            var request = new RegisterPlayFabUserRequest { Email = "testAccount@AeDeong.com", Password = "TestAccount", RequireBothUsernameAndEmail = false };
            PlayFabClientAPI.RegisterPlayFabUser(request,
                (success) =>
                {
                    AD.Managers.DataM.StrID = success.PlayFabId;
                    UpdateDisplayName("testAccount");
                },
                (failed) => AD.DebugLogger.Log("Login", "Failed SignUpWithTestAccount  " + failed.ErrorMessage));
        }

        #endregion

        #region Login with google, playFab

        private void LoginWithGoogle()
        {
            if (!Social.localUser.authenticated)
            {
                Social.localUser.Authenticate((success, error) =>
                {
                    if (success)
                    {
                        AD.DebugLogger.Log("Login", "Success LoginWithGoogle");
                        LoginWithPlayFab();
                    }
                    else
                    {
                        AD.DebugLogger.LogWarning("Login", $"Failed LoginWithGoogle -> {error}");
                        _loadingText.text = $"Failed LoginWithGoogle... \n{error}";
                    }
                });
            }
        }

        private void LoginWithPlayFab()
        {
            string id = $"{Social.localUser.id}@AeDeong.com";
            var request = new LoginWithEmailAddressRequest { Email = id, Password = "AeDeong" };

            PlayFabClientAPI.LoginWithEmailAddress(request,
                result =>
                {
                    AD.DebugLogger.Log("Login", "Success LoginWithPlayFab");
                    _loadingText.text = "Success!!";

                    AD.Managers.DataM.StrID = result.PlayFabId;

                    GetPlayerProfileAsync(AD.Managers.DataM.StrID).Forget();
                },
                error =>
                {
                    AD.DebugLogger.LogWarning("Login", $"Failed LoginWithPlayFab -> {error}");
                    SignUpWithPlayFab();
                });
        }

        private void SignUpWithPlayFab()
        {
            var request = new RegisterPlayFabUserRequest { Email = $"{Social.localUser.id}@AeDeong.com", Password = "AeDeong", RequireBothUsernameAndEmail = false };
            PlayFabClientAPI.RegisterPlayFabUser(request,
                result =>
                {
                    AD.DebugLogger.Log("Login", "Success SignUpWithPlayFab");
                    _loadingText.text = "Success!!";

                    AD.Managers.DataM.StrID = result.PlayFabId;

                    _nicknamePanel.SetActive(true);
                },
                error =>
                {
                    AD.DebugLogger.LogWarning("Login", $"Failed SignUpWithPlayFab -> {error}");
                    _loadingText.text = $"Failed SignUpWithPlayFab... :'( \n{error}";
                });
        }

        private async UniTask GetPlayerProfileAsync(string playFabId)
        {
            for (int i = 0; i < 3; i++)
            {
                var tcs = new UniTaskCompletionSource<bool>();

                PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest
                {
                    PlayFabId = playFabId,
                    ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
                },
                result =>
                {
                    if (string.IsNullOrEmpty(result.PlayerProfile.DisplayName))
                    {
                        _nicknamePanel.SetActive(true);
                    }
                    else
                    {
                        GoNext();
                    }

                    tcs.TrySetResult(true);
                },
                error =>
                {
                    AD.DebugLogger.LogWarning("Login", "Failed to get profile");
                    tcs.TrySetResult(false);
                });

                if (await tcs.Task) return;

                await UniTask.Delay(TimeSpan.FromSeconds(1));
            }
        }

        #endregion

        #region Nickname management

        public void CheckNickName()
        {
            string nickname = _nicknameInput.text;

            if (string.IsNullOrEmpty(nickname) || nickname.Contains(" ") || nickname.Length < 3 || nickname.Length > 20)
                _nicknameRulePanel.SetActive(true);
            else
                UpdateDisplayName(nickname);
        }

        private void UpdateDisplayName(string name)
        {
            PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = name },
            result =>
            {
                _nicknamePanel.SetActive(false);
                _nicknameRulePanel.SetActive(false);
                _nicknameConflictPanel.SetActive(false);

                AD.Managers.ServerM.SetData(new Dictionary<string, string> { { "NickName", name } }, false, false);

                _loadingText.text = "Save NickName...";
                SaveNickNameRx();
            },
            error => _nicknameConflictPanel.SetActive(true));
        }

        private void SaveNickNameRx()
        {
            _nicknameCheckSubscription?.Dispose();
            _nicknameCheckSubscription = Observable.EveryUpdate()
                .TakeUntilDestroy(this)
                .Where(_ => !AD.Managers.ServerM.isInprogress)
                .First()
                .Subscribe(_ => GoNext());
        }

        #endregion

        private void GoNext()
        {
            _loadingText.text = "Check Data...";
            AD.Managers.DataM.UpdatePlayerData();

            InitPlayerDataAsync().Forget();
        }

        private async UniTask InitPlayerDataAsync()
        {
            await UniTask.WaitUntil(() => !AD.Managers.ServerM.isInprogress);
            AD.Managers.SceneM.NextScene(
                AD.Managers.DataM._dic_player["Sex"] != "null"
                ? AD.GameConstants.Scene.Main
                : AD.GameConstants.Scene.SetCharacter
            );
        }

        #endregion

        public void ClickedOK() => AD.Managers.SoundM.UI_Ok();
    }
}
