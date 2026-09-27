using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.WorldLocations;

namespace UnityIsekaiGame.Tests
{
    public sealed class EntityLocationOccupancyTests
    {
        [Test]
        public void EntityPlacementHasOneActiveExactLocationAndDerivedRecursiveOccupancy()
        {
            Fixture fixture = CreateFixture();
            EntityLocationRuntime runtime = fixture.EntityLocations;
            EntityLocationReferenceData body = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.GuildMasterBodyId, fixture.WorldId);

            EntityLocationOperationResult duplicatePlace = runtime.Place(new EntityPlacementRequest
            {
                transactionId = "test.entity-location.conflict",
                entity = body,
                exactLocationId = "location.prototype.market-district",
                worldTime = 10d
            });

            LocationOccupancySnapshot direct = runtime.GetDirectOccupancy("location.prototype.adventurers-guild");
            LocationOccupancySnapshot recursive = runtime.GetRecursiveOccupancy("location.prototype.adventurers-guild");

            Assert.That(duplicatePlace.Status, Is.EqualTo(EntityLocationOperationStatus.ConflictingActivePlacement));
            Assert.That(runtime.ResolvePhysicalLocation(body).LocationId, Is.EqualTo("location.prototype.guildmaster-office"));
            Assert.That(direct.Placements.Any(item => item.EntityId == PrototypeEntityLocationFactory.GuildMasterBodyId), Is.False);
            Assert.That(recursive.Placements.Any(item => item.EntityId == PrototypeEntityLocationFactory.GuildMasterBodyId), Is.True);
            Assert.That(recursive.Placements.Select(item => item.EntityKey), Is.EqualTo(recursive.Placements.Select(item => item.EntityKey).OrderBy(id => id, StringComparer.Ordinal)));
        }

        [Test]
        public void PersonLocationResolvesThroughActiveBodyWithoutDuplicatePersonPlacement()
        {
            Fixture fixture = CreateFixture();
            EntityLocationRuntime runtime = fixture.EntityLocations;

            EntityLocationResolutionResult person = runtime.ResolvePhysicalLocation(PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PrisonerPersonId, fixture.WorldId));

