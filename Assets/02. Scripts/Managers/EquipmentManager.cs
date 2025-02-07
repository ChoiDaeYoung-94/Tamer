using System.Collections.Generic;

using UnityEngine;

namespace AD
{
    /// <summary>
    /// EquipmentManager는 장비 관리 기능을 제공
    /// 장비 카테고리별(예: Sword, Shield) 장비 데이터를 관리하고, 장비 착용 및 교체 기능을 수행
    /// </summary>
    public class EquipmentManager
    {
        // 장비 카테고리별로 분류된 장비 이름 목록
        public Dictionary<string, List<string>> SegmentedEquipment = new Dictionary<string, List<string>>();
        private List<string> _swordList = new List<string> { "SimpleSword", "MasterSword" };
        private List<string> _shieldList = new List<string> { "SimpleShield", "MasterShield" };

        // 장비 이름과 해당 장비 GameObject를 매핑 (예: "SimpleSword" → Player.Instance._simpleSword)
        public Dictionary<string, GameObject> EquipmentMapping = new Dictionary<string, GameObject>();

        public void Init()
        {
            // 장비 카테고리 등록
            SegmentedEquipment.Add("Sword", _swordList);
            SegmentedEquipment.Add("Shield", _shieldList);

            // 장비 GameObject 매핑 (Player.Instance의 해당 필드를 사용)
            EquipmentMapping.Add("SimpleSword", Player.Instance._simpleSword);
            EquipmentMapping.Add("MasterSword", Player.Instance._masterSword);
            EquipmentMapping.Add("SimpleShield", Player.Instance._simpleshield);
            EquipmentMapping.Add("MasterShield", Player.Instance._mastershield);

            InitEquip();
        }

        /// <summary>
        /// Player의 _list_playerEquippedItems에 등록된 장비를 활성화
        /// </summary>
        private void InitEquip()
        {
            foreach (string equippedItem in Player.Instance._list_playerEquippedItems)
                EquipmentMapping[equippedItem].SetActive(true);
        }

        /// <summary>
        /// 장비를 착용
        /// 이미 장비되어 있으면 아무런 처리를 하지 않고, 그렇지 않으면 장비 슬롯을 확인하여 교체 후 장비를 적용
        /// </summary>
        public void Equip(string item)
        {
            if (Player.Instance._list_playerEquippedItems.Contains(item))
                return;

            CheckSlotAndEquip(item);

            ShopMan.Instance.ResetItem();
            PlayerUICanvas.Instance.UpdatePlayerInfo();
        }

        /// <summary>
        /// 같은 카테고리 내 기존 장비가 있으면 해제하고, 새로운 장비를 착용
        /// </summary>
        private void CheckSlotAndEquip(string item)
        {
            string newItemCategory = GetEquipmentCategory(item);

            foreach (string equippedItem in Player.Instance._list_playerEquippedItems)
            {
                if (equippedItem == item)
                    continue;

                string equippedCategory = GetEquipmentCategory(equippedItem);
                if (!string.IsNullOrEmpty(newItemCategory) && newItemCategory == equippedCategory)
                {
                    Player.Instance._str_playerEquippedItems =
                        Player.Instance.RemovePrefs(
                            Player.Instance._list_playerEquippedItems,
                            Player.Instance._str_playerEquippedItems,
                            equippedItem,
                            "playerEquippedItems");

                    EquipmentMapping[equippedItem].SetActive(false);
                    Player.Instance.UnequipEquipment(equippedItem);
                    break;
                }
            }

            Player.Instance._str_playerEquippedItems =
                Player.Instance.SavePrefs(
                    Player.Instance._list_playerEquippedItems,
                    Player.Instance._str_playerEquippedItems,
                    item,
                    "playerEquippedItems");

            EquipmentMapping[item].SetActive(true);
            Player.Instance.ApplyEquipment(item);
        }

        /// <summary>
        /// 주어진 장비 이름이 어느 카테고리에 속하는지 반환
        /// </summary>
        private string GetEquipmentCategory(string item)
        {
            foreach (var kvp in SegmentedEquipment)
            {
                if (kvp.Value.Contains(item))
                    return kvp.Key;
            }
            return null;
        }
    }
}