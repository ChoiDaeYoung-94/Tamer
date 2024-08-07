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
            _crtState = value;

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
                case CreatureState.Die:
                    _crtAni.CrossFade("Die", 0f);
                    break;
            }
        }
    }

    [Header("--- 미리 가지고 있어야 할 공용 data ---")]
    [SerializeField] Animator _crtAni = null;
    protected int allyLayer = 0;
    protected int enemyLayer = 0;
    protected int dieLayer = 0;

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

    private void Awake()
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
    protected virtual void Init(AD.Define.Creature creature)
    {
        if (creature == AD.Define.Creature.Player)
        {
            _orgHp = 100;
            _hp = 100;
            _power = float.Parse(AD.Managers.DataM._dic_player["Power"]);
            _attackSpeed = float.Parse(AD.Managers.DataM._dic_player["AttackSpeed"]);
            _moveSpeed = float.Parse(AD.Managers.DataM._dic_player["MoveSpeed"]);
        }
        else
        {
            string key = creature.ToString();
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

    public abstract void Clear();

    protected abstract void AttackTarget();

    internal abstract void GetDamage(float damage);

    private void State_Attack()
    {
        string str_tag = gameObject.tag;

        if (str_tag == "Player")
        {
            int index = 0;
            if (Player.Instance._sword.activeInHierarchy)
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
}
