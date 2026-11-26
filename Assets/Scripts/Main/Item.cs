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
    private ShopMan _shopOwner;

    private void Awake()
    {
        _shopOwner = ShopMan.Instance;
        _shopOwner.ItemList.Add(this);
        Init();
    }

    private void OnDestroy()
    {
        if (_shopOwner != null) _shopOwner.ItemList.Remove(this);
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
        _lockIcon.SetActive(!_isUnlocked);

        if (_isUnlocked)
        {
            if (_creatureType == AD.GameConstants.Creatures.Player)
                itemInfo = DisplayName(_itemName);
        }

        if (_isEquipped)
            itemInfo += "\n장착 중";

        _itemInfoText.text = itemInfo;
    }

    private string GetItemInfo()
    {
        var dataSource = _itemType == AD.GameConstants.Items.None
            ? AD.Managers.DataM.MonsterData[_itemName] as Dictionary<string, object>
            : AD.Managers.DataM.ItemData[_itemName] as Dictionary<string, object>;

        _itemPrice = dataSource["Price"].ToString();
        return $"{DisplayName(_itemName)}\n{DisplayPrice(_itemPrice)} 골드";
    }

    // Display captions never replace the identifiers used by inventory, saves or purchases.
    public static string DisplayName(string itemName)
    {
        switch (itemName)
        {
            case "SimpleSword": return "기본 검";
            case "MasterSword": return "달인의 검";
            case "SimpleShield": return "기본 방패";
            case "MasterShield": return "달인의 방패";
            case "Bat": return "박쥐";
            case "Magma": return "마그마";
            case "Chest": return "보물상자";
            case "Crab": return "게";
            case "RatAssassin": return "암살쥐";
            case "Spider": return "거미";
            case "SpiderToxin": return "독거미";
            case "SpiderKing": return "거미왕";
            case "LizardWarrior": return "도마뱀 전사";
            case "Werewolf": return "늑대인간";
            case "Beholder": return "비홀더";
            case "FylingDemon": return "날개 악마";
            default: return itemName;
        }
    }

    public static string DisplayPrice(string price) =>
        int.TryParse(price, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int value)
            ? value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : price;

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

        string prefix = _itemType != AD.GameConstants.Items.None ? "추가 " : "";

        string itemInfo = $"상품 정보\n" +
            $"- {DisplayName(_itemName)}\n" +
            $"- 가격: {DisplayPrice(_itemPrice)} 골드\n" +
            $"- {prefix}체력 : {dataSource["Hp"]}\n" +
            $"- {prefix}공격력 : {dataSource["Power"]}\n" +
            $"- {prefix}공격 속도 : {dataSource["AttackSpeed"]}\n" +
            $"- {prefix}이동 속도 : {dataSource["MoveSpeed"]}";

        ShopMan.Instance.ChooseItem(_itemName, _itemPrice, itemInfo, _isItem);
    }
}
