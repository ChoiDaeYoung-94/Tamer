using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AD
{
    /// <summary>
    /// Game에서 호출되는 메서드 관리
    /// </summary>
    public class GameManager
    {
        [Header("--- 참고용 ---")]
        [SerializeField, Tooltip("현재 게임씬 여부")] private bool _isGame = false;
        internal bool IsGame { get { return _isGame; } }
    }
}
