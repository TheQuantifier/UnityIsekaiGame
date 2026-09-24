using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Observation
{
    [CreateAssetMenu(fileName = "ObservationMethodDefinition", menuName = "Unity Isekai Game/Knowledge/Observation Method")]
    public sealed class ObservationMethodDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string methodId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ObservationMethodCategory category;
        [SerializeField] private SensoryChannel[] sensoryChannels;
        [SerializeField] private ObservationTargetType[] targetTypes;
        [SerializeField] private bool active;
        [SerializeField, Min(0f)] private float maximumRange = 12f;
        [SerializeField] private bool requiresLineOfSight = true;
        [SerializeField] private bool requiresConsent;
        [SerializeField] private bool privacyBypass;
        [SerializeField, Range(0, 1000)] private int baseObservationQuality = 550;
        [SerializeField, Range(0, 2000)] private int evidenceStrengthMultiplier = 1000;
        [SerializeField] private KnowledgeTrackingPolicy defaultTrackingPolicy = KnowledgeTrackingPolicy.PlayerMechanicalOnly;
        [SerializeField] private RepeatedObservationPolicy repeatedObservationPolicy = RepeatedObservationPolicy.MergeDuplicateTransaction;
        [SerializeField] private string requiredCapabilityId;
        [SerializeField] private string requiredTraitId;
        [SerializeField] private string requiredSkillId;
        [SerializeField] private string requiredEquipmentTagId;
        [SerializeField] private string[] tags;
        [SerializeField] private string validationMetadata;

        public string Id => methodId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public ObservationMethodCategory Category => category;
        public IReadOnlyList<SensoryChannel> SensoryChannels => sensoryChannels ?? Array.Empty<SensoryChannel>();
        public IReadOnlyList<ObservationTargetType> TargetTypes => targetTypes ?? Array.Empty<ObservationTargetType>();
        public bool Active => active;
        public float MaximumRange => Mathf.Max(0f, maximumRange);
        public bool RequiresLineOfSight => requiresLineOfSight;
        public bool RequiresConsent => requiresConsent;
        public bool PrivacyBypass => privacyBypass;
        public int BaseObservationQuality => KnowledgeConfidence.Clamp(baseObservationQuality);
        public int EvidenceStrengthMultiplier => Mathf.Clamp(evidenceStrengthMultiplier, 0, 2000);
        public KnowledgeTrackingPolicy DefaultTrackingPolicy => defaultTrackingPolicy;
        public RepeatedObservationPolicy RepeatedObservationPolicy => repeatedObservationPolicy;
        public string RequiredCapabilityId => requiredCapabilityId ?? string.Empty;
        public string RequiredTraitId => requiredTraitId ?? string.Empty;
        public string RequiredSkillId => requiredSkillId ?? string.Empty;
        public string RequiredEquipmentTagId => requiredEquipmentTagId ?? string.Empty;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            methodId = methodId?.Trim();
            baseObservationQuality = KnowledgeConfidence.Clamp(baseObservationQuality);
            evidenceStrengthMultiplier = Mathf.Clamp(evidenceStrengthMultiplier, 0, 2000);
            maximumRange = Mathf.Max(0f, maximumRange);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            MethodValidation.ValidateMethod(this, "Observation Method", "observation-method.", category != ObservationMethodCategory.Unknown, BaseObservationQuality, defaultTrackingPolicy, report);
            if (SensoryChannels.Count == 0)
            {
                report?.AddError($"Observation Method '{DisplayName}' must declare at least one sensory channel.");
            }

            if (TargetTypes.Count == 0)
            {
                report?.AddError($"Observation Method '{DisplayName}' must declare at least one target type.");
            }

            MethodValidation.ValidateOptionalReference(RequiredCapabilityId, definitionsById, "capability", Id, report);
            MethodValidation.ValidateOptionalReference(RequiredTraitId, definitionsById, "trait", Id, report);
            MethodValidation.ValidateOptionalReference(RequiredSkillId, definitionsById, "skill", Id, report);
        }
    }

    internal static class MethodValidation
    {
        public static void ValidateMethod(IGameDefinition definition, string label, string prefix, bool hasCategory, int quality, KnowledgeTrackingPolicy trackingPolicy, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                report.AddError($"{label} is missing a stable ID.");
                return;
            }

            if (!definition.Id.StartsWith(prefix, StringComparison.Ordinal))
            {
                report.AddWarning($"{label} '{definition.Id}' should use the '{prefix}' namespace prefix.");
            }

            if (!hasCategory)
            {
                report.AddError($"{label} '{definition.Id}' must declare a concrete category.");
            }

            if (quality < KnowledgeConfidence.Minimum || quality > KnowledgeConfidence.Maximum)
            {
                report.AddError($"{label} '{definition.Id}' has an invalid quality range.");
            }

            if (!Enum.IsDefined(typeof(KnowledgeTrackingPolicy), trackingPolicy) || trackingPolicy == KnowledgeTrackingPolicy.None)
            {
                report.AddError($"{label} '{definition.Id}' has an invalid tracking policy.");
            }
        }

        public static void ValidateOptionalReference(string referencedId, IReadOnlyDictionary<string, IGameDefinition> definitionsById, string referenceKind, string ownerId, DefinitionValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(referencedId) || definitionsById == null || definitionsById.ContainsKey(referencedId))
            {
                return;
            }

            report?.AddError($"Definition '{ownerId}' references missing {referenceKind} definition '{referencedId}'.");
        }
    }
}
