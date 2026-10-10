using System;
using System.Collections.Generic;
using FivePRS.Core.Config;

namespace FivePRS.Server.Security
{
    public enum RateLimitResult
    {
        Allowed,
        Limited,
        LimitedFirst,
    }

    public sealed class RateLimiter
    {
        private sealed class Window
        {
            public DateTime StartedAt;
            public int Count;
            public bool Reported;
        }

        private readonly Func<DateTime> _clock;
        private readonly Func<bool> _enabled;
        private readonly Dictionary<string, Dictionary<string, Window>> _windows = new();

        public RateLimiter(Func<DateTime> clock, Func<bool> enabled)
        {
            _clock = clock;
            _enabled = enabled;
        }

        public RateLimitResult Check(string key, string bucket, RateLimit limit)
        {
            if (!_enabled() || limit.Limit <= 0 || limit.PerSeconds <= 0) return RateLimitResult.Allowed;

            if (!_windows.TryGetValue(key, out var buckets))
                _windows[key] = buckets = new Dictionary<string, Window>();

            var now = _clock();
            if (!buckets.TryGetValue(bucket, out var window) || (now - window.StartedAt).TotalSeconds >= limit.PerSeconds)
                buckets[bucket] = window = new Window { StartedAt = now };

            if (window.Count < limit.Limit)
            {
                window.Count++;
                return RateLimitResult.Allowed;
            }

            if (window.Reported) return RateLimitResult.Limited;
            window.Reported = true;
            return RateLimitResult.LimitedFirst;
        }

        public void Forget(string key) => _windows.Remove(key);
    }
}
