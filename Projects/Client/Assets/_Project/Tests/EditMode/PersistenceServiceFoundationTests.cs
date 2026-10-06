using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityIsekaiGame.GameData.Persistence;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class PersistenceServiceFoundationTests
    {
        private string root;

        [SetUp]
        public void SetUp() => root = Path.Combine(Path.GetTempPath(), "UnityIsekaiGame", "PersistenceFoundation", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void PlayerAndWorldContextsRejectForeignScopes()
        {
            PersistenceService player = Service(PersistenceContextKind.Player);
            PersistenceService world = Service(PersistenceContextKind.World);
            Assert.That(player.RegisterParticipant(new TestParticipant("player.test", PersistenceScope.Player, "local-player"), out _), Is.True);
            Assert.That(player.RegisterParticipant(new TestParticipant("world.test", PersistenceScope.SharedWorld, "local-world"), out string playerFailure), Is.False);
            Assert.That(world.RegisterParticipant(new TestParticipant("world.test", PersistenceScope.SharedWorld, "local-world"), out _), Is.True);
            Assert.That(world.RegisterParticipant(new TestParticipant("player.test", PersistenceScope.Player, "local-player"), out string worldFailure), Is.False);
            StringAssert.Contains("cannot be registered", playerFailure);
            StringAssert.Contains("cannot be registered", worldFailure);
        }

        [Test]
        public void SaveAndLoadRoundTripRestoresTypedPayload()
        {
            PersistenceService service = Service(PersistenceContextKind.Player);
            TestParticipant participant = new TestParticipant("player.test", PersistenceScope.Player, "local-player") { Value = 41 };
            Assert.That(service.RegisterParticipant(participant, out string failure), Is.True, failure);
            PersistenceSaveResult save = service.Save("manual-1", "Manual 1");
            Assert.That(save.Succeeded, Is.True, save.Message);
            participant.Value = 99;
            PersistenceLoadResult load = service.Load("manual-1");
            Assert.That(load.Succeeded, Is.True, load.Message);
            Assert.That(participant.Value, Is.EqualTo(41));
            GameSaveEnvelope envelope = PersistenceSerialization.Deserialize<GameSaveEnvelope>(File.ReadAllText(save.Path));
            Assert.That(envelope.schemaVersion, Is.EqualTo(PersistenceService.CurrentSchemaVersion));
            Assert.That(envelope.persistenceContext, Is.EqualTo((int)PersistenceContextKind.Player));
            Assert.That(envelope.completedWriteMarker, Is.True);
            Assert.That(envelope.participants[0].payloadJson, Does.StartWith("{"));
            string persistedJson = File.ReadAllText(save.Path);
            Assert.That(persistedJson, Does.Contain("\"payload\":"));
            Assert.That(persistedJson, Does.Not.Contain("\"payloadJson\""));
        }

        [Test]
        public void PreparedSaveWritesImmutableSnapshotOffThreadAndCompletesOnCaller()
        {
            PersistenceService service = Service(PersistenceContextKind.World);
            TestParticipant participant = new TestParticipant("world.test", PersistenceScope.SharedWorld, "local-world") { Value = 41 };
            Assert.That(service.RegisterParticipant(participant, out string registrationFailure), Is.True, registrationFailure);

            Assert.That(
                service.TryPrepareSave("world-current", "World Checkpoint", out PreparedPersistenceSave prepared, out PersistenceSaveResult prepareFailure),
                Is.True,
                prepareFailure?.Message);
            Assert.That(service.OperationInProgress, Is.True);
            Assert.That(File.Exists(prepared.Path), Is.False);

            participant.Value = 99;
            PersistenceSaveResult write = Task.Run(() => service.WritePreparedSave(prepared)).GetAwaiter().GetResult();
            Assert.That(write.Succeeded, Is.True, write.Message);
            Assert.That(service.OperationInProgress, Is.True, "The simulation thread must publish completion explicitly.");
            Assert.That(service.CompletePreparedSave(prepared, write).Succeeded, Is.True);
            Assert.That(service.OperationInProgress, Is.False);

            string persisted = File.ReadAllText(prepared.Path);
            Assert.That(persisted, Does.Not.Contain(Environment.NewLine), "Server checkpoints should use compact JSON to minimize serialization and disk work.");
            participant.Value = 7;
            Assert.That(service.Load("world-current").Succeeded, Is.True);
            Assert.That(participant.Value, Is.EqualTo(41), "The background writer must persist the captured snapshot, not later live mutations.");
        }

        [Test]
        public void IncrementalSaveCaptureHonorsParticipantBudgetAcrossTicks()
        {
            PersistenceService service = Service(PersistenceContextKind.World);
            Assert.That(service.RegisterParticipant(new TestParticipant("world.first", PersistenceScope.SharedWorld, "local-world") { Value = 1 }, out _), Is.True);
            Assert.That(service.RegisterParticipant(new TestParticipant("world.second", PersistenceScope.SharedWorld, "local-world") { Value = 2 }, out _), Is.True);
            Assert.That(service.TryBeginSaveCapture("world-current", "World Checkpoint", out PreparedPersistenceSaveCapture capture, out PersistenceSaveResult beginFailure), Is.True, beginFailure?.Message);

            Assert.That(service.TryContinueSaveCapture(capture, 1, out PreparedPersistenceSave firstTick, out PersistenceSaveResult firstFailure), Is.True, firstFailure?.Message);
            Assert.That(firstTick, Is.Null);
            Assert.That(capture.CapturedParticipantCount, Is.EqualTo(1));
            Assert.That(capture.IsComplete, Is.False);

            Assert.That(service.TryContinueSaveCapture(capture, 1, out PreparedPersistenceSave secondTick, out PersistenceSaveResult secondFailure), Is.True, secondFailure?.Message);
            Assert.That(secondTick, Is.Not.Null);
            Assert.That(capture.IsComplete, Is.True);
            PersistenceSaveResult write = service.WritePreparedSave(secondTick);
            Assert.That(service.CompletePreparedSave(secondTick, write).Succeeded, Is.True, write.Message);
        }

        [Test]
        public void PreparedSaveReusesSuccessfulPayloadWhileParticipantRevisionIsUnchanged()
        {
            PersistenceService service = Service(PersistenceContextKind.World);
            RevisionTestParticipant participant = new RevisionTestParticipant { Value = 7, Revision = 1L };
            Assert.That(service.RegisterParticipant(participant, out string registrationFailure), Is.True, registrationFailure);

            Assert.That(service.TryPrepareSave("world-current", "World", out PreparedPersistenceSave first, out PersistenceSaveResult firstFailure), Is.True, firstFailure?.Message);
            PersistenceSaveResult firstWrite = service.WritePreparedSave(first);
            Assert.That(service.CompletePreparedSave(first, firstWrite).Succeeded, Is.True, firstWrite.Message);
            Assert.That(participant.CaptureCount, Is.EqualTo(1));

            Assert.That(service.TryPrepareSave("world-current", "World", out PreparedPersistenceSave second, out PersistenceSaveResult secondFailure), Is.True, secondFailure?.Message);
            Assert.That(second.ReusedParticipantCount, Is.EqualTo(1));
            PersistenceSaveResult secondWrite = service.WritePreparedSave(second);
            Assert.That(service.CompletePreparedSave(second, secondWrite).Succeeded, Is.True, secondWrite.Message);
            Assert.That(participant.CaptureCount, Is.EqualTo(1), "An unchanged revision should reuse the validated payload.");

            participant.Value = 9;
            participant.Revision++;
            Assert.That(service.TryPrepareSave("world-current", "World", out PreparedPersistenceSave third, out PersistenceSaveResult thirdFailure), Is.True, thirdFailure?.Message);
            PersistenceSaveResult thirdWrite = service.WritePreparedSave(third);
            Assert.That(service.CompletePreparedSave(third, thirdWrite).Succeeded, Is.True, thirdWrite.Message);
            Assert.That(participant.CaptureCount, Is.EqualTo(2), "A changed revision must be captured again.");
        }

        [Test]
        public void WrongWorldIsRejectedBeforeChecksumValidation()
        {
            PersistenceService service = Service(PersistenceContextKind.Player);
            Assert.That(service.RegisterParticipant(new TestParticipant("player.test", PersistenceScope.Player, "local-player"), out _), Is.True);
            PersistenceSaveResult save = service.Save("manual-1");
            GameSaveEnvelope envelope = PersistenceSerialization.Deserialize<GameSaveEnvelope>(File.ReadAllText(save.Path));
            envelope.worldId = "other-world";
            File.WriteAllText(save.Path, PersistenceSerialization.Serialize(envelope, true));
            PersistenceValidationResult validation = service.ValidateSlot("manual-1");
            Assert.That(validation.Status, Is.EqualTo(PersistenceValidationStatus.WrongWorld));
        }

        [Test]
        public void TamperingPayloadBreaksChecksum()
        {
            PersistenceService service = Service(PersistenceContextKind.Player);
            Assert.That(service.RegisterParticipant(new TestParticipant("player.test", PersistenceScope.Player, "local-player") { Value = 4 }, out _), Is.True);
            PersistenceSaveResult save = service.Save("manual-1");
            GameSaveEnvelope envelope = PersistenceSerialization.Deserialize<GameSaveEnvelope>(File.ReadAllText(save.Path));
            envelope.participants[0].payloadJson = PersistenceSerialization.Serialize(new TestPayload { value = 900 });
            File.WriteAllText(save.Path, PersistenceSerialization.Serialize(envelope, true));
            Assert.That(service.ValidateSlot("manual-1").Status, Is.EqualTo(PersistenceValidationStatus.ChecksumMismatch));
        }

        [Test]
        public void ChecksumUsesUnambiguousCanonicalFieldBoundaries()
        {
            Assert.That(PersistenceService.ComputeChecksum(Envelope("a", "bc")), Is.Not.EqualTo(PersistenceService.ComputeChecksum(Envelope("ab", "c"))));
            Assert.That(
                PersistenceService.ComputeChecksum(Envelope("a", "bc")),
                Is.EqualTo("4002f60e1f3f9a0d62e91aade1a41a896afb6109992220330de7043fc4fbbc98"),
                "The incremental checksum must remain byte-for-byte compatible with existing saves.");
        }

        [Test]
        public void SerializerRejectsUnknownSaveFields()
        {
            Assert.That(() => PersistenceSerialization.Deserialize<TestPayload>("{\"value\":1,\"removedLegacyValue\":2}"), Throws.Exception);
        }

        [Test]
        public void PathProviderRejectsTraversal()
        {
            PersistencePathProvider paths = new PersistencePathProvider(root);
            Assert.That(paths.TryGetPaths("../outside", out _, out _), Is.False);
            Assert.That(paths.TryGetPaths("manual-1", out SaveSlotPaths valid, out _), Is.True);
            Assert.That(valid.PrimaryPath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase), Is.True);
        }

        private PersistenceService Service(PersistenceContextKind kind) => new PersistenceService(
            new PersistencePathProvider(Path.Combine(root, kind.ToString())),
            worldId: "local-world",
            playerId: kind == PersistenceContextKind.Player ? "local-player" : string.Empty,
            accountId: "local-account",
            contextKind: kind);

        private static GameSaveEnvelope Envelope(string displayName, string playerSummary) => new GameSaveEnvelope
        {
            formatIdentifier = PersistenceService.FormatIdentifier,
            schemaVersion = PersistenceService.CurrentSchemaVersion,
            persistenceContext = (int)PersistenceContextKind.Player,
            gameVersion = "0.4.1-prototype",
            saveId = "save",
            slotId = "manual-1",
            displayName = displayName,
            worldId = "local-world",
            playerId = "local-player",
            accountId = "local-account",
            createdUtc = "2026-01-01T00:00:00Z",
            lastWrittenUtc = "2026-01-01T00:00:00Z",
            playerSummary = playerSummary,
            completedWriteMarker = true
        };

        [Serializable]
        private sealed class TestPayload { public int value; }

        private sealed class TestParticipant : TypedPersistenceParticipant<TestPayload>
        {
            public TestParticipant(string key, PersistenceScope scope, string owner)
                : base(new PersistenceParticipantDescriptor(key, 1, true, scope, owner, PersistenceLoadPhase.Bootstrap, 0, Array.Empty<string>(), Array.Empty<string>(), true, false, false, false)) { }

            public int Value { get; set; }
            protected override bool TryCapture(out TestPayload saveData, out string failureReason) { saveData = new TestPayload { value = Value }; failureReason = string.Empty; return true; }
            protected override bool TryValidate(TestPayload saveData, out string failureReason) { failureReason = saveData == null ? "Payload is missing." : string.Empty; return saveData != null; }
            protected override TestPayload CaptureRollback() => new TestPayload { value = Value };
            protected override bool TryRestore(TestPayload saveData, out string failureReason) { Value = saveData.value; failureReason = string.Empty; return true; }
        }

        private sealed class RevisionTestParticipant : TypedPersistenceParticipant<TestPayload>, IPersistenceRevisionParticipant
        {
            public RevisionTestParticipant()
                : base(new PersistenceParticipantDescriptor("world.revision-test", 1, true, PersistenceScope.SharedWorld, "local-world", PersistenceLoadPhase.Bootstrap, 0, Array.Empty<string>(), Array.Empty<string>(), true, false, false, false)) { }

            public int Value { get; set; }
            public long Revision { get; set; }
            public int CaptureCount { get; private set; }
            public long PersistenceRevision => Revision;
            protected override bool TryCapture(out TestPayload saveData, out string failureReason) { CaptureCount++; saveData = new TestPayload { value = Value }; failureReason = string.Empty; return true; }
            protected override bool TryValidate(TestPayload saveData, out string failureReason) { failureReason = saveData == null ? "Payload is missing." : string.Empty; return saveData != null; }
            protected override TestPayload CaptureRollback() => new TestPayload { value = Value };
            protected override bool TryRestore(TestPayload saveData, out string failureReason) { Value = saveData.value; Revision++; failureReason = string.Empty; return true; }
        }
    }
}
