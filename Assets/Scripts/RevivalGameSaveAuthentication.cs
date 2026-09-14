#if UNITY_EDITOR || TAMER_GAMESAVE_CLOUD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;

namespace AD
{
    // Only this class can issue a cloud binding. No caller-supplied receipt/session ticket API.
    public sealed class RevivalGameSaveAuthentication : IDisposable
    {
        private sealed class Binding
        {
            public string Account;
            public PlayFabAuthenticationContext Context;
            public PlayFabClientInstanceAPI Client;
        }
        private Binding _binding;
        private readonly List<RevivalGameSaveSession> _sessions = new List<RevivalGameSaveSession>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private int _generation;
        private bool _disposed;
        public bool IsLoggingIn { get; private set; }
        public bool IsAuthenticated => !_disposed && _binding != null;
        public string Status { get; private set; } = "Manual dedicated-account authentication required";
#if UNITY_EDITOR
        private readonly Action<LoginWithCustomIDRequest, Action<LoginResult>, Action> _testLogin;
        private readonly Action<TimeSpan, Action> _testSchedule;
        private RevivalGameSaveAuthentication(Action<LoginWithCustomIDRequest, Action<LoginResult>, Action> testLogin,
            Action<TimeSpan, Action> testSchedule)
        { _testLogin = testLogin; _testSchedule = testSchedule; }
#endif
        public RevivalGameSaveAuthentication() { }

        public void Login(string package, string title, string customId, string expectedAccount)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RevivalGameSaveAuthentication));
            // Every attempt invalidates earlier sessions, even if new input is invalid.
            Disconnect();
            RevivalGameSaveSchema.ValidateCloudTarget(package, title);
            var request = RevivalGameSaveSchema.CreateLoginRequest(customId);
            if (expectedAccount == null || !Regex.IsMatch(expectedAccount, "\\A[A-Fa-f0-9]{8,32}\\z"))
                throw new ArgumentException("Administrator-confirmed expected account required.");
            int generation = _generation;
            IsLoggingIn = true;
            Status = "Authenticating provisioned gameplay test account";
            Action fail = () =>
            {
                if (_disposed || generation != _generation || !IsLoggingIn) return;
                IsLoggingIn = false;
                Status = "Authentication failed or timed out; no save session opened";
            };
            Action<LoginResult> success = result =>
            {
                if (_disposed || generation != _generation || !IsLoggingIn) return;
                if (result == null || result.NewlyCreated || string.IsNullOrWhiteSpace(result.SessionTicket) ||
                    !string.Equals(result.PlayFabId, expectedAccount, StringComparison.OrdinalIgnoreCase))
                { fail(); return; }
                IsLoggingIn = false;
                var context = new PlayFabAuthenticationContext
                    { PlayFabId = result.PlayFabId, ClientSessionTicket = result.SessionTicket };
                _binding = new Binding { Account = result.PlayFabId, Context = context,
                    Client = new PlayFabClientInstanceAPI(Settings(), context) };
                Status = "Expected gameplay test account authenticated; explicit read required";
            };
#if UNITY_EDITOR
            if (_testSchedule != null) _testSchedule(TimeSpan.FromSeconds(20), fail);
            else
#endif
                ExpireAsync(fail, _lifetime.Token).Forget();
            try
            {
#if UNITY_EDITOR
                if (_testLogin != null) { _testLogin(request, success, fail); return; }
#endif
                var client = new PlayFabClientInstanceAPI(Settings(), new PlayFabAuthenticationContext());
                client.LoginWithCustomID(request, success, error => fail());
            }
            catch (Exception) { fail(); }
        }
        private static PlayFabApiSettings Settings() => new PlayFabApiSettings
            { TitleId = RevivalGameSaveSchema.TestTitle, DisableDeviceInfo = true, DisableFocusTimeCollection = true };
        private static async UniTask ExpireAsync(Action expire, CancellationToken token)
        {
            if (await UniTask.Delay(TimeSpan.FromSeconds(20), ignoreTimeScale: true, cancellationToken: token)
                .SuppressCancellationThrow()) return;
            expire();
        }
        public RevivalGameSaveSession Connect(string root, string package, string title, string slot)
        {
            RevivalGameSaveSchema.ValidateCloudTarget(package, title);
            var bound = _binding;
            if (!IsAuthenticated || bound == null) throw new InvalidOperationException("Verified gameplay login required.");
            int generation = _generation;
            Func<bool> current = () => !_disposed && generation == _generation && ReferenceEquals(_binding, bound);
            string path = RevivalGameSaveSchema.SavePath(root, package, "cloud", slot);
            var session = new RevivalGameSaveSession(path, bound.Account, current, slot != "primary",
                (account, ok, fail) =>
                {
                    if (!current() || account != bound.Account) { fail(403); return; }
                    bound.Client.GetUserData(new GetUserDataRequest
                    { PlayFabId = bound.Account, Keys = RevivalGameSaveSchema.ReadKeys(), AuthenticationContext = bound.Context }, result =>
                    {
                        if (!current()) return;
                        if (result?.Data == null) { fail(400); return; }
                        ok(result.Data.ToDictionary(e => e.Key, e => e.Value?.Value));
                    }, error => { if (current()) fail(error?.HttpCode ?? 0); });
                },
                (account, patch, ok, fail) =>
                {
                    if (!current() || account != bound.Account) { fail(403); return; }
                    RevivalGameSaveSchema.ValidatePatch(patch);
                    bound.Client.UpdateUserData(ServerManager.CreateWriteRequest(patch, bound.Context),
                        result => { if (current()) ok(); }, error => { if (current()) fail(error?.HttpCode ?? 0); });
                });
            _sessions.Add(session);
            return session;
        }
        public void Disconnect()
        {
            ++_generation;
            IsLoggingIn = false;
            _binding = null;
            foreach (var session in _sessions) session.Dispose();
            _sessions.Clear();
            Status = "Disconnected; saved files preserved";
        }
        public void Dispose()
        {
            if (_disposed) return;
            Disconnect(); _disposed = true;
            _lifetime.Cancel(); _lifetime.Dispose();
        }
    }
}
#endif
