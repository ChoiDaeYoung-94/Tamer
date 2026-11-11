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
    private const string _successBuyMessage = "구매를 완료했습니다.";
    private const string _failedBuyMessage = "골드가 부족하거나 지금은 구매할 수 없는 상품입니다.";
    private const string _confirmAllyMessage = "이 동료를 구매할까요?";
    private const string _unavailableAllyMessage = "구매를 마치지 못했습니다.\n다시 구매하기 전에 기존 계정으로\n로그인해 구매 상태를 확인해 주세요.";

    public string _currentItemsText = string.Empty;
    public List<string> CurrentItemsList = new List<string>();
    public List<Item> ItemList = new List<Item>();
    public List<IAPItem> IAPitemList = new List<IAPItem>();

    private bool _isEquipmentItem;
    private bool _purchasePending;
    private bool _purchaseResultOpen;
    private bool _closeItemInfoOnResult;
    private string _inventoryOwner;
    private int _inventoryGeneration = -1;
    private AD.DataManager.AllyPurchaseIntent _pendingAllyPurchase;
    private AD.DataManager.EquipmentPurchaseIntent _pendingEquipmentPurchase;

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
        RefreshDisplayLabels();
        RefreshInventorySession();
    }

    private void RefreshDisplayLabels()
    {
        if (_popupShopUI == null) return;
        foreach (var label in _popupShopUI.GetComponentsInChildren<TMP_Text>(true))
        {
            string caption;
            switch (label.text?.Replace("\r\n", "\n").Trim())
            {
                case "SHOP": caption = "상점"; break;
                case "GEAR": caption = "장비"; break;
                case "ALLIES": caption = "동료"; break;
                case "NO ADS": caption = "광고 제거"; break;
                case "Remove Ads\nPrice shown in Google Play":
                    caption = "광고 제거\n가격은 Google Play에서 확인"; break;
                default: continue;
            }
            if (_itemInfoText != null && _itemInfoText.font != null)
            {
                label.font = _itemInfoText.font;
                label.fontSharedMaterial = _itemInfoText.fontSharedMaterial;
            }
            label.text = caption;
        }
    }

    public void ClearInventorySession()
    {
        _closeItemInfoOnResult = false;
        _purchasePending = false;
        _pendingAllyPurchase = null;
        _pendingEquipmentPurchase = null;
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
        _closeItemInfoOnResult = false;
        _purchasePending = false;
        _pendingAllyPurchase = null;
        _pendingEquipmentPurchase = null;
        AD.Managers.SoundM.UI_Click();

        _currentItemName = itemName;
        _currentItemPrice = int.Parse(price);
        _itemInfoText.text = info;
        foreach (var text in _itemInfoPanel.GetComponentsInChildren<TMP_Text>(true))
            if (text != _itemInfoText && text.name == "TMP_content") text.text = "선택한 상품을 구매할까요?";
        SetDialogCaptions(_itemInfoPanel, _itemInfoText, "Btn_yes", "구매하기");
        _isEquipmentItem = isEquipment;
        _itemInfoPanel.SetActive(true);
    }

    public void ClickBuy()
    {
        AD.Managers.SoundM.UI_Click();
        _purchasePending = false;
        _pendingAllyPurchase = null;
        _pendingEquipmentPurchase = null;

        if (_currentItemPrice < 0 || string.IsNullOrEmpty(_currentItemName) ||
            _currentItemPrice > Player.Instance.Gold ||
            (_isEquipmentItem && CurrentItemsList.Contains(_currentItemName)))
        {
            ShowPurchaseResult(_failedBuyMessage);
            return;
        }

        if (_isEquipmentItem)
        {
            try { _pendingEquipmentPurchase = Player.Instance.PrepareEquipmentPurchase(_currentItemName, _currentItemPrice); }
            catch (Exception) { _pendingEquipmentPurchase = null; }
            if (_pendingEquipmentPurchase == null) { ShowPurchaseResult(_unavailableAllyMessage); return; }
        }
        else
        {
            _pendingAllyPurchase = Player.Instance.PrepareAllyPurchase(_currentItemName, _currentItemPrice);
            if (_pendingAllyPurchase == null)
            {
                ShowPurchaseResult(_unavailableAllyMessage);
                return;
            }
        }
        _purchasePending = true;
        ShowPurchaseResult(PurchaseConfirmationMessage());
    }

    private string PurchaseConfirmationMessage() =>
        (_isEquipmentItem ? "이 장비를 구매할까요?" : _confirmAllyMessage) + "\n" + Item.DisplayName(_currentItemName) +
        "\n가격: " + _currentItemPrice.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " 골드";

    private void ShowPurchaseResult(string message, bool completed = false)
    {
        _closeItemInfoOnResult = completed;
        _purchaseResultOpen = true;
        _afterBuyText.text = message;
        SetDialogCaptions(_afterBuyPanel, _afterBuyText, "Btn_OK", _purchasePending ? "구매 확정" : "확인");
        // A pending purchase requires the explicit confirmation button, never the backdrop.
        foreach (var backdrop in _afterBuyPanel.GetComponents<UnityEngine.UI.Button>())
            backdrop.enabled = !_purchasePending;
        _afterBuyPanel.SetActive(true);
    }

    private static void SetDialogCaptions(GameObject panel, TMP_Text template, string confirmName, string confirmCaption)
    {
        foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            string caption = button.name == confirmName ? confirmCaption : button.name == "Btn_no" ? "돌아가기" : null;
            if (caption == null) continue;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (template != null && template.font != null)
                {
                    label.font = template.font;
                    label.fontSharedMaterial = template.fontSharedMaterial;
                }
                label.text = caption;
            }
        }
    }

    public void CheckSuccessBuy()
    {
        if (!_purchaseResultOpen) return;
        _purchaseResultOpen = false;
        AD.Managers.SoundM.UI_Click();

        if (!_purchasePending)
        {
            if (!DismissPurchaseResult()) _purchaseResultOpen = true;
            return;
        }

        if (!TryConsumePurchase(Player.Instance.Gold))
        {
            _pendingAllyPurchase = null;
            _pendingEquipmentPurchase = null;
            ShowPurchaseResult(_failedBuyMessage);
            return;
        }

        if (!_isEquipmentItem)
        {
            var purchase = _pendingAllyPurchase;
            _pendingAllyPurchase = null; // No popup callback may replay this confirmation.
            if (!Player.Instance.TryPurchaseAlly(purchase, () =>
            {
                ResetItems();
                PlayerUICanvas.Instance.UpdatePlayerInfo();
                ShowPurchaseResult(_successBuyMessage, completed: true);
            })) ShowPurchaseResult(_unavailableAllyMessage);
            return;
        }

        var equipment = _pendingEquipmentPurchase;
        _pendingEquipmentPurchase = null;
        var result = Player.Instance.TryPurchaseEquipment(equipment, () =>
        {
            // RefreshInventorySession is a no-op for an unchanged session; use the saved snapshot directly.
            var inventory = AD.Managers.DataM.ReadInventory();
            CurrentItemsList = inventory.OwnedItems.ToList();
            _currentItemsText = string.Join(",", CurrentItemsList);
            ResetItems(); PlayerUICanvas.Instance.UpdatePlayerInfo();
            ShowPurchaseResult(_successBuyMessage, completed: true);
        });
        if (result == AD.EquipmentPurchaseResult.AlreadyCommittedCompleted) ShowPurchaseResult(_successBuyMessage, completed: true);
        else if (result != AD.EquipmentPurchaseResult.Completed) ShowPurchaseResult(_unavailableAllyMessage);
    }

    private bool DismissPurchaseResult()
    {
        var popups = AD.Managers.PopupM;
        popups.RequestClosePopup(_afterBuyPanel);
        if (_afterBuyPanel.activeSelf) return false;
        if (_closeItemInfoOnResult) popups.RequestClosePopup(_itemInfoPanel);
        _closeItemInfoOnResult = false;
        return true;
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
