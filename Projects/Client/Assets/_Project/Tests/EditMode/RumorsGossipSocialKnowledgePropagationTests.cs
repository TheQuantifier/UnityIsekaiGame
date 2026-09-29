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
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class RumorsGossipSocialKnowledgePropagationTests
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
        public void PrototypeRumorDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeRumorDefinitionFactory.PublicNewsRumorId, out RumorDefinition publicNews), Is.True);
            Assert.That(registry.TryGet(PrototypeRumorDefinitionFactory.TavernGossipChannelId, out RumorCommunicationChannelDefinition tavern), Is.True);
            Assert.That(publicNews.Category, Is.EqualTo(RumorCategory.PublicNews));
            Assert.That(tavern.SupportsBroadcast, Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IGameDefinition definition in PrototypeRumorDefinitionFactory.CreateDefinitions())
            {
                if (definition is IDefinitionCatalogValidationParticipant participant)
                {
                    participant.ValidateCatalogDefinition(registry.DefinitionsById, report);
                }
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
        }

        [Test]
        public void RootRumorIdentityLineageAndSnapshotsAreImmutable()
        {
            using TestFixture fixture = CreateFixture();
            RumorOperationResult created = CreateRumor(fixture.Rumors, "rumor.test.root", "rumor.tx.root", "person.prototype.friend", PrototypeRumorDefinitionFactory.PublicNewsRumorId);

            RumorSnapshot snapshot = created.Rumor;
            snapshot.Data.rumorId = "rumor.mutated";
            snapshot.Data.tags = new[] { "mutated" };

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(created.Rumor.RumorId, Is.EqualTo(created.Rumor.RootRumorId));
            Assert.That(fixture.Rumors.IsAware("person.prototype.friend", "rumor.test.root"), Is.True);
            Assert.That(fixture.Rumors.QueryByRoot("rumor.test.root").Count, Is.EqualTo(1));
            Assert.That(fixture.Rumors.TryGetRumor("rumor.test.root", out RumorSnapshot after), Is.True);
            Assert.That(after.RumorId, Is.EqualTo("rumor.test.root"));
            Assert.That(after.Data.tags, Is.Empty);
        }

        [Test]
        public void TransmissionCreatesEvidenceMemoryAndDuplicateDoesNotMutateAgain()
        {
            using TestFixture fixture = CreateFixture();
            RumorOperationResult created = CreateRumor(fixture.Rumors, "rumor.test.transfer", "rumor.tx.transfer.create", "person.prototype.friend", PrototypeRumorDefinitionFactory.PersonalConductRumorId);
            RumorTransmissionRequest request = new RumorTransmissionRequest
            {
                TransactionId = "rumor.tx.transfer",
                TransmissionId = "rumor-transmission.test.transfer",
                RumorVersionId = created.Rumor.RumorId,
                SpeakerPersonId = "person.prototype.friend",
                ListenerPersonId = PersistenceService.LocalPlayerId,
                ChannelId = PrototypeRumorDefinitionFactory.ConversationChannelId,
                RequestedOutcome = RumorTransmissionOutcome.Believed,
                SpeakerConfidence = 820,
                WorldTime = 20d
            };

            RumorOperationResult first = fixture.Rumors.Transmit(request);
            RumorOperationResult duplicate = fixture.Rumors.Transmit(request);

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.KnowledgeResult?.Succeeded, Is.True);
            Assert.That(first.MemoryResult?.Succeeded, Is.True);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Knowledge.CreateSnapshot().Evidence.Count, Is.EqualTo(1));
            Assert.That(fixture.Memory.CreateSnapshot().Memories.Count, Is.EqualTo(1));
            Assert.That(fixture.Rumors.TransmissionCount, Is.EqualTo(1));
        }

        [Test]
        public void DistortionCreatesDerivedVersionWithoutMutatingOriginalClaim()
        {
            using TestFixture fixture = CreateFixture();
            RumorOperationResult created = CreateRumor(fixture.Rumors, "rumor.test.distortion", "rumor.tx.distortion.create", "person.prototype.friend", PrototypeRumorDefinitionFactory.SecretLeakRumorId, RumorDisclosure.Shareable);

            RumorOperationResult distorted = fixture.Rumors.Transmit(new RumorTransmissionRequest
            {
                TransactionId = "rumor.tx.distortion",
                TransmissionId = "rumor-transmission.test.distortion",
                RumorVersionId = created.Rumor.RumorId,
                SpeakerPersonId = "person.prototype.friend",
                ListenerPersonId = PersistenceService.LocalPlayerId,
                ChannelId = PrototypeRumorDefinitionFactory.TavernGossipChannelId,
                RequestedOutcome = RumorTransmissionOutcome.PartiallyBelieved,
                RequestedDistortionPolicy = RumorDistortionPolicy.ForcedConfidenceDecrease,
                DerivedRumorId = "rumor.test.distortion.derived",
                DeterministicSeed = "seed.distortion",
                WorldTime = 21d
            });

            Assert.That(distorted.Succeeded, Is.True, distorted.Message);
            Assert.That(distorted.Rumor.RumorId, Is.Not.EqualTo(created.Rumor.RumorId));
            Assert.That(distorted.Rumor.RootRumorId, Is.EqualTo(created.Rumor.RootRumorId));
            Assert.That(distorted.Rumor.ParentRumorId, Is.EqualTo(created.Rumor.RumorId));
            Assert.That(distorted.Rumor.Confidence, Is.EqualTo(created.Rumor.Confidence - 100));
            Assert.That(fixture.Rumors.QueryByRoot(created.Rumor.RootRumorId).Count, Is.EqualTo(2));
            Assert.That(fixture.Rumors.QueryByClaim(created.Rumor.ClaimIdentity).Count, Is.EqualTo(2));
        }

        [Test]
        public void PropagationIsBoundedAndDoesNotMutateOtherSocialRuntimes()
        {
            using TestFixture fixture = CreateFixture();
            RelationshipRuntime relationships = new RelationshipRuntime();
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            ReputationRuntime reputation = new ReputationRuntime();
            relationships.Configure(fixture.Registry, KnownPersons);
            attitudes.Configure(fixture.Registry, KnownPersons);
            reputation.Configure(fixture.Registry, KnownPersons);
            RumorOperationResult created = CreateRumor(fixture.Rumors, "rumor.test.propagation", "rumor.tx.propagation.create", "person.prototype.friend", PrototypeRumorDefinitionFactory.PublicNewsRumorId);

            RumorPropagationResult propagated = fixture.Rumors.Propagate(new RumorPropagationRequest
            {
                TransactionId = "rumor.tx.propagation",
                RumorVersionId = created.Rumor.RumorId,
                SpeakerPersonId = "person.prototype.friend",
                ListenerPersonIds = new[] { "person.prototype.rival", PersistenceService.LocalPlayerId, "person.prototype.mentor" },
                ChannelId = PrototypeRumorDefinitionFactory.PublicSpeechChannelId,
                MaximumTransmissions = 2,
                WorldTime = 30d
            });
            RumorPropagationMetrics metrics = fixture.Rumors.GetMetrics(created.Rumor.RootRumorId);

            Assert.That(propagated.Succeeded, Is.True, propagated.Message);
            Assert.That(propagated.Transmissions.Count, Is.EqualTo(2));
            Assert.That(metrics.Transmissions, Is.EqualTo(2));
            Assert.That(metrics.AwarePeople, Is.EqualTo(3));
            Assert.That(relationships.Count, Is.EqualTo(0));
            Assert.That(attitudes.Count, Is.EqualTo(0));
            Assert.That(reputation.Count, Is.EqualTo(0));
        }

        [Test]
        public void PersistenceParticipantRoundTripsAndRejectsInvalidRestoreWithoutMutation()
        {
            using TestFixture fixture = CreateFixture();
            RumorOperationResult created = CreateRumor(fixture.Rumors, "rumor.test.persisted", "rumor.tx.persisted", "person.prototype.friend", PrototypeRumorDefinitionFactory.FabricatedAccusationRumorId);
            RumorPersistenceParticipant participant = new RumorPersistenceParticipant(fixture.Rumors, () => fixture.Registry, () => KnownPersons);

            PersistenceParticipantSaveResult save = participant.CapturePayload();
            RumorRuntimeSaveData saveData = JsonUtility.FromJson<RumorRuntimeSaveData>(save.PayloadJson);
            RumorRuntime restored = CreateRumorRuntime(fixture.Registry, null, null);
            RumorOperationResult restore = restored.RestoreFromSaveData(saveData, fixture.Registry, KnownPersons, restoringState: true);
            RumorRuntimeSaveData corrupt = saveData.Clone();
            corrupt.rumors[0].definitionId = "rumor.missing";
            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), RumorPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetRumor(created.Rumor.RumorId, out RumorSnapshot restoredRumor), Is.True);
            Assert.That(restoredRumor.Authenticity, Is.EqualTo(RumorAuthenticity.Fabricated));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Rumors.RumorCount, Is.EqualTo(1));
        }

        private static RumorOperationResult CreateRumor(RumorRuntime runtime, string rumorId, string transactionId, string originatorPersonId, string definitionId, RumorDisclosure? disclosure = null)
        {
            return runtime.CreateRumor(new RumorCreateRequest
            {
                TransactionId = transactionId,
                RumorId = rumorId,
                DefinitionId = definitionId,
                Claim = Claim(rumorId),
                OriginatorPersonId = originatorPersonId,
                OriginCategory = RumorOriginCategory.FirsthandObservation,
                OriginatingEventId = $"history.{rumorId}.source",
                SourceAttributionPersonId = originatorPersonId,
                SourceNamed = true,
                Confidence = definitionId == PrototypeRumorDefinitionFactory.FabricatedAccusationRumorId ? 360 : 730,
                Salience = 600,
                Memorability = 620,
                DisclosureOverride = disclosure,
                Authenticity = definitionId == PrototypeRumorDefinitionFactory.FabricatedAccusationRumorId ? RumorAuthenticity.Fabricated : RumorAuthenticity.Unverified,
                WorldTime = 10d
            });
        }

        private static KnowledgePropositionData Claim(string id)
        {
            return new KnowledgePropositionData
            {
                factDefinitionId = BuiltInKnowledgeFacts.EventOccurred,
                subjectType = KnowledgeSubjectType.Event,
                subjectId = $"knowledge.{id}.claim",
                valueType = KnowledgeValueType.Boolean,
                booleanValue = true,
                sourceContextId = $"knowledge.{id}.source"
            };
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            GameObject owner = new GameObject("Rumor test knowledge runtime");
            PersonKnowledgeRuntime knowledge = owner.AddComponent<PersonKnowledgeRuntime>();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            PersonMemoryRuntime memory = new PersonMemoryRuntime();
            history.Configure(registry, PersistenceService.LocalWorldId, KnownPersons, Array.Empty<string>());
            knowledge.Configure(registry, PersistenceService.LocalPlayerId);
            memory.Configure(PersistenceService.LocalPlayerId, registry, history, KnownPersons);
            RumorRuntime rumors = CreateRumorRuntime(registry, knowledge, memory);
            return new TestFixture(registry, history, memory, knowledge, rumors, owner);
        }

        private static RumorRuntime CreateRumorRuntime(DefinitionRegistry registry, PersonKnowledgeRuntime knowledge, PersonMemoryRuntime memory)
        {
            RumorRuntime runtime = new RumorRuntime();
            runtime.Configure(
                registry,
                KnownPersons,
                personId => string.Equals(personId, PersistenceService.LocalPlayerId, StringComparison.Ordinal) ? knowledge : null,
                personId => string.Equals(personId, PersistenceService.LocalPlayerId, StringComparison.Ordinal) ? memory : null);
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeRumorDefinitionFactory.AddMissingPrototypeRumorDefinitions(
                PrototypeReputationDefinitionFactory.AddMissingPrototypeReputationDefinitions(
                    PrototypeAttitudeDefinitionFactory.AddMissingPrototypeAttitudeDefinitions(
                        PrototypeRelationshipDefinitionFactory.AddMissingPrototypeRelationshipDefinitions(catalog.CreateRegistry()))));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, AuthoritativeHistoryRuntime history, PersonMemoryRuntime memory, PersonKnowledgeRuntime knowledge, RumorRuntime rumors, GameObject owner)
            {
                Registry = registry;
                History = history;
                Memory = memory;
                Knowledge = knowledge;
                Rumors = rumors;
                Owner = owner;
            }

            public DefinitionRegistry Registry { get; }
            public AuthoritativeHistoryRuntime History { get; }
            public PersonMemoryRuntime Memory { get; }
            public PersonKnowledgeRuntime Knowledge { get; }
            public RumorRuntime Rumors { get; }
            public GameObject Owner { get; }

            public void Dispose()
            {
                Rumors.Dispose();
                UnityEngine.Object.DestroyImmediate(Owner);
            }
        }
    }
}
#endif
