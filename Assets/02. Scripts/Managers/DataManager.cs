using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

using UnityEngine;

using PlayFab.ClientModels;

using Cysharp.Threading.Tasks;

namespace AD
{
    /// <summary>
    /// 플레이어 데이터(로컬 및 서버), 몬스터 데이터, 아이템 데이터를 관리
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        public Dictionary<string, UserDataRecord> PlayFabPlayerData = null;
        public Dictionary<string, string> LocalPlayerData = null;
        public Dictionary<string, object> MonsterData = null;
        public Dictionary<string, object> ItemData = null;

        private string _playFabId = string.Empty;
        public string PlayFabId { get { return _playFabId; } set { _playFabId = value; } }

        [Tooltip("PlayerData 초기화 시 server, local 충돌 여부")]
        public bool IsConflict = false;

        [Tooltip("Application.persistentDataPath에 위치한 PlayerData.json 파일 경로")]
        private string _playerDataPath = string.Empty;
        private string _resourcePlayerData = string.Empty;
        private string _resourceMonstersData = string.Empty;
        private string _resourceItemsData = string.Empty;

        private CancellationTokenSource _ctsLocalDataUpdate;
        private CancellationTokenSource _ctsRefreshData;

        /// <summary>
        /// Managers - Awake() -> InitializeData()
        /// 필요한 데이터를 미리 로드 및 초기화하며, 주기적으로 로컬 데이터를 갱신하는 코루틴을 시작
        /// </summary>
        public void InitializeData()
        {
            LoadPlayerData();
            LoadMonstersData();
            LoadItemsData();

            // 주기적인 로컬 데이터 업데이트 시작 (UniTask 사용)
            _ctsLocalDataUpdate = new CancellationTokenSource();
            PeriodicLocalDataUpdateAsync(_ctsLocalDataUpdate.Token).Forget();
        }

        #region Load data

        /// <summary>
        /// 게임 시작 시 PlayerData를 초기화
        /// 첫 실행 시 Resources에 있는 PlayerData.json을 사용하며, 이후에는 persistentDataPath의 파일을 사용
        /// </summary>
        private void LoadPlayerData()
        {
            AD.DebugLogger.Log("DataManager", "LoadPlayerData() -> PlayerData 초기화");

            LocalPlayerData = new Dictionary<string, string>();

            _playerDataPath = Path.Combine(Application.persistentDataPath, "PlayerData.json");
            _resourcePlayerData = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/PlayerData").ToString();

            string playerDataJson = File.Exists(_playerDataPath)
                ? File.ReadAllText(_playerDataPath)
                : _resourcePlayerData;

            InitializePlayerData(playerDataJson);
        }

        /// <summary>
        /// JSON 데이터를 파싱하여 로컬 플레이어 데이터를 초기화
        /// </summary>
        private void InitializePlayerData(string data)
        {
            Dictionary<string, object> tempData = AD.Utility.DeserializeFromJson(data) as Dictionary<string, object>;
            foreach (KeyValuePair<string, object> kv in tempData)
            {
                LocalPlayerData.Add(kv.Key, kv.Value.ToString());
            }

            if (File.Exists(_playerDataPath))
            {
                CheckForNewPlayerData();
            }

            File.WriteAllText(_playerDataPath, data);

            AD.DebugLogger.Log("DataManager", "InitializePlayerData() -> PlayerData 초기화 완료");
        }

