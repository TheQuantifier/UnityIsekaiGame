using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.Integration;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Tests
{
    public sealed class Step14WorldLocationTravelIntegrationFinalizationTests
    {
        [Test]
        public void OwnershipMapSeparatesAuthoritativeOwnersFromSceneAndExternalState()
        {
            Step14IntegrationAuthorityEntry[] authority = Step14WorldIntegrationValidator.AuthorityMap.ToArray();

            Assert.That(authority.Where(entry => entry.Authoritative).GroupBy(entry => entry.Domain).All(group => group.Count() == 1), Is.True);
            Assert.That(authority.Single(entry => entry.Domain == "entity placements").AuthoritativeRuntime, Is.EqualTo("EntityLocationRuntime"));
            Assert.That(authority.Single(entry => entry.Domain == "scene bindings").Derived, Is.True);
            Assert.That(authority.Single(entry => entry.Domain == "Unity transforms").External, Is.True);
            Assert.That(authority.Any(entry => entry.Domain == "visibility and redaction" && entry.External), Is.True);
        }

        [Test]
        public void CompleteSnapshotEvaluatesReadyAndProducesStableContract()
        {
            Step14IntegrationSnapshot snapshot = new Step14IntegrationSnapshot(CompleteSource(), CleanSceneBinding());

            Step14IntegrationValidationReport report = Step14WorldIntegrationValidator.Validate(snapshot);

            Assert.That(report.Succeeded, Is.True, string.Join(Environment.NewLine, report.Diagnostics.Select(item => item.ToString())));
            Assert.That(report.Readiness, Is.EqualTo(Step14IntegrationReadinessState.Ready));
            Assert.That(report.Step15Contract.Succeeded, Is.True);
            Assert.That(report.Step15Contract.QueryCapabilities, Does.Contain("plan-route"));
            Assert.That(report.Step15Contract.CommandCapabilities, Does.Contain("start-journey"));
            Assert.That(report.Step15Contract.DeferredBoundaries, Does.Contain("autonomous-npc-decision-making"));
        }

        [Test]
        public void ValidationRejectsWorldScopeDriftBeforeStep15ConsumersCanUseIt()
        {
            Step14PersistenceSnapshotSource source = CompleteSource();
            source.entityLocations.worldId = "world.other";

            Step14IntegrationValidationReport report = Step14WorldIntegrationValidator.Validate(new Step14IntegrationSnapshot(source, CleanSceneBinding()));

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Failures.Any(item => item.Domain == Step14IntegrationDiagnosticDomain.WorldScope), Is.True, string.Join(Environment.NewLine, report.Diagnostics.Select(item => item.ToString())));
        }

        [Test]
        public void HierarchyCyclesAndDuplicateActivePlacementsAreCaughtTogether()
        {
            Step14PersistenceSnapshotSource source = CompleteSource();
            source.locations.containmentLinks.Add(new LocationContainmentLinkData { linkId = "link.cycle-a", parentLocationId = "location.test.guild", childLocationId = "location.test.village", state = LocationLinkState.Active, kind = LocationContainmentKind.Primary });
            source.entityLocations.placements.Add(new EntityPlacementRecordData { placementId = "placement.test.player.duplicate", exactLocationId = "location.test.guild", worldId = World, entity = Body("body.test.player"), lifecycleState = EntityPlacementLifecycleState.Active });

            Step14IntegrationValidationReport report = Step14WorldIntegrationValidator.Validate(new Step14IntegrationSnapshot(source, CleanSceneBinding()));

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Failures.Any(item => item.Domain == Step14IntegrationDiagnosticDomain.Hierarchy), Is.True);
            Assert.That(report.Failures.Any(item => item.Domain == Step14IntegrationDiagnosticDomain.EntityPlacement), Is.True);
        }

        [Test]
        public void FingerprintIsDeterministicAcrossSaveRecordOrdering()
        {
            Step14PersistenceSnapshotSource first = CompleteSource();
            Step14PersistenceSnapshotSource second = CompleteSource();
            second.locations.records.Reverse();
            second.entityLocations.placements.Reverse();
            Array.Reverse(second.routes.segments);
            Array.Reverse(second.connections.connections);

            string firstFingerprint = Step14WorldIntegrationValidator.CreateCanonicalFingerprint(new Step14IntegrationSnapshot(first, CleanSceneBinding()));
            string secondFingerprint = Step14WorldIntegrationValidator.CreateCanonicalFingerprint(new Step14IntegrationSnapshot(second, CleanSceneBinding()));

            Assert.That(firstFingerprint, Is.EqualTo(secondFingerprint));
        }

        [Test]
        public void SceneBindingErrorsRemainNonAuthoritativeButBlockReadiness()
        {
            Step14IntegrationSnapshot snapshot = new Step14IntegrationSnapshot(CompleteSource(), new WorldSceneBindingValidationReport(
                new[]
                {
                    new WorldSceneBindingSnapshot("scene.instance.guild", World, "scene.prototype", "PrototypeScene", WorldSceneBindingCategory.Location, WorldSceneBindingRole.Primary, "location.test.missing", "binding.guild", "Missing Guild", WorldSceneBindingStatus.Bound, required: true, diagnostics: string.Empty)
                },
                Array.Empty<WorldSceneBindingIssue>()));

            Step14IntegrationValidationReport report = Step14WorldIntegrationValidator.Validate(snapshot);

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Failures.Any(item => item.Domain == Step14IntegrationDiagnosticDomain.SceneBinding), Is.True);
            Assert.That(report.AuthorityMap.Single(entry => entry.Domain == "scene bindings").Authoritative, Is.False);
        }

        private static readonly string World = PersistenceService.LocalWorldId;

        private static Step14PersistenceSnapshotSource CompleteSource()
        {
            return new Step14PersistenceSnapshotSource
            {
                worldId = World,
                saveSlotId = "slot.test.step14.integration",
                authoritativeWorldTime = 120d,
                locations = new LocationRuntimeSaveData
                {
                    worldId = World,
                    records =
                    {
                        new LocationRecordData { locationId = "location.test.village", locationDefinitionId = "location-definition.test.village", worldId = World, officialName = "Test Village", lifecycleState = LocationLifecycleState.Active },
                        new LocationRecordData { locationId = "location.test.guild", locationDefinitionId = "location-definition.test.guild", worldId = World, officialName = "Test Guild", lifecycleState = LocationLifecycleState.Active }
                    },
                    containmentLinks =
                    {
                        new LocationContainmentLinkData { linkId = "link.test.guild-parent", parentLocationId = "location.test.village", childLocationId = "location.test.guild", state = LocationLinkState.Active, kind = LocationContainmentKind.Primary }
                    },
                    spatialRelationships =
                    {
                        new LocationSpatialRelationshipData { relationshipId = "spatial.test.near", sourceLocationId = "location.test.village", targetLocationId = "location.test.guild", kind = LocationSpatialRelationshipKind.Near, state = LocationLinkState.Active }
                    }
                },
                entityLocations = new EntityLocationRuntimeSaveData
                {
                    worldId = World,
                    knownEntities =
                    {
                        Body("body.test.player")
                    },
                    placements =
                    {
                        new EntityPlacementRecordData { placementId = "placement.test.player", exactLocationId = "location.test.village", worldId = World, entity = Body("body.test.player"), lifecycleState = EntityPlacementLifecycleState.Active }
                    },
                    personBodyBindings =
                    {
                        new EntityPersonBodyBindingData { personId = "person.test.player", activeBodyId = "body.test.player" }
                    }
                },
                interactionPoints = new InteractionPointRuntimeSaveData
                {
                    worldId = World,
                    points =
                    {
                        new InteractionPointRecordData { interactionPointId = "interaction.test.guild-counter", interactionPointDefinitionId = "interaction-definition.test.counter", worldId = World, activeHostLocationId = "location.test.guild", lifecycleState = InteractionPointLifecycleState.Active }
                    }
                },
                connections = new LocationConnectionRuntimeSaveData
                {
                    worldId = World,
                    connections = new[]
                    {
                        new LocationConnectionRecordData { connectionId = "connection.test.village-guild", connectionDefinitionId = "connection-definition.test.path", worldId = World, sourceLocationId = "location.test.village", destinationLocationId = "location.test.guild", lifecycleState = LocationConnectionLifecycleState.Active }
                    },
                    endpoints = new[]
                    {
                        new LocationConnectionEndpointData { endpointId = "endpoint.test.village", connectionId = "connection.test.village-guild", locationId = "location.test.village" },
                        new LocationConnectionEndpointData { endpointId = "endpoint.test.guild", connectionId = "connection.test.village-guild", locationId = "location.test.guild", role = LocationConnectionEndpointRole.Destination }
                    }
                },
                routes = new LocationRouteRuntimeSaveData
                {
                    worldId = World,
                    segments = new[]
                    {
                        new LocationRouteSegmentRecordData { segmentId = "route.test.village-guild", segmentDefinitionId = "route-definition.test.path", worldId = World, sourceLocationId = "location.test.village", destinationLocationId = "location.test.guild", distanceMeters = 35d, baseCostUnits = 35d, lifecycleState = RouteSegmentLifecycleState.Active }
                    },
                    networks = new[]
                    {
                        new LocationRouteNetworkRecordData { networkId = "route-network.test.village", worldId = World, segmentIds = new[] { "route.test.village-guild" }, lifecycleState = RouteSegmentLifecycleState.Active }
                    }
                },
                journeys = new TravelJourneyRuntimeSaveData
                {
                    worldId = World,
                    journeys = new[]
                    {
                        new TravelJourneyRecordData { journeyId = "journey.test.guild", worldId = World, traveler = Body("body.test.player"), originLocationId = "location.test.village", destinationLocationId = "location.test.guild", lifecycleState = TravelJourneyLifecycleState.Ready }
                    },
                    steps = new[]
                    {
                        new TravelJourneyStepRecordData { journeyStepId = "journey-step.test.guild.1", journeyId = "journey.test.guild", sequenceIndex = 0, sourceLocationId = "location.test.village", destinationLocationId = "location.test.guild", edgeId = "route.test.village-guild", edgeKind = RouteEdgeKind.RouteSegment, distanceMeters = 35d, lifecycleState = TravelJourneyStepLifecycleState.Pending }
                    }
                },
                travelConditions = new TravelConditionRuntimeSaveData { worldId = World },
                politicalTravel = new PoliticalTravelRuntimeSaveData
                {
                    worldId = World,
                    checkpoints = new[]
                    {
                        new BorderCheckpointRecordData { checkpointId = "checkpoint.test.guild", worldId = World, locationId = "location.test.guild", routeSegmentId = "route.test.village-guild", lifecycleState = BorderCheckpointLifecycleState.Active }
                    }
                }
            };
        }

        private static WorldSceneBindingValidationReport CleanSceneBinding()
        {
            return new WorldSceneBindingValidationReport(
                new[]
                {
                    new WorldSceneBindingSnapshot("scene.instance.village", World, "scene.prototype", "PrototypeScene", WorldSceneBindingCategory.Location, WorldSceneBindingRole.Primary, "location.test.village", "binding.village", "Test Village", WorldSceneBindingStatus.Bound, required: true, diagnostics: string.Empty)
                },
                Array.Empty<WorldSceneBindingIssue>());
        }

        private static EntityLocationReferenceData Body(string bodyId)
        {
            return new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Body, entityId = bodyId, worldId = World };
        }
    }
}
