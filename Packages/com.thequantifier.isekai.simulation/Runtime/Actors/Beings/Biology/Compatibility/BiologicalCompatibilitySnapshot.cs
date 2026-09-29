using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    public sealed class BiologicalCompatibilityRuleSnapshot
    {
        public BiologicalCompatibilityRuleSnapshot(RuntimeBiologicalInteractionRule rule)
        {
            EntryId = rule == null ? string.Empty : rule.EntryId;
            SourceId = rule == null ? string.Empty : rule.SourceId;
            SourceKind = rule == null ? BiologicalCompatibilitySourceKind.System : rule.SourceKind;
            InteractionDefinitionId = rule == null ? string.Empty : rule.InteractionDefinitionId;
            Category = rule == null ? BiologicalInteractionCategory.Unknown : rule.Category;
            RuleKind = rule == null ? BiologicalInteractionRuleKind.Resistance : rule.RuleKind;
            Priority = rule == null ? 0 : rule.Priority;
        }

        public string EntryId { get; }
        public string SourceId { get; }
        public BiologicalCompatibilitySourceKind SourceKind { get; }
        public string InteractionDefinitionId { get; }
        public BiologicalInteractionCategory Category { get; }
        public BiologicalInteractionRuleKind RuleKind { get; }
        public int Priority { get; }
    }

    public sealed class BiologicalCompatibilitySnapshot
    {
        public BiologicalCompatibilitySnapshot(
            string actorBodyId,
            string profileId,
            BiologicalCompatibilityReadinessState readiness,
            long bodyRevision,
            long anatomyRevision,
            long conditionRevision,
            long vitalRevision,
            long hazardRevision,
            long compatibilityRevision,
            IReadOnlyList<BiologicalCompatibilityRuleSnapshot> rules,
            bool dirty,
            bool coherent,
            IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            AnatomyRevision = anatomyRevision;
            ConditionRevision = conditionRevision;
            VitalRevision = vitalRevision;
            HazardRevision = hazardRevision;
            CompatibilityRevision = compatibilityRevision;
            Rules = rules == null ? Array.Empty<BiologicalCompatibilityRuleSnapshot>() : rules.ToArray();
            IsDirty = dirty;
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public string ProfileId { get; }
        public BiologicalCompatibilityReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public long AnatomyRevision { get; }
        public long ConditionRevision { get; }
        public long VitalRevision { get; }
        public long HazardRevision { get; }
        public long CompatibilityRevision { get; }
        public IReadOnlyList<BiologicalCompatibilityRuleSnapshot> Rules { get; }
        public bool IsDirty { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
