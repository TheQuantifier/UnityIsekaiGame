using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.WorldLocations
{
    [CreateAssetMenu(fileName = "LocationConnectionDefinition", menuName = "Unity Isekai Game/World/Location Connection Definition")]
    public class LocationConnectionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string connectionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private LocationConnectionCategory category = LocationConnectionCategory.Door;
        [SerializeField] private LocationConnectionDirectionality defaultDirectionality = LocationConnectionDirectionality.Bidirectional;
        [SerializeField] private LocationCategory[] supportedSourceCategories = Array.Empty<LocationCategory>();
        [SerializeField] private LocationCategory[] supportedDestinationCategories = Array.Empty<LocationCategory>();
        [SerializeField] private bool requiresAccessPoint;
        [SerializeField] private bool supportsOpenState = true;
        [SerializeField] private bool supportsLockState;
        [SerializeField] private bool supportsBlockageState = true;
        [SerializeField] private bool supportsDestructionState = true;
        [SerializeField] private bool instantaneousTraversal = true;
        [SerializeField] private bool supportsSceneBinding = true;
        [SerializeField] private bool supportsKeyAccess;
        [SerializeField] private bool supportsInstitutionalAccessRules = true;
        [SerializeField] private LocationConnectionVisibility defaultVisibility = LocationConnectionVisibility.Public;
        [SerializeField] private int version = 1;

        public string Id => connectionDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public LocationConnectionCategory Category => category;
        public LocationConnectionDirectionality DefaultDirectionality => defaultDirectionality;
        public IReadOnlyList<LocationCategory> SupportedSourceCategories => supportedSourceCategories ?? Array.Empty<LocationCategory>();
        public IReadOnlyList<LocationCategory> SupportedDestinationCategories => supportedDestinationCategories ?? Array.Empty<LocationCategory>();
        public bool RequiresAccessPoint => requiresAccessPoint;
        public bool SupportsOpenState => supportsOpenState;
        public bool SupportsLockState => supportsLockState;
        public bool SupportsBlockageState => supportsBlockageState;
        public bool SupportsDestructionState => supportsDestructionState;
        public bool InstantaneousTraversal => instantaneousTraversal;
        public bool SupportsSceneBinding => supportsSceneBinding;
        public bool SupportsKeyAccess => supportsKeyAccess;
        public bool SupportsInstitutionalAccessRules => supportsInstitutionalAccessRules;
        public LocationConnectionVisibility DefaultVisibility => defaultVisibility;
        public int Version => version;

        private void OnValidate()
        {
            connectionDefinitionId = connectionDefinitionId?.Trim();
            displayName = displayName?.Trim();
            supportedSourceCategories = CleanCategories(supportedSourceCategories);
            supportedDestinationCategories = CleanCategories(supportedDestinationCategories);
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            LocationConnectionCategory connectionCategory,
            LocationConnectionDirectionality directionality,
            IEnumerable<LocationCategory> sourceCategories,
            IEnumerable<LocationCategory> destinationCategories,
            bool accessPoint,
            bool openState,
            bool lockState,
            bool blockageState,
            bool destructionState,
            bool sceneBinding,
            bool keyAccess,
            bool institutionalAccess,
            LocationConnectionVisibility visibility = LocationConnectionVisibility.Public)
        {
            connectionDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(display) ? id : display.Trim();
            description = string.Empty;
            category = connectionCategory;
            defaultDirectionality = directionality;
            supportedSourceCategories = CleanCategories(sourceCategories);
            supportedDestinationCategories = CleanCategories(destinationCategories);
            requiresAccessPoint = accessPoint;
            supportsOpenState = openState;
            supportsLockState = lockState;
            supportsBlockageState = blockageState;
            supportsDestructionState = destructionState;
            instantaneousTraversal = true;
            supportsSceneBinding = sceneBinding;
            supportsKeyAccess = keyAccess;
            supportsInstitutionalAccessRules = institutionalAccess;
            defaultVisibility = visibility;
            version = 1;
        }

        public bool SupportsEndpoint(LocationCategory source, LocationCategory destination)
        {
            return SupportsCategory(supportedSourceCategories, source) && SupportsCategory(supportedDestinationCategories, destination);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Location Connection Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("location-connection-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Location Connection Definition '{Id}' should use the 'location-connection-definition.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(LocationConnectionCategory), category) || category == LocationConnectionCategory.Unknown)
            {
                report.AddError($"Location Connection Definition '{DisplayName}' must declare a concrete category.");
            }

            if (!Enum.IsDefined(typeof(LocationConnectionDirectionality), defaultDirectionality) || defaultDirectionality == LocationConnectionDirectionality.Unknown)
            {
                report.AddError($"Location Connection Definition '{DisplayName}' must declare concrete directionality.");
            }

            if (SupportedSourceCategories.Count == 0 || SupportedDestinationCategories.Count == 0)
            {
                report.AddError($"Location Connection Definition '{DisplayName}' must declare supported endpoint categories.");
            }

            if (!supportsLockState && supportsKeyAccess)
            {
                report.AddError($"Location Connection Definition '{DisplayName}' supports key access but not lock state.");
            }
        }

        private static bool SupportsCategory(IEnumerable<LocationCategory> supported, LocationCategory categoryToCheck)
        {
            LocationCategory[] values = CleanCategories(supported);
            return values.Length == 0 || values.Contains(categoryToCheck);
        }

        private static LocationCategory[] CleanCategories(IEnumerable<LocationCategory> values)
        {
            return (values ?? Array.Empty<LocationCategory>())
                .Where(value => Enum.IsDefined(typeof(LocationCategory), value) && value != LocationCategory.Unknown)
                .Distinct()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
    }

    [CreateAssetMenu(fileName = "LocationAccessPolicyDefinition", menuName = "Unity Isekai Game/World/Location Access Policy Definition")]
    public class LocationAccessPolicyDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string accessPolicyDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private LocationAccessPolicyCategory category = LocationAccessPolicyCategory.Public;
        [SerializeField] private int priority;
        [SerializeField] private bool allowByDefault;
        [SerializeField] private bool denyByDefault;
        [SerializeField] private string[] requiredOrganizationIds = Array.Empty<string>();
        [SerializeField] private string[] requiredRankIds = Array.Empty<string>();
        [SerializeField] private string[] requiredOfficeIds = Array.Empty<string>();
        [SerializeField] private string[] requiredAuthorityIds = Array.Empty<string>();
        [SerializeField] private string[] requiredEmploymentIds = Array.Empty<string>();
        [SerializeField] private string[] requiredPropertyIds = Array.Empty<string>();
        [SerializeField] private string[] requiredPermitIds = Array.Empty<string>();
        [SerializeField] private string[] requiredWarrantIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCustodyRoleIds = Array.Empty<string>();
        [SerializeField] private string[] requiredKeyInstanceIds = Array.Empty<string>();
        [SerializeField] private string[] requiredKeyDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCredentialIds = Array.Empty<string>();
        [SerializeField] private string[] whitelistedPersonIds = Array.Empty<string>();
        [SerializeField] private string[] blacklistedPersonIds = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => accessPolicyDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public LocationAccessPolicyCategory Category => category;
        public int Priority => priority;
        public bool AllowByDefault => allowByDefault;
        public bool DenyByDefault => denyByDefault;
        public IReadOnlyList<string> RequiredOrganizationIds => requiredOrganizationIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredRankIds => requiredRankIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredOfficeIds => requiredOfficeIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredAuthorityIds => requiredAuthorityIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredEmploymentIds => requiredEmploymentIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredPropertyIds => requiredPropertyIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredPermitIds => requiredPermitIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredWarrantIds => requiredWarrantIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredCustodyRoleIds => requiredCustodyRoleIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredKeyInstanceIds => requiredKeyInstanceIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredKeyDefinitionIds => requiredKeyDefinitionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RequiredCredentialIds => requiredCredentialIds ?? Array.Empty<string>();
        public IReadOnlyList<string> WhitelistedPersonIds => whitelistedPersonIds ?? Array.Empty<string>();
        public IReadOnlyList<string> BlacklistedPersonIds => blacklistedPersonIds ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            accessPolicyDefinitionId = accessPolicyDefinitionId?.Trim();
            displayName = displayName?.Trim();
            requiredOrganizationIds = Clean(requiredOrganizationIds);
            requiredRankIds = Clean(requiredRankIds);
            requiredOfficeIds = Clean(requiredOfficeIds);
            requiredAuthorityIds = Clean(requiredAuthorityIds);
            requiredEmploymentIds = Clean(requiredEmploymentIds);
            requiredPropertyIds = Clean(requiredPropertyIds);
            requiredPermitIds = Clean(requiredPermitIds);
            requiredWarrantIds = Clean(requiredWarrantIds);
            requiredCustodyRoleIds = Clean(requiredCustodyRoleIds);
            requiredKeyInstanceIds = Clean(requiredKeyInstanceIds);
            requiredKeyDefinitionIds = Clean(requiredKeyDefinitionIds);
            requiredCredentialIds = Clean(requiredCredentialIds);
            whitelistedPersonIds = Clean(whitelistedPersonIds);
            blacklistedPersonIds = Clean(blacklistedPersonIds);
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            LocationAccessPolicyCategory policyCategory,
            int policyPriority = 0,
            bool allow = false,
            bool deny = false,
            IEnumerable<string> organizations = null,
            IEnumerable<string> ranks = null,
            IEnumerable<string> offices = null,
            IEnumerable<string> authorities = null,
            IEnumerable<string> employments = null,
            IEnumerable<string> properties = null,
            IEnumerable<string> permits = null,
            IEnumerable<string> warrants = null,
            IEnumerable<string> custodyRoles = null,
            IEnumerable<string> keyInstances = null,
            IEnumerable<string> keyDefinitions = null,
            IEnumerable<string> credentials = null,
            IEnumerable<string> whitelist = null,
            IEnumerable<string> blacklist = null)
        {
            accessPolicyDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(display) ? id : display.Trim();
            description = string.Empty;
            category = policyCategory;
            priority = policyPriority;
            allowByDefault = allow;
            denyByDefault = deny;
            requiredOrganizationIds = Clean(organizations);
            requiredRankIds = Clean(ranks);
            requiredOfficeIds = Clean(offices);
            requiredAuthorityIds = Clean(authorities);
            requiredEmploymentIds = Clean(employments);
            requiredPropertyIds = Clean(properties);
            requiredPermitIds = Clean(permits);
            requiredWarrantIds = Clean(warrants);
            requiredCustodyRoleIds = Clean(custodyRoles);
            requiredKeyInstanceIds = Clean(keyInstances);
            requiredKeyDefinitionIds = Clean(keyDefinitions);
            requiredCredentialIds = Clean(credentials);
            whitelistedPersonIds = Clean(whitelist);
            blacklistedPersonIds = Clean(blacklist);
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
                report.AddError($"Location Access Policy Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("location-access-policy-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Location Access Policy Definition '{Id}' should use the 'location-access-policy-definition.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(LocationAccessPolicyCategory), category) || category == LocationAccessPolicyCategory.Unknown)
            {
                report.AddError($"Location Access Policy Definition '{DisplayName}' must declare a concrete category.");
            }

            if (allowByDefault && denyByDefault)
            {
                report.AddError($"Location Access Policy Definition '{DisplayName}' cannot both allow and deny by default.");
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
