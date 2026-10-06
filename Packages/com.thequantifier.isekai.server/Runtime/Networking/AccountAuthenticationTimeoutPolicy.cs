namespace UnityIsekaiGame.Networking.Server
{
    public enum AccountAuthenticationExpiration
    {
        None = 0,
        LoginConnectionIdle = 1,
        AuthenticationRequest = 2
    }

    /// <summary>
    /// Separates the time a person may spend on the login screen from the time allowed for
    /// server-side credential verification. The former is intentionally generous; the latter
    /// detects a stalled authentication worker without making the login UI feel rushed.
    /// </summary>
    public static class AccountAuthenticationTimeoutPolicy
    {
        public const double LoginConnectionIdleTimeoutSeconds = 600d;
        public const double AuthenticationRequestTimeoutSeconds = 15d;

        public static AccountAuthenticationExpiration Evaluate(
            double connectionAcceptedAt,
            double authenticationStartedAt,
            bool authenticationInFlight,
            double now)
        {
            if (authenticationInFlight)
            {
                return now - authenticationStartedAt >= AuthenticationRequestTimeoutSeconds
                    ? AccountAuthenticationExpiration.AuthenticationRequest
                    : AccountAuthenticationExpiration.None;
            }

            return now - connectionAcceptedAt >= LoginConnectionIdleTimeoutSeconds
                ? AccountAuthenticationExpiration.LoginConnectionIdle
                : AccountAuthenticationExpiration.None;
        }
    }
}
