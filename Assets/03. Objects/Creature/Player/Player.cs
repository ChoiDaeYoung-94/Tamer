using System;
using System.Linq;
using System.Text;
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
    internal int _maxCaptureCapacity = 10;
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
    [SerializeField, Tooltip("포획 가능한 몬스터 cs")] Monster captureMonster = null;

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        instance = this;
        DontDestroyOnLoad(transform.parent.gameObject);

        Init();
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
        StopBattleCoroutine();
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
    protected override void Init()
    {
        base.Init();

        _gold = int.Parse(AD.Managers.DataM._dic_player["Gold"]);

        if (AD.Managers.DataM._dic_player["AllyMonsters"] != "null")
        {
            isAllyAvailable = true;
            SettingAllyMonster();
        }

        JoyStick.Instance.SetSpeed(_moveSpeed);

        AD.Managers.UpdateM._update -= TouchEvent;
        AD.Managers.UpdateM._update += TouchEvent;
    }

    internal void ReSetPlayer()
    {
        isDie = false;
        gameObject.layer = allyLayer;
        _capsuleCollider.enabled = true;

        CrtState = CreatureState.Idle;

        Hp = OrgHp;
    }

    #region Events
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

    internal void Heal()
    {
        Hp = OrgHp;
        HealEffect();

        foreach (Monster monster in _list_groupMonsters)
        {
            monster.Hp = monster.OrgHp;
            monster.HealEffect();
        }
    }
    #endregion

    #region Player
    /// <summary>
    /// 플레이어 공격 애니메이션에서 진행
    /// </summary>
    protected override void AttackTarget()
    {
        if (isDie)
            return;

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
            StopBattleCoroutine();
            StartBattleCoroutine();
        }
    }

    protected override IEnumerator Battle()
    {
        while (true)
        {
            if (_go_targetMonster != null)
            {
                Vector3 direction = _go_targetMonster.transform.position - transform.position;
                direction.y = 0;
                transform.rotation = Quaternion.LookRotation(direction);

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
    protected override IEnumerator DistanceOfTarget()
    {
        while (true)
        {
            if (_go_targetMonster != null)
            {
                float distance = Vector3.Distance(Player.Instance.transform.position, _go_targetMonster.transform.position);

                if (distance > targetMonster.flockingRadius + 0.3f)
                    _go_targetMonster = null;
            }

            yield return null;
        }
    }
    #endregion

    #region AllyMonsters
    /// <summary>
    /// Monster.cs -> GroupMonsterMove()와 동일
    /// </summary>
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

            _list_groupMonsters[i]._navAgent.isStopped = false;
            _list_groupMonsters[i]._navAgent.SetDestination(position);
            _list_groupMonsters[i].CrtState = CreatureState.Move;

            countInRow++;
        }
    }

    internal void AllyIdle()
    {
        foreach (Monster monster in _list_groupMonsters)
            monster.CrtState = CreatureState.Idle;
    }

    internal void Capture()
    {
        captureMonster.AllySetting(playerPosition: transform.position);
        AddAllyMonster(captureMonster);

        PlayerUICanvas.Instance.UpdatePlayerInfo();
    }

    private void SettingAllyMonster()
    {
        string temp_ally = AD.Managers.DataM._dic_player["AllyMonsters"];

        List<string> temp_monsters = temp_ally.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();

        foreach (string temp_monster in temp_monsters)
        {
            Monster monster = AD.Managers.PoolM.PopFromPool(temp_monster, AD.Managers.PoolM._root_Player).GetComponent<Monster>();
            monster.AllySetting(playerPosition: transform.position, setting: true);

            _list_groupMonsters.Add(monster);
        }
    }

    private void AddAllyMonster(Monster monster)
    {
        _list_groupMonsters.Add(captureMonster);
        monster.transform.SetParent(AD.Managers.PoolM._root_Player);

        string temp_ally = AD.Managers.DataM._dic_player["AllyMonsters"];
        string temp_monster = monster._creature.ToString();

        if (temp_ally.Equals("null"))
        {
            isAllyAvailable = true;
            temp_ally = temp_monster;
        }
        else
            temp_ally += $",{temp_monster}";

        AD.Managers.DataM.UpdateLocalData("AllyMonsters", temp_ally);
    }

    internal void RemoveAllyMonster(Monster monster)
    {
        _list_groupMonsters.Remove(monster);

        string temp_ally = string.Empty;

        if (_list_groupMonsters.Count <= 0)
        {
            isAllyAvailable = false;
            temp_ally = "null";
        }
        else
        {
            StringBuilder sb = new StringBuilder();

            foreach (Monster _monster in _list_groupMonsters)
                sb.Append($",{_monster._creature.ToString()}");

            temp_ally = sb.ToString();
        }

        AD.Managers.DataM.UpdateLocalData("AllyMonsters", temp_ally);

        PlayerUICanvas.Instance.UpdatePlayerInfo();
    }

    internal void RemoveAllAllyMonster()
    {
        foreach (Monster monster in _list_groupMonsters)
            monster.GetDamage(1000f);
    }

    internal void ActiveControl(bool active)
    {
        if (active)
        {
            AD.Managers.PoolM._root_Player.transform.position = Vector3.zero;

            foreach (Monster monster in _list_groupMonsters)
            {
                monster.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(1f, 3f);
                monster._navAgent.enabled = true;
            }
        }
        else
        {
            foreach (Monster monster in _list_groupMonsters)
                monster._navAgent.enabled = false;

            AD.Managers.PoolM._root_Player.transform.position = new Vector3(100f, 100f, 100f);
        }
    }
    #endregion

    internal int GetCurMonsterCount()
    {
        return _list_groupMonsters.Count;
    }

    /// <summary>
    /// monster가 죽은 뒤 호출
    /// </summary>
    /// <param name="target"></param>
    internal void NotifyPlayerOfDeath(GameObject target, int gold)
    {
        if (target == _go_targetMonster)
            _go_targetMonster = null;

        _gold += gold;
        AD.Managers.DataM.UpdateLocalData(key: "Gold", value: _gold.ToString());
        PlayerUICanvas.Instance.UpdatePlayerInfo();
    }

    /// <summary>
    /// Die ani
    /// </summary>
    private void GameOver()
    {
        AD.Managers.GameM.GameOver();
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (isDie)
            return;

        if (col.CompareTag("Capture") && _list_groupMonsters.Count < _maxCaptureCapacity)
        {
            PlayerUICanvas.Instance.EnableCapture();
            captureMonster = col.gameObject.GetComponentInParent<Monster>();
        }
    }

    private void OnTriggerStay(Collider col)
    {
        if (isDie)
            return;

        if (col.CompareTag("Monster") && col.gameObject.layer == enemyLayer)
        {
            if (_go_targetMonster == null)
            {
                _go_targetMonster = col.gameObject;
                targetMonster = _go_targetMonster.GetComponent<Monster>();
            }
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (isDie)
            return;

        if (col.CompareTag("Capture"))
        {
            PlayerUICanvas.Instance.DisableCapture();
        }
    }

    public override void Clear()
    {

    }
}
