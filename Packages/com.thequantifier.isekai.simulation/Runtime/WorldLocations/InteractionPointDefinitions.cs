using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.WorldLocations
{
    [CreateAssetMenu(fileName = "InteractionPointDefinition", menuName = "Unity Isekai Game/World/Interaction Point Definition")]
    public class InteractionPointDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string interactionPointDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InteractionPointCategory category = InteractionPointCategory.Custom;
        [SerializeField] private LocationCategory[] supportedHostLocationCategories = Array.Empty<LocationCategory>();
        [SerializeField] private InteractionServiceCategory[] supportedServiceCategories = Array.Empty<InteractionServiceCategory>();
        [SerializeField] private InteractionSubjectLinkRole[] supportedSubjectLinkRoles = Array.Empty<InteractionSubjectLinkRole>();
        [SerializeField] private int simultaneousUserCapacity = 1;
        [SerializeField] private bool exclusiveUse = true;
        [SerializeField] private bool supportsReservation = true;
        [SerializeField] private bool activeProviderRequired;
        [SerializeField] private bool requiresEntityPresence = true;
        [SerializeField] private bool remoteUseSupported;
        [SerializeField] private bool itemPlacementSupported;
        [SerializeField] private bool mayBecomeUnavailable = true;
        [SerializeField] private bool mayBeSceneBound = true;
        [SerializeField] private InteractionPointVisibility defaultVisibility = InteractionPointVisibility.Public;
        [SerializeField] private string[] validationTags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => interactionPointDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public InteractionPointCategory Category => category;
        public IReadOnlyList<LocationCategory> SupportedHostLocationCategories => supportedHostLocationCategories ?? Array.Empty<LocationCategory>();
        public IReadOnlyList<InteractionServiceCategory> SupportedServiceCategories => supportedServiceCategories ?? Array.Empty<InteractionServiceCategory>();
        public IReadOnlyList<InteractionSubjectLinkRole> SupportedSubjectLinkRoles => supportedSubjectLinkRoles ?? Array.Empty<InteractionSubjectLinkRole>();
        public int SimultaneousUserCapacity => simultaneousUserCapacity;
        public bool ExclusiveUse => exclusiveUse;
        public bool SupportsReservation => supportsReservation;
        public bool ActiveProviderRequired => activeProviderRequired;
        public bool RequiresEntityPresence => requiresEntityPresence;
        public bool RemoteUseSupported => remoteUseSupported;
        public bool ItemPlacementSupported => itemPlacementSupported;
        public bool MayBecomeUnavailable => mayBecomeUnavailable;
        public bool MayBeSceneBound => mayBeSceneBound;
        public InteractionPointVisibility DefaultVisibility => defaultVisibility;
        public IReadOnlyList<string> ValidationTags => validationTags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            interactionPointDefinitionId = interactionPointDefinitionId?.Trim();
            displayName = displayName?.Trim();
            simultaneousUserCapacity = Math.Max(-1, simultaneousUserCapacity);
            version = Math.Max(1, version);
            validationTags = Clean(validationTags);
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            InteractionPointCategory pointCategory,
            IEnumerable<LocationCategory> hostCategories,
            IEnumerable<InteractionServiceCategory> serviceCategories,
            IEnumerable<InteractionSubjectLinkRole> subjectRoles = null,
            int capacity = 1,
            bool exclusive = true,
            bool reservations = true,
            bool providerRequired = false,
            bool presenceRequired = true,
            bool remoteSupported = false,
            bool itemPlacement = false,
            InteractionPointVisibility visibility = InteractionPointVisibility.Public,
            IEnumerable<string> tags = null)
        {
            interactionPointDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(display) ? id : display.Trim();
            description = string.Empty;
            category = pointCategory;
            supportedHostLocationCategories = CleanEnums(hostCategories, LocationCategory.Unknown);
            supportedServiceCategories = CleanEnums(serviceCategories, InteractionServiceCategory.Unknown);
            supportedSubjectLinkRoles = CleanEnums(subjectRoles, InteractionSubjectLinkRole.Unknown);
            simultaneousUserCapacity = Math.Max(-1, capacity);
            exclusiveUse = exclusive;
            supportsReservation = reservations;
            activeProviderRequired = providerRequired;
            requiresEntityPresence = presenceRequired;
            remoteUseSupported = remoteSupported;
            itemPlacementSupported = itemPlacement;
            mayBecomeUnavailable = true;
            mayBeSceneBound = true;
            defaultVisibility = visibility;
            validationTags = Clean(tags);
            version = 1;
        }

        public bool SupportsHostCategory(LocationCategory hostCategory)
        {
            return SupportedHostLocationCategories.Count == 0 || SupportedHostLocationCategories.Contains(hostCategory);
        }

        public bool SupportsServiceCategory(InteractionServiceCategory serviceCategory)
        {
            return SupportedServiceCategories.Count == 0 || SupportedServiceCategories.Contains(serviceCategory);
        }

        public bool SupportsSubjectLinkRole(InteractionSubjectLinkRole role)
        {
            return SupportedSubjectLinkRoles.Count == 0 || SupportedSubjectLinkRoles.Contains(role);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Interaction Point Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("interaction-point-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Interaction Point Definition '{Id}' should use the 'interaction-point-definition.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(InteractionPointCategory), category) || category == InteractionPointCategory.Unknown)
            {
                report.AddError($"Interaction Point Definition '{DisplayName}' must declare a concrete category.");
            }

            if (simultaneousUserCapacity == 0 || simultaneousUserCapacity < -1)
            {
                report.AddError($"Interaction Point Definition '{DisplayName}' has invalid simultaneous capacity '{simultaneousUserCapacity}'.");
            }

            foreach (LocationCategory hostCategory in SupportedHostLocationCategories)
            {
                if (!Enum.IsDefined(typeof(LocationCategory), hostCategory) || hostCategory == LocationCategory.Unknown)
                {
                    report.AddError($"Interaction Point Definition '{DisplayName}' has invalid host category '{hostCategory}'.");
                }
            }

            foreach (InteractionServiceCategory serviceCategory in SupportedServiceCategories)
            {
                if (!Enum.IsDefined(typeof(InteractionServiceCategory), serviceCategory) || serviceCategory == InteractionServiceCategory.Unknown)
                {
                    report.AddError($"Interaction Point Definition '{DisplayName}' has invalid service category '{serviceCategory}'.");
                }
            }

            foreach (InteractionSubjectLinkRole role in SupportedSubjectLinkRoles)
            {
                if (!Enum.IsDefined(typeof(InteractionSubjectLinkRole), role) || role == InteractionSubjectLinkRole.Unknown)
                {
                    report.AddError($"Interaction Point Definition '{DisplayName}' has invalid subject-link role '{role}'.");
                }
            }
        }

        private static TEnum[] CleanEnums<TEnum>(IEnumerable<TEnum> values, TEnum invalid)
            where TEnum : struct, Enum
        {
            return (values ?? Array.Empty<TEnum>())
                .Where(value => !EqualityComparer<TEnum>.Default.Equals(value, invalid))
                .Distinct()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
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

    [CreateAssetMenu(fileName = "InteractionServiceDefinition", menuName = "Unity Isekai Game/World/Interaction Service Definition")]
    public class InteractionServiceDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string interactionServiceDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InteractionServiceCategory category = InteractionServiceCategory.Custom;
        [SerializeField] private string[] compatibleInteractionPointDefinitionIds = Array.Empty<string>();
        [SerializeField] private InteractionProviderRequirementKind providerRequirement = InteractionProviderRequirementKind.NoProvider;
        [SerializeField] private LocationOccupantEntityType requiredConsumerType = LocationOccupantEntityType.Person;
        [SerializeField] private LocationOccupantEntityType requiredProviderType = LocationOccupantEntityType.Person;
        [SerializeField] private InteractionPhysicalPresencePolicy consumerPresencePolicy = InteractionPhysicalPresencePolicy.WithinHostLocation;
        [SerializeField] private InteractionPhysicalPresencePolicy providerPresencePolicy = InteractionPhysicalPresencePolicy.WithinHostLocation;
        [SerializeField] private InteractionDestinationRuntime destinationRuntime = InteractionDestinationRuntime.InteractionPoint;
        [SerializeField] private string[] authorityRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] legalRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] membershipRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] rankRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] officeRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] itemRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] statusRequirementIds = Array.Empty<string>();
        [SerializeField] private bool mutatesDestinationState;
        [SerializeField] private bool supportsPreview = true;
        [SerializeField] private bool providerInteractionRequired;
        [SerializeField] private InteractionPointVisibility visibility = InteractionPointVisibility.Public;
        [SerializeField] private int priority = 100;
        [SerializeField] private string[] validationTags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => interactionServiceDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public InteractionServiceCategory Category => category;
        public IReadOnlyList<string> CompatibleInteractionPointDefinitionIds => compatibleInteractionPointDefinitionIds ?? Array.Empty<string>();
        public InteractionProviderRequirementKind ProviderRequirement => providerRequirement;
        public LocationOccupantEntityType RequiredConsumerType => requiredConsumerType;
        public LocationOccupantEntityType RequiredProviderType => requiredProviderType;
        public InteractionPhysicalPresencePolicy ConsumerPresencePolicy => consumerPresencePolicy;
        public InteractionPhysicalPresencePolicy ProviderPresencePolicy => providerPresencePolicy;
        public InteractionDestinationRuntime DestinationRuntime => destinationRuntime;
        public IReadOnlyList<string> AuthorityRequirementIds => authorityRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> LegalRequirementIds => legalRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> MembershipRequirementIds => membershipRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> RankRequirementIds => rankRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> OfficeRequirementIds => officeRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> ItemRequirementIds => itemRequirementIds ?? Array.Empty<string>();
        public IReadOnlyList<string> StatusRequirementIds => statusRequirementIds ?? Array.Empty<string>();
        public bool MutatesDestinationState => mutatesDestinationState;
        public bool SupportsPreview => supportsPreview;
        public bool ProviderInteractionRequired => providerInteractionRequired;
        public InteractionPointVisibility Visibility => visibility;
        public int Priority => priority;
        public IReadOnlyList<string> ValidationTags => validationTags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            interactionServiceDefinitionId = interactionServiceDefinitionId?.Trim();
            displayName = displayName?.Trim();
            compatibleInteractionPointDefinitionIds = Clean(compatibleInteractionPointDefinitionIds);
            authorityRequirementIds = Clean(authorityRequirementIds);
            legalRequirementIds = Clean(legalRequirementIds);
            membershipRequirementIds = Clean(membershipRequirementIds);
            rankRequirementIds = Clean(rankRequirementIds);
            officeRequirementIds = Clean(officeRequirementIds);
            itemRequirementIds = Clean(itemRequirementIds);
            statusRequirementIds = Clean(statusRequirementIds);
            validationTags = Clean(validationTags);
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string display,
            InteractionServiceCategory serviceCategory,
            IEnumerable<string> compatiblePointDefinitions,
            InteractionDestinationRuntime runtime,
            InteractionProviderRequirementKind provider = InteractionProviderRequirementKind.NoProvider,
            InteractionPhysicalPresencePolicy consumerPresence = InteractionPhysicalPresencePolicy.WithinHostLocation,
            InteractionPhysicalPresencePolicy providerPresence = InteractionPhysicalPresencePolicy.WithinHostLocation,
            bool mutates = false,
            bool preview = true,
            bool providerInteraction = false,
            InteractionPointVisibility serviceVisibility = InteractionPointVisibility.Public,
            int servicePriority = 100,
            IEnumerable<string> authorityRequirements = null,
            IEnumerable<string> legalRequirements = null,
            IEnumerable<string> membershipRequirements = null,
            IEnumerable<string> rankRequirements = null,
            IEnumerable<string> officeRequirements = null,
            IEnumerable<string> itemRequirements = null,
            IEnumerable<string> statusRequirements = null,
            IEnumerable<string> tags = null)
        {
            interactionServiceDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(display) ? id : display.Trim();
            description = string.Empty;
            category = serviceCategory;
            compatibleInteractionPointDefinitionIds = Clean(compatiblePointDefinitions);
            destinationRuntime = runtime;
            providerRequirement = provider;
            requiredConsumerType = LocationOccupantEntityType.Person;
            requiredProviderType = LocationOccupantEntityType.Person;
            consumerPresencePolicy = consumerPresence;
            providerPresencePolicy = providerPresence;
            mutatesDestinationState = mutates;
            supportsPreview = preview;
            providerInteractionRequired = providerInteraction;
            visibility = serviceVisibility;
            priority = servicePriority;
            authorityRequirementIds = Clean(authorityRequirements);
            legalRequirementIds = Clean(legalRequirements);
            membershipRequirementIds = Clean(membershipRequirements);
            rankRequirementIds = Clean(rankRequirements);
            officeRequirementIds = Clean(officeRequirements);
            itemRequirementIds = Clean(itemRequirements);
            statusRequirementIds = Clean(statusRequirements);
            validationTags = Clean(tags);
            version = 1;
        }

        public bool SupportsInteractionPointDefinition(string pointDefinitionId)
        {
            string id = pointDefinitionId?.Trim();
            return CompatibleInteractionPointDefinitionIds.Count == 0 || CompatibleInteractionPointDefinitionIds.Contains(id, StringComparer.Ordinal);
        }

        public bool HasDeclarativeRequirements =>
            AuthorityRequirementIds.Count > 0
            || LegalRequirementIds.Count > 0
            || MembershipRequirementIds.Count > 0
            || RankRequirementIds.Count > 0
            || OfficeRequirementIds.Count > 0
            || ItemRequirementIds.Count > 0
            || StatusRequirementIds.Count > 0;

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Interaction Service Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("interaction-service.", StringComparison.Ordinal))
            {
                report.AddWarning($"Interaction Service Definition '{Id}' should use the 'interaction-service.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(InteractionServiceCategory), category) || category == InteractionServiceCategory.Unknown)
            {
                report.AddError($"Interaction Service Definition '{DisplayName}' must declare a concrete category.");
            }

            if (!Enum.IsDefined(typeof(InteractionProviderRequirementKind), providerRequirement) || providerRequirement == InteractionProviderRequirementKind.Unknown)
            {
                report.AddError($"Interaction Service Definition '{DisplayName}' must declare a concrete provider requirement.");
            }

            if (!Enum.IsDefined(typeof(InteractionPhysicalPresencePolicy), consumerPresencePolicy) || consumerPresencePolicy == InteractionPhysicalPresencePolicy.Unknown)
            {
                report.AddError($"Interaction Service Definition '{DisplayName}' must declare a concrete consumer presence policy.");
            }

            if (!Enum.IsDefined(typeof(InteractionPhysicalPresencePolicy), providerPresencePolicy) || providerPresencePolicy == InteractionPhysicalPresencePolicy.Unknown)
            {
                report.AddError($"Interaction Service Definition '{DisplayName}' must declare a concrete provider presence policy.");
            }

            if (!Enum.IsDefined(typeof(InteractionDestinationRuntime), destinationRuntime) || destinationRuntime == InteractionDestinationRuntime.Unknown)
            {
                report.AddError($"Interaction Service Definition '{DisplayName}' must declare a destination runtime.");
            }

            foreach (string pointDefinitionId in CompatibleInteractionPointDefinitionIds)
            {
                if (string.IsNullOrWhiteSpace(pointDefinitionId))
                {
                    report.AddError($"Interaction Service Definition '{DisplayName}' has an empty compatible point definition.");
                }
                else if (definitionsById != null && !definitionsById.ContainsKey(pointDefinitionId))
                {
                    report.AddError($"Interaction Service Definition '{DisplayName}' references missing point definition '{pointDefinitionId}'.");
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
