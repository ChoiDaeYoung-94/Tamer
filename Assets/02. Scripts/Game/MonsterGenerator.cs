using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterGenerator : MonoBehaviour
{
    static MonsterGenerator instance;
    public static MonsterGenerator Instance { get { return instance; } }

    [Header("--- 세팅 ---")]
    [SerializeField] GameObject _go_boss = null;

    [Header("--- 참고용 ---")]
    private Coroutine _co_settingMonster = null;
    [SerializeField, Tooltip("최대 몬스터 객체 수")] int maxMonsters = 16;
    [SerializeField, Tooltip("현재 몬스터 객체 수")] int curMonsters = 0;
    [SerializeField, Tooltip("현재 사용중인 몬스터 배열")] GameObject[] _go_curMonsters = null;
    [SerializeField, Tooltip("현재 플레이어의 지역 위치")] int curRegionOfPlayer = 1;
    [SerializeField, Tooltip("현재 플레이어의 지역에 따른 생성 가능한 몬스터(Define.Creature)")]
    Dictionary<int, (int min, int max)> _dic_numOfMonster = new Dictionary<int, (int min, int max)>()
    {
        { 1, (1, 3) },
        { 2, (3, 5) },
        { 3, (6, 8) },
        { 4, (9, 10) }
    };

    private void Awake()
    {
        instance = this;
    }

    private void OnDisable()
    {
        if (_co_settingMonster != null)
        {
            StopCoroutine(_co_settingMonster);
            _co_settingMonster = null;
        }
    }

    private void OnDestroy()
    {
        instance = null;
    }

    #region Functions
    /// <summary>
    /// InitializeGame.cs에서 호출
    /// </summary>
    internal void Init()
    {
        _go_curMonsters = new GameObject[maxMonsters];
        SettingMonsters(curRegionOfPlayer);
        _co_settingMonster = StartCoroutine(Generator());
    }

    IEnumerator Generator()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);

            int region = RegionOfPlayer();
            if (curRegionOfPlayer != region)
            {
                curRegionOfPlayer = region;
                ResetMonsters();
            }
            else if (curMonsters <= 8)
                SettingMonsters(curRegionOfPlayer);
        }
    }

    private void ResetMonsters()
    {
        foreach (GameObject go in _go_curMonsters)
            AD.Managers.PoolM.PushToPool(go);

        SettingMonsters(curRegionOfPlayer);
    }

    /// <summary>
    /// 몬스터는 최대 16마리 세팅
    /// 플레이어 주위에는 4개의 그룹이 존재하며 각 그룹의 최대 객체수는 4
    /// </summary>
    /// <param name="region"></param>
    private void SettingMonsters(int region)
    {
        if (curMonsters + 4 > maxMonsters)
            return;

        if (_dic_numOfMonster.TryGetValue(region, out (int min, int max) num))
        {
            int temp_groupMaxCount = (maxMonsters - curMonsters) / 4;
            int groupMaxCount = temp_groupMaxCount > 0 ? temp_groupMaxCount : 1;

            for (int j = -1; ++j < groupMaxCount;)
            {
                int groupSize = UnityEngine.Random.Range(2, 4);

                int temp_random = UnityEngine.Random.Range(num.min, num.max);
                string temp_name = Enum.GetValues(typeof(AD.Define.Creature)).GetValue(temp_random).ToString();
                Monster commanderMonster = AD.Managers.PoolM.PopFromPool(temp_name).GetComponent<Monster>();
                commanderMonster.isCommander = true;

                for (int i = 0; ++i < groupSize;)
                {
                    GameObject monster = AD.Managers.PoolM.PopFromPool(temp_name);
                    commanderMonster._list_groupMonsters.Add(monster.GetComponent<Monster>());
                }
            }
        }
        else
            AD.Debug.LogError("MonsterGenerator", "Invalid region number");
    }

    private int RegionOfPlayer()
    {
        Vector3 vec_player = Player.Instance.transform.position;
        float x = vec_player.x, z = vec_player.z;

        bool inNorthHalf = z >= 0f && z <= 45f;
        bool inSouthHalf = z >= -45f && z <= 0f;
        bool inEastHalf = x >= 0 && x <= 65;
        bool inWestHalf = x <= 0 && x >= -65;

        if      (inEastHalf && inSouthHalf)
            return 1;
        else if (inWestHalf && inSouthHalf)
            return 2;
        else if (inEastHalf && inNorthHalf)
            return 3;
        else if (inWestHalf && inNorthHalf)
            return 4;

        return 1;
    }
    #endregion
}
