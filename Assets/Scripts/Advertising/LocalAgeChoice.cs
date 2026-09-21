using System;

namespace AD.Advertising
{
    public enum AgeChoice { Unknown, Under13, From13To15, From16To17, Adult, Declined }

    /// <summary>Device-local choice only. No date of birth, account, or network payload.</summary>
    public sealed class LocalAgeChoice
    {
        private readonly Action<string> _save;
        public AgeChoice Value { get; private set; }
        public bool IsEditing { get; private set; }
        public bool NeedsQuestion => Value == AgeChoice.Unknown || IsEditing;
        public bool HasAge => !IsEditing && Value != AgeChoice.Unknown && Value != AgeChoice.Declined;
        public event Action Changed;

        public LocalAgeChoice(Func<string> load, Action<string> save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            try { Value = Decode(load()); }
            catch (Exception) { Value = AgeChoice.Unknown; }
        }

        public void BeginEdit()
        {
            if (IsEditing) return;
            IsEditing = true;
            Changed?.Invoke();
        }

        public bool Select(AgeChoice choice)
        {
            string encoded = Encode(choice);
            if (encoded == null) return false;
            // Invalidate before persistence or callbacks can re-enter a request.
            BeginEdit();
            try { _save(encoded); }
            catch (Exception) { return false; }
            Value = choice;
            IsEditing = false;
            Changed?.Invoke();
            return true;
        }

        public static string Encode(AgeChoice choice)
        {
            switch (choice)
            {
                case AgeChoice.Under13: return "1|under13";
                case AgeChoice.From13To15: return "1|13to15";
                case AgeChoice.From16To17: return "1|16to17";
                case AgeChoice.Adult: return "1|18plus";
                case AgeChoice.Declined: return "1|declined";
                default: return null;
            }
        }

        public static AgeChoice Decode(string encoded)
        {
            switch (encoded)
            {
                case "1|under13": return AgeChoice.Under13;
                case "1|13to15": return AgeChoice.From13To15;
                case "1|16to17": return AgeChoice.From16To17;
                case "1|18plus": return AgeChoice.Adult;
                case "1|declined": return AgeChoice.Declined;
                default: return AgeChoice.Unknown;
            }
        }
    }
}
