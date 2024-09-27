using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] GameObject _go_afterBuy = null;
    [SerializeField] TMP_Text _TMP_afterBuy = null;
    string _str_currnetItemName = string.Empty;
    int _currnetItemPrice = 0;
    string _str_successBuy = "Purchase completed.";
    string _str_failedBuy = "You don't have enough Gold.";
    public string _str_currentItems = string.Empty;
    public List<string> _list_currentItems = new List<string>();

    private void Awake()
    {
        instance = this;
        Init();
    }

    private void OnDestroy()
    {
        instance = null;
    }

    #region Functions
    private void Init()
    {
        _str_currentItems = PlayerPrefs.GetString("LocalItem");
        _list_currentItems = _str_currentItems.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public void SaveItem(string item)
    {
        if (string.IsNullOrEmpty(_str_currentItems))
            _str_currentItems = $"{item}";
        else
            _str_currentItems += $",{item}";

        PlayerPrefs.SetString("LocalItem", _str_currentItems);
        _list_currentItems.Add(_str_currentItems);
    }

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

    public void ChooseItem(string itemName, string price, string info)
    {
        _str_currnetItemName = itemName;
        _currnetItemPrice = int.Parse(price);
        _TMP_itemInfo.text = info;
        _go_itemInfo.SetActive(true);
    }

    public void ClickBuy()
    {
        if (_currnetItemPrice > Player.Instance.Gold)
        {
            _TMP_afterBuy.text = _str_failedBuy;
            _go_afterBuy.SetActive(true);
            return;
        }

        SuccessBuy();

        _TMP_afterBuy.text = _str_successBuy;
        _go_afterBuy.SetActive(true);
    }

    private void SuccessBuy()
    {
        // 몬스터, 아이템 구분 필요
        SaveItem(_str_currnetItemName);

        Player.Instance.MinusGold(_currnetItemPrice);
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_popupShop.SetActive(true);
    }
}
