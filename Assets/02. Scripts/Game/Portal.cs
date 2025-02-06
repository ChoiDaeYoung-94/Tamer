using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Portal : MonoBehaviour
{
    static Portal instance;
    public static Portal Instance { get { return instance; } }

    private enum PortalType
    {
        MoveA,
        MoveB,
        Heal
    }

    [Header("--- 세팅 ---")]
    [SerializeField] PortalType _portalType;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        instance = null;
    }

    #region Functions
    private void MoveA()
    {
        Player.Instance.transform.position = new Vector3(-30f, 0f, -10f);
    }

    private void MoveB()
    {
        Player.Instance.transform.position = new Vector3(35f, 0f, 15f);
    }

    private void Heal()
    {
        AD.Managers.PopupM.PopupHeal();
    }

    public void RewardHeal()
    {
        AD.Managers.SoundM.UnpauseBGM();

        Player.Instance.Heal();
    }
    #endregion

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            switch (_portalType)
            {
                case PortalType.MoveA:
                    MoveA();
                    break;
                case PortalType.MoveB:
                    MoveB();
                    break;
                case PortalType.Heal:
                    Heal();
                    break;
            }
        }
    }
}
