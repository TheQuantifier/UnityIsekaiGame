using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.Networking.Server
{
    [Serializable]
    public sealed class ServerPlayerProfileData
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string playerId;
        public string personId;
        public string actorId;
        public long revision;
        public long savedAtUnixMilliseconds;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float yawDegrees;
        public NetworkVitalsState vitals;
        public InventorySaveData inventory;
        public EquipmentSaveData equipment;

        public Vector3 Position => new Vector3(positionX, positionY, positionZ);

        public static ServerPlayerProfileData Create(
            PlayerSessionSnapshot session,
            Vector3 position,
            float yawDegrees,
            NetworkVitalsState initialVitals,
            InventorySaveData initialInventory,
            EquipmentSaveData initialEquipment)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return new ServerPlayerProfileData
            {
                playerId = session.PlayerId,
                personId = session.PersonId,
                actorId = session.ActorId,
                revision = 1L,
                savedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                positionX = position.x,
                positionY = position.y,
                positionZ = position.z,
                yawDegrees = Mathf.Repeat(yawDegrees, 360f),
                vitals = initialVitals,
                inventory = initialInventory,
                equipment = initialEquipment
            };
        }
    }

    [Serializable]
    internal sealed class ServerPlayerProfileEnvelope
    {
        public int formatVersion = 1;
        public string payloadJson;
        public string sha256;
    }

    /// <summary>
    /// Durable server-owned player profile storage. Files are checksummed and replaced
    /// atomically while retaining the previous valid generation as a recovery backup.
    /// </summary>
    public sealed class ServerPlayerProfileStore
    {
        public const string ProfileSlotId = "server-profile";
        private readonly string rootDirectory;

        public ServerPlayerProfileStore(string rootDirectory = null)
        {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, PersistencePathProvider.DefaultFolderName, "ServerPlayers")
                : Path.GetFullPath(rootDirectory);
        }

        public string RootDirectory => rootDirectory;

        public bool TryLoad(PlayerSessionSnapshot session, out ServerPlayerProfileData profile, out string message)
        {
            profile = null;
            if (!TryGetPaths(session, out SaveSlotPaths paths, out message)) return false;
            if (!File.Exists(paths.PrimaryPath) && !File.Exists(paths.BackupPath))
            {
                message = "No server profile exists yet.";
                return false;
            }

            if (TryRead(paths.PrimaryPath, session, out profile, out message)) return true;
            string primaryFailure = message;
            if (TryRead(paths.BackupPath, session, out profile, out message))
            {
                message = $"Recovered the server profile from backup after primary validation failed: {primaryFailure}";
                return true;
            }

            message = $"The primary and backup server profiles are invalid. Primary: {primaryFailure} Backup: {message}";
            return false;
        }

        public bool TrySave(ServerPlayerProfileData profile, out string message)
        {
            if (!Validate(profile, null, out message)) return false;
            PlayerSessionSnapshot identity = new PlayerSessionSnapshot(
                "profile-save", 0UL, "server", profile.playerId, profile.personId, profile.actorId,
                PlayerSessionPhase.Active, 0L, 0L, Math.Max(1L, profile.revision));
            if (!TryGetPaths(identity, out SaveSlotPaths paths, out message)) return false;

            try
            {
                Directory.CreateDirectory(paths.RootDirectory);
                profile.savedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string payload = JsonUtility.ToJson(profile, false);
                ServerPlayerProfileEnvelope envelope = new ServerPlayerProfileEnvelope
                {
                    payloadJson = payload,
                    sha256 = ComputeSha256(payload)
                };
                File.WriteAllText(paths.TemporaryPath, JsonUtility.ToJson(envelope, true), Encoding.UTF8);
                if (File.Exists(paths.PrimaryPath))
                {
                    File.Replace(paths.TemporaryPath, paths.PrimaryPath, paths.BackupPath, true);
                }
                else
                {
                    File.Move(paths.TemporaryPath, paths.PrimaryPath);
                }

                message = $"Saved server profile revision {profile.revision} for '{profile.playerId}'.";
                return true;
            }
            catch (Exception exception)
            {
                TryDeleteTemporary(paths.TemporaryPath);
                message = $"Could not save server profile '{profile.playerId}': {exception.Message}";
                return false;
            }
        }

        private bool TryGetPaths(PlayerSessionSnapshot session, out SaveSlotPaths paths, out string message)
        {
            paths = null;
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerId))
            {
                message = "A player session identity is required.";
                return false;
            }

            try
            {
                PersistencePathProvider provider = PersistencePathProvider.ForPlayer(session.PlayerId.ToLowerInvariant(), rootDirectory);
                return provider.TryGetPaths(ProfileSlotId, out paths, out message);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private static bool TryRead(string path, PlayerSessionSnapshot session, out ServerPlayerProfileData profile, out string message)
        {
            profile = null;
            if (!File.Exists(path))
            {
                message = "File is missing.";
                return false;
            }

            try
            {
                ServerPlayerProfileEnvelope envelope = JsonUtility.FromJson<ServerPlayerProfileEnvelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || envelope.formatVersion != 1 || string.IsNullOrWhiteSpace(envelope.payloadJson))
                {
                    message = "Envelope is missing or unsupported.";
                    return false;
                }

                if (!string.Equals(envelope.sha256, ComputeSha256(envelope.payloadJson), StringComparison.OrdinalIgnoreCase))
                {
                    message = "Checksum validation failed.";
                    return false;
                }

                ServerPlayerProfileData candidate = JsonUtility.FromJson<ServerPlayerProfileData>(envelope.payloadJson);
                if (!Validate(candidate, session, out message)) return false;
                profile = candidate;
                message = $"Loaded server profile revision {candidate.revision} for '{candidate.playerId}'.";
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private static bool Validate(ServerPlayerProfileData profile, PlayerSessionSnapshot expected, out string message)
        {
            if (profile == null || profile.formatVersion != ServerPlayerProfileData.CurrentFormatVersion)
            {
                message = "Profile is missing or uses an unsupported format.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.playerId)
                || string.IsNullOrWhiteSpace(profile.personId)
                || string.IsNullOrWhiteSpace(profile.actorId)
                || profile.revision < 1L
                || profile.inventory == null
                || profile.equipment == null
                || profile.vitals.Revision == 0u)
            {
                message = "Profile identity or required gameplay state is incomplete.";
                return false;
            }

            if (expected != null
                && (!string.Equals(profile.playerId, expected.PlayerId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(profile.personId, expected.PersonId, StringComparison.Ordinal)
                    || !string.Equals(profile.actorId, expected.ActorId, StringComparison.Ordinal)))
            {
                message = "Profile identity does not match the connecting player session.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static string ComputeSha256(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        private static void TryDeleteTemporary(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
