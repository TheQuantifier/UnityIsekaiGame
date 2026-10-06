using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace UnityIsekaiGame.Networking
{
    public enum AccountAuthenticationMode
    {
        Login = 1,
        CreateAccount = 2
    }

    public static class LocalConnectionProtocol
    {
        // Version 5 adds generation-tagged inventory and combat snapshot records. Older
        // clients must be rejected before NGO attempts to deserialize the changed wire shape.
        public const int CurrentVersion = 5;
        public const int MaximumPayloadBytes = 2048;
        public const int MaximumIdentifierLength = 64;
        public const int MaximumBuildVersionLength = 64;
        public const int MaximumAuthenticationTokenLength = 128;
        public const int MinimumAccountNameLength = 3;
        public const int MaximumAccountNameLength = 32;
        public const int MinimumPasswordLength = 8;
        public const int MaximumPasswordLength = 128;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None
        };

        public static bool TryEncode(ConnectionRequestPayload request, out byte[] payload, out string failure)
        {
            payload = Array.Empty<byte>();
            if (!Validate(request, out failure))
            {
                return false;
            }

            try
            {
                payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request, SerializerSettings));
                if (payload.Length > MaximumPayloadBytes)
                {
                    payload = Array.Empty<byte>();
                    failure = $"The connection request exceeds {MaximumPayloadBytes} bytes.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }
            catch (JsonException exception)
            {
                failure = $"The connection request could not be encoded: {exception.Message}";
                return false;
            }
        }

        public static bool TryDecode(byte[] payload, out ConnectionRequestPayload request, out string failure)
        {
            request = null;
            if (payload == null || payload.Length == 0)
            {
                failure = "The connection request is empty.";
                return false;
            }

            if (payload.Length > MaximumPayloadBytes)
            {
                failure = $"The connection request exceeds {MaximumPayloadBytes} bytes.";
                return false;
            }

            try
            {
                request = JsonConvert.DeserializeObject<ConnectionRequestPayload>(Encoding.UTF8.GetString(payload), SerializerSettings);
            }
            catch (JsonException exception)
            {
                failure = $"The connection request is malformed: {exception.Message}";
                return false;
            }

            return Validate(request, out failure);
        }

        public static bool Validate(ConnectionRequestPayload request, out string failure)
        {
            if (request == null)
            {
                failure = "The connection request is missing.";
                return false;
            }

            if (request.ProtocolVersion != CurrentVersion)
            {
                failure = $"Protocol version {request.ProtocolVersion} is not supported. Expected {CurrentVersion}.";
                return false;
            }

            if (!IsValidIdentifier(request.ClientInstanceId))
            {
                failure = "The client instance ID is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.BuildVersion)
                || request.BuildVersion.Length > MaximumBuildVersionLength
                || !string.Equals(request.BuildVersion, request.BuildVersion.Trim(), StringComparison.Ordinal))
            {
                failure = "The client build version is invalid.";
                return false;
            }

            if (request.AuthenticationToken == null
                || request.AuthenticationToken.Length > MaximumAuthenticationTokenLength
                || !string.Equals(request.AuthenticationToken, request.AuthenticationToken.Trim(), StringComparison.Ordinal))
            {
                failure = "The client authentication token is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static bool IsValidIdentifier(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Length <= MaximumIdentifierLength
                && string.Equals(value, value.Trim(), StringComparison.Ordinal)
                && value.All(character => char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == '.');
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ConnectionRequestPayload
    {
        public ConnectionRequestPayload()
        {
        }

        public ConnectionRequestPayload(
            string clientInstanceId,
            string buildVersion,
            string authenticationToken)
        {
            ProtocolVersion = LocalConnectionProtocol.CurrentVersion;
            ClientInstanceId = clientInstanceId;
            BuildVersion = buildVersion;
            AuthenticationToken = authenticationToken ?? string.Empty;
        }

        [JsonProperty("protocolVersion", Required = Required.Always)]
        public int ProtocolVersion { get; set; }

        [JsonProperty("clientInstanceId", Required = Required.Always)]
        public string ClientInstanceId { get; set; } = string.Empty;

        [JsonProperty("buildVersion", Required = Required.Always)]
        public string BuildVersion { get; set; } = string.Empty;

        [JsonProperty("authenticationToken", Required = Required.Always)]
        public string AuthenticationToken { get; set; } = string.Empty;

    }

    public readonly struct ConnectionAdmissionResult
    {
        private ConnectionAdmissionResult(bool approved, string reason, ConnectionRequestPayload request)
        {
            Approved = approved;
            Reason = reason ?? string.Empty;
            Request = request;
        }

        public bool Approved { get; }
        public string Reason { get; }
        public ConnectionRequestPayload Request { get; }

        public static ConnectionAdmissionResult Approve(ConnectionRequestPayload request) =>
            new ConnectionAdmissionResult(true, string.Empty, request);

        public static ConnectionAdmissionResult Reject(string reason) =>
            new ConnectionAdmissionResult(false, reason, null);
    }

    public static class LocalConnectionAdmission
    {
        public static ConnectionAdmissionResult Evaluate(
            byte[] payload,
            int connectedPlayerCount,
            int maximumPlayers,
            IEnumerable<string> connectedPlayerIds,
            string expectedBuildVersion = null,
            string expectedAuthenticationToken = null)
        {
            if (maximumPlayers < 1)
            {
                return ConnectionAdmissionResult.Reject("The server is not accepting players.");
            }

            if (connectedPlayerCount >= maximumPlayers)
            {
                return ConnectionAdmissionResult.Reject("The server is full.");
            }

            if (!LocalConnectionProtocol.TryDecode(payload, out ConnectionRequestPayload request, out string failure))
            {
                return ConnectionAdmissionResult.Reject(failure);
            }

            if (!string.IsNullOrWhiteSpace(expectedBuildVersion)
                && !string.Equals(request.BuildVersion, expectedBuildVersion, StringComparison.Ordinal))
            {
                return ConnectionAdmissionResult.Reject(
                    $"Client build '{request.BuildVersion}' is incompatible with server build '{expectedBuildVersion}'.");
            }

            if (!string.IsNullOrWhiteSpace(expectedAuthenticationToken)
                && !FixedTimeEquals(request.AuthenticationToken, expectedAuthenticationToken))
            {
                return ConnectionAdmissionResult.Reject("Client authentication failed.");
            }

            return ConnectionAdmissionResult.Approve(request);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
            byte[] rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
            int difference = leftBytes.Length ^ rightBytes.Length;
            int length = Math.Max(leftBytes.Length, rightBytes.Length);
            for (int i = 0; i < length; i++)
            {
                byte a = i < leftBytes.Length ? leftBytes[i] : (byte)0;
                byte b = i < rightBytes.Length ? rightBytes[i] : (byte)0;
                difference |= a ^ b;
            }

            return difference == 0;
        }
    }
}
