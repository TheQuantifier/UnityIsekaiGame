using System;
using System.Text.RegularExpressions;

namespace UnityIsekaiGame.WorldEntities
{
    public static class WorldEntityIdUtility
    {
        private static readonly Regex ValidSegmentPattern = new Regex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.Compiled);

        public static string ComposeAuthoredId(string sceneKey, string localAuthoredId)
        {
            string normalizedScene = Normalize(sceneKey);
            string normalizedLocal = Normalize(localAuthoredId);
            return string.IsNullOrWhiteSpace(normalizedScene) || string.IsNullOrWhiteSpace(normalizedLocal)
                ? string.Empty
                : $"entity.{normalizedScene}.{normalizedLocal}";
        }

        public static string CreateRuntimeId(string worldId = null)
        {
            string normalizedWorld = Normalize(worldId);
            string guid = Guid.NewGuid().ToString("N");
            return string.IsNullOrWhiteSpace(normalizedWorld)
                ? $"entity.runtime.{guid}"
                : $"entity.{normalizedWorld}.runtime.{guid}";
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        public static bool IsValidLocalAuthoredId(string value)
        {
            return IsValidSegment(Normalize(value));
        }

        public static bool IsValidEntityId(string value)
        {
            string normalized = Normalize(value);
            return normalized.StartsWith("entity.", StringComparison.Ordinal)
                && normalized.Length > "entity.".Length
                && IsValidSegment(normalized.Substring("entity.".Length));
        }

        public static bool IsTransientReference(string entityId)
        {
            return string.Equals(Normalize(entityId), "transient", StringComparison.Ordinal)
                || Normalize(entityId).StartsWith("entity.transient.", StringComparison.Ordinal);
        }

        private static bool IsValidSegment(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && ValidSegmentPattern.IsMatch(value);
        }
    }
}
