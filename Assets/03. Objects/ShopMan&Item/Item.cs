using System.Collections.Generic;

using UnityEngine;

using TMPro;

public class Item : MonoBehaviour
{
    [SerializeField] private AD.GameConstants.Items _itemType;
    [SerializeField] private AD.GameConstants.Creatures _creatureType;
    [SerializeField] private TMP_Text _itemInfoText;
    [SerializeField] private GameObject _lockIcon;

    private string _itemName;
    private string _itemPrice;
    private bool _isItem;
    private bool _isUnlocked;
    private bool _isEquipped;

    private void Awake()
    {
        ShopMan.Instance.ItemList.Add(this);
        Init();
    }

    public void Init()
    {
        _isItem = _creatureType == AD.GameConstants.Creatures.Player;
        _isUnlocked = false;
        _isEquipped = false;

        _itemName = _itemType == AD.GameConstants.Items.None ? _creatureType.ToString() : _itemType.ToString();

        UpdateItemState();
        UpdateItemUI();
    }

    private void UpdateItemState()
    {
        if (ShopMan.Instance.CurrentItemsList.Contains(_itemName) ||
            Player.Instance.PlayerMonsterCollection.Contains(_itemName))
        {
            _isUnlocked = true;
        }

        if (Player.Instance.PlayerEquippedItems.Contains(_itemName))
        {
            _isEquipped = true;
        }
    }

    private void UpdateItemUI()
    {
        string itemInfo = GetItemInfo();

        if (_isUnlocked)
        {
            _lockIcon.SetActive(false);
            if (_creatureType == AD.GameConstants.Creatures.Player)
                itemInfo = _itemName;
        }

        if (_isEquipped)
            itemInfo += "\nEquipped";

        _itemInfoText.text = itemInfo;
    }

    private string GetItemInfo()
    {
        var dataSource = _itemType == AD.GameConstants.Items.None
            ? AD.Managers.DataM.MonsterData[_itemName] as Dictionary<string, object>
            : AD.Managers.DataM.ItemData[_itemName] as Dictionary<string, object>;

        _itemPrice = dataSource["Price"].ToString();
        return $"{_itemName}\nPrice - {_itemPrice}G";
    }

    public void ChooseItem()
    {
        if ((_isUnlocked && _isEquipped) || (!_isUnlocked && _itemType == AD.GameConstants.Items.None))
            return;
        else if (_isUnlocked && !_isEquipped && _creatureType == AD.GameConstants.Creatures.Player)
            Equip();
        else
            ShowItemInfo();
    }

    private void Equip()
    {
        AD.Managers.EquipmentM.Equip(_itemName);
    }

    private void ShowItemInfo()
    {
        var dataSource = _itemType == AD.GameConstants.Items.None
            ? AD.Managers.DataM.MonsterData[_creatureType.ToString()] as Dictionary<string, object>
            : AD.Managers.DataM.ItemData[_itemType.ToString()] as Dictionary<string, object>;

        string prefix = _itemType != AD.GameConstants.Items.None ? "Plus " : "";

        string itemInfo = $"Item Info\n" +
            $"- {_itemName}\n" +
            $"- {prefix}HP : {dataSource["Hp"]}\n" +
            $"- {prefix}Power : {dataSource["Power"]}\n" +
            $"- {prefix}AttackSpeed : {dataSource["AttackSpeed"]}\n" +
            $"- {prefix}MoveSpeed : {dataSource["MoveSpeed"]}";

        ShopMan.Instance.ChooseItem(_itemName, _itemPrice, itemInfo, _isItem);
    }
}
