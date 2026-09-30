using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Networking
{
    public enum LocalNetworkLaunchMode
    {
        None = 0,
        Server = 1,
        Client = 2
    }

    public readonly struct LocalNetworkLaunchOptions
    {
        public LocalNetworkLaunchOptions(
            LocalNetworkLaunchMode mode,
            string serverAddress,
            string listenAddress,
            ushort port,
            string playerId,
            int maximumPlayers,
            string authenticationToken)
        {
            Mode = mode;
            ServerAddress = serverAddress;
            ListenAddress = listenAddress;
            Port = port;
            PlayerId = playerId;
            MaximumPlayers = maximumPlayers;
            AuthenticationToken = authenticationToken ?? string.Empty;
        }

        public LocalNetworkLaunchMode Mode { get; }
        public string ServerAddress { get; }
        public string ListenAddress { get; }
        public ushort Port { get; }
        public string PlayerId { get; }
        public int MaximumPlayers { get; }
        public string AuthenticationToken { get; }
    }

    public static class LocalNetworkCommandLine
    {
        public const string ServerFlag = "--local-server";
        public const string ClientFlag = "--local-client";

        public static bool TryParse(
            IReadOnlyList<string> arguments,
            bool dedicatedServerBuild,
            out LocalNetworkLaunchOptions options,
            out string failure)
        {
            bool serverRequested = dedicatedServerBuild;
            bool clientRequested = false;
            string serverAddress = LocalServerEndpoint.DefaultClientAddress;
            string listenAddress = LocalServerEndpoint.DefaultListenAddress;
            int port = LocalServerEndpoint.DefaultPort;
            string playerId = "local-player";
            int maximumPlayers = 8;
            string authenticationToken = string.Empty;

            for (int index = 0; index < (arguments?.Count ?? 0); index++)
            {
                string argument = arguments[index] ?? string.Empty;
                if (string.Equals(argument, ServerFlag, StringComparison.OrdinalIgnoreCase))
                {
                    serverRequested = true;
                    continue;
                }

                if (string.Equals(argument, ClientFlag, StringComparison.OrdinalIgnoreCase))
                {
                    clientRequested = true;
                    continue;
                }

                if (!TryReadValue(arguments, ref index, "--server-address", out string value, out bool matched, out failure))
                {
                    options = default;
                    return false;
                }

                if (matched)
                {
                    serverAddress = value;
                    continue;
                }

                if (!TryReadValue(arguments, ref index, "--listen-address", out value, out matched, out failure))
                {
                    options = default;
                    return false;
                }

                if (matched)
                {
                    listenAddress = value;
                    continue;
                }

                if (!TryReadValue(arguments, ref index, "--server-port", out value, out matched, out failure))
                {
                    options = default;
                    return false;
                }

                if (matched)
                {
                    if (!int.TryParse(value, out port))
                    {
                        options = default;
                        failure = $"'{value}' is not a valid server port.";
                        return false;
                    }

                    continue;
                }

                if (!TryReadValue(arguments, ref index, "--player-id", out value, out matched, out failure))
                {
                    options = default;
                    return false;
                }

                if (matched)
                {
                    playerId = value;
                    continue;
                }

                if (!TryReadValue(arguments, ref index, "--max-players", out value, out matched, out failure))
                {
                    options = default;
                    return false;
                }

                if (matched && (!int.TryParse(value, out maximumPlayers) || maximumPlayers < 1))
                {
                    options = default;
                    failure = $"'{value}' is not a valid maximum player count.";
                    return false;
                }

                if (!TryReadValue(arguments, ref index, "--auth-token", out value, out matched, out failure))
                {
                    options = default;
                    return false;
                }

                if (matched)
                {
                    if (value.Length > LocalConnectionProtocol.MaximumAuthenticationTokenLength)
                    {
                        options = default;
                        failure = "The authentication token is too long.";
                        return false;
                    }

                    authenticationToken = value;
                }
            }

            if (serverRequested && clientRequested)
            {
                options = default;
                failure = "A process cannot launch as both a local server and a local client.";
                return false;
            }

            LocalNetworkLaunchMode mode = serverRequested
                ? LocalNetworkLaunchMode.Server
                : clientRequested ? LocalNetworkLaunchMode.Client : LocalNetworkLaunchMode.None;
            string endpointAddress = mode == LocalNetworkLaunchMode.Server ? listenAddress : serverAddress;
            if (!LocalServerEndpoint.TryCreate(endpointAddress, port, out _, out failure))
            {
                options = default;
                return false;
            }

            if (!LocalConnectionProtocol.IsValidIdentifier(playerId))
            {
                options = default;
                failure = "The command-line player ID is invalid.";
                return false;
            }

            options = new LocalNetworkLaunchOptions(
                mode,
                serverAddress.Trim().ToLowerInvariant(),
                listenAddress.Trim().ToLowerInvariant(),
                (ushort)port,
                playerId,
                maximumPlayers,
                authenticationToken);
            failure = string.Empty;
            return true;
        }

        private static bool TryReadValue(
            IReadOnlyList<string> arguments,
            ref int index,
            string option,
            out string value,
            out bool matched,
            out string failure)
        {
            string argument = arguments[index] ?? string.Empty;
            string prefix = option + "=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                matched = true;
                value = argument.Substring(prefix.Length);
                failure = string.Empty;
                return RequireValue(option, value, out failure);
            }

            if (!string.Equals(argument, option, StringComparison.OrdinalIgnoreCase))
            {
                matched = false;
                value = string.Empty;
                failure = string.Empty;
                return true;
            }

            matched = true;
            if (index + 1 >= arguments.Count)
            {
                value = string.Empty;
                failure = $"{option} requires a value.";
                return false;
            }

            value = arguments[++index] ?? string.Empty;
            return RequireValue(option, value, out failure);
        }

        private static bool RequireValue(string option, string value, out string failure)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                failure = string.Empty;
                return true;
            }

            failure = $"{option} requires a value.";
            return false;
        }
    }
}
