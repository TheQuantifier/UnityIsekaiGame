using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Credential Definition")]
    public sealed class CredentialDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField] private string description;
        [SerializeField] private CredentialCategory category = CredentialCategory.Custom;
        [SerializeField] private string[] relatedProfessionIds = Array.Empty<string>();
        [SerializeField] private string[] relatedSpecializationIds = Array.Empty<string>();
        [SerializeField] private string[] authorizedIssuerIds = Array.Empty<string>();
        [SerializeField] private CredentialIssuerAuthorityKind[] issuerAuthorityKinds = Array.Empty<CredentialIssuerAuthorityKind>();
        [SerializeField] private bool requireProfessionRelationship = true;
        [SerializeField] private bool requireFormalRecognition;
        [SerializeField] private string[] requiredTrainingProgramIds = Array.Empty<string>();
        [SerializeField] private ProfessionalExperienceRequirementData experienceRequirement = new ProfessionalExperienceRequirementData();
        [SerializeField] private string[] requiredExaminationDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredRecommendationIds = Array.Empty<string>();
        [SerializeField] private string[] grantedPermissionIds = Array.Empty<string>();
        [SerializeField] private string[] relatedTitleOrRankEligibilityIds = Array.Empty<string>();
        [SerializeField] private double issueDurationHours;
        [SerializeField] private CredentialExpirationPolicy expirationPolicy = CredentialExpirationPolicy.NeverExpires;
        [SerializeField] private CredentialRenewalPolicy renewalPolicy = CredentialRenewalPolicy.NotRenewable;
        [SerializeField] private CredentialLifecyclePolicy suspensionPolicy = CredentialLifecyclePolicy.AllowedByIssuer;
        [SerializeField] private CredentialLifecyclePolicy revocationPolicy = CredentialLifecyclePolicy.RequiresAuthorityAndReason;
        [SerializeField] private CredentialTransferability transferability = CredentialTransferability.NonTransferable;
        [SerializeField] private InformationVisibilityClassification visibility = InformationVisibilityClassification.Public;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private bool allowMultipleActive;
        [SerializeField] private bool requiresApplication = true;
        [SerializeField] private bool requiresUniqueRegistrationNumber = true;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string DebugName => debugName ?? string.Empty;
        public string Description => description ?? string.Empty;
        public CredentialCategory Category => category;
        public IReadOnlyList<string> RelatedProfessionIds => Clean(relatedProfessionIds);
        public IReadOnlyList<string> RelatedSpecializationIds => Clean(relatedSpecializationIds);
        public IReadOnlyList<string> AuthorizedIssuerIds => Clean(authorizedIssuerIds);
        public IReadOnlyList<CredentialIssuerAuthorityKind> IssuerAuthorityKinds => (issuerAuthorityKinds ?? Array.Empty<CredentialIssuerAuthorityKind>()).Distinct().OrderBy(value => value).ToArray();
        public bool RequireProfessionRelationship => requireProfessionRelationship;
        public bool RequireFormalRecognition => requireFormalRecognition;
        public IReadOnlyList<string> RequiredTrainingProgramIds => Clean(requiredTrainingProgramIds);
        public ProfessionalExperienceRequirementData ExperienceRequirement => experienceRequirement == null ? new ProfessionalExperienceRequirementData() : new ProfessionalExperienceRequirementData
        {
            professionId = experienceRequirement.professionId ?? string.Empty,
            specializationId = experienceRequirement.specializationId ?? string.Empty,
            requiredCategory = experienceRequirement.requiredCategory,
            minimumValidatedActivities = Math.Max(0, experienceRequirement.minimumValidatedActivities),
            minimumIndependentActivities = Math.Max(0, experienceRequirement.minimumIndependentActivities),
            minimumSupervisedActivities = Math.Max(0, experienceRequirement.minimumSupervisedActivities),
            minimumDifficulty = experienceRequirement.minimumDifficulty,
            minimumQuality = Math.Max(0, experienceRequirement.minimumQuality),
            requireRecentActivity = experienceRequirement.requireRecentActivity
        };
        public IReadOnlyList<string> RequiredExaminationDefinitionIds => Clean(requiredExaminationDefinitionIds);
        public IReadOnlyList<string> RequiredRecommendationIds => Clean(requiredRecommendationIds);
        public IReadOnlyList<string> GrantedPermissionIds => Clean(grantedPermissionIds);
        public IReadOnlyList<string> RelatedTitleOrRankEligibilityIds => Clean(relatedTitleOrRankEligibilityIds);
        public double IssueDurationHours => Math.Max(0d, issueDurationHours);
        public CredentialExpirationPolicy ExpirationPolicy => expirationPolicy;
        public CredentialRenewalPolicy RenewalPolicy => renewalPolicy;
        public CredentialLifecyclePolicy SuspensionPolicy => suspensionPolicy;
        public CredentialLifecyclePolicy RevocationPolicy => revocationPolicy;
        public CredentialTransferability Transferability => transferability;
        public InformationVisibilityClassification Visibility => visibility;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public bool AllowMultipleActive => allowMultipleActive;
        public bool RequiresApplication => requiresApplication;
        public bool RequiresUniqueRegistrationNumber => requiresUniqueRegistrationNumber;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string name,
            CredentialCategory credentialCategory,
            IEnumerable<string> professionIds,
            IEnumerable<string> issuerIds,
            IEnumerable<CredentialIssuerAuthorityKind> issuerKinds,
            IEnumerable<string> trainingProgramIds = null,
            ProfessionalExperienceRequirementData experience = null,
            IEnumerable<string> examinationDefinitionIds = null,
            IEnumerable<string> permissions = null,
            IEnumerable<string> specializationIds = null,
            bool formalRecognition = false,
            double durationHours = 0d,
            CredentialExpirationPolicy expiration = CredentialExpirationPolicy.NeverExpires,
            CredentialRenewalPolicy renewal = CredentialRenewalPolicy.NotRenewable,
            CredentialLifecyclePolicy suspension = CredentialLifecyclePolicy.AllowedByIssuer,
            CredentialLifecyclePolicy revocation = CredentialLifecyclePolicy.RequiresAuthorityAndReason,
            InformationVisibilityClassification classification = InformationVisibilityClassification.Public,
            string policyId = "",
            bool allowMultiple = false,
            bool applicationRequired = true,
            bool uniqueRegistration = true,
            int definitionVersion = 1)
        {
            id = stableId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            debugName = displayName;
            category = credentialCategory;
            relatedProfessionIds = Clean(professionIds);
            relatedSpecializationIds = Clean(specializationIds);
            authorizedIssuerIds = Clean(issuerIds);
            issuerAuthorityKinds = (issuerKinds ?? Array.Empty<CredentialIssuerAuthorityKind>()).Distinct().OrderBy(value => value).ToArray();
            requiredTrainingProgramIds = Clean(trainingProgramIds);
            experienceRequirement = experience ?? new ProfessionalExperienceRequirementData();
            requiredExaminationDefinitionIds = Clean(examinationDefinitionIds);
            grantedPermissionIds = Clean(permissions);
            requireFormalRecognition = formalRecognition;
            issueDurationHours = Math.Max(0d, durationHours);
            expirationPolicy = expiration;
            renewalPolicy = renewal;
            suspensionPolicy = suspension;
            revocationPolicy = revocation;
            visibility = classification;
            accessPolicyId = policyId ?? string.Empty;
            allowMultipleActive = allowMultiple;
            requiresApplication = applicationRequired;
            requiresUniqueRegistrationNumber = uniqueRegistration;
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
                report.AddError("Credential definition has no stable ID.");
            }
            else if (!Id.StartsWith("credential.", StringComparison.Ordinal))
            {
                report.AddWarning($"Credential definition '{DisplayName}' should use the 'credential.' namespace prefix.");
            }

            foreach (string professionId in RelatedProfessionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(professionId, out IGameDefinition definition) || definition is not ProfessionDefinition)
                {
                    report.AddError($"Credential definition '{DisplayName}' references missing Profession '{professionId}'.");
                }
            }

            foreach (string specializationId in RelatedSpecializationIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(specializationId, out IGameDefinition definition) || definition is not ProfessionSpecializationDefinition)
                {
                    report.AddError($"Credential definition '{DisplayName}' references missing Profession Specialization '{specializationId}'.");
                }
            }

            foreach (string programId in RequiredTrainingProgramIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(programId, out IGameDefinition definition) || definition is not TrainingProgramDefinition)
                {
                    report.AddError($"Credential definition '{DisplayName}' references missing Training Program '{programId}'.");
                }
            }

            foreach (string examinationId in RequiredExaminationDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(examinationId, out IGameDefinition definition) || definition is not CredentialExaminationDefinition)
                {
                    report.AddError($"Credential definition '{DisplayName}' references missing Examination Definition '{examinationId}'.");
                }
            }

            if (AuthorizedIssuerIds.Count == 0 && IssuerAuthorityKinds.Count == 0)
            {
                report.AddError($"Credential definition '{DisplayName}' must declare an authorized issuer.");
            }

            if (ExpirationPolicy == CredentialExpirationPolicy.FixedDuration && IssueDurationHours <= 0d)
            {
                report.AddError($"Credential definition '{DisplayName}' uses fixed duration expiration without a positive duration.");
            }

            if (Version <= 0)
            {
                report.AddError($"Credential definition '{DisplayName}' has invalid version.");
            }
        }

        internal static string[] Clean(IEnumerable<string> values)
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
