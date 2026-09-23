using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public static class PrototypeInteractionPointDefinitionFactory
    {
        public const string AdventurerGuildCounterDefinitionId = "interaction-point-definition.prototype.adventurer-guild-counter";
        public const string MerchantGuildCounterDefinitionId = "interaction-point-definition.prototype.merchant-guild-counter";
        public const string MayorDeskDefinitionId = "interaction-point-definition.prototype.mayor-desk";
        public const string GuildHeadDeskDefinitionId = "interaction-point-definition.prototype.guild-head-desk";
        public const string RecordsDeskDefinitionId = "interaction-point-definition.prototype.city-records-desk";
        public const string PrisonCellDefinitionId = "interaction-point-definition.prototype.prison-cell-point";
        public const string QuestBoardDefinitionId = "interaction-point-definition.prototype.quest-board";
        public const string MerchantStallCounterDefinitionId = "interaction-point-definition.prototype.merchant-stall-counter";
        public const string StorageAccessDefinitionId = "interaction-point-definition.prototype.storage-access";
        public const string WorkstationDefinitionId = "interaction-point-definition.prototype.generic-workstation";

        public const string RegisterAdventurerServiceId = "interaction-service.prototype.register-adventurer";
        public const string AdventurerIntroductionServiceId = "interaction-service.prototype.adventurer-introduction";
        public const string AdventurerRankAdminServiceId = "interaction-service.prototype.adventurer-rank-admin";
        public const string AdventurerInformationServiceId = "interaction-service.prototype.adventurer-information";
        public const string QuestBoardBrowseServiceId = "interaction-service.prototype.quest-board-browse";
        public const string RegisterMerchantServiceId = "interaction-service.prototype.register-merchant";
        public const string MerchantPermitServiceId = "interaction-service.prototype.merchant-specialty-permit";
        public const string MerchantInformationServiceId = "interaction-service.prototype.merchant-information";
        public const string MeetMayorServiceId = "interaction-service.prototype.meet-mayor";
        public const string GovernmentInformationServiceId = "interaction-service.prototype.government-information";
        public const string RecordsPublicAccessServiceId = "interaction-service.prototype.records-public-access";
        public const string RecordsRestrictedAccessServiceId = "interaction-service.prototype.records-restricted-access";
        public const string PrisonCellInspectServiceId = "interaction-service.prototype.prison-cell-inspect";
        public const string ShopSaleServiceId = "interaction-service.prototype.shop-sale-placeholder";
        public const string StorageAccessServiceId = "interaction-service.prototype.storage-access";
        public const string WorkstationUseServiceId = "interaction-service.prototype.workstation-use";

        public const string AdventurerGuildCounterPointId = "interaction-point.prototype.adventurer-guild-counter";
        public const string MerchantGuildCounterPointId = "interaction-point.prototype.merchant-guild-counter";
        public const string MayorDeskPointId = "interaction-point.prototype.mayor-desk";
        public const string GuildHeadDeskPointId = "interaction-point.prototype.guild-head-desk";
        public const string RecordsDeskPointId = "interaction-point.prototype.city-records-desk";
        public const string PrisonCellPointId = "interaction-point.prototype.prison-cell";
        public const string QuestBoardPointId = "interaction-point.prototype.quest-board";
        public const string ShopCounterPointId = "interaction-point.prototype.shop-counter";
        public const string StorageAccessPointId = "interaction-point.prototype.guild-storage";
        public const string WorkstationPointId = "interaction-point.prototype.workstation";

        public static DefinitionRegistry AddMissingPrototypeInteractionDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            definitions.AddRange(CreateMissingPointDefinitions(ids));
            definitions.AddRange(CreateMissingServiceDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<InteractionPointDefinition> CreateMissingPointDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<InteractionPointDefinition> definitions = new List<InteractionPointDefinition>();
            AddPoint(definitions, ids, AdventurerGuildCounterDefinitionId, "Adventurer Guild Counter", InteractionPointCategory.GuildCounter, new[] { LocationCategory.Building, LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.Registration, InteractionServiceCategory.Information, InteractionServiceCategory.MembershipAdministration, InteractionServiceCategory.RankAdministration, InteractionServiceCategory.QuestAccessPlaceholder, InteractionServiceCategory.QuestSubmissionPlaceholder }, roles: OrganizationRoles(), providerRequired: true);
            AddPoint(definitions, ids, MerchantGuildCounterDefinitionId, "Merchant Guild Counter", InteractionPointCategory.MerchantCounter, new[] { LocationCategory.Building, LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.Registration, InteractionServiceCategory.MerchantService, InteractionServiceCategory.PermitAdministration, InteractionServiceCategory.Information, InteractionServiceCategory.MeetingService }, roles: OrganizationRoles(), providerRequired: true);
            AddPoint(definitions, ids, MayorDeskDefinitionId, "Mayor Desk", InteractionPointCategory.GovernmentDesk, new[] { LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.GovernmentService, InteractionServiceCategory.Information, InteractionServiceCategory.OfficeAdministrationPlaceholder }, roles: new[] { InteractionSubjectLinkRole.RepresentedGovernment, InteractionSubjectLinkRole.RepresentedOffice }, providerRequired: true, capacity: 2);
            AddPoint(definitions, ids, GuildHeadDeskDefinitionId, "Guild Head Desk", InteractionPointCategory.AdministrationDesk, new[] { LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.OfficeAdministrationPlaceholder, InteractionServiceCategory.RankAdministration, InteractionServiceCategory.Information }, roles: new[] { InteractionSubjectLinkRole.RepresentedOrganization, InteractionSubjectLinkRole.RepresentedOffice }, providerRequired: true, capacity: 2);
            AddPoint(definitions, ids, RecordsDeskDefinitionId, "City Office Records Desk", InteractionPointCategory.RecordsDesk, new[] { LocationCategory.Building, LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.RecordAccess, InteractionServiceCategory.Information, InteractionServiceCategory.GovernmentService }, roles: new[] { InteractionSubjectLinkRole.RepresentedGovernment, InteractionSubjectLinkRole.AssociatedRecordsCollection }, providerRequired: true);
            AddPoint(definitions, ids, PrisonCellDefinitionId, "Prison Cell Interaction Point", InteractionPointCategory.PrisonCellPoint, new[] { LocationCategory.Room, LocationCategory.FunctionalArea, LocationCategory.Dungeon }, new[] { InteractionServiceCategory.DetentionService, InteractionServiceCategory.Information }, roles: new[] { InteractionSubjectLinkRole.RepresentedGovernment, InteractionSubjectLinkRole.AssociatedCustodyLocation }, providerRequired: false, capacity: 1, exclusive: true, visibility: InteractionPointVisibility.Restricted);
            AddPoint(definitions, ids, QuestBoardDefinitionId, "Quest Board", InteractionPointCategory.QuestBoard, new[] { LocationCategory.Building, LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.QuestAccessPlaceholder, InteractionServiceCategory.Information }, roles: new[] { InteractionSubjectLinkRole.RepresentedOrganization, InteractionSubjectLinkRole.AssociatedQuestSourcePlaceholder }, providerRequired: false, capacity: -1, exclusive: false);
            AddPoint(definitions, ids, MerchantStallCounterDefinitionId, "Merchant Stall Counter", InteractionPointCategory.SalesCounter, new[] { LocationCategory.FunctionalArea, LocationCategory.Building, LocationCategory.Room }, new[] { InteractionServiceCategory.PurchasePlaceholder, InteractionServiceCategory.SalePlaceholder, InteractionServiceCategory.MerchantService }, roles: new[] { InteractionSubjectLinkRole.RepresentedBusiness, InteractionSubjectLinkRole.AssociatedInventory, InteractionSubjectLinkRole.AssociatedProperty }, providerRequired: true, capacity: 2, exclusive: false);
            AddPoint(definitions, ids, StorageAccessDefinitionId, "Storage Access", InteractionPointCategory.StorageAccess, new[] { LocationCategory.Building, LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.StorageAccess }, roles: new[] { InteractionSubjectLinkRole.AssociatedInventory, InteractionSubjectLinkRole.AssociatedProperty, InteractionSubjectLinkRole.RepresentedOrganization }, providerRequired: false, itemPlacement: true);
            AddPoint(definitions, ids, WorkstationDefinitionId, "Generic Workstation", InteractionPointCategory.Workstation, new[] { LocationCategory.Building, LocationCategory.Room, LocationCategory.FunctionalArea }, new[] { InteractionServiceCategory.Crafting, InteractionServiceCategory.EmploymentServicePlaceholder, InteractionServiceCategory.Custom }, roles: new[] { InteractionSubjectLinkRole.RepresentedBusiness, InteractionSubjectLinkRole.RepresentedOrganization, InteractionSubjectLinkRole.Custom }, providerRequired: false);
            return definitions;
        }

        public static IReadOnlyList<InteractionServiceDefinition> CreateMissingServiceDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<InteractionServiceDefinition> definitions = new List<InteractionServiceDefinition>();
            AddService(definitions, ids, RegisterAdventurerServiceId, "Register Adventurer", InteractionServiceCategory.Registration, new[] { AdventurerGuildCounterDefinitionId }, InteractionDestinationRuntime.OrganizationMembership, InteractionProviderRequirementKind.AnyAuthorizedMember, mutates: true, authority: new[] { "permission.prototype.guild.membership-admin" }, membership: new[] { "membership-definition.prototype.adventurer" });
            AddService(definitions, ids, AdventurerIntroductionServiceId, "Adventurer Introduction", InteractionServiceCategory.Information, new[] { AdventurerGuildCounterDefinitionId }, InteractionDestinationRuntime.KnowledgeRecords, InteractionProviderRequirementKind.AnyAuthorizedMember);
            AddService(definitions, ids, AdventurerRankAdminServiceId, "Adventurer Rank Administration", InteractionServiceCategory.RankAdministration, new[] { AdventurerGuildCounterDefinitionId, GuildHeadDeskDefinitionId }, InteractionDestinationRuntime.OrganizationMembership, InteractionProviderRequirementKind.SpecificOfficeholder, mutates: true, authority: new[] { "permission.prototype.guild.rank-admin" });
            AddService(definitions, ids, AdventurerInformationServiceId, "Adventurer Guild Information", InteractionServiceCategory.Information, new[] { AdventurerGuildCounterDefinitionId, GuildHeadDeskDefinitionId }, InteractionDestinationRuntime.KnowledgeRecords, InteractionProviderRequirementKind.AnyAuthorizedMember);
            AddService(definitions, ids, QuestBoardBrowseServiceId, "Browse Quest Board", InteractionServiceCategory.QuestAccessPlaceholder, new[] { AdventurerGuildCounterDefinitionId, QuestBoardDefinitionId }, InteractionDestinationRuntime.QuestPlaceholder, InteractionProviderRequirementKind.NoProvider, providerPresence: InteractionPhysicalPresencePolicy.NotRequired);
            AddService(definitions, ids, RegisterMerchantServiceId, "Register Merchant", InteractionServiceCategory.Registration, new[] { MerchantGuildCounterDefinitionId }, InteractionDestinationRuntime.OrganizationMembership, InteractionProviderRequirementKind.AnyAuthorizedMember, mutates: true, authority: new[] { "permission.prototype.merchant.membership-admin" });
            AddService(definitions, ids, MerchantPermitServiceId, "Merchant Specialty Permit", InteractionServiceCategory.PermitAdministration, new[] { MerchantGuildCounterDefinitionId }, InteractionDestinationRuntime.Legal, InteractionProviderRequirementKind.AnyAuthorizedMember, mutates: true, authority: new[] { "permission.prototype.merchant.permit-admin" }, legal: new[] { "legal-action.prototype.merchant-specialty-permit" });
            AddService(definitions, ids, MerchantInformationServiceId, "Merchant Guild Information", InteractionServiceCategory.Information, new[] { MerchantGuildCounterDefinitionId }, InteractionDestinationRuntime.KnowledgeRecords, InteractionProviderRequirementKind.AnyAuthorizedMember);
            AddService(definitions, ids, MeetMayorServiceId, "Meet Mayor", InteractionServiceCategory.GovernmentService, new[] { MayorDeskDefinitionId }, InteractionDestinationRuntime.Social, InteractionProviderRequirementKind.SpecificOfficeholder, office: new[] { "office.prototype.mayor" });
            AddService(definitions, ids, GovernmentInformationServiceId, "Government Information", InteractionServiceCategory.Information, new[] { MayorDeskDefinitionId }, InteractionDestinationRuntime.KnowledgeRecords, InteractionProviderRequirementKind.SpecificOfficeholder);
            AddService(definitions, ids, RecordsPublicAccessServiceId, "Public Records Access", InteractionServiceCategory.RecordAccess, new[] { RecordsDeskDefinitionId }, InteractionDestinationRuntime.KnowledgeRecords, InteractionProviderRequirementKind.AssignedClerk);
            AddService(definitions, ids, RecordsRestrictedAccessServiceId, "Restricted Records Access", InteractionServiceCategory.RecordAccess, new[] { RecordsDeskDefinitionId }, InteractionDestinationRuntime.KnowledgeRecords, InteractionProviderRequirementKind.AssignedClerk, authority: new[] { "permission.prototype.records.restricted-read" }, legal: new[] { "legal-right.prototype.records.restricted-read" }, visibility: InteractionPointVisibility.Restricted);
            AddService(definitions, ids, PrisonCellInspectServiceId, "Inspect Prison Cell", InteractionServiceCategory.DetentionService, new[] { PrisonCellDefinitionId }, InteractionDestinationRuntime.Justice, InteractionProviderRequirementKind.NoProvider, providerPresence: InteractionPhysicalPresencePolicy.NotRequired, visibility: InteractionPointVisibility.Restricted);
            AddService(definitions, ids, ShopSaleServiceId, "Shop Sale Placeholder", InteractionServiceCategory.SalePlaceholder, new[] { MerchantStallCounterDefinitionId }, InteractionDestinationRuntime.BusinessTrade, InteractionProviderRequirementKind.AssignedPerson, mutates: true);
            AddService(definitions, ids, StorageAccessServiceId, "Storage Access", InteractionServiceCategory.StorageAccess, new[] { StorageAccessDefinitionId }, InteractionDestinationRuntime.ItemInventory, InteractionProviderRequirementKind.NoProvider, providerPresence: InteractionPhysicalPresencePolicy.NotRequired, mutates: true);
            AddService(definitions, ids, WorkstationUseServiceId, "Use Workstation", InteractionServiceCategory.Crafting, new[] { WorkstationDefinitionId }, InteractionDestinationRuntime.Crafting, InteractionProviderRequirementKind.NoProvider, providerPresence: InteractionPhysicalPresencePolicy.NotRequired, mutates: true);
            return definitions;
        }

        public static void SeedPrototypeInteractionPoints(InteractionPointRuntime runtime, DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, string worldId)
        {
            if (runtime == null)
            {
                return;
            }

            string world = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
            runtime.Configure(registry, locations, entityLocations, world);
            SeedPoint(runtime, AdventurerGuildCounterPointId, AdventurerGuildCounterDefinitionId, "Adventurer Guild Counter", "location.prototype.adventurers-guild", new[] { RegisterAdventurerServiceId, AdventurerIntroductionServiceId, AdventurerRankAdminServiceId, AdventurerInformationServiceId, QuestBoardBrowseServiceId }, "prototype.scene.interaction.adventurer-guild-counter");
            Link(runtime, AdventurerGuildCounterPointId, InteractionSubjectLinkRole.RepresentedOrganization, "Organization", "organization.prototype.guild", world);
            Provider(runtime, AdventurerGuildCounterPointId, RegisterAdventurerServiceId, PrototypeEntityLocationFactory.GuildMasterPersonId, world);

            SeedPoint(runtime, MerchantGuildCounterPointId, MerchantGuildCounterDefinitionId, "Merchant Guild Counter", "location.prototype.merchant-counter", new[] { RegisterMerchantServiceId, MerchantPermitServiceId, MerchantInformationServiceId }, "prototype.scene.interaction.merchant-guild-counter");
            Link(runtime, MerchantGuildCounterPointId, InteractionSubjectLinkRole.RepresentedOrganization, "Organization", "organization.prototype.merchant-guild", world);
            Provider(runtime, MerchantGuildCounterPointId, RegisterMerchantServiceId, PrototypeEntityLocationFactory.MerchantPersonId, world);

            SeedPoint(runtime, MayorDeskPointId, MayorDeskDefinitionId, "Mayor Desk", "location.prototype.mayor-office", new[] { MeetMayorServiceId, GovernmentInformationServiceId }, "prototype.scene.interaction.mayor-desk");
            Link(runtime, MayorDeskPointId, InteractionSubjectLinkRole.RepresentedGovernment, "Government", "government.prototype.civic", world);
            Link(runtime, MayorDeskPointId, InteractionSubjectLinkRole.RepresentedOffice, "Office", "office.prototype.mayor", world);

            SeedPoint(runtime, GuildHeadDeskPointId, GuildHeadDeskDefinitionId, "Guild Head Desk", "location.prototype.guildmaster-office", new[] { AdventurerRankAdminServiceId, AdventurerInformationServiceId }, "prototype.scene.interaction.guild-head-desk");
            Link(runtime, GuildHeadDeskPointId, InteractionSubjectLinkRole.RepresentedOffice, "Office", "office.prototype.guild-head", world);

            SeedPoint(runtime, RecordsDeskPointId, RecordsDeskDefinitionId, "City Office Records Desk", "location.prototype.civic-office", new[] { RecordsPublicAccessServiceId, RecordsRestrictedAccessServiceId }, "prototype.scene.interaction.city-records-desk");
            Link(runtime, RecordsDeskPointId, InteractionSubjectLinkRole.AssociatedRecordsCollection, "KnowledgeRecordCollection", "records.prototype.civic-public", world);

            SeedPoint(runtime, PrisonCellPointId, PrisonCellDefinitionId, "Prison Cell", "location.prototype.basement-prison", new[] { PrisonCellInspectServiceId }, "prototype.scene.interaction.prison-cell", InteractionPointVisibility.Restricted);
            Link(runtime, PrisonCellPointId, InteractionSubjectLinkRole.AssociatedCustodyLocation, "Location", "location.prototype.basement-prison", world, InteractionPointVisibility.Restricted);

            SeedPoint(runtime, QuestBoardPointId, QuestBoardDefinitionId, "Quest Board", "location.prototype.adventurers-guild", new[] { QuestBoardBrowseServiceId }, "prototype.scene.interaction.quest-board");
            SeedPoint(runtime, ShopCounterPointId, MerchantStallCounterDefinitionId, "Shop Counter", "location.prototype.merchant-counter", new[] { ShopSaleServiceId }, "prototype.scene.interaction.shop-counter");
            SeedPoint(runtime, StorageAccessPointId, StorageAccessDefinitionId, "Guild Storage Access", "location.prototype.adventurers-guild", new[] { StorageAccessServiceId }, "prototype.scene.interaction.guild-storage");
            SeedPoint(runtime, WorkstationPointId, WorkstationDefinitionId, "Prototype Workstation", "location.prototype.merchant-counter", new[] { WorkstationUseServiceId }, "prototype.scene.interaction.workstation");
        }

        private static InteractionSubjectLinkRole[] OrganizationRoles()
        {
            return new[] { InteractionSubjectLinkRole.RepresentedOrganization, InteractionSubjectLinkRole.ServiceProviderOrganization, InteractionSubjectLinkRole.AssociatedQuestSourcePlaceholder, InteractionSubjectLinkRole.AssociatedRecordsCollection };
        }

        private static void AddPoint(ICollection<InteractionPointDefinition> definitions, ISet<string> ids, string id, string display, InteractionPointCategory category, IEnumerable<LocationCategory> hostCategories, IEnumerable<InteractionServiceCategory> serviceCategories, IEnumerable<InteractionSubjectLinkRole> roles, int capacity = 1, bool exclusive = true, bool reservations = true, bool providerRequired = false, bool presenceRequired = true, bool itemPlacement = false, InteractionPointVisibility visibility = InteractionPointVisibility.Public)
        {
            if (ids.Contains(id)) return;
            InteractionPointDefinition definition = ScriptableObject.CreateInstance<InteractionPointDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, hostCategories, serviceCategories, roles, capacity, exclusive, reservations, providerRequired, presenceRequired, false, itemPlacement, visibility, new[] { "prototype", category.ToString().ToLowerInvariant() });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddService(ICollection<InteractionServiceDefinition> definitions, ISet<string> ids, string id, string display, InteractionServiceCategory category, IEnumerable<string> pointDefinitions, InteractionDestinationRuntime runtime, InteractionProviderRequirementKind provider, InteractionPhysicalPresencePolicy consumerPresence = InteractionPhysicalPresencePolicy.WithinHostLocation, InteractionPhysicalPresencePolicy providerPresence = InteractionPhysicalPresencePolicy.WithinHostLocation, bool mutates = false, IEnumerable<string> authority = null, IEnumerable<string> legal = null, IEnumerable<string> membership = null, IEnumerable<string> rank = null, IEnumerable<string> office = null, InteractionPointVisibility visibility = InteractionPointVisibility.Public)
        {
            if (ids.Contains(id)) return;
            InteractionServiceDefinition definition = ScriptableObject.CreateInstance<InteractionServiceDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, pointDefinitions, runtime, provider, consumerPresence, providerPresence, mutates, true, provider != InteractionProviderRequirementKind.NoProvider && provider != InteractionProviderRequirementKind.AutomatedService, visibility, 100, authority, legal, membership, rank, office, tags: new[] { "prototype", category.ToString().ToLowerInvariant() });
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void SeedPoint(InteractionPointRuntime runtime, string pointId, string definitionId, string display, string host, IEnumerable<string> services, string binding, InteractionPointVisibility visibility = InteractionPointVisibility.Public)
        {
            runtime.CreatePoint(new InteractionPointCreateRequest
            {
                transactionId = $"prototype.seed.{pointId}",
                interactionPointId = pointId,
                interactionPointDefinitionId = definitionId,
                displayName = display,
                hostLocationId = host,
                hostAssignmentId = $"interaction-host.prototype.{pointId.Replace("interaction-point.prototype.", string.Empty)}",
                serviceDefinitionIds = services,
                visibility = visibility,
                sceneBindingKey = binding,
                sceneBindingCategory = InteractionSceneBindingCategory.PrototypeMarker,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.interaction-point.seed"
            });
        }

        private static void Link(InteractionPointRuntime runtime, string pointId, InteractionSubjectLinkRole role, string subjectType, string subjectId, string world, InteractionPointVisibility visibility = InteractionPointVisibility.Public)
        {
            runtime.AddSubjectLink(new InteractionSubjectLinkRequest
            {
                transactionId = $"prototype.seed.link.{pointId}.{role}.{subjectId}",
                linkId = $"interaction-subject.prototype.{pointId.Replace("interaction-point.prototype.", string.Empty)}.{role.ToString().ToLowerInvariant()}",
                interactionPointId = pointId,
                role = role,
                subject = new InteractionSubjectReferenceData { subjectType = subjectType, subjectId = subjectId, worldId = world },
                visibility = visibility,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.interaction-point.seed"
            });
        }

        private static void Provider(InteractionPointRuntime runtime, string pointId, string serviceId, string personId, string world)
        {
            runtime.AssignProvider(new InteractionProviderAssignmentRequest
            {
                transactionId = $"prototype.seed.provider.{pointId}.{serviceId}",
                assignmentId = $"interaction-provider.prototype.{pointId.Replace("interaction-point.prototype.", string.Empty)}.{serviceId.Replace("interaction-service.prototype.", string.Empty)}",
                interactionPointId = pointId,
                serviceDefinitionId = serviceId,
                requirementKind = InteractionProviderRequirementKind.AssignedPerson,
                providerEntity = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Person, entityId = personId, worldId = world },
                presencePolicy = InteractionPhysicalPresencePolicy.WithinHostLocation,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.interaction-point.seed"
            });
        }
    }
}
