using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UnityIsekaiGame.GameData.Persistence
{
    public static class PrototypeSaveSlotCatalog
    {
        public const int DefaultManualSlotCount = 5;
        public const int DefaultAutosaveSlotCount = 3;
        public const string AutosaveStagingSlotId = "autosave-staging";
        public const string CurrentWorldCheckpointSlotId = "world-current";

        public static string ManualSlotId(int zeroBasedIndex)
        {
            if (zeroBasedIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
            }

            return $"manual-{zeroBasedIndex + 1}";
        }

        public static string AutosaveSlotId(int zeroBasedGeneration)
        {
            if (zeroBasedGeneration < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(zeroBasedGeneration));
            }

            return $"autosave-{zeroBasedGeneration}";
        }

        public static string ManualDisplayName(int zeroBasedIndex)
        {
            return $"Manual Save {zeroBasedIndex + 1}";
        }

        public static string AutosaveDisplayName(int zeroBasedGeneration)
        {
            return zeroBasedGeneration == 0 ? "Autosave - Newest" : $"Autosave - Previous {zeroBasedGeneration}";
        }

        public static IReadOnlyList<string> BuildAutosaveSlotIds(int autosaveSlotCount)
        {
            int count = Math.Max(1, autosaveSlotCount);
            List<string> ids = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                ids.Add(AutosaveSlotId(i));
            }

            return ids;
        }

        public static IReadOnlyList<SaveSlotDescriptor> BuildDescriptors(
            PersistenceService service,
            int manualSlotCount,
            int autosaveSlotCount)
        {
            List<SaveSlotDescriptor> descriptors = new List<SaveSlotDescriptor>();
            if (service == null)
            {
                return descriptors;
            }

            Dictionary<string, SaveSlotMetadata> metadataBySlot = service.ListSaveSlots()
                .Where(metadata => metadata != null && !string.IsNullOrWhiteSpace(metadata.slotId))
                .GroupBy(metadata => metadata.slotId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            for (int i = 0; i < Math.Max(1, manualSlotCount); i++)
            {
                string slotId = ManualSlotId(i);
                metadataBySlot.TryGetValue(slotId, out SaveSlotMetadata metadata);
                descriptors.Add(CreateDescriptor(slotId, SaveSlotKind.Manual, ManualDisplayName(i), metadata, i, false, service));
            }

            for (int i = 0; i < Math.Max(1, autosaveSlotCount); i++)
            {
                string slotId = AutosaveSlotId(i);
                metadataBySlot.TryGetValue(slotId, out SaveSlotMetadata metadata);
                descriptors.Add(CreateDescriptor(slotId, SaveSlotKind.Autosave, AutosaveDisplayName(i), metadata, i, i == 0, service));
            }

            return descriptors;
        }

        public static SaveSlotDescriptor CreateDescriptor(
            string slotId,
            SaveSlotKind kind,
            string displayName,
            SaveSlotMetadata metadata,
            int generation,
            bool newestAutosave,
            PersistenceService service)
        {
            metadata ??= service?.GetSlotMetadata(slotId);
            bool primaryExists = metadata != null && metadata.hasPrimary;
            bool backupExists = metadata != null && metadata.hasBackup;
            bool exists = primaryExists;
            bool valid = metadata != null && metadata.isValid;
            SaveCompatibilityStatus compatibility = ResolveCompatibility(metadata, service);

            return new SaveSlotDescriptor
            {
                slotId = slotId,
                slotKind = kind,
                displayName = string.IsNullOrWhiteSpace(metadata?.displayName) || !exists ? displayName : metadata.displayName,
                exists = exists,
                isValid = valid,
                primaryExists = primaryExists,
                backupExists = backupExists,
                schemaVersion = metadata?.schemaVersion ?? 0,
                createdAtUtc = metadata?.createdUtc ?? string.Empty,
                lastSavedAtUtc = string.IsNullOrWhiteSpace(metadata?.lastWrittenUtc) ? metadata?.modifiedUtc ?? string.Empty : metadata.lastWrittenUtc,
                playTimeSeconds = metadata?.playtimeSeconds ?? 0,
                sceneKey = metadata?.sceneSummary ?? string.Empty,
                placeId = metadata?.currentPlaceSummary ?? string.Empty,
                placeDisplayName = metadata?.currentPlaceSummary ?? string.Empty,
                playerDisplayName = metadata?.playerSummary ?? string.Empty,
                currentHealthSummary = string.Empty,
                validationStatus = ParseStatus(metadata?.status),
                compatibilityStatus = compatibility,
                lastErrorCode = metadata?.status ?? string.Empty,
                message = metadata?.message ?? (exists ? string.Empty : "Empty slot."),
                fileSizeBytes = metadata?.fileSizeBytes ?? 0,
                saveGeneration = generation,
                isNewestAutosave = newestAutosave,
                transactionId = metadata?.transactionId ?? string.Empty,
                saveRevision = metadata?.saveRevision ?? 0
            };
        }

        public static string FormatLocalTimestamp(string utcTimestamp)
        {
            if (string.IsNullOrWhiteSpace(utcTimestamp) || !DateTime.TryParse(utcTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime utc))
            {
                return "Never";
            }

            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static string FormatPlayTime(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return span.TotalHours >= 1d
                ? $"{(int)span.TotalHours}h {span.Minutes:00}m"
                : $"{span.Minutes}m {span.Seconds:00}s";
        }

        private static SaveCompatibilityStatus ResolveCompatibility(SaveSlotMetadata metadata, PersistenceService service)
        {
            if (metadata == null || !metadata.hasPrimary)
            {
                return SaveCompatibilityStatus.Empty;
            }

            if (metadata.schemaVersion > PersistenceService.CurrentSchemaVersion)
            {
                return SaveCompatibilityStatus.FutureVersion;
            }

            if (metadata.schemaVersion != PersistenceService.CurrentSchemaVersion)
            {
                return SaveCompatibilityStatus.Corrupted;
            }

            if (service != null && service.ContextKind != PersistenceContextKind.Player)
            {
                return SaveCompatibilityStatus.WrongContext;
            }

            if (service != null && !string.IsNullOrWhiteSpace(metadata.worldId) && metadata.worldId != service.WorldId)
            {
                return SaveCompatibilityStatus.WrongWorld;
            }

            if (service != null && !string.IsNullOrWhiteSpace(metadata.playerId) && metadata.playerId != service.PlayerId)
            {
                return SaveCompatibilityStatus.WrongPlayer;
            }

            if (!metadata.isValid)
            {
                return metadata.status == PersistenceValidationStatus.FileMissing.ToString()
                    ? SaveCompatibilityStatus.Empty
                    : SaveCompatibilityStatus.Corrupted;
            }

            return SaveCompatibilityStatus.Compatible;
        }

        private static PersistenceValidationStatus ParseStatus(string status)
        {
            return Enum.TryParse(status, out PersistenceValidationStatus parsed)
                ? parsed
                : PersistenceValidationStatus.FileMissing;
        }
    }
}
