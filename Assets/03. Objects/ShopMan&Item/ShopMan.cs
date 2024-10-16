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
    bool isItem = false;
    public List<Item> _list_item = new List<Item>();
    public List<IAPItem> _list_IAPitem = new List<IAPItem>();

    private void Awake()
    {
        instance = this;
        Init();

        AD.Managers.IAPM.Init();
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
        _list_currentItems.Add(item);
    }

    public void OpenShop1()
    {
        AD.Managers.SoundM.UI_Click();
        _go_shop1.SetActive(true);
        _go_shop2.SetActive(false);
        _go_shop3.SetActive(false);
    }

    public void OpenShop2()
    {
        AD.Managers.SoundM.UI_Click();
        _go_shop2.SetActive(true);
        _go_shop1.SetActive(false);
        _go_shop3.SetActive(false);
    }

    public void OpenShop3()
    {
        AD.Managers.SoundM.UI_Click();
        _go_shop3.SetActive(true);
        _go_shop1.SetActive(false);
        _go_shop2.SetActive(false);
    }

    public void ChooseItem(string itemName, string price, string info, bool isitem)
    {
        AD.Managers.SoundM.UI_Click();

        _str_currnetItemName = itemName;
        _currnetItemPrice = int.Parse(price);
        _TMP_itemInfo.text = info;
        this.isItem = isitem;
        _go_itemInfo.SetActive(true);
    }

    public void ClickBuy()
    {
        AD.Managers.SoundM.UI_Click();

        if (_currnetItemPrice > Player.Instance.Gold)
        {
            _TMP_afterBuy.text = _str_failedBuy;
            _go_afterBuy.SetActive(true);
            return;
        }

        _TMP_afterBuy.text = _str_successBuy;
        _go_afterBuy.SetActive(true);
    }

    public void CheckSuccessBuy()
    {
        AD.Managers.SoundM.UI_Click();

        if (_TMP_afterBuy.text != _str_successBuy)
        {
            AD.Managers.PopupM.DisablePop();
            return;
        }

        if (_TMP_afterBuy.text == _str_successBuy)
        {
            AD.Managers.PopupM.DisablePop();
            AD.Managers.PopupM.DisablePop();
        }

        if (isItem)
        {
            SaveItem(_str_currnetItemName);
            AD.Managers.EquipmentM.Equip(_str_currnetItemName);
        }
        else
        {
            Player.Instance.BuyAllyMonster(_str_currnetItemName);
            ResetItem();
        }

        Player.Instance.MinusGold(_currnetItemPrice);
    }

    public void ResetItem()
    {
        foreach (Item item in _list_item)
            item.Init();
    }

    #region IAP
    public void IAP(string id)
    {
        AD.Managers.SoundM.UI_Click();

        if (id == AD.Define.IAPItems.PRODUCT_NO_ADS.ToString())
            AD.Managers.IAPM.BuyProductID(AD.Managers.IAPM.PRODUCT_NO_ADS);
    }

    public void IAPReset()
    {
        foreach (IAPItem IAPitem in _list_IAPitem)
            IAPitem.Init();
    }
    #endregion

    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
            _go_popupShop.SetActive(true);
    }
}
