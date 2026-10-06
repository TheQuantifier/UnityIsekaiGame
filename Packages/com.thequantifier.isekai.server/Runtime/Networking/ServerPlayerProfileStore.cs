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

        public ServerPlayerProfileData Clone()
        {
            return new ServerPlayerProfileData
            {
                formatVersion = formatVersion,
                playerId = playerId ?? string.Empty,
                personId = personId ?? string.Empty,
                actorId = actorId ?? string.Empty,
                revision = revision,
                savedAtUnixMilliseconds = savedAtUnixMilliseconds,
                positionX = positionX,
                positionY = positionY,
                positionZ = positionZ,
                yawDegrees = yawDegrees,
                vitals = vitals,
                inventory = CloneInventory(inventory),
                equipment = CloneEquipment(equipment)
            };
        }

        public bool HasSamePersistentState(ServerPlayerProfileData other)
        {
            if (other == null
                || !string.Equals(playerId, other.playerId, StringComparison.Ordinal)
                || !string.Equals(personId, other.personId, StringComparison.Ordinal)
                || !string.Equals(actorId, other.actorId, StringComparison.Ordinal)
                || Math.Abs(positionX - other.positionX) > 0.01f
                || Math.Abs(positionY - other.positionY) > 0.01f
                || Math.Abs(positionZ - other.positionZ) > 0.01f
                || Math.Abs(Mathf.DeltaAngle(yawDegrees, other.yawDegrees)) > 0.1f
                || !vitals.Equals(other.vitals))
            {
                return false;
            }

            if (!InventoryEquals(inventory, other.inventory)) return false;
            return EquipmentEquals(equipment, other.equipment);
        }

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

        private static InventorySaveData CloneInventory(InventorySaveData source)
        {
            InventorySaveData copy = new InventorySaveData { slotCapacity = source?.slotCapacity ?? 0 };
            if (source?.entries == null) return copy;
            for (int i = 0; i < source.entries.Count; i++)
            {
                InventoryEntrySaveData entry = source.entries[i];
                if (entry == null) continue;
                copy.entries.Add(new InventoryEntrySaveData
                {
                    mode = entry.mode,
                    definitionId = entry.definitionId ?? string.Empty,
                    itemInstanceId = entry.itemInstanceId ?? string.Empty,
                    quantity = entry.quantity
                });
            }

            return copy;
        }

        private static EquipmentSaveData CloneEquipment(EquipmentSaveData source)
        {
            EquipmentSaveData copy = new EquipmentSaveData();
            if (source?.slots == null) return copy;
            for (int i = 0; i < source.slots.Count; i++)
            {
                EquipmentSlotSaveData slot = source.slots[i];
                if (slot == null) continue;
                copy.slots.Add(new EquipmentSlotSaveData
                {
                    slotType = slot.slotType,
                    mode = slot.mode,
                    definitionId = slot.definitionId ?? string.Empty,
                    itemInstanceId = slot.itemInstanceId ?? string.Empty
                });
            }

            return copy;
        }

        private static bool InventoryEquals(InventorySaveData first, InventorySaveData second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first == null || second == null || first.slotCapacity != second.slotCapacity) return false;
            int firstCount = first.entries?.Count ?? 0;
            int secondCount = second.entries?.Count ?? 0;
            if (firstCount != secondCount) return false;
            for (int i = 0; i < firstCount; i++)
            {
                InventoryEntrySaveData left = first.entries[i];
                InventoryEntrySaveData right = second.entries[i];
                if (left == null || right == null)
                {
                    if (!ReferenceEquals(left, right)) return false;
                    continue;
                }

                if (left.mode != right.mode
                    || left.quantity != right.quantity
                    || !string.Equals(left.definitionId, right.definitionId, StringComparison.Ordinal)
                    || !string.Equals(left.itemInstanceId, right.itemInstanceId, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private static bool EquipmentEquals(EquipmentSaveData first, EquipmentSaveData second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first == null || second == null) return false;
            int firstCount = first.slots?.Count ?? 0;
            int secondCount = second.slots?.Count ?? 0;
            if (firstCount != secondCount) return false;
            for (int i = 0; i < firstCount; i++)
            {
                EquipmentSlotSaveData left = first.slots[i];
                EquipmentSlotSaveData right = second.slots[i];
                if (left == null || right == null)
                {
                    if (!ReferenceEquals(left, right)) return false;
                    continue;
                }

                if (left.slotType != right.slotType
                    || left.mode != right.mode
                    || !string.Equals(left.definitionId, right.definitionId, StringComparison.Ordinal)
                    || !string.Equals(left.itemInstanceId, right.itemInstanceId, StringComparison.Ordinal)) return false;
            }

            return true;
        }
    }

    [Serializable]
    internal sealed class ServerPlayerProfileEnvelope
    {
        public int formatVersion = 1;
        public string payloadJson;
        public string sha256;
    }

    public sealed class ServerPlayerProfileWriteRequest
    {
        internal ServerPlayerProfileWriteRequest(string playerId, long revision, SaveSlotPaths paths, string contents)
        {
            PlayerId = playerId;
            Revision = revision;
            Paths = paths;
            Contents = contents;
        }

        public string PlayerId { get; }
        public long Revision { get; }
        internal SaveSlotPaths Paths { get; }
        internal string Contents { get; }
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
            if (!TryPrepareWrite(profile, out ServerPlayerProfileWriteRequest request, out message)) return false;
            return TryWrite(request, out message);
        }

        public bool TryPrepareWrite(
            ServerPlayerProfileData profile,
            out ServerPlayerProfileWriteRequest request,
            out string message)
        {
            request = null;
            if (!Validate(profile, null, out message)) return false;
            PlayerSessionSnapshot identity = new PlayerSessionSnapshot(
                "profile-save", 0UL, "server", profile.playerId, profile.personId, profile.actorId,
                PlayerSessionPhase.Active, 0L, 0L, Math.Max(1L, profile.revision));
            if (!TryGetPaths(identity, out SaveSlotPaths paths, out message)) return false;

            try
            {
                profile.savedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string payload = JsonUtility.ToJson(profile, false);
                ServerPlayerProfileEnvelope envelope = new ServerPlayerProfileEnvelope
                {
                    payloadJson = payload,
                    sha256 = ComputeSha256(payload)
                };
                request = new ServerPlayerProfileWriteRequest(
                    profile.playerId,
                    profile.revision,
                    paths,
                    JsonUtility.ToJson(envelope, true));
                message = $"Prepared server profile revision {profile.revision} for '{profile.playerId}'.";
                return true;
            }
            catch (Exception exception)
            {
                message = $"Could not serialize server profile '{profile.playerId}': {exception.Message}";
                return false;
            }
        }

        public bool TryWrite(ServerPlayerProfileWriteRequest request, out string message)
        {
            if (request == null || request.Paths == null || string.IsNullOrWhiteSpace(request.Contents))
            {
                message = "A prepared server profile write is required.";
                return false;
            }

            SaveSlotPaths paths = request.Paths;
            try
            {
                Directory.CreateDirectory(paths.RootDirectory);
                File.WriteAllText(paths.TemporaryPath, request.Contents, Encoding.UTF8);
                if (File.Exists(paths.PrimaryPath))
                {
                    File.Replace(paths.TemporaryPath, paths.PrimaryPath, paths.BackupPath, true);
                }
                else
                {
                    File.Move(paths.TemporaryPath, paths.PrimaryPath);
                }

                message = $"Saved server profile revision {request.Revision} for '{request.PlayerId}'.";
                return true;
            }
            catch (Exception exception)
            {
                TryDeleteTemporary(paths.TemporaryPath);
                message = $"Could not save server profile '{request.PlayerId}': {exception.Message}";
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
                || profile.equipment == null)
            {
                message = "Profile identity or required gameplay state is incomplete.";
                return false;
            }

            if (!IsFinite(profile.positionX) || !IsFinite(profile.positionY) || !IsFinite(profile.positionZ) || !IsFinite(profile.yawDegrees))
            {
                message = "Profile transform contains a non-finite numeric value.";
                return false;
            }

            if (!NetworkVitalsStateValidator.TryValidate(profile.vitals, out string vitalsFailure))
            {
                message = $"Profile vitals are invalid: {vitalsFailure}";
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

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
            catch (Exception exception)
            {
                Debug.LogWarning($"[Server Persistence] Could not delete temporary profile file '{path}': {exception.Message}");
            }
        }
    }
}
