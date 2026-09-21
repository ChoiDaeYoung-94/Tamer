namespace AD
{
    // A lease belongs to one manager instance, authenticated generation and pooled activation.
    public sealed class GameplaySessionLease
    {
        private object _manager;
        private string _owner;
        private int _generation;
        public int Lifetime { get; private set; }
        private bool _rewardConsumed;

        public void Bind(object manager, string owner, int generation, bool ready)
        {
            Invalidate();
            if (!ready || manager == null || string.IsNullOrEmpty(owner)) return;
            _manager = manager; _owner = owner; _generation = generation;
        }

        public void Invalidate() { Lifetime++; _manager = null; _owner = null; _rewardConsumed = false; }

        public bool Matches(object manager, string owner, int generation, bool ready, int lifetime)
            => ready && _manager != null && ReferenceEquals(_manager, manager) &&
                _owner == owner && _generation == generation && Lifetime == lifetime;

        public bool TryConsumeReward(object manager, string owner, int generation, bool ready, int lifetime)
        {
            if (_rewardConsumed || !Matches(manager, owner, generation, ready, lifetime)) return false;
            _rewardConsumed = true;
            return true;
        }
    }
}
