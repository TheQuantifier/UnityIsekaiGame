#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Relationships;

namespace UnityIsekaiGame.Tests
{
    public sealed class InterpersonalAttitudeRelationshipValuesTests
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
        public void PrototypeAttitudeDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeAttitudeDefinitionFactory.TrustId, out AttitudeDimensionDefinition trust), Is.True);
            Assert.That(registry.TryGet(PrototypeAttitudeDefinitionFactory.FearId, out AttitudeDimensionDefinition fear), Is.True);
            Assert.That(trust.MinimumValue, Is.EqualTo(-100));
            Assert.That(fear.MinimumValue, Is.EqualTo(0));
            Assert.That(fear.NegativeValuesAllowed, Is.False);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (AttitudeDimensionDefinition definition in PrototypeAttitudeDefinitionFactory.CreateDefinitions().OfType<AttitudeDimensionDefinition>())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void DirectionalAttitudesDoNotMirrorAndNeutralReadsDoNotCreateRecords()
        {
            InterpersonalAttitudeRuntime runtime = CreateRuntime();

            AttitudeEffectiveValueSnapshot neutral = runtime.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId);
            int countAfterNeutralRead = runtime.Count;
            AttitudeMutationResult forward = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.forward",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.TrustId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 30,
                worldTime = 1d
            });
            AttitudeMutationResult reverse = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.reverse",
                observerPersonId = "person.prototype.friend",
                subjectPersonId = PersistenceService.LocalPlayerId,
                dimensionId = PrototypeAttitudeDefinitionFactory.HostilityId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 15,
                worldTime = 2d
            });

            Assert.That(neutral.EffectiveValue, Is.EqualTo(0));
            Assert.That(neutral.RecordExists, Is.False);
            Assert.That(countAfterNeutralRead, Is.EqualTo(0));
            Assert.That(forward.Succeeded, Is.True, forward.Message);
            Assert.That(reverse.Succeeded, Is.True, reverse.Message);
            Assert.That(runtime.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue, Is.EqualTo(30));
            Assert.That(runtime.ResolveValue("person.prototype.friend", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.TrustId).EffectiveValue, Is.EqualTo(0));
            Assert.That(runtime.ResolveValue("person.prototype.friend", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue, Is.EqualTo(15));
            Assert.That(runtime.QueryByObserver(PersistenceService.LocalPlayerId).Count, Is.EqualTo(1));
            Assert.That(runtime.QueryBySubject(PersistenceService.LocalPlayerId).Count, Is.EqualTo(1));
        }

        [Test]
        public void PreviewDuplicateAndFailedContributionDoNotApplyExtraMutation()
        {
            InterpersonalAttitudeRuntime runtime = CreateRuntime();
            AttitudeMutationRequest request = new AttitudeMutationRequest
            {
                transactionId = "attitude.test.contribution",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.rival",
                dimensionId = PrototypeAttitudeDefinitionFactory.HostilityId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                sourceId = "source.test.rivalry",
                sourceCategory = AttitudeContributionSourceCategory.TestLab,
                value = 120,
                worldTime = 3d,
                preview = true
            };

            AttitudeMutationResult preview = runtime.Mutate(request);
            request.preview = false;
            AttitudeMutationResult execute = runtime.Mutate(request);
            AttitudeMutationResult duplicate = runtime.Mutate(request);
            AttitudeMutationResult rejected = runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.invalid-source",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.mentor",
                dimensionId = PrototypeAttitudeDefinitionFactory.RespectId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                value = 20
            });

            AttitudeEffectiveValueSnapshot hostility = runtime.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId);
            Assert.That(preview.Status, Is.EqualTo(AttitudeOperationStatus.Preview));
            Assert.That(preview.Preview, Is.True);
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Status, Is.EqualTo(AttitudeOperationStatus.Duplicate));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Status, Is.EqualTo(AttitudeOperationStatus.InvalidSource));
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(hostility.EffectiveValue, Is.EqualTo(100));
            Assert.That(hostility.Clamped, Is.True);
            Assert.That(hostility.Contributions.Count, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotsAndQueriesAreImmutableAndDeterministic()
        {
            InterpersonalAttitudeRuntime runtime = CreateRuntime();
            runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.respect",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.mentor",
                dimensionId = PrototypeAttitudeDefinitionFactory.RespectId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 45,
                worldTime = 4d
            });
            runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.loyalty-source",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.mentor",
                dimensionId = PrototypeAttitudeDefinitionFactory.LoyaltyId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                sourceId = "source.test.training",
                sourceCategory = AttitudeContributionSourceCategory.HistoricalEvent,
                historicalEventId = "history.test.training",
                value = 25,
                worldTime = 5d
            });

            InterpersonalAttitudeSnapshot snapshot = runtime.Snapshots.Single();
            snapshot.Data.observerPersonId = "person.mutated";
            snapshot.Data.dimensions.Single(item => item.dimensionId == PrototypeAttitudeDefinitionFactory.RespectId).baselineValue = -99;
            snapshot.Data.dimensions.Single(item => item.dimensionId == PrototypeAttitudeDefinitionFactory.LoyaltyId).contributions[0].amount = -99;

            InterpersonalAttitudeSnapshot fresh = runtime.Snapshots.Single();
            Assert.That(fresh.ObserverPersonId, Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(runtime.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.mentor", PrototypeAttitudeDefinitionFactory.RespectId).EffectiveValue, Is.EqualTo(45));
            Assert.That(runtime.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.mentor", PrototypeAttitudeDefinitionFactory.LoyaltyId).EffectiveValue, Is.EqualTo(25));
            Assert.That(runtime.QueryByHistoricalEvent("history.test.training").Count, Is.EqualTo(1));
            Assert.That(runtime.QueryByThreshold(PrototypeAttitudeDefinitionFactory.RespectId, AttitudeThresholdComparison.GreaterThanOrEqual, 40).Count, Is.EqualTo(1));
            Assert.That(runtime.QueryModifiedBetween(4d, 5d).Select(item => item.RecordId).ToArray(), Is.EqualTo(runtime.QueryModifiedBetween(4d, 5d).Select(item => item.RecordId).ToArray()));
        }

        [Test]
        public void RelationshipsCanReferenceAttitudesWithoutOwningOrDeletingThem()
        {
            DefinitionRegistry registry = CreateRegistry();
            RelationshipRuntime relationships = new RelationshipRuntime();
            relationships.Configure(registry, KnownPersons);
            InterpersonalAttitudeRuntime attitudes = CreateRuntime(registry);

            RelationshipOperationResult relationship = relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.friend-attitude-source",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 6d
            });
            AttitudeMutationResult attitude = attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.relationship-source",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.AffectionId,
                mutationKind = AttitudeMutationKind.AddOrReplaceContribution,
                sourceId = "source.test.friendship",
                sourceCategory = AttitudeContributionSourceCategory.Relationship,
                relationshipRecordId = relationship.Snapshot.RecordId,
                value = 35,
                worldTime = 7d
            });
            RelationshipOperationResult ended = relationships.EndRelationship(new RelationshipEndRequest
            {
                recordId = relationship.Snapshot.RecordId,
                endWorldTime = 8d
            });

            Assert.That(relationship.Succeeded, Is.True, relationship.Message);
            Assert.That(attitude.Succeeded, Is.True, attitude.Message);
            Assert.That(ended.Succeeded, Is.True, ended.Message);
            Assert.That(relationships.QueryBetween(PersistenceService.LocalPlayerId, "person.prototype.friend", activeOnly: true), Is.Empty);
            Assert.That(attitudes.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.AffectionId).EffectiveValue, Is.EqualTo(35));
            Assert.That(attitudes.Count, Is.EqualTo(1));
        }

        [Test]
        public void PersistenceParticipantRejectsInvalidRestoreWithoutMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            InterpersonalAttitudeRuntime runtime = CreateRuntime(registry);
            runtime.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.persisted",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.rival",
                dimensionId = PrototypeAttitudeDefinitionFactory.HostilityId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 60,
                worldTime = 9d
            });
            InterpersonalAttitudePersistenceParticipant participant = new InterpersonalAttitudePersistenceParticipant(runtime, () => registry, () => KnownPersons);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            InterpersonalAttitudeRuntimeSaveData corrupt = JsonUtility.FromJson<InterpersonalAttitudeRuntimeSaveData>(save.PayloadJson);
            corrupt.records[0].dimensions[0].baselineValue = 999;

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), InterpersonalAttitudePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(runtime.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.rival", PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue, Is.EqualTo(60));
        }

        private static InterpersonalAttitudeRuntime CreateRuntime(DefinitionRegistry registry = null)
        {
            InterpersonalAttitudeRuntime runtime = new InterpersonalAttitudeRuntime();
            runtime.Configure(registry ?? CreateRegistry(), KnownPersons);
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry()));
        }
    }
}
#endif
