using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(fileName = "ProfessionSpecializationDefinition", menuName = "Unity Isekai Game/Professions/Profession Specialization Definition")]
    public sealed class ProfessionSpecializationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string specializationId;
        [SerializeField] private string parentProfessionId;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string[] relatedSkillIds;
        [SerializeField] private string[] knowledgeSubjectIds;
        [SerializeField] private string[] relatedCapabilityIds;
        [SerializeField] private string[] productionActivityCategoryIds;
        [SerializeField] private ProfessionRecognitionForm recognitionForm = ProfessionRecognitionForm.Either;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField] private int version = 1;
        [SerializeField] private string validationMetadata;

        public string Id => specializationId;
        public string ParentProfessionId => parentProfessionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string DebugName => string.IsNullOrWhiteSpace(debugName) ? DisplayName : debugName;
        public string Description => description ?? string.Empty;
        public IReadOnlyList<string> RelatedSkillIds => relatedSkillIds ?? Array.Empty<string>();
        public IReadOnlyList<string> KnowledgeSubjectIds => knowledgeSubjectIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RelatedCapabilityIds => relatedCapabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ProductionActivityCategoryIds => productionActivityCategoryIds ?? Array.Empty<string>();
        public ProfessionRecognitionForm RecognitionForm => recognitionForm;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public int Version => version;
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            specializationId = specializationId?.Trim();
            parentProfessionId = parentProfessionId?.Trim();
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string parentProfession,
            string name,
            ProfessionRecognitionForm form,
            string[] skills = null,
            string[] knowledgeSubjects = null,
            string[] capabilities = null,
            string[] activities = null,
            string accessPolicyId = "")
        {
            specializationId = id?.Trim();
            parentProfessionId = parentProfession?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            debugName = displayName;
            recognitionForm = form;
            relatedSkillIds = Clean(skills);
            knowledgeSubjectIds = Clean(knowledgeSubjects);
            relatedCapabilityIds = Clean(capabilities);
            productionActivityCategoryIds = Clean(activities);
            defaultAccessPolicyId = accessPolicyId ?? string.Empty;
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Profession Specialization '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("profession.specialization.", StringComparison.Ordinal))
            {
                report.AddWarning($"Profession Specialization '{Id}' should use the 'profession.specialization.' namespace prefix.");
            }

            if (string.IsNullOrWhiteSpace(ParentProfessionId))
            {
                report.AddError($"Profession Specialization '{DisplayName}' must declare a parent profession ID.");
            }
            else if (definitionsById != null
                && (!definitionsById.TryGetValue(ParentProfessionId, out IGameDefinition parent)
                    || parent is not ProfessionDefinition))
            {
                report.AddError($"Profession Specialization '{DisplayName}' references missing parent Profession '{ParentProfessionId}'.");
            }

            if (!Enum.IsDefined(typeof(ProfessionRecognitionForm), recognitionForm))
            {
                report.AddError($"Profession Specialization '{DisplayName}' has invalid recognition form '{recognitionForm}'.");
            }

            if (version < 1)
            {
                report.AddError($"Profession Specialization '{DisplayName}' has invalid version '{version}'.");
            }

            if (!string.IsNullOrWhiteSpace(DefaultAccessPolicyId)
                && definitionsById != null
                && (!definitionsById.TryGetValue(DefaultAccessPolicyId, out IGameDefinition policyDefinition)
                    || policyDefinition is not InformationAccessPolicyDefinition))
            {
                report.AddError($"Profession Specialization '{DisplayName}' references missing Information Access policy '{DefaultAccessPolicyId}'.");
            }
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
