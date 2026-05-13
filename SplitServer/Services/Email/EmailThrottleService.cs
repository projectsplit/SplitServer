using Microsoft.Extensions.Caching.Memory;

namespace SplitServer.Services.Email;

public class EmailThrottleService
{
    private const int DefaultBucketCapacity = 5;
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public EmailThrottleService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryConsume(string operation, string ip, string emailOrUserId)
    {
        var key = $"email-throttle:{operation}:{ip}:{emailOrUserId.ToLowerInvariant()}";

        var bucket = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultWindow;
            return new TokenBucket(DefaultBucketCapacity, DefaultWindow);
        })!;

        return bucket.TryConsume();
    }

    private class TokenBucket
    {
        private readonly object _lock = new();
        private readonly int _capacity;
        private readonly TimeSpan _refillInterval;
        private double _tokens;
        private DateTime _lastRefill;

        public TokenBucket(int capacity, TimeSpan refillInterval)
        {
            _capacity = capacity;
            _refillInterval = refillInterval;
            _tokens = capacity;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryConsume()
        {
            lock (_lock)
            {
                Refill();

                if (_tokens < 1)
                {
                    return false;
                }

                _tokens -= 1;
                return true;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastRefill;

            if (elapsed <= TimeSpan.Zero)
            {
                return;
            }

            var refillRatePerSecond = _capacity / _refillInterval.TotalSeconds;
            _tokens = Math.Min(_capacity, _tokens + elapsed.TotalSeconds * refillRatePerSecond);
            _lastRefill = now;
        }
    }
}
