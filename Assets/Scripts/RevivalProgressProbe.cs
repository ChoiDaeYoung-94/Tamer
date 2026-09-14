#if UNITY_EDITOR || TAMER_IAP_HARNESS || TAMER_PROGRESS_HARNESS
using System;
using System.Collections.Generic;
using System.Globalization;
using AD.Purchasing;
using PlayFab;
using PlayFab.ClientModels;

namespace AD
{
    /// <summary>Explicit single-key synthetic progress probe; never applies data to the game save.</summary>
    public sealed class RevivalProgressProbe : IDisposable
    {
        public const string Key = "RevivalProgressProbeV1";
        private readonly Func<ReceiptSession> _session;
        private readonly ReceiptSession _bound;
        private readonly ServerManager _server;
        private bool _disposed;
        private bool _readReady;
        public bool CanSave => IsCurrent() && _readReady && !_server.IsInProgress && !_server.HasFailed;
        private int? _restoredStep;
        public int? RestoredStep { get => IsCurrent() ? _restoredStep : null; private set => _restoredStep = value; }
        public bool IsBusy => _server.IsInProgress;
        public string Status => _disposed ? "Probe closed" : !IsCurrent() ? "Session changed; reconnect probe" :
            _server.HasFailed ? "Request failed; no restore confirmed" : _server.IsInProgress ? "Test cloud request pending" : _status;
        private string _status = "Read test progress first";

        public static bool AllowsPackage(string package)
        {
#if UNITY_EDITOR
            return package == "com.AeDeong.MonsterTamer.iaptest" || package == "com.AeDeong.MonsterTamer.revival.progress";
#elif TAMER_PROGRESS_HARNESS
            return package == "com.AeDeong.MonsterTamer.revival.progress";
#else
            return package == "com.AeDeong.MonsterTamer.iaptest";
#endif
        }

        public static RevivalProgressProbe Connect(string title, string package, Func<ReceiptSession> session)
        {
            if (!string.Equals(title, "12B656", StringComparison.OrdinalIgnoreCase) || !AllowsPackage(package))
                throw new InvalidOperationException("Approved isolated title and package required.");
            var bound = session?.Invoke();
            if (bound == null || string.IsNullOrWhiteSpace(bound.AccountId) || string.IsNullOrWhiteSpace(bound.SessionTicket))
                throw new InvalidOperationException("Existing authenticated test session required.");
            var context = new PlayFabAuthenticationContext { PlayFabId = bound.AccountId, ClientSessionTicket = bound.SessionTicket };
            var client = new PlayFabClientInstanceAPI(new PlayFabApiSettings
            { TitleId = title, DisableDeviceInfo = true, DisableFocusTimeCollection = true }, context);
            return new RevivalProgressProbe(session,
                (account, ok, fail) => client.GetUserData(new GetUserDataRequest
                { PlayFabId = account, Keys = new List<string> { Key } }, result =>
                {
                    if (result?.Data == null) { fail(400); return; }
                    var values = new Dictionary<string, string>();
                    if (result.Data.TryGetValue(Key, out var record)) values.Add(Key, record?.Value);
                    ok(values);
                }, error => fail(error?.HttpCode ?? 0)),
                (account, patch, ok, fail) =>
                {
                    ValidatePatch(patch);
                    client.UpdateUserData(ServerManager.CreateWriteRequest(patch, context),
                        result => ok(), error => fail(error?.HttpCode ?? 0));
                });
        }

        // Injected transport is for synthetic regression only; Connect supplies the real instance SDK.
        public RevivalProgressProbe(Func<ReceiptSession> session,
            Action<string, Action<Dictionary<string, string>>, Action<int>> read,
            Action<string, Dictionary<string, string>, Action, Action<int>> write)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _bound = session() ?? throw new InvalidOperationException("Session required.");
            _server = new ServerManager(() => IsCurrent() ? _bound.AccountId : null,
                IsCurrent, read, (account, patch, ok, fail) => { ValidatePatch(patch); write(account, patch, ok, fail); },
                (snapshot, update) =>
                {
                    if (!IsCurrent()) throw new InvalidOperationException();
                    if (!snapshot.TryGetValue(Key, out var value))
                    { RestoredStep = null; _readReady = true; _status = "No dedicated test progress saved"; return; }
                    RestoredStep = Parse(value);
                    _readReady = true;
                    _status = "Test cloud progress read: step " + RestoredStep.Value;
                });
        }

        private bool IsCurrent()
        {
            var current = _session();
            return !_disposed && ReferenceEquals(current, _bound);
        }
        public void Read()
        {
            if (!IsCurrent() || IsBusy) return;
            RestoredStep = null;
            _readReady = false;
            _server.GetAllData();
        }
        public void Save(int step)
        {
            if (!CanSave) return;
            if (step < 0 || step > 9999) throw new ArgumentOutOfRangeException(nameof(step));
            RestoredStep = null;
            _readReady = false;
            _server.SetData(new Dictionary<string, string> { [Key] = "v1:" + step.ToString(CultureInfo.InvariantCulture) }, getAllData: true);
        }
        public static void ValidatePatch(Dictionary<string, string> patch)
        {
            if (patch == null || patch.Count != 1 || !patch.TryGetValue(Key, out var value))
                throw new InvalidOperationException("Only the dedicated progress key may be patched.");
            Parse(value);
        }
        private static int Parse(string value)
        {
            if (value == null || !value.StartsWith("v1:", StringComparison.Ordinal) ||
                !int.TryParse(value.Substring(3), NumberStyles.None, CultureInfo.InvariantCulture, out var step) || step > 9999)
                throw new InvalidOperationException("Invalid test progress; preserved without overwrite.");
            return step;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _server.Dispose(); RestoredStep = null; }
    }
}
#endif
