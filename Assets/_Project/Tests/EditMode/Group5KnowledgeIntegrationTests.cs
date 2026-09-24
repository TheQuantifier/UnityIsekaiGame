using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Integration;
using UnityIsekaiGame.Knowledge.Observation;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class Group5KnowledgeIntegrationTests
    {
        private const string CatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        [Test]
        public void PrototypeCatalogContainsCanonicalGroup5Content()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            DefinitionRegistry registry = catalog.CreateRegistry();

            Assert.That(registry.DefinitionsById.Values.OfType<KnowledgePolicyDefinition>().Count(), Is.EqualTo(1));
            Assert.That(registry.DefinitionsById.Values.OfType<InformationSourceDefinition>().Count(), Is.EqualTo(19));
            Assert.That(registry.DefinitionsById.Values.OfType<InformationTransferDefinition>().Count(), Is.EqualTo(10));
            Assert.That(registry.DefinitionsById.Values.OfType<InformationAccessPolicyDefinition>().Count(), Is.GreaterThanOrEqualTo(6));
            Assert.That(registry.DefinitionsById.Values.OfType<KnowledgeRecordDefinition>().Count(), Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void SourceRegistrationRejectsMissingAuthoredDefinition()
        {
            InformationSourceRuntime runtime = new InformationSourceRuntime();
            runtime.Configure(new DefinitionRegistry(Array.Empty<IGameDefinition>()), "world.test");

            InformationSourceOperationResult result = runtime.RegisterSource(new InformationSourceRegistrationRequest
            {
                TransactionId = "tx.group5.missing-source-definition",
                SourceInstanceId = "source.group5.missing",
                Category = InformationSourceCategory.DirectObservation,
                ReferenceType = InformationSourceReferenceType.Body,
                ReferencedId = "body.test",
                ObserverPersonId = "person.test",
                HolderPersonId = "person.test",
                Domain = KnowledgeDomain.Biological,
                SubjectId = "body.test"
            });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("authored Information Source definition"));
        }

        [Test]
        public void LegacyMemorySchemaIsRejected()
        {
            PersonMemorySaveData legacy = new PersonMemorySaveData
            {
                schemaVersion = 1,
                personId = "person.test",
                memories = Array.Empty<HistoryMemoryRecordData>(),
                processedTransactions = Array.Empty<string>()
            };

            bool valid = PersonMemoryRuntime.ValidateSaveData(legacy, null, new[] { "person.test" }, out string failure);

            Assert.That(valid, Is.False);
            Assert.That(failure, Does.Contain("schema version 1"));
        }

        [Test]
        public void ObservationRequiresRangeLineOfSightAndCapability()
        {
            KnowledgeFactDefinition fact = ScriptableObject.CreateInstance<KnowledgeFactDefinition>();
            Set(fact, "factId", "fact.group5.observation");
            Set(fact, "displayName", "Group 5 Observation");
            Set(fact, "domain", KnowledgeDomain.Biological);
            Set(fact, "propositionType", KnowledgePropositionType.Identity);
            Set(fact, "subjectType", KnowledgeSubjectType.Body);
            Set(fact, "valueType", KnowledgeValueType.StableId);

            ObservationMethodDefinition method = ScriptableObject.CreateInstance<ObservationMethodDefinition>();
            Set(method, "methodId", "observation-method.group5.visual");
            Set(method, "displayName", "Group 5 Visual");
            Set(method, "category", ObservationMethodCategory.OrdinaryVisualObservation);
            Set(method, "sensoryChannels", new[] { SensoryChannel.Vision });
            Set(method, "targetTypes", new[] { ObservationTargetType.Body });
            Set(method, "active", true);
            Set(method, "maximumRange", 2f);
            Set(method, "requiresLineOfSight", true);
            Set(method, "requiredCapabilityId", "capability.group5.observe");

            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { fact, method });
            GameObject owner = new GameObject("Group 5 Observation Test");
            try
            {
                PersonKnowledgeRuntime knowledge = owner.AddComponent<PersonKnowledgeRuntime>();
                knowledge.Configure(registry, "person.test");
                ObservationService service = new ObservationService(registry);
                ObservableProjection projection = new ObservableProjection(
                    "projection.group5",
                    ObservationTargetType.Body,
                    new KnowledgePropositionData
                    {
                        factDefinitionId = fact.Id,
                        subjectType = KnowledgeSubjectType.Body,
                        subjectId = "body.target",
                        valueType = KnowledgeValueType.StableId,
                        stableValueId = "body.target"
                    },
                    KnowledgeVisibility.Public,
                    1,
                    700,
                    new[] { SensoryChannel.Vision });

                ObservationResult tooFar = service.Observe(knowledge, Context("tx.group5.range", 3f, true, new[] { "capability.group5.observe" }), projection, preview: true);
                ObservationResult obstructed = service.Observe(knowledge, Context("tx.group5.los", 1f, false, new[] { "capability.group5.observe" }), projection, preview: true);
                ObservationResult missingCapability = service.Observe(knowledge, Context("tx.group5.capability", 1f, true, Array.Empty<string>()), projection, preview: true);
                ObservationResult valid = service.Observe(knowledge, Context("tx.group5.valid", 1f, true, new[] { "capability.group5.observe" }), projection, preview: true);

                Assert.That(tooFar.Succeeded, Is.False);
                Assert.That(obstructed.Succeeded, Is.False);
                Assert.That(missingCapability.Succeeded, Is.False);
                Assert.That(valid.Succeeded, Is.True, valid.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(fact);
                UnityEngine.Object.DestroyImmediate(method);
            }
        }

        [Test]
        public void CharacterCreationBridgeWritesOneIdempotentLifeEvent()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            DefinitionRegistry registry = catalog.CreateRegistry();
            AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
            history.Configure(registry, "world.test", new[] { "person.test" }, new[] { "body.test" });
            KnowledgeHistoryEventBridge bridge = new KnowledgeHistoryEventBridge(history, () => 42d, () => "person.test", () => "body.test", () => "place.test");

            HistoryOperationResult first = bridge.EnsureCharacterCreation("origin.test", "birth-gift.test");
            HistoryOperationResult duplicate = bridge.EnsureCharacterCreation("origin.test", "birth-gift.test");

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(history.QueryLifeEventsForPerson("person.test").Count, Is.EqualTo(1));
        }

        [Test]
        public void SharedKnowledgeParticipantsCanBeRequiredWorldState()
        {
            DefinitionRegistry registry = new DefinitionRegistry(Array.Empty<IGameDefinition>());
            InformationSourcePersistenceParticipant sources = new InformationSourcePersistenceParticipant(new InformationSourceRuntime(), () => registry, "world.test", PersistenceScope.SharedWorld, InformationSourcePersistenceParticipant.WorldKey, required: true);
            InformationTransferPersistenceParticipant transfers = new InformationTransferPersistenceParticipant(new InformationTransferRuntime(), () => registry, "world.test", PersistenceScope.SharedWorld, InformationTransferPersistenceParticipant.WorldKey, required: true);
            InformationAccessPersistenceParticipant access = new InformationAccessPersistenceParticipant(new InformationAccessRuntime(), () => registry, "world.test", PersistenceScope.SharedWorld, InformationAccessPersistenceParticipant.WorldKey, required: true);
            KnowledgeRecordPersistenceParticipant records = new KnowledgeRecordPersistenceParticipant(new KnowledgeRecordRuntime(), () => registry, "world.test", PersistenceScope.SharedWorld, KnowledgeRecordPersistenceParticipant.WorldKey, required: true);

            Assert.That(sources.Scope, Is.EqualTo(PersistenceScope.SharedWorld));
            Assert.That(transfers.Scope, Is.EqualTo(PersistenceScope.SharedWorld));
            Assert.That(access.Scope, Is.EqualTo(PersistenceScope.SharedWorld));
            Assert.That(records.Scope, Is.EqualTo(PersistenceScope.SharedWorld));
            Assert.That(new IPersistenceParticipant[] { sources, transfers, access, records }.All(participant => participant.IsRequired), Is.True);
        }

        private static ObservationContext Context(string transactionId, float distance, bool lineOfSight, string[] capabilityIds)
        {
            return new ObservationContext(
                "person.test",
                transactionId,
                "observation-method.group5.visual",
                SensoryChannel.Vision,
                ObservationTargetType.Body,
                "body.target",
                actualDistance: distance,
                hasLineOfSight: lineOfSight,
                capabilityIds: capabilityIds);
        }

        private static void Set<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
