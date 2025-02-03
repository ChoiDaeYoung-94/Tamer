using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Item : MonoBehaviour
{
    [SerializeField] AD.GameConstants.Item _items;
    [SerializeField] AD.GameConstants.Creature _creature;
    [SerializeField] TMP_Text _TMP_info = null;
    string _str_itemName = string.Empty;
    string _str_price = string.Empty;
    [SerializeField] GameObject _go_lock = null;
    bool isItem = false;
    bool isUnlocked = false;
    bool isEquipped = false;

    private void Awake()
    {
        ShopMan.Instance._list_item.Add(this);
        Init();
    }

    #region Functions
    public void Init()
    {
        isItem = false;
        isUnlocked = false;
        isEquipped = false;

        _str_itemName = _items == AD.GameConstants.Item.None ? _creature.ToString() : _items.ToString();
        if (_creature == AD.GameConstants.Creature.Player)
            isItem = true;

        ItemState();
        ItemSetting();
    }

    private void ItemState()
    {
        if (ShopMan.Instance._list_currentItems.Contains(_str_itemName) ||
            Player.Instance._list_playerMonsters.Contains(_str_itemName))
            isUnlocked = true;

        if (Player.Instance._list_playerEquippedItems.Contains(_str_itemName))
            isEquipped = true;
    }

    private void ItemSetting()
    {
        string str_totalInfo = string.Empty;
        str_totalInfo = returnInfo();

        if (isUnlocked)
        {
            _go_lock.SetActive(false);

            if (_creature == AD.GameConstants.Creature.Player)
                str_totalInfo = $"{_str_itemName}";
        }

        if (isEquipped)
            str_totalInfo += "\nEquipped";

        _TMP_info.text = str_totalInfo;
    }

    private string returnInfo()
    {
        string str_temp = string.Empty;

        _str_price = _items == AD.GameConstants.Item.None ? ((Dictionary<string, object>)AD.Managers.DataM._dic_monsters[_str_itemName])["Price"].ToString()
                                          : ((Dictionary<string, object>)AD.Managers.DataM._dic_items[_str_itemName])["Price"].ToString();

        str_temp = $"{_str_itemName}\nPrice - {_str_price}G";

        return str_temp;
    }

    public void ChooseItem()
    {
        if ((isUnlocked && isEquipped) || (!isUnlocked && _items == AD.GameConstants.Item.None))
            return;
        else if (isUnlocked && !isEquipped && _creature == AD.GameConstants.Creature.Player)
            Equip();
        else
            ItemInfo();
    }

    private void Equip()
    {
        AD.Managers.EquipmentM.Equip(_str_itemName);
    }

    private void ItemInfo()
    {
        string str_info = "item info\n";
        string str_plus = string.Empty;
        Dictionary<string, object> dic_item = null;

        if (_items == AD.GameConstants.Item.None)
            dic_item = AD.Managers.DataM._dic_monsters[_creature.ToString()] as Dictionary<string, object>;
        else
        {
            dic_item = AD.Managers.DataM._dic_items[_items.ToString()] as Dictionary<string, object>;
            str_plus = "Plus ";
        }

        str_info += $"- {_str_itemName}\n" +
        $"- {str_plus}HP : {dic_item["Hp"]}\n" +
        $"- {str_plus}Power : {dic_item["Power"]}\n" +
        $"- {str_plus}AttackSpeed : {dic_item["AttackSpeed"]}\n" +
        $"- {str_plus}MoveSpeed : {dic_item["MoveSpeed"]}";

        ShopMan.Instance.ChooseItem(_str_itemName, _str_price, str_info, isItem);
    }
    #endregion
}