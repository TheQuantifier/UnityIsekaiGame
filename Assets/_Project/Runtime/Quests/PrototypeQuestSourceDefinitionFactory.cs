using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Quests
{
    public static class PrototypeQuestSourceDefinitionFactory
    {
        public const string AdventurerGuildBoardDefinitionId = "quest-source-definition.prototype.adventurers-guild-board";
        public const string AdventurerGuildCounterDefinitionId = "quest-source-definition.prototype.adventurers-guild-counter";
        public const string MerchantGuildCounterDefinitionId = "quest-source-definition.prototype.merchant-guild-counter";
        public const string MayorOfficeDeskDefinitionId = "quest-source-definition.prototype.mayor-office-desk";
        public const string HiddenFactionRumorDefinitionId = "quest-source-definition.prototype.hidden-faction-rumor";
        public const string EmptyArchiveDefinitionId = "quest-source-definition.prototype.empty-archive";

        public static readonly string[] PrototypeDefinitionIds =
        {
            AdventurerGuildBoardDefinitionId,
            AdventurerGuildCounterDefinitionId,
            MerchantGuildCounterDefinitionId,
            MayorOfficeDeskDefinitionId,
            HiddenFactionRumorDefinitionId,
            EmptyArchiveDefinitionId
        };

        public static DefinitionRegistry AddMissingPrototypeQuestSourceDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            foreach (QuestSourceDefinition definition in CreateMissingQuestSourceDefinitions(ids))
            {
                definitions.Add(definition);
            }

            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<QuestSourceDefinition> CreateMissingQuestSourceDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<QuestSourceDefinition> definitions = new List<QuestSourceDefinition>();

            Add(
                definitions,
                ids,
                AdventurerGuildBoardDefinitionId,
                "Adventurer Guild Quest Board",
                QuestSourceCategory.QuestBoard,
                QuestSourceVisibility.Public,
                QuestSourceDiscoveryPolicy.RequiresNearbyPresence,
                QuestListingDiscoveryPolicy.BrowseRevealsListing,
                QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason,
                new QuestSourcePublicationPolicyData { maxActiveListings = 12, duplicatePolicy = QuestListingDuplicatePolicy.RejectActiveDuplicate, expirationPolicy = QuestListingExpirationPolicy.NeverExpires, acceptedListingPolicy = QuestAcceptedListingDisplayPolicy.HideWhenAccepted, repeatableListingPolicy = QuestRepeatableListingDisplayPolicy.KeepListed },
                new QuestSourceFilterData { allowedQuestCategories = new[] { QuestCategory.GuildQuest, QuestCategory.BountyPlaceholder }, requiredQuestTagIds = Array.Empty<string>(), allowedIssuerIds = new[] { "organization.prototype.adventurers-guild" }, allowedRepeatabilityPolicies = Array.Empty<QuestDefinitionRepeatabilityPolicy>() },
                authority: new[] { "authority.prototype.guild.board-post" },
                roles: new[] { QuestSourceRole.Discovery, QuestSourceRole.Listing, QuestSourceRole.Offer, QuestSourceRole.Acceptance, QuestSourceRole.TurnIn, QuestSourceRole.RewardClaim },
                tags: new[] { "guild", "board", "public", "prototype" });

            Add(
                definitions,
                ids,
                AdventurerGuildCounterDefinitionId,
                "Adventurer Guild Counter",
                QuestSourceCategory.GuildCounter,
                QuestSourceVisibility.LocallyKnown,
                QuestSourceDiscoveryPolicy.RequiresInteraction,
                QuestListingDiscoveryPolicy.InspectRevealsDetails,
                QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason,
                new QuestSourcePublicationPolicyData { maxActiveListings = 6, duplicatePolicy = QuestListingDuplicatePolicy.RejectActiveDuplicate, expirationPolicy = QuestListingExpirationPolicy.SourceDefaultDuration, defaultListingDuration = 7d, acceptedListingPolicy = QuestAcceptedListingDisplayPolicy.ShowAsTaken, repeatableListingPolicy = QuestRepeatableListingDisplayPolicy.KeepListed },
                new QuestSourceFilterData { allowedQuestCategories = new[] { QuestCategory.GuildQuest }, requiredQuestTagIds = new[] { "guild" }, allowedIssuerIds = new[] { "organization.prototype.adventurers-guild" }, allowedRepeatabilityPolicies = Array.Empty<QuestDefinitionRepeatabilityPolicy>() },
                providerRequirements: new[] { Requirement(QuestSourceProviderRequirementKind.OrganizationMembership, "organization.prototype.adventurers-guild") },
                authority: new[] { "authority.prototype.guild.quest-offer" },
                roles: new[] { QuestSourceRole.Discovery, QuestSourceRole.Listing, QuestSourceRole.Offer, QuestSourceRole.Acceptance, QuestSourceRole.TurnIn, QuestSourceRole.RewardClaim },
                tags: new[] { "guild", "counter", "prototype" });

            Add(
                definitions,
                ids,
                MerchantGuildCounterDefinitionId,
                "Merchant Guild Counter",
                QuestSourceCategory.Business,
                QuestSourceVisibility.LocallyKnown,
                QuestSourceDiscoveryPolicy.RequiresInteraction,
                QuestListingDiscoveryPolicy.BrowseRevealsListing,
                QuestEligibilityDisplayPolicy.VisibleIneligibleWithPublicReason,
                new QuestSourcePublicationPolicyData { maxActiveListings = 8, duplicatePolicy = QuestListingDuplicatePolicy.AllowMultipleListings, expirationPolicy = QuestListingExpirationPolicy.SourceDefaultDuration, defaultListingDuration = 3d, acceptedListingPolicy = QuestAcceptedListingDisplayPolicy.KeepVisible, repeatableListingPolicy = QuestRepeatableListingDisplayPolicy.KeepListed },
                new QuestSourceFilterData { allowedQuestCategories = new[] { QuestCategory.Delivery }, requiredQuestTagIds = new[] { "merchant" }, allowedIssuerIds = new[] { "organization.prototype.merchant-guild" }, allowedRepeatabilityPolicies = new[] { QuestDefinitionRepeatabilityPolicy.RepeatablePerRecipient } },
                authority: new[] { "authority.prototype.merchant.quest-offer" },
                roles: new[] { QuestSourceRole.Discovery, QuestSourceRole.Listing, QuestSourceRole.Offer, QuestSourceRole.Acceptance, QuestSourceRole.TurnIn, QuestSourceRole.RewardClaim },
                tags: new[] { "merchant", "counter", "prototype" });

            Add(
                definitions,
                ids,
                MayorOfficeDeskDefinitionId,
                "Mayor Office Desk",
                QuestSourceCategory.GovernmentDesk,
                QuestSourceVisibility.GovernmentOfficial,
                QuestSourceDiscoveryPolicy.RequiresInteraction,
                QuestListingDiscoveryPolicy.InspectRevealsDetails,
                QuestEligibilityDisplayPolicy.VisibleIneligibleRedacted,
                new QuestSourcePublicationPolicyData { maxActiveListings = 4, duplicatePolicy = QuestListingDuplicatePolicy.RejectActiveDuplicate, expirationPolicy = QuestListingExpirationPolicy.SourceDefaultDuration, defaultListingDuration = 5d, acceptedListingPolicy = QuestAcceptedListingDisplayPolicy.ShowAsTaken, repeatableListingPolicy = QuestRepeatableListingDisplayPolicy.HideUntilRelisted },
                new QuestSourceFilterData { allowedQuestCategories = new[] { QuestCategory.Investigation }, requiredQuestTagIds = new[] { "civic" }, allowedIssuerIds = new[] { "government.prototype.civic" }, allowedRepeatabilityPolicies = Array.Empty<QuestDefinitionRepeatabilityPolicy>() },
                providerRequirements: new[] { Requirement(QuestSourceProviderRequirementKind.Office, "office.prototype.mayor") },
                authority: new[] { "authority.prototype.city.quest-assign" },
                roles: new[] { QuestSourceRole.Discovery, QuestSourceRole.Listing, QuestSourceRole.Offer, QuestSourceRole.Acceptance, QuestSourceRole.TurnIn, QuestSourceRole.RewardClaim },
                tags: new[] { "mayor", "government", "restricted", "prototype" });

            Add(
                definitions,
                ids,
                HiddenFactionRumorDefinitionId,
                "Hidden Faction Rumor Source",
                QuestSourceCategory.Faction,
                QuestSourceVisibility.Hidden,
                QuestSourceDiscoveryPolicy.RequiresPriorKnowledge,
                QuestListingDiscoveryPolicy.RequiresPriorKnowledge,
                QuestEligibilityDisplayPolicy.DiagnosticOnly,
                new QuestSourcePublicationPolicyData { maxActiveListings = 2, duplicatePolicy = QuestListingDuplicatePolicy.RejectActiveDuplicate, expirationPolicy = QuestListingExpirationPolicy.NeverExpires, acceptedListingPolicy = QuestAcceptedListingDisplayPolicy.HideWhenAccepted, repeatableListingPolicy = QuestRepeatableListingDisplayPolicy.HideUntilRelisted },
                new QuestSourceFilterData { allowedQuestCategories = new[] { QuestCategory.Discovery }, requiredQuestTagIds = new[] { "hidden" }, allowedIssuerIds = new[] { "system.quest" }, allowedRepeatabilityPolicies = Array.Empty<QuestDefinitionRepeatabilityPolicy>() },
                providerRequirements: new[] { Requirement(QuestSourceProviderRequirementKind.FactionMembership, "faction.prototype.hidden") },
                roles: new[] { QuestSourceRole.Discovery, QuestSourceRole.Listing, QuestSourceRole.InformationUnlock, QuestSourceRole.Acceptance },
                tags: new[] { "hidden", "rumor", "faction", "prototype" });

            Add(
                definitions,
                ids,
                EmptyArchiveDefinitionId,
                "Empty Quest Archive",
                QuestSourceCategory.RecordPlaceholder,
                QuestSourceVisibility.Restricted,
                QuestSourceDiscoveryPolicy.PrivilegedOnly,
                QuestListingDiscoveryPolicy.NoAutomaticDiscovery,
                QuestEligibilityDisplayPolicy.DiagnosticOnly,
                new QuestSourcePublicationPolicyData { maxActiveListings = 0, duplicatePolicy = QuestListingDuplicatePolicy.RejectActiveDuplicate, expirationPolicy = QuestListingExpirationPolicy.NeverExpires },
                new QuestSourceFilterData(),
                roles: new[] { QuestSourceRole.Discovery },
                tags: new[] { "archive", "empty", "prototype" });

            return definitions;
        }

        private static void Add(
            ICollection<QuestSourceDefinition> definitions,
            ISet<string> existingIds,
            string id,
            string displayName,
            QuestSourceCategory category,
            QuestSourceVisibility visibility,
            QuestSourceDiscoveryPolicy discoveryPolicy,
            QuestListingDiscoveryPolicy listingDiscoveryPolicy,
            QuestEligibilityDisplayPolicy eligibilityDisplayPolicy,
            QuestSourcePublicationPolicyData publicationPolicy,
            QuestSourceFilterData filters,
            IEnumerable<QuestSourceProviderRequirementData> providerRequirements = null,
            IEnumerable<string> authority = null,
            IEnumerable<QuestSourceRole> roles = null,
            IEnumerable<string> tags = null)
        {
            if (existingIds.Contains(id))
            {
                return;
            }

            QuestSourceDefinition definition = ScriptableObject.CreateInstance<QuestSourceDefinition>();
            definition.name = displayName;
            definition.DevelopmentConfigure(
                id,
                displayName,
                category,
                visibility,
                discoveryPolicy,
                listingDiscoveryPolicy,
                eligibilityDisplayPolicy,
                publicationPolicy,
                filters,
                providerRequirements,
                authority,
                roles,
                tags);
            definitions.Add(definition);
            existingIds.Add(id);
        }

        private static QuestSourceProviderRequirementData Requirement(QuestSourceProviderRequirementKind kind, string id, bool hidden = false)
        {
            return new QuestSourceProviderRequirementData
            {
                kind = kind,
                requirementId = id,
                hidden = hidden
            };
        }
    }
}
