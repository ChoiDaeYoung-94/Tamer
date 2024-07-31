#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Monster : BaseController
{
    [Header("--- 세팅 ---")]
    [SerializeField] internal AD.Define.Creature _monster;
    [SerializeField] internal NavMeshAgent _navAgent;
    [SerializeField, Tooltip("포획 가능할 시 생기는 effect")] GameObject _go_capture = null;

    [Header("--- 참고 ---")]
    [SerializeField, Tooltip("Commander Monster 여부")] internal bool isCommander = false;
    [SerializeField, Tooltip("Commander Monster가 아닐 경우")] internal Monster _commanderMonster = null;
    [SerializeField, Tooltip("Boss Monster 여부")] internal bool isBoss = false;
    [SerializeField, Tooltip("Commander Monster의 random 이동 최대 반경")] float moveRadius = 5.0f;
    [SerializeField, Tooltip("통솔 오브젝트 위임 및 다른 monster 통제")] internal List<Monster> _list_groupMonsters = new List<Monster>();
    [SerializeField, Tooltip("군집이동 반경 기본 2f, monster 크기에 따라 변경 됨")] internal float flockingRadius = 2f;
    [SerializeField, Tooltip("포획 가능한 몬스터인지 여부")] bool isAbleAlly = false;
    [SerializeField, Tooltip("포획된 몬스터인지 여부")] bool isAlly = false;
    [Tooltip("player 및 monster 감지를 위한 Coroutine")] Coroutine _co_detection = null;
    [Tooltip("죽은 뒤 pool로 돌아가기 위한 Coroutine")] Coroutine _co_afterDie = null;
    [SerializeField, Tooltip("player 및 monster 감지 범위")] float detectionRadius = 10.0f;
    [SerializeField, Tooltip("감지 여부")] bool isDetection = false;
    [SerializeField, Tooltip("감지 layer")] int detectionLayer = 0;
    [SerializeField, Tooltip("감지한 오브젝트의 position")] Vector3 _vec_detection = Vector3.zero;
    [SerializeField] private bool isDie = false;
    private float updateTime = 4f;
    private float updateTimer = 0f;
    [SerializeField, Tooltip("죽을 시 플레이어 보상 골드")] int gold = 0;

    private void Start()
    {
        base.Init(_monster);
        _navAgent.enabled = true;
    }

    private void OnEnable()
    {
        Init(_monster);
    }

    private void OnDisable()
    {
        Clear();
    }

    private void Update()
    {
        if (!isDie)
            MonsterAI();
    }

    #region Functions
    /// <summary>
    /// monster 초기화
    /// </summary>
    protected override void Init(AD.Define.Creature creature)
    {
        _hp = _orgHp;

        if (!isAlly)
        {
            BaseSetting();
            GoldSetting();
        }

        if (isCommander || isAlly)
            _co_detection = StartCoroutine(Detection());
    }

    public override void Clear()
    {
        if (isAlly)
        {
            isAlly = false;
            gameObject.tag = "Monster";
            gameObject.layer = LayerMask.NameToLayer("Enemy");
        }

        updateTimer = 0f;
        _list_groupMonsters.Clear();
        isDie = false;
        _go_capture.SetActive(false);

        DisableCoroutine();
    }

    #region MonsterAI
    private void MonsterAI()
    {
        if (isDetection)
        {
            AD.Debug.Log("monster", "catch player");

            return;
        }

        if (isCommander)
        {
            updateTimer += Time.deltaTime;

            if (updateTimer >= updateTime)
            {
                CommanderMove();
                updateTimer = 0;
            }
        }
    }

    /// <summary>
    /// Player or monster가 범위내에 있는지 검출
    /// </summary>
    IEnumerator Detection()
    {
        while (true)
        {
            Collider[] col = Physics.OverlapSphere(transform.position, detectionRadius, 1 << detectionLayer);

            if (col.Length > 0)
            {
                isDetection = true;
                _vec_detection = col[0].transform.position;
            }
            else
                isDetection = false;

            yield return null;
        }
    }

    /// <summary>
    /// commander가 플레이어와 특정 거리 이상 멀어질 경우 가까워지도록 하고
    /// commander를 이동시킨 후 나머지 구성원들이 군집형태로 이동
    /// 한 행에 들어갈 수 있는 몬스터의 수는 한정적
    /// 볼링 핀 처럼 1, 2, 3, 4 이런식으로 쭉 나열하지만 
    /// 일단 몬스터 최대 객체수는 4로 지정
    /// </summary>
    private void CommanderMove()
    {
        Vector3 temp_randomDirection = Vector3.zero;

        float distance = Vector3.Distance(Player.Instance.transform.position, transform.position);

        if (distance > 20f && !isBoss)
        {
            Warp();
            return;
        }
        else
        {
            temp_randomDirection = Random.insideUnitSphere * moveRadius;
            temp_randomDirection += transform.position;
            temp_randomDirection.y = transform.position.y;
        }

        NavMeshHit hit;
        Vector3 finalPosition = Vector3.zero;
        if (NavMesh.SamplePosition(temp_randomDirection, out hit, 5f, 1))
        {
            finalPosition = hit.position;
            _navAgent.SetDestination(finalPosition);
        }

        GroupMonsterMove(finalPosition);
    }

    private void GroupMonsterMove(Vector3 commanderDestination)
    {
        int row = 2;
        int countInRow = 0;
        int listCount = _list_groupMonsters.Count;
        Vector3 startRowPosition = commanderDestination + Vector3.forward * flockingRadius * -1f;

        for (int i = -1; ++i < listCount;)
        {
            if (countInRow >= row)
            {
                row++;
                countInRow = 0;

                // -z 방향으로 군집
                startRowPosition += (Vector3.forward * flockingRadius * -1f);
            }

            // 현재 행에서 정렬해야 하는 남은 몬스터의 수를 반환하고
            // 그 수가 현재 행에서 정렬할 수 있는 수 보다 많을 시 현재 행에서 정렬할 수 있는 몬스터 수를 반환 하도록
            // 첫 번째 행은 commander이므로 2번 째 행 부터 시작하고 list에는 commander를 포함하지 않기 때문에 1을 더해줘서 계산
            int plusrow = AD.Utils.Plus(row, 0);
            int curRow = listCount + 1 - plusrow;
            int maxCountInRow = curRow > row ? row : curRow;

            // 각 행에서 객체수에 따라 x축에 간격을 두어 군집
            Vector3 positionOffset = Vector3.right * (countInRow - (maxCountInRow - 1) / 2.0f) * flockingRadius;
            Vector3 position = startRowPosition + positionOffset;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 5f, 1))
                _list_groupMonsters[i]._navAgent.SetDestination(hit.position);

            countInRow++;
        }
    }
    private void Warp()
    {
        Vector3 directionToPlayer = (Player.Instance.transform.position - transform.position).normalized;

        _navAgent.Warp(transform.position + directionToPlayer * 5);

        foreach (Monster monster in _list_groupMonsters)
            monster._navAgent.Warp(transform.position + directionToPlayer * 5);
    }

    #endregion

    internal void GetDamage(float damage)
    {
        if (Hp <= 0)
            return;

        Hp -= damage;

        if (Hp <= 0)
            Die();
    }

    private void Die()
    {
        if (isAlly)
        {
            Player.Instance.RemoveAllyMonster(this);
            AD.Managers.PoolM.PushToPool(gameObject);

            return;
        }

        if (isBoss)
            MonsterGenerator.Instance._go_boss = null;
        else if (isCommander)
        {
            isCommander = false;

            if (_list_groupMonsters.Count > 0)
                _list_groupMonsters[0].DelegateCommander(_list_groupMonsters);
        }
        else
            _commanderMonster.UpdateMonsterList(this);

        isDie = true;

        gameObject.layer = LayerMask.NameToLayer("Die");

        Player.Instance.NotifyPlayerOfDeath(target: gameObject, gold: gold);
    }

    internal void DelegateCommander(List<Monster> list_monster)
    {
        isCommander = true;
        _co_detection = StartCoroutine(Detection());

        for (int i = -1; ++i < list_monster.Count;)
        {
            Monster monster = list_monster[i];
            if (monster != this)
            {
                monster._commanderMonster = this;
                _list_groupMonsters.Add(monster);
            }
        }
    }

    internal void UpdateMonsterList(Monster monster)
    {
        _list_groupMonsters.Remove(monster);
    }

    /// <summary>
    /// Die ani 호출
    /// </summary>
    private void AfterDie()
    {
        if (isAbleAlly)
        {
            _go_capture.SetActive(true);
            _co_afterDie = StartCoroutine(Co_AfterDie());
        }
        else
            AD.Managers.PoolM.PushToPool(gameObject);
    }

    IEnumerator Co_AfterDie()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);

            float distance = Vector3.Distance(Player.Instance.transform.position, transform.position);
            if (distance > 10f)
                AD.Managers.PoolM.PushToPool(gameObject);
        }
    }
    #endregion

    #region Setting
    private void GoldSetting()
    {
        Dictionary<string, object> dic_temp = AD.Managers.DataM._dic_monsters[_monster.ToString()] as Dictionary<string, object>;

        int temp_gold = int.Parse(dic_temp["Gold"].ToString());
        gold = temp_gold + Random.Range(-temp_gold, temp_gold + 1);
    }

    /// <summary>
    /// 포획 시 ally 세팅
    /// </summary>
    internal void AllySetting(Vector3 playerPosition, bool setting = false)
    {
        if (_co_afterDie != null)
        {
            StopCoroutine(_co_afterDie);
            _co_afterDie = null;
        }
        _go_capture.SetActive(false);

        gameObject.tag = "AllyMonster";
        gameObject.layer = LayerMask.NameToLayer("Ally");
        detectionLayer = LayerMask.NameToLayer("Enemy");

        _hp = _orgHp;

        if (setting)
            transform.position = playerPosition + Random.insideUnitSphere * Random.Range(5f, 10f);

        isAlly = true;
        isDie = false;
    }

    private void BaseSetting()
    {
        detectionLayer = LayerMask.NameToLayer("Ally");

        int temp_probability = isCommander ? 40 : 20;
        if (isBoss)
            isAbleAlly = Random.Range(0, 100) < 5;
        else
            isAbleAlly = Random.Range(0, 100) < temp_probability;
    }
    #endregion

    #region Control
    private void DisableCoroutine()
    {
        if (_co_detection != null)
        {
            StopCoroutine(_co_detection);
            _co_detection = null;
        }

        if (_co_afterDie != null)
        {
            StopCoroutine(_co_afterDie);
            _co_afterDie = null;
        }
    }
    #endregion

    #endregion

    private void OnTriggerEnter(Collider col)
    {

    }

    private void OnTriggerStay(Collider col)
    {

    }

    private void OnTriggerExit(Collider col)
    {

    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Monster))]
    public class customEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Monster " +
                "\n Tag, Layer를 통해 Ally, Enemy로 구분" +
                "\n 군집이동은 commander monster를 통해 이동하며 최대 몬스터 객체 수는 4", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}
