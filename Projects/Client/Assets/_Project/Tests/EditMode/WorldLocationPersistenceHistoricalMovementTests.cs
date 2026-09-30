using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Tests
{
    public sealed class WorldLocationPersistenceHistoricalMovementTests
    {
        [Test]
        public void ManifestDeclaresSingleOwnersAndRejectsWorldMismatch()
        {
            Step14PersistenceSnapshotSource source = Sample();
            Step14PersistenceManifest manifest = Step14PersistenceManifestBuilder.Build(source);
            Step14PersistenceSnapshotSource corrupt = source.Clone();
            corrupt.entityLocations.worldId = "world.other";

            Step14PersistenceValidationReport rejected = Step14PersistenceManifestBuilder.Validate(corrupt);

            Assert.That(manifest.Succeeded, Is.True, manifest.ValidationReport.Summary);
            Assert.That(manifest.Ownership.Where(item => item.OwnerKind == Step14PersistenceOwnerKind.Authoritative).GroupBy(item => item.Category).Any(group => group.Count() > 1), Is.False);
            Assert.That(manifest.Ownership.Any(item => item.Category == "movement historical projections" && item.OwnerKind == Step14PersistenceOwnerKind.Derived), Is.True);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Errors.Any(issue => issue.Message.Contains("world.other")), Is.True);
        }

        [Test]
        public void ExactLocationAtTimeSeparatesPlacementFromInTransitState()
        {
            MovementHistoryService service = new MovementHistoryService(Sample());
            EntityLocationReferenceData traveler = Person();

            HistoricalExactLocationResult beforeJourney = service.ResolveExactLocationAt(traveler, 5d);
            HistoricalExactLocationResult inTransit = service.ResolveExactLocationAt(traveler, 15d);
            HistoricalExactLocationResult afterArrival = service.ResolveExactLocationAt(traveler, 30d);

            Assert.That(beforeJourney.Status, Is.EqualTo(HistoricalExactLocationStatus.ExactLocationFound));
            Assert.That(beforeJourney.ExactLocationId, Is.EqualTo("location.test.room-a"));
            Assert.That(inTransit.Status, Is.EqualTo(HistoricalExactLocationStatus.InTransit));
            Assert.That(inTransit.InTransit.PreviousLocationId, Is.EqualTo("location.test.room-a"));
            Assert.That(inTransit.InTransit.NextLocationId, Is.EqualTo("location.test.room-b"));
            Assert.That(afterArrival.Status, Is.EqualTo(HistoricalExactLocationStatus.ExactLocationFound));
            Assert.That(afterArrival.ExactLocationId, Is.EqualTo("location.test.room-b"));
        }

        [Test]
        public void HistoricalPathUsesContainmentAtRequestedTime()
        {
            MovementHistoryService service = new MovementHistoryService(Sample());

            HistoricalLocationPathResult oldPath = service.ResolveHistoricalLocationPath("location.test.room-a", 5d);
            HistoricalLocationPathResult newPath = service.ResolveHistoricalLocationPath("location.test.room-a", 25d);

            Assert.That(oldPath.LocationPathIds, Is.EqualTo(new[] { "location.test.room-a", "location.test.building-old", "location.test.village", "location.test.world" }));
            Assert.That(newPath.LocationPathIds, Is.EqualTo(new[] { "location.test.room-a", "location.test.building-new", "location.test.village", "location.test.world" }));
        }

        [Test]
        public void TimelineOrderingAndSourceReferencesAreDeterministic()
        {
            MovementHistoryService service = new MovementHistoryService(Sample());
            MovementTimelineResult first = service.BuildTimeline(new MovementHistoryQuery { entity = Person(), startWorldTime = 0d, endWorldTime = 50d, limit = 128 });
            MovementTimelineResult second = service.BuildTimeline(new MovementHistoryQuery { entity = Person(), startWorldTime = 0d, endWorldTime = 50d, limit = 128 });

            Assert.That(first.Entries.Select(item => item.SourceRecordId), Is.EqualTo(second.Entries.Select(item => item.SourceRecordId)));
            Assert.That(first.Entries.Select(item => item.Kind), Does.Contain(MovementTimelineEntryKind.JourneyStarted));
            Assert.That(first.Entries.Select(item => item.Kind), Does.Contain(MovementTimelineEntryKind.TerritoryCrossed));
            Assert.That(first.Entries.All(item => !string.IsNullOrWhiteSpace(item.SourceParticipantId) && !string.IsNullOrWhiteSpace(item.SourceRecordId)), Is.True);
        }

        [Test]
        public void OccupancyVisitsDistanceAndVisibilityDoNotLeakHiddenCounts()
        {
            Step14PersistenceSnapshotSource source = Sample();
            source.entityLocations.placements.Add(new EntityPlacementRecordData
            {
                placementId = "placement.test.secret",
                entity = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Person, entityId = "person.secret", worldId = World },
                exactLocationId = "location.test.room-b",
                worldId = World,
                startWorldTime = 1d,
                visibility = LocationVisibility.Hidden
            });
            MovementHistoryService service = new MovementHistoryService(source);

            HistoricalOccupancyResult publicOccupancy = service.GetHistoricalOccupancy("location.test.building-new", 30d, recursive: true, visibilityMode: MovementHistoryVisibilityMode.Public);
            HistoricalOccupancyResult developmentOccupancy = service.GetHistoricalOccupancy("location.test.building-new", 30d, recursive: true);
            VisitedLocationSummary visits = service.GetVisitSummary(Person(), "location.test.village", 0d, 40d, exactOnly: false);
            MovementDistanceSummary distance = service.GetMovementDistance(Person(), 0d, 40d);

            Assert.That(publicOccupancy.Count, Is.EqualTo(1));
            Assert.That(developmentOccupancy.Count, Is.EqualTo(2));
            Assert.That(visits.HasVisited, Is.True);
            Assert.That(visits.VisitCount, Is.EqualTo(2));
            Assert.That(distance.TotalCompletedDistanceMeters, Is.EqualTo(50d));
        }

        [Test]
        public void SaveDataClonesKeepProjectionSnapshotsImmutable()
        {
            Step14PersistenceSnapshotSource source = Sample();
            MovementHistoryService service = new MovementHistoryService(source);
            MovementTimelineResult before = service.BuildTimeline(new MovementHistoryQuery { entity = Person(), startWorldTime = 0d, endWorldTime = 40d });
            source.entityLocations.placements.Clear();
            source.journeys.journeys = Array.Empty<TravelJourneyRecordData>();

            MovementTimelineResult afterExternalMutation = service.BuildTimeline(new MovementHistoryQuery { entity = Person(), startWorldTime = 0d, endWorldTime = 40d });

            Assert.That(afterExternalMutation.Entries.Select(item => item.SourceRecordId), Is.EqualTo(before.Entries.Select(item => item.SourceRecordId)));
        }

        private static readonly string World = PersistenceService.LocalWorldId;

        private static EntityLocationReferenceData Person()
        {
            return new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Person, entityId = "person.test.traveler", worldId = World };
        }

        private static Step14PersistenceSnapshotSource Sample()
        {
            EntityLocationReferenceData traveler = Person();
            return new Step14PersistenceSnapshotSource
            {
                worldId = World,
                saveSlotId = "slot.test",
                authoritativeWorldTime = 31d,
                locations = new LocationRuntimeSaveData
                {
                    worldId = World,
                    revision = 10,
                    records =
                    {
                        L("location.test.world", LocationLifecycleState.Active, 0d),
                        L("location.test.village", LocationLifecycleState.Active, 0d),
                        L("location.test.building-old", LocationLifecycleState.Destroyed, 0d, 20d),
                        L("location.test.building-new", LocationLifecycleState.Active, 20d),
                        L("location.test.room-a", LocationLifecycleState.Active, 0d),
                        L("location.test.room-b", LocationLifecycleState.Active, 0d)
                    },
                    containmentLinks =
                    {
                        Link("link.world.village", "location.test.world", "location.test.village", 0d),
                        Link("link.village.old", "location.test.village", "location.test.building-old", 0d, 20d),
                        Link("link.village.new", "location.test.village", "location.test.building-new", 20d),
                        Link("link.old.room-a", "location.test.building-old", "location.test.room-a", 0d, 20d),
                        Link("link.new.room-a", "location.test.building-new", "location.test.room-a", 20d),
                        Link("link.new.room-b", "location.test.building-new", "location.test.room-b", 0d)
                    }
                },
                entityLocations = new EntityLocationRuntimeSaveData
                {
                    worldId = World,
                    revision = 4,
                    placements =
                    {
                        Placement("placement.test.a", traveler, "location.test.room-a", 0d, 10d),
                        Placement("placement.test.b", traveler, "location.test.room-b", 20d)
                    }
                },
                interactionPoints = new InteractionPointRuntimeSaveData { worldId = World, revision = 1 },
                connections = new LocationConnectionRuntimeSaveData { worldId = World, revision = 1 },
                routes = new LocationRouteRuntimeSaveData
                {
                    worldId = World,
                    revision = 2,
                    segments = new[]
                    {
                        new LocationRouteSegmentRecordData
                        {
                            segmentId = "route.test.a-b",
                            worldId = World,
                            sourceLocationId = "location.test.room-a",
                            destinationLocationId = "location.test.room-b",
                            distanceMeters = 50d,
                            createdWorldTime = 0d
                        }
                    }
                },
                journeys = new TravelJourneyRuntimeSaveData
                {
                    worldId = World,
                    revision = 3,
                    journeys = new[]
                    {
                        new TravelJourneyRecordData
                        {
                            journeyId = "journey.test.a-b",
                            worldId = World,
                            traveler = traveler,
                            originLocationId = "location.test.room-a",
                            destinationLocationId = "location.test.room-b",
                            lifecycleState = TravelJourneyLifecycleState.Completed,
                            currentStepIndex = 0,
                            completedDistanceMillimeters = 50000,
                            totalDistanceMillimeters = 50000,
                            createdWorldTime = 9d,
                            startedWorldTime = 10d,
                            lastProgressWorldTime = 15d,
                            endedWorldTime = 20d
                        }
                    },
                    steps = new[]
                    {
                        new TravelJourneyStepRecordData
                        {
                            journeyStepId = "journey.test.a-b.step.0000",
                            journeyId = "journey.test.a-b",
                            sequenceIndex = 0,
                            sourceLocationId = "location.test.room-a",
                            destinationLocationId = "location.test.room-b",
                            edgeId = "route.test.a-b",
                            edgeKind = RouteEdgeKind.RouteSegment,
                            distanceMeters = 50d,
                            startedWorldTime = 10d,
                            completedWorldTime = 20d,
                            completedDistanceMillimeters = 50000,
                            lifecycleState = TravelJourneyStepLifecycleState.Completed
                        }
                    }
                },
                travelConditions = new TravelConditionRuntimeSaveData
                {
                    worldId = World,
                    revision = 2,
                    conditions = new[]
                    {
                        new TravelConditionRecordData
                        {
                            conditionId = "condition.test.mud",
                            worldId = World,
                            target = new TravelConditionTargetReferenceData { scope = TravelConditionTargetScope.RouteSegment, targetId = "route.test.a-b" },
                            startsWorldTime = 8d,
                            endsWorldTime = 21d
                        }
                    },
                    encounters = new[]
                    {
                        new TravelEncounterRecordData
                        {
                            encounterId = "encounter.test.bridge",
                            worldId = World,
                            journeyId = "journey.test.a-b",
                            traveler = traveler,
                            createdWorldTime = 12d,
                            triggeredWorldTime = 12d,
                            resolvedWorldTime = 13d,
                            lifecycleState = TravelEncounterLifecycleState.Resolved
                        }
                    }
                },
                politicalTravel = new PoliticalTravelRuntimeSaveData
                {
                    worldId = World,
                    revision = 2,
                    crossings = new[]
                    {
                        new PoliticalTravelCrossingRecordData
                        {
                            crossingId = "crossing.test.border",
                            worldId = World,
                            travelerPersonId = traveler.entityId,
                            originLocationId = "location.test.room-a",
                            destinationLocationId = "location.test.room-b",
                            routeSegmentId = "route.test.a-b",
                            sourceTerritoryId = "territory.test.old",
                            destinationTerritoryId = "territory.test.new",
                            sourceJurisdictionId = "jurisdiction.test.old",
                            destinationJurisdictionId = "jurisdiction.test.new",
                            checkpointId = "checkpoint.test.bridge",
                            legalState = PoliticalTravelLegalState.Authorized,
                            worldTime = 18d
                        }
                    }
                }
            };
        }

        private static LocationRecordData L(string id, LocationLifecycleState state, double created, double ended = -1d)
        {
            return new LocationRecordData { locationId = id, locationDefinitionId = "location-definition.test", worldId = World, officialName = id, lifecycleState = state, createdWorldTime = created, endedWorldTime = ended };
        }

        private static LocationContainmentLinkData Link(string id, string parent, string child, double start, double end = -1d)
        {
            return new LocationContainmentLinkData { linkId = id, parentLocationId = parent, childLocationId = child, kind = LocationContainmentKind.Primary, state = LocationLinkState.Active, effectiveStartWorldTime = start, effectiveEndWorldTime = end };
        }

        private static EntityPlacementRecordData Placement(string id, EntityLocationReferenceData entity, string locationId, double start, double end = -1d)
        {
            return new EntityPlacementRecordData { placementId = id, entity = entity.Clone(), exactLocationId = locationId, worldId = World, startWorldTime = start, endWorldTime = end, lifecycleState = end < 0d ? EntityPlacementLifecycleState.Active : EntityPlacementLifecycleState.Ended };
        }
    }
}
