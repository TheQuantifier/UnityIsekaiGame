using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityIsekaiGame.GameData.Persistence
{
    public sealed class PersistencePathProvider
    {
        public const string DefaultFolderName = "UnityIsekaiGame/Saves";

        private readonly string rootDirectory;

        public PersistencePathProvider(string rootDirectory = null)
        {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, DefaultFolderName)
                : rootDirectory;
        }

        public string RootDirectory => rootDirectory;

        public static PersistencePathProvider ForPlayer(string playerId, string baseRoot = null)
        {
            return ForContext("Players", playerId, baseRoot);
        }

        public static PersistencePathProvider ForWorld(string worldId, string baseRoot = null)
        {
            return ForContext("Worlds", worldId, baseRoot);
        }

        private static PersistencePathProvider ForContext(string contextFolder, string ownerId, string baseRoot)
        {
            string normalizedOwner = NormalizeOwnerPathSegment(ownerId);
            string root = string.IsNullOrWhiteSpace(baseRoot)
                ? Path.Combine(Application.persistentDataPath, DefaultFolderName)
                : Path.GetFullPath(baseRoot);
            return new PersistencePathProvider(Path.Combine(root, contextFolder, normalizedOwner));
        }

        private static string NormalizeOwnerPathSegment(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                throw new ArgumentException("Persistence owner ID cannot be empty.", nameof(ownerId));
            }

            string normalized = ownerId.Trim();
            for (int i = 0; i < normalized.Length; i++)
            {
                char value = normalized[i];
                bool valid = char.IsLetterOrDigit(value) || value == '-' || value == '_' || value == '.';
                if (!valid)
                {
                    throw new ArgumentException($"Persistence owner ID '{ownerId}' contains a path-unsafe character.", nameof(ownerId));
                }
            }

            return normalized;
        }

        public bool IsValidSlotId(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || slotId.Length > 64)
            {
                return false;
            }

            for (int i = 0; i < slotId.Length; i++)
            {
                char c = slotId[i];
                bool valid = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '-'
                    || c == '_';

                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryGetPaths(string slotId, out SaveSlotPaths paths, out string failureReason)
        {
            paths = null;
            failureReason = string.Empty;

            if (!IsValidSlotId(slotId))
            {
                failureReason = $"Invalid save slot ID '{slotId}'. Use letters, numbers, '-' or '_'.";
                return false;
            }

            string fullRoot = Path.GetFullPath(rootDirectory);
            string primary = Path.GetFullPath(Path.Combine(fullRoot, $"{slotId}.json"));
            string backup = Path.GetFullPath(Path.Combine(fullRoot, $"{slotId}.backup.json"));
            string temporary = Path.GetFullPath(Path.Combine(fullRoot, $"{slotId}.tmp"));

            if (!IsUnderRoot(fullRoot, primary) || !IsUnderRoot(fullRoot, backup) || !IsUnderRoot(fullRoot, temporary))
            {
                failureReason = $"Save slot ID '{slotId}' resolves outside the save directory.";
                return false;
            }

            paths = new SaveSlotPaths(slotId, fullRoot, primary, backup, temporary);
            return true;
        }

        public void EnsureDirectory()
        {
            Directory.CreateDirectory(rootDirectory);
        }

        public IReadOnlyList<SaveSlotPaths> EnumerateKnownSlots()
        {
            List<SaveSlotPaths> slots = new List<SaveSlotPaths>();
            if (!Directory.Exists(rootDirectory))
            {
                return slots;
            }

            HashSet<string> slotIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(rootDirectory, "*.json"))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".backup.json", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - ".backup.json".Length);
                }
                else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - ".json".Length);
                }

                if (IsValidSlotId(name))
                {
                    slotIds.Add(name);
                }
            }

            foreach (string slotId in slotIds)
            {
                if (TryGetPaths(slotId, out SaveSlotPaths paths, out _))
                {
                    slots.Add(paths);
                }
            }

            slots.Sort((a, b) => string.CompareOrdinal(a.SlotId, b.SlotId));
            return slots;
        }

        private static bool IsUnderRoot(string root, string path)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class SaveSlotPaths
    {
        public SaveSlotPaths(string slotId, string rootDirectory, string primaryPath, string backupPath, string temporaryPath)
        {
            SlotId = slotId;
            RootDirectory = rootDirectory;
            PrimaryPath = primaryPath;
            BackupPath = backupPath;
            TemporaryPath = temporaryPath;
        }

        public string SlotId { get; }
        public string RootDirectory { get; }
        public string PrimaryPath { get; }
        public string BackupPath { get; }
        public string TemporaryPath { get; }
    }
}
