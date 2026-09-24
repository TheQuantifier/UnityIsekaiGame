using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Development;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Knowledge.History;
using UnityIsekaiGame.Knowledge.Integration;
using UnityIsekaiGame.Knowledge.Records;
using UnityIsekaiGame.Knowledge.Sharing;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Tests
{
    public sealed class KnowledgeHistoryIntegrationFinalizationTests
    {
        [Test]
        public void ReadinessSnapshotCollections_AreImmutable()
        {
            KnowledgeHistoryFacade facade = new KnowledgeHistoryFacade(new KnowledgeHistoryRuntimeSet
            {
                DefinitionRegistry = new DefinitionRegistry(Array.Empty<IGameDefinition>()),
                PersonId = "person.prototype.test",
                WorldId = "world.prototype.test"
            });

            KnowledgeHistoryReadinessSnapshot snapshot = facade.CreateReadinessSnapshot();

            Assert.That(snapshot.Subsystems, Is.Not.Empty);
            Assert.That(snapshot.Ready, Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<KnowledgeHistorySubsystemReadiness>)snapshot.Subsystems).Add(new KnowledgeHistorySubsystemReadiness(KnowledgeHistorySubsystem.Knowledge, KnowledgeHistoryReadinessState.Ready, 0, "mutate")));
        }

        [Test]
        public void MissingRuntimeOperationFailsWithDiagnostic()
        {
            KnowledgeHistoryFacade facade = new KnowledgeHistoryFacade(new KnowledgeHistoryRuntimeSet
            {
                DefinitionRegistry = new DefinitionRegistry(Array.Empty<IGameDefinition>()),
                PersonId = "person.prototype.test",
                WorldId = "world.prototype.test"
            });

            KnowledgeHistoryOperationResult result = facade.RecordObservation(new KnowledgeObservationRequest
            {
                PersonId = "person.prototype.test",
                TransactionId = "tx.integration.missing-runtime"
            });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo("MissingKnowledgeRuntime"));
            Assert.That(result.Diagnostic.FailureStage, Is.EqualTo(KnowledgeHistoryFailureStage.Readiness));
            Assert.That(result.Diagnostic.Participants, Does.Contain(KnowledgeHistorySubsystem.Knowledge));
        }

        [Test]
        public void DefinitionCoverageDiagnostics_ReportMissingDefinitionsWithoutFallbacks()
        {
            DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[]
            {
                new FakeDefinition("record-definition.authored")
            });
            KnowledgeHistoryFacade facade = new KnowledgeHistoryFacade(new KnowledgeHistoryRuntimeSet
            {
                DefinitionRegistry = registry,
                PersonId = "person.prototype.test",
                WorldId = "world.prototype.test"
            });

            IReadOnlyList<KnowledgeHistoryDefinitionCoverageDiagnostic> diagnostics = facade.CreateDefinitionCoverageDiagnostics(new[] { "record-definition.authored", "record-definition.missing" });

            KnowledgeHistoryDefinitionCoverageDiagnostic authored = diagnostics.First(item => item.DefinitionId == "record-definition.authored");
            KnowledgeHistoryDefinitionCoverageDiagnostic missing = diagnostics.First(item => item.DefinitionId == "record-definition.missing");
            Assert.That(authored.CatalogAuthored, Is.True);
            Assert.That(authored.Missing, Is.False);
            Assert.That(missing.CatalogAuthored, Is.False);
            Assert.That(missing.Missing, Is.True);
        }

        [Test]
        public void PersistenceInventoryDocumentsStep8ParticipantGraph()
        {
            KnowledgeHistoryPersistenceInventory inventory = new KnowledgeHistoryFacade(new KnowledgeHistoryRuntimeSet()).CreatePersistenceInventory();

            Assert.That(inventory.Participants, Does.Contain("person.knowledge"));
            Assert.That(inventory.Participants, Does.Contain("person.memory"));
            Assert.That(inventory.Participants, Does.Contain("world.authoritative-history"));
            Assert.That(inventory.RequiredDependencies, Does.Contain("person.memory -> world.authoritative-history"));
            Assert.That(inventory.OptionalDependencies.Any(value => value.Contains("world.knowledge-records")), Is.True);
        }

        [Test]
        public void Step8MasterSuite_IsNotRegisteredWhenFeatureSuitesAreRunnableDirectly()
        {
            TestLabAutomationRegistry registry = PrototypeTestLabAutomationCatalog.CreateDefaultRegistry();
            ITestLabAutomationSuite[] step8Suites = registry.Suites.Where(suite => suite.SuiteId.StartsWith("feature.8.", StringComparison.Ordinal)).ToArray();

            ITestLabAutomationSuite feature = step8Suites.Single(suite => suite.SuiteId == "feature.8.10.knowledge-history-integration");
            Assert.That(feature.IncludeInRunAll, Is.True);
            Assert.That(step8Suites.Any(suite => suite.SuiteId == "step.8.knowledge-history-integration"), Is.False);
        }

        [Test]
        public void ValidationReportsDanglingCrossRuntimeRecordReferences()
        {
            const string personId = "person.prototype.integration-test";
            const string worldId = "world.prototype.integration-test";
            GameObject owner = new GameObject("Knowledge History Integration Test");
            try
            {
                DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeTestLabService.PrototypeCatalogPath);
                Assert.That(catalog, Is.Not.Null);
                DefinitionRegistry registry = catalog.CreateRegistry();
                PersonKnowledgeRuntime knowledge = owner.AddComponent<PersonKnowledgeRuntime>();
                knowledge.Configure(registry, personId, "actor.prototype.integration-test", "body.prototype.integration-test");
                AuthoritativeHistoryRuntime history = new AuthoritativeHistoryRuntime();
                history.Configure(registry, worldId, new[] { personId }, new[] { "body.prototype.integration-test" });
                PersonMemoryRuntime memory = new PersonMemoryRuntime();
                memory.Configure(personId, registry, history, new[] { personId });
                InformationSourceRuntime sources = new InformationSourceRuntime();
                sources.Configure(registry, personId);
                InformationTransferRuntime transfers = new InformationTransferRuntime();
                transfers.Configure(registry, personId);
                InformationAccessRuntime access = new InformationAccessRuntime();
                access.Configure(registry, personId);
                KnowledgeRecordRuntime records = new KnowledgeRecordRuntime();
                records.Configure(registry, personId);

                KnowledgeRecordOperationResult created = records.CreateRecord(new KnowledgeRecordCreateRequest
                {
                    TransactionId = "tx.integration.dangling-record",
                    RecordId = "record.integration.dangling",
                    DefinitionId = "record-definition.journal-entry",
                    Category = KnowledgeRecordCategory.PersonalJournal,
                    OwnerKind = KnowledgeRecordOwnerKind.Person,
                    OwnerId = personId,
                    Subject = new InformationSubjectReferenceData { subjectType = InformationSubjectType.HistoricalEvent, subjectId = "event.integration.missing", ownerPersonId = personId },
                    AuthorPersonId = personId,
                    SourceIds = new[] { "information-source.integration.missing" },
                    EvidenceIds = new[] { "evidence.integration.missing" },
                    HistoricalEventIds = new[] { "event.integration.missing" },
                    Confidence = 500,
                    Reliability = 500
                });
                Assert.That(created.Succeeded, Is.True, created.Message);

                KnowledgeHistoryFacade facade = new KnowledgeHistoryFacade(new KnowledgeHistoryRuntimeSet
                {
                    DefinitionRegistry = registry,
                    PersonId = personId,
                    WorldId = worldId,
                    KnowledgeRuntime = knowledge,
                    HistoryRuntime = history,
                    MemoryRuntime = memory,
                    SourceRuntime = sources,
                    TransferRuntime = transfers,
                    AccessRuntime = access,
                    RecordRuntime = records
                });

                KnowledgeHistoryValidationResult validation = facade.ValidateCurrentState();

                Assert.That(validation.Succeeded, Is.False);
                Assert.That(validation.Errors.Any(error => error.Contains("sourceIds") && error.Contains("information-source.integration.missing")), Is.True, string.Join("\n", validation.Errors));
                Assert.That(validation.Errors.Any(error => error.Contains("evidenceIds") && error.Contains("evidence.integration.missing")), Is.True, string.Join("\n", validation.Errors));
                Assert.That(validation.Errors.Any(error => error.Contains("historicalEventIds") && error.Contains("event.integration.missing")), Is.True, string.Join("\n", validation.Errors));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Step9ContractsRepresentRequestsWithoutRuntimeOwnership()
        {
            ItemKnowledgeRequest item = new ItemKnowledgeRequest
            {
                RequestingPersonId = "person.prototype.test",
                ItemDefinitionId = "item.health-potion"
            };
            RecipeKnowledgeRequest recipe = new RecipeKnowledgeRequest
            {
                RequestingPersonId = "person.prototype.test",
                RecipeDefinitionId = "recipe.prototype.health-potion",
                RequiredSkillId = "skill.alchemy"
            };
            CraftedItemProvenanceRequest provenance = new CraftedItemProvenanceRequest
            {
                CraftedItemInstanceId = "item-instance.prototype.crafted",
                CrafterPersonId = "person.prototype.test",
                RecipeDefinitionId = recipe.RecipeDefinitionId,
                WorkstationId = "workstation.prototype.alchemy",
                WorldTimeSeconds = 10d
            };

            Step9KnowledgeContractResult result = Step9KnowledgeContractResult.PreviewReady("contracts ready");

            Assert.That(item.SubjectId, Is.EqualTo("item.health-potion"));
            Assert.That(recipe.RequiredSkillId, Is.EqualTo("skill.alchemy"));
            Assert.That(provenance.CraftedItemInstanceId, Is.EqualTo("item-instance.prototype.crafted"));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Code, Is.EqualTo("ContractReady"));
        }

        private sealed class FakeDefinition : IGameDefinition
        {
            public FakeDefinition(string id)
            {
                Id = id;
            }

            public string Id { get; }
            public string DisplayName => Id;
        }
    }
}
