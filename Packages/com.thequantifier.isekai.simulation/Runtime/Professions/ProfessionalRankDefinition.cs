using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Professional Rank Definition")]
    public sealed class ProfessionalRankDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField] private string professionId;
        [SerializeField] private string specializationId;
        [SerializeField] private int rankOrder;
        [SerializeField] private ProfessionalRankCategory category = ProfessionalRankCategory.Custom;
        [SerializeField] private string[] priorRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private bool allowRankSkipping;
        [SerializeField] private string[] requiredCredentialDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredTrainingProgramIds = Array.Empty<string>();
        [SerializeField] private ProfessionalExperienceRequirementData experienceRequirement = new ProfessionalExperienceRequirementData();
        [SerializeField] private string[] requiredExaminationDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredAuthorityIds = Array.Empty<string>();
        [SerializeField] private bool selfClaimAllowed;
        [SerializeField] private ProfessionalRankTrackKind trackKind = ProfessionalRankTrackKind.Formal;
        [SerializeField] private string[] grantedPermissionFoundationIds = Array.Empty<string>();
        [SerializeField] private string[] titleEligibilityFoundationIds = Array.Empty<string>();
        [SerializeField] private bool teachingEligibility;
        [SerializeField] private bool supervisionEligibility;
        [SerializeField] private int maximumApprenticeCapacity;
        [SerializeField] private bool suspensionAllowed = true;
        [SerializeField] private bool revocationAllowed = true;
        [SerializeField] private bool secret;
        [SerializeField] private InformationVisibilityClassification visibility = InformationVisibilityClassification.Public;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string DebugName => debugName ?? string.Empty;
        public string ProfessionId => professionId ?? string.Empty;
        public string SpecializationId => specializationId ?? string.Empty;
        public int RankOrder => rankOrder;
        public ProfessionalRankCategory Category => category;
        public IReadOnlyList<string> PriorRankDefinitionIds => CredentialDefinition.Clean(priorRankDefinitionIds);
        public bool AllowRankSkipping => allowRankSkipping;
        public IReadOnlyList<string> RequiredCredentialDefinitionIds => CredentialDefinition.Clean(requiredCredentialDefinitionIds);
        public IReadOnlyList<string> RequiredTrainingProgramIds => CredentialDefinition.Clean(requiredTrainingProgramIds);
        public ProfessionalExperienceRequirementData ExperienceRequirement => CloneExperience(experienceRequirement);
        public IReadOnlyList<string> RequiredExaminationDefinitionIds => CredentialDefinition.Clean(requiredExaminationDefinitionIds);
        public IReadOnlyList<string> RequiredAuthorityIds => CredentialDefinition.Clean(requiredAuthorityIds);
        public bool SelfClaimAllowed => selfClaimAllowed;
        public ProfessionalRankTrackKind TrackKind => trackKind;
        public IReadOnlyList<string> GrantedPermissionFoundationIds => CredentialDefinition.Clean(grantedPermissionFoundationIds);
        public IReadOnlyList<string> TitleEligibilityFoundationIds => CredentialDefinition.Clean(titleEligibilityFoundationIds);
        public bool TeachingEligibility => teachingEligibility;
        public bool SupervisionEligibility => supervisionEligibility;
        public int MaximumApprenticeCapacity => Math.Max(0, maximumApprenticeCapacity);
        public bool SuspensionAllowed => suspensionAllowed;
        public bool RevocationAllowed => revocationAllowed;
        public bool Secret => secret;
        public InformationVisibilityClassification Visibility => visibility;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string name,
            string profession,
            int order,
            ProfessionalRankCategory rankCategory,
            string specialization = "",
            IEnumerable<string> priorRanks = null,
            IEnumerable<string> requiredCredentials = null,
            IEnumerable<string> requiredTraining = null,
            ProfessionalExperienceRequirementData experience = null,
            IEnumerable<string> requiredExaminations = null,
            IEnumerable<string> requiredAuthorities = null,
            bool allowSelfClaim = false,
            ProfessionalRankTrackKind track = ProfessionalRankTrackKind.Formal,
            IEnumerable<string> permissionFoundations = null,
            IEnumerable<string> titleEligibility = null,
            bool canTeach = false,
            bool canSupervise = false,
            int apprenticeCapacity = 0,
            bool allowSkipping = false,
            bool allowSuspension = true,
            bool allowRevocation = true,
            bool isSecret = false,
            InformationVisibilityClassification classification = InformationVisibilityClassification.Public,
            string policyId = "",
            int definitionVersion = 1)
        {
            id = stableId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            debugName = displayName;
            professionId = profession ?? string.Empty;
            specializationId = specialization ?? string.Empty;
            rankOrder = Math.Max(0, order);
            category = rankCategory;
            priorRankDefinitionIds = CredentialDefinition.Clean(priorRanks);
            requiredCredentialDefinitionIds = CredentialDefinition.Clean(requiredCredentials);
            requiredTrainingProgramIds = CredentialDefinition.Clean(requiredTraining);
            experienceRequirement = experience ?? new ProfessionalExperienceRequirementData();
            requiredExaminationDefinitionIds = CredentialDefinition.Clean(requiredExaminations);
            requiredAuthorityIds = CredentialDefinition.Clean(requiredAuthorities);
            selfClaimAllowed = allowSelfClaim;
            trackKind = track;
            grantedPermissionFoundationIds = CredentialDefinition.Clean(permissionFoundations);
            titleEligibilityFoundationIds = CredentialDefinition.Clean(titleEligibility);
            teachingEligibility = canTeach;
            supervisionEligibility = canSupervise;
            maximumApprenticeCapacity = Math.Max(0, apprenticeCapacity);
            allowRankSkipping = allowSkipping;
            suspensionAllowed = allowSuspension;
            revocationAllowed = allowRevocation;
            secret = isSecret;
            visibility = classification;
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
                report.AddError("Professional Rank definition has no stable ID.");
            }
            else if (!Id.StartsWith("profession.rank.", StringComparison.Ordinal))
            {
                report.AddWarning($"Professional Rank definition '{DisplayName}' should use the 'profession.rank.' namespace prefix.");
            }

            if (definitionsById == null || !definitionsById.TryGetValue(ProfessionId, out IGameDefinition professionDefinition) || professionDefinition is not ProfessionDefinition)
            {
                report.AddError($"Professional Rank definition '{DisplayName}' references missing Profession '{ProfessionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(SpecializationId) && (definitionsById == null || !definitionsById.TryGetValue(SpecializationId, out IGameDefinition specializationDefinition) || specializationDefinition is not ProfessionSpecializationDefinition))
            {
                report.AddError($"Professional Rank definition '{DisplayName}' references missing Profession Specialization '{SpecializationId}'.");
            }

            foreach (string priorRankId in PriorRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(priorRankId, out IGameDefinition rankDefinition) || rankDefinition is not ProfessionalRankDefinition)
                {
                    report.AddError($"Professional Rank definition '{DisplayName}' references missing prior Rank '{priorRankId}'.");
                }
            }

            foreach (string credentialId in RequiredCredentialDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(credentialId, out IGameDefinition credentialDefinition) || credentialDefinition is not CredentialDefinition)
                {
                    report.AddError($"Professional Rank definition '{DisplayName}' references missing Credential '{credentialId}'.");
                }
            }

            foreach (string trainingId in RequiredTrainingProgramIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(trainingId, out IGameDefinition trainingDefinition) || trainingDefinition is not TrainingProgramDefinition)
                {
                    report.AddError($"Professional Rank definition '{DisplayName}' references missing Training Program '{trainingId}'.");
                }
            }

            foreach (string examinationId in RequiredExaminationDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(examinationId, out IGameDefinition examinationDefinition) || examinationDefinition is not CredentialExaminationDefinition)
                {
                    report.AddError($"Professional Rank definition '{DisplayName}' references missing Examination '{examinationId}'.");
                }
            }

            if (RankOrder < 0)
            {
                report.AddError($"Professional Rank definition '{DisplayName}' has invalid rank order.");
            }

            if (TrackKind == ProfessionalRankTrackKind.Formal && RequiredAuthorityIds.Count == 0 && !SelfClaimAllowed)
            {
                report.AddError($"Professional Rank definition '{DisplayName}' is formal but has no recognizing authority.");
            }

            if (Version <= 0)
            {
                report.AddError($"Professional Rank definition '{DisplayName}' has invalid version.");
            }
        }

        internal static ProfessionalExperienceRequirementData CloneExperience(ProfessionalExperienceRequirementData source)
        {
            source ??= new ProfessionalExperienceRequirementData();
            return new ProfessionalExperienceRequirementData
            {
                professionId = source.professionId ?? string.Empty,
                specializationId = source.specializationId ?? string.Empty,
                requiredCategory = source.requiredCategory,
                minimumValidatedActivities = Math.Max(0, source.minimumValidatedActivities),
                minimumIndependentActivities = Math.Max(0, source.minimumIndependentActivities),
                minimumSupervisedActivities = Math.Max(0, source.minimumSupervisedActivities),
                minimumDifficulty = source.minimumDifficulty,
                minimumQuality = Math.Max(0, source.minimumQuality),
                requireRecentActivity = source.requireRecentActivity
            };
        }
    }
}
