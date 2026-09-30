using System;
using System.IO;
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
    }
}
