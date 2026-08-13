using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;

using PlayFab;
using PlayFab.ClientModels;

using Cysharp.Threading.Tasks;

namespace AD
{
    /// <summary>
    /// PlayFab 서버 연동 관리
    ///
    /// 모든 요청은 실패 시 제한된 횟수만 재시도하며, 최종 실패해도 반드시 IsInProgress를 내린다.
    /// (IsInProgress가 계속 true면 Login에서 무한 대기 -> 게임 진입 불가)
    /// </summary>
    public class ServerManager
    {
        // 서버 요청 진행 여부
        public bool IsInProgress { get; private set; } = false;

        /// <summary>
        /// 마지막 서버 요청이 실패했는지 여부 (로컬 데이터로 진행했는지 판단용)
        /// </summary>
        public bool HasFailed { get; private set; } = false;

        // 서버 연동 시 사용할 변수
        private int _currentIndex = 0;
        private Dictionary<string, string> _tempData = new Dictionary<string, string>();

        private const int MaxRetryCount = 3;
        private const string LogTag = "[Tamer/Server]";

        #region Functions
        /// <summary>
        /// 서버에서 데이터 가져온 후 업데이트
        /// isInprogress - AD.Managers.DataM.UpdateData(); 진행 후 false 처리
        /// _isConflict - AD.Managers.DataM.UpdateData() -> 데이터 문제가 생길 시
        /// -> AD.Managers.DataM SanitizeData()를 진행 후 마지막 데이터를 보낸 뒤 false 처리
        /// </summary>
        public void GetAllData(bool update = false)
        {
            IsInProgress = true;
            HasFailed = false;

            GetAllDataInternal(update, 0);
        }

        private void GetAllDataInternal(bool update, int attempt)
        {
            var request = new GetUserDataRequest() { PlayFabId = AD.Managers.DataM.PlayFabId };
            PlayFabClientAPI.GetUserData(request,
                (result) =>
                {
                    AD.DebugLogger.Log("ServerManager", $"Successfully GetAllData from PlayFab");

                    AD.Managers.DataM.PlayFabPlayerData = result.Data ?? new Dictionary<string, UserDataRecord>();

                    if (update)
                    {
                        AD.Managers.DataM.UpdateData();
                        return;
                    }
                    else
                        AD.Managers.DataM.IsConflict = false;

                    IsInProgress = false;
                },
                (error) =>
                {
                    AD.DebugLogger.LogWarning("ServerManager", $"Failed to GetAllData from PlayFab: {error}");

                    if (attempt + 1 < MaxRetryCount)
                    {
                        RetryAsync(attempt, () => GetAllDataInternal(update, attempt + 1)).Forget();
                        return;
                    }

                    // 재시도 소진 -> 진행 중 상태를 반드시 해제해야 로그인이 멈추지 않는다
                    Abort($"GetAllData 실패 -> {Describe(error)}");
                });
        }

        /// <summary>
        /// 서버에 데이터 저장
        /// </summary>
        /// <param name="dic"></param>
        public void SetData(Dictionary<string, string> dic, bool getAllData = false, bool update = false)
        {
            IsInProgress = true;
            HasFailed = false;

            SetDataInternal(dic, getAllData, update, 0);
        }

        private void SetDataInternal(Dictionary<string, string> dic, bool getAllData, bool update, int attempt)
        {
            if (dic == null || dic.Count == 0)
            {
                _currentIndex = 0;
                IsInProgress = false;
                return;
            }

            _tempData.Clear();

            // 실패 시 같은 구간을 다시 보내기 위해 시작 위치를 기억한다
            int chunkStartIndex = _currentIndex;

            while (_currentIndex < dic.Count)
            {
                string key = dic.Keys.ElementAt(_currentIndex);
                string value = dic.Values.ElementAt(_currentIndex);

                if (_tempData.Count < 10)
                    _tempData.Add(key, value);

                bool isFinal = _currentIndex == dic.Count - 1 ? true : false;
                ++_currentIndex;

                if (_tempData.Count % 10 == 0 || isFinal)
                {
                    var request = new UpdateUserDataRequest() { Data = _tempData, Permission = UserDataPermission.Public };
                    PlayFabClientAPI.UpdateUserData(request,
                        (result) =>
                        {
                            if (isFinal)
                            {
                                AD.DebugLogger.Log("ServerManager", "Successfully SetData to PlayFab");
                                _currentIndex = 0;
                                _tempData.Clear();

                                if (getAllData)
                                {
                                    this.GetAllData(update: update);
                                    return;
                                }

                                IsInProgress = false;
                            }
                            else
                                this.SetDataInternal(dic, getAllData, update, 0);
                        },
                        (error) =>
                        {
                            AD.DebugLogger.LogWarning("ServerManager", $"Failed to SetData to PlayFab: {error}");

                            // 실패한 구간부터 다시 보낸다 (그대로 진행하면 해당 구간이 유실됨)
                            _currentIndex = chunkStartIndex;

                            if (attempt + 1 < MaxRetryCount)
                            {
                                RetryAsync(attempt, () => SetDataInternal(dic, getAllData, update, attempt + 1)).Forget();
                                return;
                            }

                            _currentIndex = 0;
                            _tempData.Clear();
                            Abort($"SetData 실패 -> {Describe(error)}");
                        });

                    break;
                }
            }
        }

