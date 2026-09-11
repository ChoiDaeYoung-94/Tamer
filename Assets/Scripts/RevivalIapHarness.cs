#if TAMER_IAP_HARNESS
using AD;
using UnityEngine;

/// <summary>Explicit development-only controls; never initiates a purchase automatically.</summary>
public sealed class RevivalIapHarness : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        RevivalIapIsolation.ValidateRuntime();
        var root = new GameObject("Isolated IAP test controls");
        DontDestroyOnLoad(root);
        root.AddComponent<RevivalIapHarness>();
    }
    private void OnGUI()
    {
        GUI.depth = -2000;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 900f, Screen.width / 900f, 1));
        GUILayout.BeginArea(new Rect(10, 10, 880, 460), GUI.skin.box);
        GUILayout.Label("IAP TEST APP — use an approved Google license tester only");
        GUILayout.Label(RevivalIapIsolation.Status);
        var iap = Managers.IAPM;
        GUILayout.Label("Store: " + (iap == null ? "Waiting for managers" : iap.Status.ToString()));
        GUI.enabled = Managers.DataM != null && RevivalIapIsolation.Session == null;
        if (GUILayout.Button("Log into dedicated test title", GUILayout.Height(65))) RevivalIapIsolation.Login();
        GUI.enabled = iap != null && RevivalIapIsolation.Session != null && Managers.DataM.IsServerDataReady;
        if (GUILayout.Button("Connect store / fetch owned purchases", GUILayout.Height(65))) iap.InitializePurchasing();
        if (GUILayout.Button("Restore purchases", GUILayout.Height(65))) iap.RestorePurchases();
        GUI.enabled = GUI.enabled && iap.IsReadyToPurchase;
        if (GUILayout.Button("Open Google purchase sheet — verify TEST payment", GUILayout.Height(65)))
            iap.BuyProductID(iap.ProductNoAds);
        GUI.enabled = true;
        GUILayout.EndArea();
    }
}
#endif
