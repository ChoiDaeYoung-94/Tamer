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

    private void Awake()
    {
        if (instance == null)
        {
            GameObject go = this.gameObject;
            if (go == null)
            {
                string sex = AD.Managers.DataM._dic_PlayFabPlayerData["Sex"].Value.Equals("Man") ? "Man" : "Woman";

                go = AD.Managers.ResourceM.Instantiate_("Player", "Player/Player_" + sex);
            }

            DontDestroyOnLoad(go);
            instance = go.GetComponent<Player>();
        }
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    private void StartInit()
    {
        Init();
    }

    #region Functions
    protected override void Init()
    {
        base.Init();

        this._gold = int.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["Gold"].Value);
        this._level = int.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["Level"].Value);
        this._experience = long.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["Experience"].Value);
        this._maxCount = int.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["MaxCount"].Value);
        this._hp = int.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["HP"].Value);
        this._power = float.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["Power"].Value);
        this._attackSpeed = float.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["AttackSpeed"].Value);
        this._moveSpeed = float.Parse(AD.Managers.DataM._dic_PlayFabPlayerData["MoveSpeed"].Value);
    }
    #endregion

    private void OnTriggerEnter2D(Collider2D col)
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
