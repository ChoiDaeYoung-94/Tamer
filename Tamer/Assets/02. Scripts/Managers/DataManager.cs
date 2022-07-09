using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AD
{
    public class DataManager
    {
        [Header("--- 참고용 ---")]
        [Tooltip("Dictionary<string, object> - Test")]
        public Dictionary<string, object> _dic_test = null;
        [Tooltip("현재 Player가 PlayFab에 접속한 ID")]
        string _str_ID = string.Empty;
        public string StrID { get { return _str_ID; } set { _str_ID = value; } }
        [Tooltip("현재 Player가 설정한 NickName")]
        string _str_NickName = string.Empty;
        public string StrNickName { get { return _str_NickName; } set { _str_NickName = value; } }

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 필요한 데이터 미리 받아 둠
        /// </summary>
        public void Init()
        {
            //string getTest = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/getTest").ToString();
            //_dic_test = Utils.JsonToObject(getTest) as Dictionary<string, object>;
        }
    }
}