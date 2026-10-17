#if UNITY_EDITOR || TAMER_IAP_HARNESS
using AD;
using UnityEngine;

/// <summary>Explicit development-only controls; never initiates a purchase automatically.</summary>
public sealed class RevivalIapHarness : MonoBehaviour
{
    private string _customId = "", _expectedAccount = "";
    private bool _resumeConfirmed;
    private bool _reopenProfile;
    private Vector2 _scroll;
    private void Update() => RevivalIapIsolation.Tick();
    private void OnDestroy()
    {
        _customId = ""; _expectedAccount = "";
        RevivalIapIsolation.CancelRecovery();
    }
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
        GUILayout.BeginArea(new Rect(10, 10, 880, Mathf.Min(1000, Screen.height * 900f / Screen.width - 20)), GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Label("IAP TEST ACCOUNT RECOVERY — existing approved test account only");
        GUILayout.Label("Two prior receipt failures: STOP. No new purchase, automatic retry or progress probe.");
        GUILayout.Label("Original identity, progress, inventory, backups and No Ads stay in place.");
        GUILayout.Label("A NEW local profile starts at schema defaults. No existing progress is copied.");
        GUILayout.Label(RevivalIapIsolation.Status);
        var iap = Managers.IAPM;
        GUILayout.Label("Store: " + (iap == null ? "Waiting for managers" : iap.Status.ToString()));
        if (iap != null && !string.IsNullOrEmpty(iap.VerificationDiagnostic))
            GUILayout.Label("Receipt check: " + iap.VerificationDiagnostic);
        if (iap != null && iap.VerificationRetryStopped)
            GUILayout.Label("Approved recovery failed: STOP. Further execution requires coordinator review.");
        if (Managers.DataM != null && !RevivalIapIsolation.CanPrepare && !RevivalIapIsolation.IsStaging && RevivalIapIsolation.Session == null)
            GUILayout.Label("Recovery unavailable: retained save/deletion/purchase state or prior preparation requires coordinator review.");
        GUI.enabled = RevivalIapIsolation.CanPrepare;
        GUILayout.Label("Enter existing full test CustomID privately (masked; memory only)");
        _customId = GUILayout.PasswordField(_customId, '*', 128, GUILayout.Height(55));
        GUILayout.Label("Enter expected existing account ID privately (masked; never a login credential)");
        _expectedAccount = GUILayout.PasswordField(_expectedAccount, '*', 32, GUILayout.Height(55));
        _reopenProfile = GUILayout.Toggle(_reopenProfile, "Reauthenticate the previously committed recovery profile (no new blank profile)");
        if (GUILayout.Button("Authenticate existing account / prepare new local profile", GUILayout.Height(65)))
        {
            var credential = _customId; var expected = _expectedAccount;
            _customId = ""; _expectedAccount = "";
            RevivalIapIsolation.LoginExisting(credential, expected, _reopenProfile);
        }
        GUI.enabled = true;
        if (RevivalIapIsolation.IsStaging && GUILayout.Button("Cancel recovery staging", GUILayout.Height(60)))
            RevivalIapIsolation.CancelRecovery();
        GUILayout.Label(RevivalIapIsolation.ResumeBuildApproved
            ? "Coordinator-approved build: separately approved one-time restore only."
            : "Execution locked in this build. Coordinator approval and validation are still required.");
        GUI.enabled = RevivalIapIsolation.ResumeBuildApproved && RevivalIapIsolation.Session != null;
        _resumeConfirmed = GUILayout.Toggle(_resumeConfirmed, "Coordinator has recorded user approval to resume THIS failed receipt once");
        GUI.enabled = GUI.enabled && _resumeConfirmed;
        if (GUILayout.Button("Run approved recovery ONCE / fetch owned purchases", GUILayout.Height(65)))
        {
            RevivalIapIsolation.StartApprovedRecovery(_resumeConfirmed);
            _resumeConfirmed = false;
        }
        GUI.enabled = true;
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
#endif
