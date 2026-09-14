#if UNITY_EDITOR || TAMER_IAP_HARNESS
using AD;
using UnityEngine;

/// <summary>Explicit development-only controls; never initiates a purchase automatically.</summary>
public sealed class RevivalIapHarness : MonoBehaviour
{
    private RevivalProgressProbe _progress;
    private string _progressError;
    private void OnDestroy() => _progress?.Dispose();
#if TAMER_IAP_HARNESS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        RevivalIapIsolation.ValidateRuntime();
        var root = new GameObject("Isolated IAP test controls");
        DontDestroyOnLoad(root);
        root.AddComponent<RevivalIapHarness>();
    }
#endif
    private void OnGUI()
    {
        GUI.depth = -2000;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 900f, Screen.width / 900f, 1));
        GUILayout.BeginArea(new Rect(10, 10, 880, 760), GUI.skin.box);
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
        GUILayout.Label("Dedicated cloud test progress only — game save / No Ads unchanged");
        GUILayout.Label(_progressError ?? _progress?.Status ?? "Progress probe not connected");
        GUI.enabled = RevivalIapIsolation.Session != null && (_progress == null || !_progress.IsBusy);
        if (GUILayout.Button("Reconnect probe / read cloud test progress", GUILayout.Height(65)))
        {
            _progress?.Dispose();
            _progress = null;
            _progressError = null;
            try { _progress = RevivalIapIsolation.CreateProgressProbe(); _progress.Read(); }
            catch (System.Exception) { _progressError = "Probe unavailable; existing data preserved"; }
        }
        GUI.enabled = _progress != null && _progress.CanSave;
        if (GUILayout.Button("Save synthetic test step 1 / read back", GUILayout.Height(65))) _progress.Save(1);
        if (GUILayout.Button("Save synthetic test step 2 / read back", GUILayout.Height(65))) _progress.Save(2);
        GUI.enabled = true;
        GUILayout.EndArea();
    }
}
#endif
