using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(fileName = "ProfessionEntryPathDefinition", menuName = "Unity Isekai Game/Professions/Profession Entry Path")]
    public sealed class ProfessionEntryPathDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string entryPathId;
        [SerializeField] private string professionId;
        [SerializeField] private string specializationId;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ProfessionEntryType entryType = ProfessionEntryType.SelfDeclaredPractice;
        [SerializeField] private ProfessionEntryFormality formality = ProfessionEntryFormality.Informal;
        [SerializeField] private ProfessionSelfDeclarationPolicy selfDeclarationPolicy = ProfessionSelfDeclarationPolicy.Allowed;
        [SerializeField] private string[] recognizingAuthorityIds;
        [SerializeField] private RequirementSetDefinition requirementSet;
        [SerializeField] private int minimumAge;
        [SerializeField] private string[] allowedLifeStageIds;
        [SerializeField] private string[] requiredSkillIds;
        [SerializeField] private string[] requiredKnowledgeSubjectIds;
        [SerializeField] private string[] requiredCapabilityIds;
        [SerializeField] private string[] requiredTraitIds;
        [SerializeField] private string[] requiredStatusIds;
        [SerializeField] private string[] requiredOrganizationIds;
        [SerializeField] private string[] requiredAccessKeys;
        [SerializeField] private string[] requiredActiveProfessionIds;
        [SerializeField] private string[] prohibitedActiveProfessionIds;
        [SerializeField] private string[] exclusiveProfessionIds;
        [SerializeField] private bool allowSecretEntry;
        [SerializeField] private bool allowDisputedEntry;
        [SerializeField] private bool allowRestrictedEntry;
        [SerializeField] private bool allowIllegalEntry;
        [SerializeField] private bool requiresRecognizingAuthority;
        [SerializeField] private bool immediateApprovalAllowed = true;
        [SerializeField] private bool specializationRequiresParentActive = true;
        [SerializeField] private ProfessionReentryPolicy reentryPolicy = ProfessionReentryPolicy.NotApplicable;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField] private int version = 1;
        [SerializeField] private string validationMetadata;

        public string Id => entryPathId ?? string.Empty;
        public string ProfessionId => professionId ?? string.Empty;
        public string SpecializationId => specializationId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string DebugName => string.IsNullOrWhiteSpace(debugName) ? DisplayName : debugName;
        public string Description => description ?? string.Empty;
        public ProfessionEntryType EntryType => entryType;
        public ProfessionEntryFormality Formality => formality;
        public ProfessionSelfDeclarationPolicy SelfDeclarationPolicy => selfDeclarationPolicy;
        public IReadOnlyList<string> RecognizingAuthorityIds => recognizingAuthorityIds ?? Array.Empty<string>();
        public RequirementSetDefinition RequirementSet => requirementSet;
        public int MinimumAge => Math.Max(0, minimumAge);
        public IReadOnlyList<string> AllowedLifeStageIds => allowedLifeStageIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredSkillIds => requiredSkillIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredKnowledgeSubjectIds => requiredKnowledgeSubjectIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredCapabilityIds => requiredCapabilityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredTraitIds => requiredTraitIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredStatusIds => requiredStatusIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredOrganizationIds => requiredOrganizationIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredAccessKeys => requiredAccessKeys ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredActiveProfessionIds => requiredActiveProfessionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ProhibitedActiveProfessionIds => prohibitedActiveProfessionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ExclusiveProfessionIds => exclusiveProfessionIds ?? Array.Empty<string>();
        public bool AllowSecretEntry => allowSecretEntry;
        public bool AllowDisputedEntry => allowDisputedEntry;
        public bool AllowRestrictedEntry => allowRestrictedEntry;
        public bool AllowIllegalEntry => allowIllegalEntry;
        public bool RequiresRecognizingAuthority => requiresRecognizingAuthority;
        public bool ImmediateApprovalAllowed => immediateApprovalAllowed;
        public bool SpecializationRequiresParentActive => specializationRequiresParentActive;
        public ProfessionReentryPolicy ReentryPolicy => reentryPolicy;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);
        public string ValidationMetadata => validationMetadata ?? string.Empty;

        private void OnValidate()
        {
            entryPathId = entryPathId?.Trim();
            professionId = professionId?.Trim();
            specializationId = specializationId?.Trim();
            version = Math.Max(1, version);
            minimumAge = Math.Max(0, minimumAge);
        }

        public void DevelopmentConfigure(
            string id,
            string profession,
            string name,
            ProfessionEntryType type,
            ProfessionEntryFormality pathFormality,
            ProfessionSelfDeclarationPolicy selfDeclaration,
            string specialization = "",
            string[] authorities = null,
            RequirementSetDefinition requirements = null,
            int minAge = 0,
            string[] lifeStages = null,
            string[] skills = null,
            string[] knowledge = null,
            string[] capabilities = null,
            string[] traits = null,
            string[] statuses = null,
            string[] organizations = null,
            string[] accessKeys = null,
            string[] requiredActiveProfessions = null,
            string[] prohibitedActiveProfessions = null,
            string[] exclusiveProfessions = null,
            bool secret = false,
            bool disputed = false,
            bool restricted = false,
            bool illegal = false,
            bool requiresAuthority = false,
            bool immediateApproval = true,
            bool specializationNeedsParent = true,
            ProfessionReentryPolicy reentry = ProfessionReentryPolicy.NotApplicable,
            string accessPolicy = "")
        {
            entryPathId = id?.Trim();
            professionId = profession?.Trim();
            specializationId = specialization?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            debugName = displayName;
            entryType = type;
            formality = pathFormality;
            selfDeclarationPolicy = selfDeclaration;
            recognizingAuthorityIds = Clean(authorities);
            requirementSet = requirements;
            minimumAge = Math.Max(0, minAge);
            allowedLifeStageIds = Clean(lifeStages);
            requiredSkillIds = Clean(skills);
            requiredKnowledgeSubjectIds = Clean(knowledge);
            requiredCapabilityIds = Clean(capabilities);
            requiredTraitIds = Clean(traits);
            requiredStatusIds = Clean(statuses);
            requiredOrganizationIds = Clean(organizations);
            requiredAccessKeys = Clean(accessKeys);
            requiredActiveProfessionIds = Clean(requiredActiveProfessions);
            prohibitedActiveProfessionIds = Clean(prohibitedActiveProfessions);
            exclusiveProfessionIds = Clean(exclusiveProfessions);
            allowSecretEntry = secret;
            allowDisputedEntry = disputed;
            allowRestrictedEntry = restricted;
            allowIllegalEntry = illegal;
            requiresRecognizingAuthority = requiresAuthority;
            immediateApprovalAllowed = immediateApproval;
            specializationRequiresParentActive = specializationNeedsParent;
            reentryPolicy = reentry;
            defaultAccessPolicyId = accessPolicy ?? string.Empty;
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
                report.AddError($"Profession Entry Path '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("profession.entry.", StringComparison.Ordinal))
            {
                report.AddWarning($"Profession Entry Path '{Id}' should use the 'profession.entry.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(ProfessionEntryType), entryType)
                || !Enum.IsDefined(typeof(ProfessionEntryFormality), formality)
                || !Enum.IsDefined(typeof(ProfessionSelfDeclarationPolicy), selfDeclarationPolicy)
                || !Enum.IsDefined(typeof(ProfessionReentryPolicy), reentryPolicy))
            {
                report.AddError($"Profession Entry Path '{DisplayName}' has an invalid enum value.");
            }

            ProfessionDefinition profession = null;
            if (string.IsNullOrWhiteSpace(ProfessionId)
                || definitionsById == null
                || !definitionsById.TryGetValue(ProfessionId, out IGameDefinition professionDefinition)
                || (profession = professionDefinition as ProfessionDefinition) == null)
            {
                report.AddError($"Profession Entry Path '{DisplayName}' references missing Profession '{ProfessionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(SpecializationId))
            {
                if (definitionsById == null
                    || !definitionsById.TryGetValue(SpecializationId, out IGameDefinition specializationDefinition)
                    || specializationDefinition is not ProfessionSpecializationDefinition specialization)
                {
                    report.AddError($"Profession Entry Path '{DisplayName}' references missing specialization '{SpecializationId}'.");
                }
                else if (!string.Equals(specialization.ParentProfessionId, ProfessionId, StringComparison.Ordinal))
                {
                    report.AddError($"Profession Entry Path '{DisplayName}' specialization '{SpecializationId}' belongs to '{specialization.ParentProfessionId}', expected '{ProfessionId}'.");
                }
            }

            if (requirementSet != null)
            {
                ValidateReference<RequirementSetDefinition>(requirementSet.Id, "requirementSet", definitionsById, report);
            }

            if (!string.IsNullOrWhiteSpace(DefaultAccessPolicyId))
            {
                ValidateReference<InformationAccessPolicyDefinition>(DefaultAccessPolicyId, "defaultAccessPolicyId", definitionsById, report);
            }

            ValidateUnique(RecognizingAuthorityIds, "recognizingAuthorityIds", report);
            ValidateUnique(RequiredSkillIds, "requiredSkillIds", report);
            ValidateUnique(RequiredKnowledgeSubjectIds, "requiredKnowledgeSubjectIds", report);
            ValidateUnique(RequiredCapabilityIds, "requiredCapabilityIds", report);
            ValidateUnique(RequiredTraitIds, "requiredTraitIds", report);
            ValidateUnique(RequiredStatusIds, "requiredStatusIds", report);
            ValidateUnique(RequiredOrganizationIds, "requiredOrganizationIds", report);
            ValidateUnique(RequiredAccessKeys, "requiredAccessKeys", report);
            ValidateUnique(RequiredActiveProfessionIds, "requiredActiveProfessionIds", report);
            ValidateUnique(ProhibitedActiveProfessionIds, "prohibitedActiveProfessionIds", report);
            ValidateUnique(ExclusiveProfessionIds, "exclusiveProfessionIds", report);

            ValidateReferences<SkillDefinition>(RequiredSkillIds, "requiredSkillIds", definitionsById, report);
            ValidateReferences<KnowledgeFactDefinition>(RequiredKnowledgeSubjectIds, "requiredKnowledgeSubjectIds", definitionsById, report, allowMissing: true);
            ValidateReferences<CapabilityDefinition>(RequiredCapabilityIds, "requiredCapabilityIds", definitionsById, report);
            ValidateReferences<TraitDefinition>(RequiredTraitIds, "requiredTraitIds", definitionsById, report);
            ValidateReferences<ProfessionDefinition>(RequiredActiveProfessionIds, "requiredActiveProfessionIds", definitionsById, report);
            ValidateReferences<ProfessionDefinition>(ProhibitedActiveProfessionIds, "prohibitedActiveProfessionIds", definitionsById, report);
            ValidateReferences<ProfessionDefinition>(ExclusiveProfessionIds, "exclusiveProfessionIds", definitionsById, report);

            if ((Formality == ProfessionEntryFormality.Formal || RequiresRecognizingAuthority) && RecognizingAuthorityIds.Count == 0)
            {
                report.AddError($"Profession Entry Path '{DisplayName}' requires a recognizing authority but declares none.");
            }

            if (profession != null && RecognizingAuthorityIds.Count > 0 && profession.RecognizingAuthorityIds.Count > 0)
            {
                foreach (string authorityId in RecognizingAuthorityIds)
                {
                    if (!profession.RecognizingAuthorityIds.Contains(authorityId, StringComparer.Ordinal))
                    {
                        report.AddError($"Profession Entry Path '{DisplayName}' authority '{authorityId}' is not valid for Profession '{profession.Id}'.");
                    }
                }
            }

            if (SelfDeclarationPolicy != ProfessionSelfDeclarationPolicy.Disallowed && profession != null && !profession.SelfDeclarationAllowed)
            {
                report.AddError($"Profession Entry Path '{DisplayName}' allows self declaration but Profession '{profession.Id}' disallows it.");
            }

            if (Formality == ProfessionEntryFormality.Formal && profession != null && !profession.FormalRecognitionPossible)
            {
                report.AddError($"Profession Entry Path '{DisplayName}' is formal but Profession '{profession.Id}' cannot be formally recognized.");
            }
        }

        internal bool AllowsAuthority(string authorityId)
        {
            return !string.IsNullOrWhiteSpace(authorityId)
                && (RecognizingAuthorityIds.Count == 0 || RecognizingAuthorityIds.Contains(authorityId, StringComparer.Ordinal));
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

        private void ValidateUnique(IReadOnlyList<string> ids, string field, DefinitionValidationReport report)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    report.AddError($"Profession Entry Path '{DisplayName}' field '{field}' has duplicate or blank value '{id}'.");
                }
            }
        }

        private void ValidateReferences<TDefinition>(IReadOnlyList<string> ids, string field, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, bool allowMissing = false)
            where TDefinition : class, IGameDefinition
        {
            foreach (string id in ids ?? Array.Empty<string>())
            {
                ValidateReference<TDefinition>(id, field, definitionsById, report, allowMissing);
            }
        }

        private void ValidateReference<TDefinition>(string id, string field, IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report, bool allowMissing = false)
            where TDefinition : class, IGameDefinition
        {
            if (string.IsNullOrWhiteSpace(id) || definitionsById == null)
            {
                return;
            }

            if (!definitionsById.TryGetValue(id, out IGameDefinition definition))
            {
                if (!allowMissing)
                {
                    report.AddError($"Profession Entry Path '{DisplayName}' field '{field}' references missing definition '{id}'.");
                }

                return;
            }

            if (definition is not TDefinition)
            {
                report.AddError($"Profession Entry Path '{DisplayName}' field '{field}' references '{id}' as {typeof(TDefinition).Name}, but found {definition.GetType().Name}.");
            }
        }
    }
}
