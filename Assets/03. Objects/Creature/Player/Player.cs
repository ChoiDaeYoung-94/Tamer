using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : BaseController
{
    static Player instance;
    public static Player Instance { get { return instance; } }

    [Header("플레어어 고유 Data")]
    [SerializeField] private int _gold = 0;
    public int Gold { get { return instance._gold; } }
    [SerializeField] private int _curCaptureCapacity = 0;
    public int CurCaptureCapacity { get { return instance._curCaptureCapacity; } }
    [SerializeField] private int _maxCaptureCapacity = 0;
    public int MaxCaptureCapacity { get { return instance._maxCaptureCapacity; } }

    [Header("플레이어 Settings")]
    [SerializeField] internal GameObject _go_player = null;
    [SerializeField] internal Transform _tr_cameraArm = null;
    [SerializeField] internal GameObject _sword = null;
    [SerializeField] internal GameObject _shield = null;
    [SerializeField] private GameObject _buff = null;

    [Header("플레이어 버프 시 적용되는 status")]
    [SerializeField] public float _bufPower = 0;
    [SerializeField] public float _bufAttackSpeed = 0;
    [SerializeField] public float _bufMoveSpeed = 0;

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
    /// <summary>
    /// HP는 장비에 맞게 따로 계산
    /// Player의 기본 HP는 100
    /// </summary>
    protected override void Init()
    {
        base.Init();

        _orgHp = 100;
        _hp = 100;
        _gold = int.Parse(AD.Managers.DataM._dic_player["Gold"]);
        _curCaptureCapacity = int.Parse(AD.Managers.DataM._dic_player["CurCaptureCapacity"]);
        _maxCaptureCapacity = int.Parse(AD.Managers.DataM._dic_player["MaxCaptureCapacity"]);
        _power = float.Parse(AD.Managers.DataM._dic_player["Power"]);
        _attackSpeed = float.Parse(AD.Managers.DataM._dic_player["AttackSpeed"]);
        _moveSpeed = float.Parse(AD.Managers.DataM._dic_player["MoveSpeed"]);

        AD.Managers.UpdateM._update -= TouchEvent;
        AD.Managers.UpdateM._update += TouchEvent;

        PlayerUICanvas.Instance.StartInit();
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
                        AD.Managers.SceneM.NextScene(AD.Define.Scenes.Game);
                        break;
                }
            }
        }
    }

    internal void SetBuff()
    {
        _bufPower = _power * 1.3f;
        _bufAttackSpeed = _attackSpeed * 1.3f;
        _bufMoveSpeed = _moveSpeed * 2f;

        _buff.SetActive(true);
    }

    internal void EndBuff()
    {
        _buff.SetActive(false);
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

    public override void Clear()
    {

    }
}
