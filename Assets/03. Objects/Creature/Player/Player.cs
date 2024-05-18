using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : BaseController
{
    static Player instance;
    public static Player Instance { get { return instance; } }

    [Header("플레어어 고유 Data")]
    [SerializeField] private int _gold = 0;
    [SerializeField] private int _level = 0;
    [SerializeField] private long _experience = 0;
    [SerializeField] private int _maxCount = 0;

    [Header("플레이어 Settings")]
    [SerializeField] internal GameObject _go_player = null;
    [SerializeField] internal Transform _tr_cameraArm = null;
    [SerializeField] internal GameObject _sword = null;
    [SerializeField] internal GameObject _shield = null;

    [Header("특정 구역 진입 시 계산 위함")]
    [SerializeField] private float _stayTime = 0;
    [SerializeField] private bool isClear = false;

    /// <summary>
    /// LoginCheck.cs 에서 호출
    /// </summary>
    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(transform.parent.gameObject);

        Init();
    }

    #region Functions
    protected override void Init()
    {
        base.Init();

        _gold = int.Parse(AD.Managers.DataM._dic_player["Gold"]);
        _level = int.Parse(AD.Managers.DataM._dic_player["Level"]);
        _experience = long.Parse(AD.Managers.DataM._dic_player["Experience"]);
        _hp = int.Parse(AD.Managers.DataM._dic_player["HP"]);
        _power = float.Parse(AD.Managers.DataM._dic_player["Power"]);
        _attackSpeed = float.Parse(AD.Managers.DataM._dic_player["AttackSpeed"]);
        _moveSpeed = float.Parse(AD.Managers.DataM._dic_player["MoveSpeed"]);
        _maxCount = int.Parse(AD.Managers.DataM._dic_player["MaxCount"]);
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Monster"))
        {

        }

        if (col.CompareTag("DropItem"))
        {

        }
    }

    private void OnTriggerStay(Collider col)
    {
        if (col.CompareTag("BuffingMan"))
        {
            if (_stayTime >= 0.5f && !isClear)
            {
                isClear = true;
                AD.Managers.GoogleAdMobM.ShowRewardedAd();
            }
            else
                _stayTime += Time.deltaTime;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        isClear = false;
        _stayTime = 0f;
    }

    public override void Clear()
    {

    }
}
