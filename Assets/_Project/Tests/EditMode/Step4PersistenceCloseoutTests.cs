using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class Step4PersistenceCloseoutTests
    {
        private const string InventoryEquipmentKey = "player.inventory-equipment";
        private const string StatsVitalsStatusKey = "player.stats-vitals-status";
        private const string QuestContractKey = "player.quests-contracts";
        private const string PlayerLocationKey = "player.location";
        private const string PrototypeCatalogPath = "Assets/_Project/Prototype/Content/GameData/PrototypeDefinitionCatalog.asset";

        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "UnityIsekaiGameStep4CloseoutTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(testRoot) && Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [Test]
        public void ParticipantInventoryAndSchemaVersionsMatchStep4Closeout()
        {
            Assert.That(PersistenceService.CurrentSchemaVersion, Is.EqualTo(2));
            Assert.That(PrototypePersistenceStateParticipant.Key, Is.EqualTo("prototype.state"));
            Assert.That(PrototypePersistenceStateParticipant.CurrentParticipantSchemaVersion, Is.EqualTo(1));
            Assert.That(GetStatic<string>("UnityIsekaiGame.Persistence.PlayerInventoryEquipmentPersistenceParticipant", "Key"), Is.EqualTo(InventoryEquipmentKey));
            Assert.That(GetStatic<int>("UnityIsekaiGame.Persistence.PlayerInventoryEquipmentPersistenceParticipant", "CurrentParticipantSchemaVersion"), Is.EqualTo(2));
            Assert.That(GetStatic<string>("UnityIsekaiGame.Persistence.PlayerStatsVitalsStatusPersistenceParticipant", "Key"), Is.EqualTo(StatsVitalsStatusKey));
            Assert.That(GetStatic<int>("UnityIsekaiGame.Persistence.PlayerStatsVitalsStatusPersistenceParticipant", "CurrentParticipantSchemaVersion"), Is.EqualTo(1));
            Assert.That(GetStatic<string>("UnityIsekaiGame.Persistence.PlayerQuestContractPersistenceParticipant", "Key"), Is.EqualTo(QuestContractKey));
            Assert.That(GetStatic<int>("UnityIsekaiGame.Persistence.PlayerQuestContractPersistenceParticipant", "CurrentParticipantSchemaVersion"), Is.EqualTo(2));
            Assert.That(GetStatic<string>("UnityIsekaiGame.Persistence.PlayerLocationPersistenceParticipant", "ParticipantKeyValue"), Is.EqualTo(PlayerLocationKey));
            Assert.That(GetStatic<int>("UnityIsekaiGame.Persistence.PlayerLocationPersistenceParticipant", "CurrentParticipantSchemaVersion"), Is.EqualTo(1));
            Assert.That(GetStatic<int>("UnityIsekaiGame.WorldEntities.WorldEntityReference", "CurrentSchemaVersion"), Is.EqualTo(1));
        }

        [Test]
        public void ScopeAndOwnerMetadataRemainPlayerScopedForCurrentParticipants()
        {
            PersistenceService service = Service(
                new TestParticipant(PlayerLocationKey, 5, PersistenceLoadPhase.PositionAndPlace, required: false),
                new TestParticipant(QuestContractKey, 4, PersistenceLoadPhase.QuestsAndContracts),
                new TestParticipant(StatsVitalsStatusKey, 3, PersistenceLoadPhase.Statuses),
                new TestParticipant(InventoryEquipmentKey, 2, PersistenceLoadPhase.Inventory),
                new TestParticipant(PrototypePersistenceStateParticipant.Key, 1, PersistenceLoadPhase.Prototype));

            PersistenceDependencyReport report = service.BuildParticipantDependencyReport();

            Assert.That(report.succeeded, Is.True, report.message);
            Assert.That(report.orderedParticipantKeys, Is.EqualTo(new[]
            {
                InventoryEquipmentKey,
                StatsVitalsStatusKey,
                QuestContractKey,
                PlayerLocationKey,
                PrototypePersistenceStateParticipant.Key
            }));

            PersistenceSaveResult save = service.Save("manual-1");
            Assert.That(save.Succeeded, Is.True, save.Message);

            GameSaveEnvelope envelope = ReadEnvelope("manual-1");
            Assert.That(envelope.worldId, Is.EqualTo(PersistenceService.LocalWorldId));
            Assert.That(envelope.playerId, Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(envelope.accountId, Is.EqualTo(PersistenceService.LocalAccountId));
            Assert.That(envelope.transactionId, Is.Not.Empty);
            Assert.That(envelope.completedWriteMarker, Is.True);
            Assert.That(envelope.saveRevision, Is.GreaterThanOrEqualTo(1));

            foreach (SaveParticipantRecord record in envelope.participants)
            {
                Assert.That(record.persistenceScope, Is.EqualTo((int)PersistenceScope.Player), record.participantKey);
                Assert.That(record.ownerId, Is.EqualTo(PersistenceService.LocalPlayerId), record.participantKey);
            }
        }

        [Test]
        public void FiveRepeatedLoadsKeepRuntimeFingerprintStable()
        {
            TestParticipant inventory = new TestParticipant(InventoryEquipmentKey, 10, PersistenceLoadPhase.Inventory);
            TestParticipant stats = new TestParticipant(StatsVitalsStatusKey, 20, PersistenceLoadPhase.Statuses);
            TestParticipant quests = new TestParticipant(QuestContractKey, 30, PersistenceLoadPhase.QuestsAndContracts);
            PersistenceService service = Service(inventory, stats, quests);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);

            inventory.Value = 99;
            stats.Value = 88;
            quests.Value = 77;

            string expectedFingerprint = null;
            for (int i = 0; i < 5; i++)
            {
                PersistenceLoadResult load = service.Load("manual-1");
                Assert.That(load.Succeeded, Is.True, load.Message);
                string fingerprint = service.BuildRuntimeStateFingerprint();
                expectedFingerprint ??= fingerprint;
                Assert.That(fingerprint, Is.EqualTo(expectedFingerprint), $"Load {i + 1} changed fingerprint.");
            }
        }

        [Test]
        public void CorruptPrimaryDoesNotMutateLiveStateAndBackupRecoveryRemainsExplicit()
        {
            TestParticipant participant = new TestParticipant(PrototypePersistenceStateParticipant.Key, 1, PersistenceLoadPhase.Prototype);
            PersistenceService service = Service(participant);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            participant.Value = 2;
            Assert.That(service.Save("manual-1").Succeeded, Is.True);

            File.WriteAllText(Paths("manual-1").PrimaryPath, "{ bad json");
            participant.Value = 99;
            string liveFingerprint = service.BuildRuntimeStateFingerprint();

            PersistenceLoadResult primary = service.Load("manual-1");

            Assert.That(primary.Succeeded, Is.False);
            Assert.That(primary.Status, Is.EqualTo(PersistenceLoadStatus.BackupAvailable));
            Assert.That(primary.BackupAvailable, Is.True);
            Assert.That(service.BuildRuntimeStateFingerprint(), Is.EqualTo(liveFingerprint));
            Assert.That(participant.Value, Is.EqualTo(99));

            PersistenceLoadResult backup = service.Load("manual-1", loadBackup: true);

            Assert.That(backup.Succeeded, Is.True, backup.Message);
            Assert.That(backup.LoadedBackup, Is.True);
            Assert.That(participant.Value, Is.EqualTo(1));
        }

        [Test]
        public void RollbackFailureMarksRuntimeUnsafeAndBlocksFurtherSaves()
        {
            TestParticipant first = new TestParticipant("test.first", 1, PersistenceLoadPhase.Inventory);
            TestParticipant second = new TestParticipant("test.second", 2, PersistenceLoadPhase.Statuses) { FailCommit = true };
            PersistenceService service = Service(first, second);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);

            first.Value = 100;
            second.Value = 200;
            service.FaultInjection.nextFailurePoint = PersistenceFaultInjectionPoint.RollbackCommit;
            service.FaultInjection.message = "Injected rollback failure.";

            PersistenceLoadResult load = service.Load("manual-1");

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Status, Is.EqualTo(PersistenceLoadStatus.ParticipantCommitFailedRollbackFailed));
            Assert.That(load.RollbackAttempted, Is.True);
            Assert.That(load.RollbackSucceeded, Is.False);
            Assert.That(service.RuntimeSafety, Is.EqualTo(PersistenceRuntimeSafety.Unsafe));

            PersistenceSaveResult save = service.Save("manual-2");
            Assert.That(save.Succeeded, Is.False);
            Assert.That(save.Status, Is.EqualTo(PersistenceSaveStatus.UnsafeRuntimeState));
        }

        [Test]
        public void UnsupportedFutureEnvelopeVersionIsRejectedWithoutMutation()
        {
            TestParticipant participant = new TestParticipant(PrototypePersistenceStateParticipant.Key, 1, PersistenceLoadPhase.Prototype);
            PersistenceService service = Service(participant);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);

            GameSaveEnvelope envelope = ReadEnvelope("manual-1");
            envelope.schemaVersion = PersistenceService.CurrentSchemaVersion + 1;
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(Paths("manual-1").PrimaryPath, JsonUtility.ToJson(envelope, true));
            participant.Value = 55;

            PersistenceLoadResult load = service.Load("manual-1");

            Assert.That(load.Succeeded, Is.False);
            Assert.That(load.Status, Is.EqualTo(PersistenceLoadStatus.UnsupportedSchemaVersion));
            Assert.That(participant.Value, Is.EqualTo(55));
        }

        [Test]
        public void SaveSlotIdsAndRecoveryRecommendationPriorityAreStable()
        {
            Assert.That(PrototypeSaveSlotCatalog.ManualSlotId(0), Is.EqualTo("manual-1"));
            Assert.That(PrototypeSaveSlotCatalog.ManualSlotId(4), Is.EqualTo("manual-5"));
            Assert.That(PrototypeSaveSlotCatalog.AutosaveSlotId(0), Is.EqualTo("autosave-0"));
            Assert.That(PrototypeSaveSlotCatalog.AutosaveSlotId(2), Is.EqualTo("autosave-2"));
            Assert.That(PrototypeSaveSlotCatalog.AutosaveStagingSlotId, Is.EqualTo("autosave-staging"));

            TestParticipant participant = new TestParticipant(PrototypePersistenceStateParticipant.Key, 1, PersistenceLoadPhase.Prototype);
            PersistenceService service = Service(participant);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            participant.Value = 2;
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            File.WriteAllText(Paths("manual-1").TemporaryPath, File.ReadAllText(Paths("manual-1").BackupPath));

            SaveRecoveryScanReport report = service.ScanRecoverySources();

            Assert.That(report.recommendation, Does.Contain(RecoveryRecommendationAction.LoadBackup.ToString()));
            Assert.That(report.recommendation, Does.Not.Contain(RecoveryRecommendationAction.InspectTemporary.ToString()));
        }

        [Test]
        public void PrototypeCatalog_UsesCanonicalDefinitionIdsWithoutLegacyWarnings()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Expected prototype definition catalog at {PrototypeCatalogPath}.");

            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);
            DefinitionRegistry registry = catalog.CreateRegistry(report);

            Assert.That(report.HasErrors, Is.False, report.GetSummary());
            Assert.That(report.WarningCount, Is.Zero, report.GetSummary());

            string[] canonicalIds =
            {
                "item.health-potion",
                "item.prototype-sword",
                "item.prototype-helmet",
                "person.prototype-npc",
                "quest.prototype-strange-disturbance",
                "contract.prototype-enemy-elimination",
                "contract.prototype-potion-collection",
                "contract.prototype-potion-delivery",
                "category.status",
                "category.status.beneficial",
                "category.status.harmful",
                "category.status.neutral",
                "category.being",
                "category.being.person",
                "category.being.monster",
                "category.place",
                "category.place.settlement",
                "category.place.point-of-interest",
                "category.faction",
                "category.faction.guild",
                "category.damage",
                "category.damage.physical",
                "damage.physical.slashing",
                "scene.prototype"
            };

            foreach (string id in canonicalIds)
            {
                if (id == "scene.prototype")
                {
                    Assert.That(DefinitionIdValidator.Validate(id).WarningCount, Is.Zero, id);
                    continue;
                }

                Assert.That(registry.Contains(id), Is.True, $"Expected canonical definition ID '{id}' to resolve.");
            }
        }

        [Test]
        public void PrototypeCatalog_DoesNotResolveObsoletePrototypeDefinitionIds()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(PrototypeCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Expected prototype definition catalog at {PrototypeCatalogPath}.");

            DefinitionRegistry registry = catalog.CreateRegistry();
            string[] obsoleteIds =
            {
                "health_potion",
                "prototype_sword",
                "prototype_helmet",
                "prototype_npc",
                "prototype_strange_disturbance",
                "prototype_enemy_elimination",
                "prototype_potion_collection",
                "prototype_potion_delivery",
                "status.beneficial",
                "status.harmful",
                "status.neutral",
                "being-category",
                "being-category.person",
                "being-category.monster",
                "place-category",
                "place-category.settlement",
                "place-category.point-of-interest",
                "faction-category",
                "faction-category.guild",
                "damage-category",
                "damage-category.physical"
            };

            foreach (string id in obsoleteIds)
            {
                Assert.That(registry.Contains(id), Is.False, $"Obsolete prototype definition ID '{id}' should not resolve without an explicit migration.");
            }
        }

        private PersistenceService Service(params TestParticipant[] participants)
        {
            PersistenceService service = new PersistenceService(new PersistencePathProvider(testRoot));
            foreach (TestParticipant participant in participants)
            {
                Assert.That(service.RegisterParticipant(participant, out string failure), Is.True, failure);
            }

            return service;
        }

        private SaveSlotPaths Paths(string slotId)
        {
            PersistencePathProvider provider = new PersistencePathProvider(testRoot);
            Assert.That(provider.TryGetPaths(slotId, out SaveSlotPaths paths, out string failure), Is.True, failure);
            return paths;
        }

        private GameSaveEnvelope ReadEnvelope(string slotId)
        {
            return JsonUtility.FromJson<GameSaveEnvelope>(File.ReadAllText(Paths(slotId).PrimaryPath));
        }

        private static T GetStatic<T>(string typeName, string fieldName)
        {
            Type type = TestTypeResolver.RequiredType(typeName);
            Assert.That(type, Is.Not.Null, typeName);
            object value = type.GetField(fieldName).GetValue(null);
            return (T)value;
        }

        [Serializable]
        private sealed class TestPayload
        {
            public int value;
        }

        private sealed class TestParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
        {
            private readonly string key;
            private readonly PersistenceLoadPhase phase;
            private readonly bool required;

            public TestParticipant(string key, int value, PersistenceLoadPhase phase, bool required = true)
            {
                this.key = key;
                Value = value;
                this.phase = phase;
                this.required = required;
            }

            public int Value;
            public bool FailCommit;

            public string ParticipantKey => key;
            public int ParticipantSchemaVersion => 1;
            public bool IsRequired => required;
            public PersistenceScope Scope => PersistenceScope.Player;
            public string OwnerId => PersistenceService.LocalPlayerId;
            public PersistenceLoadPhase LoadPhase => phase;
            public int LoadPriority => 0;
            public IReadOnlyList<string> RequiredDependencies => Array.Empty<string>();
            public IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
            public bool SupportsRollback => true;
            public bool RequiresSceneReadiness => key == PlayerLocationKey;
            public bool RequiresDefinitionRegistry => key.StartsWith("player.", StringComparison.Ordinal);
            public bool RequiresWorldEntityRegistry => false;

            public PersistenceParticipantSaveResult CapturePayload()
            {
                return PersistenceParticipantSaveResult.Success(JsonUtility.ToJson(new TestPayload { value = Value }));
            }

            public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
            {
                if (payloadSchemaVersion != ParticipantSchemaVersion)
                {
                    return PersistenceParticipantPrepareResult.Failure($"Unsupported schema {payloadSchemaVersion}.");
                }

                TestPayload payload = JsonUtility.FromJson<TestPayload>(payloadJson);
                return payload == null
                    ? PersistenceParticipantPrepareResult.Failure("Payload parse failed.")
                    : PersistenceParticipantPrepareResult.Success(payload);
            }

            public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
            {
                if (FailCommit)
                {
                    return PersistenceParticipantCommitResult.Failure("Injected commit failure.");
                }

                Value = ((TestPayload)preparedPayload).value;
                return PersistenceParticipantCommitResult.Success();
            }

            public void DiscardPreparedPayload(object preparedPayload)
            {
            }
        }
    }
}
