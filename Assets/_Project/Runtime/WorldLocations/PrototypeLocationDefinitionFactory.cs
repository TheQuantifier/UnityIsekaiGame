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
        public const string RoomDefinitionId = "location-definition.room";
        public const string OfficeDefinitionId = "location-definition.office";
        public const string DetentionAreaDefinitionId = "location-definition.detention-area";
        public const string DungeonDefinitionId = "location-definition.dungeon";
        public const string WildernessDefinitionId = "location-definition.wilderness";
        public const string InteractionPointDefinitionId = "location-definition.interaction-point";

        public static readonly string[] PrototypeLocationIds =
        {
            "location.prototype.world",
            "location.prototype.region",
            "location.prototype.village",
            "location.prototype.market-district",
            "location.prototype.adventurers-guild",
            "location.prototype.civic-office",
            "location.prototype.merchant-counter",
            "location.prototype.guildmaster-office",
            "location.prototype.mayor-office",
            "location.prototype.basement-prison",
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
            Add(definitions, ids, RoomDefinitionId, "Room", LocationCategory.Room, tags: new[] { "room", "interior" });
            Add(definitions, ids, OfficeDefinitionId, "Office", LocationCategory.Room, organizationAssociation: true, governmentAssociation: true, tags: new[] { "office", "interior", "restricted" });
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
            Seed(runtime, "location.prototype.world", WorldDefinitionId, "Prototype World", "World", new[] { "world", "root" });
            Seed(runtime, "location.prototype.region", RegionDefinitionId, "Prototype Region", "Region", new[] { "region" });
            Seed(runtime, "location.prototype.village", SettlementDefinitionId, "Prototype Village", "Village", new[] { "settlement", "public" });
            Seed(runtime, "location.prototype.market-district", DistrictDefinitionId, "Prototype Market District", "Market District", new[] { "district", "public" });
            Seed(runtime, "location.prototype.adventurers-guild", GuildHallDefinitionId, "Prototype Adventurers Guild Hall", "Adventurers Guild", new[] { "guild", "building", "service" }, organizationId: "organization.prototype.guild", binding: "prototype.scene.adventurers-guild");
            Seed(runtime, "location.prototype.civic-office", GovernmentBuildingDefinitionId, "Prototype Civic Office", "Civic Office", new[] { "government", "building", "civic" }, organizationId: "organization.prototype.government", governmentId: "government.prototype.civic", binding: "prototype.scene.civic-office");
            Seed(runtime, "location.prototype.merchant-counter", MarketStallDefinitionId, "Prototype Merchant Counter", "Merchant Counter", new[] { "market", "commerce", "stall" }, organizationId: "organization.prototype.merchant-guild", binding: "prototype.scene.merchant-counter");
            Seed(runtime, "location.prototype.guildmaster-office", OfficeDefinitionId, "Prototype Guildmaster Office", "Guildmaster Office", new[] { "office", "interior", "restricted" }, organizationId: "organization.prototype.guild");
            Seed(runtime, "location.prototype.mayor-office", OfficeDefinitionId, "Prototype Mayor Office", "Mayor Office", new[] { "office", "interior", "restricted" }, organizationId: "organization.prototype.government", governmentId: "government.prototype.civic");
            Seed(runtime, "location.prototype.basement-prison", DetentionAreaDefinitionId, "Prototype Basement Prison", "Basement Prison", new[] { "detention", "justice", "restricted" }, governmentId: "government.prototype.civic", visibility: LocationVisibility.Restricted);
            Seed(runtime, "location.prototype.dungeon-entry", DungeonDefinitionId, "Prototype Dungeon Entry", "Dungeon Entry", new[] { "dungeon", "hazard", "interior" }, visibility: LocationVisibility.Secret, binding: "prototype.scene.dungeon-entry");
            Seed(runtime, "location.prototype.wilderness-ring", WildernessDefinitionId, "Prototype Wilderness Ring", "Wilderness Ring", new[] { "wilderness", "outdoor" });
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
            Link(runtime, "location.prototype.region", "location.prototype.village", "region-village");
            Link(runtime, "location.prototype.region", "location.prototype.wilderness-ring", "region-wilderness");
            Link(runtime, "location.prototype.village", "location.prototype.market-district", "village-market");
            Link(runtime, "location.prototype.village", "location.prototype.adventurers-guild", "village-guild");
            Link(runtime, "location.prototype.village", "location.prototype.civic-office", "village-civic");
            Link(runtime, "location.prototype.market-district", "location.prototype.merchant-counter", "market-counter", LocationContainmentKind.Site);
            Link(runtime, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", "guild-office", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.adventurers-guild", "location.prototype.basement-prison", "guild-prison", LocationContainmentKind.Interior);
            Link(runtime, "location.prototype.civic-office", "location.prototype.mayor-office", "civic-mayor", LocationContainmentKind.Interior);
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
            Relate(runtime, "location.prototype.basement-prison", "location.prototype.guildmaster-office", "prison-below-office", LocationSpatialRelationshipKind.Below, LocationSpatialDirectionality.Directional, LocationVisibility.Restricted);
            Relate(runtime, "location.prototype.dungeon-entry", "location.prototype.wilderness-ring", "dungeon-inside-wilderness", LocationSpatialRelationshipKind.PartOfComplex, LocationSpatialDirectionality.Directional, LocationVisibility.Secret);
        }

        private static void Seed(LocationRuntime runtime, string locationId, string definitionId, string officialName, string commonName, IEnumerable<string> tags, string organizationId = null, string governmentId = null, LocationVisibility visibility = LocationVisibility.Public, string binding = null)
        {
            runtime.CreateLocation(new LocationCreateRequest
            {
                transactionId = $"prototype.seed.{locationId}",
                locationId = locationId,
                locationDefinitionId = definitionId,
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
