using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityIsekaiGame.Networking.Server
{
    public enum ServerAccountAuthenticationStatus
    {
        Authenticated = 1,
        Created = 2,
        Rejected = 3,
        StorageFailure = 4
    }

    public readonly struct ServerAccountAuthenticationResult
    {
        public ServerAccountAuthenticationResult(
            ServerAccountAuthenticationStatus status,
            string playerId,
            string message)
        {
            Status = status;
            PlayerId = playerId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ServerAccountAuthenticationStatus Status { get; }
        public string PlayerId { get; }
        public string Message { get; }
        public bool Succeeded => Status == ServerAccountAuthenticationStatus.Authenticated
            || Status == ServerAccountAuthenticationStatus.Created;
    }

    /// <summary>
    /// Server-owned account repository. Passwords are never persisted directly: each account
    /// stores an independent random salt and PBKDF2-SHA256 verifier. The repository is excluded
    /// from client assemblies and writes each record atomically.
    /// </summary>
    public sealed class ServerAccountStore
    {
        internal const int PasswordHashIterations = 210000;
        private const int SaltBytes = 32;
        private const int PasswordHashBytes = 32;
        private const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None
        };

        private readonly object syncRoot = new object();
        private readonly string accountDirectory;

        public ServerAccountStore(string rootDirectory = null)
        {
            string root = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "ServerData")
                : Path.GetFullPath(rootDirectory);
            accountDirectory = Path.Combine(root, "Accounts");
        }

        public string AccountDirectory => accountDirectory;

        public ServerAccountAuthenticationResult Authenticate(ConnectionRequestPayload request)
        {
            if (request == null)
            {
                return Rejected("The account request is missing.");
            }

            if (!LocalConnectionProtocol.Validate(request, out string validationFailure))
            {
                return Rejected(validationFailure);
            }

            string normalizedName = NormalizeAccountName(request.PlayerId);
            lock (syncRoot)
            {
                try
                {
                    Directory.CreateDirectory(accountDirectory);
                    string path = ResolveAccountPath(normalizedName);
                    return request.AuthenticationMode == AccountAuthenticationMode.CreateAccount
                        ? CreateAccount(path, normalizedName, request.Password)
                        : AuthenticateExisting(path, normalizedName, request.Password);
                }
                catch (Exception exception) when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is CryptographicException
                    || exception is JsonException)
                {
                    return new ServerAccountAuthenticationResult(
                        ServerAccountAuthenticationStatus.StorageFailure,
                        string.Empty,
                        "Account services are temporarily unavailable. Please try again.");
                }
            }
        }

        public bool AccountExists(string username)
        {
            if (!LocalConnectionProtocol.IsValidIdentifier(username)
                || username.Length < LocalConnectionProtocol.MinimumAccountNameLength
                || username.Length > LocalConnectionProtocol.MaximumAccountNameLength)
            {
                return false;
            }

            lock (syncRoot)
            {
                return File.Exists(ResolveAccountPath(NormalizeAccountName(username)));
            }
        }

        private ServerAccountAuthenticationResult CreateAccount(string path, string username, string password)
        {
            if (File.Exists(path))
            {
                return Rejected("That username is already registered. Choose Login or use another username.");
            }

            byte[] salt = new byte[SaltBytes];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(salt);
            }

            byte[] hash = DerivePasswordHash(password, salt, PasswordHashIterations);
            var record = new ServerAccountRecord
            {
                schemaVersion = CurrentSchemaVersion,
                accountId = CreateStableAccountId(username),
                username = username,
                saltBase64 = Convert.ToBase64String(salt),
                passwordHashBase64 = Convert.ToBase64String(hash),
                passwordHashIterations = PasswordHashIterations,
                createdAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            WriteAtomically(path, JsonConvert.SerializeObject(record, SerializerSettings));
            return new ServerAccountAuthenticationResult(
                ServerAccountAuthenticationStatus.Created,
                username,
                "Account created successfully.");
        }

        private ServerAccountAuthenticationResult AuthenticateExisting(string path, string username, string password)
        {
            const string genericFailure = "The username or password is incorrect.";
            if (!File.Exists(path))
            {
                // Complete equivalent hash work even when an account does not exist so the most
                // obvious username-enumeration timing difference is avoided.
                byte[] dummySalt = new byte[SaltBytes];
                DerivePasswordHash(password, dummySalt, PasswordHashIterations);
                return Rejected(genericFailure);
            }

            ServerAccountRecord record = JsonConvert.DeserializeObject<ServerAccountRecord>(File.ReadAllText(path, Encoding.UTF8));
            if (!IsValidRecord(record, username, out byte[] salt, out byte[] expectedHash))
            {
                return new ServerAccountAuthenticationResult(
                    ServerAccountAuthenticationStatus.StorageFailure,
                    string.Empty,
                    "Account services are temporarily unavailable. Please try again.");
            }

            byte[] actualHash = DerivePasswordHash(password, salt, record.passwordHashIterations);
            if (!FixedTimeEquals(actualHash, expectedHash))
            {
                return Rejected(genericFailure);
            }

            return new ServerAccountAuthenticationResult(
                ServerAccountAuthenticationStatus.Authenticated,
                record.username,
                "Login successful.");
        }

        private string ResolveAccountPath(string normalizedName)
        {
            return Path.Combine(accountDirectory, CreateStableAccountId(normalizedName) + ".json");
        }

        private static bool IsValidRecord(
            ServerAccountRecord record,
            string expectedUsername,
            out byte[] salt,
            out byte[] passwordHash)
        {
            salt = Array.Empty<byte>();
            passwordHash = Array.Empty<byte>();
            if (record == null
                || record.schemaVersion != CurrentSchemaVersion
                || !string.Equals(record.username, expectedUsername, StringComparison.Ordinal)
                || record.passwordHashIterations < 100000
                || record.passwordHashIterations > 2000000)
            {
                return false;
            }

            try
            {
                salt = Convert.FromBase64String(record.saltBase64 ?? string.Empty);
                passwordHash = Convert.FromBase64String(record.passwordHashBase64 ?? string.Empty);
                return salt.Length == SaltBytes && passwordHash.Length == PasswordHashBytes;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] DerivePasswordHash(string password, byte[] salt, int iterations)
        {
            using var derivation = new Rfc2898DeriveBytes(
                password ?? string.Empty,
                salt,
                iterations,
                HashAlgorithmName.SHA256);
            return derivation.GetBytes(PasswordHashBytes);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            int difference = left.Length ^ right.Length;
            int count = Math.Max(left.Length, right.Length);
            for (int index = 0; index < count; index++)
            {
                byte a = index < left.Length ? left[index] : (byte)0;
                byte b = index < right.Length ? right[index] : (byte)0;
                difference |= a ^ b;
            }

            return difference == 0;
        }

        private static string NormalizeAccountName(string username) => username.Trim().ToLowerInvariant();

        private static string CreateStableAccountId(string normalizedName)
        {
            using SHA256 sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedName));
            var builder = new StringBuilder("account-");
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2"));
            }

            return builder.ToString();
        }

        private static void WriteAtomically(string destinationPath, string contents)
        {
            string temporaryPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
            string backupPath = destinationPath + ".bak";
            try
            {
                File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
                if (File.Exists(destinationPath))
                {
                    File.Replace(temporaryPath, destinationPath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, destinationPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static ServerAccountAuthenticationResult Rejected(string message) =>
            new ServerAccountAuthenticationResult(
                ServerAccountAuthenticationStatus.Rejected,
                string.Empty,
                message);

        [Serializable]
        private sealed class ServerAccountRecord
        {
            public int schemaVersion;
            public string accountId = string.Empty;
            public string username = string.Empty;
            public string saltBase64 = string.Empty;
            public string passwordHashBase64 = string.Empty;
            public int passwordHashIterations;
            public long createdAtUnixMilliseconds;
        }
    }
}
