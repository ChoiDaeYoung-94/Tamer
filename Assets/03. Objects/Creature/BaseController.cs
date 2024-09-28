using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public abstract class BaseController : MonoBehaviour
{
    public enum CreatureState
    {
        Idle,
        Move,
        Attack,
        Die
    }

    [SerializeField] CreatureState _crtState = CreatureState.Idle;

    public CreatureState CrtState
    {
        get { return _crtState; }
        set
        {
            if (value != CreatureState.Attack && _crtState == value)
                return;

            _crtState = value;

            if (!isDie)
            {
                switch (_crtState)
                {
                    case CreatureState.Idle:
                        _crtAni.CrossFade("Idle", 0f);
                        break;
                    case CreatureState.Move:
                        _crtAni.CrossFade("Move", 0f);
                        break;
                    case CreatureState.Attack:
                        State_Attack();
                        break;
                }
            }
            else
                _crtAni.CrossFade("Die", 0f);
        }
    }

    [SerializeField] internal AD.Define.Creature _creature;

    [Header("--- 미리 가지고 있어야 할 공용 data ---")]
    [SerializeField] Animator _crtAni = null;
    [SerializeField] protected int allyLayer = 0;
    [SerializeField] protected int enemyLayer = 0;
    [SerializeField] protected int dieLayer = 0;
    [SerializeField] protected CapsuleCollider _capsuleCollider = null;
    [SerializeField] GameObject _go_heal = null;
    [SerializeField] Transform _tr_uiCanvas = null;
    [SerializeField] protected GameObject _go_effectSpawn = null;
    [SerializeField] protected GameObject _go_effectDie = null;

    [Header("--- 공용 데이터 초기화 시 세팅 ---")]
    [SerializeField] protected float _orgHp = 0;
    public float OrgHp { get { return _orgHp; } }

    [SerializeField] protected float _hp = 0;
    public float Hp { get { return _hp; } set { _hp = value; } }

    [SerializeField] protected float _power = 0f;
    public float Power { get { return _power; } }

    [SerializeField] protected float _attackSpeed = 0f;
    public float AttackSpeed { get { return _attackSpeed; } }

    [SerializeField] protected float _moveSpeed = 0f;
    public float MoveSpeed { get { return _moveSpeed; } }
    [SerializeField] protected bool isDie = false;

    [Header("--- Coroutine ---")]
    protected Coroutine _co_battle;
    protected Coroutine _co_distanceOfTarget;

    protected virtual void Awake()
    {
        SetLayer();
    }

    private void Start()
    {

    }

    private void Update()
    {

    }

    private void FixedUpdate()
    {

    }

    #region Functions

    #region Settings
    protected virtual void Init()
    {
        if (_creature == AD.Define.Creature.Player)
        {
            _orgHp = 100;
            _hp = 100;
            _power = float.Parse(AD.Managers.DataM._dic_player["Power"]);
            _attackSpeed = float.Parse(AD.Managers.DataM._dic_player["AttackSpeed"]);
            _moveSpeed = float.Parse(AD.Managers.DataM._dic_player["MoveSpeed"]);
        }
        else
        {
            string key = _creature.ToString();
            Dictionary<string, object> dic_temp = AD.Managers.DataM._dic_monsters[key] as Dictionary<string, object>;

            _hp = _orgHp = float.Parse(dic_temp["Hp"].ToString());
            _power = float.Parse(dic_temp["Power"].ToString());
            _attackSpeed = float.Parse(dic_temp["AttackSpeed"].ToString());
            _moveSpeed = float.Parse(dic_temp["MoveSpeed"].ToString());
        }
    }

    private void SetLayer()
    {
        allyLayer = LayerMask.NameToLayer("Ally");
        enemyLayer = LayerMask.NameToLayer("Enemy");
        dieLayer = LayerMask.NameToLayer("Die");
    }
    #endregion

    public void HealEffect()
    {
        _go_heal.SetActive(true);
    }

    #region Battle
    public void GetDamage(float damage)
    {
        if (Hp <= 0)
            return;

        if (gameObject.layer == enemyLayer)
        {
            _tr_uiCanvas.LookAt(Camera.main.transform);

            GameObject go_damage = AD.Managers.PoolM.PopFromPool(AD.Define.ETC.TMP_Damage.ToString());
            go_damage.transform.SetParent(_tr_uiCanvas, false);
            go_damage.GetComponent<TMP_Damage>().Init(damage);
        }

        Hp -= damage;
        if (Hp < 0)
            Hp = 0;

        if (_creature == AD.Define.Creature.Player)
            PlayerUICanvas.Instance.UpdatePlayerInfo();

        if (Hp <= 0)
        {
            _go_effectDie.SetActive(true);

            isDie = true;
            gameObject.layer = dieLayer;
            _capsuleCollider.enabled = false;

            CrtState = CreatureState.Die;
        }
    }

    private void State_Attack()
    {
        string str_tag = gameObject.tag;

        if (str_tag == "Player")
        {
            int index = 0;
            if (Player.Instance.isEquippedSword)
            {
                index = Random.Range(1, 4);
                _crtAni.CrossFade($"Attack0{index}_sword", 0f);
            }
            else
            {
                index = Random.Range(1, 3);
                _crtAni.CrossFade($"Punch0{index}", 0f);
            }
        }
        else if (str_tag == "Boss")
        {

        }
        else
            _crtAni.CrossFade("Attack", 0.1f);
    }
    #endregion

    #region Coroutine
    protected void StartBattleCoroutine()
    {
        _co_battle = StartCoroutine(Battle());
        _co_distanceOfTarget = StartCoroutine(DistanceOfTarget());
    }

    protected void StopBattleCoroutine()
    {
        if (_co_battle != null)
        {
            StopCoroutine(_co_battle);
            _co_battle = null;
        }

        if (_co_distanceOfTarget != null)
        {
            StopCoroutine(_co_distanceOfTarget);
            _co_distanceOfTarget = null;
        }
    }

    protected abstract IEnumerator Battle();

    protected abstract IEnumerator DistanceOfTarget();
    #endregion

    public abstract void Clear();

    protected abstract void AttackTarget();

    #endregion
}
