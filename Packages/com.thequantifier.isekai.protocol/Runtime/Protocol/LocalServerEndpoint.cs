using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace UnityIsekaiGame.Networking
{
    public readonly struct LocalServerEndpoint : IEquatable<LocalServerEndpoint>
    {
        public const string DefaultClientAddress = "127.0.0.1";
        public const string DefaultListenAddress = "0.0.0.0";
        public const ushort DefaultPort = 7777;
        public const uint DefaultTickRate = 60;

        public LocalServerEndpoint(string address, ushort port)
        {
            Address = address;
            Port = port;
        }

        public string Address { get; }
        public ushort Port { get; }

        public static bool TryCreate(string address, int port, out LocalServerEndpoint endpoint, out string failure)
        {
            endpoint = default;
            string candidate = address?.Trim() ?? string.Empty;
            if (!IsValidAddress(candidate))
            {
                failure = "The server address must be an IPv4 address or a valid host name.";
                return false;
            }

            if (port < 1 || port > ushort.MaxValue)
            {
                failure = $"The server port must be between 1 and {ushort.MaxValue}.";
                return false;
            }

            endpoint = new LocalServerEndpoint(candidate.ToLowerInvariant(), (ushort)port);
            failure = string.Empty;
            return true;
        }

        public bool Equals(LocalServerEndpoint other) =>
            string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase) && Port == other.Port;

        public override bool Equals(object obj) => obj is LocalServerEndpoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Address?.ToLowerInvariant(), Port);
        public override string ToString() => $"{Address}:{Port}";

        private static bool IsValidAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || address.Length > 253)
            {
                return false;
            }

            if (IPAddress.TryParse(address, out IPAddress parsed))
            {
                return parsed.AddressFamily == AddressFamily.InterNetwork;
            }

            if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] labels = address.Split('.');
            return labels.All(IsValidHostLabel);
        }

        private static bool IsValidHostLabel(string label)
        {
            if (string.IsNullOrEmpty(label) || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-')
            {
                return false;
            }

            return label.All(character => char.IsLetterOrDigit(character) || character == '-');
        }
    }
}
