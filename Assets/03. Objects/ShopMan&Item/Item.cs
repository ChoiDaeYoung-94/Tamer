using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Item : MonoBehaviour
{
    [SerializeField] AD.Define.Items _items;
    [SerializeField] AD.Define.Creature _creature;
    [SerializeField] TMP_Text _TMP_info = null;

    private void Awake()
    {
        Init();
    }

    #region Functions

    private void Init()
    {
        string str_itemName = _items == AD.Define.Items.None ? _creature.ToString() : _items.ToString();
        string str_price = _items == AD.Define.Items.None ? ((Dictionary<string, object>)AD.Managers.DataM._dic_monsters[str_itemName])["Price"].ToString()
                                                      : ((Dictionary<string, object>)AD.Managers.DataM._dic_items[str_itemName])["Price"].ToString();

        string str_currency = str_itemName == AD.Define.Items.NoADs.ToString() ? "$" : "G";

        _TMP_info.text = $"{str_itemName}\nPrice - {str_price}{str_currency}";
    }

    public void ChooseItem()
    {
        string str_info = "item info\n";
        string str_itemName = string.Empty;
        string str_plus = string.Empty;
        Dictionary<string, object> dic_item = null;

        if (_items == AD.Define.Items.None)
        {
            dic_item = AD.Managers.DataM._dic_monsters[_creature.ToString()] as Dictionary<string, object>;
            str_itemName = _creature.ToString();
        }
        else
        {
            dic_item = AD.Managers.DataM._dic_items[_items.ToString()] as Dictionary<string, object>;
            str_itemName = _items.ToString();
            str_plus = "Plus ";
        }

        if (_items == AD.Define.Items.NoADs)
        {
            str_info += $"- {str_itemName}\nEarn rewards without watching in-game ads.";
        }
        else
        {
            str_info += $"- {str_itemName}\n" +
            $"- {str_plus}HP : {dic_item["Hp"]}\n" +
            $"- {str_plus}Power : {dic_item["Power"]}\n" +
            $"- {str_plus}AttackSpeed : {dic_item["AttackSpeed"]}\n" +
            $"- {str_plus}MoveSpeed : {dic_item["MoveSpeed"]}";
        }

        ShopMan.Instance.ChooseItem(str_info);
    }

    #endregion
}