        /// <summary>
        /// 기존 플레이어 데이터와 리소스의 플레이어 데이터를 비교하여 새로운 데이터가 있으면 추가
        /// </summary>
        private void CheckForNewPlayerData()
        {
            AD.DebugLogger.Log("DataManager", "CheckForNewPlayerData() -> 새로운 PlayerData 검출");

            Dictionary<string, object> resourceData = AD.Utility.DeserializeFromJson(_resourcePlayerData) as Dictionary<string, object>;
            if (resourceData.Count > LocalPlayerData.Count)
            {
                foreach (KeyValuePair<string, object> newData in resourceData)
                {
                    if (!LocalPlayerData.ContainsKey(newData.Key))
                    {
                        LocalPlayerData.Add(newData.Key, newData.Value.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Resources에서 몬스터 데이터를 로드
        /// </summary>
        private void LoadMonstersData()
        {
            _resourceMonstersData = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/MonstersData").ToString();
            MonsterData = AD.Utility.DeserializeFromJson(_resourceMonstersData) as Dictionary<string, object>;
        }

        /// <summary>
        /// Resources에서 아이템 데이터를 로드합니다.
        /// </summary>
        private void LoadItemsData()
        {
            _resourceItemsData = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/ItemsData").ToString();
            ItemData = AD.Utility.DeserializeFromJson(_resourceItemsData) as Dictionary<string, object>;
        }

        #endregion

        #region Update, save data

        /// <summary>
        /// UniTask를 사용하여 60초마다 로컬 데이터를 업데이트합니다.
        /// </summary>
        private async UniTask PeriodicLocalDataUpdateAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(60), cancellationToken: token);
                UpdateLocalData("null", "null", updateAll: true);
            }
        }

        /// <summary>
        /// 플레이어의 고유 데이터를 갱신한 후 JSON 파일로 저장
        /// </summary>
        /// <param name="key">갱신할 데이터의 키</param>
        /// <param name="value">갱신할 데이터의 값</param>
        /// <param name="updateAll">전체 데이터를 갱신할지 여부</param>
        public void UpdateLocalData(string key, string value, bool updateAll = false)
        {
            if (Player.Instance)
            {
                if (updateAll)
                {
                    LocalPlayerData["Gold"] = Player.Instance.Gold.ToString();
                }
                else
                {
                    LocalPlayerData[key] = value;
                }

                SaveLocalData();
            }
        }

        /// <summary>
        /// 로컬 플레이어 데이터를 JSON 파일로 저장
        /// </summary>
        public void SaveLocalData()
        {
            string json = AD.Utility.SerializeToJson(LocalPlayerData);
            File.WriteAllText(_playerDataPath, json);
            AD.DebugLogger.Log("DataManager", "SaveLocalData() -> PlayerData json 저장 완료");
        }

        /// <summary>
        /// 서버에 존재하는 플레이어 데이터를 가져와 업데이트
        /// </summary>
        public void UpdatePlayerData()
        {
            AD.DebugLogger.Log("DataManager", "UpdatePlayerData() -> PlayerData 갱신 작업 시작");
            AD.Managers.ServerM.GetAllData(update: true);
        }

        /// <summary>
        /// server data, local data를 비교하여 최신화
        /// _dic_PlayFabPlayerData.Count가 1인 경우 -> NickName data만 가지고 있는 경우 -> 케릭터 선택 전 이기 때문에 무시
        /// _dic_PlayFabPlayerData.Count가 2인 경우 -> NickName, Sex data 가지고 있기 때문에 게임 씬 진입 -> 기본 데이터 세팅
        /// 만약 데이터가 추가 된다면(10개 이상 추가되지 않는다고 가정) PlayerData.json을 통해 데이터를 추가하고 이 경우 서버에 데이터를 다시 세팅
        /// RefreshData() 후 데이터가 다를 경우 데이터 갱신
        /// </summary>
        public void UpdateData()
        {
            if (PlayFabPlayerData.Count == 1)
            {
                AD.Managers.ServerM.SetInProgress(false);
                return;
            }

            if (PlayFabPlayerData.Count == 2)
            {
                LocalPlayerData["NickName"] = PlayFabPlayerData["NickName"].Value;
                LocalPlayerData["Sex"] = PlayFabPlayerData["Sex"].Value;

                AD.Managers.ServerM.SetData(LocalPlayerData, getAllData: true, update: true);
                _ctsRefreshData = new CancellationTokenSource();
                RefreshDataAsync(_ctsRefreshData.Token).Forget();
                return;
            }

            if (PlayFabPlayerData.Count > 2 && LocalPlayerData.Count > PlayFabPlayerData.Count)
            {
                AD.Managers.ServerM.UpdateNewPlayerData();
                return;
            }

            SanitizeData();
        }

        /// <summary>
        /// 서버 데이터 갱신 작업이 완료될 때까지 대기 후 데이터 정리
        /// </summary>
        private async UniTask RefreshDataAsync(CancellationToken token)
        {
            while (AD.Managers.ServerM.IsInProgress && !token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            StopRefreshDataTask();
        }

        /// <summary>
        /// RefreshDataAsync 관련 작업을 중지하고 데이터 정리
        /// </summary>
        private void StopRefreshDataTask()
        {
            if (_ctsRefreshData != null)
            {
                _ctsRefreshData.Cancel();
                _ctsRefreshData.Dispose();
                _ctsRefreshData = null;
            }
            SanitizeData();
        }

        /// <summary>
        /// 서버와 로컬 데이터를 비교하여 최신화
        /// 충돌이 발생하면 서버에 데이터를 다시 설정
        /// </summary>
        private void SanitizeData()
        {
            AD.DebugLogger.Log("DataManager", "SanitizeData() -> local, server 비교하여 PlayerData 최신화");

            // 동기화: NickName, Sex, Tutorial
            LocalPlayerData["NickName"] = PlayFabPlayerData["NickName"].Value;
            LocalPlayerData["Sex"] = PlayFabPlayerData["Sex"].Value;
            LocalPlayerData["Tutorial"] = PlayFabPlayerData["Tutorial"].Value;

            // 충돌 비교
            CompareValues(int.Parse(LocalPlayerData["Gold"]), int.Parse(PlayFabPlayerData["Gold"].Value));
            CompareValues(float.Parse(LocalPlayerData["Power"]), float.Parse(PlayFabPlayerData["Power"].Value));
            CompareValues(float.Parse(LocalPlayerData["AttackSpeed"]), float.Parse(PlayFabPlayerData["AttackSpeed"].Value));
            CompareValues(float.Parse(LocalPlayerData["MoveSpeed"]), float.Parse(PlayFabPlayerData["MoveSpeed"].Value));
            CompareValues(LocalPlayerData["AllyMonsters"], PlayFabPlayerData["AllyMonsters"].Value.ToString());

            string googlePlayValue = PlayFabPlayerData["GooglePlay"].Value.ToString();
            int comparisonResult = CompareValues(LocalPlayerData["GooglePlay"], googlePlayValue);
            if (comparisonResult < 0 && !string.IsNullOrEmpty(googlePlayValue) && !string.Equals(googlePlayValue, "null"))
            {
                LocalPlayerData["GooglePlay"] = googlePlayValue;
            }

            if (IsConflict)
            {
                AD.Managers.ServerM.SetData(LocalPlayerData, getAllData: true, update: false);
            }
            else
            {
                AD.Managers.ServerM.SetInProgress(false);
            }
        }

        /// <summary>
        /// 두 값을 비교합니다.
        /// 값이 다르면 IsConflict 플래그를 true로 설정합니다.
        /// </summary>
        /// <typeparam name="T">비교 가능한 타입</typeparam>
        /// <param name="value1">첫 번째 값</param>
        /// <param name="value2">두 번째 값</param>
        /// <returns>비교 결과</returns>
        private int CompareValues<T>(T value1, T value2) where T : System.IComparable
        {
            int result = value1.CompareTo(value2);

            if (result != 0)
            {
                IsConflict = true;
            }
            return result;
        }

        #endregion

        private void OnDestroy()
        {
            _ctsLocalDataUpdate?.Cancel();
            _ctsLocalDataUpdate?.Dispose();
            _ctsRefreshData?.Cancel();
            _ctsRefreshData?.Dispose();
        }
    }
}