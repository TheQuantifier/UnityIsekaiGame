using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Tests
{
    public sealed class RoutesDistanceTravelNetworksTests
    {
        [Test]
        public void PrototypeDefinitionsValidateAndSeedRouteNetworks()
        {
            Fixture fixture = CreateFixture();
            DefinitionValidationReport report = ValidateRouteDefinitions(fixture.Registry);

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.GetSummary());
            Assert.That(fixture.Registry.TryGet(PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId, out TravelModeDefinition walking), Is.True);
            Assert.That(fixture.Registry.TryGet(PrototypeLocationRouteDefinitionFactory.StreetSegmentDefinitionId, out RouteSegmentDefinition street), Is.True);
            Assert.That(walking.SupportsCategory(RouteSegmentCategory.Street), Is.True);
            Assert.That(street.SupportsTravelMode(PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId), Is.True);
            Assert.That(fixture.Routes.SegmentCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(fixture.Routes.NetworkCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(fixture.Routes.ValidateCurrent(out string failure), Is.True, failure);
            Assert.That(fixture.Routes.TryGetSegment(PrototypeLocationRouteDefinitionFactory.TownMarketStreetSegmentId, out LocationRouteSegmentSnapshot segment), Is.True);
            Assert.That(segment.NetworkIds, Does.Contain(PrototypeLocationRouteDefinitionFactory.TownStreetNetworkId));
        }

        [Test]
        public void RoutePlansComposeRouteSegmentsAndConnectionEdgesWithoutOwningConnections()
        {
            Fixture fixture = CreateFixture();

            LocationRouteSearchResult result = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.merchant-counter", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Plan.EdgeCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.Plan.Steps.Any(step => step.EdgeKind == RouteEdgeKind.RouteSegment), Is.True);
            Assert.That(result.Plan.Steps.Any(step => step.EdgeKind == RouteEdgeKind.LocalConnection), Is.True);
            Assert.That(fixture.Connections.ConnectionCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(fixture.Routes.Segments.All(segment => segment.SegmentId != PrototypeLocationConnectionDefinitionFactory.MarketMerchantCounterConnectionId), Is.True);
        }

        [Test]
        public void ObjectivesTieBreaksCyclesAndParallelEdgesAreDeterministic()
        {
            Fixture fixture = CreateFixture();
            LocationRouteMutationResult cheap = CreateRoute(fixture, "route-segment.test.long-cheap", "location.prototype.town", "location.prototype.market-district", 180d, 5d);
            LocationRouteMutationResult cycle = CreateRoute(fixture, "route-segment.test.market-cycle", "location.prototype.market-district", "location.prototype.town", 10d, 10d, directionality: LocationConnectionDirectionality.SourceToDestinationOnly);

            LocationRouteSearchResult shortest = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.market-district", RoutePlanningObjective.ShortestDistance, RouteAccessEvaluationMode.RequireCurrentAccess));
            LocationRouteSearchResult lowest = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.market-district", RoutePlanningObjective.LowestCost, RouteAccessEvaluationMode.RequireCurrentAccess));
            LocationRouteSearchResult lowestAgain = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.market-district", RoutePlanningObjective.LowestCost, RouteAccessEvaluationMode.RequireCurrentAccess));
            LocationRouteReachabilityResult reachable = fixture.Routes.GetReachableLocations(Request(fixture, "location.prototype.town", "location.prototype.basement-prison"));

            Assert.That(cheap.Succeeded, Is.True, cheap.Message);
            Assert.That(cycle.Succeeded, Is.True, cycle.Message);
            Assert.That(shortest.Plan.Steps[0].EdgeId, Is.EqualTo(PrototypeLocationRouteDefinitionFactory.TownMarketStreetSegmentId));
            Assert.That(lowest.Plan.Steps[0].EdgeId, Is.EqualTo(cheap.Segment.SegmentId));
            Assert.That(lowest.Plan.PlanId, Is.EqualTo(lowestAgain.Plan.PlanId));
            Assert.That(reachable.BudgetExceeded, Is.False);
            Assert.That(reachable.ReachableLocationIds, Does.Contain("location.prototype.market-district"));
        }

        [Test]
        public void CurrentAccessAndUnlockablePlanningConsumeConnectionAccessWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            LocationConnectionAccessContextData authorized = AccessContext(fixture, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" });

            LocationRouteSearchResult current = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.guildmaster-office", RoutePlanningObjective.ShortestDistance, RouteAccessEvaluationMode.RequireCurrentAccess, authorized));
            LocationRouteSearchResult unlockable = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.guildmaster-office", RoutePlanningObjective.ShortestDistance, RouteAccessEvaluationMode.PermitUnlockableConnections, authorized));

            Assert.That(current.Succeeded, Is.False);
            Assert.That(unlockable.Succeeded, Is.True, unlockable.Message);
            Assert.That(unlockable.Plan.Requirements.requiredActions.Any(action => action.StartsWith("open:", StringComparison.Ordinal)), Is.True);
            Assert.That(unlockable.Plan.Requirements.requiredActions.Any(action => action.StartsWith("unlock:", StringComparison.Ordinal)), Is.True);
            Assert.That(fixture.Connections.TryGetConnection(PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, out LocationConnectionSnapshot connection), Is.True);
            Assert.That(connection.OpenState, Is.EqualTo(LocationConnectionOpenState.Closed));
            Assert.That(connection.LockState, Is.EqualTo(LocationConnectionLockState.Locked));
        }

        [Test]
        public void KnowledgeSafePlanningFiltersHiddenEdgesWithoutLeakingCounts()
        {
            Fixture fixture = CreateFixture();
            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            LocationRouteSearchRequest hiddenRequest = Request(fixture, "location.prototype.guildmaster-office", "location.prototype.basement-prison", accessMode: RouteAccessEvaluationMode.IgnoreTravelerAccessDevelopment);
            hiddenRequest.includeHiddenDevelopmentRoutes = true;
            LocationRouteSearchRequest publicRequest = Request(fixture, "location.prototype.guildmaster-office", "location.prototype.basement-prison", accessMode: RouteAccessEvaluationMode.KnowledgeSafeCurrentAccess);
            publicRequest.knowledgeMode = RouteKnowledgeMode.PublicKnownOnly;
            LocationRouteSearchRequest knownRequest = Request(fixture, "location.prototype.guildmaster-office", "location.prototype.basement-prison", accessMode: RouteAccessEvaluationMode.KnowledgeSafeCurrentAccess);
            knownRequest.knowledgeMode = RouteKnowledgeMode.KnownToTraveler;
            knownRequest.knownEdgeIds = new[] { PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId };
            knownRequest.accessContext = AccessContext(fixture, actor, privileged: true);

            LocationRouteSearchResult hidden = fixture.Routes.PlanRoute(hiddenRequest);
            LocationRouteSearchResult filtered = fixture.Routes.PlanRoute(publicRequest);
            LocationRouteSearchResult known = fixture.Routes.PlanRoute(knownRequest);

            Assert.That(hidden.Succeeded, Is.True, hidden.Message);
            Assert.That(filtered.Succeeded, Is.False);
            Assert.That(filtered.Status, Is.EqualTo(RoutePlanningStatus.UnknownUnderKnowledgeView));
            Assert.That(filtered.Plan, Is.Null);
            Assert.That(known.Succeeded, Is.True, known.Message);
        }

        [Test]
        public void RoutePlansAreImmutableAndRevalidateAgainstRouteAndConnectionChanges()
        {
            Fixture fixture = CreateFixture();
            LocationRouteSearchResult planned = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));
            LocationRoutePlan plan = planned.Plan;
            string firstEdge = plan.Steps[0].EdgeId;

            LocationRouteMutationResult blocked = fixture.Routes.MutateSegment(new LocationRouteSegmentMutationRequest
            {
                transactionId = "test.route.block-market",
                segmentId = PrototypeLocationRouteDefinitionFactory.TownMarketStreetSegmentId,
                blockageState = RouteSegmentBlockageState.TemporarilyBlocked,
                worldTime = 40d
            });
            LocationRouteRevalidationResult revalidation = fixture.Routes.RevalidatePlan(plan, Request(fixture, "location.prototype.town", "location.prototype.market-district", accessMode: RouteAccessEvaluationMode.RequireCurrentAccess));

            Assert.That(planned.Succeeded, Is.True, planned.Message);
            Assert.That(blocked.Succeeded, Is.True, blocked.Message);
            Assert.That(plan.Steps[0].EdgeId, Is.EqualTo(firstEdge));
            Assert.That(revalidation.Status, Is.EqualTo(RoutePlanRevalidationStatus.ChangedAccess));
        }

        [Test]
        public void PersistenceRoundTripRejectsCorruptRouteGraphsWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            LocationRoutePersistenceParticipant participant = new LocationRoutePersistenceParticipant(fixture.Routes, () => fixture.Registry, () => fixture.Locations, () => fixture.Connections, fixture.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(save.PayloadJson, LocationRoutePersistenceParticipant.CurrentParticipantSchemaVersion);
            LocationRouteRuntime restored = new LocationRouteRuntime();
            restored.Configure(fixture.Registry, fixture.Locations, fixture.Connections, fixture.WorldId);
            LocationRouteMutationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<LocationRouteRuntimeSaveData>(save.PayloadJson), fixture.Locations, fixture.Connections, fixture.WorldId);
            LocationRouteRuntimeSaveData before = fixture.Routes.CreateSaveData();
            LocationRouteRuntimeSaveData corrupt = before.Clone();
            corrupt.segments[0].destinationLocationId = "location.prototype.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), LocationRoutePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.SegmentCount, Is.EqualTo(fixture.Routes.SegmentCount));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Routes.CreateSaveData().segments.Select(item => item.destinationLocationId), Is.EqualTo(before.segments.Select(item => item.destinationLocationId)));
        }

        [Test]
        public void InvalidDefinitionsModesEndpointsAndNetworksRejectBeforeMutation()
        {
            Fixture fixture = CreateFixture();
            long before = fixture.Routes.Revision;

            LocationRouteMutationResult missingDefinition = CreateRoute(fixture, "route-segment.test.missing-definition", "location.prototype.town", "location.prototype.market-district", 10d, 10d, definitionId: "route-segment-definition.missing");
            LocationRouteMutationResult missingLocation = CreateRoute(fixture, "route-segment.test.missing-location", "location.prototype.town", "location.prototype.missing", 10d, 10d);
            LocationRouteMutationResult badNetwork = fixture.Routes.CreateSegment(new LocationRouteSegmentCreateRequest
            {
                transactionId = "test.route.bad-network",
                segmentId = "route-segment.test.bad-network",
                segmentDefinitionId = PrototypeLocationRouteDefinitionFactory.StreetSegmentDefinitionId,
                sourceLocationId = "location.prototype.town",
                destinationLocationId = "location.prototype.market-district",
                distanceMeters = 10d,
                baseCostUnits = 10d,
                supportedTravelModeDefinitionIds = new[] { PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId },
                networkIds = new[] { "route-network.prototype.missing" }
            });

            Assert.That(missingDefinition.Succeeded, Is.False);
            Assert.That(missingLocation.Succeeded, Is.False);
            Assert.That(badNetwork.Succeeded, Is.False);
            Assert.That(fixture.Routes.Revision, Is.EqualTo(before));
        }

        private static LocationRouteSearchRequest Request(
            Fixture fixture,
            string origin,
            string destination,
            RoutePlanningObjective objective = RoutePlanningObjective.ShortestDistance,
            RouteAccessEvaluationMode accessMode = RouteAccessEvaluationMode.StructuralOnly,
            LocationConnectionAccessContextData accessContext = null)
        {
            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            return new LocationRouteSearchRequest
            {
                requestId = $"test.route.request.{origin}.{destination}.{objective}.{accessMode}",
                traveler = actor,
                originLocationId = origin,
                destinationLocationId = destination,
                travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                objective = objective,
                accessMode = accessMode,
                accessContext = accessContext ?? AccessContext(fixture, actor),
                worldTime = 20d
            };
        }

        private static LocationRouteMutationResult CreateRoute(Fixture fixture, string id, string source, string destination, double distance, double cost, string definitionId = PrototypeLocationRouteDefinitionFactory.StreetSegmentDefinitionId, LocationConnectionDirectionality directionality = LocationConnectionDirectionality.Bidirectional)
        {
            return fixture.Routes.CreateSegment(new LocationRouteSegmentCreateRequest
            {
                transactionId = $"test.route.create.{id}",
                segmentId = id,
                segmentDefinitionId = definitionId,
                displayName = id,
                sourceLocationId = source,
                destinationLocationId = destination,
                directionality = directionality,
                distanceMeters = distance,
                baseCostUnits = cost,
                supportedTravelModeDefinitionIds = new[] { PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId },
                visibility = RouteVisibility.Public,
                worldTime = 10d,
                sourceEventId = "event.test.route",
                provenanceId = "test.route"
            });
        }

        private static LocationConnectionAccessContextData AccessContext(
            Fixture fixture,
            EntityLocationReferenceData actor,
            bool privileged = false,
            string[] offices = null,
            string[] authorities = null)
        {
            return new LocationConnectionAccessContextData
            {
                actor = actor,
                personId = PrototypeEntityLocationFactory.PlayerPersonId,
                officeIds = offices ?? Array.Empty<string>(),
                authorityIds = authorities ?? Array.Empty<string>(),
                privileged = privileged
            };
        }

        private static DefinitionValidationReport ValidateRouteDefinitions(DefinitionRegistry registry)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in registry.DefinitionsById.Values.Where(item => item is RouteSegmentDefinition || item is TravelModeDefinition))
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            return report;
        }

        private static Fixture CreateFixture()
        {
            DefinitionRegistry registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(null);
            registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
            registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
            registry = PrototypeLocationRouteDefinitionFactory.AddMissingPrototypeRouteDefinitions(registry);
            LocationRuntime locations = new LocationRuntime();
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            locations.Configure(registry, PersistenceService.LocalWorldId);
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            InteractionPointRuntime interactions = new InteractionPointRuntime();
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactions, registry, locations, entityLocations, PersistenceService.LocalWorldId);
            LocationConnectionRuntime connections = new LocationConnectionRuntime();
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactions, PersistenceService.LocalWorldId);
            LocationRouteRuntime routes = new LocationRouteRuntime();
            PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(routes, registry, locations, connections, PersistenceService.LocalWorldId);
            return new Fixture(registry, locations, entityLocations, interactions, connections, routes, PersistenceService.LocalWorldId);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactions, LocationConnectionRuntime connections, LocationRouteRuntime routes, string worldId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                Interactions = interactions;
                Connections = connections;
                Routes = routes;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public InteractionPointRuntime Interactions { get; }
            public LocationConnectionRuntime Connections { get; }
            public LocationRouteRuntime Routes { get; }
            public string WorldId { get; }
        }
    }
}
