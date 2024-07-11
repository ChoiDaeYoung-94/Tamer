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
    [SerializeField] AD.Define.Creature _monster;
    [SerializeField] internal NavMeshAgent _navAgent;

    [Header("--- 참고 ---")]
    [SerializeField, Tooltip("Commander Monster 여부")] internal bool isCommander = false;
    [SerializeField, Tooltip("Boss Monster 여부")] internal bool isBoss = false;
    [SerializeField, Tooltip("Commander Monster의 random 이동 최대 반경")] float moveRadius = 5.0f;
    [SerializeField, Tooltip("통솔 오브젝트 위임 및 다른 monster 통제")] internal List<Monster> _list_groupMonsters = new List<Monster>();
    [SerializeField, Tooltip("군집이동 반경 기본 2f, monster 크기에 따라 변경 됨")] internal float flockingRadius = 2f;
    [SerializeField, Tooltip("포획 가능한 몬스터인지 여부")] bool isAbleAlly = false;
    [SerializeField, Tooltip("포획된 몬스터인지 여부")] bool isAlly = false;
    [SerializeField, Tooltip("player 및 monster 감지를 위한 Coroutine")] Coroutine _co_detection = null;
    [SerializeField, Tooltip("player 및 monster 감지 범위")] float detectionRadius = 10.0f;
    [SerializeField, Tooltip("감지 여부")] bool isDetection = false;
    [SerializeField, Tooltip("감지 layer")] int detectionLayer = 0;
    [SerializeField, Tooltip("감지한 오브젝트의 position")] Vector3 _vec_detection = Vector3.zero;
    [SerializeField] private bool isDie = false;
    private float updateTime = 4f;
    private float updateTimer = 0f;

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

        if (isAlly)
            AllySetting();
        else
        {
            detectionLayer = 10;

            int temp_probability = isCommander ? 40 : 20;
            if (isBoss)
                isAbleAlly = Random.Range(0, 100) < 5;
            else
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

    private void CommanderMove()
    {
        Vector3 temp_randomDirection = Vector3.zero;

        float distance = Vector3.Distance(Player.Instance.transform.position, transform.position);

        if (distance > 20f && !isBoss)
        {
            Vector3 directionToPlayer = (Player.Instance.transform.position - transform.position).normalized;
            temp_randomDirection = Player.Instance.transform.position - directionToPlayer * 15f;
        }
        else
        {
            temp_randomDirection = Random.insideUnitSphere * moveRadius;
            temp_randomDirection.y = 0f;
            temp_randomDirection += transform.position;
        }

        NavMeshHit hit;
        Vector3 finalPosition = Vector3.zero;
        if (NavMesh.SamplePosition(temp_randomDirection, out hit, 5f, 1))
        {
            finalPosition = hit.position;
            _navAgent.SetDestination(finalPosition);
        }

        int row = 1;
        int countInRow = 0;
        int listCount = _list_groupMonsters.Count;
        Vector3 startRowPosition = finalPosition;

        for (int i = -1; ++i < listCount;)
        {
            if (countInRow >= row)
            {
                row++;
                countInRow = 0;
                startRowPosition += (Vector3.forward * flockingRadius * -1f);
            }

            int maxCountInRow = 0;
            int plusrow = AD.Utils.Plus(row, 0);
            int curRow = listCount - plusrow;
            if (curRow > row)
                maxCountInRow = row;
            else
                maxCountInRow = curRow;

            Vector3 positionOffset = Vector3.right * (countInRow - (maxCountInRow - 1) / 2.0f) * flockingRadius;
            Vector3 position = startRowPosition + positionOffset;

            if (NavMesh.SamplePosition(position, out hit, 5f, 1))
                _list_groupMonsters[i]._navAgent.SetDestination(hit.position);

            countInRow++;
        }
    }

    private void Die()
    {
        if (isAlly)
            gameObject.SetActive(false);

        if (isCommander)
        {
            isCommander = false;

        }

        if (isBoss)
        {
            MonsterGenerator.Instance._go_boss = null;
        }

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
        isAlly = true;
        gameObject.tag = "AllyMonster";
        gameObject.layer = 10;
        detectionLayer = 11;
    }
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
                "\n Layer를 통해 Ally, Enemy로 구분" +
                "\n 군집이동은 commander monster를 통해 이동하며 최대 몬스터 객체 수는 4", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}
