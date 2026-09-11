using UnityEngine;

public class IAPItem : MonoBehaviour
{
    [SerializeField] private AD.GameConstants.IAPItems _IAPItem;
    private ShopMan _shopOwner;

    private void OnEnable()
    {
        Init();
    }

    private void Start()
    {
        _shopOwner = ShopMan.Instance;
        _shopOwner.IAPitemList.Add(this);
    }

    private void OnDestroy()
    {
        if (_shopOwner != null) _shopOwner.IAPitemList.Remove(this);
    }

    public void Init()
    {
        if (AD.Managers.DataM.LocalPlayerData["GooglePlay"].Contains(_IAPItem.ToString()))
            gameObject.SetActive(false);
    }
}
