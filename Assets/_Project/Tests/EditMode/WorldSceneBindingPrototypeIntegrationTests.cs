using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.WorldLocations;
using UnityIsekaiGame.WorldLocations.SceneBinding;

namespace UnityIsekaiGame.Tests
{
    public sealed class WorldSceneBindingPrototypeIntegrationTests
    {
        [SetUp]
        public void SetUp()
        {
            WorldSceneBindingRuntime.Default.ClearTransientBindings();
        }

        [TearDown]
        public void TearDown()
        {
            WorldSceneBindingRuntime.Default.ClearTransientBindings();
            foreach (GameObject obj in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (obj.name.StartsWith("scene-binding-test-", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }

        [Test]
        public void DuplicatePrimaryBindingsAreRejectedWithoutCreatingLogicalRecords()
        {
            Fixture fixture = CreateFixture();
            WorldSceneBindingRuntime runtime = CreateBindingRuntime(fixture);
            int locationCount = fixture.Locations.Count;
            LocationSceneBinding first = LocationBinding("guild-primary-a", "location.prototype.adventurers-guild", "prototype.scene.adventurers-guild", fixture.WorldId);
            LocationSceneBinding second = LocationBinding("guild-primary-b", "location.prototype.adventurers-guild", "prototype.scene.adventurers-guild.replacement", fixture.WorldId);
            LocationSceneBinding missing = LocationBinding("missing", "location.prototype.does-not-exist", "prototype.scene.missing", fixture.WorldId, required: false);

            runtime.Register(first);
            runtime.Register(second);
            runtime.Register(missing);
            WorldSceneBindingValidationReport report = runtime.Validate();

            Assert.That(first.Status, Is.EqualTo(WorldSceneBindingStatus.Bound));
            Assert.That(second.Status, Is.EqualTo(WorldSceneBindingStatus.Duplicate));
            Assert.That(missing.Status, Is.EqualTo(WorldSceneBindingStatus.WaitingForLogicalRecord));
            Assert.That(report.ErrorCount, Is.EqualTo(1), string.Join(Environment.NewLine, report.Issues));
            Assert.That(report.WarningCount, Is.EqualTo(1), string.Join(Environment.NewLine, report.Issues));
            Assert.That(fixture.Locations.Count, Is.EqualTo(locationCount));
            Assert.That(fixture.Locations.TryGetSnapshot("location.prototype.does-not-exist", out _), Is.False);
        }

        [Test]
        public void EntityBindingMaterializesFromAuthoritativePlacementWithoutChangingPlacement()
        {
            Fixture fixture = CreateFixture();
            WorldSceneBindingRuntime runtime = CreateBindingRuntime(fixture);
            LocationSceneBinding village = LocationBinding("village-anchor", "location.prototype.village", "prototype.scene.village", fixture.WorldId);
            village.transform.position = new Vector3(10f, 2f, 30f);
            WorldEntitySceneBinding player = EntityBinding("player", LocationOccupantEntityType.Body, PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            long before = fixture.EntityLocations.Revision;

            runtime.Register(village);
            runtime.Register(player);
            WorldSceneBindingValidationReport report = runtime.SyncAllFromAuthoritative(initialSync: true);

            Assert.That(report.Succeeded, Is.True, string.Join(Environment.NewLine, report.Issues));
            Assert.That(player.transform.position, Is.EqualTo(village.transform.position));
            Assert.That(fixture.EntityLocations.Revision, Is.EqualTo(before));
            Assert.That(fixture.EntityLocations.TryGetActivePlacement(PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId), out EntityPlacementSnapshot placement), Is.True);
            Assert.That(placement.ExactLocationId, Is.EqualTo("location.prototype.village"));
        }

        [Test]
        public void SceneTransitionUsesConnectionAuthorityAndPreservesStateWhenDenied()
        {
            Fixture fixture = CreateFixture();
            WorldSceneBindingRuntime runtime = CreateBindingRuntime(fixture);
            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            EntityLocationOperationResult moveToGuild = fixture.EntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.scene-binding.move-to-guild",
                entity = actor,
                destinationLocationId = "location.prototype.adventurers-guild",
                worldTime = 5d
            });
            ConnectionSceneBinding door = ConnectionBinding("guild-head-door", PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", fixture.WorldId);
            LocationSceneBinding guild = LocationBinding("guild", "location.prototype.adventurers-guild", "prototype.scene.adventurers-guild", fixture.WorldId);
            LocationSceneBinding office = LocationBinding("office", "location.prototype.guildmaster-office", "prototype.scene.guild-head-office", fixture.WorldId);

            runtime.Register(guild);
            runtime.Register(office);
            runtime.Register(door);
            long beforeDenied = fixture.EntityLocations.Revision;
            SceneBindingTransitionResult denied = door.RequestTraversal(actor, AccessContext(actor, fixture.WorldId), 10d);
            EntityPlacementSnapshot deniedPlacement = Active(fixture, actor);
            long afterDenied = fixture.EntityLocations.Revision;

            fixture.Connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = "test.scene-binding.unlock-door",
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                openState = LocationConnectionOpenState.Open,
                lockState = LocationConnectionLockState.Unlocked,
                worldTime = 11d
            });
            SceneBindingTransitionResult allowed = door.RequestTraversal(actor, AccessContext(actor, fixture.WorldId, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" }), 12d);
            EntityPlacementSnapshot allowedPlacement = Active(fixture, actor);

            Assert.That(moveToGuild.Succeeded, Is.True, moveToGuild.Message);
            Assert.That(denied.Succeeded, Is.False);
            Assert.That(denied.Status, Is.EqualTo(SceneBindingTransitionStatus.AccessDenied));
            Assert.That(afterDenied, Is.EqualTo(beforeDenied));
            Assert.That(deniedPlacement.ExactLocationId, Is.EqualTo("location.prototype.adventurers-guild"));
            Assert.That(allowed.Succeeded, Is.True, allowed.Message);
            Assert.That(allowedPlacement.ExactLocationId, Is.EqualTo("location.prototype.guildmaster-office"));
        }

        [Test]
        public void ConnectionBindingMirrorsDoorStateWithoutOwningIt()
        {
            Fixture fixture = CreateFixture();
            WorldSceneBindingRuntime runtime = CreateBindingRuntime(fixture);
            GameObject doorObject = new GameObject("scene-binding-test-door-collider");
            BoxCollider collider = doorObject.AddComponent<BoxCollider>();
            ConnectionSceneBinding door = doorObject.AddComponent<ConnectionSceneBinding>();
            door.ConfigureConnection(PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, "prototype.connection.guild-head-door", "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", "scene.prototype", fixture.WorldId, collider);

            runtime.Register(door);
            runtime.SyncAllFromAuthoritative(initialSync: true);
            bool closedEnabled = collider.enabled;
            fixture.Connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = "test.scene-binding.open-visual-door",
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                openState = LocationConnectionOpenState.Open,
                lockState = LocationConnectionLockState.Unlocked,
                worldTime = 20d
            });
            runtime.SyncAllFromAuthoritative();

            Assert.That(door.Status, Is.EqualTo(WorldSceneBindingStatus.Bound));
            Assert.That(closedEnabled, Is.True);
            Assert.That(collider.enabled, Is.False);
            Assert.That(fixture.Connections.TryGetConnection(PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, out LocationConnectionSnapshot authoritative), Is.True);
            Assert.That(authoritative.OpenState, Is.EqualTo(LocationConnectionOpenState.Open));
        }

