using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;

namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    public sealed class RuntimeBiologicalInteractionRule
    {
        private const float MaximumAuthoredMultiplier = 10f;
        private const float MaximumAuthoredSeverity = 999f;

        public RuntimeBiologicalInteractionRule(
            string entryId,
            BiologicalCompatibilitySourceKind sourceKind,
            string sourceId,
            string interactionDefinitionId,
            BiologicalInteractionCategory category,
            BiologicalInteractionRuleKind ruleKind,
            BiologicalCompatibilityState compatibilityState,
            float rateMultiplier,
            float severityMultiplier,
            float consequenceMultiplier,
            float minimumEffectFloor,
            float maximumSeverity,
            int priority,
            string convertedInteractionDefinitionId,
            IReadOnlyList<string> requiredRuntimeCapabilityKeys,
            IReadOnlyList<string> blockingRuntimeCapabilityKeys,
            IReadOnlyList<string> requiredAnatomyTagIds,
            IReadOnlyList<AnatomyStructuralCategory> requiredNodeCategories,
            string requiredNodeId,
            string explanation,
            bool alphaEnabled = true)
        {
            EntryId = entryId ?? string.Empty;
            SourceKind = sourceKind;
            SourceId = sourceId ?? string.Empty;
            InteractionDefinitionId = interactionDefinitionId ?? string.Empty;
            Category = category;
            RuleKind = ruleKind;
            CompatibilityState = compatibilityState;
            RateMultiplier = SanitizeMultiplier(rateMultiplier, 1f);
            SeverityMultiplier = SanitizeMultiplier(severityMultiplier, 1f);
            ConsequenceMultiplier = SanitizeMultiplier(consequenceMultiplier, 1f);
            MinimumEffectFloor = SanitizeMultiplier(minimumEffectFloor, 0f);
            MaximumSeverity = SanitizeMaximumSeverity(maximumSeverity);
            Priority = priority;
            ConvertedInteractionDefinitionId = convertedInteractionDefinitionId ?? string.Empty;
            RequiredRuntimeCapabilityKeys = requiredRuntimeCapabilityKeys == null ? Array.Empty<string>() : requiredRuntimeCapabilityKeys.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray();
            BlockingRuntimeCapabilityKeys = blockingRuntimeCapabilityKeys == null ? Array.Empty<string>() : blockingRuntimeCapabilityKeys.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray();
            RequiredAnatomyTagIds = requiredAnatomyTagIds == null ? Array.Empty<string>() : requiredAnatomyTagIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray();
            RequiredNodeCategories = requiredNodeCategories == null ? Array.Empty<AnatomyStructuralCategory>() : requiredNodeCategories.ToArray();
            RequiredNodeId = requiredNodeId ?? string.Empty;
            Explanation = explanation ?? string.Empty;
            AlphaEnabled = alphaEnabled;
        }

        public string EntryId { get; }
        public BiologicalCompatibilitySourceKind SourceKind { get; }
        public string SourceId { get; }
        public string InteractionDefinitionId { get; }
        public BiologicalInteractionCategory Category { get; }
        public BiologicalInteractionRuleKind RuleKind { get; }
        public BiologicalCompatibilityState CompatibilityState { get; }
        public float RateMultiplier { get; }
        public float SeverityMultiplier { get; }
        public float ConsequenceMultiplier { get; }
        public float MinimumEffectFloor { get; }
        public float MaximumSeverity { get; }
        public int Priority { get; }
        public string ConvertedInteractionDefinitionId { get; }
        public IReadOnlyList<string> RequiredRuntimeCapabilityKeys { get; }
        public IReadOnlyList<string> BlockingRuntimeCapabilityKeys { get; }
        public IReadOnlyList<string> RequiredAnatomyTagIds { get; }
        public IReadOnlyList<AnatomyStructuralCategory> RequiredNodeCategories { get; }
        public string RequiredNodeId { get; }
        public string Explanation { get; }
        public bool AlphaEnabled { get; }

        public string StableKey => $"{(int)SourceKind}:{SourceId}:{EntryId}";

        public RuntimeBiologicalInteractionRule WithSource(BiologicalCompatibilitySourceKind sourceKind, string sourceId)
        {
            return new RuntimeBiologicalInteractionRule(
                EntryId,
                sourceKind,
                sourceId,
                InteractionDefinitionId,
                Category,
                RuleKind,
                CompatibilityState,
                RateMultiplier,
                SeverityMultiplier,
                ConsequenceMultiplier,
                MinimumEffectFloor,
                MaximumSeverity,
                Priority,
                ConvertedInteractionDefinitionId,
                RequiredRuntimeCapabilityKeys,
                BlockingRuntimeCapabilityKeys,
                RequiredAnatomyTagIds,
                RequiredNodeCategories,
                RequiredNodeId,
                Explanation,
                AlphaEnabled);
        }

        private static float SanitizeMultiplier(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return fallback;
            }

            return Math.Min(value, MaximumAuthoredMultiplier);
        }

        private static float SanitizeMaximumSeverity(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return MaximumAuthoredSeverity;
            }

            return Math.Min(value, MaximumAuthoredSeverity);
        }
    }

    public sealed class BiologicalInteractionEvaluationContext
    {
        public BiologicalInteractionEvaluationContext(
            BodySnapshot body,
            BiologicalInteractionDefinition interaction,
            string interactionDefinitionId,
            BiologicalInteractionCategory category,
            AnatomyNodeSnapshot targetNode = null,
            BodyConditionSnapshot condition = null,
            VitalProcessSnapshot vitalProcesses = null,
            string sourceId = "",
            string transactionId = "",
            bool preview = true,
            IReadOnlyList<string> contextTags = null)
        {
            Body = body;
            Interaction = interaction;
            InteractionDefinitionId = string.IsNullOrWhiteSpace(interactionDefinitionId) ? interaction == null ? string.Empty : interaction.Id : interactionDefinitionId;
            Category = category == BiologicalInteractionCategory.Unknown && interaction != null ? interaction.Category : category;
            TargetNode = targetNode;
            Condition = condition ?? body?.Condition;
            VitalProcesses = vitalProcesses ?? body?.VitalProcesses;
            SourceId = sourceId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            ContextTags = contextTags == null ? Array.Empty<string>() : contextTags.ToArray();
        }

        public BodySnapshot Body { get; }
        public BiologicalInteractionDefinition Interaction { get; }
        public string InteractionDefinitionId { get; }
        public BiologicalInteractionCategory Category { get; }
        public AnatomyNodeSnapshot TargetNode { get; }
        public BodyConditionSnapshot Condition { get; }
        public VitalProcessSnapshot VitalProcesses { get; }
        public string SourceId { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public IReadOnlyList<string> ContextTags { get; }
        public string ActorBodyId => Body == null ? string.Empty : Body.ActorBodyId;
    }

    public sealed class BiologicalInteractionRuleTrace
    {
        public BiologicalInteractionRuleTrace(RuntimeBiologicalInteractionRule rule, bool matched, string reason)
        {
            EntryId = rule == null ? string.Empty : rule.EntryId;
            SourceId = rule == null ? string.Empty : rule.SourceId;
            SourceKind = rule == null ? BiologicalCompatibilitySourceKind.System : rule.SourceKind;
            RuleKind = rule == null ? BiologicalInteractionRuleKind.Resistance : rule.RuleKind;
            Priority = rule == null ? 0 : rule.Priority;
            Matched = matched;
            Reason = reason ?? string.Empty;
        }

        public string EntryId { get; }
        public string SourceId { get; }
        public BiologicalCompatibilitySourceKind SourceKind { get; }
        public BiologicalInteractionRuleKind RuleKind { get; }
        public int Priority { get; }
        public bool Matched { get; }
        public string Reason { get; }
    }

    public sealed class BiologicalInteractionEvaluationResult
    {
        public BiologicalInteractionEvaluationResult(
            BiologicalCompatibilityResultCode code,
            string message,
            string actorBodyId,
            string interactionDefinitionId,
            BiologicalInteractionCategory category,
            BiologicalCompatibilityState compatibilityState,
            bool immune,
            bool suppressed,
            bool affinity,
            bool absorbed,
            string convertedInteractionDefinitionId,
            float rateMultiplier,
            float severityMultiplier,
            float consequenceMultiplier,
            float minimumEffectFloor,
            float maximumSeverity,
            long bodyRevision,
            long anatomyRevision,
            long conditionRevision,
            long vitalRevision,
            long hazardRevision,
            long compatibilityRevision,
            IReadOnlyList<BiologicalInteractionRuleTrace> traces)
        {
            Code = code;
            Message = message ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            InteractionDefinitionId = interactionDefinitionId ?? string.Empty;
            Category = category;
            CompatibilityState = compatibilityState;
            Immune = immune;
            Suppressed = suppressed;
            Affinity = affinity;
            Absorbed = absorbed;
            ConvertedInteractionDefinitionId = convertedInteractionDefinitionId ?? string.Empty;
            RateMultiplier = Mathf.Max(0f, rateMultiplier);
            SeverityMultiplier = Mathf.Max(0f, severityMultiplier);
            ConsequenceMultiplier = Mathf.Max(0f, consequenceMultiplier);
            MinimumEffectFloor = Mathf.Max(0f, minimumEffectFloor);
            MaximumSeverity = float.IsNaN(maximumSeverity) || float.IsInfinity(maximumSeverity) ? 999f : Mathf.Clamp(maximumSeverity, 0f, 999f);
            BodyRevision = bodyRevision;
            AnatomyRevision = anatomyRevision;
            ConditionRevision = conditionRevision;
            VitalRevision = vitalRevision;
            HazardRevision = hazardRevision;
            CompatibilityRevision = compatibilityRevision;
            RuleTrace = traces == null ? Array.Empty<BiologicalInteractionRuleTrace>() : traces.ToArray();
        }

        public BiologicalCompatibilityResultCode Code { get; }
        public string Message { get; }
        public string ActorBodyId { get; }
        public string InteractionDefinitionId { get; }
        public BiologicalInteractionCategory Category { get; }
        public BiologicalCompatibilityState CompatibilityState { get; }
        public bool Compatible => Code == BiologicalCompatibilityResultCode.Success && CompatibilityState == BiologicalCompatibilityState.Compatible && !Immune && !Suppressed;
        public bool Immune { get; }
        public bool Suppressed { get; }
        public bool Affinity { get; }
        public bool Absorbed { get; }
        public string ConvertedInteractionDefinitionId { get; }
        public bool Converted => !string.IsNullOrWhiteSpace(ConvertedInteractionDefinitionId);
        public float RateMultiplier { get; }
        public float SeverityMultiplier { get; }
        public float ConsequenceMultiplier { get; }
        public float MinimumEffectFloor { get; }
        public float MaximumSeverity { get; }
        public long BodyRevision { get; }
        public long AnatomyRevision { get; }
        public long ConditionRevision { get; }
        public long VitalRevision { get; }
        public long HazardRevision { get; }
        public long CompatibilityRevision { get; }
        public IReadOnlyList<BiologicalInteractionRuleTrace> RuleTrace { get; }

        public static BiologicalInteractionEvaluationResult Failure(BiologicalCompatibilityResultCode code, string message, string actorBodyId = "", string interactionDefinitionId = "")
        {
            return new BiologicalInteractionEvaluationResult(
                code,
                message,
                actorBodyId,
                interactionDefinitionId,
                BiologicalInteractionCategory.Unknown,
                BiologicalCompatibilityState.Incompatible,
                false,
                false,
                false,
                false,
                string.Empty,
                0f,
                0f,
                0f,
                0f,
                0f,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<BiologicalInteractionRuleTrace>());
        }
    }

    public sealed class BiologicalCompatibilityOperationResult
    {
        private BiologicalCompatibilityOperationResult(bool succeeded, BiologicalCompatibilityResultCode code, string message, bool duplicate, BiologicalCompatibilitySnapshot snapshot)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Duplicate = duplicate;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public BiologicalCompatibilityResultCode Code { get; }
        public string Message { get; }
        public bool Duplicate { get; }
        public BiologicalCompatibilitySnapshot Snapshot { get; }

        public static BiologicalCompatibilityOperationResult Success(string message, BiologicalCompatibilitySnapshot snapshot, bool duplicate = false)
        {
            return new BiologicalCompatibilityOperationResult(true, duplicate ? BiologicalCompatibilityResultCode.Duplicate : BiologicalCompatibilityResultCode.Success, message, duplicate, snapshot);
        }

        public static BiologicalCompatibilityOperationResult Failure(BiologicalCompatibilityResultCode code, string message, BiologicalCompatibilitySnapshot snapshot = null)
        {
            return new BiologicalCompatibilityOperationResult(false, code, message, false, snapshot);
        }
    }
}
