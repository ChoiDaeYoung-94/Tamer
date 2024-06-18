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

    [Header("--- 공용 데이터 초기화 시 세팅 ---")]
    [SerializeField] protected int _orgHp = 0;
    public int OrgHp { get { return _orgHp; } }
    [SerializeField] protected int _hp = 0;
    public int Hp { get { return _hp; } }
    [SerializeField] protected float _power = 0f;
    public float Power { get { return _power; } }
    [SerializeField] protected float _attackSpeed = 0f;
    public float AttackSpeed { get { return _attackSpeed; } }
    [SerializeField] protected float _moveSpeed = 0f;
    public float MoveSpeed { get { return _moveSpeed; } }

    private void Awake()
    {

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
    protected virtual void Init()
    {

    }

    public abstract void Clear();

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
