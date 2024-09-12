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
    [SerializeField] internal NavMeshAgent _navAgent;
    [SerializeField, Tooltip("포획 가능할 시 생기는 effect")] GameObject _go_capture = null;

    [Header("--- 참고 ---")]
    [SerializeField, Tooltip("Commander Monster 여부")] internal bool isCommander = false;
    [SerializeField, Tooltip("Commander Monster 전투 타겟")] GameObject _go_commanderTarget = null;
    private BaseController commanderTarget = null;
    [SerializeField, Tooltip("Commander Monster 목적지 도착 여부")] bool isCommanderArrived = false;
    [SerializeField, Tooltip("Commander Monster가 아닐 경우")] internal Monster _commanderMonster = null;
    [SerializeField, Tooltip("Follower Monster 전투 타겟")] GameObject _go_followerTarget = null;
    private BaseController followerTarget = null;
    [SerializeField, Tooltip("Boss Monster 여부")] bool isBoss = false;
    [SerializeField, Tooltip("Commander Monster의 random 이동 최대 반경")] float moveRadius = 5.0f;
    [SerializeField, Tooltip("통솔 오브젝트 위임 및 다른 monster 통제")] internal List<Monster> _list_groupMonsters = new List<Monster>();
    [SerializeField, Tooltip("군집이동 반경 기본 2f, monster 크기에 따라 변경 됨")] internal float flockingRadius = 2f;
    [SerializeField, Tooltip("포획 가능한 몬스터인지 여부")] bool isAbleAlly = false;
    [SerializeField, Tooltip("포획된 몬스터인지 여부")] bool isAlly = false;
    [SerializeField, Tooltip("포획된 몬스터의 전투 타겟")] GameObject _go_allyTarget = null;
    private BaseController allyTarget = null;
    private bool isTarget = false;
    [Tooltip("player 및 monster 감지를 위한 Coroutine")] Coroutine _co_detection = null;
    [Tooltip("죽은 뒤 pool로 돌아가기 위한 Coroutine")] Coroutine _co_afterDie = null;
    [SerializeField, Tooltip("player 및 monster 감지 범위")] float detectionRadius = 5.0f;
    [SerializeField, Tooltip("감지 여부")] bool isDetection = false;
    [SerializeField, Tooltip("감지 layer")] int detectionLayer = 0;
    [SerializeField, Tooltip("감지한 오브젝트의 position")] Vector3 _vec_detection = Vector3.zero;
    private float updateTime = 4f;
    private float updateTimer = 0f;
    [SerializeField, Tooltip("죽을 시 플레이어 보상 골드")] int gold = 0;

    protected override void Awake()
    {
        base.Awake();

        base.Init();
    }

    private void Start()
    {
        _navAgent.enabled = true;
    }

    private void OnEnable()
    {
        Init();
    }

    private void OnDisable()
    {
        Clear();
    }

    private void Update()
    {
        if (!isDie || !_navAgent.enabled)
            MonsterAI();
    }

    #region Functions
    /// <summary>
    /// monster 초기화
    /// </summary>
    protected override void Init()
    {
        _go_effectSpawn.SetActive(true);

        _hp = _orgHp;
        _navAgent.speed = _moveSpeed;

        if (!isAlly)
        {
            BaseSetting();
            GoldSetting();
        }

        StartBattleCoroutine();
    }

    public override void Clear()
    {
        DisableCoroutine();
        RemoveTarget();
        ResetMonster();
    }

    #region MonsterAI
    private void MonsterAI()
    {
        if (isDetection)
        {
            AfterDetection();

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

            CheckCommanderArrival();
        }
    }

    /// <summary>
    /// Player or monster가 범위내에 있는지 검출
    /// </summary>
    IEnumerator Detection()
    {
        while (!isDie)
        {
            float temp_detectionRadius = isAlly ? 5f : detectionRadius;
            Collider[] col = Physics.OverlapSphere(transform.position, temp_detectionRadius, 1 << detectionLayer);

            if (col.Length <= 0)
                isDetection = false;
            else
            {
                bool temp_detection = false;

                foreach (Collider detection in col)
                {
                    if (detection.gameObject.activeSelf)
                    {
                        temp_detection = true;
                        _vec_detection = detection.transform.position;
                        break;
                    }
                }

                isDetection = temp_detection;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void AfterDetection()
    {
        if (isCommander)
            CommanderDetectionMove(_vec_detection);
        else if (isAlly && !_go_allyTarget)
        {
            if (Vector3.Distance(transform.position, Player.Instance.transform.position) < 10f)
                BattleMove(this, _vec_detection);
        }
    }

    #region normal move
    /// <summary>
    /// commander가 플레이어와 특정 거리 이상 멀어질 경우 가까워지도록 하고
    /// commander를 이동시킨 후 나머지 구성원들이 군집형태로 이동
    /// 한 행에 들어갈 수 있는 몬스터의 수는 한정적
    /// 볼링 핀 처럼 1, 2, 3, 4 이런식으로 쭉 나열하지만 
    /// 일단 몬스터 최대 객체수는 4로 지정
    /// </summary>
    private void CommanderMove()
    {
        Vector3 targetDirection = GetRandomDirection(moveRadius, transform.position);
        float distance = Vector3.Distance(Player.Instance.transform.position, transform.position);

        while (true)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetDirection, out hit, 5f, 1))
            {
                _navAgent.SetDestination(hit.position);
                CrtState = CreatureState.Move;
                GroupMonsterMove(hit.position);

                break;
            }
        }
    }

    private void GroupMonsterMove(Vector3 commanderDestination)
    {
        int row = 2;
        int countInRow = 0;
        int listCount = _list_groupMonsters.Count;
        Vector3 startRowPosition = commanderDestination + Vector3.forward * flockingRadius * -1f;

        for (int i = -1; ++i < listCount;)
        {
            if (_list_groupMonsters[i].isDie)
                continue;

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

            while (true)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(position, out hit, 5f, 1))
                {
                    _list_groupMonsters[i]._navAgent.SetDestination(hit.position);
                    _list_groupMonsters[i].CrtState = CreatureState.Move;

                    break;
                }
            }

            countInRow++;
        }
    }

    internal void Warp()
    {
        Vector3 playerPos = Player.Instance.transform.position;
        float distance = Vector3.Distance(transform.position, playerPos);
        Vector3 randomDirection = GetRandomDirection(distance, playerPos);

        distance = Vector3.Distance(randomDirection, playerPos);
        Vector3 directionToPlayer = (playerPos - randomDirection).normalized;
        if (distance > 20f)
            randomDirection += directionToPlayer * 5f;
        else if (distance < 10f)
            randomDirection -= directionToPlayer * 5f;

        Vector3 finalPos = randomDirection;

        int failCount = 0;
        while (true)
        {
            failCount++;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(finalPos, out hit, 5f, 1))
            {
                _navAgent.Warp(hit.position);
                _go_effectSpawn.SetActive(true);

                WarpGroup();
                break;
            }
            else if (failCount > 20)
            {
                failCount = 0;
                break;
            }
        }
    }

    private void WarpGroup()
    {
        for (int i = -1; ++i < _list_groupMonsters.Count;)
        {
            if (_list_groupMonsters[i].isDie)
                continue;

            Vector3 temp_vec = (i + 1) % 2 == 0 ? new Vector3((i + 1) / 2, 0, 0) : new Vector3(0, 0, (i + 1));
            while (true)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position + temp_vec, out hit, 5f, 1))
                {
                    _list_groupMonsters[i]._navAgent.Warp(hit.position);
                    _list_groupMonsters[i]._go_effectSpawn.SetActive(true);
                    break;
                }
            }
        }
    }

    private Vector3 GetRandomDirection(float distance, Vector3 basePos)
    {
        Vector3 randomDirection = Random.insideUnitSphere * distance + basePos;
        randomDirection.y = transform.position.y;
        return randomDirection;
    }

    /// <summary>
    /// commander 이동 끝난 뒤 commander, groupMonsters ani -> Idle
    /// </summary>
    private void CheckCommanderArrival()
    {
        if (_navAgent.remainingDistance <= _navAgent.stoppingDistance)
        {
            if (!_navAgent.hasPath || _navAgent.velocity.sqrMagnitude == 0f)
                if (!isCommanderArrived)
                {
                    isCommanderArrived = true;

                    CrtState = CreatureState.Idle;
                    foreach (Monster monster in _list_groupMonsters)
                        monster.CrtState = CreatureState.Idle;
                }
        }
        else
            isCommanderArrived = false;
    }

    private void SafeSetDestination(Monster monster, Vector3 targetPosition)
    {
        int failCount = 0;
        float maxDistance = 5f;

        while (true)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPosition, out hit, maxDistance, 1))
            {
                monster._navAgent.SetDestination(hit.position);
                monster.CrtState = CreatureState.Move;

                break;
            }
            else
            {
                ++failCount;

                if (failCount > 10)
                    maxDistance = 10f;

                if (failCount > 30)
                    break;
            }
        }
    }
    #endregion

    #region detection move
    private void CommanderDetectionMove(Vector3 targetPosition)
    {
        if (!_go_commanderTarget)
            SafeSetDestination(this, targetPosition);

        foreach (Monster monster in _list_groupMonsters)
        {
            if (monster.isDie)
                continue;

            if (!monster._go_followerTarget)
                BattleMove(monster, targetPosition);
        }
    }

    private void BattleMove(Monster monster, Vector3 targetDestination)
    {
        SafeSetDestination(monster, targetDestination);
    }
    #endregion

    #region target
    private void SetTarget(GameObject go)
    {
        if (isTarget || !_navAgent.isOnNavMesh)
            return;

        isTarget = true;

        bool isNewTarget = SetNewTarget(go);

        if (!isNewTarget)
            return;

        AfterSetTarget();

        BaseController temp_bs = go.GetComponent<BaseController>();
        if (isCommander)
        {
            commanderTarget = temp_bs;

            foreach (Monster monster in _list_groupMonsters)
            {
                if (monster.isDie || monster._navAgent.isStopped)
                    continue;

                BattleMove(monster, go.transform.position);
            }
        }
        else if (!isCommander && !isAlly)
            followerTarget = temp_bs;
        else if (isAlly)
            allyTarget = temp_bs;
    }

    private bool SetNewTarget(GameObject go)
    {
        if (isCommander)
        {
            if (_go_commanderTarget == null || _go_commanderTarget != go)
            {
                _go_commanderTarget = go;
                return true;
            }
        }
        else if (!isCommander && !isAlly)
        {
            if (_go_followerTarget == null || _go_followerTarget != go)
            {
                _go_followerTarget = go;
                return true;
            }
        }
        else if (isAlly)
        {
            if (_go_allyTarget == null || _go_allyTarget != go)
            {
                _go_allyTarget = go;
                return true;
            }
        }

        return false;
    }

    private void AfterSetTarget()
    {
        if (!_navAgent.isStopped)
        {
            CrtState = CreatureState.Idle;
            _navAgent.isStopped = true;
        }
    }

    private void ResetTarget()
    {
        if (!isTarget || !_navAgent.isOnNavMesh)
            return;

        isTarget = false;

        RemoveTarget();

        CrtState = CreatureState.Idle;
        _navAgent.isStopped = false;
    }
    #endregion

    protected override IEnumerator Battle()
    {
        while (!isDie)
        {
            BaseController target = null;

            if (isCommander) target = commanderTarget;
            else if (isAlly) target = allyTarget;
            else target = followerTarget;

            if (target != null && (!target.gameObject.activeSelf || target.gameObject.layer == dieLayer))
                ResetTarget();
            else if (target != null)
            {
                Vector3 direction = target.transform.position - transform.position;
                direction.y = 0;
                transform.rotation = Quaternion.LookRotation(direction);

                CrtState = CreatureState.Attack;
                yield return new WaitForSeconds(1f / _attackSpeed);
            }
            else
                yield return null;
        }
    }

    protected override IEnumerator DistanceOfTarget()
    {
        while (!isDie)
        {
            Vector3 position = Vector3.zero;

            if (isCommander && _go_commanderTarget)
                position = _go_commanderTarget.transform.position;
            else if (!isCommander && _go_followerTarget)
                position = _go_followerTarget.transform.position;
            else if (isAlly && _go_allyTarget)
                position = _go_allyTarget.transform.position;

            float distance = Vector3.Distance(transform.position, position);
            if (distance > 3.0f)
                ResetTarget();

            yield return null;
        }
    }
    #endregion

    #region life cycle
    /// <summary>
    /// 몬스터 공격 애니메이션에서 진행
    /// </summary>
    protected override void AttackTarget()
    {
        if (isDie)
            return;

        BaseController target = null;
        if (isCommander && commanderTarget) target = commanderTarget;
        else if (!isCommander && followerTarget) target = followerTarget;
        else if (isAlly && allyTarget) target = allyTarget;

        target?.GetDamage(Power);
    }

    internal void DelegateCommander(List<Monster> list_monster)
    {
        isCommander = true;
        StartDetectionCoroutine();

        foreach (Monster monster in list_monster)
        {
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

        _navAgent.enabled = false;
        isDetection = false;
        Player.Instance.NotifyPlayerOfDeath(target: gameObject, gold: gold);
        MonsterGenerator.Instance.MinusMonster(this);

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
    private void BaseSetting()
    {
        detectionLayer = allyLayer;

        int temp_probability = isCommander ? 40 : 20;
        isAbleAlly = isBoss ? Random.Range(0, 100) < 5 : Random.Range(0, 100) < temp_probability;
    }

    private void GoldSetting()
    {
        Dictionary<string, object> dic_temp = AD.Managers.DataM._dic_monsters[_creature.ToString()] as Dictionary<string, object>;

        int temp_gold = int.Parse(dic_temp["Gold"].ToString());
        gold = temp_gold + Random.Range(-temp_gold, temp_gold + 1);
    }

    /// <summary>
    /// 포획 시 ally 세팅
    /// </summary>
    internal void AllySetting(Vector3 playerPosition, bool setting = false)
    {
        ResetMonster();
        RemoveTarget();

        if (_co_afterDie != null)
        {
            StopCoroutine(_co_afterDie);
            _co_afterDie = null;
        }

        gameObject.tag = "AllyMonster";
        gameObject.layer = allyLayer;
        detectionLayer = enemyLayer;

        _hp = _orgHp;
        _navAgent.speed = Player.Instance.MoveSpeed + 0.5f;

        isAlly = true;

        if (setting)
            transform.position = playerPosition + Random.insideUnitSphere * Random.Range(5f, 10f);

        CrtState = CreatureState.Idle;

        StartBattleCoroutine();
        StartDetectionCoroutine();
    }

    private void ResetMonster()
    {
        isAlly = false;
        gameObject.tag = "Monster";
        gameObject.layer = enemyLayer;

        _navAgent.enabled = true;
        _capsuleCollider.enabled = true;

        updateTimer = 0f;
        isCommanderArrived = false;

        _list_groupMonsters.Clear();

        _go_capture.SetActive(false);

        isDie = false;
    }

    private void RemoveTarget()
    {
        _go_commanderTarget = null;
        commanderTarget = null;

        _go_followerTarget = null;
        followerTarget = null;

        _go_allyTarget = null;
        allyTarget = null;
    }
    #endregion

    #region Control
    private void DisableCoroutine()
    {
        StopBattleCoroutine();

        if (_co_detection != null)
        {
            StopCoroutine(_co_detection);
            _co_detection = null;

            isDetection = false;
        }

        if (_co_afterDie != null)
        {
            StopCoroutine(_co_afterDie);
            _co_afterDie = null;
        }
    }

    internal void StartDetectionCoroutine() => _co_detection = StartCoroutine(Detection());
    #endregion

    #endregion

    private void OnTriggerStay(Collider col)
    {
        if (isDie)
            return;

        if (gameObject.CompareTag("Monster") && col.gameObject.layer == allyLayer ||
            gameObject.CompareTag("AllyMonster") && col.gameObject.layer == enemyLayer)
            SetTarget(col.gameObject);

        if (gameObject.CompareTag("Monster") && !col.gameObject.CompareTag("Monster") && col.gameObject.layer == dieLayer ||
            gameObject.CompareTag("AllyMonster") && !col.gameObject.CompareTag("AllyMonster") && col.gameObject.layer == dieLayer)
            ResetTarget();
    }

    private void OnTriggerExit(Collider col)
    {
        if (isDie)
            return;

        if (gameObject.CompareTag("Monster") && col.gameObject.layer == allyLayer ||
            gameObject.CompareTag("AllyMonster") && col.gameObject.layer == enemyLayer)
            ResetTarget();
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
