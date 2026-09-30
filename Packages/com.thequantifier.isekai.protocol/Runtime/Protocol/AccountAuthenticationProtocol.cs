using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace UnityIsekaiGame.Networking
{
    public static class AccountAuthenticationProtocol
    {
        public const string RequestMessageName = "unity-isekai/account-auth/request/v1";
        public const string ResponseMessageName = "unity-isekai/account-auth/response/v1";
        public const int CurrentVersion = 1;
        public const int SecureUserIdLength = 64;
        public const int MaximumPayloadBytes = 2048;

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None
        };

        public static bool TryEncodeRequest(AccountAuthenticationRequest request, out byte[] payload, out string failure) =>
            TryEncode(request, ValidateRequest, out payload, out failure);

        public static bool TryDecodeRequest(byte[] payload, out AccountAuthenticationRequest request, out string failure) =>
            TryDecode(payload, ValidateRequest, out request, out failure);

        public static bool TryEncodeResponse(AccountAuthenticationResponse response, out byte[] payload, out string failure) =>
            TryEncode(response, ValidateResponse, out payload, out failure);

        public static bool TryDecodeResponse(byte[] payload, out AccountAuthenticationResponse response, out string failure) =>
            TryDecode(payload, ValidateResponse, out response, out failure);

        public static bool ValidateRequest(AccountAuthenticationRequest request, out string failure)
        {
            if (request == null || request.ProtocolVersion != CurrentVersion)
            {
                failure = "The account authentication request uses an unsupported protocol version.";
                return false;
            }

            if (!Enum.IsDefined(typeof(AccountAuthenticationMode), request.Mode))
            {
                failure = "The account authentication mode is invalid.";
                return false;
            }

            string identifier = request.AccountIdentifier ?? string.Empty;
            bool validUsername = identifier.Length >= LocalConnectionProtocol.MinimumAccountNameLength
                && identifier.Length <= LocalConnectionProtocol.MaximumAccountNameLength
                && LocalConnectionProtocol.IsValidIdentifier(identifier);
            bool validUserId = IsSecureUserId(identifier);
            if ((request.Mode == AccountAuthenticationMode.CreateAccount && !validUsername)
                || (request.Mode == AccountAuthenticationMode.Login && !validUsername && !validUserId))
            {
                failure = request.Mode == AccountAuthenticationMode.CreateAccount
                    ? $"Use {LocalConnectionProtocol.MinimumAccountNameLength}-{LocalConnectionProtocol.MaximumAccountNameLength} letters, numbers, periods, underscores, or hyphens for the username."
                    : "Enter a valid username or user ID.";
                return false;
            }

            if (request.Password == null
                || request.Password.Length < LocalConnectionProtocol.MinimumPasswordLength
                || request.Password.Length > LocalConnectionProtocol.MaximumPasswordLength
                || request.Password.Any(char.IsControl))
            {
                failure = $"The password must be {LocalConnectionProtocol.MinimumPasswordLength}-{LocalConnectionProtocol.MaximumPasswordLength} characters and cannot contain control characters.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static bool ValidateResponse(AccountAuthenticationResponse response, out string failure)
        {
            if (response == null || response.ProtocolVersion != CurrentVersion)
            {
                failure = "The account authentication response uses an unsupported protocol version.";
                return false;
            }

            if (response.Succeeded
                && (!IsSecureUserId(response.UserId)
                    || string.IsNullOrWhiteSpace(response.Username)
                    || response.Username.Length > LocalConnectionProtocol.MaximumAccountNameLength))
            {
                failure = "The account authentication response contains an invalid identity.";
                return false;
            }

            if (response.Message == null || response.Message.Length > 512)
            {
                failure = "The account authentication response message is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static bool IsSecureUserId(string value) =>
            value != null
            && value.Length == SecureUserIdLength
            && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));

        private static bool TryEncode<T>(T value, TryValidate<T> validate, out byte[] payload, out string failure)
        {
            payload = Array.Empty<byte>();
            if (!validate(value, out failure)) return false;
            try
            {
                payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Settings));
                if (payload.Length <= MaximumPayloadBytes) return true;
                payload = Array.Empty<byte>();
                failure = $"The authentication message exceeds {MaximumPayloadBytes} bytes.";
                return false;
            }
            catch (JsonException exception)
            {
                failure = $"The authentication message could not be encoded: {exception.Message}";
                return false;
            }
        }

        private static bool TryDecode<T>(byte[] payload, TryValidate<T> validate, out T value, out string failure) where T : class
        {
            value = null;
            if (payload == null || payload.Length == 0 || payload.Length > MaximumPayloadBytes)
            {
                failure = "The authentication message has an invalid size.";
                return false;
            }

            try { value = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(payload), Settings); }
            catch (JsonException exception)
            {
                failure = $"The authentication message is malformed: {exception.Message}";
                return false;
            }

            return validate(value, out failure);
        }

        private delegate bool TryValidate<in T>(T value, out string failure);
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AccountAuthenticationRequest
    {
        public AccountAuthenticationRequest() { }

        public AccountAuthenticationRequest(string accountIdentifier, string password, AccountAuthenticationMode mode)
        {
            ProtocolVersion = AccountAuthenticationProtocol.CurrentVersion;
            AccountIdentifier = accountIdentifier ?? string.Empty;
            Password = password ?? string.Empty;
            Mode = mode;
        }

        [JsonProperty("protocolVersion", Required = Required.Always)] public int ProtocolVersion { get; set; }
        [JsonProperty("accountIdentifier", Required = Required.Always)] public string AccountIdentifier { get; set; } = string.Empty;
        [JsonProperty("password", Required = Required.Always)] public string Password { get; set; } = string.Empty;
        [JsonProperty("mode", Required = Required.Always)] public AccountAuthenticationMode Mode { get; set; }
        public void ClearPassword() => Password = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AccountAuthenticationResponse
    {
        public AccountAuthenticationResponse() { }

        public AccountAuthenticationResponse(bool succeeded, bool created, string userId, string username, string message)
        {
            ProtocolVersion = AccountAuthenticationProtocol.CurrentVersion;
            Succeeded = succeeded;
            Created = created;
            UserId = userId ?? string.Empty;
            Username = username ?? string.Empty;
            Message = message ?? string.Empty;
        }

        [JsonProperty("protocolVersion", Required = Required.Always)] public int ProtocolVersion { get; set; }
        [JsonProperty("succeeded", Required = Required.Always)] public bool Succeeded { get; set; }
        [JsonProperty("created", Required = Required.Always)] public bool Created { get; set; }
        [JsonProperty("userId", Required = Required.Always)] public string UserId { get; set; } = string.Empty;
        [JsonProperty("username", Required = Required.Always)] public string Username { get; set; } = string.Empty;
        [JsonProperty("message", Required = Required.Always)] public string Message { get; set; } = string.Empty;
    }
}
