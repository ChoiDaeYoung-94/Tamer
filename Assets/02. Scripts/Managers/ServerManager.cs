using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PlayFab;
using PlayFab.ClientModels;

namespace AD
{
    public class ServerManager
    {
        [Header("초기 데이터 세팅 시 10개를 초과하는 데이터를 보낼 시 오류 방지")]
        int _curindex = 0;
        Dictionary<string, string> _tempData = new Dictionary<string, string>();

        #region Functions
        /// <summary>
        /// 서버에서 데이터 가져오기
        /// </summary>
        internal void GetAllData(bool isFinalStep = false)
        {
            var request = new GetUserDataRequest() { PlayFabId = AD.Managers.DataM.StrID };
            PlayFabClientAPI.GetUserData(request,
                (result) =>
                {
                    AD.Managers.DataM._dic_PlayFabPlayerData = result.Data;
                    if (isFinalStep)
                        AD.Managers.DataM.SetPlayerData();
                    else
                        FixPlayerData();
                },
                (error) => AD.Debug.LogWarning("ServerManager", $"Failed to GetData with PlayFab: {error}"));
        }

        /// <summary>
        /// * 현재 Player Data를 받아온 뒤 데이터 검증
        /// 1. Data가 없을 경우 초기 data 세팅
        /// 2. 지우는 작업 (지워야 할 Data의 경우 value를 null로 보내면 됨)
        /// 3. 추가 작업
        /// 4. 위 작업이 모두 끝나면 데이터 세팅 종료
        /// </summary
        internal void FixPlayerData()
        {
            if (AD.Managers.DataM._dic_PlayFabPlayerData == null || AD.Managers.DataM._dic_PlayFabPlayerData.Count == 0)
            {
                SetBasicData();
                return;
            }

            foreach (string key in AD.Managers.DataM._dic_PlayFabPlayerData.Keys)
            {
                if (!AD.Managers.DataM._dic_player.ContainsKey(key))
                {
                    DeleteData(new Dictionary<string, string> { { key, null } }, isLogin: true);
                    AD.Managers.DataM._dic_PlayFabPlayerData.Remove(key);
                    return;
                }
            }

            foreach (KeyValuePair<string, string> dic in AD.Managers.DataM._dic_player)
            {
                if (!AD.Managers.DataM._dic_PlayFabPlayerData.ContainsKey(dic.Key))
                {
                    SetData(new Dictionary<string, string> { { dic.Key, dic.Value } }, isLogin: true);
                    AD.Managers.DataM._dic_PlayFabPlayerData.Add(dic.Key, new UserDataRecord());
                    return;
                }
            }

            this.GetAllData(isFinalStep: true);
        }

        /// <summary>
        /// * 초기 데이터가 없을 경우 기본 데이터 세팅
        /// </summary>
        internal void SetBasicData()
        {
            _tempData.Clear();

            while (_curindex < AD.Managers.DataM._dic_player.Count)
            {
                string key = AD.Managers.DataM._dic_player.Keys.ElementAt(_curindex);
                string value = AD.Managers.DataM._dic_player.Values.ElementAt(_curindex);

                if (_tempData.Count < 10)
                    _tempData.Add(key, value);

                bool isFinal = _curindex == AD.Managers.DataM._dic_player.Count - 1 ? true : false;
                ++_curindex;
                if (_tempData.Count == 10 || isFinal)
                {
                    var request = new UpdateUserDataRequest() { Data = _tempData, Permission = UserDataPermission.Public };
                    PlayFabClientAPI.UpdateUserData(request,
                        (result) =>
                        {
                            if (isFinal)
                            {
                                AD.Debug.Log("ServerManager", "Success to SetBasicData with PlayFab");
                                _curindex = 0;
                                this.GetAllData();
                            }
                            else
                                this.SetBasicData();
                        },
                        (error) =>
                            AD.Debug.LogWarning("ServerManager", "Failed to SetBasicData with PlayFab - " + error));

                    break;
                }
            }
        }

        /// <summary>
        /// 서버에 데이터 저장
        /// </summary>
        /// <param name="dic"></param>
        internal void SetData(Dictionary<string, string> dic, bool isLogin = false)
        {
            var request = new UpdateUserDataRequest() { Data = dic, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.Debug.Log("ServerManager", "Success to SetData with PlayFab");
                    if (isLogin)
                        FixPlayerData();
                },
                (error) =>
                {
                    AD.Debug.LogWarning("ServerManager", "Failed to SetData with PlayFab");
                    this.SetData(dic);
                });
        }

        /// <summary>
        /// 서버에 데이터 삭제
        /// Login시 데이터 체킹하는 부분인지 확인하여 데이터 오류 방지
        /// </summary>
        /// <param name="dic"></param>
        internal void DeleteData(Dictionary<string, string> dic, bool isLogin = false)
        {
            var request = new UpdateUserDataRequest() { Data = dic, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.Debug.Log("ServerManager", "Success to DeleteData with PlayFab");
                    if (isLogin)
                        this.FixPlayerData();
                },
                (error) =>
                {
                    AD.Debug.LogWarning("ServerManager", "Failed to DeleteData with PlayFab");
                    this.DeleteData(dic);
                });
        }
        #endregion
    }
}