        [Test]
        public void InteractionBindingRoutesSceneInteractionToAuthoritativePoint()
        {
            Fixture fixture = CreateFixture();
            WorldSceneBindingRuntime runtime = CreateBindingRuntime(fixture);
            InteractionPointSceneBinding counter = InteractionBinding("adventurer-counter", PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, "prototype.scene.interaction.adventurer-guild-counter", fixture.WorldId);
            GameObject player = new GameObject("scene-binding-test-interactor");
            player.transform.position = counter.transform.position + Vector3.forward;
            runtime.Register(counter);

            bool canInteract = counter.CanInteract(new InteractionContext(player, player.transform, default));
            counter.Interact(new InteractionContext(player, player.transform, default));

            Assert.That(counter.Status, Is.EqualTo(WorldSceneBindingStatus.Bound));
            Assert.That(canInteract, Is.True);
            Assert.That(counter.LastPoint, Is.Not.Null);
            Assert.That(counter.LastPoint.InteractionPointId, Is.EqualTo(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId));
            Assert.That(fixture.Interactions.TryGetPoint(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, out InteractionPointSnapshot point), Is.True);
            Assert.That(point.SceneBindingKey, Is.EqualTo("prototype.scene.interaction.adventurer-guild-counter"));
        }

        [Test]
        public void InteractionBindingUsesColliderHitDistanceInsteadOfObjectPivot()
        {
            Fixture fixture = CreateFixture();
            WorldSceneBindingRuntime runtime = CreateBindingRuntime(fixture);
            InteractionPointSceneBinding counter = InteractionBinding("offset-counter", PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, "prototype.scene.interaction.offset-counter", fixture.WorldId);
            BoxCollider collider = counter.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(6f, 0f, 0f);
            collider.size = new Vector3(2f, 2f, 1f);
            GameObject player = new GameObject("scene-binding-test-offset-interactor");
            player.transform.position = new Vector3(6f, 0f, -2f);
            Physics.SyncTransforms();

            runtime.Register(counter);
            bool hit = Physics.Raycast(player.transform.position, Vector3.forward, out RaycastHit raycastHit, 5f, ~0, QueryTriggerInteraction.Collide);
            bool canInteract = counter.CanInteract(new InteractionContext(player, player.transform, raycastHit));

            Assert.That(hit, Is.True);
            Assert.That(Vector3.Distance(player.transform.position, counter.transform.position), Is.GreaterThan(3f));
            Assert.That(canInteract, Is.True);
        }

