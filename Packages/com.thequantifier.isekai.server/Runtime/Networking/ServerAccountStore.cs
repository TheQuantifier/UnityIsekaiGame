using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityIsekaiGame.Networking.Server
{
    public enum ServerAccountAuthenticationStatus { Authenticated = 1, Created = 2, Rejected = 3, StorageFailure = 4 }

    public readonly struct ServerAccountAuthenticationResult
    {
        public ServerAccountAuthenticationResult(ServerAccountAuthenticationStatus status, string userId, string username, string message)
        {
            Status = status; UserId = userId ?? string.Empty; Username = username ?? string.Empty; Message = message ?? string.Empty;
        }
        public ServerAccountAuthenticationStatus Status { get; }
        public string UserId { get; }
        public string Username { get; }
        public string Message { get; }
        public bool Succeeded => Status == ServerAccountAuthenticationStatus.Authenticated || Status == ServerAccountAuthenticationStatus.Created;
    }

    /// <summary>Server-only account repository with opaque IDs and salted PBKDF2-SHA256 verifiers.</summary>
    public sealed class ServerAccountStore
    {
        internal const int PasswordHashIterations = 310000;
        private const int SaltBytes = 32;
        private const int PasswordHashBytes = 32;
        private const int CurrentSchemaVersion = 2;
        private const string GenericFailure = "The account identifier or password is incorrect.";
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented, MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include, TypeNameHandling = TypeNameHandling.None
        };

        private readonly object syncRoot = new object();
        private readonly string accountDirectory;
        private readonly string usernameIndexDirectory;

        public ServerAccountStore(string rootDirectory = null)
        {
            string root = string.IsNullOrWhiteSpace(rootDirectory) ? Path.Combine(Application.persistentDataPath, "ServerData") : Path.GetFullPath(rootDirectory);
            accountDirectory = Path.Combine(root, "Accounts");
            usernameIndexDirectory = Path.Combine(accountDirectory, "UsernameIndex");
        }

        public string AccountDirectory => accountDirectory;

        public ServerAccountAuthenticationResult Authenticate(AccountAuthenticationRequest request)
        {
            if (!AccountAuthenticationProtocol.ValidateRequest(request, out string failure)) return Rejected(failure);
            lock (syncRoot)
            {
                try
                {
                    Directory.CreateDirectory(accountDirectory);
                    Directory.CreateDirectory(usernameIndexDirectory);
                    return request.Mode == AccountAuthenticationMode.CreateAccount
                        ? CreateAccount(request.AccountIdentifier.Trim().ToLowerInvariant(), request.Password)
                        : AuthenticateExisting(request.AccountIdentifier.Trim().ToLowerInvariant(), request.Password);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is CryptographicException || exception is JsonException)
                {
                    return new ServerAccountAuthenticationResult(ServerAccountAuthenticationStatus.StorageFailure, string.Empty, string.Empty,
                        "Account services are temporarily unavailable. Please try again.");
                }
                finally { request.ClearPassword(); }
            }
        }

        public bool AccountExists(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            lock (syncRoot) return File.Exists(ResolveUsernameIndexPath(username.Trim().ToLowerInvariant()));
        }

        private ServerAccountAuthenticationResult CreateAccount(string username, string password)
        {
            string indexPath = ResolveUsernameIndexPath(username);
            if (File.Exists(indexPath)) return Rejected("That username is already registered. Choose Login or use another username.");

            string userId;
            string accountPath;
            do { userId = CreateSecureUserId(); accountPath = ResolveAccountPath(userId); } while (File.Exists(accountPath));
            byte[] salt = RandomBytes(SaltBytes);
            byte[] hash = DerivePasswordHash(password, salt, PasswordHashIterations);
            var record = new ServerAccountRecord
            {
                schemaVersion = CurrentSchemaVersion, userId = userId, username = username,
                saltBase64 = Convert.ToBase64String(salt), passwordHashBase64 = Convert.ToBase64String(hash),
                passwordHashIterations = PasswordHashIterations, createdAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            WriteAtomically(accountPath, JsonConvert.SerializeObject(record, Settings));
            try
            {
                WriteAtomically(indexPath, JsonConvert.SerializeObject(new UsernameIndexRecord { schemaVersion = CurrentSchemaVersion, userId = userId }, Settings));
            }
            catch { if (File.Exists(accountPath)) File.Delete(accountPath); throw; }
            return new ServerAccountAuthenticationResult(ServerAccountAuthenticationStatus.Created, userId, username, "Account created successfully.");
        }

        private ServerAccountAuthenticationResult AuthenticateExisting(string identifier, string password)
        {
            string userId = AccountAuthenticationProtocol.IsSecureUserId(identifier) ? identifier : ResolveUserIdForUsername(identifier);
            string path = string.IsNullOrEmpty(userId) ? string.Empty : ResolveAccountPath(userId);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                DerivePasswordHash(password, new byte[SaltBytes], PasswordHashIterations);
                return Rejected(GenericFailure);
            }

            ServerAccountRecord record = JsonConvert.DeserializeObject<ServerAccountRecord>(File.ReadAllText(path, Encoding.UTF8));
            if (!IsValidRecord(record, userId, out byte[] salt, out byte[] expectedHash))
                return new ServerAccountAuthenticationResult(ServerAccountAuthenticationStatus.StorageFailure, string.Empty, string.Empty, "Account services are temporarily unavailable. Please try again.");
            byte[] actualHash = DerivePasswordHash(password, salt, record.passwordHashIterations);
            if (!FixedTimeEquals(actualHash, expectedHash)) return Rejected(GenericFailure);
            return new ServerAccountAuthenticationResult(ServerAccountAuthenticationStatus.Authenticated, record.userId, record.username, "Login successful.");
        }

        private string ResolveUserIdForUsername(string username)
        {
            string path = ResolveUsernameIndexPath(username);
            if (!File.Exists(path)) return string.Empty;
            UsernameIndexRecord index = JsonConvert.DeserializeObject<UsernameIndexRecord>(File.ReadAllText(path, Encoding.UTF8));
            return index != null && index.schemaVersion == CurrentSchemaVersion && AccountAuthenticationProtocol.IsSecureUserId(index.userId) ? index.userId : string.Empty;
        }

        private string ResolveAccountPath(string userId) => Path.Combine(accountDirectory, userId + ".json");
        private string ResolveUsernameIndexPath(string username) => Path.Combine(usernameIndexDirectory, HashHex(username) + ".json");
        private static bool IsValidRecord(ServerAccountRecord record, string expectedUserId, out byte[] salt, out byte[] hash)
        {
            salt = Array.Empty<byte>(); hash = Array.Empty<byte>();
            if (record == null || record.schemaVersion != CurrentSchemaVersion || record.userId != expectedUserId
                || !AccountAuthenticationProtocol.IsSecureUserId(record.userId)
                || record.passwordHashIterations < 100000 || record.passwordHashIterations > 2000000) return false;
            try
            {
                salt = Convert.FromBase64String(record.saltBase64 ?? string.Empty);
                hash = Convert.FromBase64String(record.passwordHashBase64 ?? string.Empty);
                return salt.Length == SaltBytes && hash.Length == PasswordHashBytes;
            }
            catch (FormatException) { return false; }
        }

        private static string CreateSecureUserId() => ToHex(RandomBytes(AccountAuthenticationProtocol.SecureUserIdLength / 2));
        private static byte[] RandomBytes(int length) { byte[] bytes = new byte[length]; using RandomNumberGenerator rng = RandomNumberGenerator.Create(); rng.GetBytes(bytes); return bytes; }
        private static string HashHex(string value) { using SHA256 sha = SHA256.Create(); return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        private static string ToHex(byte[] bytes) { var builder = new StringBuilder(bytes.Length * 2); foreach (byte value in bytes) builder.Append(value.ToString("x2")); return builder.ToString(); }
        private static byte[] DerivePasswordHash(string password, byte[] salt, int iterations) { using var derivation = new Rfc2898DeriveBytes(password ?? string.Empty, salt, iterations, HashAlgorithmName.SHA256); return derivation.GetBytes(PasswordHashBytes); }
        private static bool FixedTimeEquals(byte[] left, byte[] right) { int difference = left.Length ^ right.Length; int count = Math.Max(left.Length, right.Length); for (int i = 0; i < count; i++) difference |= (i < left.Length ? left[i] : 0) ^ (i < right.Length ? right[i] : 0); return difference == 0; }
        private static void WriteAtomically(string path, string contents)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try { File.WriteAllText(temporary, contents, new UTF8Encoding(false)); File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static ServerAccountAuthenticationResult Rejected(string message) => new ServerAccountAuthenticationResult(ServerAccountAuthenticationStatus.Rejected, string.Empty, string.Empty, message);
        [Serializable] private sealed class ServerAccountRecord { public int schemaVersion; public string userId = string.Empty; public string username = string.Empty; public string saltBase64 = string.Empty; public string passwordHashBase64 = string.Empty; public int passwordHashIterations; public long createdAtUnixMilliseconds; }
        [Serializable] private sealed class UsernameIndexRecord { public int schemaVersion; public string userId = string.Empty; }
    }
}
