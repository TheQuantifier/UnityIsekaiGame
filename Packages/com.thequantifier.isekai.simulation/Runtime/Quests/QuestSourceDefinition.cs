using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Quests
{
    [CreateAssetMenu(fileName = "QuestSourceDefinition", menuName = "Unity Isekai Game/Quests/Quest Source Definition")]
    public sealed class QuestSourceDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField] private QuestSourceCategory category = QuestSourceCategory.QuestBoard;
        [SerializeField] private QuestSourceVisibility defaultVisibility = QuestSourceVisibility.Public;
        [SerializeField] private QuestSourceDiscoveryPolicy discoveryPolicy = QuestSourceDiscoveryPolicy.RequiresInteraction;
        [SerializeField] private QuestListingDiscoveryPolicy listingDiscoveryPolicy = QuestListingDiscoveryPolicy.BrowseRevealsListing;
        [SerializeField] private QuestEligibilityDisplayPolicy eligibilityDisplayPolicy = QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason;
        [SerializeField] private QuestSourcePublicationPolicyData publicationPolicy = new QuestSourcePublicationPolicyData();
        [SerializeField] private QuestSourceFilterData filters = new QuestSourceFilterData();
        [SerializeField] private QuestSourceProviderRequirementData[] providerRequirements = Array.Empty<QuestSourceProviderRequirementData>();
        [SerializeField] private string[] publicationAuthorityRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] sourceRoleIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string Id => definitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public QuestSourceCategory Category => category;
        public QuestSourceVisibility DefaultVisibility => defaultVisibility;
        public QuestSourceDiscoveryPolicy DiscoveryPolicy => discoveryPolicy;
        public QuestListingDiscoveryPolicy ListingDiscoveryPolicy => listingDiscoveryPolicy;
        public QuestEligibilityDisplayPolicy EligibilityDisplayPolicy => eligibilityDisplayPolicy;
        public QuestSourcePublicationPolicyData PublicationPolicy => publicationPolicy?.Clone() ?? new QuestSourcePublicationPolicyData();
        public QuestSourceFilterData Filters => filters?.Clone() ?? new QuestSourceFilterData();
        public IReadOnlyList<QuestSourceProviderRequirementData> ProviderRequirements => (providerRequirements ?? Array.Empty<QuestSourceProviderRequirementData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<string> PublicationAuthorityRequirementIds => QuestRuntimeModelUtility.Clean(publicationAuthorityRequirementIds);
        public IReadOnlyList<string> SourceRoleIds => QuestRuntimeModelUtility.Clean(sourceRoleIds);
        public IReadOnlyList<string> Tags => QuestRuntimeModelUtility.Clean(tags);

        public QuestSourceDefinitionRecordData ToRecordData()
        {
            return new QuestSourceDefinitionRecordData
            {
                definitionId = Id,
                displayName = DisplayName,
                category = Category,
                defaultVisibility = DefaultVisibility,
                discoveryPolicy = DiscoveryPolicy,
                listingDiscoveryPolicy = ListingDiscoveryPolicy,
                eligibilityDisplayPolicy = EligibilityDisplayPolicy,
                publicationPolicy = PublicationPolicy,
                filters = Filters,
                providerRequirements = ProviderRequirements.ToArray(),
                publicationAuthorityRequirementIds = PublicationAuthorityRequirementIds.ToArray(),
                sourceRoleIds = SourceRoleIds.ToArray(),
                tags = Tags.ToArray()
            };
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            QuestSourceCategory sourceCategory,
            QuestSourceVisibility visibility,
            QuestSourceDiscoveryPolicy sourceDiscovery,
            QuestListingDiscoveryPolicy listingDiscovery,
            QuestEligibilityDisplayPolicy displayPolicy,
            QuestSourcePublicationPolicyData publication,
            QuestSourceFilterData sourceFilters = null,
            IEnumerable<QuestSourceProviderRequirementData> providers = null,
            IEnumerable<string> publicationAuthority = null,
            IEnumerable<QuestSourceRole> roles = null,
            IEnumerable<string> tagIds = null)
        {
            definitionId = id ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(name) ? definitionId : name;
            category = sourceCategory == QuestSourceCategory.Unknown ? QuestSourceCategory.Custom : sourceCategory;
            defaultVisibility = visibility == QuestSourceVisibility.Unknown ? QuestSourceVisibility.Public : visibility;
            discoveryPolicy = sourceDiscovery == QuestSourceDiscoveryPolicy.Unknown ? QuestSourceDiscoveryPolicy.RequiresInteraction : sourceDiscovery;
            listingDiscoveryPolicy = listingDiscovery == QuestListingDiscoveryPolicy.Unknown ? QuestListingDiscoveryPolicy.BrowseRevealsListing : listingDiscovery;
            eligibilityDisplayPolicy = displayPolicy == QuestEligibilityDisplayPolicy.Unknown ? QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason : displayPolicy;
            publicationPolicy = publication?.Clone() ?? new QuestSourcePublicationPolicyData();
            filters = sourceFilters?.Clone() ?? new QuestSourceFilterData();
            providerRequirements = (providers ?? Array.Empty<QuestSourceProviderRequirementData>()).Where(value => value != null).Select(value => value.Clone()).ToArray();
            publicationAuthorityRequirementIds = QuestRuntimeModelUtility.Clean(publicationAuthority);
            sourceRoleIds = QuestRuntimeModelUtility.Clean((roles ?? Array.Empty<QuestSourceRole>()).Where(value => value != QuestSourceRole.Unknown).Select(value => $"quest-source-role.{value.ToString().ToLowerInvariant()}"));
            tags = QuestRuntimeModelUtility.Clean(tagIds);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Quest Source definition is missing a stable ID.");
            }
            else if (!Id.StartsWith("quest-source-definition.", StringComparison.Ordinal))
            {
                report.AddWarning($"Quest Source definition '{DisplayName}' should use the 'quest-source-definition.' namespace prefix.");
            }

            if (Category == QuestSourceCategory.Unknown)
            {
                report.AddError($"Quest Source definition '{DisplayName}' must declare a concrete category.");
            }

            if (DefaultVisibility == QuestSourceVisibility.Unknown)
            {
                report.AddError($"Quest Source definition '{DisplayName}' must declare a concrete default visibility.");
            }

            if (DiscoveryPolicy == QuestSourceDiscoveryPolicy.Unknown)
            {
                report.AddError($"Quest Source definition '{DisplayName}' must declare a source discovery policy.");
            }

            if (ListingDiscoveryPolicy == QuestListingDiscoveryPolicy.Unknown)
            {
                report.AddError($"Quest Source definition '{DisplayName}' must declare a listing discovery policy.");
            }

            if (EligibilityDisplayPolicy == QuestEligibilityDisplayPolicy.Unknown)
            {
                report.AddError($"Quest Source definition '{DisplayName}' must declare an eligibility display policy.");
            }

            QuestSourcePublicationPolicyData policy = PublicationPolicy;
            if (policy.duplicatePolicy == QuestListingDuplicatePolicy.Unknown)
            {
                report.AddError($"Quest Source definition '{DisplayName}' must declare a duplicate listing policy.");
            }

            if (policy.expirationPolicy == QuestListingExpirationPolicy.Unknown)
            {
                report.AddError($"Quest Source definition '{DisplayName}' must declare a listing expiration policy.");
            }

            if (policy.expirationPolicy == QuestListingExpirationPolicy.SourceDefaultDuration && policy.defaultListingDuration < 0d)
            {
                report.AddError($"Quest Source definition '{DisplayName}' source-default listing expiration requires a non-negative duration.");
            }

            foreach (QuestSourceProviderRequirementData requirement in ProviderRequirements)
            {
                if (requirement.kind == QuestSourceProviderRequirementKind.Unknown)
                {
                    report.AddError($"Quest Source definition '{DisplayName}' has an unknown provider requirement.");
                }
            }
        }
    }
}
