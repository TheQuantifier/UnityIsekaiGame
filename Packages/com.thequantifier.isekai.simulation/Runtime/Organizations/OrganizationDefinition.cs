using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Organizations
{
    [CreateAssetMenu(fileName = "OrganizationDefinition", menuName = "Unity Isekai Game/Organizations/Organization Definition")]
    public sealed class OrganizationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string organizationDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationCategory category = OrganizationCategory.Institution;
        [SerializeField] private OrganizationLifecycleState defaultLifecycleState = OrganizationLifecycleState.Active;
        [SerializeField] private bool allowAliases = true;
        [SerializeField] private bool allowSecretAliases;
        [SerializeField] private bool allowBranches = true;
        [SerializeField] private bool allowAffiliates = true;
        [SerializeField] private bool allowMultipleParents;
        [SerializeField] private bool allowHeadquarters = true;
        [SerializeField] private bool requireHeadquarters;
        [SerializeField] private bool allowOperatingAreas = true;
        [SerializeField] private bool allowDissolution = true;
        [SerializeField] private bool allowSuccessors = true;
        [SerializeField] private bool supportsPublicVisibility = true;
        [SerializeField] private bool supportsRestrictedVisibility = true;
        [SerializeField] private bool supportsSecretVisibility;
        [SerializeField] private bool supportsHiddenVisibility;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField] private string[] referencedSocialNormIds;
        [SerializeField] private string[] tags;
        [SerializeField] private int version = 1;

        public string Id => organizationDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationCategory Category => category;
        public OrganizationLifecycleState DefaultLifecycleState => defaultLifecycleState;
        public bool AllowAliases => allowAliases;
        public bool AllowSecretAliases => allowSecretAliases;
        public bool AllowBranches => allowBranches;
        public bool AllowAffiliates => allowAffiliates;
        public bool AllowMultipleParents => allowMultipleParents;
        public bool AllowHeadquarters => allowHeadquarters;
        public bool RequireHeadquarters => requireHeadquarters;
        public bool AllowOperatingAreas => allowOperatingAreas;
        public bool AllowDissolution => allowDissolution;
        public bool AllowSuccessors => allowSuccessors;
        public bool SupportsPublicVisibility => supportsPublicVisibility;
        public bool SupportsRestrictedVisibility => supportsRestrictedVisibility;
        public bool SupportsSecretVisibility => supportsSecretVisibility;
        public bool SupportsHiddenVisibility => supportsHiddenVisibility;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public IReadOnlyList<string> ReferencedSocialNormIds => referencedSocialNormIds ?? Array.Empty<string>();
        public IReadOnlyList<string> TagIds => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            organizationDefinitionId = organizationDefinitionId?.Trim();
            displayName = displayName?.Trim();
            version = Math.Max(1, version);
            if (requireHeadquarters)
            {
                allowHeadquarters = true;
            }
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            OrganizationCategory organizationCategory,
            OrganizationLifecycleState defaultState = OrganizationLifecycleState.Active,
            bool branches = true,
            bool affiliates = true,
            bool multipleParents = false,
            bool headquarters = true,
            bool requiredHeadquarters = false,
            bool operatingAreas = true,
            bool secretVisibility = false,
            bool hiddenVisibility = false,
            IEnumerable<string> socialNormIds = null,
            IEnumerable<string> tagIds = null)
        {
            organizationDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(display) ? id : display.Trim();
            description = string.Empty;
            category = organizationCategory;
            defaultLifecycleState = defaultState;
            allowAliases = true;
            allowSecretAliases = secretVisibility || hiddenVisibility;
            allowBranches = branches;
            allowAffiliates = affiliates;
            allowMultipleParents = multipleParents;
            allowHeadquarters = headquarters || requiredHeadquarters;
            requireHeadquarters = requiredHeadquarters;
            allowOperatingAreas = operatingAreas;
            allowDissolution = true;
            allowSuccessors = true;
            supportsPublicVisibility = true;
            supportsRestrictedVisibility = true;
            supportsSecretVisibility = secretVisibility;
            supportsHiddenVisibility = hiddenVisibility;
            defaultAccessPolicyId = string.Empty;
            referencedSocialNormIds = Clean(socialNormIds);
            tags = Clean(tagIds);
            version = 1;
        }

        public bool SupportsVisibility(OrganizationVisibility visibility)
        {
            return visibility switch
            {
                OrganizationVisibility.Public => SupportsPublicVisibility,
                OrganizationVisibility.Restricted => SupportsRestrictedVisibility,
                OrganizationVisibility.Secret => SupportsSecretVisibility,
                OrganizationVisibility.Hidden => SupportsHiddenVisibility,
                _ => false
            };
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Organization Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("organization-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Definition '{Id}' should use the 'organization-definition.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(OrganizationCategory), category) || category == OrganizationCategory.Unknown)
            {
                report.AddError($"Organization Definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(OrganizationLifecycleState), defaultLifecycleState)
                || defaultLifecycleState == OrganizationLifecycleState.Unknown
                || defaultLifecycleState == OrganizationLifecycleState.Dissolved
                || defaultLifecycleState == OrganizationLifecycleState.Archived)
            {
                report.AddError($"Organization Definition '{DisplayName}' has invalid default lifecycle state '{defaultLifecycleState}'.");
            }

            if (requireHeadquarters && !allowHeadquarters)
            {
                report.AddError($"Organization Definition '{DisplayName}' requires headquarters but disallows headquarters.");
            }

            if (allowSecretAliases && !supportsSecretVisibility && !supportsHiddenVisibility)
            {
                report.AddError($"Organization Definition '{DisplayName}' allows secret aliases without supporting secret or hidden visibility.");
            }

            if (version < 1)
            {
                report.AddError($"Organization Definition '{DisplayName}' has invalid version '{version}'.");
            }

            foreach (string socialNormId in Clean(referencedSocialNormIds))
            {
                if (definitionsById != null && !definitionsById.ContainsKey(socialNormId))
                {
                    report.AddError($"Organization Definition '{DisplayName}' references missing Social Norm '{socialNormId}'.");
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
}