            Assert.That(person.Status, Is.EqualTo(EntityPhysicalLocationResolutionStatus.ResolvedThroughBody));
            Assert.That(person.LocationId, Is.EqualTo("location.prototype.basement-prison"));
            Assert.That(runtime.TryGetActivePlacement(PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PrisonerPersonId, fixture.WorldId), out _), Is.False);
        }

        [Test]
        public void RelocationEndsPreviousPlacementAndPreservesHistoricalQueries()
        {
            Fixture fixture = CreateFixture();
            EntityLocationRuntime runtime = fixture.EntityLocations;
            EntityLocationReferenceData merchant = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.MerchantBodyId, fixture.WorldId);

            EntityLocationOperationResult move = runtime.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.entity-location.move",
                newPlacementId = "placement.test.merchant.civic",
                entity = merchant,
                expectedOriginLocationId = "location.prototype.merchant-counter",
                destinationLocationId = "location.prototype.civic-office",
                category = EntityPlacementCategory.Visiting,
                worldTime = 100d
            });

            Assert.That(move.Succeeded, Is.True, move.Message);
            Assert.That(runtime.ResolvePhysicalLocation(merchant).LocationId, Is.EqualTo("location.prototype.civic-office"));
            Assert.That(runtime.GetPlacementAtTime(merchant, 50d).ExactLocationId, Is.EqualTo("location.prototype.merchant-counter"));
            Assert.That(move.TransitionDiff.EnteredLocationIds, Does.Contain("location.prototype.civic-office"));
            Assert.That(move.TransitionDiff.ExitedLocationIds, Does.Contain("location.prototype.merchant-counter"));
        }

        [Test]
        public void UnplacementIsDistinctFromMissingAndPreservesLastKnownPlacement()
        {
            Fixture fixture = CreateFixture();
            EntityLocationRuntime runtime = fixture.EntityLocations;
            EntityLocationReferenceData arrow = PrototypeEntityLocationFactory.Item(PrototypeEntityLocationFactory.ArrowItemInstanceId, fixture.WorldId);

            EntityLocationOperationResult result = runtime.Unplace(new EntityUnplacementRequest { transactionId = "test.entity-location.unplace", entity = arrow, worldTime = 120d });
            EntityLocationResolutionResult active = runtime.ResolvePhysicalLocation(arrow);
            EntityPlacementSnapshot lastKnown = runtime.GetLastKnownPlacement(arrow);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(active.Status, Is.EqualTo(EntityPhysicalLocationResolutionStatus.Unplaced));
            Assert.That(lastKnown, Is.Not.Null);
            Assert.That(lastKnown.ExactLocationId, Is.EqualTo("location.prototype.market-district"));
            Assert.That(lastKnown.LifecycleState, Is.EqualTo(EntityPlacementLifecycleState.Ended));
        }

        [Test]
        public void CapacityLifecycleAndInventoryRulesRejectWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            EntityLocationRuntime runtime = fixture.EntityLocations;
            LocationOperationResult room = fixture.Locations.CreateLocation(new LocationCreateRequest
            {
                transactionId = "test.location.capacity-room",
                locationId = "location.test.capacity-room",
                locationDefinitionId = PrototypeLocationDefinitionFactory.RoomDefinitionId,
                officialName = "Capacity Room",
                commonName = "Capacity Room",
                semanticTagIds = new[] { "room", "interior" }
            });
            Assert.That(room.Succeeded, Is.True, room.Message);
            runtime.ConfigureCapacity(new EntityLocationCapacityRuleData { locationId = room.Snapshot.LocationId, maxDirectOccupants = 1, allowedEntityTypes = new[] { LocationOccupantEntityType.Body } });

            EntityLocationReferenceData body = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Body, entityId = "body.test.capacity", worldId = fixture.WorldId };
            EntityLocationReferenceData item = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.ItemInstance, entityId = "item-instance.test.capacity", worldId = fixture.WorldId };
            EntityLocationReferenceData held = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.ItemInstance, entityId = "item-instance.test.held", worldId = fixture.WorldId };
            runtime.RegisterKnownEntity(body);
            runtime.RegisterKnownEntity(item);
            runtime.RegisterKnownEntity(held);
            runtime.MarkInventoryHeld(held, true);

            EntityLocationOperationResult first = runtime.Place(new EntityPlacementRequest { transactionId = "test.entity-location.capacity.first", entity = body, exactLocationId = room.Snapshot.LocationId, worldTime = 1d });
            EntityLocationOperationResult wrongType = runtime.Place(new EntityPlacementRequest { transactionId = "test.entity-location.capacity.item", entity = item, exactLocationId = room.Snapshot.LocationId, worldTime = 2d });
            EntityLocationOperationResult inventory = runtime.Place(new EntityPlacementRequest { transactionId = "test.entity-location.inventory", entity = held, exactLocationId = room.Snapshot.LocationId, worldTime = 3d });
            fixture.Locations.TransitionLifecycle(new LocationLifecycleTransitionRequest { transactionId = "test.location.capacity.closed", locationId = room.Snapshot.LocationId, targetState = LocationLifecycleState.Closed, worldTime = 4d });
            EntityLocationReferenceData later = new EntityLocationReferenceData { entityType = LocationOccupantEntityType.Body, entityId = "body.test.closed", worldId = fixture.WorldId };
            runtime.RegisterKnownEntity(later);
            EntityLocationOperationResult closed = runtime.Place(new EntityPlacementRequest { transactionId = "test.entity-location.closed", entity = later, exactLocationId = room.Snapshot.LocationId, worldTime = 5d });

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(wrongType.Status, Is.EqualTo(EntityLocationOperationStatus.OccupantTypeNotAllowed));
            Assert.That(inventory.Status, Is.EqualTo(EntityLocationOperationStatus.InventoryConflict));
            Assert.That(closed.Status, Is.EqualTo(EntityLocationOperationStatus.InactiveLocation));
            Assert.That(runtime.GetDirectOccupancy(room.Snapshot.LocationId).Count, Is.EqualTo(1));
        }

        [Test]
        public void PersistenceRejectsDoubleActiveAndMissingLocationBeforeMutation()
        {
            Fixture fixture = CreateFixture();
            EntityLocationRuntime runtime = fixture.EntityLocations;
            EntityLocationRuntimeSaveData before = runtime.CreateSaveData();
            EntityLocationRuntimeSaveData corrupt = before.Clone();
            EntityPlacementRecordData duplicate = corrupt.placements.First(item => item.entity.entityId == PrototypeEntityLocationFactory.PlayerBodyId).Clone();
            duplicate.placementId = "placement.test.duplicate-active";
            duplicate.exactLocationId = "location.prototype.market-district";
            corrupt.placements.Add(duplicate);

            EntityLocationOperationResult rejected = runtime.RestoreFromSaveData(corrupt, fixture.Locations, fixture.WorldId);

            Assert.That(rejected.Status, Is.EqualTo(EntityLocationOperationStatus.PersistenceInvalid));
            Assert.That(runtime.CreateSaveData().placements.Select(item => item.placementId), Is.EqualTo(before.placements.Select(item => item.placementId)));

            corrupt = before.Clone();
            corrupt.placements[0].exactLocationId = "location.prototype.missing";
            rejected = runtime.RestoreFromSaveData(corrupt, fixture.Locations, fixture.WorldId);

            Assert.That(rejected.Status, Is.EqualTo(EntityLocationOperationStatus.PersistenceInvalid));
            Assert.That(runtime.CreateSaveData().placements.Select(item => item.exactLocationId), Is.EqualTo(before.placements.Select(item => item.exactLocationId)));
        }

        [Test]
        public void PersistenceParticipantRoundTripRestoresWithoutReplay()
        {
            Fixture fixture = CreateFixture();
            EntityLocationPersistenceParticipant participant = new EntityLocationPersistenceParticipant(fixture.EntityLocations, () => fixture.Locations, fixture.WorldId);

            PersistenceParticipantSaveResult capture = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(capture.PayloadJson, participant.ParticipantSchemaVersion);
            EntityLocationOperationResult move = fixture.EntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.entity-location.participant.move",
                newPlacementId = "placement.test.player.moved",
                entity = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId),
                destinationLocationId = "location.prototype.market-district",
                worldTime = 10d
            });
            PersistenceParticipantCommitResult commit = participant.CommitPreparedPayload(prepare.PreparedPayload);

            Assert.That(capture.Succeeded, Is.True, capture.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(move.Succeeded, Is.True, move.Message);
            Assert.That(commit.Succeeded, Is.True, commit.Message);
            Assert.That(fixture.EntityLocations.ResolvePhysicalLocation(PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId)).LocationId, Is.EqualTo("location.prototype.town"));
        }

        private static Fixture CreateFixture()
        {
            DefinitionRegistry registry = PrototypeLocationDefinitionFactory.AddMissingPrototypeLocationDefinitions(null);
            LocationRuntime locations = new LocationRuntime();
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            locations.Configure(registry, PersistenceService.LocalWorldId);
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            return new Fixture(registry, locations, entityLocations, PersistenceService.LocalWorldId);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, string worldId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public string WorldId { get; }
        }
    }
}
