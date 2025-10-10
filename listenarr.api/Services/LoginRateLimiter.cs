using System.Collections.Concurrent;

namespace Listenarr.Api.Services
{
    public interface ILoginRateLimiter
    {
        bool IsBlocked(string key);
        void RecordFailure(string key);
        void RecordSuccess(string key);
        /// <summary>
        /// If the key is blocked, returns remaining block duration in seconds; otherwise 0.
        /// </summary>
        int GetSecondsUntilUnblock(string key);
    }

    public class LoginRateLimiter : ILoginRateLimiter
    {
        private class Entry { public int Failures; public DateTime? BlockUntil; }
        private readonly ConcurrentDictionary<string, Entry> _map = new();

        // Configurable thresholds
        private readonly int _maxFailures = 5;
        private readonly TimeSpan _blockDuration = TimeSpan.FromMinutes(10);

        public bool IsBlocked(string key)
        {
            if (_map.TryGetValue(key, out var e))
            {
                if (e.BlockUntil.HasValue && e.BlockUntil.Value > DateTime.UtcNow) return true;
            }
            return false;
        }

        public int GetSecondsUntilUnblock(string key)
        {
            if (_map.TryGetValue(key, out var e))
            {
                if (e.BlockUntil.HasValue)
                {
                    var ts = e.BlockUntil.Value - DateTime.UtcNow;
                    return ts.Ticks > 0 ? (int)Math.Ceiling(ts.TotalSeconds) : 0;
                }
            }
            return 0;
        }

        public void RecordFailure(string key)
        {
            var entry = _map.GetOrAdd(key, _ => new Entry());
            entry.Failures++;
            if (entry.Failures >= _maxFailures)
            {
                entry.BlockUntil = DateTime.UtcNow.Add(_blockDuration);
            }
        }

        public void RecordSuccess(string key)
        {
            _map.TryRemove(key, out _);
        }
    }
}
