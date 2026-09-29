using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [CreateAssetMenu(menuName = "Unity Isekai/Professions/Duty Definition")]
    public sealed class DutyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string id;
        [SerializeField] private string positionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private string debugName;
        [SerializeField] private DutyCategory category = DutyCategory.Custom;
        [SerializeField] private bool required = true;
        [SerializeField] private int priority = 100;
        [SerializeField] private string activityOrServiceCategoryId;
        [SerializeField] private string requiredProfessionId;
        [SerializeField] private string requiredAuthorityId;
        [SerializeField] private string requiredAccessPolicyId;
        [SerializeField] private string targetFoundationId;
        [SerializeField] private string expectedFrequencyFoundationId;
        [SerializeField] private bool delegationAllowed;
        [SerializeField] private bool supervisionRequired;
        [SerializeField] private bool completionEvidenceRequired = true;
        [SerializeField] private string failureNeglectPolicyFoundationId;
        [SerializeField] private bool secret;
        [SerializeField] private InformationVisibilityClassification visibility = InformationVisibilityClassification.Public;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private int version = 1;

        public string Id => id ?? string.Empty;
        public string PositionDefinitionId => positionDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string DebugName => debugName ?? string.Empty;
        public DutyCategory Category => category;
        public bool Required => required;
        public int Priority => Math.Max(0, priority);
        public string ActivityOrServiceCategoryId => activityOrServiceCategoryId ?? string.Empty;
        public string RequiredProfessionId => requiredProfessionId ?? string.Empty;
        public string RequiredAuthorityId => requiredAuthorityId ?? string.Empty;
        public string RequiredAccessPolicyId => requiredAccessPolicyId ?? string.Empty;
        public string TargetFoundationId => targetFoundationId ?? string.Empty;
        public string ExpectedFrequencyFoundationId => expectedFrequencyFoundationId ?? string.Empty;
        public bool DelegationAllowed => delegationAllowed;
        public bool SupervisionRequired => supervisionRequired;
        public bool CompletionEvidenceRequired => completionEvidenceRequired;
        public string FailureNeglectPolicyFoundationId => failureNeglectPolicyFoundationId ?? string.Empty;
        public bool Secret => secret;
        public InformationVisibilityClassification Visibility => visibility;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string stableId,
            string positionId,
            string name,
            DutyCategory dutyCategory,
            bool isRequired = true,
            int dutyPriority = 100,
            string professionId = "",
            string authorityId = "",
            bool allowDelegation = false,
            bool requireSupervision = false,
            bool requireEvidence = true,
            bool isSecret = false,
            string policyId = "",
            int definitionVersion = 1)
        {
            id = stableId ?? string.Empty;
            positionDefinitionId = positionId ?? string.Empty;
            displayName = name ?? stableId ?? string.Empty;
            debugName = displayName;
            category = dutyCategory;
            required = isRequired;
            priority = Math.Max(0, dutyPriority);
            requiredProfessionId = professionId ?? string.Empty;
            requiredAuthorityId = authorityId ?? string.Empty;
            delegationAllowed = allowDelegation;
            supervisionRequired = requireSupervision;
            completionEvidenceRequired = requireEvidence;
            secret = isSecret;
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
                report.AddError("Duty definition has no stable ID.");
            }
            else if (!Id.StartsWith("duty.", StringComparison.Ordinal))
            {
                report.AddWarning($"Duty definition '{DisplayName}' should use the 'duty.' namespace prefix.");
            }

            if (definitionsById == null || !definitionsById.TryGetValue(PositionDefinitionId, out IGameDefinition position) || position is not PositionDefinition)
            {
                report.AddError($"Duty definition '{DisplayName}' references missing Position definition '{PositionDefinitionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(RequiredProfessionId) && (definitionsById == null || !definitionsById.TryGetValue(RequiredProfessionId, out IGameDefinition profession) || profession is not ProfessionDefinition))
            {
                report.AddError($"Duty definition '{DisplayName}' references missing Profession '{RequiredProfessionId}'.");
            }

            if (Version <= 0)
            {
                report.AddError($"Duty definition '{DisplayName}' has invalid version.");
            }
        }
    }
}
