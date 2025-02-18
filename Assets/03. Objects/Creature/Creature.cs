using System.Collections.Generic;
using System.Threading;

using UnityEngine;

using Cysharp.Threading.Tasks;

public abstract class Creature : MonoBehaviour
{
    public AD.GameConstants.Creatures CreatureType;

    public enum CreatureState
    {
        Idle,
        Move,
        Attack,
        Die
    }

    [SerializeField] private CreatureState _state = CreatureState.Idle;

    public CreatureState State
    {
        get { return _state; }
        set
        {
            if (value != CreatureState.Attack && _state == value)
                return;

            _state = value;

            if (_state == CreatureState.Die)
                _animator.CrossFade("Die", 0f);

            if (isDie)
                return;

            switch (_state)
            {
                case CreatureState.Idle:
                    _animator.CrossFade("Idle", 0f);
                    break;
                case CreatureState.Move:
                    _animator.CrossFade("Move", 0f);
                    break;
                case CreatureState.Attack:
                    Attack();
                    break;
            }
        }
    }

    [Header("--- Creature 데이터 ---")]
    [SerializeField] protected Animator _animator;
    [SerializeField] protected CapsuleCollider _capsuleCollider;
    [SerializeField] protected GameObject _healObject;
    [SerializeField] protected GameObject _dieEffect;
    [SerializeField] protected GameObject _spawnEffect;
    [SerializeField] protected Transform _uiCanvas;
    [SerializeField] protected AudioSource _audioSource;

    [Header("--- Creature 공용 데이터 ---")]
    [SerializeField] protected float _originalHp = 0f;
    public float OriginalHP { get { return _originalHp; } }

    [SerializeField] protected float _hp = 0f;
    public float Hp { get { return _hp; } set { _hp = value; } }

    [SerializeField] protected float _itemAdditionalHp = 0f;
    public float ItmeAdditionalHp { get { return _itemAdditionalHp; } }

    [SerializeField] protected float _power = 0f;
    public float Power { get { return _power; } }

    [SerializeField] protected float _attackSpeed = 0f;
    public float AttackSpeed { get { return _attackSpeed; } }

    [SerializeField] protected float _moveSpeed = 0f;
    public float MoveSpeed { get { return _moveSpeed; } }

    // 그 외 접근 불가 데이터
    protected int allyLayer = 0;
    protected int enemyLayer = 0;
    protected int dieLayer = 0;
    protected bool isDie = false;
    private CancellationTokenSource _battleTokenSource;
    private CancellationTokenSource _monitorTargetDistanceTokenSource;

    protected virtual void Awake()
    {
        Settings();
    }

    #region Functions

    protected virtual void Settings()
    {
        allyLayer = LayerMask.NameToLayer("Ally");
        enemyLayer = LayerMask.NameToLayer("Enemy");
        dieLayer = LayerMask.NameToLayer("Die");

        if (CreatureType == AD.GameConstants.Creatures.Player)
        {
            _itemAdditionalHp = _originalHp = 100f;
            _hp = 100f;
            _power = float.Parse(AD.Managers.DataM.LocalPlayerData["Power"]);
            _attackSpeed = float.Parse(AD.Managers.DataM.LocalPlayerData["AttackSpeed"]);
            _moveSpeed = float.Parse(AD.Managers.DataM.LocalPlayerData["MoveSpeed"]);
        }
        else
        {
            Dictionary<string, object> monsterInformation = AD.Managers.DataM.MonsterData[CreatureType.ToString()] as Dictionary<string, object>;

            _hp = _originalHp = float.Parse(monsterInformation["Hp"].ToString());
            _power = float.Parse(monsterInformation["Power"].ToString());
            _attackSpeed = float.Parse(monsterInformation["AttackSpeed"].ToString());
            _moveSpeed = float.Parse(monsterInformation["MoveSpeed"].ToString());
        }
    }

    public void HealEffect()
    {
        _healObject.SetActive(true);
    }

    protected void PlaySFX(AudioClip clip)
    {
        _audioSource.clip = clip;
        _audioSource.Play();
    }

    #region 전투 시스템

    protected void StartBattle()
    {
        _battleTokenSource = new CancellationTokenSource();
        _monitorTargetDistanceTokenSource = new CancellationTokenSource();

        BattleLoop(_battleTokenSource.Token).Forget();
        MonitorTargetDistance(_monitorTargetDistanceTokenSource.Token).Forget();
    }

    protected void StopBattle()
    {
        _battleTokenSource?.Cancel();
        _battleTokenSource?.Dispose();
        _battleTokenSource = null;

        _monitorTargetDistanceTokenSource?.Cancel();
        _monitorTargetDistanceTokenSource?.Dispose();
        _monitorTargetDistanceTokenSource = null;
    }

    protected abstract UniTask BattleLoop(CancellationToken token);
    protected abstract UniTask MonitorTargetDistance(CancellationToken token);

    protected abstract void AttackTarget();

    public void GetDamage(float damage)
    {
        if (Hp <= 0f)
            return;

        if (gameObject.layer == enemyLayer)
        {
            _uiCanvas.LookAt(Camera.main.transform);

            GameObject damageObject = AD.Managers.PoolM.PopFromPool(AD.GameConstants.ETC.TMPDamage.ToString());
            damageObject.transform.SetParent(_uiCanvas, false);
            damageObject.GetComponent<TMP_Damage>().Init(damage);
        }

        Hp -= damage;
        if (Hp < 0f)
            Hp = 0f;

        if (CreatureType == AD.GameConstants.Creatures.Player)
        {
            PlayerUICanvas.Instance.UpdatePlayerInfo();
        }

        if (Hp <= 0)
        {
            _dieEffect.SetActive(true);

            isDie = true;
            gameObject.layer = dieLayer;
            _capsuleCollider.enabled = false;

            State = CreatureState.Die;
        }
    }

    private void Attack()
    {
        if (CreatureType == AD.GameConstants.Creatures.Player)
        {
            if (Player.Instance.IsEquippedSword)
            {
                PlaySFX(AD.Managers.SoundM.SFXSwordClip);
                _animator.CrossFade($"Attack0{Random.Range(1, 4)}_sword", 0f);
            }
            else
            {
                PlaySFX(AD.Managers.SoundM.SFXPunchClip);
                _animator.CrossFade($"Punch0{Random.Range(1, 3)}", 0f);
            }
        }
        else if (CreatureType == AD.GameConstants.Creatures.FylingDemon)
        {
            _animator.CrossFade($"Attack{Random.Range(1, 3)}", 0f);
        }
        else
        {
            _animator.CrossFade("Attack", 0.1f);
        }
    }

    #endregion

    public abstract void Clear();

    #endregion
}
