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

        #region Functions
        /// <summary>
        /// 서버에서 데이터 가져오기
        /// </summary>
        internal void GetAllData()
        {
            isInprogress = true;

            var request = new GetUserDataRequest() { PlayFabId = AD.Managers.DataM.StrID };
            PlayFabClientAPI.GetUserData(request,
                (result) =>
                {
                    AD.Managers.DataM._dic_PlayFabPlayerData = result.Data;

                    isInprogress = false;
                },
                (error) => AD.Debug.LogWarning("ServerManager", $"Failed to GetData with PlayFab: {error}"));
        }

        /// <summary>
        /// 서버에 데이터 저장
        /// </summary>
        /// <param name="dic"></param>
        internal void SetData(Dictionary<string, string> dic, bool GetAllData = false)
        {
            isInprogress = true;

            var request = new UpdateUserDataRequest() { Data = dic, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.Debug.Log("ServerManager", "Success to SetData with PlayFab");

                    if (GetAllData)
                    {
                        this.GetAllData();
                        return;
                    }

                    isInprogress = false;
                },
                (error) =>
                {
                    AD.Debug.LogWarning("ServerManager", "Failed to SetData with PlayFab");
                    this.SetData(dic);
                });
        }

        /// <summary>
        /// 서버에 데이터 삭제
        /// Login시 데이터 체킹하는 부분인지 확인하여 데이터 오류 
        /// 지워야 할 Data의 경우 value를 null로 보내면 됨
        /// ex > DeleteData(new Dictionary<string, string> { { key, null } });
        /// </summary>
        /// <param name="dic"></param>
        internal void DeleteData(Dictionary<string, string> dic)
        {
            var request = new UpdateUserDataRequest() { Data = dic, Permission = UserDataPermission.Public };
            PlayFabClientAPI.UpdateUserData(request,
                (result) =>
                {
                    AD.Debug.Log("ServerManager", "Success to DeleteData with PlayFab");
                    GetAllData();
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
