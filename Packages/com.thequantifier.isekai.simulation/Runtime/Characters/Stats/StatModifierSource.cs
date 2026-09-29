using System;

namespace UnityIsekaiGame.Stats
{
    public readonly struct StatModifierSource : IEquatable<StatModifierSource>
    {
        public StatModifierSource(StatModifierSourceType sourceType, string sourceId)
        {
            SourceType = sourceType;
            SourceId = string.IsNullOrWhiteSpace(sourceId) ? string.Empty : sourceId;
        }

        public StatModifierSourceType SourceType { get; }
        public string SourceId { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(SourceId);

        public bool Equals(StatModifierSource other)
        {
            return SourceType == other.SourceType && string.Equals(SourceId, other.SourceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StatModifierSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)SourceType * 397) ^ StringComparer.Ordinal.GetHashCode(SourceId ?? string.Empty);
            }
        }

        public override string ToString()
        {
            return $"{SourceType}:{SourceId}";
        }
    }
}