        [Test]
        public void RouteAndCheckpointBindingsRemainTransientPresentationMappings()
        {
            Fixture fixture = CreateFixture(includeRoutes: true, includeCheckpoint: true);
            WorldSceneBindingRuntime runtime = CreateBindingRuntime(fixture);
            RouteSegmentSceneBinding route = New<RouteSegmentSceneBinding>("route");
            route.ConfigureBinding(PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId, "prototype.route.village-market", "scene.prototype", fixture.WorldId);
            CheckpointSceneBinding checkpoint = New<CheckpointSceneBinding>("checkpoint");
            checkpoint.ConfigureBinding("checkpoint.prototype.village-gate", "prototype.checkpoint.village-gate", "scene.prototype", fixture.WorldId);
            int routeCount = fixture.Routes.SegmentCount;
            int checkpointCount = fixture.PoliticalTravel.CheckpointCount;

            runtime.Register(route);
            runtime.Register(checkpoint);
            WorldSceneBindingValidationReport report = runtime.Validate();

            Assert.That(report.Succeeded, Is.True, string.Join(Environment.NewLine, report.Issues));
            Assert.That(route.Status, Is.EqualTo(WorldSceneBindingStatus.Bound));
            Assert.That(checkpoint.Status, Is.EqualTo(WorldSceneBindingStatus.Bound));
            Assert.That(fixture.Routes.SegmentCount, Is.EqualTo(routeCount));
            Assert.That(fixture.PoliticalTravel.CheckpointCount, Is.EqualTo(checkpointCount));
        }

        private static LocationSceneBinding LocationBinding(string name, string locationId, string bindingKey, string worldId, bool required = true)
        {
            LocationSceneBinding binding = New<LocationSceneBinding>(name);
            binding.ConfigureLocation(locationId, bindingKey, "scene.prototype", worldId, requiredBinding: required);
            return binding;
        }

        private static InteractionPointSceneBinding InteractionBinding(string name, string pointId, string bindingKey, string worldId)
        {
            InteractionPointSceneBinding binding = New<InteractionPointSceneBinding>(name);
            binding.ConfigureBinding(pointId, bindingKey, "scene.prototype", worldId, WorldSceneBindingRole.Primary, true);
            return binding;
        }

        private static ConnectionSceneBinding ConnectionBinding(string name, string connectionId, string source, string destination, string worldId)
        {
            GameObject obj = new GameObject($"scene-binding-test-{name}");
            ConnectionSceneBinding binding = obj.AddComponent<ConnectionSceneBinding>();
            binding.ConfigureConnection(connectionId, $"prototype.connection.{name}", source, destination, "scene.prototype", worldId, obj.AddComponent<BoxCollider>(), true);
            return binding;
        }

