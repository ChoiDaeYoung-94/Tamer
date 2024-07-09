using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterGenerator : MonoBehaviour
{
    static MonsterGenerator instance;
    public static MonsterGenerator Instance { get { return instance; } }

    [Header("--- 세팅 ---")]
    [SerializeField] GameObject _go_boss = null;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    #region Functions
    /// <summary>
    /// InitializeGame.cs에서 호출
    /// </summary>
    internal void Init()
    {
        
    }
    #endregion
}
