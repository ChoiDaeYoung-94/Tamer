using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using TMPro;

public class ShopMan : MonoBehaviour
{
    private static ShopMan _instance;
    public static ShopMan Instance { get { return _instance; } }

    [Header("--- UI Elements ---")]
    [SerializeField] private GameObject _popupShopUI;
    [SerializeField] private GameObject[] _shops;
    [SerializeField] private GameObject _itemInfoPanel;
    [SerializeField] private TMP_Text _itemInfoText;
    [SerializeField] private GameObject _afterBuyPanel;
    [SerializeField] private TMP_Text _afterBuyText;

    private string _currentItemName = string.Empty;
    private int _currentItemPrice = 0;
    private const string _successBuyMessage = "Purchase completed.";
    private const string _failedBuyMessage = "You don't have enough Gold.";

    public string _currentItemsText = string.Empty;
    public List<string> CurrentItemsList = new List<string>();
    public List<Item> ItemList = new List<Item>();
    public List<IAPItem> IAPitemList = new List<IAPItem>();

    private bool _isEquipmentItem;

    private void Awake()
    {
        _instance = this;
        Init();

        AD.Managers.IAPM.Init();
    }

    private void OnDestroy()
    {
        _instance = null;
    }

    #region Functions
    private void Init()
    {
        _currentItemsText = PlayerPrefs.GetString("LocalItem");
        CurrentItemsList = _currentItemsText.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public void SaveItem(string item)
    {
        if (string.IsNullOrEmpty(_currentItemsText))
            _currentItemsText = item;
        else
            _currentItemsText += $",{item}";

        PlayerPrefs.SetString("LocalItem", _currentItemsText);
        CurrentItemsList.Add(item);
    }

    public void OpenShop(int index)
    {
        AD.Managers.SoundM.UI_Click();

        for (int i = 0; i < _shops.Length; i++)
            _shops[i].SetActive(i == index);
    }

    public void ChooseItem(string itemName, string price, string info, bool isEquipment)
    {
        AD.Managers.SoundM.UI_Click();

        _currentItemName = itemName;
        _currentItemPrice = int.Parse(price);
        _itemInfoText.text = info;
        _isEquipmentItem = isEquipment;
        _itemInfoPanel.SetActive(true);
    }

    public void ClickBuy()
    {
        AD.Managers.SoundM.UI_Click();

        if (_currentItemPrice > Player.Instance.Gold)
        {
            ShowPurchaseResult(_failedBuyMessage);
            return;
        }

        ShowPurchaseResult(_successBuyMessage);
    }

    private void ShowPurchaseResult(string message)
    {
        _afterBuyText.text = message;
        _afterBuyPanel.SetActive(true);
    }

    public void CheckSuccessBuy()
    {
        AD.Managers.SoundM.UI_Click();

        if (_afterBuyText.text != _successBuyMessage)
        {
            AD.Managers.PopupM.DisablePop();
            return;
        }

        if (_afterBuyText.text == _successBuyMessage)
        {
            AD.Managers.PopupM.DisablePop();
            AD.Managers.PopupM.DisablePop();
        }

        if (_isEquipmentItem)
        {
            SaveItem(_currentItemName);
            AD.Managers.EquipmentM.Equip(_currentItemName);
        }
        else
        {
            Player.Instance.BuyAllyMonster(_currentItemName);
            ResetItems();
        }

        Player.Instance.MinusGold(_currentItemPrice);
    }

    public void ResetItems()
    {
        foreach (Item item in ItemList)
            item.Init();
    }

    #region IAP
    public void IAP(string id)
    {
        AD.Managers.SoundM.UI_Click();

        if (id == AD.GameConstants.IAPItems.ProductNoAds.ToString())
            AD.Managers.IAPM.BuyProductID(AD.Managers.IAPM.ProductNoAds);
    }

    public void IAPReset()
    {
        foreach (IAPItem iapItem in IAPitemList)
            iapItem.Init();
    }
    #endregion

    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
            _popupShopUI.SetActive(true);
    }
}
