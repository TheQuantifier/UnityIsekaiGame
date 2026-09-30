#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class SocialNormsEtiquetteContextualExpectationsTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";

        private static readonly string[] KnownPersons =
        {
            PersistenceService.LocalPlayerId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.mentor",
            "person.prototype.student",
            "person.prototype.listener"
        };

        [Test]
        public void PrototypeSocialNormDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeSocialNormDefinitionFactory.HostGreetingNormId, out SocialNormDefinition greeting), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialNormDefinitionFactory.PromiseKeepingNormId, out SocialNormDefinition promise), Is.True);
            Assert.That(greeting.ExpectedInteractionDefinitionId, Is.EqualTo(PrototypeSocialInteractionDefinitionFactory.GreetId));
            Assert.That(promise.ExpectedPromiseState, Is.EqualTo(SocialPromiseStatus.Breached.ToString()));

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (SocialNormDefinition definition in PrototypeSocialNormDefinitionFactory.CreateDefinitions())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void PreviewExecuteDuplicateAndSnapshotsAreImmutable()
        {
            using TestFixture fixture = CreateFixture();
            SocialNormEvaluationRequest request = Request("norm.tx.greeting", PrototypeSocialInteractionDefinitionFactory.GreetId, PrototypeSocialNormDefinitionFactory.HostGreetingNormId);

            SocialNormEvaluationResult preview = fixture.Norms.Preview(request);
            SocialNormEvaluationResult execute = fixture.Norms.Execute(request);
            SocialNormEvaluationResult duplicate = fixture.Norms.Execute(request);
            SocialNormAssessmentSnapshot snapshot = execute.Assessments[0];
            snapshot.Data.actorPersonId = "person.mutated";
            snapshot.Data.conditions = Array.Empty<SocialNormConditionEvaluationData>();

            Assert.That(preview.Status, Is.EqualTo(SocialNormOperationStatus.Preview), preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Status, Is.EqualTo(SocialNormOperationStatus.Duplicate), duplicate.Message);
            Assert.That(fixture.Norms.Count, Is.EqualTo(1));
            Assert.That(fixture.Norms.TryGetSnapshot(execute.Assessments[0].AssessmentRecordId, out SocialNormAssessmentSnapshot stored), Is.True);
            Assert.That(stored.ActorPersonId, Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(stored.Data.conditions.Length, Is.GreaterThan(0));
        }

        [Test]
        public void PublicAndPrivateInsultsApplyDifferentConsequences()
        {
            using TestFixture fixture = CreateFixture();

            SocialNormEvaluationResult privateInsult = fixture.Norms.Execute(Request(
                "norm.tx.private-insult",
                PrototypeSocialInteractionDefinitionFactory.InsultId,
                PrototypeSocialNormDefinitionFactory.PrivateInsultNormId,
                visibility: SocialInteractionVisibility.Private));
            SocialNormEvaluationResult publicInsult = fixture.Norms.Execute(Request(
                "norm.tx.public-insult",
                PrototypeSocialInteractionDefinitionFactory.InsultId,
                PrototypeSocialNormDefinitionFactory.PublicInsultNormId,
                witnesses: new[] { "person.prototype.rival" },
                visibility: SocialInteractionVisibility.Public,
                classification: SocialNormAssessmentClassification.Violation));

            Assert.That(privateInsult.Succeeded, Is.True, privateInsult.Message);
            Assert.That(publicInsult.Succeeded, Is.True, publicInsult.Message);
            Assert.That(publicInsult.Assessments[0].Severity, Is.GreaterThan(privateInsult.Assessments[0].Severity));
            Assert.That(privateInsult.Assessments[0].Consequences.Any(item => item.targetRuntime == SocialNormConsequenceTargetRuntime.InterpersonalAttitude && item.committed), Is.True);
            Assert.That(publicInsult.Assessments[0].Consequences.Any(item => item.targetRuntime == SocialNormConsequenceTargetRuntime.Reputation && item.committed), Is.True);
            Assert.That(fixture.Norms.QueryByObserver("person.prototype.rival").Count, Is.EqualTo(1));
        }

        [Test]
        public void ActorKnowledgeAndExceptionMitigationRemainExplicit()
        {
            using TestFixture fixture = CreateFixture();

            SocialNormEvaluationResult result = fixture.Norms.Execute(Request(
                "norm.tx.ignorance",
                PrototypeSocialInteractionDefinitionFactory.CustomActionId,
                PrototypeSocialNormDefinitionFactory.IgnoranceMitigatedEtiquetteNormId,
                witnesses: new[] { "person.prototype.friend" },
                tags: new[] { "culture.prototype.formal", "actor-unaware" },
                actorKnowledge: SocialNormActorKnowledgeState.Unknown,
                classification: SocialNormAssessmentClassification.Violation));
            SocialNormAssessmentSnapshot assessment = result.Assessments[0];

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(assessment.ActorKnowledge, Is.EqualTo(SocialNormActorKnowledgeState.Unknown));
            Assert.That(assessment.Classification, Is.EqualTo(SocialNormAssessmentClassification.MinorViolation));
            Assert.That(assessment.Data.exceptions.Any(item => item.applied && item.effect == SocialNormExceptionEffect.ReduceSeverity), Is.True);
            Assert.That(assessment.Observers.Any(item => item.observerPersonId == "person.prototype.friend"), Is.True);
        }

        [Test]
        public void ConflictResolutionSuppressesLowerPrecedenceNorm()
        {
            using TestFixture fixture = CreateFixture();

            SocialNormEvaluationResult result = fixture.Norms.Execute(Request(
                "norm.tx.conflict",
                PrototypeSocialInteractionDefinitionFactory.PublicPraiseId,
                new[] { PrototypeSocialNormDefinitionFactory.PraiseEnemyConflictNormId, PrototypeSocialNormDefinitionFactory.HospitalityOverrideNormId },
                witnesses: new[] { "person.prototype.friend" },
                tags: new[] { "audience.enemy-of-target", "hospitality-duty", "actor-role.host", "target-role.rival" },
                visibility: SocialInteractionVisibility.Public,
                classification: SocialNormAssessmentClassification.Satisfied));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Assessments.Any(item => item.Applicability == SocialNormApplicabilityStatus.SuppressedByConflict), Is.True);
            Assert.That(result.Assessments.Any(item => item.Conflicts.Count > 0), Is.True);
        }

        [Test]
        public void PromiseBreachUsesInteractionPromiseStateWithoutOwningPromises()
        {
            using TestFixture fixture = CreateFixture();
            string promiseId = "social-promise.test.norm-breach";

            SocialNormEvaluationResult result = fixture.Norms.Execute(Request(
                "norm.tx.promise-breach",
                PrototypeSocialInteractionDefinitionFactory.PromiseId,
                PrototypeSocialNormDefinitionFactory.PromiseKeepingNormId,
                promiseId: promiseId,
                tags: new[] { "promise-context", "promise-state.Breached" },
                classification: SocialNormAssessmentClassification.Violation));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Assessments[0].PromiseId, Is.EqualTo(promiseId));
            Assert.That(result.Assessments[0].Classification, Is.EqualTo(SocialNormAssessmentClassification.Violation));
            Assert.That(fixture.Interactions.PromiseCount, Is.Zero);
        }

        [Test]
        public void PersistenceRoundTripAndInvalidRestoreRejectWithoutMutation()
        {
            using TestFixture fixture = CreateFixture();
            SocialNormEvaluationResult execute = fixture.Norms.Execute(Request("norm.tx.persist", PrototypeSocialInteractionDefinitionFactory.GreetId, PrototypeSocialNormDefinitionFactory.HostGreetingNormId));
            SocialNormPersistenceParticipant participant = new SocialNormPersistenceParticipant(fixture.Norms, () => fixture.Registry, () => KnownPersons);

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialNormRuntimeSaveData saveData = JsonUtility.FromJson<SocialNormRuntimeSaveData>(save.PayloadJson);
            SocialNormRuntime restored = new SocialNormRuntime();
            restored.Configure(fixture.Registry, KnownPersons, fixture.Relationships, fixture.Attitudes, fixture.Reputation, fixture.Rumors, fixture.Interactions);
            SocialNormEvaluationResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, KnownPersons, restoringState: true);
            SocialNormRuntimeSaveData corrupt = saveData.Clone();
            corrupt.assessments[0].normDefinitionId = "social-norm.prototype.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialNormPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Norms.Count, Is.EqualTo(1));
        }

        private static SocialNormEvaluationRequest Request(
            string transactionId,
            string interactionDefinitionId,
            string normDefinitionId,
            string target = "person.prototype.friend",
            string[] witnesses = null,
            string[] tags = null,
            SocialInteractionVisibility visibility = SocialInteractionVisibility.Private,
            SocialNormAssessmentClassification classification = SocialNormAssessmentClassification.Unknown,
            SocialNormActorKnowledgeState actorKnowledge = SocialNormActorKnowledgeState.Knew,
            string promiseId = "")
        {
            return Request(transactionId, interactionDefinitionId, new[] { normDefinitionId }, target, witnesses, tags, visibility, classification, actorKnowledge, promiseId);
        }

        private static SocialNormEvaluationRequest Request(
            string transactionId,
            string interactionDefinitionId,
            string[] normDefinitionIds,
            string target = "person.prototype.friend",
            string[] witnesses = null,
            string[] tags = null,
            SocialInteractionVisibility visibility = SocialInteractionVisibility.Private,
            SocialNormAssessmentClassification classification = SocialNormAssessmentClassification.Unknown,
            SocialNormActorKnowledgeState actorKnowledge = SocialNormActorKnowledgeState.Knew,
            string promiseId = "")
        {
            return new SocialNormEvaluationRequest
            {
                TransactionId = transactionId,
                AssessmentRecordId = $"{transactionId}.assessment",
                ActorPersonId = PersistenceService.LocalPlayerId,
                TargetPersonId = target,
                InteractionRecordId = $"{transactionId}.interaction",
                InteractionDefinitionId = interactionDefinitionId,
                PromiseId = promiseId,
                Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Person, subjectId = target, ownerPersonId = target },
                PlaceId = "place.prototype.test-lab",
                AudienceId = PrototypeReputationDefinitionFactory.GlobalPublicAudienceId,
                WitnessPersonIds = witnesses ?? Array.Empty<string>(),
                ContextTags = tags ?? Array.Empty<string>(),
                RequestedNormIds = normDefinitionIds,
                Visibility = visibility,
                Channel = SocialInteractionCommunicationChannel.Conversation,
                ConductClassification = classification,
                ActorKnowledge = actorKnowledge,
                OccurrenceWorldTime = 1d,
                EvaluationWorldTime = 1d,
                DeterministicSeed = "social-norm-tests"
            };
        }

        private static SocialInteractionRequest InteractionRequest(string definitionId, string transactionId)
        {
            return new SocialInteractionRequest
            {
                TransactionId = transactionId,
                InteractionDefinitionId = definitionId,
                InitiatorPersonId = PersistenceService.LocalPlayerId,
                TargetPersonId = "person.prototype.friend",
                PlaceId = "place.prototype.test-lab",
                Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Person, subjectId = "person.prototype.friend" },
                Channel = SocialInteractionCommunicationChannel.Conversation,
                WorldTime = 1d,
                DeterministicSeed = "social-norm-tests"
            };
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            GameObject owner = new GameObject("Social norm test knowledge runtime");
            PersonKnowledgeRuntime knowledge = owner.AddComponent<PersonKnowledgeRuntime>();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            RelationshipRuntime relationships = new RelationshipRuntime();
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            ReputationRuntime reputation = new ReputationRuntime();
            RumorRuntime rumors = new RumorRuntime();
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();
            SocialNormRuntime norms = new SocialNormRuntime();

            history.Configure(registry, PersistenceService.LocalWorldId, KnownPersons, Array.Empty<string>());
            knowledge.Configure(registry, PersistenceService.LocalPlayerId);
            memory.Configure(PersistenceService.LocalPlayerId, registry, history, KnownPersons);
            relationships.Configure(registry, KnownPersons);
            attitudes.Configure(registry, KnownPersons);
            reputation.Configure(registry, KnownPersons);
            rumors.Configure(
                registry,
                KnownPersons,
                personId => string.Equals(personId, PersistenceService.LocalPlayerId, StringComparison.Ordinal) ? knowledge : null,
                personId => string.Equals(personId, PersistenceService.LocalPlayerId, StringComparison.Ordinal) ? memory : null);
            interactions.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors);
            norms.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors, interactions);

            return new TestFixture(registry, owner, relationships, attitudes, reputation, rumors, interactions, norms);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeSocialNormDefinitionFactory.AddMissingPrototypeSocialNormDefinitions(
                PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                    PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                        PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                            PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                                PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry()))))));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, GameObject owner, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, ReputationRuntime reputation, RumorRuntime rumors, SocialInteractionRuntime interactions, SocialNormRuntime norms)
            {
                Registry = registry;
                Owner = owner;
                Relationships = relationships;
                Attitudes = attitudes;
                Reputation = reputation;
                Rumors = rumors;
                Interactions = interactions;
                Norms = norms;
            }

            public DefinitionRegistry Registry { get; }
            public GameObject Owner { get; }
            public RelationshipRuntime Relationships { get; }
            public InterpersonalAttitudeRuntime Attitudes { get; }
            public ReputationRuntime Reputation { get; }
            public RumorRuntime Rumors { get; }
            public SocialInteractionRuntime Interactions { get; }
            public SocialNormRuntime Norms { get; }

            public void Dispose()
            {
                Norms.Dispose();
                Interactions.Dispose();
                Rumors.Dispose();
                UnityEngine.Object.DestroyImmediate(Owner);
            }
        }
    }
}
#endif
