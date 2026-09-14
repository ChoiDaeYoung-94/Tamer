#if UNITY_EDITOR || TAMER_GAMESAVE_HARNESS
using System;
using UnityEngine;

namespace AD
{
    public sealed class RevivalGameSaveHarness : MonoBehaviour
    {
        private RevivalGameSaveSession _session;
        private string _status = "Offline only. Existing server fields; PlayerPrefs progress excluded.";
#if TAMER_GAMESAVE_HARNESS && !TAMER_GAMESAVE_CLOUD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            RevivalGameSaveSchema.ValidatePackage(Application.identifier);
            if (!Debug.isDebugBuild) throw new InvalidOperationException("Debug player required.");
            var go = new GameObject("Gameplay save offline probe");
            DontDestroyOnLoad(go);
            go.AddComponent<RevivalGameSaveHarness>();
        }
#endif
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception) { _status = "FAILED / rejected; files preserved. See private verification evidence."; }
        }
        private void Open(string slot)
        {
            _session?.Dispose(); _session = null;
            _session = RevivalGameSaveSession.Offline(Application.persistentDataPath, Application.identifier, slot);
            _session.Read();
            _status = "Opened " + slot + "; synthetic server only";
        }
        private void Verify(int step, bool pending)
        {
            _session.Verify(step, pending);
            _status = "PASS step " + step + (pending ? " pending restart" : " acknowledged restore") + "; all 8 fields match";
        }
        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 900f, Screen.width / 900f, 1));
            GUILayout.BeginArea(new Rect(15, 25, 870, 1800), GUI.skin.box);
            GUILayout.Label("GAME SAVE — OFFLINE SYNTHETIC SERVER / NO AUTHENTICATION");
            GUILayout.Label(_status);
            GUILayout.Label(_session == null ? "No session" : "cloud step=" + _session.CloudStep + " pending=" + _session.PendingCount +
                " reads=" + _session.Reads + " writes=" + _session.Writes + " failed=" + _session.HasFailed);
            if (_session != null)
                foreach (string key in RevivalGameSaveSchema.ReadKeys()) GUILayout.Label(key + "=" + _session.Values[key]);
            GUI.enabled = _session == null || !_session.IsBusy;
            if (GUILayout.Button("Open / reconnect primary", GUILayout.Height(75))) Run(() => Open("primary"));
            GUI.enabled = _session != null && _session.CanMutate;
            if (GUILayout.Button("Prepare step 1 locally (do not upload)", GUILayout.Height(75))) Run(() => { _session.Prepare(1); _status = "Step 1 pending; force-stop and restart"; });
            if (GUILayout.Button("Prepare step 2 locally (do not upload)", GUILayout.Height(75))) Run(() => { _session.Prepare(2); _status = "Step 2 pending; force-stop and restart"; });
            if (GUILayout.Button("Upload pending / read back", GUILayout.Height(75))) Run(() => { _session.Upload(); _status = "Upload complete only when failed=false and pending=0"; });
            GUI.enabled = _session != null && !_session.IsBusy;
            if (GUILayout.Button("Verify step 1 pending", GUILayout.Height(75))) Run(() => Verify(1, true));
            if (GUILayout.Button("Verify step 1 acknowledged", GUILayout.Height(75))) Run(() => Verify(1, false));
            if (GUILayout.Button("Verify step 2 pending", GUILayout.Height(75))) Run(() => Verify(2, true));
            if (GUILayout.Button("Verify step 2 acknowledged", GUILayout.Height(75))) Run(() => Verify(2, false));
            if (GUILayout.Button("Fresh read-only restore slot 1", GUILayout.Height(75))) Run(() => { Open("restore1"); Verify(1, false); });
            if (GUILayout.Button("Fresh read-only restore slot 2", GUILayout.Height(75))) Run(() => { Open("restore2"); Verify(2, false); });
            GUI.enabled = true;
            GUILayout.EndArea();
        }
        private void OnDestroy() => _session?.Dispose();
    }
}
#endif
