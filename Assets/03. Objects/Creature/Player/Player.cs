using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : BaseController
{
    static Player instance;
    public static Player Instance { get { return instance; } }

    [Header("--- 플레어어 고유 Data ---")]
    [SerializeField] private int _gold = 0;
    public int Gold { get { return instance._gold; } }
    [SerializeField] private int _curCaptureCapacity = 0;
    public int CurCaptureCapacity { get { return instance._curCaptureCapacity; } }
    [SerializeField] private int _maxCaptureCapacity = 0;
    public int MaxCaptureCapacity { get { return instance._maxCaptureCapacity; } }
    [SerializeField, Tooltip("ally monster 통제")] List<Monster> _list_groupMonsters = new List<Monster>();

    [Header("--- 세팅 ---")]
    [SerializeField] internal GameObject _go_player = null;
    [SerializeField] internal Transform _tr_cameraArm = null;
    [SerializeField] internal GameObject _sword = null;
    [SerializeField] internal GameObject _shield = null;
    [SerializeField] private GameObject _buff = null;

    [Header("--- 플레이어 버프 시 적용되는 status ---")]
    [SerializeField] private bool isBuffing = false;
    [SerializeField] public float _buffPower = 0;
    [SerializeField] public float _buffAttackSpeed = 0;
    [SerializeField] public float _buffMoveSpeed = 0;

    [Header("--- 참고 ---")]
    [SerializeField] bool isAllyAvailable = false;
    [SerializeField, Tooltip("현재 타겟 몬스터")] GameObject _go_targetMonster = null;
    [SerializeField, Tooltip("현재 타겟 몬스터 cs")] Monster targetMonster = null;
    [Tooltip("플레이어 공격 감지 coroutine")] Coroutine _co_battle;
    [Tooltip("타겟 몬스터 거리 감지 coroutine")] Coroutine _co_distanceOfTarget;

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(transform.parent.gameObject);

        Init(AD.Define.Creature.Player);
    }

    /// <summary>
    /// 플레이어의 초기화가 끝난 후
    /// 플레이어와 관련된 요소들 초기화
    /// </summary>
    private void OnEnable()
    {
        JoyStick.Instance.StartInit();
        CameraManage.Instance.StartInit();
        PlayerUICanvas.Instance.StartInit();
    }

    private void OnDisable()
    {
        DisableCoroutine();
    }

    private void Update()
    {
        if (isAllyAvailable && CrtState == CreatureState.Move)
            AllyMove();
    }

    #region Functions
    /// <summary>
    /// HP는 장비에 맞게 따로 계산
    /// Player의 기본 HP는 100
    /// </summary>
    protected override void Init(AD.Define.Creature creture)
    {
        base.Init(creture);

        _gold = int.Parse(AD.Managers.DataM._dic_player["Gold"]);
        _curCaptureCapacity = int.Parse(AD.Managers.DataM._dic_player["CurCaptureCapacity"]);
        _maxCaptureCapacity = int.Parse(AD.Managers.DataM._dic_player["MaxCaptureCapacity"]);

        if (AD.Managers.DataM._dic_player["AllyMonsters"] != "null")
            isAllyAvailable = true;

        JoyStick.Instance.SetSpeed(_moveSpeed);

        AD.Managers.UpdateM._update -= TouchEvent;
        AD.Managers.UpdateM._update += TouchEvent;
    }

    /// <summary>
    /// 화면을 클릭하여 대응해야할 부분
    /// 버프, 몬스터 포획 등
    /// </summary>
    private void TouchEvent()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                string str_temp = hit.collider.tag;

                switch (str_temp)
                {
                    case "GoogleAdMob":
                        if (!AD.Managers.GoogleAdMobM.isInprogress)
                            AD.Managers.GoogleAdMobM.ShowRewardedAd();
                        break;
                    case "GoGameScene":
                        AD.Managers.GameM.SwitchMainOrGameScene(AD.Define.Scenes.Game);
                        break;
                }
            }
        }
    }

    internal void SetBuff()
    {
        isBuffing = true;

        _buffPower = _power * 1.3f;
        _buffAttackSpeed = _attackSpeed * 1.3f;
        _buffMoveSpeed = _moveSpeed * 2f;
        JoyStick.Instance.SetSpeed(_buffMoveSpeed);

        _buff.SetActive(true);
    }

    internal void EndBuff()
    {
        isBuffing = false;

        JoyStick.Instance.SetSpeed(_moveSpeed);

        _buff.SetActive(false);
    }

    private void AllyMove()
    {
        int row = 2;
        int countInRow = 0;
        int listCount = _list_groupMonsters.Count;
        Vector3 startRowPosition = transform.position + (-transform.forward * _list_groupMonsters[0].flockingRadius);

        for (int i = -1; ++i < listCount;)
        {
            if (countInRow >= row)
            {
                row++;
                countInRow = 0;
                startRowPosition += (-transform.forward * _list_groupMonsters[i].flockingRadius);
            }

            int plusrow = AD.Utils.Plus(row, 0);
            int curRow = listCount + 1 - plusrow;
            int maxCountInRow = curRow > row ? row : curRow;

            Vector3 positionOffset = transform.right * (countInRow - (maxCountInRow - 1) / 2.0f) * _list_groupMonsters[i].flockingRadius;
            Vector3 position = startRowPosition + positionOffset;

            _list_groupMonsters[i]._navAgent.SetDestination(position);

            countInRow++;
        }
    }

    /// <summary>
    /// monster가 죽은 뒤 호출
    /// </summary>
    /// <param name="target"></param>
    internal void CheckTarget(GameObject target)
    {
        if (target == _go_targetMonster)
            _go_targetMonster = null;
    }

    /// <summary>
    /// 플레이어 공격 애니메이션에서 진행
    /// </summary>
    private void AttackTarget()
    {
        if (_go_targetMonster != null)
        {
            float power = isBuffing ? _buffPower : Power;
            targetMonster.GetDamage(power);
        }
    }

    internal void HandleAttackCoroutine(bool isGame)
    {
        if (isGame)
        {
            DisableCoroutine();

            _co_battle = StartCoroutine(Battle());
            _co_distanceOfTarget = StartCoroutine(DistanceOfTarget());
        }
    }

    IEnumerator Battle()
    {
        while (true)
        {
            if (_go_targetMonster != null)
            {
                CrtState = CreatureState.Attack;
                yield return new WaitForSeconds(1f / _attackSpeed);
            }

            yield return null;
        }
    }

    /// <summary>
    /// 추후 몬스터 객체 크기에 따라 distance 비교 차이를 둬야 함
    /// 몬스터 Data에 추가해도 괜찮을 것 같음
    /// </summary>
    /// <returns></returns>
    IEnumerator DistanceOfTarget()
    {
        while (true)
        {
            if (_go_targetMonster != null)
            {
                float distance = Vector3.Distance(Player.Instance.transform.position, _go_targetMonster.transform.position);

                if (distance > 1.5f)
                    _go_targetMonster = null;
            }

            yield return null;
        }
    }

    private void DisableCoroutine()
    {
        if (_co_battle != null)
        {
            StopCoroutine(_co_battle);
            _co_battle = null;

            StopCoroutine(_co_distanceOfTarget);
            _co_distanceOfTarget = null;
        }
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("DropItem"))
        {

        }
    }

    private void OnTriggerStay(Collider col)
    {
        if (col.CompareTag("Monster") && col.gameObject.layer == 11)
        {
            if (_go_targetMonster == null)
            {
                _go_targetMonster = col.gameObject;
                targetMonster = _go_targetMonster.GetComponent<Monster>();
            }
        }
    }

    public override void Clear()
    {

    }
}
