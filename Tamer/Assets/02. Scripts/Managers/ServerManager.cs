using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PlayFab;
using PlayFab.ClientModels;

namespace AD
{
    public class ServerManager
    {
        #region Functions
        /// <summary>
        /// 초기 데이터가 없을 경우 기본 데이터 세팅
        /// </summary>
        internal void SetBasicData()
        {
            var request = new UpdateUserDataRequest() { Data = AD.Managers.DataM._dic_player, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) => GetAllData(),
                (error) => AD.Debug.LogWarning("ServerManager", "Failed to SetBasicData with PlayFab"));
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