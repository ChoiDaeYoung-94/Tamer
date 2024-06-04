using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniMap : MonoBehaviour
{
    static MiniMap instance;
    public static MiniMap Instance { get { return instance; } }

    [Header("--- 세팅 ---")]
    [SerializeField] private Sprite[] _spr_player = null;
    [SerializeField] private Transform _tr_player = null;
    [SerializeField] private SpriteRenderer _sprR_player = null;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    /// <summary>
    /// InitializeGame.cs에서 호출
    /// </summary>
    internal void StartInit()
    {
        Settings();
    }

    private void Settings()
    {
        _sprR_player.sprite = AD.Managers.DataM._dic_player["Sex"] == "Man" ? _spr_player[0] : _spr_player[1];

        AD.Managers.UpdateM._update -= SetPlayerIcon;
        AD.Managers.UpdateM._update += SetPlayerIcon;
    }

    private void SetPlayerIcon()
    {
        Transform tr_temp = Player.Instance.transform;

        _tr_player.localPosition = new Vector3(tr_temp.position.x, tr_temp.position.z, -1f);
    }
}
