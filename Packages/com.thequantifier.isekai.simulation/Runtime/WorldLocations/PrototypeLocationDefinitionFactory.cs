using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public static class PrototypeLocationDefinitionFactory
    {
        public const string WorldDefinitionId = "location-definition.world";
        public const string RegionDefinitionId = "location-definition.region";
        public const string SettlementDefinitionId = "location-definition.settlement";
        public const string DistrictDefinitionId = "location-definition.district";
        public const string GuildHallDefinitionId = "location-definition.guild-hall";
        public const string GovernmentBuildingDefinitionId = "location-definition.government-building";
        public const string MarketStallDefinitionId = "location-definition.market-stall";
        public const string TempleDefinitionId = "location-definition.temple";
        public const string UniversityDefinitionId = "location-definition.university";
        public const string ForgeDefinitionId = "location-definition.forge";
        public const string GuardPostDefinitionId = "location-definition.guard-post";
        public const string CourtDefinitionId = "location-definition.court";
        public const string RoomDefinitionId = "location-definition.room";
        public const string OfficeDefinitionId = "location-definition.office";
        public const string StorageDefinitionId = "location-definition.storage";
        public const string WorkshopDefinitionId = "location-definition.workshop";
        public const string DetentionAreaDefinitionId = "location-definition.detention-area";
        public const string DungeonDefinitionId = "location-definition.dungeon";
        public const string WildernessDefinitionId = "location-definition.wilderness";
        public const string InteractionPointDefinitionId = "location-definition.interaction-point";

        public static readonly string[] PrototypeLocationIds =
        {
            "location.prototype.world",
            "location.prototype.region",
            "location.prototype.town",
            "location.prototype.market-district",
            "location.prototype.adventurers-guild",
            "location.prototype.merchant-guild",
            "location.prototype.civic-office",
            "location.prototype.merchant-counter",
            "location.prototype.guildmaster-office",
            "location.prototype.mayor-office",
            "location.prototype.records-office",
            "location.prototype.guild-storage",
            "location.prototype.guard-station",
            "location.prototype.courthouse",
            "location.prototype.basement-prison",
            "location.prototype.temple",
            "location.prototype.university",
            "location.prototype.forge",
            "location.prototype.forge-workshop",
            "location.prototype.dungeon-entry",
            "location.prototype.wilderness-ring"
        };

        public static DefinitionRegistry AddMissingPrototypeLocationDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            foreach (LocationDefinition definition in CreateMissingLocationDefinitions(ids))
            {
                definitions.Add(definition);
            }

            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<LocationDefinition> CreateMissingLocationDefinitions(IEnumerable<string> existingDefinitionIds)
        {
            HashSet<string> ids = existingDefinitionIds == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(existingDefinitionIds, StringComparer.Ordinal);
            List<LocationDefinition> definitions = new List<LocationDefinition>();
            Add(definitions, ids, WorldDefinitionId, "World", LocationCategory.World, tags: new[] { "world", "root" });
            Add(definitions, ids, RegionDefinitionId, "Region", LocationCategory.Region, tags: new[] { "region" });
            Add(definitions, ids, SettlementDefinitionId, "Settlement", LocationCategory.Settlement, tags: new[] { "settlement", "public" });
            Add(definitions, ids, DistrictDefinitionId, "District", LocationCategory.District, tags: new[] { "district", "public" });
            Add(definitions, ids, GuildHallDefinitionId, "Guild Hall", LocationCategory.Building, organizationAssociation: true, governmentAssociation: false, tags: new[] { "guild", "building", "service" });
            Add(definitions, ids, GovernmentBuildingDefinitionId, "Government Building", LocationCategory.Building, organizationAssociation: true, governmentAssociation: true, tags: new[] { "government", "building", "civic" });
            Add(definitions, ids, MarketStallDefinitionId, "Market Stall", LocationCategory.FunctionalArea, propertyAssociation: true, organizationAssociation: true, governmentAssociation: false, tags: new[] { "market", "commerce", "stall" });
            Add(definitions, ids, TempleDefinitionId, "Temple", LocationCategory.Building, tags: new[] { "temple", "building", "service" });
            Add(definitions, ids, UniversityDefinitionId, "University", LocationCategory.Building, tags: new[] { "university", "building", "education" });
            Add(definitions, ids, ForgeDefinitionId, "Forge", LocationCategory.Building, tags: new[] { "forge", "building", "crafting" });
            Add(definitions, ids, GuardPostDefinitionId, "Guard Post", LocationCategory.Building, governmentAssociation: true, tags: new[] { "guard", "building", "justice" });
            Add(definitions, ids, CourtDefinitionId, "Court", LocationCategory.Building, governmentAssociation: true, tags: new[] { "court", "building", "justice" });
            Add(definitions, ids, RoomDefinitionId, "Room", LocationCategory.Room, tags: new[] { "room", "interior" });
            Add(definitions, ids, OfficeDefinitionId, "Office", LocationCategory.Room, organizationAssociation: true, governmentAssociation: true, tags: new[] { "office", "interior", "restricted" });
            Add(definitions, ids, StorageDefinitionId, "Storage", LocationCategory.Room, organizationAssociation: true, tags: new[] { "storage", "interior", "restricted" });
            Add(definitions, ids, WorkshopDefinitionId, "Workshop", LocationCategory.FunctionalArea, organizationAssociation: true, tags: new[] { "workshop", "crafting", "service" });
            Add(definitions, ids, DetentionAreaDefinitionId, "Detention Area", LocationCategory.Room, secret: true, governmentAssociation: true, tags: new[] { "detention", "justice", "restricted" });
            Add(definitions, ids, DungeonDefinitionId, "Dungeon", LocationCategory.Dungeon, secret: true, hidden: true, tags: new[] { "dungeon", "hazard", "interior" });
            Add(definitions, ids, WildernessDefinitionId, "Wilderness", LocationCategory.Wilderness, governmentAssociation: false, organizationAssociation: false, tags: new[] { "wilderness", "outdoor" });
            Add(definitions, ids, InteractionPointDefinitionId, "Interaction Point", LocationCategory.InteractionPoint, propertyAssociation: false, organizationAssociation: true, governmentAssociation: true, territoryAssociation: false, tags: new[] { "interaction", "service" });
            return definitions;
        }

        public static void SeedPrototypeLocations(LocationRuntime runtime, DefinitionRegistry registry, string worldId)
        {
            if (runtime == null)
            {
                return;
            }

            string world = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId;
            runtime.Configure(registry, world);
            Seed(runtime, "location.prototype.world", WorldDefinitionId, "Prototype World", "World", new[] { "world", "root" }, authoredPlaceDefinitionId: "place.world.prototype");
            Seed(runtime, "location.prototype.region", RegionDefinitionId, "Prototype Region", "Region", new[] { "region" }, authoredPlaceDefinitionId: "place.region.prototype");
            Seed(runtime, "location.prototype.town", SettlementDefinitionId, "Prototype Town", "Town", new[] { "settlement", "public" }, authoredPlaceDefinitionId: "place.settlement.prototype-town");
            Seed(runtime, "location.prototype.market-district", DistrictDefinitionId, "Prototype Market District", "Market District", new[] { "district", "public" });
            Seed(runtime, "location.prototype.adventurers-guild", GuildHallDefinitionId, "Prototype Adventurers Guild Hall", "Adventurers Guild", new[] { "guild", "building", "service" }, organizationId: "organization.prototype.adventurers-guild", binding: "prototype.scene.adventurers-guild");
            Seed(runtime, "location.prototype.merchant-guild", GuildHallDefinitionId, "Prototype Merchant Guild Hall", "Merchant Guild", new[] { "guild", "building", "service" }, organizationId: "organization.prototype.merchant-guild");
            Seed(runtime, "location.prototype.civic-office", GovernmentBuildingDefinitionId, "Prototype Civic Office", "Civic Office", new[] { "government", "building", "civic" }, organizationId: "organization.prototype.government", governmentId: "government.prototype.civic", binding: "prototype.scene.civic-office");
            Seed(runtime, "location.prototype.merchant-counter", MarketStallDefinitionId, "Prototype Merchant Counter", "Merchant Counter", new[] { "market", "commerce", "stall" }, organizationId: "organization.prototype.merchant-guild", binding: "prototype.scene.merchant-counter");
            Seed(runtime, "location.prototype.guildmaster-office", OfficeDefinitionId, "Prototype Guildmaster Office", "Guildmaster Office", new[] { "office", "interior", "restricted" }, organizationId: "organization.prototype.adventurers-guild");
            Seed(runtime, "location.prototype.mayor-office", OfficeDefinitionId, "Prototype Mayor Office", "Mayor Office", new[] { "office", "interior", "restricted" }, organizationId: "organization.prototype.government", governmentId: "government.prototype.civic");
            Seed(runtime, "location.prototype.records-office", OfficeDefinitionId, "Prototype Records Office", "Records Office", new[] { "office", "interior", "restricted" }, organizationId: "organization.prototype.government", governmentId: "government.prototype.civic");
            Seed(runtime, "location.prototype.guild-storage", StorageDefinitionId, "Prototype Guild Storage", "Guild Storage", new[] { "storage", "interior", "restricted" }, organizationId: "organization.prototype.adventurers-guild", visibility: LocationVisibility.Restricted);
            Seed(runtime, "location.prototype.guard-station", GuardPostDefinitionId, "Prototype Guard Station", "Guard Station", new[] { "guard", "building", "justice" }, organizationId: "organization.prototype.government", governmentId: "government.prototype.civic");
            Seed(runtime, "location.prototype.courthouse", CourtDefinitionId, "Prototype Courthouse", "Courthouse", new[] { "court", "building", "justice" }, organizationId: "organization.prototype.government", governmentId: "government.prototype.civic");
            Seed(runtime, "location.prototype.basement-prison", DetentionAreaDefinitionId, "Prototype Basement Prison", "Basement Prison", new[] { "detention", "justice", "restricted" }, governmentId: "government.prototype.civic", visibility: LocationVisibility.Restricted);
            Seed(runtime, "location.prototype.temple", TempleDefinitionId, "Prototype Temple", "Temple", new[] { "temple", "building", "service" }, organizationId: "organization.prototype.temple");
            Seed(runtime, "location.prototype.university", UniversityDefinitionId, "Prototype University", "University", new[] { "university", "building", "education" }, organizationId: "organization.prototype.university");
            Seed(runtime, "location.prototype.forge", ForgeDefinitionId, "Prototype Forge", "Forge", new[] { "forge", "building", "crafting" });
            Seed(runtime, "location.prototype.forge-workshop", WorkshopDefinitionId, "Prototype Forge Workshop", "Forge Workshop", new[] { "workshop", "crafting", "service" });
            Seed(runtime, "location.prototype.dungeon-entry", DungeonDefinitionId, "Prototype Dungeon Entry", "Dungeon Entry", new[] { "dungeon", "hazard", "interior" }, visibility: LocationVisibility.Secret, binding: "prototype.scene.dungeon-entry");
            Seed(runtime, "location.prototype.wilderness-ring", WildernessDefinitionId, "Prototype Wilderness Ring", "Wilderness Ring", new[] { "wilderness", "outdoor" }, authoredPlaceDefinitionId: "place.wilderness.prototype-outskirts");
            SeedPrototypeLocationHierarchy(runtime);
            SeedPrototypeSpatialRelationships(runtime);
        }

        public static void SeedPrototypeLocationHierarchy(LocationRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            Link(runtime, "location.prototype.world", "location.prototype.region", "world-region");
            Link(runtime, "location.prototype.region", "location.prototype.town", "region-town");
            Link(runtime, "location.prototype.region", "location.prototype.wilderness-ring", "region-wilderness");
            Link(runtime, "location.prototype.town", "location.prototype.market-district", "town-market");
            Link(runtime, "location.prototype.town", "location.prototype.adventurers-guild", "town-adventurers-guild");
            Link(runtime, "location.prototype.town", "location.prototype.merchant-guild", "town-merchant-guild");
            Link(runtime, "location.prototype.town", "location.prototype.civic-office", "town-civic");
            Link(runtime, "location.prototype.town", "location.prototype.guard-station", "town-guard");
            Link(runtime, "location.prototype.town", "location.prototype.courthouse", "town-court");
            Link(runtime, "location.prototype.town", "location.prototype.temple", "town-temple");
            Link(runtime, "location.prototype.town", "location.prototype.university", "town-university");
            Link(runtime, "location.prototype.town", "location.prototype.forge", "town-forge");
            Link(runtime, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", "guild-office", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.adventurers-guild", "location.prototype.guild-storage", "guild-storage", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.merchant-guild", "location.prototype.merchant-counter", "merchant-guild-counter", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.civic-office", "location.prototype.mayor-office", "civic-mayor", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.civic-office", "location.prototype.records-office", "civic-records", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.guard-station", "location.prototype.basement-prison", "guard-prison", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.forge", "location.prototype.forge-workshop", "forge-workshop", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.wilderness-ring", "location.prototype.dungeon-entry", "wilderness-dungeon", LocationContainmentKind.Dungeon, LocationVisibility.Secret);
        }

        public static void SeedPrototypeSpatialRelationships(LocationRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            Relate(runtime, "location.prototype.market-district", "location.prototype.adventurers-guild", "market-near-guild", LocationSpatialRelationshipKind.Near, LocationSpatialDirectionality.Symmetric);
            Relate(runtime, "location.prototype.adventurers-guild", "location.prototype.civic-office", "guild-facing-civic", LocationSpatialRelationshipKind.Facing, LocationSpatialDirectionality.Directional);
            Relate(runtime, "location.prototype.basement-prison", "location.prototype.guard-station", "prison-below-guard", LocationSpatialRelationshipKind.Below, LocationSpatialDirectionality.Directional, LocationVisibility.Restricted);
            Relate(runtime, "location.prototype.dungeon-entry", "location.prototype.wilderness-ring", "dungeon-inside-wilderness", LocationSpatialRelationshipKind.PartOfComplex, LocationSpatialDirectionality.Directional, LocationVisibility.Secret);
        }

        private static void Seed(LocationRuntime runtime, string locationId, string definitionId, string officialName, string commonName, IEnumerable<string> tags, string organizationId = null, string governmentId = null, LocationVisibility visibility = LocationVisibility.Public, string binding = null, string authoredPlaceDefinitionId = null)
        {
            runtime.CreateLocation(new LocationCreateRequest
            {
                transactionId = $"prototype.seed.{locationId}",
                locationId = locationId,
                locationDefinitionId = definitionId,
                authoredPlaceDefinitionId = authoredPlaceDefinitionId,
                officialName = officialName,
                commonName = commonName,
                semanticTagIds = tags,
                associatedOrganizationId = organizationId,
                associatedGovernmentId = governmentId,
                visibility = visibility,
                prototypeSceneBindingKey = binding,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.location.seed"
            });
        }

        private static void Link(LocationRuntime runtime, string parentId, string childId, string suffix, LocationContainmentKind kind = LocationContainmentKind.Primary, LocationVisibility visibility = LocationVisibility.Public)
        {
            runtime.AssignContainment(new LocationContainmentRequest
            {
                transactionId = $"prototype.seed.containment.{suffix}",
                linkId = $"location-containment.prototype.{suffix}",
                parentLocationId = parentId,
                childLocationId = childId,
                kind = kind,
                visibility = visibility,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.location.seed"
            });
        }

        private static void Relate(LocationRuntime runtime, string sourceId, string targetId, string suffix, LocationSpatialRelationshipKind kind, LocationSpatialDirectionality directionality, LocationVisibility visibility = LocationVisibility.Public)
        {
            runtime.CreateSpatialRelationship(new LocationSpatialRelationshipRequest
            {
                transactionId = $"prototype.seed.spatial.{suffix}",
                relationshipId = $"location-spatial.prototype.{suffix}",
                sourceLocationId = sourceId,
                targetLocationId = targetId,
                kind = kind,
                directionality = directionality,
                visibility = visibility,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.location.seed"
            });
        }

        private static void Add(
            ICollection<LocationDefinition> definitions,
            ISet<string> existingIds,
            string id,
            string displayName,
            LocationCategory category,
            bool secret = false,
            bool hidden = false,
            bool propertyAssociation = true,
            bool organizationAssociation = true,
            bool governmentAssociation = true,
            bool territoryAssociation = true,
            IEnumerable<string> tags = null)
        {
            if (existingIds.Contains(id))
            {
                return;
            }

            LocationDefinition definition = ScriptableObject.CreateInstance<LocationDefinition>();
            definition.name = displayName;
            definition.DevelopmentConfigure(id, displayName, category, secret, hidden, propertyAssociation, organizationAssociation, governmentAssociation, territoryAssociation, tags);
            definitions.Add(definition);
            existingIds.Add(id);
        }
    }
}
