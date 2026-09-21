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
    private bool _purchasePending;
    private bool _purchaseResultOpen;
    private string _inventoryOwner;
    private int _inventoryGeneration = -1;

    private void Awake()
    {
        _instance = this;
        Init();

        AD.Managers.IAPM.Init();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    #region Functions
    private void Init()
    {
        RefreshInventorySession();
    }

    public void ClearInventorySession()
    {
        _purchasePending = false;
        _inventoryOwner = null; _inventoryGeneration = -1;
        _currentItemsText = string.Empty; CurrentItemsList.Clear();
    }

    public void RefreshInventorySession()
    {
        var data = AD.Managers.DataM;
        if (!data.IsServerDataReady) { ClearInventorySession(); return; }
        if (_inventoryOwner == data.PlayFabId && _inventoryGeneration == data.AccountGeneration) return;
        ClearInventorySession();
        var snapshot = data.ReadInventory();
        _inventoryOwner = snapshot.Owner; _inventoryGeneration = data.AccountGeneration;
        CurrentItemsList = snapshot.OwnedItems.ToList();
        _currentItemsText = string.Join(",", CurrentItemsList);
    }

    public void SaveItem(string item)
    {
        if (CurrentItemsList.Contains(item)) return;
        var data = AD.Managers.DataM;
        var current = data.ReadInventory();
        var owned = current.OwnedItems.ToList();
        if (!owned.Contains(item)) owned.Add(item);
        var saved = data.WriteInventory(_inventoryOwner, _inventoryGeneration, current.Collection, owned, current.Equipped);
        CurrentItemsList = saved.OwnedItems.ToList();
        _currentItemsText = string.Join(",", CurrentItemsList);
    }

    public void OpenShop(int index)
    {
        AD.Managers.SoundM.UI_Click();

        for (int i = 0; i < _shops.Length; i++)
            _shops[i].SetActive(i == index);
    }

    public void ChooseItem(string itemName, string price, string info, bool isEquipment)
    {
        _purchasePending = false;
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
        _purchasePending = false;

        if (_currentItemPrice < 0 || string.IsNullOrEmpty(_currentItemName) ||
            _currentItemPrice > Player.Instance.Gold ||
            (_isEquipmentItem && CurrentItemsList.Contains(_currentItemName)))
        {
            ShowPurchaseResult(_failedBuyMessage);
            return;
        }

        _purchasePending = true;
        ShowPurchaseResult(_successBuyMessage);
    }

    private void ShowPurchaseResult(string message)
    {
        _purchaseResultOpen = true;
        _afterBuyText.text = message;
        _afterBuyPanel.SetActive(true);
    }

    public void CheckSuccessBuy()
    {
        if (!_purchaseResultOpen) return;
        _purchaseResultOpen = false;
        AD.Managers.SoundM.UI_Click();

        if (!_purchasePending)
        {
            AD.Managers.PopupM.DisablePop();
            return;
        }

        if (!TryConsumePurchase(Player.Instance.Gold))
        {
            ShowPurchaseResult(_failedBuyMessage);
            return;
        }

        AD.Managers.PopupM.DisablePop();
        AD.Managers.PopupM.DisablePop();

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

    private bool TryConsumePurchase(int availableGold)
    {
        if (!_purchasePending) return false;
        _purchasePending = false; // Consume before popup callbacks or a second button click.
        return _currentItemPrice >= 0 && _currentItemPrice <= availableGold &&
            !string.IsNullOrEmpty(_currentItemName) &&
            (!_isEquipmentItem || !CurrentItemsList.Contains(_currentItemName));
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
