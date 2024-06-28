using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Monster : BaseController
{
    [Header("--- 세팅 ---")]
    [SerializeField] AD.Define.Monsters _monster;
    [SerializeField] NavMeshAgent _navAgent;
    [SerializeField, Tooltip("통솔 오브젝트 - Monster 중 commander or player")] internal GameObject _go_commander = null;
    [SerializeField, Tooltip("Monster 통솔 오브젝트 여부")] internal bool isCommander = false;
    [SerializeField, Tooltip("통솔 오브젝트 멈충 상태")] internal bool isCommanderStop = false;
    [SerializeField, Tooltip("통솔 오브젝트 위임 및 다른 monster 통제")] internal List<Monster> _list_groupMonsters = new List<Monster>();
    [SerializeField, Tooltip("포획된 몬스터인지 여부")] bool isAlly = false;
    [SerializeField, Tooltip("포획 가능한 몬스터인지 여부")] bool isAbleAlly = false;
    [SerializeField, Tooltip("player 및 monster 감지 범위")] float detectionRadius = 10.0f;
    [SerializeField, Tooltip("감지 여부")] bool isDetection = false;
    [Tooltip("감지 layer")] int detectionLayer = 0;
    [Tooltip("감지한 오브젝트의 position")] Vector3 _vec_detection = Vector3.zero;
    [Tooltip("player 및 monster 감지를 위한 Coroutine")] Coroutine _co_detection = null;
    [Tooltip("MonsgerCommander의 random 이동 최대 반경")] float moveRadius = 5.0f;
    private float updateTime = 4f;
    private float updateTimer = 0f;
    private bool isDie = false;

    [Header("--- 테스트 ---")]
    public Transform _player = null;

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
        if (!isDie)
            MonsterAI();
    }

    #region Functions
    /// <summary>
    /// monster 초기화
    /// _go_commander, _list_groupMonsters의 경우 추후 MonsterGenerator.cs에서 관리
    /// BaseController에 존재하는 공용 data의 경우 base.Init()에서 초기화 진행 -> Player.cs 수정 필요
    /// </summary>
    protected override void Init()
    {
        base.Init();

        if (isAlly)
            AllySetting();
        else
        {
            detectionLayer = 10;

            int temp_probability = isCommander ? 50 : 10;
            isAbleAlly = Random.Range(0, 100) < temp_probability;
        }

        _co_detection = StartCoroutine(Detection());
    }

    public override void Clear()
    {
        if (isAlly)
        {
            isAlly = false;
            gameObject.tag = "Monster";
            gameObject.layer = 11;
        }

        updateTimer = 0f;
        _list_groupMonsters.Clear();
        isDie = false;

        if (_co_detection != null)
        {
            StopCoroutine(_co_detection);
            _co_detection = null;
        }
    }

    private void MonsterAI()
    {
        if (isDetection)
        {
            AD.Debug.Log("monster", "catch player");
        }
        else
        {
            if (isCommander)
            {
                updateTimer += Time.deltaTime;

                if (updateTimer >= updateTime)
                {
                    CommanderMove();
                    updateTimer = 0;
                }

                CheckCommanderStatus();
            }

            if (!isCommander)
            {
                FollowerMove();
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

    private void CommanderMove()
    {
        //if (Vector3.Distance(_player.position, transform.position) > 15f)
        //{
        //    _navAgent.SetDestination(_player.position);
        //    return;
        //}

        Vector3 temp_randomDirection = Random.insideUnitSphere * moveRadius;
        temp_randomDirection += transform.position;

        NavMeshHit hit;
        Vector3 finalPosition = Vector3.zero;
        if (NavMesh.SamplePosition(temp_randomDirection, out hit, 5f, 1))
            finalPosition = hit.position;

        _navAgent.SetDestination(finalPosition);

        foreach (Monster monster in _list_groupMonsters)
        {
            if (monster.gameObject != this.gameObject)
            {
                monster.isCommanderStop = false;
            }
        }
    }

    private void CheckCommanderStatus()
    {
        if (_navAgent.remainingDistance <= _navAgent.stoppingDistance && _navAgent.velocity.sqrMagnitude == 0f)
        {
            foreach (Monster monster in _list_groupMonsters)
            {
                if (monster.gameObject != this.gameObject)
                {
                    monster.isCommanderStop = true;
                }
            }
        }
    }

    private void FollowerMove()
    {
        _navAgent.SetDestination(_go_commander.transform.position);
    }

    private void Die()
    {
        if (isAlly)
            gameObject.SetActive(false);

        isDie = true;

        gameObject.layer = 12;

        if (isAbleAlly)
        {

        }

        StartCoroutine(AfterDie());
    }

    IEnumerator AfterDie()
    {
        while (true)
        {
            if (!isDetection)
            {
                gameObject.SetActive(false);
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// ally 세팅
    /// Init 시
    /// 포획 버튼 클릭 시 
    /// </summary>
    public void AllySetting()
    {
        gameObject.tag = "AllyMonster";
        gameObject.layer = 10;
        detectionLayer = 11;
        _go_commander = Player.Instance.gameObject;
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (!isCommander)
        {
            if (col.gameObject == _go_commander)
                _navAgent.isStopped = true;
        }
    }

    private void OnTriggerStay(Collider col)
    {
        if (!isCommander)
        {
            if (isCommanderStop)
            {
                if (col.gameObject.layer == gameObject.layer && Vector3.Distance(transform.position, _go_commander.transform.position) < 3f)
                {
                    _navAgent.isStopped = true;
                }
            }
            else
                _navAgent.isStopped = false;
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (!isCommander && col.gameObject == _go_commander)
        {
            _navAgent.isStopped = false;
        }
    }
}
