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
        [Tooltip("Dictionary<string, object> - 플레이어 초기 데이터")]
        public Dictionary<string, string> _dic_player = null;
        [Tooltip("Dictionary<string, UserDataRecord> - PlayFab에서 받아온 PlayerData")]
        public Dictionary<string, UserDataRecord> _dic_PlayFabPlayerData = null;

        [Header("--- 참고용 ---")]
        [Tooltip("현재 Player가 PlayFab에 접속한 ID")]
        private string _str_ID = string.Empty;
        public string StrID { get { return _str_ID; } set { _str_ID = value; } }
        [Tooltip("현재 Player가 설정한 NickName")]
        private string _str_NickName = string.Empty;
        public string StrNickName { get { return _str_NickName; } set { _str_NickName = value; } }

        [Tooltip("접속 후 PlayerData 를 다 받고 세팅이 끝났는지 여부 확인")]
        bool _isFinished = false;
        public bool IsFinished { get { return _isFinished; } }

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 필요한 데이터 미리 받아 둠
        /// </summary>
        public void Init()
        {
            _dic_player = new Dictionary<string, string>();

            string getPlayer = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/Player").ToString();
            Dictionary<string, object> dic_temp = Utils.JsonToObject(getPlayer) as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> content in dic_temp)
                _dic_player.Add(content.Key, content.Value.ToString());
        }

        #region Functions

        #region Server Data Checking
        /// <summary>
        /// 서버에 존재하는 플레이어 데이터 초기화
        /// * 게임 씬 진입 전 호출 됨
        /// </summary>
        internal void InitPlayerData()
        {
            AD.Managers.ServerM.GetAllData();
        }

        /// <summary>
        /// 서버에서 받아온 Player Data 가공 후 데이터 세팅 종료
        /// </summary>
        internal void SetPlayerData()
        {
            if (string.IsNullOrEmpty(StrNickName))
                StrNickName = _dic_PlayFabPlayerData["NickName"].Value;
            else
                AD.Managers.ServerM.SetData(new Dictionary<string, string> { { "NickName", StrNickName } });

            _isFinished = true;
        }
        #endregion

        #endregion
    }
}