        private static WorldEntitySceneBinding EntityBinding(string name, LocationOccupantEntityType type, string entityId, string worldId)
        {
            WorldEntitySceneBinding binding = New<WorldEntitySceneBinding>(name);
            binding.ConfigureEntity(type, entityId, $"prototype.entity.{name}", "scene.prototype", worldId, snapToGround: false);
            return binding;
        }

        private static T New<T>(string name) where T : Component
        {
            GameObject obj = new GameObject($"scene-binding-test-{name}");
            return obj.AddComponent<T>();
        }

        private static EntityPlacementSnapshot Active(Fixture fixture, EntityLocationReferenceData actor)
        {
            Assert.That(fixture.EntityLocations.TryGetActivePlacement(actor, out EntityPlacementSnapshot placement), Is.True);
            return placement;
        }

        private static LocationConnectionAccessContextData AccessContext(EntityLocationReferenceData actor, string worldId, string[] offices = null, string[] authorities = null)
        {
            return new LocationConnectionAccessContextData
            {
                actor = actor?.Clone(),
                personId = PrototypeEntityLocationFactory.PlayerPersonId,
                officeIds = offices ?? Array.Empty<string>(),
                authorityIds = authorities ?? Array.Empty<string>()
            };
        }

        private static WorldSceneBindingRuntime CreateBindingRuntime(Fixture fixture)
        {
            WorldSceneBindingRuntime runtime = new WorldSceneBindingRuntime();
            runtime.Configure(fixture.Locations, fixture.EntityLocations, fixture.Interactions, fixture.Connections, fixture.Routes, fixture.Journeys, fixture.PoliticalTravel, fixture.WorldId);
            return runtime;
        }

        private static Fixture CreateFixture(bool includeRoutes = false, bool includeCheckpoint = false)
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
            LocationRouteRuntime routes = null;
            if (includeRoutes)
            {
                routes = new LocationRouteRuntime();
                PrototypeLocationRouteDefinitionFactory.SeedPrototypeRoutes(routes, registry, locations, connections, PersistenceService.LocalWorldId);
            }

            TravelJourneyRuntime journeys = new TravelJourneyRuntime();
            journeys.Configure(registry, locations, entityLocations, connections, routes, PersistenceService.LocalWorldId);

            PoliticalTravelRuntime politicalTravel = null;
            if (includeCheckpoint)
            {
                politicalTravel = new PoliticalTravelRuntime();
                politicalTravel.Configure(registry, new GovernmentRuntime(), null, null, null, locations, routes, PersistenceService.LocalWorldId);
                PoliticalTravelOperationResult checkpoint = politicalTravel.CreateCheckpoint(new BorderCheckpointCreateRequest
                {
                    transactionId = "test.scene-binding.checkpoint",
                    checkpointId = "checkpoint.prototype.village-gate",
                    displayName = "Prototype Village Gate",
                    locationId = "location.prototype.village",
                    routeSegmentId = PrototypeLocationRouteDefinitionFactory.VillageMarketStreetSegmentId,
                    policy = BorderCheckpointPolicy.RequireInspection,
                    lifecycleState = BorderCheckpointLifecycleState.Active,
                    visibility = PoliticalVisibility.Public,
                    worldTime = 1d,
                    sourceEventId = "test.scene-binding",
                    provenanceId = "test.scene-binding"
                });
                Assert.That(checkpoint.Succeeded, Is.True, checkpoint.Message);
            }

            return new Fixture(registry, locations, entityLocations, interactions, connections, routes, journeys, politicalTravel, PersistenceService.LocalWorldId);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactions, LocationConnectionRuntime connections, LocationRouteRuntime routes, TravelJourneyRuntime journeys, PoliticalTravelRuntime politicalTravel, string worldId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                Interactions = interactions;
                Connections = connections;
                Routes = routes;
                Journeys = journeys;
                PoliticalTravel = politicalTravel;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public InteractionPointRuntime Interactions { get; }
            public LocationConnectionRuntime Connections { get; }
            public LocationRouteRuntime Routes { get; }
            public TravelJourneyRuntime Journeys { get; }
            public PoliticalTravelRuntime PoliticalTravel { get; }
            public string WorldId { get; }
        }
    }
}
