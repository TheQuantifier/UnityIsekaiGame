using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class PersistenceIntegrationRecoveryTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "UnityIsekaiGamePersistenceIntegrationTests", Guid.NewGuid().ToString("N"));
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
        public void DependencyGraphOrdersCurrentPlayerParticipantsIndependentOfRegistrationOrder()
        {
            PersistenceService service = Service();
            service.RegisterParticipant(new TestParticipant("player.location", 4, new[] { "player.quests-contracts" }), out _);
            service.RegisterParticipant(new TestParticipant("player.quests-contracts", 3, new[] { "player.status-effects" }), out _);
            service.RegisterParticipant(new TestParticipant("player.status-effects", 2, new[] { "player.inventory-equipment" }), out _);
            service.RegisterParticipant(new TestParticipant("player.inventory-equipment", 1), out _);

            PersistenceDependencyReport report = service.BuildParticipantDependencyReport();

            Assert.That(report.succeeded, Is.True, report.message);
            Assert.That(report.orderedParticipantKeys, Is.EqualTo(new[]
            {
                "player.inventory-equipment",
                "player.status-effects",
                "player.quests-contracts",
                "player.location"
            }));
        }

        [Test]
        public void MissingRequiredDependencyRejectsSaveBeforeCapture()
        {
            TestParticipant stats = new TestParticipant("player.status-effects", 2, requiredDependencies: new[] { "player.inventory-equipment" });
            PersistenceService service = Service(stats);

            PersistenceSaveResult result = service.Save("manual-1");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(PersistenceSaveStatus.DependencyValidationFailed));
            Assert.That(stats.CaptureCount, Is.EqualTo(0));
        }

        [Test]
        public void CircularDependencyIsReported()
        {
            PersistenceService service = Service(
                new TestParticipant("a", 1, requiredDependencies: new[] { "b" }),
                new TestParticipant("b", 2, requiredDependencies: new[] { "a" }));

            PersistenceDependencyReport report = service.BuildParticipantDependencyReport();

            Assert.That(report.succeeded, Is.False);
            Assert.That(report.circularDependencies, Is.Not.Empty);
        }

        [Test]
        public void PrepareFailureLeavesLiveFingerprintUnchanged()
        {
            TestParticipant participant = new TestParticipant("player.test-state", 1);
            PersistenceService service = Service(participant);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);

            participant.Value = 99;
            string before = service.BuildRuntimeStateFingerprint();
            service.FaultInjection.nextFailurePoint = PersistenceFaultInjectionPoint.LoadPrepare;

            PersistenceLoadResult result = service.Load("manual-1");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(PersistenceLoadStatus.ParticipantPrepareFailed));
            Assert.That(service.BuildRuntimeStateFingerprint(), Is.EqualTo(before));
            Assert.That(participant.Value, Is.EqualTo(99));
        }

        [Test]
        public void CommitFailureRollsBackPreviouslyCommittedParticipants()
        {
            TestParticipant first = new TestParticipant("test.first", 1);
            TestParticipant second = new TestParticipant("test.second", 2) { FailCommit = true };
            PersistenceService service = Service(first, second);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);

            first.Value = 100;
            second.Value = 200;
            string before = service.BuildRuntimeStateFingerprint();

            PersistenceLoadResult result = service.Load("manual-1");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(PersistenceLoadStatus.ParticipantCommitFailedRollbackSucceeded));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(service.BuildRuntimeStateFingerprint(), Is.EqualTo(before));
            Assert.That(service.RuntimeSafety, Is.EqualTo(PersistenceRuntimeSafety.RolledBack));
        }

        [Test]
        public void CriticalAuditFailureRollsBackCommittedLoad()
        {
            TestParticipant participant = new TestParticipant("player.test-state", 1);
            PersistenceService service = Service(participant);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);

            participant.Value = 42;
            string before = service.BuildRuntimeStateFingerprint();
            service.RegisterConsistencyValidator(
                new UnityIsekaiGame.Persistence.DelegatePersistenceConsistencyValidator(
                    "test.failure",
                    true,
                    () => PersistenceConsistencyAuditReport.Critical("DuplicateItemInstance", "Injected duplicate item instance.")),
                out _);

            PersistenceLoadResult result = service.Load("manual-1");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(PersistenceLoadStatus.ConsistencyAuditFailedRollbackSucceeded));
            Assert.That(service.BuildRuntimeStateFingerprint(), Is.EqualTo(before));
        }

        [Test]
        public void RecoveryScanReportsValidBackupAndStaleTemporary()
        {
            TestParticipant participant = new TestParticipant("player.test-state", 1);
            PersistenceService service = Service(participant);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            participant.Value = 2;
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            File.WriteAllText(Paths("manual-1").TemporaryPath, File.ReadAllText(Paths("manual-1").BackupPath));

            SaveRecoveryScanReport report = service.ScanRecoverySources();

            Assert.That(report.staleTemporaryFiles, Has.Length.EqualTo(1));
            Assert.That(HasValidBackupCandidate(report), Is.True);
            Assert.That(report.recommendation, Does.Contain("LoadBackup"));
        }

        [Test]
        public void PromoteBackupReplacesCorruptPrimaryWithValidBackup()
        {
            TestParticipant participant = new TestParticipant("player.test-state", 1);
            PersistenceService service = Service(participant);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            participant.Value = 2;
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            File.WriteAllText(Paths("manual-1").PrimaryPath, "{ bad json");

            PersistenceSaveResult promotion = service.PromoteBackup("manual-1");

            Assert.That(promotion.Succeeded, Is.True, promotion.Message);
            Assert.That(service.ValidateSlot("manual-1").Succeeded, Is.True);
            Assert.That(Directory.GetFiles(testRoot, "*.quarantine.*.json"), Is.Not.Empty);
        }

        private PersistenceService Service(params TestParticipant[] participants)
        {
            PersistenceService service = new PersistenceService(new PersistencePathProvider(testRoot));
            for (int i = 0; i < participants.Length; i++)
            {
                Assert.That(service.RegisterParticipant(participants[i], out string failure), Is.True, failure);
            }

            return service;
        }

        private SaveSlotPaths Paths(string slotId)
        {
            PersistencePathProvider provider = new PersistencePathProvider(testRoot);
            Assert.That(provider.TryGetPaths(slotId, out SaveSlotPaths paths, out string failure), Is.True, failure);
            return paths;
        }

        private static bool HasValidBackupCandidate(SaveRecoveryScanReport report)
        {
            if (report?.candidates == null)
            {
                return false;
            }

            for (int i = 0; i < report.candidates.Length; i++)
            {
                SaveRecoveryCandidate candidate = report.candidates[i];
                if (candidate != null && candidate.source == SaveRecoverySource.RequestedBackup && candidate.valid)
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class TestPayload
        {
            public int value;
        }

        private sealed class TestParticipant : IPersistenceParticipant, IPersistenceParticipantDependencies
        {
            private readonly string key;
            private readonly string[] requiredDependencies;

            public TestParticipant(string key, int value, string[] requiredDependencies = null)
            {
                this.key = key;
                Value = value;
                this.requiredDependencies = requiredDependencies ?? Array.Empty<string>();
            }

            public int Value;
            public bool FailCommit;
            public int CaptureCount;

            public string ParticipantKey => key;
            public int ParticipantSchemaVersion => 1;
            public bool IsRequired => true;
            public PersistenceScope Scope => PersistenceScope.Player;
            public string OwnerId => PersistenceService.LocalPlayerId;
            public PersistenceLoadPhase LoadPhase => PersistenceLoadPhase.Bootstrap;
            public int LoadPriority => 0;
            public IReadOnlyList<string> RequiredDependencies => requiredDependencies;
            public IReadOnlyList<string> OptionalDependencies => Array.Empty<string>();
            public bool SupportsRollback => true;
            public bool RequiresSceneReadiness => false;
            public bool RequiresDefinitionRegistry => false;
            public bool RequiresWorldEntityRegistry => false;

            public PersistenceParticipantSaveResult CapturePayload()
            {
                CaptureCount++;
                return PersistenceParticipantSaveResult.Success(PersistenceSerialization.Serialize(new TestPayload { value = Value }));
            }

            public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
            {
                TestPayload payload = PersistenceSerialization.Deserialize<TestPayload>(payloadJson);
                return payload == null
                    ? PersistenceParticipantPrepareResult.Failure("Payload parse failed.")
                    : PersistenceParticipantPrepareResult.Success(payload);
            }

            public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
            {
                if (FailCommit)
                {
                    return PersistenceParticipantCommitResult.Failure("Injected participant commit failure.");
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
