using System.IO;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using PlayFab.ClientModels;

namespace AD
{
    /// <summary>
    /// 사용하는 Data 관리
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        [Header("--- Dictionary 데이터 ---")]
        [Tooltip("Dictionary<string, UserDataRecord> - PlayFab에서 받아온 PlayerData (server data)")]
        public Dictionary<string, UserDataRecord> _dic_PlayFabPlayerData = null;
        [Tooltip("Dictionary<string, string> - 로컬에서 사용할 PlayerData (local data)")]
        public Dictionary<string, string> _dic_player = null;

        [Header("--- 참고용 ---")]
        [Tooltip("현재 Player가 PlayFab에 접속한 ID")]
        private string _str_ID = string.Empty;
        public string StrID { get { return _str_ID; } set { _str_ID = value; } }
        [Tooltip("PlayerData 초기화 시 server, local 충돌 여부")]
        private bool _isConflict = false;
        Coroutine _co_refreshData = null;

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 필요한 데이터 미리 받아 둠
        /// </summary>
        internal void Init()
        {
            _dic_player = new Dictionary<string, string>();

            string getPlayer = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/PlayerData").ToString();
            Dictionary<string, object> dic_temp = Utils.JsonToObject(getPlayer) as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> content in dic_temp)
                _dic_player.Add(content.Key, content.Value.ToString());
        }

        #region Functions

        #region Local Data

        #endregion

        #region Server Data
        /// <summary>
        /// 서버에 존재하는 플레이어 데이터 받아옴
        /// * 게임 씬 진입 전, 씬 전환 시 데이터를 갱신하기 위해 호출 됨
        /// </summary>
        internal void UpdatePlayerData()
        {
            AD.Managers.ServerM.GetAllData(Update: true);
        }
        #endregion

        /// <summary>
        /// server data, local data를 비교하여 최신화
        /// _dic_PlayFabPlayerData.Count가 1인 경우 -> NickName data만 가지고 있는 경우 -> 케릭터 선택 전 이기 때문에 무시
        /// _dic_PlayFabPlayerData.Count가 2인 경우 -> NickName, Sex data 가지고 있기 때문에 게임 씬 진입 -> 기본 데이터 세팅
        /// 만약 데이터가 추가 된다면(10개 이상 추가되지 않는다고 가정) PlayerData.json을 통해 데이터를 추가하고 이 경우 서버에 데이터를 다시 세팅
        /// RefreshData() 후 데이터가 다를 경우 데이터 갱신
        /// </summary>
        internal void UpdateData()
        {
            if (_dic_PlayFabPlayerData.Count == 1)
            {
                AD.Managers.ServerM.isInprogress = false;
                return;
            }

            if (_dic_PlayFabPlayerData.Count == 2)
            {
                _dic_player["NickName"] = _dic_PlayFabPlayerData["NickName"].Value;
                _dic_player["Sex"] = _dic_PlayFabPlayerData["Sex"].Value;

                AD.Managers.ServerM.SetData(_dic_player, GetAllData: true, Update: true);

                _co_refreshData = StartCoroutine(RefreshData());

                return;
            }

            if (_dic_PlayFabPlayerData.Count > 2 && _dic_player.Count > _dic_PlayFabPlayerData.Count)
            {
                AD.Managers.ServerM.NewData();
                return;
            }

            SanitizeData();
        }

        IEnumerator RefreshData()
        {
            while (AD.Managers.ServerM.isInprogress)
                yield return null;

            StopRefreshDataCoroutine();
        }

        void StopRefreshDataCoroutine()
        {
            if (_co_refreshData != null)
            {
                StopCoroutine(_co_refreshData);
                _co_refreshData = null;

                SanitizeData();
            }
        }

        private void SanitizeData()
        {
            int temp_result;

            _dic_player["NickName"] = _dic_PlayFabPlayerData["NickName"].Value;
            _dic_player["Sex"] = _dic_PlayFabPlayerData["Sex"].Value;
            _dic_player["Tutorial"] = _dic_PlayFabPlayerData["Tutorial"].Value;

            temp_result = CompareValues(int.Parse(_dic_player["Gold"]), int.Parse(_dic_PlayFabPlayerData["Gold"].Value));
            if (temp_result < 0)
                _dic_player["Gold"] = _dic_PlayFabPlayerData["Gold"].ToString();

            temp_result = CompareValues(int.Parse(_dic_player["Level"]), int.Parse(_dic_PlayFabPlayerData["Level"].Value));
            if (temp_result < 0)
                _dic_player["Level"] = _dic_PlayFabPlayerData["Level"].ToString();

            temp_result = CompareValues(long.Parse(_dic_player["Experience"]), long.Parse(_dic_PlayFabPlayerData["Experience"].Value));
            if (temp_result < 0)
                _dic_player["Experience"] = _dic_PlayFabPlayerData["Experience"].ToString();

            temp_result = CompareValues(int.Parse(_dic_player["HP"]), int.Parse(_dic_PlayFabPlayerData["HP"].Value));
            if (temp_result < 0)
                _dic_player["HP"] = _dic_PlayFabPlayerData["HP"].ToString();

            temp_result = CompareValues(float.Parse(_dic_player["Power"]), float.Parse(_dic_PlayFabPlayerData["Power"].Value));
            if (temp_result < 0)
                _dic_player["Power"] = _dic_PlayFabPlayerData["Power"].ToString();

            temp_result = CompareValues(float.Parse(_dic_player["AttackSpeed"]), float.Parse(_dic_PlayFabPlayerData["AttackSpeed"].Value));
            if (temp_result < 0)
                _dic_player["AttackSpeed"] = _dic_PlayFabPlayerData["AttackSpeed"].ToString();

            temp_result = CompareValues(float.Parse(_dic_player["MoveSpeed"]), float.Parse(_dic_PlayFabPlayerData["MoveSpeed"].Value));
            if (temp_result < 0)
                _dic_player["MoveSpeed"] = _dic_PlayFabPlayerData["MoveSpeed"].ToString();

            temp_result = CompareValues(int.Parse(_dic_player["MaxCount"]), int.Parse(_dic_PlayFabPlayerData["MaxCount"].Value));
            if (temp_result < 0)
                _dic_player["MaxCount"] = _dic_PlayFabPlayerData["MaxCount"].ToString();

            if (_isConflict)
                AD.Managers.ServerM.SetData(_dic_player, GetAllData: true, Update: false);
            else
                AD.Managers.ServerM.isInprogress = false;
        }

        private int CompareValues<T>(T value1, T value2) where T : System.IComparable
        {
            int result = value1.CompareTo(value2);

            if (result != 0)
                _isConflict = true;

            return result;
        }
        #endregion
    }
}