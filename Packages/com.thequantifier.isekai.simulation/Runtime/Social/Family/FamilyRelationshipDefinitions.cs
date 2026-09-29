using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Relationships;

namespace UnityIsekaiGame.Social.Family
{
    [CreateAssetMenu(fileName = "RomanticEligibilityPolicyDefinition", menuName = "Unity Isekai Game/Social/Romantic Eligibility Policy Definition")]
    public sealed class RomanticEligibilityPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string policyId;
        [SerializeField] private string displayName;
        [SerializeField] private bool requireAdults = true;
        [SerializeField] private bool requireConsent = true;
        [SerializeField] private bool prohibitGuardianDependent = true;
        [SerializeField] private bool exclusivePartnerships = true;
        [SerializeField] private int maximumActivePartnerships = 1;
        [SerializeField] private KinshipClassification[] prohibitedKinshipClassifications = Array.Empty<KinshipClassification>();
        [SerializeField] private int minimumRomanticAttraction;
        [SerializeField] private int minimumAffection;
        [SerializeField] private string[] normDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public bool RequireAdults => requireAdults;
        public bool RequireConsent => requireConsent;
        public bool ProhibitGuardianDependent => prohibitGuardianDependent;
        public bool ExclusivePartnerships => exclusivePartnerships;
        public int MaximumActivePartnerships => maximumActivePartnerships;
        public IReadOnlyList<KinshipClassification> ProhibitedKinshipClassifications => prohibitedKinshipClassifications ?? Array.Empty<KinshipClassification>();
        public int MinimumRomanticAttraction => minimumRomanticAttraction;
        public int MinimumAffection => minimumAffection;
        public IReadOnlyList<string> NormDefinitionIds => normDefinitionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            policyId = policyId?.Trim();
            version = Math.Max(1, version);
            maximumActivePartnerships = Math.Max(0, maximumActivePartnerships);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            bool adultRequired,
            bool consentRequired,
            bool guardianDependentProhibited,
            bool exclusive,
            int maximumPartners,
            IEnumerable<KinshipClassification> prohibitedKinship,
            int attractionThreshold = 0,
            int affectionThreshold = 0,
            IEnumerable<string> normIds = null,
            IEnumerable<string> tagIds = null)
        {
            policyId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            requireAdults = adultRequired;
            requireConsent = consentRequired;
            prohibitGuardianDependent = guardianDependentProhibited;
            exclusivePartnerships = exclusive;
            maximumActivePartnerships = Math.Max(0, maximumPartners);
            prohibitedKinshipClassifications = (prohibitedKinship ?? Array.Empty<KinshipClassification>()).Distinct().OrderBy(value => value).ToArray();
            minimumRomanticAttraction = Math.Max(0, attractionThreshold);
            minimumAffection = Math.Max(0, affectionThreshold);
            normDefinitionIds = Clean(normIds);
            tags = Clean(tagIds);
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
                report.AddError($"Romantic Eligibility Policy '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("romance-policy.", StringComparison.Ordinal))
            {
                report.AddWarning($"Romantic Eligibility Policy '{Id}' should use the 'romance-policy.' namespace prefix.");
            }

            if (maximumActivePartnerships < 0)
            {
                report.AddError($"Romantic Eligibility Policy '{DisplayName}' has invalid maximum active partnerships.");
            }

            if (exclusivePartnerships && maximumActivePartnerships != 1)
            {
                report.AddError($"Romantic Eligibility Policy '{DisplayName}' is exclusive and must allow exactly one active partnership.");
            }

            if (minimumRomanticAttraction < 0 || minimumAffection < 0)
            {
                report.AddError($"Romantic Eligibility Policy '{DisplayName}' has invalid attitude thresholds.");
            }

            if (version < 1)
            {
                report.AddError($"Romantic Eligibility Policy '{DisplayName}' has invalid version.");
            }

            foreach (KinshipClassification classification in prohibitedKinshipClassifications ?? Array.Empty<KinshipClassification>())
            {
                if (!Enum.IsDefined(typeof(KinshipClassification), classification))
                {
                    report.AddError($"Romantic Eligibility Policy '{DisplayName}' has invalid prohibited kinship '{classification}'.");
                }
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

    [CreateAssetMenu(fileName = "HouseholdDefinition", menuName = "Unity Isekai Game/Social/Household Definition")]
    public sealed class HouseholdDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string householdDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private HouseholdRole[] allowedRoles = Array.Empty<HouseholdRole>();
        [SerializeField] private int minimumActiveMembers = 1;
        [SerializeField] private int maximumActiveMembers = 32;
        [SerializeField] private int minimumHeads = 0;
        [SerializeField] private int maximumHeads = 2;
        [SerializeField] private bool requireResidenceReference;
        [SerializeField] private bool requireAdultHead;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => householdDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public IReadOnlyList<HouseholdRole> AllowedRoles => allowedRoles ?? Array.Empty<HouseholdRole>();
        public int MinimumActiveMembers => minimumActiveMembers;
        public int MaximumActiveMembers => maximumActiveMembers;
        public int MinimumHeads => minimumHeads;
        public int MaximumHeads => maximumHeads;
        public bool RequireResidenceReference => requireResidenceReference;
        public bool RequireAdultHead => requireAdultHead;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            householdDefinitionId = householdDefinitionId?.Trim();
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            IEnumerable<HouseholdRole> roles,
            int minimumMembers,
            int maximumMembers,
            int minHeads,
            int maxHeads,
            bool residenceRequired,
            bool adultHeadRequired,
            string accessPolicyId = "",
            IEnumerable<string> tagIds = null)
        {
            householdDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            allowedRoles = (roles ?? Array.Empty<HouseholdRole>()).Distinct().OrderBy(role => role).ToArray();
            minimumActiveMembers = Math.Max(0, minimumMembers);
            maximumActiveMembers = Math.Max(0, maximumMembers);
            minimumHeads = Math.Max(0, minHeads);
            maximumHeads = Math.Max(0, maxHeads);
            requireResidenceReference = residenceRequired;
            requireAdultHead = adultHeadRequired;
            defaultAccessPolicyId = accessPolicyId ?? string.Empty;
            tags = Clean(tagIds);
            version = 1;
        }

        public bool AllowsRole(HouseholdRole role)
        {
            return AllowedRoles.Contains(role);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Household Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("household.", StringComparison.Ordinal))
            {
                report.AddWarning($"Household Definition '{Id}' should use the 'household.' namespace prefix.");
            }

            if (AllowedRoles.Count == 0)
            {
                report.AddError($"Household Definition '{DisplayName}' must allow at least one role.");
            }

            foreach (HouseholdRole role in AllowedRoles)
            {
                if (!Enum.IsDefined(typeof(HouseholdRole), role))
                {
                    report.AddError($"Household Definition '{DisplayName}' has invalid role '{role}'.");
                }
            }

            if (minimumActiveMembers < 0 || maximumActiveMembers < minimumActiveMembers)
            {
                report.AddError($"Household Definition '{DisplayName}' has invalid member limits.");
            }

            if (minimumHeads < 0 || maximumHeads < minimumHeads)
            {
                report.AddError($"Household Definition '{DisplayName}' has invalid head limits.");
            }

            if (minimumHeads > maximumActiveMembers)
            {
                report.AddError($"Household Definition '{DisplayName}' requires more heads than maximum members.");
            }

            if (version < 1)
            {
                report.AddError($"Household Definition '{DisplayName}' has invalid version.");
            }

            if (!string.IsNullOrWhiteSpace(defaultAccessPolicyId)
                && definitionsById != null
                && !definitionsById.ContainsKey(defaultAccessPolicyId))
            {
                report.AddError($"Household Definition '{DisplayName}' references missing access policy '{defaultAccessPolicyId}'.");
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
