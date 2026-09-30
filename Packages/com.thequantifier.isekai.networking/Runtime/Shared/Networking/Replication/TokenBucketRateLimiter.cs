using System;

namespace UnityIsekaiGame.Networking
{
    public sealed class TokenBucketRateLimiter
    {
        private readonly double capacity;
        private readonly double refillPerSecond;
        private double available;
        private double lastTimestamp;
        private bool initialized;

        public TokenBucketRateLimiter(double capacity, double refillPerSecond)
        {
            if (capacity <= 0d) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (refillPerSecond <= 0d) throw new ArgumentOutOfRangeException(nameof(refillPerSecond));
            this.capacity = capacity;
            this.refillPerSecond = refillPerSecond;
            available = capacity;
        }

        public bool TryConsume(double timestamp, double cost = 1d)
        {
            if (double.IsNaN(timestamp) || double.IsInfinity(timestamp) || cost <= 0d || cost > capacity)
                return false;
            if (!initialized)
            {
                initialized = true;
                lastTimestamp = timestamp;
            }
            else if (timestamp > lastTimestamp)
            {
                available = Math.Min(capacity, available + (timestamp - lastTimestamp) * refillPerSecond);
                lastTimestamp = timestamp;
            }

            if (available + 0.000001d < cost) return false;
            available -= cost;
            return true;
        }

        public void Reset()
        {
            available = capacity;
            lastTimestamp = 0d;
            initialized = false;
        }
    }
}
