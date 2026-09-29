using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.Hazards
{
    public sealed class BiologicalHazardSourceSnapshot
    {
        public BiologicalHazardSourceSnapshot(string sourceContributionId, BiologicalHazardSourceCategory sourceCategory, BiologicalHazardSeverity severity, float rateMultiplier, float remainingSeconds, string sourceObjectId, string reason)
        {
            SourceContributionId = sourceContributionId ?? string.Empty;
            SourceCategory = sourceCategory;
            Severity = severity;
            RateMultiplier = rateMultiplier;
            RemainingSeconds = remainingSeconds;
            SourceObjectId = sourceObjectId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string SourceContributionId { get; }
        public BiologicalHazardSourceCategory SourceCategory { get; }
        public BiologicalHazardSeverity Severity { get; }
        public float RateMultiplier { get; }
        public float RemainingSeconds { get; }
        public string SourceObjectId { get; }
        public string Reason { get; }
        public bool Timed => RemainingSeconds > 0f;
    }

    public sealed class BiologicalHazardSuppressionSnapshot
    {
        public BiologicalHazardSuppressionSnapshot(string sourceContributionId, BiologicalHazardSuppressionMode mode, float rateMultiplier, string reason)
        {
            SourceContributionId = sourceContributionId ?? string.Empty;
            Mode = mode;
            RateMultiplier = rateMultiplier;
            Reason = reason ?? string.Empty;
        }

        public string SourceContributionId { get; }
        public BiologicalHazardSuppressionMode Mode { get; }
        public float RateMultiplier { get; }
        public string Reason { get; }
    }

    public sealed class BiologicalHazardInstanceSnapshot
    {
        public BiologicalHazardInstanceSnapshot(
            string instanceId,
            string hazardDefinitionId,
            string displayName,
            BiologicalHazardSeverity severity,
            BiologicalHazardStackingPolicy stackingPolicy,
            float effectiveRatePerHour,
            float elapsedSeconds,
            IReadOnlyList<BiologicalHazardSourceSnapshot> sources,
            IReadOnlyList<BiologicalHazardSuppressionSnapshot> suppressions,
            long revision)
        {
            InstanceId = instanceId ?? string.Empty;
            HazardDefinitionId = hazardDefinitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Severity = severity;
            StackingPolicy = stackingPolicy;
            EffectiveRatePerHour = effectiveRatePerHour;
            ElapsedSeconds = elapsedSeconds;
            Sources = sources == null ? Array.Empty<BiologicalHazardSourceSnapshot>() : sources.ToArray();
            Suppressions = suppressions == null ? Array.Empty<BiologicalHazardSuppressionSnapshot>() : suppressions.ToArray();
            Revision = revision;
        }

        public string InstanceId { get; }
        public string HazardDefinitionId { get; }
        public string DisplayName { get; }
        public BiologicalHazardSeverity Severity { get; }
        public BiologicalHazardStackingPolicy StackingPolicy { get; }
        public float EffectiveRatePerHour { get; }
        public float ElapsedSeconds { get; }
        public IReadOnlyList<BiologicalHazardSourceSnapshot> Sources { get; }
        public IReadOnlyList<BiologicalHazardSuppressionSnapshot> Suppressions { get; }
        public long Revision { get; }
    }

    public sealed class BiologicalHazardSnapshot
    {
        public BiologicalHazardSnapshot(string actorBodyId, BiologicalHazardReadinessState readiness, long bodyRevision, long vitalRevision, long hazardRevision, IReadOnlyList<BiologicalHazardInstanceSnapshot> activeHazards, bool dirty, bool coherent, IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            VitalRevision = vitalRevision;
            HazardRevision = hazardRevision;
            ActiveHazards = activeHazards == null ? Array.Empty<BiologicalHazardInstanceSnapshot>() : activeHazards.ToArray();
            Dirty = dirty;
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public BiologicalHazardReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public long VitalRevision { get; }
        public long HazardRevision { get; }
        public IReadOnlyList<BiologicalHazardInstanceSnapshot> ActiveHazards { get; }
        public bool Dirty { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
