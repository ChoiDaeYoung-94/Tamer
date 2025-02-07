using System.Linq;
using System.Collections.Generic;

using PlayFab;
using PlayFab.ClientModels;

namespace AD
{
    /// <summary>
    /// PlayFab 서버 연동 관리
    /// </summary>
    public class ServerManager
    {
        // 서버 요청 진행 여부
        public bool IsInProgress { get; private set; } = false;

        // 서버 연동 시 사용할 변수
        private int _currentIndex = 0;
        private Dictionary<string, string> _tempData = new Dictionary<string, string>();

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

            var request = new GetUserDataRequest() { PlayFabId = AD.Managers.DataM.PlayFabId };
            PlayFabClientAPI.GetUserData(request,
                (result) =>
                {
                    AD.DebugLogger.Log("ServerManager", $"Successfully GetAllData from PlayFab");

                    AD.Managers.DataM.PlayFabPlayerData = result.Data;

                    if (update)
                    {
                        AD.Managers.DataM.UpdateData();
                        return;
                    }
                    else
                        AD.Managers.DataM.IsConflict = false;

                    IsInProgress = false;
                },
                (error) => AD.DebugLogger.LogWarning("ServerManager", $"Failed to GetAllData from PlayFab: {error}"));
        }

        /// <summary>
        /// 서버에 데이터 저장
        /// </summary>
        /// <param name="dic"></param>
        public void SetData(Dictionary<string, string> dic, bool getAllData = false, bool update = false)
        {
            IsInProgress = true;

            _tempData.Clear();

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
                                this.SetData(dic, getAllData: getAllData, update: update);
                        },
                        (error) =>
                        {
                            AD.DebugLogger.LogWarning("ServerManager", "Failed to SetData to PlayFab");
                            this.SetData(dic, getAllData: getAllData, update: update);
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
            _tempData.Clear();

            foreach (KeyValuePair<string, string> data in AD.Managers.DataM.LocalPlayerData)
            {
                if (!AD.Managers.DataM.PlayFabPlayerData.ContainsKey(data.Key))
                    _tempData.Add(data.Key, data.Value);
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
                    AD.DebugLogger.LogWarning("ServerManager", "Failed to NewData to PlayFab - " + error));
        }

        /// <summary>
        /// 서버에 데이터 삭제
        /// Login시 데이터 체킹하는 부분인지 확인하여 데이터 오류 
        /// 지워야 할 Data의 경우 value를 null로 보내면 됨
        /// ex > DeleteData(new Dictionary<string, string> { { key, null } });
        /// </summary>
        public void DeleteData(Dictionary<string, string> dic, bool update = false)
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
                    AD.DebugLogger.LogWarning("ServerManager", "Failed to DeleteData from PlayFab");
                    this.DeleteData(dic, update: update);
                });
        }

        public void SetInProgress(bool value) => IsInProgress = value;
        #endregion
    }
}
