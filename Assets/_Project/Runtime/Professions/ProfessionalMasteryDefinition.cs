using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Professional Mastery Definition")]
    public sealed class ProfessionalMasteryDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string professionId;
        [SerializeField] private string specializationId;
        [SerializeField] private string requiredRankDefinitionId;
        [SerializeField] private ProfessionalExperienceRequirementData experienceRequirement = new ProfessionalExperienceRequirementData();
        [SerializeField] private int requiredBreadthCount;
        [SerializeField] private int requiredDepthQuality;
        [SerializeField] private int requiredIndependentWorkCount;
        [SerializeField] private int requiredTeachingOrLeadershipCount;
        [SerializeField] private string[] requiredCredentialDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredExaminationDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredAchievementIds = Array.Empty<string>();
        [SerializeField] private string[] requiredAuthorityIds = Array.Empty<string>();
        [SerializeField] private ProfessionalRankTrackKind recognitionTrack = ProfessionalRankTrackKind.Formal;
        [SerializeField] private InformationVisibilityClassification visibility = InformationVisibilityClassification.Public;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string ProfessionId => professionId ?? string.Empty;
        public string SpecializationId => specializationId ?? string.Empty;
        public string RequiredRankDefinitionId => requiredRankDefinitionId ?? string.Empty;
        public ProfessionalExperienceRequirementData ExperienceRequirement => ProfessionalRankDefinition.CloneExperience(experienceRequirement);
        public int RequiredBreadthCount => Math.Max(0, requiredBreadthCount);
        public int RequiredDepthQuality => Math.Max(0, requiredDepthQuality);
        public int RequiredIndependentWorkCount => Math.Max(0, requiredIndependentWorkCount);
        public int RequiredTeachingOrLeadershipCount => Math.Max(0, requiredTeachingOrLeadershipCount);
        public IReadOnlyList<string> RequiredCredentialDefinitionIds => CredentialDefinition.Clean(requiredCredentialDefinitionIds);
        public IReadOnlyList<string> RequiredExaminationDefinitionIds => CredentialDefinition.Clean(requiredExaminationDefinitionIds);
        public IReadOnlyList<string> RequiredAchievementIds => CredentialDefinition.Clean(requiredAchievementIds);
        public IReadOnlyList<string> RequiredAuthorityIds => CredentialDefinition.Clean(requiredAuthorityIds);
        public ProfessionalRankTrackKind RecognitionTrack => recognitionTrack;
        public InformationVisibilityClassification Visibility => visibility;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string name,
            string profession,
            string requiredRank,
            string specialization = "",
            ProfessionalExperienceRequirementData experience = null,
            int breadth = 0,
            int depthQuality = 0,
            int independentWork = 0,
            int teachingOrLeadership = 0,
            IEnumerable<string> credentials = null,
            IEnumerable<string> examinations = null,
            IEnumerable<string> achievements = null,
            IEnumerable<string> authorities = null,
            ProfessionalRankTrackKind track = ProfessionalRankTrackKind.Formal,
            InformationVisibilityClassification classification = InformationVisibilityClassification.Public,
            string policyId = "",
            int definitionVersion = 1)
        {
            id = stableId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            professionId = profession ?? string.Empty;
            specializationId = specialization ?? string.Empty;
            requiredRankDefinitionId = requiredRank ?? string.Empty;
            experienceRequirement = experience ?? new ProfessionalExperienceRequirementData();
            requiredBreadthCount = Math.Max(0, breadth);
            requiredDepthQuality = Math.Max(0, depthQuality);
            requiredIndependentWorkCount = Math.Max(0, independentWork);
            requiredTeachingOrLeadershipCount = Math.Max(0, teachingOrLeadership);
            requiredCredentialDefinitionIds = CredentialDefinition.Clean(credentials);
            requiredExaminationDefinitionIds = CredentialDefinition.Clean(examinations);
            requiredAchievementIds = CredentialDefinition.Clean(achievements);
            requiredAuthorityIds = CredentialDefinition.Clean(authorities);
            recognitionTrack = track;
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
                report.AddError("Professional Mastery definition has no stable ID.");
            }
            else if (!Id.StartsWith("profession.mastery.", StringComparison.Ordinal))
            {
                report.AddWarning($"Professional Mastery definition '{DisplayName}' should use the 'profession.mastery.' namespace prefix.");
            }

            if (definitionsById == null || !definitionsById.TryGetValue(ProfessionId, out IGameDefinition professionDefinition) || professionDefinition is not ProfessionDefinition)
            {
                report.AddError($"Professional Mastery definition '{DisplayName}' references missing Profession '{ProfessionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(SpecializationId) && (definitionsById == null || !definitionsById.TryGetValue(SpecializationId, out IGameDefinition specializationDefinition) || specializationDefinition is not ProfessionSpecializationDefinition))
            {
                report.AddError($"Professional Mastery definition '{DisplayName}' references missing Profession Specialization '{SpecializationId}'.");
            }

            if (definitionsById == null || !definitionsById.TryGetValue(RequiredRankDefinitionId, out IGameDefinition rankDefinition) || rankDefinition is not ProfessionalRankDefinition)
            {
                report.AddError($"Professional Mastery definition '{DisplayName}' references missing required Rank '{RequiredRankDefinitionId}'.");
            }

            foreach (string credentialId in RequiredCredentialDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(credentialId, out IGameDefinition credentialDefinition) || credentialDefinition is not CredentialDefinition)
                {
                    report.AddError($"Professional Mastery definition '{DisplayName}' references missing Credential '{credentialId}'.");
                }
            }

            foreach (string examinationId in RequiredExaminationDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(examinationId, out IGameDefinition examinationDefinition) || examinationDefinition is not CredentialExaminationDefinition)
                {
                    report.AddError($"Professional Mastery definition '{DisplayName}' references missing Examination '{examinationId}'.");
                }
            }

            if (RequiredAuthorityIds.Count == 0 && RecognitionTrack == ProfessionalRankTrackKind.Formal)
            {
                report.AddError($"Professional Mastery definition '{DisplayName}' requires formal recognition but has no authority.");
            }

            if (RequiredBreadthCount <= 0 && RequiredDepthQuality <= 0 && RequiredIndependentWorkCount <= 0 && RequiredTeachingOrLeadershipCount <= 0 && RequiredAchievementIds.Count == 0)
            {
                report.AddError($"Professional Mastery definition '{DisplayName}' must require breadth, depth, independent work, teaching, leadership, or achievement evidence.");
            }

            if (Version <= 0)
            {
                report.AddError($"Professional Mastery definition '{DisplayName}' has invalid version.");
            }
        }
    }
}
