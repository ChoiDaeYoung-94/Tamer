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
        /// 초기 데이터가 없을 경우 기본 데이터 세팅
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
                                _curindex = 0;
                                GetAllData();
                            }
                            else
                                SetBasicData();
                        },
                        (error) =>
                            AD.Debug.LogWarning("ServerManager", "Failed to SetBasicData with PlayFab - " + error));

                    break;
                }
            }
        }

        /// <summary>
        /// 서버에서 데이터 가져오기
        /// </summary>
        internal void GetAllData()
        {
            var request = new GetUserDataRequest() { PlayFabId = AD.Managers.DataM.StrID };
            PlayFabClientAPI.GetUserData(request,
                (result) =>
                {
                    AD.Managers.DataM._dic_PlayFabPlayerData = result.Data;
                    AD.Managers.DataM.CheckBasicData();
                },
                (error) => AD.Debug.LogWarning("ServerManager", $"Failed to GetData with PlayFab: {error}"));
        }

        /// <summary>
        /// 서버에 데이터 저장
        /// </summary>
        /// <param name="dic"></param>
        internal void SetData(Dictionary<string, string> dic)
        {
            var request = new UpdateUserDataRequest() { Data = dic, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) => AD.Debug.Log("ServerManager", "Success to SetData with PlayFab"),
                (error) =>
                {
                    AD.Debug.LogWarning("ServerManager", "Failed to SetData with PlayFab");
                    SetData(dic);
                });
        }
        #endregion
    }
}