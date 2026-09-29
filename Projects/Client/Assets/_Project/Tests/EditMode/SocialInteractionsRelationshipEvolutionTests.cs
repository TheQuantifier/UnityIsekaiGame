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
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class SocialInteractionsRelationshipEvolutionTests
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
        public void PrototypeSocialInteractionDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeSocialInteractionDefinitionFactory.GreetId, out SocialInteractionDefinition greet), Is.True);
            Assert.That(registry.TryGet(PrototypeSocialInteractionDefinitionFactory.PromiseId, out SocialInteractionDefinition promise), Is.True);
            Assert.That(greet.RequiresRole(SocialInteractionRole.Initiator), Is.True);
            Assert.That(greet.RequiresRole(SocialInteractionRole.Target), Is.True);
            Assert.That(promise.RequiresResponse, Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (SocialInteractionDefinition definition in PrototypeSocialInteractionDefinitionFactory.CreateDefinitions())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void PreviewExecuteDuplicateAndSnapshotsAreImmutable()
        {
            using TestFixture fixture = CreateFixture();
            SocialInteractionRequest request = Request(PrototypeSocialInteractionDefinitionFactory.GreetId, "interaction.tx.greet", worldTime: 1d);

            SocialInteractionResult preview = fixture.Interactions.Preview(request);
            SocialInteractionResult execute = fixture.Interactions.Execute(request);
            SocialInteractionResult duplicate = fixture.Interactions.Execute(request);
            SocialInteractionSnapshot snapshot = execute.Record;
            int originalConsequenceCount = snapshot.Consequences.Count;
            snapshot.Data.initiatorPersonId = "person.mutated";
            snapshot.Data.consequences = Array.Empty<SocialConsequenceRecordData>();

            Assert.That(preview.Status, Is.EqualTo(SocialInteractionStatus.Preview), preview.Message);
            Assert.That(fixture.Interactions.Count, Is.EqualTo(1));
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Status, Is.EqualTo(SocialInteractionStatus.Duplicate), duplicate.Message);
            Assert.That(fixture.Interactions.TryGetSnapshot(execute.Record.InteractionRecordId, out SocialInteractionSnapshot stored), Is.True);
            Assert.That(stored.InitiatorPersonId, Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(stored.Consequences.Count, Is.EqualTo(originalConsequenceCount));
        }

        [Test]
        public void ComplimentAndInsultMutateDirectedAttitudesOnly()
        {
            using TestFixture fixture = CreateFixture();

            SocialInteractionResult compliment = fixture.Interactions.Execute(Request(PrototypeSocialInteractionDefinitionFactory.ComplimentId, "interaction.tx.compliment", "person.prototype.friend", 10d));
            SocialInteractionResult insult = fixture.Interactions.Execute(Request(PrototypeSocialInteractionDefinitionFactory.InsultId, "interaction.tx.insult", "person.prototype.friend", 20d));

            Assert.That(compliment.Succeeded, Is.True, compliment.Message);
            Assert.That(insult.Succeeded, Is.True, insult.Message);
            Assert.That(fixture.Attitudes.ResolveValue("person.prototype.friend", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.AffectionId).EffectiveValue, Is.EqualTo(-2));
            Assert.That(fixture.Attitudes.ResolveValue("person.prototype.friend", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.HostilityId).EffectiveValue, Is.EqualTo(15));
            Assert.That(fixture.Attitudes.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.friend", PrototypeAttitudeDefinitionFactory.AffectionId).EffectiveValue, Is.EqualTo(0));
        }

        [Test]
        public void PendingPromiseAcceptanceCreatesPromiseWithoutPreviewMutation()
        {
            using TestFixture fixture = CreateFixture();
            SocialInteractionResult pending = fixture.Interactions.Execute(Request(PrototypeSocialInteractionDefinitionFactory.PromiseId, "interaction.tx.promise", "person.prototype.friend", 30d));

            SocialInteractionResult preview = fixture.Interactions.RespondToPending("interaction.tx.promise.accept.preview", pending.Pending.PendingInteractionId, SocialInteractionResponse.Accept, 32d, preview: true);
            SocialInteractionResult accepted = fixture.Interactions.RespondToPending("interaction.tx.promise.accept", pending.Pending.PendingInteractionId, SocialInteractionResponse.Accept, 33d);

            Assert.That(pending.Status, Is.EqualTo(SocialInteractionStatus.Pending), pending.Message);
            Assert.That(preview.Status, Is.EqualTo(SocialInteractionStatus.Preview), preview.Message);
            Assert.That(fixture.Interactions.PromiseCount, Is.EqualTo(1));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(accepted.Record.Outcome, Is.EqualTo(SocialInteractionOutcome.Accepted));
            Assert.That(accepted.Promise.Status, Is.EqualTo(SocialPromiseStatus.Active));
            Assert.That(fixture.Interactions.TryGetPending(pending.Pending.PendingInteractionId, out SocialPendingInteractionSnapshot resolved), Is.True);
            Assert.That(resolved.Status, Is.EqualTo(SocialInteractionStatus.Succeeded));
        }

        [Test]
        public void PublicAndWitnessedInteractionsDelegateToReputation()
        {
            using TestFixture fixture = CreateFixture();

            SocialInteractionResult praise = fixture.Interactions.Execute(Request(PrototypeSocialInteractionDefinitionFactory.PublicPraiseId, "interaction.tx.praise", "person.prototype.friend", 40d, visibility: SocialInteractionVisibility.Public));
            SocialInteractionResult threat = fixture.Interactions.Execute(Request(PrototypeSocialInteractionDefinitionFactory.ThreatenId, "interaction.tx.threat", "person.prototype.friend", 50d, witnesses: new[] { "person.prototype.rival" }, visibility: SocialInteractionVisibility.Witnessed));

            Assert.That(praise.Succeeded, Is.True, praise.Message);
            Assert.That(threat.Succeeded, Is.True, threat.Message);
            Assert.That(fixture.Reputation.ResolveValue("person.prototype.friend", PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.EsteemId).EffectiveValue, Is.EqualTo(12));
            Assert.That(fixture.Reputation.ResolveValue(PersistenceService.LocalPlayerId, PrototypeReputationDefinitionFactory.GlobalPublicAudienceId, PrototypeReputationDefinitionFactory.PerceivedDangerId).EffectiveValue, Is.EqualTo(15));
        }

        [Test]
        public void ShareInformationDelegatesToRumorRuntime()
        {
            using TestFixture fixture = CreateFixture();
            RumorOperationResult rumor = CreateRumor(fixture.Rumors, "rumor.test.social-share", "rumor.tx.social-share.create", PersistenceService.LocalPlayerId);
            SocialInteractionRequest request = Request(PrototypeSocialInteractionDefinitionFactory.ShareInformationId, "interaction.tx.share-rumor", "person.prototype.friend", 60d);
            request.Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Rumor, subjectId = rumor.Rumor.RumorId };

            SocialInteractionResult shared = fixture.Interactions.Execute(request);

            Assert.That(rumor.Succeeded, Is.True, rumor.Message);
            Assert.That(shared.Succeeded, Is.True, shared.Message);
            Assert.That(fixture.Rumors.TransmissionCount, Is.EqualTo(1));
            Assert.That(fixture.Rumors.IsAware("person.prototype.friend", rumor.Rumor.RumorId), Is.True);
        }

        [Test]
        public void PersistenceRoundTripAndInvalidRestoreRejectWithoutMutation()
        {
            using TestFixture fixture = CreateFixture();
            SocialInteractionResult execute = fixture.Interactions.Execute(Request(PrototypeSocialInteractionDefinitionFactory.ThankId, "interaction.tx.persist", "person.prototype.friend", 70d));
            SocialInteractionPersistenceParticipant participant = new SocialInteractionPersistenceParticipant(fixture.Interactions, () => fixture.Registry, () => KnownPersons);

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            SocialInteractionRuntimeSaveData saveData = JsonUtility.FromJson<SocialInteractionRuntimeSaveData>(save.PayloadJson);
            SocialInteractionRuntime restored = new SocialInteractionRuntime();
            restored.Configure(fixture.Registry, KnownPersons, fixture.Relationships, fixture.Attitudes, fixture.Reputation, fixture.Rumors);
            SocialInteractionResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, KnownPersons, restoringState: true);
            SocialInteractionRuntimeSaveData corrupt = saveData.Clone();
            corrupt.records[0].interactionDefinitionId = "social-interaction.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), SocialInteractionPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Interactions.Count, Is.EqualTo(1));
        }

        private static SocialInteractionRequest Request(
            string definitionId,
            string transactionId,
            string target = "person.prototype.friend",
            double worldTime = 0d,
            string[] witnesses = null,
            SocialInteractionVisibility? visibility = null)
        {
            return new SocialInteractionRequest
            {
                TransactionId = transactionId,
                InteractionDefinitionId = definitionId,
                InitiatorPersonId = PersistenceService.LocalPlayerId,
                TargetPersonId = target,
                WitnessPersonIds = witnesses ?? Array.Empty<string>(),
                AudienceId = visibility == SocialInteractionVisibility.Public ? PrototypeReputationDefinitionFactory.GlobalPublicAudienceId : string.Empty,
                PlaceId = "place.prototype.test-lab",
                Subject = new SocialInteractionSubjectData { kind = SocialInteractionSubjectKind.Person, subjectId = target },
                Channel = SocialInteractionCommunicationChannel.Conversation,
                VisibilityOverride = visibility,
                WorldTime = worldTime,
                DeterministicSeed = "social-interactions-tests"
            };
        }

        private static RumorOperationResult CreateRumor(RumorRuntime runtime, string rumorId, string transactionId, string originatorPersonId)
        {
            return runtime.CreateRumor(new RumorCreateRequest
            {
                TransactionId = transactionId,
                RumorId = rumorId,
                DefinitionId = PrototypeRumorDefinitionFactory.PublicNewsRumorId,
                Claim = new KnowledgePropositionData
                {
                    factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                    subjectType = KnowledgeSubjectType.Event,
                    subjectId = $"knowledge.{rumorId}.claim",
                    valueType = KnowledgeValueType.Boolean,
                    booleanValue = true,
                    sourceContextId = $"knowledge.{rumorId}.source"
                },
                OriginatorPersonId = originatorPersonId,
                OriginCategory = RumorOriginCategory.FirsthandObservation,
                SourceAttributionPersonId = originatorPersonId,
                Confidence = 730,
                WorldTime = 1d
            });
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            GameObject owner = new GameObject("Social interaction test knowledge runtime");
            PersonKnowledgeRuntime knowledge = owner.AddComponent<PersonKnowledgeRuntime>();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            RelationshipRuntime relationships = new RelationshipRuntime();
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            ReputationRuntime reputation = new ReputationRuntime();
            RumorRuntime rumors = new RumorRuntime();
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();

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

            return new TestFixture(registry, owner, relationships, attitudes, reputation, rumors, interactions);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeSocialInteractionDefinitionFactory.AddMissingPrototypeSocialInteractionDefinitions(
                PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                    PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                        PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                            PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry())))));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, GameObject owner, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, ReputationRuntime reputation, RumorRuntime rumors, SocialInteractionRuntime interactions)
            {
                Registry = registry;
                Owner = owner;
                Relationships = relationships;
                Attitudes = attitudes;
                Reputation = reputation;
                Rumors = rumors;
                Interactions = interactions;
            }

            public DefinitionRegistry Registry { get; }
            public GameObject Owner { get; }
            public RelationshipRuntime Relationships { get; }
            public InterpersonalAttitudeRuntime Attitudes { get; }
            public ReputationRuntime Reputation { get; }
            public RumorRuntime Rumors { get; }
            public SocialInteractionRuntime Interactions { get; }

            public void Dispose()
            {
                Interactions.Dispose();
                Rumors.Dispose();
                UnityEngine.Object.DestroyImmediate(Owner);
            }
        }
    }
}
#endif
