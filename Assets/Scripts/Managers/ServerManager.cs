using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Cysharp.Threading.Tasks;

namespace AD
{
    /// <summary>
    /// Serial, account-scoped PlayFab requests. A failed group stops queued work;
    /// cancellation invalidates callbacks but cannot undo an already accepted server write.
    /// </summary>
    public class ServerManager : IDisposable
    {
        public bool IsInProgress => _active != null || _pending.Count != 0;
        public bool HasFailed { get; private set; }

        private const int ChunkSize = 10;
        private const int MaxAttempts = 3;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
        private readonly Func<string> _accountId;
        private readonly Func<bool> _canWrite;
        private readonly Action<string, Action<Dictionary<string, string>>, Action<int>> _read;
        private readonly Action<string, Dictionary<string, string>, Action, Action<int>> _write;
        private readonly Action<TimeSpan, Action> _schedule;
        private readonly Action<Dictionary<string, string>, bool> _applyRead;
        private readonly Queue<Operation> _pending = new Queue<Operation>();
        private Operation _active;
        private int _generation;
        private int _requestVersion;
        private bool _disposed;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private sealed class Operation
        {
            public string AccountId;
            public int Generation;
            public List<KeyValuePair<string, string>> Snapshot;
            public int Offset;
            public bool ReadAfterWrite;
            public bool Update;
            public Action OnWritten;
        }

        public ServerManager() : this(Managers.DataM) { }

        // Production callbacks stay bound to their owner, never a replacement singleton.
        public ServerManager(DataManager data) : this(
            () => data != null ? data.PlayFabId : null,
            () => data != null && data.IsServerDataReady,
            ReadFromPlayFab, WriteToPlayFab,
            (delay, action) => { },
            (snapshot, update) => ApplyServerData(data, snapshot, update))
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _schedule = Schedule;
        }

        // Dependencies use plain values so isolated tests never need Managers or a PlayFab session.
        public ServerManager(Func<string> accountId, Func<bool> canWrite,
            Action<string, Action<Dictionary<string, string>>, Action<int>> read,
            Action<string, Dictionary<string, string>, Action, Action<int>> write,
            Action<Dictionary<string, string>, bool> applyRead)
            : this(accountId, canWrite, read, write, (delay, action) => { }, applyRead)
        {
            _schedule = Schedule;
        }

        public ServerManager(Func<string> accountId, Func<bool> canWrite,
            Action<string, Action<Dictionary<string, string>>, Action<int>> read,
            Action<string, Dictionary<string, string>, Action, Action<int>> write,
            Action<TimeSpan, Action> schedule, Action<Dictionary<string, string>, bool> applyRead)
        {
            _accountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
            _canWrite = canWrite ?? throw new ArgumentNullException(nameof(canWrite));
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _write = write ?? throw new ArgumentNullException(nameof(write));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _applyRead = applyRead ?? throw new ArgumentNullException(nameof(applyRead));
        }

        public void GetAllData(bool update = false)
        {
            Enqueue(new Operation { Update = update });
        }

        /// <summary>Copies values now; caller mutations and later requests cannot alter this upload.</summary>
        public void SetData(Dictionary<string, string> dic, bool getAllData = false,
            bool update = false, Action onWritten = null)
        {
            Enqueue(new Operation
            {
                Snapshot = dic == null ? new List<KeyValuePair<string, string>>() : dic.ToList(),
                ReadAfterWrite = getAllData,
                Update = update,
                OnWritten = onWritten
            });
        }

        /// <summary>Missing server fields are resolved locally; never seed them from an unproven cache.</summary>
        public void UpdateNewPlayerData() => GetAllData(update: true);

        public void DeleteData(Dictionary<string, string> dic, bool update = false)
        {
            var deletions = new Dictionary<string, string>();
            if (dic != null)
                foreach (string key in dic.Keys) deletions.Add(key, null);
            SetData(deletions, getAllData: true, update: update);
        }

