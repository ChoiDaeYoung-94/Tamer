using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IAPItem : MonoBehaviour
{
    [SerializeField] AD.Define.IAPItems _IAPItem;

    private void Awake()
    {
        ShopMan.Instance._list_IAPitem.Add(this);
        Init();
    }

    public void Init()
    {
        if (AD.Managers.DataM._dic_player["GooglePlay"].Contains(_IAPItem.ToString()))
            gameObject.SetActive(false);
    }
}
