using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Capabilities;

namespace UnityIsekaiGame.Beings.Biology.Compatibility
{
    [Serializable]
    public sealed class BiologicalInteractionRuleDefinition
    {
        [SerializeField] private string entryId;
        [SerializeField] private BiologicalCompatibilitySourceKind sourceKind = BiologicalCompatibilitySourceKind.System;
        [SerializeField] private string sourceId;
        [SerializeField] private BiologicalInteractionDefinition interactionDefinition;
        [SerializeField] private BiologicalInteractionCategory category = BiologicalInteractionCategory.Unknown;
        [SerializeField] private BiologicalInteractionRuleKind ruleKind = BiologicalInteractionRuleKind.Resistance;
        [SerializeField] private BiologicalCompatibilityState compatibilityState = BiologicalCompatibilityState.Compatible;
        [SerializeField, Min(0f)] private float rateMultiplier = 1f;
        [SerializeField, Min(0f)] private float severityMultiplier = 1f;
        [SerializeField, Min(0f)] private float consequenceMultiplier = 1f;
        [SerializeField, Min(0f)] private float minimumEffectFloor;
        [SerializeField, Min(0f)] private float maximumSeverity = float.PositiveInfinity;
        [SerializeField] private int priority;
        [SerializeField] private BiologicalInteractionDefinition convertedInteractionDefinition;
        [SerializeField] private CapabilityDefinition[] requiredCapabilities;
        [SerializeField] private CapabilityDefinition[] blockingCapabilities;
        [SerializeField] private string[] requiredAnatomyTagIds;
        [SerializeField] private AnatomyStructuralCategory[] requiredNodeCategories;
        [SerializeField] private string requiredNodeId;
        [SerializeField, TextArea(1, 3)] private string explanation;
        [SerializeField] private bool alphaEnabled = true;

        public string EntryId => entryId ?? string.Empty;
        public BiologicalCompatibilitySourceKind SourceKind => sourceKind;
        public string SourceId => sourceId ?? string.Empty;
        public BiologicalInteractionDefinition InteractionDefinition => interactionDefinition;
        public string InteractionDefinitionId => interactionDefinition == null ? string.Empty : interactionDefinition.Id;
        public BiologicalInteractionCategory Category => category;
        public BiologicalInteractionRuleKind RuleKind => ruleKind;
        public BiologicalCompatibilityState CompatibilityState => compatibilityState;
        public float RateMultiplier => Mathf.Max(0f, rateMultiplier);
        public float SeverityMultiplier => Mathf.Max(0f, severityMultiplier);
        public float ConsequenceMultiplier => Mathf.Max(0f, consequenceMultiplier);
        public float MinimumEffectFloor => Mathf.Max(0f, minimumEffectFloor);
        public float MaximumSeverity => float.IsNaN(maximumSeverity) ? float.PositiveInfinity : Mathf.Max(0f, maximumSeverity);
        public int Priority => priority;
        public BiologicalInteractionDefinition ConvertedInteractionDefinition => convertedInteractionDefinition;
        public string ConvertedInteractionDefinitionId => convertedInteractionDefinition == null ? string.Empty : convertedInteractionDefinition.Id;
        public CapabilityDefinition[] RequiredCapabilities => requiredCapabilities ?? Array.Empty<CapabilityDefinition>();
        public CapabilityDefinition[] BlockingCapabilities => blockingCapabilities ?? Array.Empty<CapabilityDefinition>();
        public string[] RequiredRuntimeCapabilityKeys => RequiredCapabilities.Select(definition => definition.Id).ToArray();
        public string[] BlockingRuntimeCapabilityKeys => BlockingCapabilities.Select(definition => definition.Id).ToArray();
        public string[] RequiredAnatomyTagIds => requiredAnatomyTagIds ?? Array.Empty<string>();
        public AnatomyStructuralCategory[] RequiredNodeCategories => requiredNodeCategories ?? Array.Empty<AnatomyStructuralCategory>();
        public string RequiredNodeId => requiredNodeId ?? string.Empty;
        public string Explanation => explanation ?? string.Empty;
        public bool AlphaEnabled => alphaEnabled;

        public RuntimeBiologicalInteractionRule ToRuntimeRule(string fallbackSourceId)
        {
            return new RuntimeBiologicalInteractionRule(
                EntryId,
                SourceKind,
                string.IsNullOrWhiteSpace(SourceId) ? fallbackSourceId : SourceId,
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
    }
}
