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
    public class DataManager
    {
        [Header("--- Dictionary 데이터 ---")]
        [Tooltip("Dictionary<string, object> - 플레이어 데이터 (local data)")]
        public Dictionary<string, string> _dic_player = null;
        [Tooltip("Dictionary<string, UserDataRecord> - PlayFab에서 받아온 PlayerData (server data)")]
        public Dictionary<string, UserDataRecord> _dic_PlayFabPlayerData = null;

        [Header("--- 참고용 ---")]
        [Tooltip("현재 Player가 PlayFab에 접속한 ID")]
        private string _str_ID = string.Empty;
        public string StrID { get { return _str_ID; } set { _str_ID = value; } }

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 필요한 데이터 미리 받아 둠
        /// </summary>
        public void Init()
        {
            _dic_player = new Dictionary<string, string>();

            string getPlayer = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/Player-local").ToString();
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
        /// server data의 경우 인앱 결제 상품 등 현금과 관련된 data 혹은 바뀌지 않아야 하는 data로 구성된다. (Resources > data-readme 참고)
        /// * 게임 씬 진입 전 호출 됨
        /// </summary>
        internal void InitPlayerData()
        {
            AD.Managers.ServerM.GetAllData();
        }
        #endregion

        #endregion
    }
}