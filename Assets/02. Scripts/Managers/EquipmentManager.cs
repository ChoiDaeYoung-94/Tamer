using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AD
{
    public class EquipmentManager
    {
        public Dictionary<string, List<string>> _dic_segmentedEquipment = new Dictionary<string, List<string>>();
        public List<string> _list_sword = new List<string> { "SimpleSword", "MasterSword" };
        public List<string> _list_shield = new List<string> { "SimpleShield", "MasterShield" };

        public Dictionary<string, GameObject> equipmentMapping = new Dictionary<string, GameObject>();

        internal void Init()
        {
            _dic_segmentedEquipment.Add("Sword", _list_sword);
            _dic_segmentedEquipment.Add("Shield", _list_shield);

            equipmentMapping.Add("SimpleSword", Player.Instance._simpleSword);
            equipmentMapping.Add("MasterSword", Player.Instance._masterSword);
            equipmentMapping.Add("SimpleShield", Player.Instance._simpleshield);
            equipmentMapping.Add("MasterShield", Player.Instance._mastershield);
        }

        public void Equip(string item)
        {
            if (Player.Instance._list_playerEquippedItems.Contains(item))
                return;

            ChcekSlotAndEquip(item);

            ShopMan.Instance.ResetItem();
        }

        private void ChcekSlotAndEquip(string item)
        {
            foreach (string temp_str in Player.Instance._list_playerEquippedItems)
            {
                if (temp_str == item)
                    continue;

                if (_dic_segmentedEquipment["Sword"].Contains(temp_str) && _dic_segmentedEquipment["Sword"].Contains(item) ||
                    _dic_segmentedEquipment["Shield"].Contains(temp_str) && _dic_segmentedEquipment["Shield"].Contains(item))
                {
                    Player.Instance.RemovePrefs(Player.Instance._list_playerEquippedItems, Player.Instance._str_playerEquippedItems, temp_str, "playerEquippedItems");
                    equipmentMapping[temp_str].SetActive(false);
                    break;
                }
            }

            Player.Instance.SavePrefs(Player.Instance._list_playerEquippedItems, Player.Instance._str_playerEquippedItems, item, "playerEquippedItems");
            equipmentMapping[item].SetActive(true);
        }
    }
}