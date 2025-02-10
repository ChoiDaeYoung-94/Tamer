using UnityEngine;

public class IAPItem : MonoBehaviour
{
    [SerializeField] private AD.GameConstants.IAPItem _IAPItem;

    private void OnEnable()
    {
        Init();
    }

    private void Start()
    {
        ShopMan.Instance.IAPitemList.Add(this);
    }

    public void Init()
    {
        if (AD.Managers.DataM.LocalPlayerData["GooglePlay"].Contains(_IAPItem.ToString()))
            gameObject.SetActive(false);
    }
}
