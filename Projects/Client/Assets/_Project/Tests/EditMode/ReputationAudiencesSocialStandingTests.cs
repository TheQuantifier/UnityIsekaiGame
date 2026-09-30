#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;

namespace UnityIsekaiGame.Tests
{
    public sealed class ReputationAudiencesSocialStandingTests
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
        public void PrototypeReputationDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, out ReputationAudienceDefinition global), Is.True);
            Assert.That(registry.TryGet(PrototypeReputationDefinitionFactory.PrototypeTownAudienceId, out ReputationAudienceDefinition town), Is.True);
            Assert.That(registry.TryGet(PrototypeReputationDefinitionFactory.RenownId, out ReputationDimensionDefinition renown), Is.True);
            Assert.That(registry.TryGet(PrototypeReputationDefinitionFactory.EsteemId, out ReputationDimensionDefinition esteem), Is.True);
            Assert.That(global.Scope, Is.EqualTo(ReputationAudienceScope.Global));
            Assert.That(town.ParentAudienceId, Is.EqualTo(PrototypeReputationDefinitionFactory.GlobalPublicAudienceId));
            Assert.That(renown.NegativeValuesAllowed, Is.False);
            Assert.That(esteem.MinimumValue, Is.EqualTo(-100));

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (ReputationAudienceDefinition definition in PrototypeReputationDefinitionFactory.CreateDefinitions().OfType<ReputationAudienceDefinition>())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            foreach (ReputationDimensionDefinition definition in PrototypeReputationDefinitionFactory.CreateDefinitions().OfType<ReputationDimensionDefinition>())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            Assert.That(ReputationRuntime.ValidateAudienceGraph(registry, out string failure), Is.True, failure);
        }

        [Test]
        public void RecordsAreStableAudienceSpecificAndNeutralReadsDoNotMutate()
        {
            ReputationRuntime runtime = CreateRuntime();

            ReputationEffectiveValueSnapshot neutral = runtime.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.EsteemId);
            int countAfterNeutral = runtime.Count;
            ReputationMutationResult global = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.global-esteem",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.EsteemId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 25,
                worldTime = 1d
            });
            ReputationMutationResult local = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.local-esteem",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.EsteemId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = -20,
                worldTime = 2d
            });

            Assert.That(neutral.EffectiveValue, Is.EqualTo(0));
            Assert.That(neutral.RecordExists, Is.False);
            Assert.That(countAfterNeutral, Is.EqualTo(0));
            Assert.That(global.Succeeded, Is.True, global.Message);
            Assert.That(local.Succeeded, Is.True, local.Message);
            Assert.That(global.RecordId, Is.Not.EqualTo(local.RecordId));
            Assert.That(runtime.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.EsteemId).EffectiveValue, Is.EqualTo(25));
            Assert.That(runtime.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.PrototypeTownAudienceId, PrototypeReputationDefinitionFactory.EsteemId).EffectiveValue, Is.EqualTo(-20));
            Assert.That(runtime.QueryBySubject(PersistenceService.LocalPlayerId).Count, Is.EqualTo(2));
            Assert.That(runtime.QueryByAudience(PrototypeReputationDefinitionFactory.PrototypeTownAudienceId).Count, Is.EqualTo(1));
        }

        [Test]
        public void ContributionsPreviewDuplicateDisputeAndRemovalAreDeterministic()
        {
            ReputationRuntime runtime = CreateRuntime();
            ReputationMutationRequest request = new ReputationMutationRequest
            {
                transactionId = "reputation.test.contribution",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.NotorietyId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                sourceId = "source.reputation.accusation",
                sourceCategory = ReputationContributionSourceCategory.Accusation,
                authenticity = ReputationAuthenticity.Disputed,
                historicalEventId = "event.prototype.accusation",
                value = 120,
                worldTime = 3d,
                preview = true
            };

            ReputationMutationResult preview = runtime.Mutate(request);
            request.preview = false;
            ReputationMutationResult execute = runtime.Mutate(request);
            ReputationMutationResult duplicate = runtime.Mutate(request);
            ReputationMutationResult second = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.verified",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.NotorietyId,
                mutationKind = ReputationMutationKind.AddOrReplaceContribution,
                sourceId = "source.reputation.conviction",
                sourceCategory = ReputationContributionSourceCategory.Conviction,
                authenticity = ReputationAuthenticity.Verified,
                value = 15,
                worldTime = 4d
            });
            ReputationMutationResult removed = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.remove-accusation",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.NotorietyId,
                mutationKind = ReputationMutationKind.RemoveContribution,
                sourceId = "source.reputation.accusation",
                worldTime = 5d
            });

            ReputationEffectiveValueSnapshot notoriety = runtime.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.RoyalJurisdictionAudienceId, PrototypeReputationDefinitionFactory.NotorietyId);
            Assert.That(preview.Status, Is.EqualTo(ReputationOperationStatus.Preview));
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Status, Is.EqualTo(ReputationOperationStatus.Duplicate));
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(removed.Succeeded, Is.True, removed.Message);
            Assert.That(runtime.QueryByHistoricalEvent("event.prototype.accusation"), Is.Empty);
            Assert.That(notoriety.EffectiveValue, Is.EqualTo(15));
            Assert.That(notoriety.Contributions.Single().Authenticity, Is.EqualTo(ReputationAuthenticity.Verified));
        }

        [Test]
        public void HierarchyThresholdsAndRenownRemainDistinctFromEsteem()
        {
            ReputationRuntime runtime = CreateRuntime();
            runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.guild-renown",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.RenownId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 90,
                worldTime = 6d
            });
            runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.guild-esteem",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.AdventurersGuildAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.EsteemId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = -45,
                worldTime = 7d
            });

            ReputationEffectiveValueSnapshot inheritedRenown = runtime.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.AdventurersGuildVeteransAudienceId, PrototypeReputationDefinitionFactory.RenownId, allowInherited: true);
            ReputationEffectiveValueSnapshot directRenown = runtime.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.AdventurersGuildVeteransAudienceId, PrototypeReputationDefinitionFactory.RenownId, allowInherited: false);
            ReputationThresholdResult threshold = runtime.EvaluateThreshold(new ReputationThresholdRequest
            {
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.AdventurersGuildVeteransAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.EsteemId,
                comparison = ReputationThresholdComparison.LessThanOrEqual,
                value = -40,
                allowInherited = true,
                minimumRenown = 50
            });

            Assert.That(inheritedRenown.EffectiveValue, Is.EqualTo(90));
            Assert.That(inheritedRenown.Inherited, Is.True);
            Assert.That(directRenown.EffectiveValue, Is.EqualTo(0));
            Assert.That(threshold.Passed, Is.True, threshold.Message);
            Assert.That(threshold.Inherited, Is.True);
            Assert.That(threshold.RenownValue, Is.EqualTo(90));
        }

        [Test]
        public void RelationshipsAndAttitudesRemainSeparateFromReputation()
        {
            DefinitionRegistry registry = CreateRegistry();
            RelationshipRuntime relationships = new RelationshipRuntime();
            relationships.Configure(registry, KnownPersons);
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            attitudes.Configure(registry, KnownPersons);
            ReputationRuntime reputation = CreateRuntime(registry);

            relationships.CreateRelationship(new RelationshipCreateRequest
            {
                recordId = "relationship.test.reputation-separation",
                relationshipDefinitionId = PrototypeRelationshipDefinitionFactory.FriendRelationshipId,
                firstPersonId = PersistenceService.LocalPlayerId,
                firstRoleId = "friend",
                secondPersonId = "person.prototype.friend",
                secondRoleId = "friend",
                startWorldTime = 8d
            });
            attitudes.Mutate(new AttitudeMutationRequest
            {
                transactionId = "attitude.test.reputation-separation",
                observerPersonId = PersistenceService.LocalPlayerId,
                subjectPersonId = "person.prototype.friend",
                dimensionId = PrototypeAttitudeDefinitionFactory.TrustId,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = 40,
                worldTime = 9d
            });
            reputation.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.separation",
                subjectPersonId = "person.prototype.friend",
                audienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.RenownId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 55,
                worldTime = 10d
            });

            Assert.That(relationships.Count, Is.EqualTo(1));
            Assert.That(attitudes.Count, Is.EqualTo(1));
            Assert.That(reputation.Count, Is.EqualTo(1));
            Assert.That(reputation.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.RenownId).EffectiveValue, Is.EqualTo(0));
        }

        [Test]
        public void PersistenceParticipantRoundTripsAndRejectsInvalidRestoreWithoutMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            ReputationRuntime runtime = CreateRuntime(registry);
            ReputationMutationResult create = runtime.Mutate(new ReputationMutationRequest
            {
                transactionId = "reputation.test.persisted",
                subjectPersonId = PersistenceService.LocalPlayerId,
                audienceId = PrototypeReputationDefinitionFactory.PrototypeTownAudienceId,
                dimensionId = PrototypeReputationDefinitionFactory.CredibilityId,
                mutationKind = ReputationMutationKind.SetBaseline,
                value = 35,
                worldTime = 11d
            });
            ReputationPersistenceParticipant participant = new ReputationPersistenceParticipant(runtime, () => registry, () => KnownPersons);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            ReputationRuntimeSaveData saveData = JsonUtility.FromJson<ReputationRuntimeSaveData>(save.PayloadJson);
            ReputationRuntime restored = CreateRuntime(registry);
            ReputationMutationResult restore = restored.RestoreFromSaveData(saveData, registry, KnownPersons, restoringState: true);
            ReputationRuntimeSaveData corrupt = saveData.Clone();
            corrupt.records[0].dimensions[0].baselineValue = 999;
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), ReputationPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetSnapshot(create.RecordId, out ReputationSnapshot snapshot), Is.True);
            Assert.That(snapshot.SubjectPersonId, Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(restored.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.PrototypeTownAudienceId, PrototypeReputationDefinitionFactory.CredibilityId).EffectiveValue, Is.EqualTo(35));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(runtime.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.PrototypeTownAudienceId, PrototypeReputationDefinitionFactory.CredibilityId).EffectiveValue, Is.EqualTo(35));
        }

        private static ReputationRuntime CreateRuntime(DefinitionRegistry registry = null)
        {
            ReputationRuntime runtime = new ReputationRuntime();
            runtime.Configure(registry ?? CreateRegistry(), KnownPersons);
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                    PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry())));
        }
    }
}
#endif
