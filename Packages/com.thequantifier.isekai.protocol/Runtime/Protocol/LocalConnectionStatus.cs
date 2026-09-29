using System;

namespace UnityIsekaiGame.Networking
{
    public enum LocalConnectionPhase
    {
        Offline = 0,
        StartingServer = 10,
        Listening = 20,
        Connecting = 30,
        Connected = 40,
        Disconnecting = 50,
        Failed = 60
    }

    public readonly struct LocalConnectionStatus : IEquatable<LocalConnectionStatus>
    {
        public LocalConnectionStatus(LocalConnectionPhase phase, string message, LocalServerEndpoint endpoint = default)
        {
            Phase = phase;
            Message = message ?? string.Empty;
            Endpoint = endpoint;
        }

        public LocalConnectionPhase Phase { get; }
        public string Message { get; }
        public LocalServerEndpoint Endpoint { get; }
        public bool IsOnline => Phase == LocalConnectionPhase.Listening || Phase == LocalConnectionPhase.Connected;

        public bool Equals(LocalConnectionStatus other) =>
            Phase == other.Phase && string.Equals(Message, other.Message, StringComparison.Ordinal) && Endpoint.Equals(other.Endpoint);

        public override bool Equals(object obj) => obj is LocalConnectionStatus other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Phase, Message, Endpoint);
        public override string ToString() => string.IsNullOrEmpty(Message) ? Phase.ToString() : $"{Phase}: {Message}";
    }
}
