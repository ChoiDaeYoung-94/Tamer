using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : BaseController
{
    [Header("--- 참고용 ---")]
    [SerializeField, Tooltip("플레이어 레벨")]
    int _level = 0;

    /// <summary>
    /// Initialize_Game.cs 에서 호출
    /// </summary>
    public void StartInit()
    {

    }

    #region Functions

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
}
