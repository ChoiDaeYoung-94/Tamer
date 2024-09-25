using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShopMan : MonoBehaviour
{
    static ShopMan instance;
    public static ShopMan Instance { get { return instance; } }

    [Header("--- 세팅 ---")]
    [SerializeField] GameObject _go_popupShop = null;
    [SerializeField] GameObject _go_shop1 = null;
    [SerializeField] GameObject _go_shop2 = null;
    [SerializeField] GameObject _go_shop3 = null;
    [SerializeField] GameObject _go_itemInfo = null;
    [SerializeField] TMP_Text _TMP_itemInfo = null;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    #region Functions
    public void OpenShop1()
    {
        _go_shop1.SetActive(true);
        _go_shop2.SetActive(false);
        _go_shop3.SetActive(false);
    }

    public void OpenShop2()
    {
        _go_shop1.SetActive(false);
        _go_shop2.SetActive(true);
        _go_shop3.SetActive(false);
    }

    public void OpenShop3()
    {
        _go_shop1.SetActive(false);
        _go_shop2.SetActive(false);
        _go_shop3.SetActive(true);
    }

    public void ChooseItem(string info)
    {
        _TMP_itemInfo.text = info;
        _go_itemInfo.SetActive(true);
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_popupShop.SetActive(true);
    }
}
