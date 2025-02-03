using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterGenerator : MonoBehaviour
{
    static MonsterGenerator instance;
    public static MonsterGenerator Instance { get { return instance; } }

    [Header("--- 참고용 ---")]
    private Coroutine _co_settingMonster = null;
    [SerializeField, Tooltip("최대 몬스터 객체 수")] int maxMonsters = 15;
    [SerializeField, Tooltip("보스 몬스터 관리")] internal GameObject _go_boss = null;
    [SerializeField, Tooltip("현재 사용중인 몬스터 배열")] List<Monster> _list_curMonsters = new List<Monster>();
    [SerializeField, Tooltip("몬스터 생성 범위")] float spawnRadius = 12f;
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
        SettingMonsters(curRegionOfPlayer);
        _co_settingMonster = StartCoroutine(Generator());
    }

    IEnumerator Generator()
    {
        while (true)
        {
            yield return new WaitForSeconds(15f);

            int region = RegionOfPlayer();

            SetBoss(region);

            if (curRegionOfPlayer != region)
                curRegionOfPlayer = region;

            ResetMonster();

            if (_list_curMonsters.Count <= 8)
                SettingMonsters(curRegionOfPlayer);
        }
    }

    #region set monsters
    /// <summary>
    /// 몬스터는 최대 15마리 세팅
    /// 플레이어 주위에는 5개의 그룹이 존재하며 각 그룹의 최대 객체수는 3
    /// </summary>
    /// <param name="region"></param>
    private void SettingMonsters(int region)
    {
        if (_list_curMonsters.Count + 3 > maxMonsters)
            return;

        if (_dic_numOfMonster.TryGetValue(region, out (int min, int max) num))
        {
            int temp_groupMaxCount = (maxMonsters - _list_curMonsters.Count) / 3;
            int groupMaxCount = temp_groupMaxCount > 0 ? temp_groupMaxCount : 1;

            for (int j = -1; ++j < groupMaxCount;)
            {
                int groupSize = UnityEngine.Random.Range(1, 4);

                int temp_random = UnityEngine.Random.Range(num.min, num.max + 1);
                string temp_name = Enum.GetValues(typeof(AD.Define.Creature)).GetValue(temp_random).ToString();
                Monster commanderMonster = AD.Managers.PoolM.PopFromPool(temp_name).GetComponent<Monster>();
                commanderMonster.isCommander = true;
                commanderMonster.StartDetectionCoroutine();
                commanderMonster.transform.position = SetPosition();

                PlusMonster(commanderMonster);

                for (int i = 0; ++i < groupSize;)
                {
                    GameObject go_monster = AD.Managers.PoolM.PopFromPool(temp_name);
                    Monster monster = go_monster.GetComponent<Monster>();

                    Vector3 temp_vec = i % 2 == 0 ? new Vector3(i / 2, 0, 0) : new Vector3(0, 0, i);
                    go_monster.transform.position = commanderMonster.transform.position + temp_vec;

                    commanderMonster._list_groupMonsters.Add(monster);
                    monster._commanderMonster = commanderMonster;

                    PlusMonster(monster);
                }
            }
        }
        else
            AD.DebugLogger.LogError("MonsterGenerator", "Invalid region number");
    }

    private void SetBoss(int region)
    {
        if (region != 4 && _go_boss)
        {
            AD.Managers.PoolM.PushToPool(_go_boss);
            _go_boss = null;
        }

        if (region == 4 && !_go_boss && UnityEngine.Random.Range(0, 100) < 5f)
        {
            _go_boss = AD.Managers.PoolM.PopFromPool("FylingDemon");

            Monster boss = _go_boss.GetComponent<Monster>();
            boss.isCommander = true;
            boss.StartDetectionCoroutine();

            _go_boss.transform.position = new Vector3(-40f, 2f, 20f);
        }
    }

    private void PlusMonster(Monster monster) => _list_curMonsters.Add(monster);

    internal void MinusMonster(Monster monster) => _list_curMonsters.Remove(monster);
    #endregion

    #region position
    private Vector3 SetPosition()
    {
        float x = 0, z = 0;
        if (UnityEngine.Random.value > 0.5f)
        {
            x = UnityEngine.Random.value < 0.5f ? -1.0f : 1.0f;
            z = UnityEngine.Random.value;
        }
        else
        {
            x = UnityEngine.Random.value;
            z = UnityEngine.Random.value < 0.5f ? -1.0f : 1.0f;
        }

        Vector3 temp_vec = Player.Instance.transform.position + new Vector3(x, 0, z) * spawnRadius;

        return new Vector3(Mathf.Clamp(temp_vec.x, -50f, 55.5f), 2f, Mathf.Clamp(temp_vec.z, -25f, 20f));
    }

    private void ResetMonster()
    {
        for (int i = -1; ++i < _list_curMonsters.Count;)
        {
            Monster monster = _list_curMonsters[i];

            if (!monster.isCommander)
                continue;

            if (!CheckViewPort(monster.transform.position))
            {
                if (!RegionOfMonster(monster))
                {
                    foreach (Monster follower in monster._list_groupMonsters)
                    {
                        follower.BackPool();
                        --i;
                    }

                    monster.BackPool();
                    --i;
                }
                else
                    monster.Warp();
            }

            if (i < 0)
                i = 0;
        }
    }

    bool CheckViewPort(Vector3 pos)
    {
        bool include = false;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(pos);

        float x = Screen.width * 0.3f;
        float y = Screen.height * 0.3f;

        if (screenPos.x < 0 - x || screenPos.x > Screen.width + x || screenPos.y < 0 - y || screenPos.y > Screen.height + y)
            include = false;
        else if (screenPos.x >= 0 && screenPos.x <= Screen.width && screenPos.y >= 0 && screenPos.y <= Screen.height)
            include = true;

        return include;
    }

    bool RegionOfMonster(Monster monster)
    {
        int value = (int)monster._creature;

        if (_dic_numOfMonster.TryGetValue(curRegionOfPlayer, out (int min, int max) num))
            return value >= num.min && value <= num.max;

        return false;
    }

    private int RegionOfPlayer()
    {
        Vector3 vec_player = Player.Instance.transform.position;
        float x = vec_player.x, z = vec_player.z;

        bool inNorthHalf = z >= 0f && z <= 45f;
        bool inSouthHalf = z >= -45f && z <= 0f;
        bool inEastHalf = x >= 0 && x <= 65;
        bool inWestHalf = x <= 0 && x >= -65;

        if (inEastHalf && inSouthHalf)
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

    #endregion
}
