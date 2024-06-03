using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameMan : MonoBehaviour
{
    [Header("--- 세팅 ---")]
    [SerializeField] private GameObject _go_Game = null;

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_Game.SetActive(true);
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_Game.SetActive(false);
    }
}
