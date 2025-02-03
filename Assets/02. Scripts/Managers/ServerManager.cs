using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PlayFab;
using PlayFab.ClientModels;

namespace AD
{
    /// <summary>
    /// PlayFab Server 연동 관리
    /// </summary>
    public class ServerManager
    {
        [Tooltip("Server 통신 확인 용")]
        internal bool isInprogress = false;
        [Header("초기 데이터 세팅 시 10개를 초과하는 데이터를 보낼 시 오류 방지")]
        int _curindex = 0;
        Dictionary<string, string> _tempData = new Dictionary<string, string>();

        #region Functions
        /// <summary>
        /// 서버에서 데이터 가져온 후 업데이트
        /// isInprogress - AD.Managers.DataM.UpdateData(); 진행 후 false 처리
        /// _isConflict - AD.Managers.DataM.UpdateData() -> 데이터 문제가 생길 시
        /// -> AD.Managers.DataM SanitizeData()를 진행 후 마지막 데이터를 보낸 뒤 false 처리
        /// </summary>
        internal void GetAllData(bool Update = false)
        {
            isInprogress = true;

            var request = new GetUserDataRequest() { PlayFabId = AD.Managers.DataM.StrID };
            PlayFabClientAPI.GetUserData(request,
                (result) =>
                {
                    AD.DebugLogger.Log("ServerManager", $"Successfully GetAllData with PlayFab");

                    AD.Managers.DataM._dic_PlayFabPlayerData = result.Data;

                    if (Update)
                    {
                        AD.Managers.DataM.UpdateData();
                        return;
                    }
                    else
                        AD.Managers.DataM._isConflict = false;

                    isInprogress = false;
                },
                (error) => AD.DebugLogger.LogWarning("ServerManager", $"Failed to GetAllData with PlayFab: {error}"));
        }

        /// <summary>
        /// 서버에 데이터 저장
        /// </summary>
        /// <param name="dic"></param>
        internal void SetData(Dictionary<string, string> dic, bool GetAllData = false, bool Update = false)
        {
            isInprogress = true;

            _tempData.Clear();

            while (_curindex < dic.Count)
            {
                string key = dic.Keys.ElementAt(_curindex);
                string value = dic.Values.ElementAt(_curindex);

                if (_tempData.Count < 10)
                    _tempData.Add(key, value);

                bool isFinal = _curindex == dic.Count - 1 ? true : false;
                ++_curindex;

                if (_tempData.Count % 10 == 0 || isFinal)
                {
                    var request = new UpdateUserDataRequest() { Data = _tempData, Permission = UserDataPermission.Public };
                    PlayFabClientAPI.UpdateUserData(request,
                        (result) =>
                        {
                            if (isFinal)
                            {
                                AD.DebugLogger.Log("ServerManager", "Successfully SetData with PlayFab");
                                _curindex = 0;
                                _tempData.Clear();

                                if (GetAllData)
                                {
                                    this.GetAllData(Update: Update);
                                    return;
                                }

                                isInprogress = false;
                            }
                            else
                                this.SetData(dic, GetAllData: GetAllData, Update: Update);
                        },
                        (error) =>
                        {
                            AD.DebugLogger.LogWarning("ServerManager", "Failed to SetData with PlayFab");
                            this.SetData(dic, GetAllData: GetAllData, Update: Update);
                        });

                    break;
                }
            }
        }

        /// <summary>
        /// 새로운 PlayerData 갱신
        /// </summary>
        internal void NewData()
        {
            _tempData.Clear();

            foreach (KeyValuePair<string, string> data in AD.Managers.DataM._dic_player)
            {
                if (!AD.Managers.DataM._dic_PlayFabPlayerData.ContainsKey(data.Key))
                    _tempData.Add(data.Key, data.Value);
            }

            var request = new UpdateUserDataRequest() { Data = _tempData, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.DebugLogger.Log("ServerManager", "Successfully NewData with PlayFab");
                    _tempData.Clear();
                    GetAllData(Update: false);
                },
                (error) =>
                    AD.DebugLogger.LogWarning("ServerManager", "Failed to NewData with PlayFab - " + error));
        }

        /// <summary>
        /// 서버에 데이터 삭제
        /// Login시 데이터 체킹하는 부분인지 확인하여 데이터 오류 
        /// 지워야 할 Data의 경우 value를 null로 보내면 됨
        /// ex > DeleteData(new Dictionary<string, string> { { key, null } });
        /// </summary>
        /// <param name="dic"></param>
        internal void DeleteData(Dictionary<string, string> dic, bool Update = false)
        {
            var request = new UpdateUserDataRequest() { Data = dic, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.DebugLogger.Log("ServerManager", "Successfully DeleteData with PlayFab");
                    GetAllData(Update: Update);
                },
                (error) =>
                {
                    AD.DebugLogger.LogWarning("ServerManager", "Failed to DeleteData with PlayFab");
                    this.DeleteData(dic, Update: Update);
                });
        }
        #endregion
    }
}
