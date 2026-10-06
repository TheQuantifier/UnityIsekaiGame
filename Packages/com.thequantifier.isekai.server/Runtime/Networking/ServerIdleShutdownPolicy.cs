using System;

namespace UnityIsekaiGame.Networking.Server
{
    /// <summary>
    /// Tracks how long an authoritative server has had no remote transport clients.
    /// The policy deliberately counts app-authenticated clients that are still on the login screen,
    /// rather than only account-authenticated player sessions.
    /// </summary>
    public sealed class ServerIdleShutdownPolicy
    {
        public const float DefaultTimeoutSeconds = 120f;

        private double idleStartedAt = double.NaN;

        public bool IsCountingDown => !double.IsNaN(idleStartedAt);
        public double IdleStartedAt => idleStartedAt;

        public bool Observe(double realtimeSeconds, bool hasRemoteClients, float timeoutSeconds)
        {
            if (hasRemoteClients || timeoutSeconds <= 0f)
            {
                Reset();
                return false;
            }

            if (!IsCountingDown)
            {
                idleStartedAt = realtimeSeconds;
                return false;
            }

            return realtimeSeconds - idleStartedAt >= timeoutSeconds;
        }

        public void Reset()
        {
            idleStartedAt = double.NaN;
        }
    }
}
