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
    public sealed class TravelConditionsRestrictionsEncountersTests
    {
        [Test]
        public void MuddyRouteChangesPlanningCostWithoutMutatingRouteGraph()
        {
            Fixture fixture = Fixture.Create();
            long routeRevision = fixture.Routes.Revision;
            LocationRouteSearchResult before = fixture.Routes.PlanRoute(fixture.RouteRequest(TravelConditionEvaluationMode.CurrentConditions));
            TravelConditionOperationResult muddy = fixture.Conditions.CreateCondition(fixture.ConditionRequest("muddy", PrototypeTravelConditionDefinitionFactory.MuddyRoadConditionId));
            LocationRouteSearchResult after = fixture.Routes.PlanRoute(fixture.RouteRequest(TravelConditionEvaluationMode.CurrentConditions));

            Assert.That(muddy.Succeeded, Is.True, muddy.Message);
            Assert.That(before.Succeeded, Is.True, before.Message);
            Assert.That(after.Succeeded, Is.True, after.Message);
            Assert.That(after.Plan.TotalCost.units, Is.GreaterThan(before.Plan.TotalCost.units));
            Assert.That(after.Plan.TotalDistance.meters, Is.GreaterThan(before.Plan.TotalDistance.meters));
            Assert.That(fixture.Routes.Revision, Is.EqualTo(routeRevision));
        }

        [Test]
        public void HardBlockInvalidatesPreviouslyAcceptedRoutePlan()
        {
            Fixture fixture = Fixture.Create();
            LocationRouteSearchRequest request = fixture.RouteRequest(TravelConditionEvaluationMode.CurrentConditions);
            LocationRouteSearchResult before = fixture.Routes.PlanRoute(request);
            TravelConditionOperationResult block = fixture.Conditions.CreateCondition(fixture.ConditionRequest("block", PrototypeTravelConditionDefinitionFactory.CollapsedPassConditionId));
            LocationRouteSearchResult after = fixture.Routes.PlanRoute(request);
            LocationRouteRevalidationResult revalidate = fixture.Routes.RevalidatePlan(before.Plan, request);

            Assert.That(block.Succeeded, Is.True, block.Message);
            Assert.That(before.Succeeded, Is.True, before.Message);
            Assert.That(after.Succeeded, Is.False);
            Assert.That(revalidate.Valid, Is.False);
            Assert.That(revalidate.Status, Is.EqualTo(RoutePlanRevalidationStatus.ChangedAccess));
        }

        [Test]
        public void RequirementEvaluationIsReadOnlyAndDeterministic()
        {
            Fixture fixture = Fixture.Create(PrototypeLocationRouteDefinitionFactory.TownWildernessTrailSegmentId, "location.prototype.wilderness-ring");
            TravelConditionOperationResult condition = fixture.Conditions.CreateCondition(fixture.ConditionRequest("climb", PrototypeTravelConditionDefinitionFactory.ClimbingRequiredConditionId));
            long revision = fixture.Conditions.Revision;

            TravelConditionEvaluationResult missing = fixture.Conditions.Evaluate(fixture.EvaluateRequest(capabilities: Array.Empty<string>()));
            TravelConditionEvaluationResult allowed = fixture.Conditions.Evaluate(fixture.EvaluateRequest(capabilities: new[] { PrototypeTravelConditionDefinitionFactory.ClimbCapabilityId }));
            TravelConditionEvaluationResult again = fixture.Conditions.Evaluate(fixture.EvaluateRequest(capabilities: Array.Empty<string>()));

            Assert.That(condition.Succeeded, Is.True, condition.Message);
            Assert.That(missing.HardBlocked, Is.True);
            Assert.That(missing.MissingCapabilityIds, Does.Contain(PrototypeTravelConditionDefinitionFactory.ClimbCapabilityId));
            Assert.That(allowed.HardBlocked, Is.False);
            Assert.That(again.MissingCapabilityIds, Is.EqualTo(missing.MissingCapabilityIds));
            Assert.That(fixture.Conditions.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void HiddenTravelRiskDoesNotLeakCountsUntilKnown()
        {
            Fixture fixture = Fixture.Create(PrototypeLocationRouteDefinitionFactory.TownWildernessTrailSegmentId, "location.prototype.wilderness-ring");
            TravelConditionOperationResult condition = fixture.Conditions.CreateCondition(fixture.ConditionRequest("hidden", PrototypeTravelConditionDefinitionFactory.HiddenAmbushRiskConditionId));

            TravelConditionEvaluationResult safe = fixture.Conditions.Evaluate(fixture.EvaluateRequest(TravelConditionEvaluationMode.KnowledgeSafeCurrentConditions));
            TravelConditionEvaluationResult known = fixture.Conditions.Evaluate(fixture.EvaluateRequest(TravelConditionEvaluationMode.KnowledgeSafeCurrentConditions, knownConditionIds: new[] { condition.Condition.ConditionId }, knownEncounterIds: new[] { PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId }));

            Assert.That(safe.ApplicableConditions.Count, Is.Zero);
            Assert.That(safe.EncounterRisk.VisibleEncounterCount, Is.Zero);
            Assert.That(known.ApplicableConditions.Count, Is.EqualTo(1));
            Assert.That(known.EncounterRisk.HiddenKnownEncounterDefinitionIds, Does.Contain(PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId));
        }

        [Test]
        public void HazardAndEncounterRecordsPersistWithoutRetriggering()
        {
            Fixture fixture = Fixture.Create();
            TravelConditionOperationResult condition = fixture.Conditions.CreateCondition(fixture.ConditionRequest("hidden", PrototypeTravelConditionDefinitionFactory.HiddenAmbushRiskConditionId));
            TravelConditionOperationResult encounter = fixture.Conditions.TriggerEncounter(new TravelEncounterTriggerRequest
            {
                transactionId = "tx.encounter",
                encounterDefinitionId = PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId,
                sourceConditionId = condition.Condition.ConditionId,
                target = fixture.Target(),
                journeyId = "journey.test",
                traveler = fixture.Traveler,
                worldTime = 20d
            });
            TravelConditionOperationResult hazard = fixture.Conditions.TriggerHazard(new TravelHazardTriggerRequest
            {
                transactionId = "tx.hazard",
                hazardDefinitionId = PrototypeTravelConditionDefinitionFactory.HeatExposureHazardId,
                sourceConditionId = condition.Condition.ConditionId,
                target = fixture.Target(),
                traveler = fixture.Traveler,
                worldTime = 20d
            });

            TravelConditionRuntimeSaveData save = fixture.Conditions.CreateSaveData();
            TravelConditionRuntime restored = new TravelConditionRuntime();
            restored.Configure(fixture.Registry, fixture.Routes, fixture.Journeys, fixture.WorldId);
            TravelConditionOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Routes, fixture.Journeys, fixture.WorldId);
            TravelConditionOperationResult duplicate = restored.TriggerEncounter(new TravelEncounterTriggerRequest
            {
                transactionId = "tx.encounter",
                encounterDefinitionId = PrototypeTravelConditionDefinitionFactory.HiddenAmbushEncounterId,
                sourceConditionId = condition.Condition.ConditionId,
                target = fixture.Target(),
                journeyId = "journey.test",
                traveler = fixture.Traveler,
                worldTime = 21d
            });

            Assert.That(encounter.Succeeded, Is.True, encounter.Message);
            Assert.That(hazard.Succeeded, Is.True, hazard.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.ConditionCount, Is.EqualTo(1));
            Assert.That(restored.EncounterCount, Is.EqualTo(1));
            Assert.That(restored.HazardExposureCount, Is.EqualTo(1));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(restored.EncounterCount, Is.EqualTo(1));
        }

        [Test]
        public void PersistenceRejectsCorruptPayloadWithoutPartialMutation()
        {
            Fixture fixture = Fixture.Create();
            TravelConditionOperationResult condition = fixture.Conditions.CreateCondition(fixture.ConditionRequest("persist", PrototypeTravelConditionDefinitionFactory.MuddyRoadConditionId));
            TravelConditionRuntimeSaveData before = fixture.Conditions.CreateSaveData();
            TravelConditionPersistenceParticipant participant = new TravelConditionPersistenceParticipant(fixture.Conditions, () => fixture.Registry, () => fixture.Routes, () => fixture.Journeys, fixture.WorldId);
            TravelConditionRuntimeSaveData corrupt = before.Clone();
            corrupt.conditions[0].conditionDefinitionId = "travel-condition-definition.missing";

            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(corrupt), TravelConditionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(condition.Succeeded, Is.True, condition.Message);
            Assert.That(prepared.Succeeded, Is.False);
            Assert.That(fixture.Conditions.CreateSaveData().conditions.Select(item => item.conditionDefinitionId), Is.EqualTo(before.conditions.Select(item => item.conditionDefinitionId)));
            Assert.That(fixture.Conditions.Revision, Is.EqualTo(before.revision));
        }

        private sealed class Fixture
        {
            private readonly string segmentId;
            private readonly string destinationId;

            private Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, LocationConnectionRuntime connections, LocationRouteRuntime routes, TravelJourneyRuntime journeys, TravelConditionRuntime conditions, string worldId, string segmentId, string destinationId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                Connections = connections;
                Routes = routes;
                Journeys = journeys;
                Conditions = conditions;
                WorldId = worldId;
                this.segmentId = segmentId;
                this.destinationId = destinationId;
                Traveler = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, worldId);
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public LocationConnectionRuntime Connections { get; }
            public LocationRouteRuntime Routes { get; }
            public TravelJourneyRuntime Journeys { get; }
            public TravelConditionRuntime Conditions { get; }
            public string WorldId { get; }
            public EntityLocationReferenceData Traveler { get; }

            public static Fixture Create(string segmentId = PrototypeLocationRouteDefinitionFactory.TownMarketStreetSegmentId, string destinationId = "location.prototype.market-district")
            {
                string worldId = PersistenceService.LocalWorldId;
                DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
                registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(registry);
                registry = PrototypeInteractionPointDefinitionFactory.AddMissingPrototypeInteractionDefinitions(registry);
                registry = PrototypeLocationConnectionDefinitionFactory.AddMissingPrototypeConnectionDefinitions(registry);
                registry = PrototypeLocationRouteDefinitionFactory.AddMissingPrototypeRouteDefinitions(registry);
                registry = PrototypeTravelConditionDefinitionFactory.AddMissingPrototypeTravelConditionDefinitions(registry);
                LocationRuntime locations = new LocationRuntime();
                PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, worldId);
                locations.Configure(registry, worldId);
                EntityLocationRuntime entityLocations = new EntityLocationRuntime();
                PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, worldId);
                InteractionPointRuntime interactions = new InteractionPointRuntime();
                PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactions, registry, locations, entityLocations, worldId);
                LocationConnectionRuntime connections = new LocationConnectionRuntime();
                PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactions, worldId);
                LocationRouteRuntime routes = new LocationRouteRuntime();
                PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(routes, registry, locations, connections, worldId);
                TravelJourneyRuntime journeys = new TravelJourneyRuntime();
                TravelConditionRuntime conditions = new TravelConditionRuntime();
                conditions.Configure(registry, routes, journeys, worldId);
                routes.Configure(registry, locations, connections, worldId, conditions);
                journeys.Configure(registry, locations, entityLocations, connections, routes, worldId, conditions);
                return new Fixture(registry, locations, entityLocations, connections, routes, journeys, conditions, worldId, segmentId, destinationId);
            }

            public TravelConditionCreateRequest ConditionRequest(string suffix, string definitionId)
            {
                return new TravelConditionCreateRequest
                {
                    transactionId = $"tx.condition.{suffix}",
                    conditionId = $"travel-condition.test.{suffix}",
                    conditionDefinitionId = definitionId,
                    target = Target(),
                    lifecycleState = TravelConditionLifecycleState.Active,
                    startsWorldTime = 0d,
                    provenanceId = "editmode.feature.14.8"
                };
            }

            public TravelConditionEvaluationRequest EvaluateRequest(TravelConditionEvaluationMode mode = TravelConditionEvaluationMode.CurrentConditions, string[] capabilities = null, string[] equipment = null, string[] knownConditionIds = null, string[] knownEncounterIds = null)
            {
                return new TravelConditionEvaluationRequest
                {
                    evaluationMode = mode,
                    target = Target(),
                    traveler = Traveler,
                    travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                    travelerCapabilityIds = capabilities ?? Array.Empty<string>(),
                    travelerEquipmentDefinitionIds = equipment ?? Array.Empty<string>(),
                    knownConditionIds = knownConditionIds ?? Array.Empty<string>(),
                    knownEncounterIds = knownEncounterIds ?? Array.Empty<string>(),
                    worldTime = 10d
                };
            }

            public LocationRouteSearchRequest RouteRequest(TravelConditionEvaluationMode mode)
            {
                return new LocationRouteSearchRequest
                {
                    traveler = Traveler,
                    originLocationId = "location.prototype.town",
                    destinationLocationId = destinationId,
                    travelModeDefinitionId = PrototypeLocationRouteDefinitionFactory.WalkingModeDefinitionId,
                    accessMode = RouteAccessEvaluationMode.RequireCurrentAccess,
                    conditionEvaluationMode = mode,
                    accessContext = new LocationConnectionAccessContextData { actor = Traveler },
                    worldTime = 10d
                };
            }

            public TravelConditionTargetReferenceData Target()
            {
                return new TravelConditionTargetReferenceData
                {
                    scope = TravelConditionTargetScope.RouteSegment,
                    targetId = segmentId,
                    sourceLocationId = "location.prototype.town",
                    destinationLocationId = destinationId,
                    edgeKind = RouteEdgeKind.RouteSegment,
                    traveler = Traveler
                };
            }
        }
    }
}
