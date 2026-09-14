#if UNITY_EDITOR || TAMER_GAMESAVE_CLOUD
using System;
using UnityEngine;

namespace AD
{
    public sealed class RevivalGameSaveCloudHarness : MonoBehaviour
    {
        private readonly RevivalGameSaveAuthentication _authentication = new RevivalGameSaveAuthentication();
        private RevivalGameSaveSession _session;
        private string _customId = "";
        private string _expectedAccount = "";
        private string _status = "Personal phone and newly provisioned gameplay account required";
        private Vector2 _scroll;
#if TAMER_GAMESAVE_CLOUD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            RevivalGameSaveSchema.ValidateCloudTarget(Application.identifier, RevivalGameSaveSchema.TestTitle);
            if (!Debug.isDebugBuild) throw new InvalidOperationException("Dedicated debug cloud player required.");
            var root = new GameObject("Game-save cloud verification");
            DontDestroyOnLoad(root);
            root.AddComponent<RevivalGameSaveCloudHarness>();
        }
#endif
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception) { _status = "Rejected or failed; no automatic recovery or overwrite"; }
        }
        private void Login()
        {
            _session = null;
            try
            {
                _authentication.Login(Application.identifier, RevivalGameSaveSchema.TestTitle, _customId, _expectedAccount);
                _status = "Wait for expected-account authentication; then explicitly open primary";
            }
            finally { _customId = ""; _expectedAccount = ""; GUI.FocusControl(null); }
        }
        private void Open(string slot)
        {
            _session?.Dispose(); _session = null;
            _session = _authentication.Connect(Application.persistentDataPath, Application.identifier, RevivalGameSaveSchema.TestTitle, slot);
            _session.Read();
            _status = "Reading " + slot + "; wait for busy=false / failed=false, then verify";
        }
        private void Verify(int step, bool pending)
        {
            _session.Verify(step, pending);
            _status = "PASS all eight fields: step " + step + (pending ? " with pending journal" : " acknowledged restore");
        }
        private void OnGUI()
        {
            float scale = Screen.width / 900f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(15, 20, 870, Screen.height / scale - 40), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("CLOUD GAME SAVE — TEST TITLE / SYNTHETIC EIGHT FIELDS ONLY");
            GUILayout.Label(_authentication.Status);
            GUILayout.Label(_status);
            GUI.enabled = !_authentication.IsLoggingIn;
            GUILayout.Label("New gameplay-save CustomID (manual entry; never saved)");
            _customId = GUILayout.PasswordField(_customId, '*', 64, GUILayout.Height(60));
            GUILayout.Label("Expected PlayFab ID confirmed by administrator (manual entry)");
            _expectedAccount = GUILayout.PasswordField(_expectedAccount, '*', 32, GUILayout.Height(60));
            if (GUILayout.Button("Authenticate provisioned gameplay account", GUILayout.Height(75))) Run(Login);
            GUI.enabled = true;
            if (GUILayout.Button("Disconnect / cancel authentication (preserve files)", GUILayout.Height(65)))
            {
                _authentication.Disconnect(); _session = null; _customId = ""; _expectedAccount = "";
                _status = "Disconnected";
            }
            GUILayout.Label(_session == null ? "No save session" : "step=" + _session.CloudStep + " pending=" + _session.PendingCount +
                " busy=" + _session.IsBusy + " failed=" + _session.HasFailed + " reads=" + _session.Reads + " writes=" + _session.Writes);
            if (_session != null)
                foreach (string key in RevivalGameSaveSchema.ReadKeys()) GUILayout.Label(key + "=" + _session.Values[key]);
            GUI.enabled = _authentication.IsAuthenticated && (_session == null || !_session.IsBusy);
            if (GUILayout.Button("Open / reconnect primary and read", GUILayout.Height(75))) Run(() => Open("primary"));
            if (GUILayout.Button("Open NEW read-only restore slot 1", GUILayout.Height(75))) Run(() => Open("restore1"));
            if (GUILayout.Button("Open NEW read-only restore slot 2", GUILayout.Height(75))) Run(() => Open("restore2"));
            GUI.enabled = _session != null && _session.CanMutate;
            if (GUILayout.Button("Prepare synthetic step 1 locally", GUILayout.Height(75))) Run(() => { _session.Prepare(1); _status = "Step 1 pending locally; upload explicitly"; });
            if (GUILayout.Button("Prepare synthetic step 2 locally", GUILayout.Height(75))) Run(() => { _session.Prepare(2); _status = "Step 2 pending locally; upload explicitly"; });
            if (GUILayout.Button("Upload pending / read back", GUILayout.Height(75))) Run(() => { _session.Upload(); _status = "Upload requested; wait for busy=false and verify"; });
            GUI.enabled = _session != null && !_session.IsBusy;
            if (GUILayout.Button("Verify step 1 pending", GUILayout.Height(65))) Run(() => Verify(1, true));
            if (GUILayout.Button("Verify step 1 acknowledged", GUILayout.Height(65))) Run(() => Verify(1, false));
            if (GUILayout.Button("Verify step 2 pending", GUILayout.Height(65))) Run(() => Verify(2, true));
            if (GUILayout.Button("Verify step 2 acknowledged", GUILayout.Height(65))) Run(() => Verify(2, false));
            GUI.enabled = true;
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void OnDestroy()
        {
            _customId = ""; _expectedAccount = "";
            _authentication.Dispose(); _session = null;
        }
    }
}
#endif
