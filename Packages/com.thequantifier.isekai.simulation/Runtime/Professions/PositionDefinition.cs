using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Position Definition")]
    public sealed class PositionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField] private PositionCategory category = PositionCategory.Custom;
        [SerializeField] private string[] relatedProfessionIds = Array.Empty<string>();
        [SerializeField] private string[] relatedSpecializationIds = Array.Empty<string>();
        [SerializeField] private string requiredOrganizationTypeId;
        [SerializeField] private string[] requiredRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCredentialDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredTrainingProgramIds = Array.Empty<string>();
        [SerializeField] private ProfessionalExperienceRequirementData experienceRequirement = new ProfessionalExperienceRequirementData();
        [SerializeField] private string[] requiredSkillIds = Array.Empty<string>();
        [SerializeField] private string[] requiredKnowledgeFactIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCapabilityIds = Array.Empty<string>();
        [SerializeField] private string[] requiredOrganizationMembershipIds = Array.Empty<string>();
        [SerializeField] private string[] requiredRoleIds = Array.Empty<string>();
        [SerializeField] private string[] requiredTitleIds = Array.Empty<string>();
        [SerializeField] private string[] dutyDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] authorityGrantIds = Array.Empty<string>();
        [SerializeField] private string reportingRelationshipFoundationId;
        [SerializeField] private int supervisionCapacity;
        [SerializeField] private EmploymentClassification defaultClassification = EmploymentClassification.Permanent;
        [SerializeField] private int maximumSimultaneousHolders = 1;
        [SerializeField] private bool vacanciesAllowed = true;
        [SerializeField] private bool sharedPositionAllowed;
        [SerializeField] private bool exclusiveFullTime = true;
        [SerializeField] private bool secret;
        [SerializeField] private string compensationPolicyId;
        [SerializeField] private string paymentScheduleFoundationId;
        [SerializeField] private string wageOrSalaryFoundationId;
        [SerializeField] private string benefitsFoundationId;
        [SerializeField] private string employerCostCenterFoundationId;
        [SerializeField] private string contractTermsFoundationId;
        [SerializeField] private string commissionOrProfitShareFoundationId;
        [SerializeField] private InformationVisibilityClassification visibility = InformationVisibilityClassification.Public;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string DebugName => debugName ?? string.Empty;
        public PositionCategory Category => category;
        public IReadOnlyList<string> RelatedProfessionIds => CredentialDefinition.Clean(relatedProfessionIds);
        public IReadOnlyList<string> RelatedSpecializationIds => CredentialDefinition.Clean(relatedSpecializationIds);
        public string RequiredOrganizationTypeId => requiredOrganizationTypeId ?? string.Empty;
        public IReadOnlyList<string> RequiredRankDefinitionIds => CredentialDefinition.Clean(requiredRankDefinitionIds);
        public IReadOnlyList<string> RequiredCredentialDefinitionIds => CredentialDefinition.Clean(requiredCredentialDefinitionIds);
        public IReadOnlyList<string> RequiredTrainingProgramIds => CredentialDefinition.Clean(requiredTrainingProgramIds);
        public ProfessionalExperienceRequirementData ExperienceRequirement => ProfessionalRankDefinition.CloneExperience(experienceRequirement);
        public IReadOnlyList<string> RequiredSkillIds => CredentialDefinition.Clean(requiredSkillIds);
        public IReadOnlyList<string> RequiredKnowledgeFactIds => CredentialDefinition.Clean(requiredKnowledgeFactIds);
        public IReadOnlyList<string> RequiredCapabilityIds => CredentialDefinition.Clean(requiredCapabilityIds);
        public IReadOnlyList<string> RequiredOrganizationMembershipIds => CredentialDefinition.Clean(requiredOrganizationMembershipIds);
        public IReadOnlyList<string> RequiredRoleIds => CredentialDefinition.Clean(requiredRoleIds);
        public IReadOnlyList<string> RequiredTitleIds => CredentialDefinition.Clean(requiredTitleIds);
        public IReadOnlyList<string> DutyDefinitionIds => CredentialDefinition.Clean(dutyDefinitionIds);
        public IReadOnlyList<string> AuthorityGrantIds => CredentialDefinition.Clean(authorityGrantIds);
        public string ReportingRelationshipFoundationId => reportingRelationshipFoundationId ?? string.Empty;
        public int SupervisionCapacity => Math.Max(0, supervisionCapacity);
        public EmploymentClassification DefaultClassification => defaultClassification;
        public int MaximumSimultaneousHolders => Math.Max(1, maximumSimultaneousHolders);
        public bool VacanciesAllowed => vacanciesAllowed;
        public bool SharedPositionAllowed => sharedPositionAllowed;
        public bool ExclusiveFullTime => exclusiveFullTime;
        public bool Secret => secret;
        public string CompensationPolicyId => compensationPolicyId ?? string.Empty;
        public string PaymentScheduleFoundationId => paymentScheduleFoundationId ?? string.Empty;
        public string WageOrSalaryFoundationId => wageOrSalaryFoundationId ?? string.Empty;
        public string BenefitsFoundationId => benefitsFoundationId ?? string.Empty;
        public string EmployerCostCenterFoundationId => employerCostCenterFoundationId ?? string.Empty;
        public string ContractTermsFoundationId => contractTermsFoundationId ?? string.Empty;
        public string CommissionOrProfitShareFoundationId => commissionOrProfitShareFoundationId ?? string.Empty;
        public InformationVisibilityClassification Visibility => visibility;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string name,
            PositionCategory positionCategory,
            IEnumerable<string> professions = null,
            IEnumerable<string> specializations = null,
            string organizationTypeId = "",
            IEnumerable<string> ranks = null,
            IEnumerable<string> credentials = null,
            IEnumerable<string> trainingPrograms = null,
            ProfessionalExperienceRequirementData experience = null,
            IEnumerable<string> duties = null,
            IEnumerable<string> authorities = null,
            EmploymentClassification classification = EmploymentClassification.Permanent,
            int maxHolders = 1,
            bool allowVacancy = true,
            bool allowShared = false,
            bool exclusive = true,
            bool isSecret = false,
            string policyId = "",
            int definitionVersion = 1,
            IEnumerable<string> skills = null,
            IEnumerable<string> knowledge = null,
            IEnumerable<string> capabilities = null,
            IEnumerable<string> organizationMemberships = null,
            IEnumerable<string> roles = null,
            IEnumerable<string> titles = null,
            string reportingFoundation = "",
            int supervisorCapacity = 0,
            string compensationPolicy = "",
            string paymentSchedule = "",
            string wageOrSalary = "",
            string benefits = "",
            string costCenter = "",
            string contractTerms = "",
            string commissionOrProfitShare = "")
        {
            id = stableId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            debugName = displayName;
            category = positionCategory;
            relatedProfessionIds = CredentialDefinition.Clean(professions);
            relatedSpecializationIds = CredentialDefinition.Clean(specializations);
            requiredOrganizationTypeId = organizationTypeId ?? string.Empty;
            requiredRankDefinitionIds = CredentialDefinition.Clean(ranks);
            requiredCredentialDefinitionIds = CredentialDefinition.Clean(credentials);
            requiredTrainingProgramIds = CredentialDefinition.Clean(trainingPrograms);
            experienceRequirement = experience ?? new ProfessionalExperienceRequirementData();
            requiredSkillIds = CredentialDefinition.Clean(skills);
            requiredKnowledgeFactIds = CredentialDefinition.Clean(knowledge);
            requiredCapabilityIds = CredentialDefinition.Clean(capabilities);
            requiredOrganizationMembershipIds = CredentialDefinition.Clean(organizationMemberships);
            requiredRoleIds = CredentialDefinition.Clean(roles);
            requiredTitleIds = CredentialDefinition.Clean(titles);
            dutyDefinitionIds = CredentialDefinition.Clean(duties);
            authorityGrantIds = CredentialDefinition.Clean(authorities);
            reportingRelationshipFoundationId = reportingFoundation ?? string.Empty;
            supervisionCapacity = Math.Max(0, supervisorCapacity);
            defaultClassification = classification;
            maximumSimultaneousHolders = Math.Max(1, maxHolders);
            vacanciesAllowed = allowVacancy;
            sharedPositionAllowed = allowShared || maxHolders > 1;
            exclusiveFullTime = exclusive;
            secret = isSecret;
            compensationPolicyId = compensationPolicy ?? string.Empty;
            paymentScheduleFoundationId = paymentSchedule ?? string.Empty;
            wageOrSalaryFoundationId = wageOrSalary ?? string.Empty;
            benefitsFoundationId = benefits ?? string.Empty;
            employerCostCenterFoundationId = costCenter ?? string.Empty;
            contractTermsFoundationId = contractTerms ?? string.Empty;
            commissionOrProfitShareFoundationId = commissionOrProfitShare ?? string.Empty;
            accessPolicyId = policyId ?? string.Empty;
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
                report.AddError("Position definition has no stable ID.");
            }
            else if (!Id.StartsWith("position.", StringComparison.Ordinal))
            {
                report.AddWarning($"Position definition '{DisplayName}' should use the 'position.' namespace prefix.");
            }

            foreach (string professionId in RelatedProfessionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(professionId, out IGameDefinition profession) || profession is not ProfessionDefinition)
                {
                    report.AddError($"Position definition '{DisplayName}' references missing Profession '{professionId}'.");
                }
            }

            foreach (string specializationId in RelatedSpecializationIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(specializationId, out IGameDefinition specialization) || specialization is not ProfessionSpecializationDefinition)
                {
                    report.AddError($"Position definition '{DisplayName}' references missing Profession Specialization '{specializationId}'.");
                }
            }

            foreach (string rankId in RequiredRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(rankId, out IGameDefinition rank) || rank is not ProfessionalRankDefinition)
                {
                    report.AddError($"Position definition '{DisplayName}' references missing Rank '{rankId}'.");
                }
            }

            foreach (string credentialId in RequiredCredentialDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(credentialId, out IGameDefinition credential) || credential is not CredentialDefinition)
                {
                    report.AddError($"Position definition '{DisplayName}' references missing Credential '{credentialId}'.");
                }
            }

            foreach (string trainingId in RequiredTrainingProgramIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(trainingId, out IGameDefinition training) || training is not TrainingProgramDefinition)
                {
                    report.AddError($"Position definition '{DisplayName}' references missing Training Program '{trainingId}'.");
                }
            }

            foreach (string dutyId in DutyDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(dutyId, out IGameDefinition duty) || duty is not DutyDefinition)
                {
                    report.AddError($"Position definition '{DisplayName}' references missing Duty '{dutyId}'.");
                }
            }

            foreach (string authorityId in AuthorityGrantIds)
            {
                if (!authorityId.StartsWith("authority.", StringComparison.Ordinal) && !authorityId.StartsWith("permission.", StringComparison.Ordinal))
                {
                    report.AddError($"Position definition '{DisplayName}' has invalid authority grant '{authorityId}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(RequiredOrganizationTypeId) && !RequiredOrganizationTypeId.StartsWith("organization-type.", StringComparison.Ordinal))
            {
                report.AddError($"Position definition '{DisplayName}' has invalid organization type '{RequiredOrganizationTypeId}'.");
            }

            if (MaximumSimultaneousHolders <= 0)
            {
                report.AddError($"Position definition '{DisplayName}' has invalid holder capacity.");
            }

            if (!SharedPositionAllowed && MaximumSimultaneousHolders > 1)
            {
                report.AddError($"Position definition '{DisplayName}' allows multiple holders by capacity but does not allow sharing.");
            }

            if (Version <= 0)
            {
                report.AddError($"Position definition '{DisplayName}' has invalid version.");
            }
        }
    }
}
