using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Skills;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(fileName = "ProfessionDefinition", menuName = "Unity Isekai Game/Professions/Profession Definition")]
    public sealed class ProfessionDefinition : ScriptableObject, IGameDefinition, ICategorizableDefinition, ITaggedDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string professionId;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ProfessionCategory category = ProfessionCategory.Custom;
        [SerializeField] private ProfessionRecognitionForm recognitionForm = ProfessionRecognitionForm.Either;
        [SerializeField] private CategoryDefinition primaryCategory;
        [SerializeField] private TagDefinition[] tags;
        [SerializeField] private string[] relatedSkillIds;
        [SerializeField] private string[] knowledgeSubjectIds;
        [SerializeField] private string[] relatedCapabilityIds;
        [SerializeField] private string[] productionActivityCategoryIds;
        [SerializeField] private string[] allowedSpecializationIds;
        [SerializeField] private string[] recognizingOrganizationTypeIds;
        [SerializeField] private string[] recognizingAuthorityIds;
        [SerializeField] private bool selfDeclarationAllowed = true;
        [SerializeField] private bool formalRecognitionPossible = true;
        [SerializeField] private bool secretAllowed;
        [SerializeField] private bool illegal;
        [SerializeField] private bool restricted;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField] private int version = 1;
        [SerializeField] private string validationMetadata;

        public string Id => professionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string DebugName => string.IsNullOrWhiteSpace(debugName) ? DisplayName : debugName;
        public string Description => description ?? string.Empty;
        public ProfessionCategory Category => category;
        public ProfessionRecognitionForm RecognitionForm => recognitionForm;
        public CategoryDefinition PrimaryCategory => primaryCategory;
        public CategoryDomain ClassificationDomain => CategoryDomain.Profession;
        public IReadOnlyList<TagDefinition> Tags => tags ?? Array.Empty<TagDefinition>();
        public IReadOnlyList<string> RelatedSkillIds => relatedSkillIds ?? Array.Empty<string>();
        public IReadOnlyList<string> KnowledgeSubjectIds => knowledgeSubjectIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RelatedCapabilityIds => relatedCapabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ProductionActivityCategoryIds => productionActivityCategoryIds ?? Array.Empty<string>();
        public IReadOnlyList<string> AllowedSpecializationIds => allowedSpecializationIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RecognizingOrganizationTypeIds => recognizingOrganizationTypeIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RecognizingAuthorityIds => recognizingAuthorityIds ?? Array.Empty<string>();
        public bool SelfDeclarationAllowed => selfDeclarationAllowed;
        public bool FormalRecognitionPossible => formalRecognitionPossible;
        public bool SecretAllowed => secretAllowed;
        public bool Illegal => illegal;
        public bool Restricted => restricted;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public int Version => version;
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            professionId = professionId?.Trim();
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            ProfessionCategory professionCategory,
            ProfessionRecognitionForm form,
            string[] skills = null,
            string[] knowledgeSubjects = null,
            string[] capabilities = null,
            string[] activities = null,
            string[] specializations = null,
            string[] authorities = null,
            bool allowSelfDeclaration = true,
            bool allowFormalRecognition = true,
            bool allowSecret = false,
            bool isIllegal = false,
            bool isRestricted = false,
            string accessPolicyId = "",
            string[] tagIds = null)
        {
            professionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            debugName = displayName;
            category = professionCategory;
            recognitionForm = form;
            relatedSkillIds = Clean(skills);
            knowledgeSubjectIds = Clean(knowledgeSubjects);
            relatedCapabilityIds = Clean(capabilities);
            productionActivityCategoryIds = Clean(activities);
            allowedSpecializationIds = Clean(specializations);
            recognizingAuthorityIds = Clean(authorities);
            recognizingOrganizationTypeIds = Array.Empty<string>();
            selfDeclarationAllowed = allowSelfDeclaration;
            formalRecognitionPossible = allowFormalRecognition;
            secretAllowed = allowSecret;
            illegal = isIllegal;
            restricted = isRestricted;
            defaultAccessPolicyId = accessPolicyId ?? string.Empty;
            validationMetadata = tagIds == null ? string.Empty : string.Join(",", Clean(tagIds));
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
                report.AddError($"Profession '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("profession.", StringComparison.Ordinal))
            {
                report.AddWarning($"Profession '{Id}' should use the 'profession.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(ProfessionCategory), category))
            {
                report.AddError($"Profession '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(ProfessionRecognitionForm), recognitionForm))
            {
                report.AddError($"Profession '{DisplayName}' has invalid recognition form '{recognitionForm}'.");
            }

            if (version < 1)
            {
                report.AddError($"Profession '{DisplayName}' has invalid version '{version}'.");
            }

            ValidateUniqueIds(RelatedSkillIds, "relatedSkillIds", report);
            ValidateUniqueIds(KnowledgeSubjectIds, "knowledgeSubjectIds", report);
            ValidateUniqueIds(RelatedCapabilityIds, "relatedCapabilityIds", report);
            ValidateUniqueIds(ProductionActivityCategoryIds, "productionActivityCategoryIds", report);
            ValidateUniqueIds(AllowedSpecializationIds, "allowedSpecializationIds", report);
            ValidateUniqueIds(RecognizingAuthorityIds, "recognizingAuthorityIds", report);

            ValidateReferences<SkillDefinition>(RelatedSkillIds, definitionsById, "relatedSkillIds", report);
            ValidateReferences<KnowledgeFactDefinition>(KnowledgeSubjectIds, definitionsById, "knowledgeSubjectIds", report, allowMissing: true);
            ValidateReferences<CapabilityDefinition>(RelatedCapabilityIds, definitionsById, "relatedCapabilityIds", report);
            ValidateReferences<ProfessionSpecializationDefinition>(AllowedSpecializationIds, definitionsById, "allowedSpecializationIds", report);

            foreach (string specializationId in AllowedSpecializationIds)
            {
                if (definitionsById != null
                    && definitionsById.TryGetValue(specializationId, out IGameDefinition definition)
                    && definition is ProfessionSpecializationDefinition specialization
                    && !string.Equals(specialization.ParentProfessionId, Id, StringComparison.Ordinal))
                {
                    report.AddError($"Profession '{DisplayName}' lists specialization '{specializationId}' whose parent is '{specialization.ParentProfessionId}', expected '{Id}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(DefaultAccessPolicyId)
                && definitionsById != null
                && (!definitionsById.TryGetValue(DefaultAccessPolicyId, out IGameDefinition policyDefinition)
                    || policyDefinition is not InformationAccessPolicyDefinition))
            {
                report.AddError($"Profession '{DisplayName}' references missing Information Access policy '{DefaultAccessPolicyId}'.");
            }
        }

        internal bool AllowsSpecialization(string specializationId)
        {
            return !string.IsNullOrWhiteSpace(specializationId)
                && AllowedSpecializationIds.Contains(specializationId, StringComparer.Ordinal);
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

        private void ValidateUniqueIds(IReadOnlyList<string> ids, string field, DefinitionValidationReport report)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.AddError($"Profession '{DisplayName}' field '{field}' contains a blank ID.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    report.AddError($"Profession '{DisplayName}' field '{field}' contains duplicate ID '{id}'.");
                }
            }
        }

        private void ValidateReferences<TDefinition>(IReadOnlyList<string> ids, IReadOnlyDictionary<string, IGameDefinition> definitionsById, string field, DefinitionValidationReport report, bool allowMissing = false)
            where TDefinition : class, IGameDefinition
        {
            if (definitionsById == null)
            {
                return;
            }

            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (!definitionsById.TryGetValue(id, out IGameDefinition definition))
                {
                    if (!allowMissing)
                    {
                        report.AddError($"Profession '{DisplayName}' field '{field}' references missing definition '{id}'.");
                    }

                    continue;
                }

                if (definition is not TDefinition)
                {
                    report.AddError($"Profession '{DisplayName}' field '{field}' references '{id}' as {typeof(TDefinition).Name}, but found {definition.GetType().Name}.");
                }
            }
        }
    }

}
