using System;
using System.Collections.Generic;

namespace AD
{
    internal static class LoginGuestEntryPolicy
    {
        public static bool CanOffer(bool knownOwner, bool localProgress, string mode,
            string googleId, string customId, bool deletion, bool deletionPause,
            bool receiptRecovery, bool googleCreationPending, bool scopeApproved) =>
            string.IsNullOrEmpty(customId) && LoginContinuityPolicy.CanOfferGoogleSignup(
                knownOwner, localProgress, mode, googleId, deletion, deletionPause,
                receiptRecovery, googleCreationPending, scopeApproved);
    }

    /// <summary>A first-login choice belongs to one unplayed manager generation.</summary>
    internal sealed class LoginGuestEntryGate
    {
        private object _owner;
        private int _generation;
        private int _deletionEpoch;
        public long Version { get; private set; }

        public bool Offer(object owner, int generation, int deletionEpoch, bool eligible)
        {
            Revoke();
            Version++;
            if (owner == null || !eligible) return false;
            _owner = owner;
            _generation = generation;
            _deletionEpoch = deletionEpoch;
            return true;
        }

        public bool TryChoose(object owner, int generation, int deletionEpoch, long version, bool eligible)
        {
            if (version != Version) return false; // An old callback must not revoke a newer offer.
            bool allowed = _owner != null && ReferenceEquals(_owner, owner) &&
                _generation == generation && _deletionEpoch == deletionEpoch && eligible;
            Revoke(); // One click, including a rejected or stale click, consumes the offer.
            return allowed;
        }

        public void Revoke() => _owner = null;
    }

    /// <summary>Persist before authentication; uncertain writes cannot start an SDK in this process.</summary>
    internal sealed class LoginGuestIdentityPreparation
    {
        public const string CustomIdKey = "AD_CustomId";
        public const string ModeKey = "AD_LoginMode";
        public const string GoogleIdKey = "AD_LastGpgsId";
        public const string GooglePendingKey = "AD_GoogleSignupPending";
        public const string Prefix = "tamer-guest-v1-";
        private readonly HashSet<string> _uncertain = new HashSet<string>(StringComparer.Ordinal);
        public bool HasUncertainWrites => _uncertain.Count != 0;

        public static string NewIdentity() => Prefix + Guid.NewGuid().ToString("N");
        public static bool IsGuestIdentity(string value) =>
            value != null && value.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool IsValidGuestIdentity(string value) => IsGuestIdentity(value) &&
            Guid.TryParseExact(value.Substring(Prefix.Length), "N", out var id) && id != Guid.Empty;

        public static bool IsConsistent(string mode, string value) => !IsGuestIdentity(value) ||
            (IsValidGuestIdentity(value) && (mode == "custom" || mode == "custom-pending"));

        public bool IsBlockedUntilRestart(string value) => value != null && _uncertain.Contains(value);

        public bool TryPrepare(Func<string, string> read, Action<string, string> write,
            Action flush, Func<string> generate, Func<bool> current)
        {
            string identity = null;
            bool writing = false;
            try
            {
                if (HasUncertainWrites || !current() || !string.IsNullOrEmpty(read(ModeKey)) ||
                    !string.IsNullOrEmpty(read(CustomIdKey)) || !string.IsNullOrEmpty(read(GoogleIdKey)) ||
                    !string.IsNullOrEmpty(read(GooglePendingKey))) return false;
                identity = generate();
                if (!IsValidGuestIdentity(identity) || !current()) return false;
                writing = true;
                write(CustomIdKey, identity);
                write(ModeKey, "custom-pending");
                flush();
                // Read-back is not a substitute for a successful flush. On a write/flush
                // exception the process-wide instance blocks this ID until a fresh launch.
                if (!current() || read(CustomIdKey) != identity || read(ModeKey) != "custom-pending")
                    throw new InvalidOperationException("Guest setup changed during persistence.");
                return true;
            }
            catch (Exception)
            {
                if (writing && identity != null) _uncertain.Add(identity);
                return false;
            }
        }
    }
}