        /// <summary>Call before an account/login attempt changes. Late callbacks become inert.</summary>
        public void CancelPendingRequests()
        {
            ++_generation;
            ++_requestVersion;
            _active = null;
            _pending.Clear();
            HasFailed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelPendingRequests();
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        [Obsolete("ServerManager owns completion. Use CancelPendingRequests to stop requests.")]
        public void SetInProgress(bool value)
        {
            if (!value) CancelPendingRequests();
        }

        private void Enqueue(Operation operation)
        {
            if (_disposed) return;
            if (!IsInProgress) HasFailed = false;
            operation.AccountId = _accountId();
            operation.Generation = _generation;
            if (string.IsNullOrWhiteSpace(operation.AccountId) ||
                (operation.Snapshot != null && !_canWrite()))
            {
                Abort("Request rejected before account data was ready.");
                return;
            }
            _pending.Enqueue(operation);
            if (_active == null) StartNext();
        }

        private void StartNext()
        {
            if (_pending.Count == 0) return;
            _active = _pending.Dequeue();
            Continue(_active);
        }

        private bool IsCurrent(Operation operation)
        {
            if (_disposed) return false;
            if (!ReferenceEquals(_active, operation) || operation.Generation != _generation) return false;
            if (string.Equals(operation.AccountId, _accountId(), StringComparison.Ordinal)) return true;
            Abort("Account changed while a request was pending.");
            return false;
        }

        private void Continue(Operation operation)
        {
            if (!IsCurrent(operation)) return;
            if (operation.Snapshot == null)
            {
                Read(operation);
                return;
            }
            if (!_canWrite())
            {
                Abort("Account data is no longer ready for writes.");
                return;
            }
            if (operation.Offset < operation.Snapshot.Count)
            {
                int count = Math.Min(ChunkSize, operation.Snapshot.Count - operation.Offset);
                var chunk = new Dictionary<string, string>();
                for (int i = 0; i < count; ++i)
                {
                    var pair = operation.Snapshot[operation.Offset + i];
                    chunk.Add(pair.Key, pair.Value);
                }
                Dispatch(operation,
                    (success, failure) => _write(operation.AccountId,
                        new Dictionary<string, string>(chunk), success, failure),
                    () => { operation.Offset += count; Continue(operation); });
                return;
            }
            Action onWritten = operation.OnWritten;
            operation.OnWritten = null;
            try { onWritten?.Invoke(); }
            catch (Exception) { Abort("Upload completion could not be applied locally."); return; }
            if (!IsCurrent(operation)) return;
            if (operation.ReadAfterWrite) Read(operation);
            else Complete(operation);
        }

        private void Read(Operation operation)
        {
            Dictionary<string, string> response = null;
            Dispatch(operation,
                (success, failure) => _read(operation.AccountId,
                    data => { response = data; success(); }, failure),
                () =>
                {
                    // Never interpret a missing/malformed response as a new, empty account.
                    if (response == null) throw new InvalidOperationException("Missing server data.");
                    _applyRead(new Dictionary<string, string>(response), operation.Update);
                    Complete(operation);
                });
        }

        private void Dispatch(Operation operation, Action<Action, Action<int>> send,
            Action success, int attempt = 0)
        {
            if (!IsCurrent(operation)) return;
            int version = ++_requestVersion;
            bool settled = false;
            Func<bool> claim = () =>
            {
                if (settled || version != _requestVersion || !IsCurrent(operation)) return false;
                settled = true;
                return true;
            };
            Action<int> fail = code =>
            {
                if (!claim()) return;
                bool transient = code == 0 || code == 408 || code == 429 || (code >= 500 && code <= 599);
                if (transient && attempt + 1 < MaxAttempts)
                {
                    _schedule(TimeSpan.FromSeconds(1 << attempt), () =>
                    {
                        if (version == _requestVersion && IsCurrent(operation))
                            Dispatch(operation, send, success, attempt + 1);
                    });
                }
                else Abort("Server request failed or timed out; queued work stopped.");
            };
            _schedule(RequestTimeout, () => fail(408));
            try
            {
                send(() =>
                {
                    if (!claim()) return;
                    try { success(); }
                    catch (Exception) { Abort("Server response could not be applied safely."); }
                }, fail);
            }
            catch (Exception) { fail(400); }
        }

        private void Complete(Operation operation)
        {
            if (!IsCurrent(operation)) return;
            _active = null;
            StartNext();
        }

        private void Abort(string message)
        {
            CancelPendingRequests();
            // Never log response bodies, account identifiers, or tokens.
            Debug.LogWarning("[Tamer/Server] " + message);
        }

        private void Schedule(TimeSpan delay, Action action) => DelayAsync(delay, action, _lifetime.Token).Forget();

        private static async UniTask DelayAsync(TimeSpan delay, Action action, CancellationToken token)
        {
            if (await UniTask.Delay(delay, ignoreTimeScale: true, cancellationToken: token)
                .SuppressCancellationThrow()) return;
            action();
        }

        private static PlayFabAuthenticationContext CopyContext(string accountId)
        {
            var current = PlayFabSettings.staticPlayer;
            if (!current.IsClientLoggedIn() || !string.Equals(accountId, current.PlayFabId, StringComparison.Ordinal))
                throw new InvalidOperationException("The authenticated account changed.");
            var context = new PlayFabAuthenticationContext();
            context.CopyFrom(current);
            return context;
        }

        private static void ReadFromPlayFab(string accountId,
            Action<Dictionary<string, string>> success, Action<int> failure)
        {
            var request = new GetUserDataRequest { PlayFabId = accountId, AuthenticationContext = CopyContext(accountId) };
            PlayFabClientAPI.GetUserData(request, result =>
            {
                if (result?.Data == null) { success(null); return; }
                var data = new Dictionary<string, string>();
                foreach (var pair in result.Data) data.Add(pair.Key, pair.Value?.Value);
                success(data);
            }, error => failure(error?.HttpCode ?? 0));
        }

        private static void WriteToPlayFab(string accountId, Dictionary<string, string> data,
            Action success, Action<int> failure)
        {
            var request = CreateWriteRequest(data, CopyContext(accountId));
            PlayFabClientAPI.UpdateUserData(request, result => success(), error => failure(error?.HttpCode ?? 0));
        }

        internal static UpdateUserDataRequest CreateWriteRequest(Dictionary<string, string> data,
            PlayFabAuthenticationContext context)
        {
            return new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string>(data),
                Permission = UserDataPermission.Private,
                AuthenticationContext = context
            };
        }

        private static void ApplyServerData(DataManager owner, Dictionary<string, string> data, bool update)
        {
            if (owner == null) throw new InvalidOperationException("The data owner was destroyed.");
            var records = new Dictionary<string, UserDataRecord>();
            foreach (var pair in data) records.Add(pair.Key, new UserDataRecord { Value = pair.Value });
            owner.PlayFabPlayerData = records;
            if (update) owner.UpdateData();
            else owner.IsConflict = false;
        }
    }
}
