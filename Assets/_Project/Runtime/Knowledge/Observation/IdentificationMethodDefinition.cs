using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Observation
{
    [CreateAssetMenu(fileName = "IdentificationMethodDefinition", menuName = "Unity Isekai Game/Knowledge/Identification Method")]
    public sealed class IdentificationMethodDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string methodId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private IdentificationMethodCategory category;
        [SerializeField] private bool active;
        [SerializeField] private ObservationTargetType targetType;
        [SerializeField] private string factDefinitionId;
        [SerializeField, Range(0, 1000)] private int partialThreshold = 350;
        [SerializeField, Range(0, 1000)] private int exactThreshold = 700;
        [SerializeField, Range(0, 1000)] private int falsePositiveRisk = 100;
        [SerializeField] private bool allowsMisidentification = true;
        [SerializeField] private string requiredSkillId;
        [SerializeField] private string[] candidateTags;
        [SerializeField] private string validationMetadata;

        public string Id => methodId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public IdentificationMethodCategory Category => category;
        public bool Active => active;
        public ObservationTargetType TargetType => targetType;
        public string FactDefinitionId => factDefinitionId ?? string.Empty;
        public int PartialThreshold => KnowledgeConfidence.Clamp(partialThreshold);
        public int ExactThreshold => KnowledgeConfidence.Clamp(exactThreshold);
        public int FalsePositiveRisk => KnowledgeConfidence.Clamp(falsePositiveRisk);
        public bool AllowsMisidentification => allowsMisidentification;
        public string RequiredSkillId => requiredSkillId ?? string.Empty;
        public IReadOnlyList<string> CandidateTags => candidateTags ?? Array.Empty<string>();
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            methodId = methodId?.Trim();
            partialThreshold = KnowledgeConfidence.Clamp(partialThreshold);
            exactThreshold = KnowledgeConfidence.Clamp(exactThreshold);
            falsePositiveRisk = KnowledgeConfidence.Clamp(falsePositiveRisk);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            MethodValidation.ValidateMethod(this, "Identification Method", "identification-method.", category != IdentificationMethodCategory.Unknown, exactThreshold, KnowledgeTrackingPolicy.PlayerMechanicalOnly, report);
            if (targetType == ObservationTargetType.Unknown)
            {
                report?.AddError($"Identification Method '{DisplayName}' must declare a concrete target type.");
            }

            if (string.IsNullOrWhiteSpace(FactDefinitionId))
            {
                report?.AddError($"Identification Method '{DisplayName}' must declare a Fact definition ID.");
            }
            else if (definitionsById != null && !definitionsById.ContainsKey(FactDefinitionId))
            {
                report?.AddError($"Identification Method '{DisplayName}' references missing Fact definition '{FactDefinitionId}'.");
            }

            if (PartialThreshold > ExactThreshold)
            {
                report?.AddError($"Identification Method '{DisplayName}' partial threshold cannot exceed exact threshold.");
            }

            MethodValidation.ValidateOptionalReference(RequiredSkillId, definitionsById, "skill", Id, report);
        }
    }
}
