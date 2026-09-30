using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.PrototypeIntegration
{
    public static class PrototypeSceneIntegrationContract
    {
        public static IReadOnlyList<PrototypeSceneWorldBindingExpectation> WorldBindings { get; } = new[]
        {
            Location("location.prototype.town", "prototype.scene.location.town", "Prototype Town", PrototypeLocationDefinitionFactory.SettlementDefinitionId),
            Location("location.prototype.adventurers-guild", "prototype.scene.location.adventurers-guild", "Adventurer Guild", PrototypeLocationDefinitionFactory.GuildHallDefinitionId),
            Location("location.prototype.civic-office", "prototype.scene.location.civic-office", "Civic Office", PrototypeLocationDefinitionFactory.GovernmentBuildingDefinitionId),
            Location("location.prototype.merchant-counter", "prototype.scene.location.merchant-counter", "Merchant Guild Counter", PrototypeLocationDefinitionFactory.MarketStallDefinitionId),
            Location("location.prototype.guildmaster-office", "prototype.scene.location.guild-head-office", "Guild Head Office", PrototypeLocationDefinitionFactory.OfficeDefinitionId),
            Location("location.prototype.mayor-office", "prototype.scene.location.mayor-office", "Mayor Office", PrototypeLocationDefinitionFactory.OfficeDefinitionId),
            Location("location.prototype.basement-prison", "prototype.scene.location.basement-prison", "Basement Prison", PrototypeLocationDefinitionFactory.DetentionAreaDefinitionId),
            Location("location.prototype.dungeon-entry", "prototype.scene.location.dungeon-entry", "Dungeon Entry", PrototypeLocationDefinitionFactory.DungeonDefinitionId),

            Interaction(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, "prototype.scene.interaction.adventurer-guild-counter", "Adventurer Guild Counter", PrototypeInteractionPointDefinitionFactory.RegisterAdventurerServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, "prototype.scene.interaction.merchant-guild-counter", "Merchant Guild Counter", PrototypeInteractionPointDefinitionFactory.RegisterMerchantServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.MayorDeskPointId, "prototype.scene.interaction.mayor-desk", "Mayor Desk", PrototypeInteractionPointDefinitionFactory.MeetMayorServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.GuildHeadDeskPointId, "prototype.scene.interaction.guild-head-desk", "Guild Head Desk", PrototypeInteractionPointDefinitionFactory.AdventurerRankAdminServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId, "prototype.scene.interaction.city-records-desk", "City Records Desk", PrototypeInteractionPointDefinitionFactory.RecordsPublicAccessServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.PrisonCellPointId, "prototype.scene.interaction.prison-cell", "Prison Cell", PrototypeInteractionPointDefinitionFactory.PrisonCellInspectServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.QuestBoardPointId, "prototype.scene.interaction.quest-board", "Adventurer Guild Quest Board", PrototypeInteractionPointDefinitionFactory.QuestBoardBrowseServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.ShopCounterPointId, "prototype.scene.interaction.shop-counter", "Shop Counter", PrototypeInteractionPointDefinitionFactory.ShopSaleServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.StorageAccessPointId, "prototype.scene.interaction.guild-storage", "Guild Storage", PrototypeInteractionPointDefinitionFactory.StorageAccessServiceId),
            Interaction(PrototypeInteractionPointDefinitionFactory.WorkstationPointId, "prototype.scene.interaction.workstation", "Prototype Workstation", PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId),

            Connection(PrototypeLocationConnectionDefinitionFactory.TownGuildEntranceConnectionId, "prototype.connection.town-guild", "Town to Guild Entrance", "location.prototype.town", "location.prototype.adventurers-guild"),
            Connection(PrototypeLocationConnectionDefinitionFactory.TownCivicEntranceConnectionId, "prototype.connection.town-civic", "Town to Civic Entrance", "location.prototype.town", "location.prototype.civic-office"),
            Connection(PrototypeLocationConnectionDefinitionFactory.MarketMerchantCounterConnectionId, "prototype.connection.market-merchant", "Market to Merchant Counter", "location.prototype.market-district", "location.prototype.merchant-counter"),
            Connection(PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, "prototype.connection.guild-head-door", "Guild Head Office Door", "location.prototype.adventurers-guild", "location.prototype.guildmaster-office"),
            Connection(PrototypeLocationConnectionDefinitionFactory.MayorOfficeConnectionId, "prototype.connection.mayor-door", "Mayor Office Door", "location.prototype.civic-office", "location.prototype.mayor-office"),
            Connection(PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId, "prototype.connection.records-door", "Records Office Door", "location.prototype.civic-office", "location.prototype.records-office"),
            Connection(PrototypeLocationConnectionDefinitionFactory.GuildStorageConnectionId, "prototype.connection.guild-storage", "Guild Storage Door", "location.prototype.adventurers-guild", "location.prototype.guild-storage"),
            Connection(PrototypeLocationConnectionDefinitionFactory.PrisonCellConnectionId, "prototype.connection.prison-cell", "Prison Cell Door", "location.prototype.guard-station", "location.prototype.basement-prison"),
            Connection(PrototypeLocationConnectionDefinitionFactory.WildernessDungeonConnectionId, "prototype.connection.dungeon-entrance", "Dungeon Entrance", "location.prototype.wilderness-ring", "location.prototype.dungeon-entry"),
            Connection(PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId, "prototype.connection.hidden-passage", "Hidden Guild Prison Passage", "location.prototype.guildmaster-office", "location.prototype.basement-prison"),

            Route(PrototypeLocationRouteDefinitionFactory.TownMarketStreetSegmentId, "prototype.scene.route.town-market", "Town Market Route"),
            Route(PrototypeLocationRouteDefinitionFactory.MarketGuildStreetSegmentId, "prototype.scene.route.market-guild", "Market Guild Route"),
            Route(PrototypeLocationRouteDefinitionFactory.TownWildernessTrailSegmentId, "prototype.scene.route.town-wilderness", "Town Wilderness Route"),
            Checkpoint(PrototypePoliticalTravelFactory.DungeonEntryCheckpointId, "prototype.scene.checkpoint.dungeon-entry", "Dungeon Entry Checkpoint"),
            SpawnAnchor("location.prototype.town", "prototype.scene.spawn.player-town", "Player Town Spawn"),

            Entity(LocationOccupantEntityType.Person, PrototypeEntityLocationFactory.PlayerPersonId, "prototype.scene.entity.player", "Prototype Player"),
            Entity(LocationOccupantEntityType.Person, PrototypeEntityLocationFactory.GuildMasterPersonId, "prototype.scene.entity.guildmaster", "Guild Master"),
            Entity(LocationOccupantEntityType.Person, PrototypeEntityLocationFactory.MerchantPersonId, "prototype.scene.entity.merchant", "Merchant Clerk"),
            Entity(LocationOccupantEntityType.Person, PrototypeEntityLocationFactory.PrisonerPersonId, "prototype.scene.entity.prisoner", "Prototype Prisoner"),
            Entity(LocationOccupantEntityType.WorldEntity, PrototypeEntityLocationFactory.GuildChestEntityId, "prototype.scene.entity.guild-chest", "Guild Chest"),
            Entity(LocationOccupantEntityType.WorldEntity, PrototypeEntityLocationFactory.DungeonDoorEntityId, "prototype.scene.entity.dungeon-door", "Dungeon Door")
        };

        public static IReadOnlyList<PrototypeQuestSourceBindingExpectation> QuestSourceBindings { get; } = new[]
        {
            QuestSource(PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId, PrototypeQuestSourceDefinitionFactory.AdventurerGuildBoardDefinitionId, "prototype.scene.quest-source.adventurer-guild-board", "Adventurer Guild Quest Board", "location.prototype.adventurers-guild", PrototypeInteractionPointDefinitionFactory.QuestBoardPointId, operatingOrganizationId: "organization.prototype.adventurers-guild"),
            QuestSource(PrototypeSceneIntegrationIds.AdventurerGuildCounterSourceId, PrototypeQuestSourceDefinitionFactory.AdventurerGuildCounterDefinitionId, "prototype.scene.quest-source.adventurer-guild-counter", "Adventurer Guild Counter Source", "location.prototype.adventurers-guild", PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, operatingOrganizationId: "organization.prototype.adventurers-guild"),
            QuestSource(PrototypeSceneIntegrationIds.MerchantGuildCounterSourceId, PrototypeQuestSourceDefinitionFactory.MerchantGuildCounterDefinitionId, "prototype.scene.quest-source.merchant-guild-counter", "Merchant Guild Counter Source", "location.prototype.merchant-counter", PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, operatingOrganizationId: "organization.prototype.merchant-guild"),
            QuestSource(PrototypeSceneIntegrationIds.MayorOfficeDeskSourceId, PrototypeQuestSourceDefinitionFactory.MayorOfficeDeskDefinitionId, "prototype.scene.quest-source.mayor-office-desk", "Mayor Office Quest Source", "location.prototype.mayor-office", PrototypeInteractionPointDefinitionFactory.MayorDeskPointId, operatingGovernmentId: "government.prototype.civic"),
            QuestSource(PrototypeSceneIntegrationIds.CityRecordsArchiveSourceId, PrototypeQuestSourceDefinitionFactory.EmptyArchiveDefinitionId, "prototype.scene.quest-source.city-records-archive", "City Records Archive Source", "location.prototype.records-office", PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId, operatingGovernmentId: "government.prototype.civic")
        };

        public static IReadOnlyList<PrototypeScenePhysicalSurfaceExpectation> PhysicalSurfaces { get; } = new[]
        {
            Surface("surface.prototype.guild.quest-board", "Adventurer Guild Quest Board", "PrototypeScene/Gameplay/Buildings/AdventurerGuildBuilding/Quest Board", PrototypeSceneIntegrationIds.AdventurerGuildBoardSourceId, "Authored board mesh carries the quest-source binding key; add separate physical blockers only when the board should block movement."),
            Surface("surface.prototype.guild.counter", "Adventurer Guild Counter", "PrototypeScene/Gameplay/Buildings/AdventurerGuildBuilding/AdventurerGuildCounter", PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, "Authored counter carries the clerk interaction trigger; Quest and Dialogue runtimes remain authoritative."),
            Surface("surface.prototype.merchant.counter", "Merchant Guild Counter", "PrototypeScene/Gameplay/Buildings/AdventurerGuildBuilding/MerchantGuildCounter", PrototypeInteractionPointDefinitionFactory.MerchantGuildCounterPointId, "Authored counter carries merchant interaction trigger; trade, quest-source, and economy runtimes remain authoritative."),
            Surface("surface.prototype.mayor.desk", "Mayor Desk", "PrototypeScene/Gameplay/Phase 2 Production Bindings/Civic/Mayor Desk", PrototypeInteractionPointDefinitionFactory.MayorDeskPointId, "Replace with office desk prop; government and dialogue records remain runtime-owned."),
            Surface("surface.prototype.records.desk", "City Records Desk", "PrototypeScene/Gameplay/Phase 2 Production Bindings/Civic/Records Desk", PrototypeInteractionPointDefinitionFactory.RecordsDeskPointId, "Replace with archive counter/bookcase; Step 8 access controls records projection."),
            Surface("surface.prototype.prison.cell", "Basement Prison Cell", "PrototypeScene/Gameplay/Phase 2 Production Bindings/Prison/Cell", PrototypeInteractionPointDefinitionFactory.PrisonCellPointId, "Replace with built cell geometry; location, connection, and detention state remain runtime-owned."),
            Surface("surface.prototype.guild.storage", "Guild Storage Access", "PrototypeScene/Gameplay/Phase 2 Production Bindings/Storage/Guild Chest", PrototypeEntityLocationFactory.GuildChestEntityId, "Replace with authored storage chest; inventory/property systems own contents."),
            Surface("surface.prototype.dungeon.entrance", "Dungeon Entrance", "PrototypeScene/Gameplay/Phase 2 Production Bindings/Dungeon/Dungeon Door", PrototypeEntityLocationFactory.DungeonDoorEntityId, "Replace with authored dungeon entrance; world connection runtime owns traversal."),
            Surface("surface.prototype.guild.workstation", "Prototype Workstation", "PrototypeScene/Gameplay/Phase 2 Production Bindings/Services/Workstation", PrototypeInteractionPointDefinitionFactory.WorkstationPointId, "Replace with crafting/service station when the scene art is finalized.")
        };

        public static IReadOnlyList<string> RequiredLogicalIds => WorldBindings.Select(item => item.LogicalId)
            .Concat(QuestSourceBindings.Select(item => item.QuestSourceId))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        private static PrototypeSceneWorldBindingExpectation Location(string id, string bindingKey, string display, string definitionId)
        {
            return new PrototypeSceneWorldBindingExpectation(WorldSceneBindingCategory.Location, id, bindingKey, display, WorldSceneBindingRole.Primary, true, definitionId);
        }

        private static PrototypeSceneWorldBindingExpectation Interaction(string id, string bindingKey, string display, string preferredServiceDefinitionId)
        {
            return new PrototypeSceneWorldBindingExpectation(WorldSceneBindingCategory.InteractionPoint, id, bindingKey, display, preferredServiceDefinitionId: preferredServiceDefinitionId);
        }

        private static PrototypeSceneWorldBindingExpectation Connection(string id, string bindingKey, string display, string source, string destination)
        {
            return new PrototypeSceneWorldBindingExpectation(WorldSceneBindingCategory.Connection, id, bindingKey, display, WorldSceneBindingRole.Primary, true, sourceLocationId: source, destinationLocationId: destination);
        }

        private static PrototypeSceneWorldBindingExpectation Entity(LocationOccupantEntityType type, string id, string bindingKey, string display)
        {
            string logical = EntityLocationReferenceKey.Build(type, id, PersistenceService.LocalWorldId);
            return new PrototypeSceneWorldBindingExpectation(WorldSceneBindingCategory.Entity, logical, bindingKey, display);
        }

        private static PrototypeSceneWorldBindingExpectation Route(string id, string bindingKey, string display)
        {
            return new PrototypeSceneWorldBindingExpectation(WorldSceneBindingCategory.RouteSegment, id, bindingKey, display);
        }

        private static PrototypeSceneWorldBindingExpectation Checkpoint(string id, string bindingKey, string display)
        {
            return new PrototypeSceneWorldBindingExpectation(WorldSceneBindingCategory.Checkpoint, id, bindingKey, display);
        }

        private static PrototypeSceneWorldBindingExpectation SpawnAnchor(string locationId, string bindingKey, string display)
        {
            return new PrototypeSceneWorldBindingExpectation(WorldSceneBindingCategory.SpawnAnchor, locationId, bindingKey, display);
        }

        private static PrototypeQuestSourceBindingExpectation QuestSource(string id, string definitionId, string bindingKey, string display, string hostLocationId, string interactionPointId, string operatingOrganizationId = "", string operatingGovernmentId = "")
        {
            return new PrototypeQuestSourceBindingExpectation(id, definitionId, bindingKey, display, hostLocationId, interactionPointId, operatingOrganizationId, operatingGovernmentId);
        }

        private static PrototypeScenePhysicalSurfaceExpectation Surface(string id, string display, string hierarchyPath, string logicalBindingId, string replacementExpectation)
        {
            return new PrototypeScenePhysicalSurfaceExpectation(id, display, hierarchyPath, logicalBindingId, replacementExpectation);
        }
    }
}
