using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Knowledge.Observation
{
    [CreateAssetMenu(fileName = "DiagnosticMethodDefinition", menuName = "Unity Isekai Game/Knowledge/Diagnostic Method")]
    public sealed class DiagnosticMethodDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string methodId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private DiagnosticMethodCategory category;
        [SerializeField] private bool active;
        [SerializeField] private string factDefinitionId = BuiltInKnowledgeFacts.BodySymptom;
        [SerializeField] private string requiredKnowledgeDomain;
        [SerializeField] private string requiredSkillId;
        [SerializeField] private string requiredToolTagId;
        [SerializeField] private string[] candidateConditionFamilies;
        [SerializeField] private string[] candidateDefinitionTags;
        [SerializeField, Range(0, 1000)] private int evidenceWeight = 600;
        [SerializeField, Range(0, 1000)] private int confidenceCeiling = 800;
        [SerializeField, Range(0, 1000)] private int exactDiagnosisThreshold = 720;
        [SerializeField, Range(0, 1000)] private int differentialHypothesisThreshold = 300;
        [SerializeField] private bool allowsFalsePositive = true;
        [SerializeField] private bool allowsFalseNegative = true;
        [SerializeField] private ObservationAccessLevel requiredAccess = ObservationAccessLevel.Medical;
        [SerializeField] private string validationMetadata;

        public string Id => methodId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public DiagnosticMethodCategory Category => category;
        public bool Active => active;
        public string FactDefinitionId => factDefinitionId ?? string.Empty;
        public string RequiredKnowledgeDomain => requiredKnowledgeDomain ?? string.Empty;
        public string RequiredSkillId => requiredSkillId ?? string.Empty;
        public string RequiredToolTagId => requiredToolTagId ?? string.Empty;
        public IReadOnlyList<string> CandidateConditionFamilies => candidateConditionFamilies ?? Array.Empty<string>();
        public IReadOnlyList<string> CandidateDefinitionTags => candidateDefinitionTags ?? Array.Empty<string>();
        public int EvidenceWeight => KnowledgeConfidence.Clamp(evidenceWeight);
        public int ConfidenceCeiling => KnowledgeConfidence.Clamp(confidenceCeiling);
        public int ExactDiagnosisThreshold => KnowledgeConfidence.Clamp(exactDiagnosisThreshold);
        public int DifferentialHypothesisThreshold => KnowledgeConfidence.Clamp(differentialHypothesisThreshold);
        public bool AllowsFalsePositive => allowsFalsePositive;
        public bool AllowsFalseNegative => allowsFalseNegative;
        public ObservationAccessLevel RequiredAccess => requiredAccess;
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            methodId = methodId?.Trim();
            evidenceWeight = KnowledgeConfidence.Clamp(evidenceWeight);
            confidenceCeiling = KnowledgeConfidence.Clamp(confidenceCeiling);
            exactDiagnosisThreshold = KnowledgeConfidence.Clamp(exactDiagnosisThreshold);
            differentialHypothesisThreshold = KnowledgeConfidence.Clamp(differentialHypothesisThreshold);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            MethodValidation.ValidateMethod(this, "Diagnostic Method", "diagnostic-method.", category != DiagnosticMethodCategory.Unknown, confidenceCeiling, KnowledgeTrackingPolicy.PlayerMechanicalOnly, report);
            if (string.IsNullOrWhiteSpace(FactDefinitionId))
            {
                report?.AddError($"Diagnostic Method '{DisplayName}' must declare a Fact definition ID.");
            }
            else if (definitionsById != null && !definitionsById.ContainsKey(FactDefinitionId))
            {
                report?.AddError($"Diagnostic Method '{DisplayName}' references missing Fact definition '{FactDefinitionId}'.");
            }

            if (DifferentialHypothesisThreshold > ExactDiagnosisThreshold)
            {
                report?.AddError($"Diagnostic Method '{DisplayName}' differential threshold cannot exceed exact threshold.");
            }

            if (ExactDiagnosisThreshold > ConfidenceCeiling)
            {
                report?.AddWarning($"Diagnostic Method '{DisplayName}' exact threshold exceeds its confidence ceiling; exact diagnosis will not be reachable.");
            }

            MethodValidation.ValidateOptionalReference(RequiredSkillId, definitionsById, "skill", Id, report);
        }
    }
}
