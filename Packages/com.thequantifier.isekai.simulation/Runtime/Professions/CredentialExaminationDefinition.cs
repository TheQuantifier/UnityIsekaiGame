using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Credential Examination Definition")]
    public sealed class CredentialExaminationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField] private string[] relatedCredentialDefinitionIds = Array.Empty<string>();
        [SerializeField] private CredentialAssessmentCategory assessmentCategory = CredentialAssessmentCategory.Custom;
        [SerializeField] private string[] knowledgeSubjectIds = Array.Empty<string>();
        [SerializeField] private string[] requiredSkillOrCapabilityIds = Array.Empty<string>();
        [SerializeField] private string[] practicalActivityDefinitionIds = Array.Empty<string>();
        [SerializeField] private int passingScore = 700;
        [SerializeField] private int attemptLimit = 3;
        [SerializeField] private string retakePolicyId;
        [SerializeField] private string[] requiredEvaluatorAuthorityIds = Array.Empty<string>();
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string DebugName => debugName ?? string.Empty;
        public IReadOnlyList<string> RelatedCredentialDefinitionIds => CredentialDefinition.Clean(relatedCredentialDefinitionIds);
        public CredentialAssessmentCategory AssessmentCategory => assessmentCategory;
        public IReadOnlyList<string> KnowledgeSubjectIds => CredentialDefinition.Clean(knowledgeSubjectIds);
        public IReadOnlyList<string> RequiredSkillOrCapabilityIds => CredentialDefinition.Clean(requiredSkillOrCapabilityIds);
        public IReadOnlyList<string> PracticalActivityDefinitionIds => CredentialDefinition.Clean(practicalActivityDefinitionIds);
        public int PassingScore => Math.Max(0, Math.Min(1000, passingScore));
        public int AttemptLimit => Math.Max(1, attemptLimit);
        public string RetakePolicyId => retakePolicyId ?? string.Empty;
        public IReadOnlyList<string> RequiredEvaluatorAuthorityIds => CredentialDefinition.Clean(requiredEvaluatorAuthorityIds);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string name,
            IEnumerable<string> credentialDefinitionIds,
            CredentialAssessmentCategory category,
            int passScore,
            int maxAttempts,
            IEnumerable<string> evaluatorAuthorityIds,
            IEnumerable<string> knowledgeSubjects = null,
            IEnumerable<string> skillOrCapabilityIds = null,
            IEnumerable<string> practicalActivityIds = null,
            string policyId = "",
            string retakePolicy = "",
            int definitionVersion = 1)
        {
            id = stableId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            debugName = displayName;
            relatedCredentialDefinitionIds = CredentialDefinition.Clean(credentialDefinitionIds);
            assessmentCategory = category;
            passingScore = Math.Max(0, Math.Min(1000, passScore));
            attemptLimit = Math.Max(1, maxAttempts);
            requiredEvaluatorAuthorityIds = CredentialDefinition.Clean(evaluatorAuthorityIds);
            knowledgeSubjectIds = CredentialDefinition.Clean(knowledgeSubjects);
            requiredSkillOrCapabilityIds = CredentialDefinition.Clean(skillOrCapabilityIds);
            practicalActivityDefinitionIds = CredentialDefinition.Clean(practicalActivityIds);
            accessPolicyId = policyId ?? string.Empty;
            retakePolicyId = retakePolicy ?? string.Empty;
            version = Math.Max(1, definitionVersion);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Credential Examination definition has no stable ID.");
            }
            else if (!Id.StartsWith("examination.", StringComparison.Ordinal))
            {
                report.AddWarning($"Credential Examination definition '{DisplayName}' should use the 'examination.' namespace prefix.");
            }

            if (RelatedCredentialDefinitionIds.Count == 0)
            {
                report.AddError($"Credential Examination definition '{DisplayName}' must reference at least one credential definition.");
            }

            foreach (string credentialId in RelatedCredentialDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(credentialId, out IGameDefinition definition) || definition is not CredentialDefinition)
                {
                    report.AddError($"Credential Examination definition '{DisplayName}' references missing Credential '{credentialId}'.");
                }
            }

            if (RequiredEvaluatorAuthorityIds.Count == 0)
            {
                report.AddError($"Credential Examination definition '{DisplayName}' must require an evaluator authority.");
            }

            if (PassingScore <= 0)
            {
                report.AddError($"Credential Examination definition '{DisplayName}' has invalid passing score.");
            }

            if (Version <= 0)
            {
                report.AddError($"Credential Examination definition '{DisplayName}' has invalid version.");
            }
        }
    }
}
