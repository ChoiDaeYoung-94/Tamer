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
        Run,
        Attack,
        GetHit,
        Death
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
                    _crtAni.CrossFade("Idle", 0.5f);
                    break;
                case CreatureState.Run:
                    _crtAni.CrossFade("Run", 0.5f);
                    break;
                case CreatureState.Attack:
                    _crtAni.CrossFade("Attack", 0.1f);
                    break;
                case CreatureState.Death:
                    _crtAni.CrossFade("Death", 0.5f);
                    break;
            }
        }
    }

    [Header("--- 미리 가지고 있어야 할 공용 data ---")]
    [SerializeField] Animator _crtAni = null;

    [Header("--- 공용 데이터 초기화 시 세팅 ---")]
    [SerializeField] protected int _hp = 0;
    [SerializeField] protected float _power = 0f;
    [SerializeField] protected float _attackSpeed = 0f;
    [SerializeField] protected float _moveSpeed = 0f;
    

    private void Awake()
    {
        Init();
    }

    private void Start()
    {

    }

    private void Update()
    {
        UpdateAni();
    }

    private void FixedUpdate()
    {

    }

    #region Functions
    protected virtual void Init()
    {

    }

    public abstract void Clear();

    #region Ani
    protected virtual void UpdateAni()
    {
        switch (CrtState)
        {
            case CreatureState.Idle:
                Idle();
                break;
            case CreatureState.Run:
                Run();
                break;
            case CreatureState.Attack:
                Attack();
                break;
            case CreatureState.Death:
                Death();
                break;
        }
    }

    protected virtual void Idle()
    {

    }

    protected virtual void Run()
    {

    }

    protected virtual void Attack()
    {

    }

    protected virtual void Death()
    {

    }
    #endregion

    #endregion
}
