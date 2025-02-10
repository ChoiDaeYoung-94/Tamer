using System;
using System.Collections.Generic;

using UnityEngine;

using Cysharp.Threading.Tasks;

public class MonsterGenerator : MonoBehaviour
{
    private static MonsterGenerator _instance;
    public static MonsterGenerator Instance { get { return _instance; } }

    public GameObject BossMonster = null;
    [SerializeField] private int _maxMonsters = 15;
    [SerializeField] private List<Monster> _activeMonsters = new List<Monster>();
    [SerializeField] private float _spawnRadius = 12f;
    [SerializeField] private int _currentRegion = 1;

    private readonly Dictionary<int, (int min, int max)> _monsterRegionMap = new Dictionary<int, (int min, int max)>
    {
        { 1, (1, 3) },
        { 2, (3, 5) },
        { 3, (6, 8) },
        { 4, (9, 10) }
    };

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        _instance = null;
    }

    #region Functions

    public void Init()
    {
        SpawnMonsters(_currentRegion);
        SpawnLoop().Forget();
    }

    private async UniTaskVoid SpawnLoop()
    {
        while (true)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(15f));

            int region = RegionOfPlayer();
            SetBoss(region);

            if (_currentRegion != region)
                _currentRegion = region;

            ResetMonsters();

            if (_activeMonsters.Count <= 8)
                SpawnMonsters(_currentRegion);
        }
    }

    #region set monsters
    /// <summary>
    /// 몬스터는 최대 15마리 세팅
    /// 플레이어 주위에는 5개의 그룹이 존재하며 각 그룹의 최대 객체수는 3
    /// </summary>
    private void SpawnMonsters(int region)
    {
        if (_activeMonsters.Count + 3 > _maxMonsters)
            return;

        if (_monsterRegionMap.TryGetValue(region, out (int min, int max) range))
        {
            int temp_groupMaxCount = (_maxMonsters - _activeMonsters.Count) / 3;
            int groupMaxCount = temp_groupMaxCount > 0 ? temp_groupMaxCount : 1;

            for (int j = 0; j < groupMaxCount; j++)
            {
                int groupSize = UnityEngine.Random.Range(1, 4);

                int temp_random = UnityEngine.Random.Range(range.min, range.max + 1);
                string temp_name = Enum.GetValues(typeof(AD.GameConstants.Creature)).GetValue(temp_random).ToString();
                Monster commanderMonster = AD.Managers.PoolM.PopFromPool(temp_name).GetComponent<Monster>();
                commanderMonster.isCommander = true;
                commanderMonster.StartDetectionCoroutine();
                commanderMonster.transform.position = SetPosition();

                PlusMonster(commanderMonster);

                for (int i = 0; i < groupSize; i++)
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
        if (region != 4 && BossMonster)
        {
            AD.Managers.PoolM.PushToPool(BossMonster);
            BossMonster = null;
        }

        if (region == 4 && !BossMonster && UnityEngine.Random.Range(0, 100) < 5f)
        {
            BossMonster = AD.Managers.PoolM.PopFromPool("FylingDemon");

            Monster boss = BossMonster.GetComponent<Monster>();
            boss.isCommander = true;
            boss.StartDetectionCoroutine();

            BossMonster.transform.position = new Vector3(-40f, 2f, 20f);
        }
    }

    private void PlusMonster(Monster monster) => _activeMonsters.Add(monster);

    public void MinusMonster(Monster monster) => _activeMonsters.Remove(monster);
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

        Vector3 temp_vec = Player.Instance.transform.position + new Vector3(x, 0, z) * _spawnRadius;

        return new Vector3(Mathf.Clamp(temp_vec.x, -50f, 55.5f), 2f, Mathf.Clamp(temp_vec.z, -25f, 20f));
    }

    private void ResetMonsters()
    {
        for (int i = 0; i < _activeMonsters.Count; i++)
        {
            Monster monster = _activeMonsters[i];

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

    private bool CheckViewPort(Vector3 pos)
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

        if (_monsterRegionMap.TryGetValue(_currentRegion, out (int min, int max) num))
            return value >= num.min && value <= num.max;

        return false;
    }

    private int RegionOfPlayer()
    {
        Vector3 pos = Player.Instance.transform.position;
        float x = pos.x, z = pos.z;

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
