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
    public sealed class TravelPlanningJourneyStateTests
    {
        [Test]
        public void JourneyCreationUsesAcceptedRoutePlanWithoutTeleportingTraveler()
        {
            Fixture fixture = CreateFixture();
            LocationRouteSearchResult plan = fixture.Routes.PlanRoute(Request(fixture, "location.prototype.town", "location.prototype.market-district"));

            TravelJourneyOperationResult created = CreateJourney(fixture, "journey.test.accepted", "location.prototype.market-district", plan.Plan);

            Assert.That(plan.Succeeded, Is.True, plan.Message);
            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(created.Journey.LifecycleState, Is.EqualTo(TravelJourneyLifecycleState.Ready));
            Assert.That(created.Journey.Steps.Count, Is.EqualTo(plan.Plan.EdgeCount));
            Assert.That(fixture.EntityLocations.TryGetActivePlacement(PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId), out EntityPlacementSnapshot placement), Is.True);
            Assert.That(placement.ExactLocationId, Is.EqualTo("location.prototype.town"));
        }

        [Test]
        public void DeterministicWorldTimeProgressKeepsExactPlacementUntilSegmentCompletion()
        {
            Fixture fixture = CreateFixture();
            TravelJourneyOperationResult created = CreateJourney(fixture, "journey.test.progress", "location.prototype.market-district", rate: 5d);
            TravelJourneyOperationResult started = fixture.Journeys.StartJourney(Lifecycle(fixture, created.Journey.JourneyId, "start", 10d, 5d));
            TravelJourneyOperationResult partial = fixture.Journeys.AdvanceJourney(Lifecycle(fixture, created.Journey.JourneyId, "partial", 11d, 5d));
            TravelJourneyOperationResult duplicateBoundary = fixture.Journeys.AdvanceJourney(Lifecycle(fixture, created.Journey.JourneyId, "duplicate", 11d, 5d));
            TravelJourneyPhysicalContextResult context = fixture.Journeys.GetPhysicalContext(created.Journey.JourneyId, 11d);

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(started.Succeeded, Is.True, started.Message);
            Assert.That(partial.Succeeded, Is.True, partial.Message);
            Assert.That(partial.Journey.LifecycleState, Is.EqualTo(TravelJourneyLifecycleState.Active));
            Assert.That(partial.Journey.CompletedDistance.meters, Is.GreaterThan(0d));
            Assert.That(duplicateBoundary.Duplicate, Is.True);
            Assert.That(context.InTransit, Is.True);
            Assert.That(context.ExactPlacement.ExactLocationId, Is.EqualTo("location.prototype.town"));
            Assert.That(context.NextLocationId, Is.EqualTo("location.prototype.market-district"));
        }

        [Test]
        public void JourneyCompletesRouteSegmentsAndDelegatesLocalConnections()
        {
            Fixture fixture = CreateFixture();
            TravelJourneyOperationResult created = CreateJourney(fixture, "journey.test.connection", "location.prototype.merchant-counter", rate: 500d);
            fixture.Journeys.StartJourney(Lifecycle(fixture, created.Journey.JourneyId, "start", 10d, 500d));
            long connectionRevisionBefore = fixture.Connections.Revision;

            TravelJourneyOperationResult advanced = fixture.Journeys.AdvanceJourney(Lifecycle(fixture, created.Journey.JourneyId, "arrive", 11d, 500d));

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(advanced.Succeeded, Is.True, advanced.Message);
            Assert.That(advanced.Journey.LifecycleState, Is.EqualTo(TravelJourneyLifecycleState.Completed));
            Assert.That(advanced.Journey.Steps.Any(step => step.EdgeKind == RouteEdgeKind.LocalConnection && step.LifecycleState == TravelJourneyStepLifecycleState.Completed), Is.True);
            Assert.That(fixture.Connections.Revision, Is.GreaterThan(connectionRevisionBefore));
            Assert.That(fixture.EntityLocations.TryGetActivePlacement(PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId), out EntityPlacementSnapshot placement), Is.True);
            Assert.That(placement.ExactLocationId, Is.EqualTo("location.prototype.merchant-counter"));
        }

        [Test]
        public void BlockedRouteCanReplanFromCurrentExactPlacement()
        {
            Fixture fixture = CreateFixture();
            TravelJourneyOperationResult created = CreateJourney(fixture, "journey.test.replan", "location.prototype.market-district", rate: 5d);
            fixture.Journeys.StartJourney(Lifecycle(fixture, created.Journey.JourneyId, "start", 10d, 5d));
            LocationRouteMutationResult blockedSegment = fixture.Routes.MutateSegment(new LocationRouteSegmentMutationRequest
            {
                transactionId = "test.journey.block-segment",
                segmentId = PrototypeLocationRouteDefinitionFactory.TownMarketStreetSegmentId,
                blockageState = RouteSegmentBlockageState.TemporarilyBlocked,
                worldTime = 11d
            });
            TravelJourneyOperationResult blocked = fixture.Journeys.AdvanceJourney(Lifecycle(fixture, created.Journey.JourneyId, "blocked", 12d, 5d));
            LocationRouteMutationResult replacement = CreateRoute(fixture, "route-segment.test.journey-alternative", "location.prototype.town", "location.prototype.market-district", 95d, 30d);

            TravelJourneyOperationResult replanned = fixture.Journeys.ReplanJourney(new TravelJourneyReplanRequest
            {
                transactionId = "test.journey.replan",
                journeyId = created.Journey.JourneyId,
                destinationLocationId = "location.prototype.market-district",
                accessContext = AccessContext(fixture, PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId)),
                movementRateOverrideMetersPerSecond = 5d,
                worldTime = 13d
            });

            Assert.That(blockedSegment.Succeeded, Is.True, blockedSegment.Message);
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Status, Is.EqualTo(TravelJourneyMutationStatus.Blocked));
            Assert.That(replacement.Succeeded, Is.True, replacement.Message);
            Assert.That(replanned.Succeeded, Is.True, replanned.Message);
            Assert.That(replanned.Journey.LifecycleState, Is.EqualTo(TravelJourneyLifecycleState.Active));
            Assert.That(replanned.Journey.ReplanCount, Is.EqualTo(1));
        }

        [Test]
        public void PersistenceRoundTripRejectsCorruptJourneyGraphWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            TravelJourneyOperationResult created = CreateJourney(fixture, "journey.test.persistence", "location.prototype.market-district", rate: 5d);
            fixture.Journeys.StartJourney(Lifecycle(fixture, created.Journey.JourneyId, "start", 10d, 5d));
            fixture.Journeys.AdvanceJourney(Lifecycle(fixture, created.Journey.JourneyId, "partial", 11d, 5d));
            TravelJourneyPersistenceParticipant participant = new TravelJourneyPersistenceParticipant(fixture.Journeys, () => fixture.Registry, () => fixture.Locations, () => fixture.EntityLocations, () => fixture.Connections, () => fixture.Routes, fixture.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(save.PayloadJson, TravelJourneyPersistenceParticipant.CurrentParticipantSchemaVersion);
            TravelJourneyRuntime restored = new TravelJourneyRuntime();
            restored.Configure(fixture.Registry, fixture.Locations, fixture.EntityLocations, fixture.Connections, fixture.Routes, fixture.WorldId);
            TravelJourneyOperationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<TravelJourneyRuntimeSaveData>(save.PayloadJson), fixture.Registry, fixture.Locations, fixture.EntityLocations, fixture.Connections, fixture.Routes, fixture.WorldId);
            TravelJourneyRuntimeSaveData before = fixture.Journeys.CreateSaveData();
            TravelJourneyRuntimeSaveData corrupt = before.Clone();
            corrupt.journeys[0].destinationLocationId = "location.prototype.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), TravelJourneyPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepared.Succeeded, Is.True, prepared.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.JourneyCount, Is.EqualTo(fixture.Journeys.JourneyCount));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Journeys.CreateSaveData().journeys.Select(item => item.destinationLocationId), Is.EqualTo(before.journeys.Select(item => item.destinationLocationId)));
        }

        private static TravelJourneyOperationResult CreateJourney(Fixture fixture, string id, string destination, LocationRoutePlan acceptedPlan = null, double rate = -1d)
        {
            EntityLocationReferenceData traveler = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            return fixture.Journeys.CreateJourney(new TravelJourneyCreateRequest
            {
                transactionId = $"test.{id}.create",
                journeyId = id,
                traveler = traveler,
                controller = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId),
                originLocationId = "location.prototype.town",
                destinationLocationId = destination,
                acceptedRoutePlan = acceptedPlan,
                accessContext = AccessContext(fixture, traveler),
                movementRateOverrideMetersPerSecond = rate,
                worldTime = 10d,
                sourceEventId = "event.test.journey",
                provenanceId = "test.journey"
            });
        }

        private static TravelJourneyLifecycleRequest Lifecycle(Fixture fixture, string journeyId, string suffix, double worldTime, double rate)
        {
            EntityLocationReferenceData traveler = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            return new TravelJourneyLifecycleRequest
            {
                transactionId = $"test.journey.{suffix}",
                journeyId = journeyId,
                actor = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId),
                accessContext = AccessContext(fixture, traveler),
                movementRateOverrideMetersPerSecond = rate,
                worldTime = worldTime
            };
        }

        private static LocationRouteSearchRequest Request(Fixture fixture, string origin, string destination)
        {
            EntityLocationReferenceData traveler = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            return new LocationRouteSearchRequest
            {
                requestId = $"test.journey.route.{origin}.{destination}",
                traveler = traveler,
                originLocationId = origin,
                destinationLocationId = destination,
                travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                objective = RoutePlanningObjective.ShortestDistance,
                accessMode = RouteAccessEvaluationMode.RequireCurrentAccess,
                accessContext = AccessContext(fixture, traveler),
                worldTime = 20d
            };
        }

        private static LocationRouteMutationResult CreateRoute(Fixture fixture, string id, string source, string destination, double distance, double cost)
        {
            return fixture.Routes.CreateSegment(new LocationRouteSegmentCreateRequest
            {
                transactionId = $"test.route.create.{id}",
                segmentId = id,
                segmentDefinitionId = PrototypeLocationRouteDefinitionFactory.StreetSegmentDefinitionId,
                displayName = id,
                sourceLocationId = source,
                destinationLocationId = destination,
                directionality = LocationConnectionDirectionality.Bidirectional,
                distanceMeters = distance,
                baseCostUnits = cost,
                supportedTravelModeDefinitionIds = new[] { PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId },
                visibility = RouteVisibility.Public,
                worldTime = 10d,
                sourceEventId = "event.test.route",
                provenanceId = "test.route"
            });
        }

        private static LocationConnectionAccessContextData AccessContext(Fixture fixture, EntityLocationReferenceData actor)
        {
            return new LocationConnectionAccessContextData
            {
                actor = actor,
                personId = PrototypeEntityLocationFactory.PlayerPersonId
            };
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
            TravelJourneyRuntime journeys = new TravelJourneyRuntime();
            journeys.Configure(registry, locations, entityLocations, connections, routes, PersistenceService.LocalWorldId);
            return new Fixture(registry, locations, entityLocations, interactions, connections, routes, journeys, PersistenceService.LocalWorldId);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactions, LocationConnectionRuntime connections, LocationRouteRuntime routes, TravelJourneyRuntime journeys, string worldId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                Interactions = interactions;
                Connections = connections;
                Routes = routes;
                Journeys = journeys;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public InteractionPointRuntime Interactions { get; }
            public LocationConnectionRuntime Connections { get; }
            public LocationRouteRuntime Routes { get; }
            public TravelJourneyRuntime Journeys { get; }
            public string WorldId { get; }
        }
    }
}
