using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class PersistenceServiceFoundationTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "UnityIsekaiGamePersistenceTests", Guid.NewGuid().ToString("N"));
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
        public void PathProvider_AcceptsSafeSlotIdsAndRejectsTraversal()
        {
            PersistencePathProvider provider = new PersistencePathProvider(testRoot);

            Assert.That(provider.IsValidSlotId("slot-0001"), Is.True);
            Assert.That(provider.IsValidSlotId("slot_0001"), Is.True);
            Assert.That(provider.IsValidSlotId("../slot"), Is.False);
            Assert.That(provider.IsValidSlotId("slot/0001"), Is.False);
            Assert.That(provider.IsValidSlotId("slot:0001"), Is.False);

            Assert.That(provider.TryGetPaths("slot-0001", out SaveSlotPaths paths, out string failureReason), Is.True, failureReason);
            Assert.That(paths.PrimaryPath, Does.EndWith("slot-0001.json"));
            Assert.That(paths.BackupPath, Does.EndWith("slot-0001.backup.json"));
            Assert.That(paths.TemporaryPath, Does.EndWith("slot-0001.tmp"));
        }

        [Test]
        public void Save_CreatesDirectoryWritesEnvelopeAndListsMetadata()
        {
            TestState state = new TestState { Value = 7, Note = "saved", Flag = true };
            PersistenceService service = CreateService(state);

            PersistenceSaveResult result = service.Save("slot-0001", "Test Slot");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(File.Exists(result.Path), Is.True);
            Assert.That(Directory.Exists(testRoot), Is.True);

            string json = File.ReadAllText(result.Path);
            GameSaveEnvelope envelope = JsonUtility.FromJson<GameSaveEnvelope>(json);
            Assert.That(envelope.formatIdentifier, Is.EqualTo(PersistenceService.FormatIdentifier));
            Assert.That(envelope.schemaVersion, Is.EqualTo(PersistenceService.CurrentSchemaVersion));
            Assert.That(envelope.displayName, Is.EqualTo("Test Slot"));
            Assert.That(envelope.worldId, Is.EqualTo(PersistenceService.LocalWorldId));
            Assert.That(envelope.playerId, Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(envelope.accountId, Is.EqualTo(PersistenceService.LocalAccountId));
            Assert.That(envelope.participants, Has.Count.EqualTo(1));
            Assert.That(envelope.participants[0].persistenceScope, Is.EqualTo((int)PersistenceScope.Player));
            Assert.That(envelope.participants[0].ownerId, Is.EqualTo(PersistenceService.LocalPlayerId));
            Assert.That(envelope.contentChecksum, Is.EqualTo(PersistenceService.ComputeChecksum(envelope)));

            Assert.That(service.ListSaveSlots(), Has.Count.EqualTo(1));
            Assert.That(service.ListSaveSlots()[0].isValid, Is.True);
        }

        [Test]
        public void Load_RestoresPreparedStateOnlyAfterSuccessfulValidation()
        {
            TestState state = new TestState { Value = 5, Note = "before", Flag = false };
            PersistenceService service = CreateService(state);

            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            state.Value = 99;
            state.Note = "changed";
            state.Flag = true;

            PersistenceLoadResult result = service.Load("slot-0001");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(state.Value, Is.EqualTo(5));
            Assert.That(state.Note, Is.EqualTo("before"));
            Assert.That(state.Flag, Is.False);
        }

        [Test]
        public void Load_InvalidPayloadDoesNotMutateLiveState()
        {
            TestState state = new TestState { Value = 2, Note = "good", Flag = true };
            PersistenceService service = CreateService(state);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            SaveSlotPaths paths = Paths("slot-0001");
            GameSaveEnvelope envelope = JsonUtility.FromJson<GameSaveEnvelope>(File.ReadAllText(paths.PrimaryPath));
            envelope.participants[0].payloadJson = JsonUtility.ToJson(new PrototypePersistenceStateSaveData { testValue = 1000000, note = "bad", flag = false });
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));

            state.Value = 44;
            state.Note = "live";
            state.Flag = false;
            PersistenceLoadResult result = service.Load("slot-0001");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(PersistenceLoadStatus.ParticipantPrepareFailed));
            Assert.That(state.Value, Is.EqualTo(44));
            Assert.That(state.Note, Is.EqualTo("live"));
            Assert.That(state.Flag, Is.False);
        }

        [Test]
        public void Save_PreservesExistingPrimaryAfterFailedParticipantCapture()
        {
            TestState state = new TestState { Value = 1, Note = "first" };
            PersistenceService service = CreateService(state);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            string firstJson = File.ReadAllText(Paths("slot-0001").PrimaryPath);

            state.FailCapture = true;
            PersistenceSaveResult result = service.Save("slot-0001");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(PersistenceSaveStatus.ParticipantCaptureFailed));
            Assert.That(File.ReadAllText(Paths("slot-0001").PrimaryPath), Is.EqualTo(firstJson));
        }

        [Test]
        public void Save_CreatesOneBackupFromPreviousPrimary()
        {
            TestState state = new TestState { Value = 1, Note = "first" };
            PersistenceService service = CreateService(state);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            string firstJson = File.ReadAllText(Paths("slot-0001").PrimaryPath);

            state.Value = 2;
            state.Note = "second";
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            Assert.That(File.Exists(Paths("slot-0001").BackupPath), Is.True);
            Assert.That(File.ReadAllText(Paths("slot-0001").BackupPath), Is.EqualTo(firstJson));
        }

        [Test]
        public void Load_PrimaryCorruptReportsBackupAvailableAndExplicitBackupLoadRestores()
        {
            TestState state = new TestState { Value = 1, Note = "first" };
            PersistenceService service = CreateService(state);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            state.Value = 2;
            state.Note = "second";
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);

            File.WriteAllText(Paths("slot-0001").PrimaryPath, "{ bad json");
            state.Value = 100;
            state.Note = "live";

            PersistenceLoadResult primary = service.Load("slot-0001");
            Assert.That(primary.Succeeded, Is.False);
            Assert.That(primary.Status, Is.EqualTo(PersistenceLoadStatus.BackupAvailable));
            Assert.That(primary.BackupAvailable, Is.True);
            Assert.That(state.Value, Is.EqualTo(100));

            PersistenceLoadResult backup = service.Load("slot-0001", loadBackup: true);
            Assert.That(backup.Succeeded, Is.True, backup.Message);
            Assert.That(backup.LoadedBackup, Is.True);
            Assert.That(state.Value, Is.EqualTo(1));
            Assert.That(state.Note, Is.EqualTo("first"));
        }

        [Test]
        public void Validate_ReportsEmptyTruncatedWrongFormatAndFutureVersion()
        {
            PersistenceService service = CreateService(new TestState());
            SaveSlotPaths paths = Paths("slot-0001");
            Directory.CreateDirectory(testRoot);

            File.WriteAllText(paths.PrimaryPath, string.Empty);
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.MalformedJson));

            File.WriteAllText(paths.PrimaryPath, "{");
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.MalformedJson));

            GameSaveEnvelope envelope = ValidEnvelope("slot-0001");
            envelope.formatIdentifier = "Wrong";
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.WrongFormatIdentifier));

            envelope = ValidEnvelope("slot-0001");
            envelope.schemaVersion = PersistenceService.CurrentSchemaVersion + 1;
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.UnsupportedSchemaVersion));

            envelope = ValidEnvelope("slot-0001");
            envelope.schemaVersion = PersistenceService.CurrentSchemaVersion - 1;
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.UnsupportedSchemaVersion));
        }

        [Test]
        public void Validate_DetectsChecksumMismatchDuplicateParticipantAndMissingRequiredPayload()
        {
            PersistenceService service = CreateService(new TestState());
            SaveSlotPaths paths = Paths("slot-0001");
            Directory.CreateDirectory(testRoot);

            GameSaveEnvelope envelope = ValidEnvelope("slot-0001");
            envelope.displayName = "Changed Without Checksum";
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.ChecksumMismatch));

            envelope = ValidEnvelope("slot-0001");
            envelope.participants.Add(envelope.participants[0]);
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.DuplicateParticipantKey));

            envelope = ValidEnvelope("slot-0001");
            envelope.participants.Clear();
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.ValidateSlot("slot-0001").Status, Is.EqualTo(PersistenceValidationStatus.MissingRequiredParticipantPayload));
        }

        [Test]
        public void Registration_PreventsDuplicateKeysAndOrderingIsDeterministic()
        {
            PersistenceService service = new PersistenceService(new PersistencePathProvider(testRoot));
            TestState late = new TestState { Value = 1 };
            TestState early = new TestState { Value = 2 };
            TestParticipant lateParticipant = new TestParticipant("test.late", late, PersistenceLoadPhase.Prototype, 10, true);
            TestParticipant earlyParticipant = new TestParticipant("test.early", early, PersistenceLoadPhase.Bootstrap, 0, true);

            Assert.That(service.RegisterParticipant(lateParticipant, out string failure), Is.True, failure);
            Assert.That(service.RegisterParticipant(earlyParticipant, out failure), Is.True, failure);
            Assert.That(service.RegisterParticipant(new TestParticipant("test.early", new TestState(), PersistenceLoadPhase.Prototype, 0, true), out failure), Is.False);

            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            GameSaveEnvelope envelope = JsonUtility.FromJson<GameSaveEnvelope>(File.ReadAllText(Paths("slot-0001").PrimaryPath));
            Assert.That(envelope.participants[0].participantKey, Is.EqualTo("test.early"));
            Assert.That(envelope.participants[1].participantKey, Is.EqualTo("test.late"));
        }

        [Test]
        public void OptionalUnknownPayloadIsIgnoredButRequiredUnknownPayloadFails()
        {
            PersistenceService service = CreateService(new TestState());
            SaveSlotPaths paths = Paths("slot-0001");
            Directory.CreateDirectory(testRoot);

            GameSaveEnvelope envelope = ValidEnvelope("slot-0001");
            envelope.participants.Add(new SaveParticipantRecord
            {
                participantKey = "future.optional",
                participantSchemaVersion = 1,
                required = false,
                loadPhase = 999,
                payloadJson = "{}"
            });
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.Load("slot-0001").Succeeded, Is.True);

            envelope = ValidEnvelope("slot-0001");
            envelope.participants.Add(new SaveParticipantRecord
            {
                participantKey = "future.required",
                participantSchemaVersion = 1,
                required = true,
                loadPhase = 999,
                payloadJson = "{}"
            });
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            File.WriteAllText(paths.PrimaryPath, JsonUtility.ToJson(envelope, true));
            Assert.That(service.Load("slot-0001").Status, Is.EqualTo(PersistenceLoadStatus.MissingRuntimeParticipant));
        }

        [Test]
        public void Delete_RemovesPrimaryBackupAndTemporaryFiles()
        {
            TestState state = new TestState { Value = 1 };
            PersistenceService service = CreateService(state);
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            state.Value = 2;
            Assert.That(service.Save("slot-0001").Succeeded, Is.True);
            File.WriteAllText(Paths("slot-0001").TemporaryPath, "stale");

            PersistenceDeleteResult result = service.DeleteSlot("slot-0001");

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(File.Exists(Paths("slot-0001").PrimaryPath), Is.False);
            Assert.That(File.Exists(Paths("slot-0001").BackupPath), Is.False);
            Assert.That(File.Exists(Paths("slot-0001").TemporaryPath), Is.False);
        }

        [Test]
        public void OperationLockRejectsNestedSave()
        {
            TestState state = new TestState { Value = 1 };
            PersistenceService service = new PersistenceService(new PersistencePathProvider(testRoot));
            service.RegisterParticipant(new TestParticipant(PrototypePersistenceStateParticipant.Key, state, PersistenceLoadPhase.Prototype, 0, true, service), out _);

            PersistenceSaveResult result = service.Save("slot-0001");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(PersistenceSaveStatus.ParticipantCaptureFailed));
        }

        private PersistenceService CreateService(TestState state)
        {
            PersistenceService service = new PersistenceService(new PersistencePathProvider(testRoot));
            Assert.That(service.RegisterParticipant(new TestParticipant(PrototypePersistenceStateParticipant.Key, state, PersistenceLoadPhase.Prototype, 0, true), out string failure), Is.True, failure);
            return service;
        }

        private SaveSlotPaths Paths(string slotId)
        {
            PersistencePathProvider provider = new PersistencePathProvider(testRoot);
            Assert.That(provider.TryGetPaths(slotId, out SaveSlotPaths paths, out string failure), Is.True, failure);
            return paths;
        }

        private static GameSaveEnvelope ValidEnvelope(string slotId)
        {
            GameSaveEnvelope envelope = new GameSaveEnvelope
            {
                formatIdentifier = PersistenceService.FormatIdentifier,
                schemaVersion = PersistenceService.CurrentSchemaVersion,
                gameVersion = "test",
                saveId = "save-test",
                slotId = slotId,
                displayName = slotId,
                worldId = PersistenceService.LocalWorldId,
                playerId = PersistenceService.LocalPlayerId,
                accountId = PersistenceService.LocalAccountId,
                createdUtc = "2026-01-01T00:00:00.0000000Z",
                lastWrittenUtc = "2026-01-01T00:00:00.0000000Z",
                sceneSummary = "test",
                placeSummary = "test",
                playerSummary = "test"
            };
            envelope.participants.Add(new SaveParticipantRecord
            {
                participantKey = PrototypePersistenceStateParticipant.Key,
                participantSchemaVersion = PrototypePersistenceStateParticipant.CurrentParticipantSchemaVersion,
                required = true,
                persistenceScope = (int)PersistenceScope.Player,
                ownerId = PersistenceService.LocalPlayerId,
                loadPhase = (int)PersistenceLoadPhase.Prototype,
                payloadJson = JsonUtility.ToJson(new PrototypePersistenceStateSaveData { testValue = 3, note = "valid", flag = true })
            });
            envelope.contentChecksum = PersistenceService.ComputeChecksum(envelope);
            return envelope;
        }

        private sealed class TestState : IPrototypePersistenceState
        {
            public int Value;
            public string Note;
            public bool Flag;
            public bool FailCapture;

            public PrototypePersistenceStateSaveData CreateSaveData()
            {
                if (FailCapture)
                {
                    return null;
                }

                return new PrototypePersistenceStateSaveData
                {
                    testValue = Value,
                    note = Note,
                    flag = Flag
                };
            }

            public void RestoreFromSaveData(PrototypePersistenceStateSaveData saveData)
            {
                Value = saveData.testValue;
                Note = saveData.note;
                Flag = saveData.flag;
            }
        }

        private sealed class TestParticipant : IPersistenceParticipant
        {
            private readonly string key;
            private readonly TestState state;
            private readonly PersistenceLoadPhase phase;
            private readonly int priority;
            private readonly bool required;
            private readonly PersistenceService nestedSaveService;
            private readonly PrototypePersistenceStateParticipant inner;

            public TestParticipant(string key, TestState state, PersistenceLoadPhase phase, int priority, bool required, PersistenceService nestedSaveService = null)
            {
                this.key = key;
                this.state = state;
                this.phase = phase;
                this.priority = priority;
                this.required = required;
                this.nestedSaveService = nestedSaveService;
                inner = new PrototypePersistenceStateParticipant(state);
            }

            public string ParticipantKey => key;
            public int ParticipantSchemaVersion => PrototypePersistenceStateParticipant.CurrentParticipantSchemaVersion;
            public bool IsRequired => required;
            public PersistenceScope Scope => PersistenceScope.Player;
            public string OwnerId => PersistenceService.LocalPlayerId;
            public PersistenceLoadPhase LoadPhase => phase;
            public int LoadPriority => priority;

            public PersistenceParticipantSaveResult CapturePayload()
            {
                if (nestedSaveService != null)
                {
                    PersistenceSaveResult nested = nestedSaveService.Save("slot-nested");
                    return PersistenceParticipantSaveResult.Failure(nested.Message);
                }

                return inner.CapturePayload();
            }

            public PersistenceParticipantPrepareResult PreparePayload(string payloadJson, int payloadSchemaVersion)
            {
                return inner.PreparePayload(payloadJson, payloadSchemaVersion);
            }

            public PersistenceParticipantCommitResult CommitPreparedPayload(object preparedPayload)
            {
                return inner.CommitPreparedPayload(preparedPayload);
            }

            public void DiscardPreparedPayload(object preparedPayload)
            {
                inner.DiscardPreparedPayload(preparedPayload);
            }
        }

    }
}
