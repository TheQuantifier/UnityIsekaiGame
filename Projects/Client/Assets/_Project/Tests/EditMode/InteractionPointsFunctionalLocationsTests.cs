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
    public sealed class InteractionPointsFunctionalLocationsTests
    {
        [Test]
        public void PrototypeDefinitionsValidateAndSeedLogicalInteractionPoints()
        {
            Fixture fixture = CreateFixture();
            DefinitionValidationReport report = ValidateInteractionDefinitions(fixture.Registry);

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.GetSummary());
            Assert.That(fixture.Registry.TryGet(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterDefinitionId, out InteractionPointDefinition counter), Is.True);
            Assert.That(fixture.Registry.TryGet(PrototypeInteractionPointDefinitionFactory.RegisterAdventurerServiceId, out InteractionServiceDefinition service), Is.True);
            Assert.That(counter.Category, Is.EqualTo(InteractionPointCategory.GuildCounter));
            Assert.That(service.DestinationRuntime, Is.EqualTo(InteractionDestinationRuntime.OrganizationMembership));
            Assert.That(fixture.Interactions.PointCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(fixture.Interactions.ValidateCurrent(out string failure), Is.True, failure);
            Assert.That(fixture.Interactions.GetPointsByHost("location.prototype.adventurers-guild").Select(item => item.InteractionPointId), Does.Contain(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId));
            Assert.That(fixture.Interactions.GetPointsWithinLocationSubtree("location.prototype.adventurers-guild").Select(item => item.InteractionPointId), Does.Contain(PrototypeInteractionPointDefinitionFactory.GuildHeadDeskPointId));
        }

        [Test]
        public void PointDefinitionsRemainSeparateFromRuntimeInstances()
        {
            Fixture fixture = CreateFixture();
            InteractionPointOperationResult first = CreateWorkstation(fixture, "interaction-point.test.workstation-a", "location.prototype.merchant-counter");
            InteractionPointOperationResult second = CreateWorkstation(fixture, "interaction-point.test.workstation-b", "location.prototype.merchant-counter");
            InteractionPointSnapshot mutated = first.Point;
            InteractionPointRecordData snapshotCopy = mutated.ToSaveData();
            snapshotCopy.displayName = "Mutated Snapshot";

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(first.Point.InteractionPointId, Is.Not.EqualTo(second.Point.InteractionPointId));
            Assert.That(first.Point.InteractionPointDefinitionId, Is.EqualTo(second.Point.InteractionPointDefinitionId));
            Assert.That(fixture.Interactions.GetPointsByDefinition(PrototypeInteractionPointDefinitionFactory.WorkstationDefinitionId).Select(item => item.InteractionPointId), Does.Contain(first.Point.InteractionPointId));
            Assert.That(mutated.DisplayName, Is.Not.EqualTo("Mutated Snapshot"));
        }

        [Test]
        public void InvalidHostRejectsWithoutMutationAndValidReassignmentPreservesHistory()
        {
            Fixture fixture = CreateFixture();
            long before = fixture.Interactions.Revision;
            InteractionPointOperationResult invalid = fixture.Interactions.CreatePoint(new InteractionPointCreateRequest
            {
                transactionId = "test.interaction.invalid-host",
                interactionPointId = "interaction-point.test.invalid-host",
                interactionPointDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationDefinitionId,
                displayName = "Invalid Wilderness Workstation",
                hostLocationId = "location.prototype.wilderness-ring",
                serviceDefinitionIds = new[] { PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId }
            });

            Assert.That(invalid.Status, Is.EqualTo(InteractionPointOperationStatus.InvalidHostLocation));
            Assert.That(fixture.Interactions.Revision, Is.EqualTo(before));

            InteractionPointOperationResult create = CreateWorkstation(fixture, "interaction-point.test.reassign", "location.prototype.merchant-counter");
            InteractionPointOperationResult reassign = fixture.Interactions.ReassignHost(new InteractionPointHostReassignmentRequest
            {
                transactionId = "test.interaction.reassign",
                interactionPointId = create.Point.InteractionPointId,
                newHostLocationId = "location.prototype.adventurers-guild",
                worldTime = 20d
            });

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(reassign.Succeeded, Is.True, reassign.Message);
            Assert.That(reassign.Point.InteractionPointId, Is.EqualTo(create.Point.InteractionPointId));
            Assert.That(reassign.Point.ActiveHostLocationId, Is.EqualTo("location.prototype.adventurers-guild"));
            Assert.That(fixture.Interactions.HostAssignments.Count(item => item.InteractionPointId == create.Point.InteractionPointId), Is.EqualTo(2));
            Assert.That(fixture.Interactions.HostAssignments.Count(item => item.InteractionPointId == create.Point.InteractionPointId && item.IsActive), Is.EqualTo(1));
        }

        [Test]
        public void SubjectLinksProvidersAndInvocationDoNotOwnDestinationState()
        {
            Fixture fixture = CreateFixture();
            InteractionPointRuntime runtime = fixture.Interactions;
            long before = runtime.Revision;

            InteractionPointOperationResult link = runtime.AddSubjectLink(new InteractionSubjectLinkRequest
            {
                transactionId = "test.interaction.subject-link",
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                role = InteractionSubjectLinkRole.AssociatedQuestSource,
                subject = Subject("QuestSource", "quest-source.prototype.board", fixture.WorldId)
            });
            InteractionPointOperationResult provider = runtime.AssignProvider(new InteractionProviderAssignmentRequest
            {
                transactionId = "test.interaction.provider",
                interactionPointId = PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.AdventurerInformationServiceId,
                providerEntity = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.GuildMasterPersonId, fixture.WorldId)
            });
            EntityLocationOperationResult movePlayer = fixture.EntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.interaction.invoke.move-player",
                entity = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId),
                destinationLocationId = "location.prototype.adventurers-guild",
                worldTime = 24d
            });
            InteractionInvocationResult invoke = runtime.Invoke(new InteractionRequest
            {
                transactionId = "test.interaction.invoke",
                interactionPointId = PrototypeInteractionPointDefinitionFactory.QuestBoardPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.QuestBoardBrowseServiceId,
                consumerEntity = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId),
                targetSubject = Subject("QuestBoard", "quest-board.prototype.guild", fixture.WorldId),
                worldTime = 25d
            });

            Assert.That(link.Succeeded, Is.True, link.Message);
            Assert.That(provider.Succeeded, Is.True, provider.Message);
            Assert.That(movePlayer.Succeeded, Is.True, movePlayer.Message);
            Assert.That(invoke.Success, Is.True, invoke.Message);
            Assert.That(invoke.DestinationRuntime, Is.EqualTo(InteractionDestinationRuntime.Quest));
            Assert.That(invoke.RevisionBefore, Is.EqualTo(invoke.RevisionAfter));
            Assert.That(runtime.Revision, Is.EqualTo(before + 2L));
            Assert.That(runtime.GetSubjectLinks(PrototypeInteractionPointDefinitionFactory.AdventurerGuildCounterPointId, includeHidden: true).Any(item => item.Subject.subjectId == "quest-source.prototype.board"), Is.True);
        }

        [Test]
        public void PresenceEligibilityUsesEntityLocationAuthority()
        {
            Fixture fixture = CreateFixture();
            InteractionPointRuntime runtime = fixture.Interactions;

            InteractionEligibilityResult playerAbsent = runtime.EvaluateEligibility(new InteractionEligibilityRequest
            {
                interactionPointId = PrototypeInteractionPointDefinitionFactory.QuestBoardPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.QuestBoardBrowseServiceId,
                consumerEntity = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId),
            });
            EntityLocationOperationResult movePlayer = fixture.EntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.interaction.move-player.guild",
                entity = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId),
                destinationLocationId = "location.prototype.adventurers-guild",
                worldTime = 30d
            });
            InteractionEligibilityResult eligible = runtime.EvaluateEligibility(new InteractionEligibilityRequest
            {
                interactionPointId = PrototypeInteractionPointDefinitionFactory.QuestBoardPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.QuestBoardBrowseServiceId,
                consumerEntity = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId),
            });

            Assert.That(playerAbsent.Status, Is.EqualTo(InteractionPointOperationStatus.ConsumerAbsent));
            Assert.That(movePlayer.Succeeded, Is.True, movePlayer.Message);
            Assert.That(eligible.Eligible, Is.True, eligible.Message);
            Assert.That(eligible.HostLocationId, Is.EqualTo("location.prototype.adventurers-guild"));
        }

        [Test]
        public void ExclusiveSessionsBlockOverflowUntilCompleted()
        {
            Fixture fixture = CreateFixture();
            InteractionPointOperationResult point = CreateWorkstation(fixture, "interaction-point.test.exclusive", "location.prototype.merchant-counter");
            InteractionPointRuntime runtime = fixture.Interactions;
            EntityLocationOperationResult movePlayer = fixture.EntityLocations.Relocate(new EntityRelocationRequest
            {
                transactionId = "test.interaction.session.move-player",
                entity = PrototypeEntityLocationFactory.Body(PrototypeEntityLocationFactory.PlayerBodyId, fixture.WorldId),
                destinationLocationId = "location.prototype.merchant-counter",
                worldTime = 39d
            });

            InteractionPointOperationResult first = runtime.StartSession(new InteractionSessionStartRequest
            {
                transactionId = "test.interaction.session.first",
                interactionPointId = point.Point.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                consumerEntity = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId),
                startWorldTime = 40d
            });
            InteractionPointOperationResult secondBlocked = runtime.StartSession(new InteractionSessionStartRequest
            {
                transactionId = "test.interaction.session.second-blocked",
                interactionPointId = point.Point.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                consumerEntity = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.MerchantPersonId, fixture.WorldId),
                startWorldTime = 41d
            });
            InteractionPointOperationResult completed = runtime.TransitionSession(new InteractionSessionTransitionRequest
            {
                transactionId = "test.interaction.session.complete",
                sessionId = first.Session.SessionId,
                targetState = InteractionUseSessionLifecycle.Completed,
                worldTime = 42d
            });
            InteractionPointOperationResult second = runtime.StartSession(new InteractionSessionStartRequest
            {
                transactionId = "test.interaction.session.second",
                interactionPointId = point.Point.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                consumerEntity = PrototypeEntityLocationFactory.Person(PrototypeEntityLocationFactory.MerchantPersonId, fixture.WorldId),
                startWorldTime = 43d
            });

            Assert.That(point.Succeeded, Is.True, point.Message);
            Assert.That(movePlayer.Succeeded, Is.True, movePlayer.Message);
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(secondBlocked.Status, Is.EqualTo(InteractionPointOperationStatus.CapacityFull));
            Assert.That(completed.Succeeded, Is.True, completed.Message);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(runtime.GetUseSessions(point.Point.InteractionPointId).Count(item => item.LifecycleState == InteractionUseSessionLifecycle.Active), Is.EqualTo(1));
        }

        [Test]
        public void ReservationsRejectOverlapWithoutMutatingCapacityState()
        {
            Fixture fixture = CreateFixture();
            InteractionPointOperationResult point = CreateWorkstation(fixture, "interaction-point.test.reservation", "location.prototype.merchant-counter");
            long before = fixture.Interactions.Revision;
            InteractionPointOperationResult first = fixture.Interactions.Reserve(new InteractionReservationRequest
            {
                transactionId = "test.interaction.reserve.first",
                reservationId = "interaction-reservation.test.first",
                interactionPointId = point.Point.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                reservingSubject = Subject("Person", PrototypeEntityLocationFactory.PlayerPersonId, fixture.WorldId),
                startWorldTime = 50d,
                endWorldTime = 55d
            });
            InteractionPointOperationResult overlap = fixture.Interactions.Reserve(new InteractionReservationRequest
            {
                transactionId = "test.interaction.reserve.overlap",
                reservationId = "interaction-reservation.test.overlap",
                interactionPointId = point.Point.InteractionPointId,
                serviceDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId,
                reservingSubject = Subject("Person", PrototypeEntityLocationFactory.MerchantPersonId, fixture.WorldId),
                startWorldTime = 52d,
                endWorldTime = 56d
            });

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(overlap.Status, Is.EqualTo(InteractionPointOperationStatus.ReservationConflict));
            Assert.That(fixture.Interactions.GetReservations(point.Point.InteractionPointId).Count, Is.EqualTo(1));
            Assert.That(fixture.Interactions.Revision, Is.EqualTo(before + 1L));
        }

        [Test]
        public void HiddenPointsAndSceneBindingsAreSceneIndependent()
        {
            Fixture fixture = CreateFixture();
            InteractionPointOperationResult hidden = fixture.Interactions.CreatePoint(new InteractionPointCreateRequest
            {
                transactionId = "test.interaction.hidden",
                interactionPointId = "interaction-point.test.hidden-cell",
                interactionPointDefinitionId = PrototypeInteractionPointDefinitionFactory.PrisonCellDefinitionId,
                displayName = "Hidden Cell Point",
                hostLocationId = "location.prototype.basement-prison",
                serviceDefinitionIds = new[] { PrototypeInteractionPointDefinitionFactory.PrisonCellInspectServiceId },
                visibility = InteractionPointVisibility.Hidden,
                sceneBindingKey = "scene.prototype.hidden-cell-marker",
                sceneBindingCategory = InteractionSceneBindingCategory.PrototypeMarker
            });

            Assert.That(hidden.Succeeded, Is.True, hidden.Message);
            Assert.That(fixture.Interactions.GetPointsByHost("location.prototype.basement-prison").Any(item => item.InteractionPointId == hidden.Point.InteractionPointId), Is.False);
            Assert.That(fixture.Interactions.GetPointsByHost("location.prototype.basement-prison", includeHidden: true).Any(item => item.InteractionPointId == hidden.Point.InteractionPointId), Is.True);
            Assert.That(hidden.Point.SceneBindingKey, Is.EqualTo("scene.prototype.hidden-cell-marker"));
            Assert.That(hidden.Point.SceneBindingCategory, Is.EqualTo(InteractionSceneBindingCategory.PrototypeMarker));
        }

        [Test]
        public void PersistenceRoundTripRejectsCorruptGraphsBeforeCommit()
        {
            Fixture fixture = CreateFixture();
            InteractionPointPersistenceParticipant participant = new InteractionPointPersistenceParticipant(fixture.Interactions, () => fixture.Registry, () => fixture.Locations, () => fixture.EntityLocations, fixture.WorldId);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepare = participant.PreparePayload(save.PayloadJson, InteractionPointPersistenceParticipant.CurrentParticipantSchemaVersion);
            InteractionPointRuntime restored = new InteractionPointRuntime();
            restored.Configure(fixture.Registry, fixture.Locations, fixture.EntityLocations, fixture.WorldId);
            InteractionPointOperationResult restore = restored.RestoreFromSaveData(JsonUtility.FromJson<InteractionPointRuntimeSaveData>(save.PayloadJson), fixture.Locations, fixture.EntityLocations, fixture.WorldId);
            InteractionPointRuntimeSaveData before = fixture.Interactions.CreateSaveData();
            InteractionPointRuntimeSaveData corrupt = before.Clone();
            corrupt.points[0].activeHostLocationId = "location.prototype.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), InteractionPointPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(prepare.Succeeded, Is.True, prepare.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.PointCount, Is.EqualTo(fixture.Interactions.PointCount));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Interactions.CreateSaveData().points.Select(item => item.activeHostLocationId), Is.EqualTo(before.points.Select(item => item.activeHostLocationId)));
        }

        private static InteractionPointOperationResult CreateWorkstation(Fixture fixture, string id, string hostLocationId)
        {
            return fixture.Interactions.CreatePoint(new InteractionPointCreateRequest
            {
                transactionId = $"test.interaction.create.{id}",
                interactionPointId = id,
                interactionPointDefinitionId = PrototypeInteractionPointDefinitionFactory.WorkstationDefinitionId,
                displayName = id,
                hostLocationId = hostLocationId,
                serviceDefinitionIds = new[] { PrototypeInteractionPointDefinitionFactory.WorkstationUseServiceId },
                sourceEventId = "event.test.interaction",
                provenanceId = "test.interaction"
            });
        }

        private static InteractionSubjectReferenceData Subject(string type, string id, string worldId)
        {
            return new InteractionSubjectReferenceData { subjectType = type, subjectId = id, worldId = worldId };
        }

        private static DefinitionValidationReport ValidateInteractionDefinitions(DefinitionRegistry registry)
        {
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in registry.DefinitionsById.Values.Where(item => item is InteractionPointDefinition || item is InteractionServiceDefinition))
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
            LocationRuntime locations = new LocationRuntime();
            PrototypeLocationDefinitionFactory.SeedPrototypeLocations(locations, registry, PersistenceService.LocalWorldId);
            locations.Configure(registry, PersistenceService.LocalWorldId);
            EntityLocationRuntime entityLocations = new EntityLocationRuntime();
            PrototypeEntityLocationFactory.SeedPrototypePlacements(entityLocations, locations, PersistenceService.LocalWorldId);
            InteractionPointRuntime interactions = new InteractionPointRuntime();
            PrototypeInteractionPointDefinitionFactory.SeedPrototypeInteractionPoints(interactions, registry, locations, entityLocations, PersistenceService.LocalWorldId);
            return new Fixture(registry, locations, entityLocations, interactions, PersistenceService.LocalWorldId);
        }

        private sealed class Fixture
        {
            public Fixture(DefinitionRegistry registry, LocationRuntime locations, EntityLocationRuntime entityLocations, InteractionPointRuntime interactions, string worldId)
            {
                Registry = registry;
                Locations = locations;
                EntityLocations = entityLocations;
                Interactions = interactions;
                WorldId = worldId;
            }

            public DefinitionRegistry Registry { get; }
            public LocationRuntime Locations { get; }
            public EntityLocationRuntime EntityLocations { get; }
            public InteractionPointRuntime Interactions { get; }
            public string WorldId { get; }
        }
    }
}
