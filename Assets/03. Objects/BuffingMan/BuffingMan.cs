using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuffingMan : MonoBehaviour
{
    [Header("--- 세팅 ---")]
    [SerializeField, Tooltip("Admob 광고 오브젝트")] GameObject _go_admob = null;

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_admob.SetActive(true);
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_admob.SetActive(false);
    }
}