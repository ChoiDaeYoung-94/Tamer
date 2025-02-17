using System.Collections.Generic;
using System.Threading;

using UnityEngine;
using UnityEngine.AI;

using Cysharp.Threading.Tasks;

public class Monster : Creature
{
    [Header("--- Monster 데이터 ---")]
    public NavMeshAgent NavMeshAgent;
    [SerializeField] private GameObject _captureEffect;

    [Header("--- Monster 공용 데이터 ---")]
    public bool IsCommander = false;
    public Monster CommanderMonster;
    public List<Monster> MonsterGroupList = new List<Monster>();
    // 군집이동 반경 기본 2f, monster 크기에 따라 변경됨
    public float FlockingRadius = 2f;

    // 그 외 접근 불가 데이터
    private GameObject _commanderTarget;
    private Creature _commanderTargetCreature;
    private float _commanderMoveRadius = 5.0f;
    private bool _isCommanderArrived = false;
    private GameObject _followerTarget;
    private Creature _followerTargetCreature;
    private bool _isBoss = false;
    private bool _isAbleAlly = false;
    private bool _isAlly = false;
    private GameObject _allyTarget;
    private Creature _allyTargetCreature;
    private bool _isTarget = false;
    private float _detectionRadius = 5.0f;
    private bool _isDetection = false;
    private int _detectionLayer = 0;
    private Vector3 _detectionPosition = Vector3.zero;
    private float _updateTime = 4f;
    private float _updateTimer = 0f;
    private int _rewardGold = 0;
    private CancellationTokenSource _detectionTokenSource;
    private CancellationTokenSource _afterDieTokenSource;

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnEnable()
    {
        Init();
    }

    private void Start()
    {
        NavMeshAgent.enabled = true;
    }

    private void Update()
    {
        if (!isDie || !NavMeshAgent.enabled)
            MonsterAI();
    }

    private void OnDisable()
    {
        Clear();
    }

    #region Initialization and Cleanup

    /// <summary>
    /// Monster 초기화
    /// </summary>
    protected override void Init()
    {
        _spawnEffect.SetActive(true);

        _hp = _originalHp;
        NavMeshAgent.speed = _moveSpeed;

        if (!_isAlly)
        {
            BaseSetting();
            GoldSetting();
        }

        StartBattle();

        _detectionTokenSource = new CancellationTokenSource();
        _afterDieTokenSource = new CancellationTokenSource();
    }

    public override void Clear()
    {
        StopBattle();

        _detectionTokenSource?.Cancel();
        _detectionTokenSource?.Dispose();
        _detectionTokenSource = null;

        _afterDieTokenSource?.Cancel();
        _afterDieTokenSource?.Dispose();
        _afterDieTokenSource = null;

        _isDetection = false;

        RemoveTarget();
        ResetMonster();
    }

    #endregion

    #region Monster AI

