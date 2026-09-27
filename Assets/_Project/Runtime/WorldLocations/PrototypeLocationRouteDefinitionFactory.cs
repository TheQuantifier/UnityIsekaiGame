using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.WorldLocations
{
    public static class PrototypeLocationRouteDefinitionFactory
    {
        public const string WalkingModeDefinitionId = "travel-mode-definition.prototype.walking";
        public const string RunningModeDefinitionId = "travel-mode-definition.prototype.running";
        public const string CartModeDefinitionId = "travel-mode-definition.prototype.cart";

        public const string StreetSegmentDefinitionId = "route-segment-definition.prototype.street";
        public const string RoadSegmentDefinitionId = "route-segment-definition.prototype.road";
        public const string TrailSegmentDefinitionId = "route-segment-definition.prototype.trail";
        public const string CorridorSegmentDefinitionId = "route-segment-definition.prototype.corridor";
        public const string DungeonRouteSegmentDefinitionId = "route-segment-definition.prototype.dungeon-route";
        public const string BridgeSegmentDefinitionId = "route-segment-definition.prototype.bridge";

        public const string TownStreetNetworkId = "route-network.prototype.town-streets";
        public const string RegionalTrailNetworkId = "route-network.prototype.regional-trails";

        public const string TownMarketStreetSegmentId = "route-segment.prototype.town-market-street";
        public const string TownWildernessTrailSegmentId = "route-segment.prototype.town-wilderness-trail";
        public const string MarketGuildStreetSegmentId = "route-segment.prototype.market-guild-street";
        public const string TownCivicStreetSegmentId = "route-segment.prototype.town-civic-street";
        public const string TownMerchantGuildStreetSegmentId = "route-segment.prototype.town-merchant-guild-street";
        public const string TownGuardStreetSegmentId = "route-segment.prototype.town-guard-street";
        public const string TownCourtStreetSegmentId = "route-segment.prototype.town-court-street";
        public const string TownTempleStreetSegmentId = "route-segment.prototype.town-temple-street";
        public const string TownUniversityStreetSegmentId = "route-segment.prototype.town-university-street";
        public const string TownForgeStreetSegmentId = "route-segment.prototype.town-forge-street";

        public static DefinitionRegistry AddMissingPrototypeRouteDefinitions(DefinitionRegistry baseRegistry)
        {
            HashSet<string> ids = new HashSet<string>(baseRegistry?.DefinitionsById.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<IGameDefinition> definitions = new List<IGameDefinition>();
            if (baseRegistry != null)
            {
                definitions.AddRange(baseRegistry.DefinitionsById.Values.Where(definition => definition != null));
            }

            definitions.AddRange(CreateMissingTravelModeDefinitions(ids));
            definitions.AddRange(CreateMissingRouteSegmentDefinitions(ids));
            return new DefinitionRegistry(definitions);
        }

        public static IReadOnlyList<TravelModeDefinition> CreateMissingTravelModeDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<TravelModeDefinition> definitions = new List<TravelModeDefinition>();
            RouteSegmentCategory[] walkingCategories =
            {
                RouteSegmentCategory.Road,
                RouteSegmentCategory.Street,
                RouteSegmentCategory.Path,
                RouteSegmentCategory.Trail,
                RouteSegmentCategory.Corridor,
                RouteSegmentCategory.Bridge,
                RouteSegmentCategory.Tunnel,
                RouteSegmentCategory.StairRoute,
                RouteSegmentCategory.WildernessRoute,
                RouteSegmentCategory.DungeonRoute,
                RouteSegmentCategory.RegionalRoad,
                RouteSegmentCategory.TradeRoad,
                RouteSegmentCategory.MountainPass,
                RouteSegmentCategory.RiverCrossingPlaceholder,
                RouteSegmentCategory.FerryPlaceholder,
                RouteSegmentCategory.PortalRoutePlaceholder,
                RouteSegmentCategory.Custom
            };

            AddMode(definitions, ids, WalkingModeDefinitionId, "Prototype Walking", TravelModeCategory.Walking, walkingCategories, 1d, 1d, baseSpeed: 1.4d);
            AddMode(definitions, ids, RunningModeDefinitionId, "Prototype Running", TravelModeCategory.RunningPlaceholder, walkingCategories, 1d, 0.75d, capabilities: new[] { "capability.prototype.movement.run" }, baseSpeed: 2.8d);
            AddMode(definitions, ids, CartModeDefinitionId, "Prototype Cart Travel", TravelModeCategory.CartPlaceholder, new[] { RouteSegmentCategory.Road, RouteSegmentCategory.Street, RouteSegmentCategory.RegionalRoad, RouteSegmentCategory.TradeRoad, RouteSegmentCategory.Bridge }, 1d, 1.2d, equipment: new[] { "item.prototype-cart" }, baseSpeed: 2.2d);
            return definitions;
        }

        public static IReadOnlyList<RouteSegmentDefinition> CreateMissingRouteSegmentDefinitions(IEnumerable<string> existingIds)
        {
            HashSet<string> ids = new HashSet<string>(existingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<RouteSegmentDefinition> definitions = new List<RouteSegmentDefinition>();
            AddSegment(definitions, ids, StreetSegmentDefinitionId, "Prototype Street", RouteSegmentCategory.Street, 60d, 60d, new[] { WalkingModeDefinitionId, RunningModeDefinitionId, CartModeDefinitionId }, hidden: false);
            AddSegment(definitions, ids, RoadSegmentDefinitionId, "Prototype Road", RouteSegmentCategory.Road, 100d, 100d, new[] { WalkingModeDefinitionId, RunningModeDefinitionId, CartModeDefinitionId }, hidden: false);
            AddSegment(definitions, ids, TrailSegmentDefinitionId, "Prototype Trail", RouteSegmentCategory.Trail, 120d, 150d, new[] { WalkingModeDefinitionId, RunningModeDefinitionId });
            AddSegment(definitions, ids, CorridorSegmentDefinitionId, "Prototype Corridor Route", RouteSegmentCategory.Corridor, 8d, 8d, new[] { WalkingModeDefinitionId, RunningModeDefinitionId });
            AddSegment(definitions, ids, DungeonRouteSegmentDefinitionId, "Prototype Dungeon Route", RouteSegmentCategory.DungeonRoute, 20d, 30d, new[] { WalkingModeDefinitionId }, visibility: RouteVisibility.Secret);
            AddSegment(definitions, ids, BridgeSegmentDefinitionId, "Prototype Bridge Route", RouteSegmentCategory.Bridge, 20d, 20d, new[] { WalkingModeDefinitionId, RunningModeDefinitionId, CartModeDefinitionId }, hidden: false);
            return definitions;
        }

        public static void SeedPrototypeRoutes(LocationRouteRuntime runtime, DefinitionRegistry registry, LocationRuntime locations, LocationConnectionRuntime connections, string worldId)
        {
            if (runtime == null)
            {
                return;
            }

            string world = string.IsNullOrWhiteSpace(worldId) ? PersistenceService.LocalWorldId : worldId.Trim();
            runtime.Configure(registry, locations, connections, world);
            SeedSegment(runtime, TownMarketStreetSegmentId, StreetSegmentDefinitionId, "Town Market Street", "location.prototype.town", "location.prototype.market-district", 70d, 70d, RouteVisibility.Public);
            SeedSegment(runtime, MarketGuildStreetSegmentId, StreetSegmentDefinitionId, "Market to Guild Street", "location.prototype.market-district", "location.prototype.adventurers-guild", 85d, 85d, RouteVisibility.Public);
            SeedSegment(runtime, TownCivicStreetSegmentId, StreetSegmentDefinitionId, "Town Civic Street", "location.prototype.town", "location.prototype.civic-office", 55d, 55d, RouteVisibility.Public);
            SeedSegment(runtime, TownMerchantGuildStreetSegmentId, StreetSegmentDefinitionId, "Town Merchant Guild Street", "location.prototype.town", "location.prototype.merchant-guild", 80d, 80d, RouteVisibility.Public);
            SeedSegment(runtime, TownGuardStreetSegmentId, StreetSegmentDefinitionId, "Town Guard Street", "location.prototype.town", "location.prototype.guard-station", 65d, 65d, RouteVisibility.Public);
            SeedSegment(runtime, TownCourtStreetSegmentId, StreetSegmentDefinitionId, "Town Court Street", "location.prototype.town", "location.prototype.courthouse", 60d, 60d, RouteVisibility.Public);
            SeedSegment(runtime, TownTempleStreetSegmentId, StreetSegmentDefinitionId, "Town Temple Street", "location.prototype.town", "location.prototype.temple", 90d, 90d, RouteVisibility.Public);
            SeedSegment(runtime, TownUniversityStreetSegmentId, StreetSegmentDefinitionId, "Town University Street", "location.prototype.town", "location.prototype.university", 110d, 110d, RouteVisibility.Public);
            SeedSegment(runtime, TownForgeStreetSegmentId, StreetSegmentDefinitionId, "Town Forge Street", "location.prototype.town", "location.prototype.forge", 75d, 75d, RouteVisibility.Public);
            SeedSegment(runtime, TownWildernessTrailSegmentId, TrailSegmentDefinitionId, "Town Wilderness Trail", "location.prototype.town", "location.prototype.wilderness-ring", 140d, 170d, RouteVisibility.Public);
            runtime.CreateNetwork(new LocationRouteNetworkCreateRequest
            {
                transactionId = $"prototype.seed.{TownStreetNetworkId}",
                networkId = TownStreetNetworkId,
                displayName = "Prototype Town Street Network",
                category = RouteNetworkCategory.StreetNetwork,
                segmentIds = new[] { TownMarketStreetSegmentId, MarketGuildStreetSegmentId, TownCivicStreetSegmentId, TownMerchantGuildStreetSegmentId, TownGuardStreetSegmentId, TownCourtStreetSegmentId, TownTempleStreetSegmentId, TownUniversityStreetSegmentId, TownForgeStreetSegmentId },
                visibility = RouteVisibility.Public
            });
            runtime.CreateNetwork(new LocationRouteNetworkCreateRequest
            {
                transactionId = $"prototype.seed.{RegionalTrailNetworkId}",
                networkId = RegionalTrailNetworkId,
                displayName = "Prototype Regional Trail Network",
                category = RouteNetworkCategory.TrailNetwork,
                segmentIds = new[] { TownWildernessTrailSegmentId },
                visibility = RouteVisibility.LocallyKnown
            });
        }

        private static void SeedSegment(LocationRouteRuntime runtime, string id, string definitionId, string display, string source, string destination, double distance, double cost, RouteVisibility visibility)
        {
            runtime.CreateSegment(new LocationRouteSegmentCreateRequest
            {
                transactionId = $"prototype.seed.{id}",
                segmentId = id,
                segmentDefinitionId = definitionId,
                displayName = display,
                sourceLocationId = source,
                destinationLocationId = destination,
                directionality = LocationConnectionDirectionality.Bidirectional,
                distanceMeters = distance,
                baseCostUnits = cost,
                supportedTravelModeDefinitionIds = definitionId == TrailSegmentDefinitionId
                    ? new[] { WalkingModeDefinitionId, RunningModeDefinitionId }
                    : new[] { WalkingModeDefinitionId, RunningModeDefinitionId, CartModeDefinitionId },
                visibility = visibility,
                worldTime = 0d,
                sourceEventId = "event.prototype.world-setup",
                provenanceId = "prototype.location-route.seed"
            });
        }

        private static void AddMode(ICollection<TravelModeDefinition> definitions, ISet<string> ids, string id, string display, TravelModeCategory category, IEnumerable<RouteSegmentCategory> routeCategories, double distanceScale, double costScale, IEnumerable<string> capabilities = null, IEnumerable<string> equipment = null, double baseSpeed = 1.4d)
        {
            if (ids.Contains(id)) return;
            TravelModeDefinition definition = ScriptableObject.CreateInstance<TravelModeDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, routeCategories, distanceScale, costScale, capabilities, equipment, baseSpeed: baseSpeed);
            definitions.Add(definition);
            ids.Add(id);
        }

        private static void AddSegment(ICollection<RouteSegmentDefinition> definitions, ISet<string> ids, string id, string display, RouteSegmentCategory category, double distance, double cost, IEnumerable<string> modes, bool hidden = true, RouteVisibility visibility = RouteVisibility.Public)
        {
            if (ids.Contains(id)) return;
            RouteSegmentDefinition definition = ScriptableObject.CreateInstance<RouteSegmentDefinition>();
            definition.name = display;
            definition.DevelopmentConfigure(id, display, category, LocationConnectionDirectionality.Bidirectional, distance, cost, modes, accessPolicies: true, networkMembership: true, sceneBinding: true, hidden: hidden, visibility: visibility);
            definitions.Add(definition);
            ids.Add(id);
        }
    }
}
