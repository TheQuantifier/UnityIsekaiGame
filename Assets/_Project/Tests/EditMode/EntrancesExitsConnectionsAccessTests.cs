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
    public sealed class EntrancesExitsConnectionsAccessTests
    {
        [Test]
        public void PrototypeDefinitionsValidateAndSeedSceneIndependentConnections()
        {
            Fixture fixture = CreateFixture();
            DefinitionValidationReport report = ValidateConnectionDefinitions(fixture.Registry);

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.GetSummary());
            Assert.That(fixture.Registry.TryGet(PrototypeLocationConnectionDefinitionFactory.LockableDoorDefinitionId, out LocationConnectionDefinition lockable), Is.True);
            Assert.That(fixture.Registry.TryGet(PrototypeLocationConnectionDefinitionFactory.GuildMemberAccessPolicyId, out LocationAccessPolicyDefinition memberPolicy), Is.True);
            Assert.That(lockable.SupportsLockState, Is.True);
            Assert.That(memberPolicy.Category, Is.EqualTo(LocationAccessPolicyCategory.OrganizationMembers));
            Assert.That(fixture.Connections.ConnectionCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(fixture.Connections.EndpointCount, Is.EqualTo(fixture.Connections.ConnectionCount * 2));
            Assert.That(fixture.Connections.ValidateCurrent(out string failure), Is.True, failure);
            Assert.That(fixture.Connections.GetOutgoingConnections("location.prototype.village").Select(item => item.ConnectionId), Does.Contain(PrototypeLocationConnectionDefinitionFactory.VillageGuildEntranceConnectionId));
        }

        [Test]
        public void ConnectionsRemainSeparateFromSpatialAdjacencyAndDefinitions()
        {
            Fixture fixture = CreateFixture();
            LocationConnectionOperationResult preview = CreateDoor(fixture, "connection.test.preview", "location.prototype.adventurers-guild", "location.prototype.merchant-counter", preview: true);
            LocationConnectionOperationResult first = CreateDoor(fixture, "connection.test.a", "location.prototype.adventurers-guild", "location.prototype.merchant-counter");
            LocationConnectionOperationResult second = CreateDoor(fixture, "connection.test.b", "location.prototype.adventurers-guild", "location.prototype.merchant-counter");
            LocationConnectionOperationResult duplicate = fixture.Connections.CreateConnection(new LocationConnectionCreateRequest
            {
                transactionId = "test.connection.create.connection.test.a",
                connectionId = "connection.test.a",
                connectionDefinitionId = PrototypeLocationConnectionDefinitionFactory.PublicDoorwayDefinitionId,
                sourceLocationId = "location.prototype.adventurers-guild",
                destinationLocationId = "location.prototype.merchant-counter"
            });

            Assert.That(preview.Status, Is.EqualTo(LocationConnectionOperationStatus.Preview));
            Assert.That(fixture.Connections.TryGetConnection("connection.test.preview", out _), Is.False);
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(first.Connection.ConnectionDefinitionId, Is.EqualTo(second.Connection.ConnectionDefinitionId));
            Assert.That(first.Connection.ConnectionId, Is.Not.EqualTo(second.Connection.ConnectionId));
            Assert.That(duplicate.Status, Is.EqualTo(LocationConnectionOperationStatus.Duplicate));
            Assert.That(fixture.Locations.GetSpatialRelationships("location.prototype.market-district").Any(), Is.True);
            Assert.That(fixture.Connections.GetOutgoingConnections("location.prototype.market-district").Any(item => item.DestinationLocationId == "location.prototype.adventurers-guild"), Is.False);
        }

        [Test]
        public void OpenLockBlockageAndLifecycleGateTraversalWithoutPartialMutation()
        {
            Fixture fixture = CreateFixture();
            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            EntityLocationOperationResult moveToGuild = fixture.EntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.connection.move.guild",
                entity = actor,
                destinationLocationId = "location.prototype.adventurers-guild",
                worldTime = 9d
            });
            long entityBefore = fixture.EntityLocations.Revision;
            LocationConnectionOperationResult lockedDenied = fixture.Connections.Traverse(Traversal(fixture, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(fixture, actor, organizations: new[] { "organization.prototype.adventurers-guild" })));
            long entityAfterLockedDenied = fixture.EntityLocations.Revision;
            LocationConnectionOperationResult unlocked = fixture.Connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = "test.connection.guild-head.unlock",
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                openState = LocationConnectionOpenState.Open,
                lockState = LocationConnectionLockState.Unlocked,
                worldTime = 11d
            });
            LocationConnectionOperationResult blocked = fixture.Connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = "test.connection.guild-head.block",
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                blockageState = LocationConnectionBlockageState.TemporarilyBlocked,
                worldTime = 12d
            });
            LocationConnectionOperationResult blockedDenied = fixture.Connections.Traverse(Traversal(fixture, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(fixture, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" })));
            LocationConnectionOperationResult clear = fixture.Connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = "test.connection.guild-head.clear",
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                blockageState = LocationConnectionBlockageState.Clear,
                worldTime = 13d
            });
            LocationConnectionOperationResult traverse = fixture.Connections.Traverse(Traversal(fixture, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(fixture, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" })));
            LocationConnectionOperationResult historical = fixture.Connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = "test.connection.guild-head.historical",
                connectionId = PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId,
                lifecycleState = LocationConnectionLifecycleState.Historical,
                worldTime = 20d
            });

            Assert.That(moveToGuild.Succeeded, Is.True, moveToGuild.Message);
            Assert.That(lockedDenied.Status, Is.EqualTo(LocationConnectionOperationStatus.MissingKey));
            Assert.That(entityAfterLockedDenied, Is.EqualTo(entityBefore));
            Assert.That(unlocked.Succeeded, Is.True, unlocked.Message);
            Assert.That(blocked.Succeeded, Is.True, blocked.Message);
            Assert.That(blockedDenied.Status, Is.EqualTo(LocationConnectionOperationStatus.DeniedByBlockage));
            Assert.That(clear.Succeeded, Is.True, clear.Message);
            Assert.That(traverse.Succeeded, Is.True, traverse.Message);
            Assert.That(traverse.PlacementResult.Succeeded, Is.True, traverse.PlacementResult.Message);
            Assert.That(fixture.EntityLocations.TryGetActivePlacement(actor, out EntityPlacementSnapshot placement), Is.True);
            Assert.That(placement.ExactLocationId, Is.EqualTo("location.prototype.guildmaster-office"));
            Assert.That(historical.Succeeded, Is.True, historical.Message);
        }

        [Test]
        public void AccessPoliciesConsumeExternalAuthorityReferencesWithoutOwningThem()
        {
            Fixture fixture = CreateFixture();
            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            Unlock(fixture, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId);
            Unlock(fixture, PrototypeLocationConnectionDefinitionFactory.MayorOfficeConnectionId);
            Unlock(fixture, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId);
            Unlock(fixture, PrototypeLocationConnectionDefinitionFactory.GuildStorageConnectionId);
            Unlock(fixture, PrototypeLocationConnectionDefinitionFactory.PrisonCellConnectionId);

            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(fixture, actor)).accessState, Is.EqualTo(LocationConnectionAccessState.MissingAuthority));
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.GuildHeadOfficeConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.guildmaster-office", AccessContext(fixture, actor, offices: new[] { "office.prototype.guild-head" }, authorities: new[] { "permission.prototype.guild.rank-admin" })).Allowed, Is.True);
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.MayorOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(fixture, actor, offices: new[] { "office.prototype.mayor" }, authorities: new[] { "authority.government.prototype" })).Allowed, Is.True);
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(fixture, actor, employments: new[] { "employment.prototype.records-clerk" })).Allowed, Is.True);
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(fixture, actor, permits: new[] { "legal-right.prototype.records.restricted-read" })).Allowed, Is.True);
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.RecordsOfficeConnectionId, actor, "location.prototype.civic-office", "location.prototype.mayor-office", AccessContext(fixture, actor, warrants: new[] { "warrant.prototype.search" })).Allowed, Is.True);
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.GuildStorageConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.merchant-counter", AccessContext(fixture, actor, properties: new[] { "property.prototype.guild-storage" })).Allowed, Is.True);
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.GuildStorageConnectionId, actor, "location.prototype.adventurers-guild", "location.prototype.merchant-counter", AccessContext(fixture, actor, keyDefinitions: new[] { "item.prototype-storage-key" })).Allowed, Is.True);
            Assert.That(Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.PrisonCellConnectionId, actor, "location.prototype.civic-office", "location.prototype.basement-prison", AccessContext(fixture, actor, custodyRoles: new[] { "custody-role.prototype.guard" })).Allowed, Is.True);
        }

        [Test]
        public void HiddenAndOneWayConnectionsRemainAuthoritativeButProjectionSafe()
        {
            Fixture fixture = CreateFixture();
            Assert.That(fixture.Connections.GetOutgoingConnections("location.prototype.guildmaster-office").Any(item => item.ConnectionId == PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId), Is.False);
            Assert.That(fixture.Connections.GetOutgoingConnections("location.prototype.guildmaster-office", includeHidden: true).Any(item => item.ConnectionId == PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId), Is.True);

            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            LocationConnectionAccessResult forward = Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.DungeonOneWayDropConnectionId, actor, "location.prototype.wilderness-ring", "location.prototype.dungeon-entry", AccessContext(fixture, actor, privileged: true));
            LocationConnectionAccessResult reverse = Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.DungeonOneWayDropConnectionId, actor, "location.prototype.dungeon-entry", "location.prototype.wilderness-ring", AccessContext(fixture, actor, privileged: true));

            Assert.That(forward.Allowed, Is.True, forward.diagnostics);
            Assert.That(reverse.accessState, Is.EqualTo(LocationConnectionAccessState.DeniedByDirection));
        }

        [Test]
        public void ExplicitGrantCanAuthorizeTraversalWithoutChangingPolicyDefinitions()
        {
            Fixture fixture = CreateFixture();
            EntityLocationReferenceData actor = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId);
            EntityLocationOperationResult move = fixture.EntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.connection.hidden.move",
                entity = actor,
                destinationLocationId = "location.prototype.guildmaster-office",
                worldTime = 30d
            });
            LocationConnectionAccessResult denied = Evaluate(fixture, PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId, actor, "location.prototype.guildmaster-office", "location.prototype.basement-prison", AccessContext(fixture, actor));
            LocationConnectionOperationResult grant = fixture.Connections.GrantAccess(new LocationAccessGrantRequest
            {
                transactionId = "test.connection.hidden.grant",
                grantId = "location-access-grant.test.hidden",
                connectionId = PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId,
                grantee = actor,
                startWorldTime = 30d,
                endWorldTime = 40d
            });
            LocationConnectionOperationResult traverse = fixture.Connections.Traverse(Traversal(fixture, PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId, actor, "location.prototype.guildmaster-office", "location.prototype.basement-prison", AccessContext(fixture, actor), worldTime: 35d));

            Assert.That(move.Succeeded, Is.True, move.Message);
            Assert.That(denied.Allowed, Is.False);
            Assert.That(grant.Succeeded, Is.True, grant.Message);
            Assert.That(traverse.Succeeded, Is.True, traverse.Message);
            Assert.That(fixture.Connections.GrantCount, Is.EqualTo(1));
            Assert.That(fixture.Connections.TryGetConnection(PrototypeLocationConnectionDefinitionFactory.HiddenPassageConnectionId, out LocationConnectionSnapshot connection), Is.True);
            Assert.That(connection.AccessPolicyDefinitionIds, Is.EqualTo(new[] { PrototypeLocationConnectionDefinitionFactory.ExplicitWhitelistAccessPolicyId }));
        }

        [Test]
        public void PersistenceRoundTripRejectsCorruptConnectionGraphsWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            LocationConnectionPersistenceParticipant participant = new LocationConnectionPersistenceParticipant(fixture.Connections, () => fixture.Registry, () => fixture.Locations, () => fixture.EntityLocations, () => fixture.Interactions, fixture.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(save.PayloadJson, LocationConnectionPersistenceParticipant.CurrentParticipantSchemaVersion);
            LocationConnectionRuntime restored = new LocationConnectionRuntime();
            restored.Configure(fixture.Registry, fixture.Locations, fixture.EntityLocations, fixture.Interactions, fixture.WorldId);
            LocationConnectionOperationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<LocationConnectionRuntimeSaveData>(save.PayloadJson), fixture.Locations, fixture.EntityLocations, fixture.Interactions, fixture.WorldId);
            LocationConnectionRuntimeSaveData before = fixture.Connections.CreateSaveData();
            LocationConnectionRuntimeSaveData corrupt = before.Clone();
            corrupt.connections[0].destinationLocationId = "location.prototype.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), LocationConnectionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.ConnectionCount, Is.EqualTo(fixture.Connections.ConnectionCount));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Connections.CreateSaveData().connections.Select(item => item.destinationLocationId), Is.EqualTo(before.connections.Select(item => item.destinationLocationId)));
        }

        private static LocationConnectionOperationResult CreateDoor(Fixture fixture, string id, string source, string destination, bool preview = false)
        {
            return fixture.Connections.CreateConnection(new LocationConnectionCreateRequest
            {
                transactionId = $"test.connection.create.{id}",
                connectionId = id,
                connectionDefinitionId = PrototypeLocationConnectionDefinitionFactory.PublicDoorwayDefinitionId,
                displayName = id,
                sourceLocationId = source,
                destinationLocationId = destination,
                accessPolicyDefinitionIds = new[] { PrototypeLocationConnectionDefinitionFactory.PublicAccessPolicyId },
                sceneBindingKey = $"scene.{id}",
                sceneBindingCategory = LocationConnectionSceneBindingCategory.PrototypeMarker,
                worldTime = 5d,
                preview = preview
            });
        }

        private static void Unlock(Fixture fixture, string connectionId)
        {
            fixture.Connections.MutateState(new LocationConnectionStateMutationRequest
            {
                transactionId = $"test.connection.unlock.{connectionId}",
                connectionId = connectionId,
                openState = LocationConnectionOpenState.Open,
                lockState = LocationConnectionLockState.Unlocked,
                blockageState = LocationConnectionBlockageState.Clear,
                worldTime = 15d
            });
        }

        private static LocationConnectionAccessResult Evaluate(Fixture fixture, string connectionId, EntityLocationReferenceData actor, string from, string to, LocationConnectionAccessContextData accessContext)
        {
            return fixture.Connections.EvaluateAccess(Traversal(fixture, connectionId, actor, from, to, accessContext));
        }

        private static LocationConnectionTraversalRequest Traversal(Fixture fixture, string connectionId, EntityLocationReferenceData actor, string from, string to, LocationConnectionAccessContextData accessContext, double worldTime = 20d)
        {
            return new LocationConnectionTraversalRequest
            {
                transactionId = $"test.connection.traverse.{connectionId}.{worldTime}",
                connectionId = connectionId,
                actor = actor,
                fromLocationId = from,
                toLocationId = to,
                accessContext = accessContext,
                worldTime = worldTime,
                sourceEventId = "event.test.connection",
                provenanceId = "test.connection"
            };
        }

        private static LocationConnectionAccessContextData AccessContext(
            Fixture fixture,
            EntityLocationReferenceData actor,
            bool privileged = false,
            string[] organizations = null,
            string[] ranks = null,
            string[] offices = null,
            string[] authorities = null,
            string[] employments = null,
            string[] properties = null,
            string[] permits = null,
            string[] warrants = null,
            string[] custodyRoles = null,
            string[] keyDefinitions = null)
        {
            return new LocationConnectionAccessContextData
            {
                actor = actor,
                personId = PrototypeEntityLocationFactory.PlayerPersonId,
                organizationIds = organizations ?? Array.Empty<string>(),
                rankIds = ranks ?? Array.Empty<string>(),
                officeIds = offices ?? Array.Empty<string>(),
                authorityIds = authorities ?? Array.Empty<string>(),
                employmentIds = employments ?? Array.Empty<string>(),
                propertyIds = properties ?? Array.Empty<string>(),
                permitIds = permits ?? Array.Empty<string>(),
                warrantIds = warrants ?? Array.Empty<string>(),
                custodyRoleIds = custodyRoles ?? Array.Empty<string>(),
                keyDefinitionIds = keyDefinitions ?? Array.Empty<string>(),
                privileged = privileged
            };
        }

        private static DefinitionValidationReport ValidateConnectionDefinitions(DefinitionRegistry registry)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in registry.DefinitionsById.Values.Where(item => item is LocationConnectionDefinition || item is LocationAccessPolicyDefinition))
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
            LocationRuntime locations = new LocationRuntime();
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            locations.Configure(registry, PersistenceService.LocalWorldId);
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            InteractionPointRuntime interactions = new InteractionPointRuntime();
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactions, registry, locations, entityLocations, PersistenceService.LocalWorldId);
            LocationConnectionRuntime connections = new LocationConnectionRuntime();
            PrototypeLocationConnectionDefinitionFactory.SeedPrototypeConnections(connections, registry, locations, entityLocations, interactions, PersistenceService.LocalWorldId);
            return new Fixture(registry, locations, entityLocations, interactions, connections, PersistenceService.LocalWorldId);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactions, LocationConnectionRuntime connections, string worldId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                Interactions = interactions;
                Connections = connections;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public InteractionPointRuntime Interactions { get; }
            public LocationConnectionRuntime Connections { get; }
            public string WorldId { get; }
        }
    }
}
