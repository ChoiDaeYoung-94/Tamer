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
        [Tooltip("Dictionary<string, object> - monster data")]
        public Dictionary<string, object> _dic_monsters = null;

        [Header("--- 참고용 ---")]
        [Tooltip("현재 Player가 PlayFab에 접속한 ID")]
        private string _str_ID = string.Empty;
        public string StrID { get { return _str_ID; } set { _str_ID = value; } }
        [Tooltip("PlayerData 초기화 시 server, local 충돌 여부")]
        internal bool _isConflict = false;
        [Tooltip("Application.persistentDataPath.PlayerData.json 위치")]
        private string _str_apPlayerDataPath = string.Empty;
        [Tooltip("Resources/Data/PlayerData.json 내용")]
        private string _str_rePlayerData = string.Empty;
        [Tooltip("Resources/Data/MonstersData.json 내용")]
        private string _str_reMonstersData = string.Empty;
        Coroutine _co_refreshData = null;

        /// <summary>
        /// Managers - Awake() -> Init()
        /// 필요한 데이터 미리 받고 세팅 및 데이터 갱신 코루틴 실행
        /// </summary>
        internal void Init()
        {
            LoadPlayerData();
            LoadMonstersData();

            StartCoroutine(Co_UpdateFewMinutes());
        }

        #region Functions
        /// <summary>
        /// 게임 시작 시 PlayerData를 초기화
        /// 첫 시작일 경우 Resources에 있는 PlayerData.json, 두번째 이상일 경우 persistentDataPath
        /// </summary>
        private void LoadPlayerData()
        {
            AD.Debug.Log("DataManager", "LoadPlayerData() -> PlayerData 초기화");

            _dic_player = new Dictionary<string, string>();

            _str_apPlayerDataPath = Path.Combine(Application.persistentDataPath, "PlayerData.json");
            _str_rePlayerData = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/PlayerData").ToString();

            string str_temp_getPlayerData = File.Exists(_str_apPlayerDataPath) ? File.ReadAllText(_str_apPlayerDataPath) : _str_rePlayerData;

            InitPlayerData(str_temp_getPlayerData);
        }

        private void InitPlayerData(string data)
        {
            Dictionary<string, object> dic_temp = AD.Utils.JsonToObject(data) as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> content in dic_temp)
                _dic_player.Add(content.Key, content.Value.ToString());

            if (File.Exists(_str_apPlayerDataPath))
                CheckNewPlayerData();

            File.WriteAllText(_str_apPlayerDataPath, data);

            AD.Debug.Log("DataManager", "InitPlayerData() -> PlayerData 초기화 완료");
        }

        private void CheckNewPlayerData()
        {
            AD.Debug.Log("DataManager", "CheckNewPlayerData() -> 새로운 PlayerData 검출");

            Dictionary<string, object> dic_temp = AD.Utils.JsonToObject(_str_rePlayerData) as Dictionary<string, object>;

            if (dic_temp.Count > _dic_player.Count)
            {
                foreach (KeyValuePair<string, object> newdata in dic_temp)
                    if (!_dic_player.ContainsKey(newdata.Key))
                        _dic_player.Add(newdata.Key, newdata.Value.ToString());
            }
        }

        private void LoadMonstersData()
        {
            _str_reMonstersData = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/MonstersData").ToString();

            _dic_monsters = new Dictionary<string, object>();

            Dictionary<string, object> dic_temp = AD.Utils.JsonToObject(_str_reMonstersData) as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> content in dic_temp)
                _dic_monsters.Add(content.Key, content.Value);
        }

        #region Local Data
        IEnumerator Co_UpdateFewMinutes()
        {
            while (true)
            {
                yield return new WaitForSeconds(60f);

                UpdateLocalData();
            }
        }

        /// <summary>
        /// Player가 가지고 있는 고유 Data들을 _dic_player에 갱신 후 Json 저장
        /// </summary>
        internal void UpdateLocalData()
        {
            if (Player.Instance)
            {
                _dic_player["Gold"] = Player.Instance.Gold.ToString();
                _dic_player["CurCaptureCapacity"] = Player.Instance.CurCaptureCapacity.ToString();
                _dic_player["MaxCaptureCapacity"] = Player.Instance.MaxCaptureCapacity.ToString();
                _dic_player["Power"] = Player.Instance.Power.ToString();
                _dic_player["AttackSpeed"] = Player.Instance.AttackSpeed.ToString();
                _dic_player["MoveSpeed"] = Player.Instance.MoveSpeed.ToString();

                SaveLocalData();
            }
        }

        internal void SaveLocalData()
        {
            string str_temp = AD.Utils.ObjectToJson(_dic_player);
            File.WriteAllText(_str_apPlayerDataPath, str_temp);

            AD.Debug.Log("DataManager", "SaveLocalData() -> PlayerData json저장 완료");
        }
        #endregion

        #region Server Data
        /// <summary>
        /// 서버에 존재하는 플레이어 데이터 받아옴
        /// * 게임 씬 진입 전, 씬 전환 시 데이터를 갱신하기 위해 호출 됨
        /// </summary>
        internal void UpdatePlayerData()
        {
            AD.Debug.Log("DataManager", "UpdatePlayerData() -> PlayerData 갱신 작업 시작");

            AD.Managers.ServerM.GetAllData(Update: true);
        }

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
        #endregion

        /// <summary>
        /// PlayerData 정리
        /// local, server 비교하여 데이터 최신화 위함
        /// </summary>
        private void SanitizeData()
        {
            AD.Debug.Log("DataManager", "SanitizeData() -> local, server 비교하여 PlayerData 최신화");

            int temp_result;

            _dic_player["NickName"] = _dic_PlayFabPlayerData["NickName"].Value;
            _dic_player["Sex"] = _dic_PlayFabPlayerData["Sex"].Value;
            _dic_player["Tutorial"] = _dic_PlayFabPlayerData["Tutorial"].Value;

            temp_result = CompareValues(int.Parse(_dic_player["Gold"]), int.Parse(_dic_PlayFabPlayerData["Gold"].Value));
            if (temp_result < 0)
                _dic_player["Gold"] = _dic_PlayFabPlayerData["Gold"].Value.ToString();

            temp_result = CompareValues(int.Parse(_dic_player["CurCaptureCapacity"]), int.Parse(_dic_PlayFabPlayerData["CurCaptureCapacity"].Value));
            if (temp_result < 0)
                _dic_player["CurCaptureCapacity"] = _dic_PlayFabPlayerData["CurCaptureCapacity"].Value.ToString();

            temp_result = CompareValues(int.Parse(_dic_player["MaxCaptureCapacity"]), int.Parse(_dic_PlayFabPlayerData["MaxCaptureCapacity"].Value));
            if (temp_result < 0)
                _dic_player["MaxCaptureCapacity"] = _dic_PlayFabPlayerData["MaxCaptureCapacity"].Value.ToString();

            temp_result = CompareValues(float.Parse(_dic_player["Power"]), float.Parse(_dic_PlayFabPlayerData["Power"].Value));
            if (temp_result < 0)
                _dic_player["Power"] = _dic_PlayFabPlayerData["Power"].Value.ToString();

            temp_result = CompareValues(float.Parse(_dic_player["AttackSpeed"]), float.Parse(_dic_PlayFabPlayerData["AttackSpeed"].Value));
            if (temp_result < 0)
                _dic_player["AttackSpeed"] = _dic_PlayFabPlayerData["AttackSpeed"].Value.ToString();

            temp_result = CompareValues(float.Parse(_dic_player["MoveSpeed"]), float.Parse(_dic_PlayFabPlayerData["MoveSpeed"].Value));
            if (temp_result < 0)
                _dic_player["MoveSpeed"] = _dic_PlayFabPlayerData["MoveSpeed"].Value.ToString();

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