    private void MonsterAI()
    {
        if (_isDetection)
        {
            AfterDetection();

            return;
        }

        if (IsCommander)
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer >= _updateTime)
            {
                CommanderMove();
                _updateTimer = 0f;
            }
            CheckCommanderArrival();
        }
    }

    /// <summary>
    /// Player or monster가 범위내에 있는지 검출
    /// </summary>
    private async UniTaskVoid DetectionLoop(CancellationToken token)
    {
        while (!isDie && !token.IsCancellationRequested)
        {
            float currentDetectionRadius = _isAlly ? 5f : _detectionRadius;
            Collider[] colliders = Physics.OverlapSphere(transform.position, currentDetectionRadius, 1 << _detectionLayer);

            bool detectionFound = false;
            if (colliders != null && colliders.Length > 0)
            {
                foreach (Collider collider in colliders)
                {
                    if (collider.gameObject.activeSelf)
                    {
                        detectionFound = true;
                        _detectionPosition = collider.transform.position;
                        break;
                    }
                }
            }
            _isDetection = detectionFound;

            await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: token);
        }
    }

    private void AfterDetection()
    {
        if (IsCommander)
        {
            CommanderDetectionMove(_detectionPosition);
        }
        else if (_isAlly && _allyTarget == null)
        {
            if (Vector3.Distance(transform.position, Player.Instance.transform.position) < 10f)
            {
                BattleMove(this, _detectionPosition);
            }
        }
    }

    #endregion

    #region Movement

    /// <summary>
    /// commander가 플레이어와 특정 거리 이상 멀어질 경우 가까워지도록 하고
    /// commander를 이동시킨 후 나머지 구성원들이 군집형태로 이동
    /// 한 행에 들어갈 수 있는 몬스터의 수는 한정적
    /// 볼링 핀 처럼 1, 2, 3, 4 이런식으로 쭉 나열하지만 
    /// 일단 몬스터 최대 객체수는 4로 지정
    /// </summary>
    private void CommanderMove()
    {
        Vector3 targetDirection = GetRandomDirection(_commanderMoveRadius, transform.position);
        const int maxAttempts = 10;
        int attempt = 0;
        while (attempt < maxAttempts)
        {
            if (NavMesh.SamplePosition(targetDirection, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                NavMeshAgent.SetDestination(hit.position);
                State = CreatureState.Move;
                GroupMonsterMove(hit.position);
                break;
            }
            targetDirection = GetRandomDirection(_commanderMoveRadius, transform.position);
            attempt++;
        }
    }

    private void GroupMonsterMove(Vector3 commanderDestination)
    {
        int row = 2;
        int countInRow = 0;
        int listCount = MonsterGroupList.Count;
        Vector3 startRowPosition = commanderDestination + Vector3.forward * FlockingRadius * -1f;

        for (int i = 0; i < listCount; i++)
        {
            if (MonsterGroupList[i].isDie)
                continue;

            if (countInRow >= row)
            {
                row++;
                countInRow = 0;

                // -z 방향으로 군집
                startRowPosition += (Vector3.forward * FlockingRadius * -1f);
            }

            // 현재 행에서 정렬해야 하는 남은 몬스터의 수를 반환하고
            // 그 수가 현재 행에서 정렬할 수 있는 수 보다 많을 시 현재 행에서 정렬할 수 있는 몬스터 수를 반환 하도록
            // 첫 번째 행은 commander이므로 2번 째 행 부터 시작하고 list에는 commander를 포함하지 않기 때문에 1을 더해줘서 계산
            int plusRow = AD.Utility.GetSortedMonsterCount(row, 0);
            int currentRowCount = listCount + 1 - plusRow;
            int maxCountInRow = currentRowCount > row ? row : currentRowCount;

            // 각 행에서 객체수에 따라 x축에 간격을 두어 군집
            Vector3 positionOffset = Vector3.right * (countInRow - (maxCountInRow - 1) / 2.0f) * FlockingRadius;
            Vector3 targetPosition = startRowPosition + positionOffset;

            const int maxAttempts = 10;
            int attempt = 0;
            while (attempt < maxAttempts)
            {
                if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    MonsterGroupList[i].NavMeshAgent.SetDestination(hit.position);
                    MonsterGroupList[i].State = CreatureState.Move;
                    break;
                }
                attempt++;
            }
            countInRow++;
        }
    }

    public void Warp()
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
        const int maxFailCount = 20;
        while (failCount < maxFailCount)
        {
            failCount++;
            if (NavMesh.SamplePosition(finalPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                NavMeshAgent.Warp(hit.position);
                _spawnEffect.SetActive(true);
                WarpGroup();
                break;
            }
        }
    }

    private void WarpGroup()
    {
        for (int i = 0; i < MonsterGroupList.Count; i++)
        {
            if (MonsterGroupList[i].isDie)
                continue;

            Vector3 offset = ((i + 1) % 2 == 0) ? new Vector3((i + 1) / 2f, 0, 0) : new Vector3(0, 0, (i + 1));
            int attempt = 0;
            const int maxAttempts = 10;
            while (attempt < maxAttempts)
            {
                if (NavMesh.SamplePosition(transform.position + offset, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    MonsterGroupList[i].NavMeshAgent.Warp(hit.position);
                    MonsterGroupList[i]._spawnEffect.SetActive(true);
                    break;
                }
                attempt++;
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
        if (NavMeshAgent.remainingDistance <= NavMeshAgent.stoppingDistance)
        {
            if (!NavMeshAgent.hasPath || NavMeshAgent.velocity.sqrMagnitude == 0f)
            {
                if (!_isCommanderArrived)
                {
                    _isCommanderArrived = true;
                    State = CreatureState.Idle;
                    foreach (Monster monster in MonsterGroupList)
                    {
                        monster.State = CreatureState.Idle;
                    }
                }
            }
        }
        else
        {
            _isCommanderArrived = false;
        }
    }

    private void SafeSetDestination(Monster monster, Vector3 targetPosition)
    {
        int failCount = 0;
        float maxDistance = 5f;
        const int maxAttempts = 30;
        while (failCount < maxAttempts)
        {
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                monster.NavMeshAgent.SetDestination(hit.position);
                monster.State = CreatureState.Move;
                break;
            }
            else
            {
                failCount++;
                if (failCount > 10)
                    maxDistance = 10f;
            }
        }
    }

    #endregion

    #region Detection Movement

    private void CommanderDetectionMove(Vector3 targetPosition)
    {
        if (_commanderTarget == null)
            SafeSetDestination(this, targetPosition);

        foreach (Monster monster in MonsterGroupList)
        {
            if (monster.isDie)
                continue;

            if (monster._followerTarget == null)
                BattleMove(monster, targetPosition);
        }
    }

    private void BattleMove(Monster monster, Vector3 targetDestination)
    {
        SafeSetDestination(monster, targetDestination);
    }

    #endregion

    #region Target Management

    private void SetTarget(GameObject go)
    {
        if (_isTarget || !NavMeshAgent.isOnNavMesh)
            return;

        _isTarget = true;
        bool isNewTarget = SetNewTarget(go);
        if (!isNewTarget)
            return;

        AfterSetTarget();

        Creature targetCreature = go.GetComponent<Creature>();
        if (IsCommander)
        {
            _commanderTargetCreature = targetCreature;
            foreach (Monster monster in MonsterGroupList)
            {
                if (monster.isDie || monster.NavMeshAgent.isStopped)
                    continue;
                BattleMove(monster, go.transform.position);
            }
        }
        else if (!IsCommander && !_isAlly)
        {
            _followerTargetCreature = targetCreature;
        }
        else if (_isAlly)
        {
            _allyTargetCreature = targetCreature;
        }
    }

    private bool SetNewTarget(GameObject go)
    {
        if (IsCommander)
        {
            if (_commanderTarget == null || _commanderTarget != go)
            {
                _commanderTarget = go;
                return true;
            }
        }
        else if (!IsCommander && !_isAlly)
        {
            if (_followerTarget == null || _followerTarget != go)
            {
                _followerTarget = go;
                return true;
            }
        }
        else if (_isAlly)
        {
            if (_allyTarget == null || _allyTarget != go)
            {
                _allyTarget = go;
                return true;
            }
        }
        return false;
    }

    private void AfterSetTarget()
    {
        if (!NavMeshAgent.isStopped)
        {
            State = CreatureState.Idle;
            NavMeshAgent.isStopped = true;
        }
    }

    private void ResetTarget()
    {
        if (!_isTarget || !NavMeshAgent.isOnNavMesh)
            return;

        _isTarget = false;
        RemoveTarget();
        State = CreatureState.Idle;
        NavMeshAgent.isStopped = false;
    }

    private void RemoveTarget()
    {
        _commanderTarget = null;
        _commanderTargetCreature = null;
        _followerTarget = null;
        _followerTargetCreature = null;
        _allyTarget = null;
        _allyTargetCreature = null;
    }

    #endregion

    #region Battle and Monitoring

    protected override async UniTask BattleLoop(CancellationToken token)
    {
        while (!isDie && !token.IsCancellationRequested)
        {
            Creature target = null;
            if (IsCommander)
                target = _commanderTargetCreature;
            else if (_isAlly)
                target = _allyTargetCreature;
            else
                target = _followerTargetCreature;

            if (target != null && (!target.gameObject.activeSelf || target.gameObject.layer == dieLayer))
            {
                ResetTarget();
            }
            else if (target != null)
            {
                Vector3 direction = target.transform.position - transform.position;
                direction.y = 0;
                transform.rotation = Quaternion.LookRotation(direction);
                State = CreatureState.Attack;
                await UniTask.Delay(System.TimeSpan.FromSeconds(1f / _attackSpeed), cancellationToken: token);
            }
            else
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
    }

    protected override async UniTask MonitorTargetDistance(CancellationToken token)
    {
        while (!isDie && !token.IsCancellationRequested)
        {
            Vector3 targetPosition = Vector3.zero;
            if (IsCommander && _commanderTarget != null)
                targetPosition = _commanderTarget.transform.position;
            else if (!IsCommander && _followerTarget != null)
                targetPosition = _followerTarget.transform.position;
            else if (_isAlly && _allyTarget != null)
                targetPosition = _allyTarget.transform.position;

            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > 3.0f)
                ResetTarget();

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    #endregion

    #region Life Cycle

    /// <summary>
    /// 몬스터 공격 애니메이션에서 진행
    /// </summary>
    protected override void AttackTarget()
    {
        if (isDie)
            return;

        Creature target = null;
        if (IsCommander && _commanderTarget != null)
            target = _commanderTargetCreature;
        else if (!IsCommander && _followerTarget != null)
            target = _followerTargetCreature;
        else if (_isAlly && _allyTarget != null)
            target = _allyTargetCreature;

        target?.GetDamage(Power);
    }

    public void DelegateCommander(List<Monster> monsters)
    {
        IsCommander = true;
        StartDetection();
        foreach (Monster monster in monsters)
        {
            if (monster != this)
            {
                monster.CommanderMonster = this;
                MonsterGroupList.Add(monster);
            }
        }
    }

    public void UpdateMonsterList(Monster monster)
    {
        MonsterGroupList.Remove(monster);
    }

    /// <summary>
    /// Die ani 호출
    /// </summary>
    private void AfterDie()
    {
        if (_isAlly)
        {
            Player.Instance.RemoveAllyMonster(this);
            AD.Managers.PoolM.PushToPool(gameObject);
            return;
        }

        if (_isBoss)
        {
            IsCommander = false;
            MonsterGenerator.Instance.BossMonster = null;
        }
        else if (IsCommander)
        {
            IsCommander = false;
            if (MonsterGroupList.Count > 0)
                MonsterGroupList[0].DelegateCommander(MonsterGroupList);
        }
        else
        {
            CommanderMonster?.UpdateMonsterList(this);
        }

        NavMeshAgent.enabled = false;
        _isDetection = false;
        Player.Instance.NotifyPlayerOfDeath(target: gameObject, gold: _rewardGold);
        MonsterGenerator.Instance.MinusMonster(this);

        if (_isAbleAlly)
        {
            _captureEffect.SetActive(true);
            AfterDieAsync(_afterDieTokenSource.Token).Forget();
        }
        else
        {
            AD.Managers.PoolM.PushToPool(gameObject);
        }
    }

    private async UniTask AfterDieAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(5f), cancellationToken: token);
            float distance = Vector3.Distance(Player.Instance.transform.position, transform.position);
            if (distance > 10f)
            {
                AD.Managers.PoolM.PushToPool(gameObject);
                break;
            }
        }
    }

    public void BackPool()
    {
        isDie = true;
        gameObject.layer = dieLayer;
        _capsuleCollider.enabled = false;

        if (_isBoss)
            MonsterGenerator.Instance.BossMonster = null;

        IsCommander = false;
        MonsterGroupList.Clear();

        NavMeshAgent.enabled = false;
        _isDetection = false;

        MonsterGenerator.Instance.MinusMonster(this);

        AD.Managers.PoolM.PushToPool(gameObject);
    }

    #endregion

    #region Settings

    public void StartDetection()
    {
        if (_detectionTokenSource != null)
            DetectionLoop(_detectionTokenSource.Token).Forget();
    }

    private void BaseSetting()
    {
        _detectionLayer = allyLayer;
        int probability = IsCommander ? 40 : 20;
        _isAbleAlly = _isBoss ? Random.Range(0, 100) < 5 : Random.Range(0, 100) < probability;
    }

    private void GoldSetting()
    {
        if (AD.Managers.DataM.MonsterData[CreatureType.ToString()] is Dictionary<string, object> monsterData)
        {
            int gold = int.Parse(monsterData["Gold"].ToString());
            _rewardGold = gold + Random.Range(-gold, gold + 1);
        }
    }

    /// <summary>
    /// 포획 시 ally 세팅
    /// </summary>
    public void AllySetting(Vector3 playerPosition, bool setting = false)
    {
        ResetMonster();
        RemoveTarget();

        _afterDieTokenSource?.Cancel();
        _afterDieTokenSource?.Dispose();
        _afterDieTokenSource = null;

        gameObject.tag = "AllyMonster";
        gameObject.layer = allyLayer;
        _detectionLayer = enemyLayer;

        _hp = _originalHp;
        SetSpeed();

        _isAlly = true;

        if (setting)
            transform.position = playerPosition + Random.insideUnitSphere * Random.Range(5f, 10f);

        State = CreatureState.Idle;
        StartBattle();
        StartDetection();
    }

    public void SetSpeed()
    {
        if (Player.Instance.IsBuffing)
            NavMeshAgent.speed = Player.Instance.BuffMoveSpeed + 0.5f;
        else
            NavMeshAgent.speed = Player.Instance.MoveSpeed + 0.5f;
    }

    private void ResetMonster()
    {
        _isAlly = false;
        gameObject.tag = "Monster";
        gameObject.layer = enemyLayer;

        NavMeshAgent.enabled = true;
        _capsuleCollider.enabled = true;

        _updateTimer = 0f;
        _isCommanderArrived = false;

        MonsterGroupList.Clear();

        _captureEffect.SetActive(false);

        isDie = false;
    }

    #endregion

    #region Trigger Events

    private void OnTriggerStay(Collider col)
    {
        if (isDie)
            return;

        if ((gameObject.CompareTag("Monster") && col.gameObject.layer == allyLayer) ||
            (gameObject.CompareTag("AllyMonster") && col.gameObject.layer == enemyLayer))
        {
            SetTarget(col.gameObject);
        }

        if ((gameObject.CompareTag("Monster") && !col.gameObject.CompareTag("Monster") && col.gameObject.layer == dieLayer) ||
            (gameObject.CompareTag("AllyMonster") && !col.gameObject.CompareTag("AllyMonster") && col.gameObject.layer == dieLayer))
        {
            ResetTarget();
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (isDie)
            return;

        if ((gameObject.CompareTag("Monster") && col.gameObject.layer == allyLayer) ||
            (gameObject.CompareTag("AllyMonster") && col.gameObject.layer == enemyLayer))
        {
            ResetTarget();
        }
    }

    #endregion

}
