using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IAPItem : MonoBehaviour
{
    [SerializeField] AD.Define.IAPItems _IAPItem;

    private void OnEnable()
    {
        Init();
    }

    private void Start()
    {
        ShopMan.Instance._list_IAPitem.Add(this);
    }

    public void Init()
    {
        if (AD.Managers.DataM._dic_player["GooglePlay"].Contains(_IAPItem.ToString()))
            gameObject.SetActive(false);
    }
}