        /// <summary>
        /// 새로운 플레이어 데이터 갱신
        /// </summary>
        public void UpdateNewPlayerData()
        {
            IsInProgress = true;
            UpdateNewPlayerDataInternal(0);
        }

        private void UpdateNewPlayerDataInternal(int attempt)
        {
            _tempData.Clear();

            foreach (KeyValuePair<string, string> data in AD.Managers.DataM.LocalPlayerData)
            {
                if (!AD.Managers.DataM.PlayFabPlayerData.ContainsKey(data.Key))
                    _tempData.Add(data.Key, data.Value);
            }

            if (_tempData.Count == 0)
            {
                GetAllData(update: false);
                return;
            }

            var request = new UpdateUserDataRequest() { Data = _tempData, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.DebugLogger.Log("ServerManager", "Successfully NewData to PlayFab");
                    _tempData.Clear();
                    GetAllData(update: false);
                },
                (error) =>
                {
                    AD.DebugLogger.LogWarning("ServerManager", "Failed to NewData to PlayFab - " + error);

                    if (attempt + 1 < MaxRetryCount)
                    {
                        RetryAsync(attempt, () => UpdateNewPlayerDataInternal(attempt + 1)).Forget();
                        return;
                    }

                    _tempData.Clear();
                    Abort($"UpdateNewPlayerData 실패 -> {Describe(error)}");
                });
        }

        /// <summary>
        /// 서버에 데이터 삭제
        /// Login시 데이터 체킹하는 부분인지 확인하여 데이터 오류
        /// 지워야 할 Data의 경우 value를 null로 보내면 됨
        /// ex > DeleteData(new Dictionary<string, string> { { key, null } });
        /// </summary>
        public void DeleteData(Dictionary<string, string> dic, bool update = false)
        {
            IsInProgress = true;
            DeleteDataInternal(dic, update, 0);
        }

        private void DeleteDataInternal(Dictionary<string, string> dic, bool update, int attempt)
        {
            var request = new UpdateUserDataRequest() { Data = dic, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.DebugLogger.Log("ServerManager", "Successfully DeleteData from PlayFab");
                    GetAllData(update: update);
                },
                (error) =>
                {
                    AD.DebugLogger.LogWarning("ServerManager", $"Failed to DeleteData from PlayFab: {error}");

                    if (attempt + 1 < MaxRetryCount)
                    {
                        RetryAsync(attempt, () => DeleteDataInternal(dic, update, attempt + 1)).Forget();
                        return;
                    }

                    Abort($"DeleteData 실패 -> {Describe(error)}");
                });
        }

        public void SetInProgress(bool value) => IsInProgress = value;
        #endregion

        #region Internal

        /// <summary>
        /// backoff 후 재시도 (즉시 재귀 호출하면 실패 시 서버를 계속 두드리게 된다)
        /// </summary>
        private static async UniTaskVoid RetryAsync(int attempt, Action retry)
        {
            float delay = Mathf.Pow(2f, attempt); // 1s, 2s
            Debug.Log($"{LogTag} 재시도 대기 {delay}s (attempt {attempt + 1}/{MaxRetryCount})");

            await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true);
            retry();
        }

        /// <summary>
        /// 최종 실패 처리. 대기 중인 흐름이 풀리도록 IsInProgress를 반드시 내린다.
        /// </summary>
        private void Abort(string message)
        {
            HasFailed = true;
            IsInProgress = false;

            // release 빌드에서도 원인 추적이 가능하도록 남긴다
            Debug.LogWarning($"{LogTag} {message}");
        }

        private static string Describe(PlayFabError error)
        {
            if (error == null)
                return "unknown";

            return $"{error.Error}({error.HttpCode}) {error.ErrorMessage}";
        }

        #endregion
    }
}
