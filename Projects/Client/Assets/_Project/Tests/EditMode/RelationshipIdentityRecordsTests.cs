#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Relationships;

namespace UnityIsekaiGame.Tests
{
    public sealed class RelationshipIdentityRecordsTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";
        private static readonly string[] KnownPersons =
        {
            PersistenceService.LocalPlayerId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.parent",
            "person.prototype.child",
            "person.prototype.mentor",
            "person.prototype.student"
        };

        [Test]
        public void PrototypeRelationshipDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeRelationshipDefinitionFactory.FriendRelationshipId, out RelationshipDefinition friend), Is.True);
            Assert.That(friend.Directionality, Is.EqualTo(RelationshipDirectionality.Symmetric));
            Assert.That(friend.HasRole("friend"), Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (RelationshipDefinition definition in PrototypeRelationshipDefinitionFactory.CreateDefinitions().OfType<RelationshipDefinition>())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void SymmetricRelationshipCanonicalizesParticipantsAndPreventsDuplicateActive()
        {
            RelationshipRuntime runtime = CreateRuntime();

            RelationshipOperationResult create = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.friend",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = "person.prototype.friend",
                firstRoleId = "friend",
                secondPersonId = PersistenceService.LocalPlayerId,
                secondRoleId = "friend",
                startWorldTime = 1d
            });
            RelationshipOperationResult duplicateSameId = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.friend",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 1d
            });
            RelationshipOperationResult duplicateActive = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.friend.second",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 2d
            });

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(create.Snapshot.Participants[0].personId, Is.EqualTo("local-player"));
            Assert.That(duplicateSameId.Duplicate, Is.True);
            Assert.That(duplicateActive.Status, Is.EqualTo(RelationshipOperationStatus.DuplicateActiveRelationship));
            Assert.That(runtime.QueryBetween(PersistenceService.LocalPlayerId, "person.prototype.friend", activeOnly: true).Count, Is.EqualTo(1));
        }

        [Test]
        public void DirectedRoleQueriesAndLifecycleAreDeterministic()
        {
            RelationshipRuntime runtime = CreateRuntime();

            RelationshipOperationResult create = runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.mentor-student",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.MentorStudentRelationshipId,
                firstPersonId = "person.prototype.mentor",
                firstRoleId = "mentor",
                secondPersonId = "person.prototype.student",
                secondRoleId = "student",
                startWorldTime = 3d,
                sourceEventId = "event.test.birth-record"
            });
            RelationshipOperationResult ended = runtime.EndRelationship(new RelationshipEndRequest
            {
                recordId = "relationship.test.mentor-student",
                endWorldTime = 20d,
                sourceRecordId = "record.test.family-correction"
            });

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(runtime.QueryByRole("mentor").Count, Is.EqualTo(1));
            Assert.That(runtime.QueryByRole("student").Count, Is.EqualTo(1));
            Assert.That(ended.Succeeded, Is.True, ended.Message);
            Assert.That(runtime.QueryByStatus(RelationshipLifecycleStatus.Ended).Count, Is.EqualTo(1));
            Assert.That(runtime.QueryBetween("person.prototype.mentor", "person.prototype.student", activeOnly: true), Is.Empty);
        }

        [Test]
        public void SnapshotsAreImmutableAndHistoryIsOnlyReferenced()
        {
            RelationshipRuntime runtime = CreateRuntime();
            runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.mentor",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.MentorStudentRelationshipId,
                firstPersonId = "person.prototype.mentor",
                firstRoleId = "mentor",
                secondPersonId = "person.prototype.student",
                secondRoleId = "student",
                startWorldTime = 4d,
                sourceEventId = "event.test.apprenticeship"
            });

            RelationshipSnapshot before = runtime.QueryByRole("mentor").Single();
            before.Data.participants[0].personId = "person.mutated";
            before.Data.tags = new[] { "mutated" };

            RelationshipSnapshot after = runtime.QueryByRole("mentor").Single();

            Assert.That(after.Participants[0].personId, Is.EqualTo("person.prototype.mentor"));
            Assert.That(after.Tags, Is.Empty);
            Assert.That(after.SourceEventId, Is.EqualTo("event.test.apprenticeship"));
        }

        [Test]
        public void PersistenceParticipantRejectsInvalidRestoreWithoutMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            RelationshipRuntime runtime = CreateRuntime(registry);
            runtime.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.persisted",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.RivalRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "rival",
                secondPersonId = "person.prototype.rival",
                secondRoleId = "rival",
                startWorldTime = 7d
            });
            RelationshipPersistenceParticipant participant = new RelationshipPersistenceParticipant(runtime, () => registry, () => KnownPersons);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            RelationshipRuntimeSaveData corrupt = JsonUtility.FromJson<RelationshipRuntimeSaveData>(save.PayloadJson);
            corrupt.records[0].participants[0].roleId = "invalid-role";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), RelationshipPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(runtime.QueryByRole("rival", activeOnly: true).Count, Is.EqualTo(1));
        }

        private static RelationshipRuntime CreateRuntime(DefinitionRegistry registry = null)
        {
            RelationshipRuntime runtime = new RelationshipRuntime();
            runtime.Configure(registry ?? CreateRegistry(), KnownPersons);
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry());
        }
    }
}
#endif
