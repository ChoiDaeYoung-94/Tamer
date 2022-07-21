using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PlayFab.ClientModels;

namespace AD
{
    public class DataManager
    {
        [Header("--- Dictionary 데이터 ---")]
        [Tooltip("Dictionary<string, object> - 플레이어 초기 데이터")]
        public Dictionary<string, string> _dic_player = null;
        [Tooltip("Dictionary<string, UserDataRecord> - PlayFab에서 받아온 PlayerData")]
        public Dictionary<string, UserDataRecord> _dic_PlayFabPlayerData = null;

        [Header("--- 참고용 ---")]
        [Tooltip("현재 Player가 PlayFab에 접속한 ID")]
        string _str_ID = string.Empty;
        public string StrID { get { return _str_ID; } set { _str_ID = value; } }
        [Tooltip("현재 Player가 설정한 NickName")]
        string _str_NickName = string.Empty;
        public string StrNickName { get { return _str_NickName; } set { _str_NickName = value; } }
        public int Ply_gold { get; set; }
        public int Ply_level { get; set; }
        public long Ply_experience { get; set; }
        public float Ply_power { get; set; }
        public float Ply_attackSpeed { get; set; }
        public int Ply_maxCount { get; set; }
        [Tooltip("접속 후 PlayerData 를 다 받고 세팅이 끝났는지 여부 확인")]
        bool _isFinished = false;
        public bool IsFinished { get { return _isFinished; } }

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 필요한 데이터 미리 받아 둠
        /// </summary>
        public void Init()
        {
            this._dic_player = new Dictionary<string, string>();

            string getPlayer = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/Player").ToString();
            Dictionary<string, object> dic_temp = Utils.JsonToObject(getPlayer) as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> content in dic_temp)
                this._dic_player.Add(content.Key, content.Value.ToString());
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
        /// 서버에서 받아온 데이터 체킹 후
        /// * 데이터가 존재하지 않을 경우 초기화 진행
        /// * 기본 Player데이터 보다 적을 경우 [ 어딘가에서 꼬인 느낌이라 ]
        /// </summary>
        internal void CheckBasicData()
        {
            if (this._dic_PlayFabPlayerData == null || this._dic_PlayFabPlayerData.Count < this._dic_player.Count)
                AD.Managers.ServerM.SetBasicData();
            else
                SetPlayerData();
        }

        /// <summary>
        /// Player Data 미리 가공
        /// </summary>
        void SetPlayerData()
        {
            if (string.IsNullOrEmpty(this.StrNickName))
                this.StrNickName = this._dic_PlayFabPlayerData["NickName"].Value;
            else
                Managers.ServerM.SetData(new Dictionary<string, string> { { "NickName", this.StrNickName } });

            this.Ply_gold = int.Parse(_dic_PlayFabPlayerData["Gold"].Value);
            this.Ply_level = int.Parse(_dic_PlayFabPlayerData["Level"].Value);
            this.Ply_experience = long.Parse(_dic_PlayFabPlayerData["Experience"].Value);
            this.Ply_power = float.Parse(_dic_PlayFabPlayerData["Power"].Value);
            this.Ply_attackSpeed = float.Parse(_dic_PlayFabPlayerData["AttackSpeed"].Value);
            this.Ply_maxCount = int.Parse(_dic_PlayFabPlayerData["MaxCount"].Value);

            _isFinished = true;
        }
        #endregion

        #endregion
    }
}