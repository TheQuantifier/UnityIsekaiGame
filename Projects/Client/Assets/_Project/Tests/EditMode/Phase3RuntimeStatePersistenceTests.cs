using System;
using System.IO;
using NUnit.Framework;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class Phase3RuntimeStatePersistenceTests
    {
        private string root;

        [SetUp]
        public void SetUp() => root = Path.Combine(Path.GetTempPath(), "UnityIsekaiGame", "Phase3Persistence", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void CurrentFormatIsCleanBreakSchemaThree()
        {
            Assert.That(PersistenceService.CurrentSchemaVersion, Is.EqualTo(3));
            Assert.That(Enum.GetNames(typeof(SaveCompatibilityStatus)), Does.Not.Contain("OlderSupported"));
            Assert.That(Enum.GetNames(typeof(SaveCompatibilityStatus)), Does.Not.Contain("MigrationRequired"));
            Assert.That(Enum.GetNames(typeof(SaveSlotKind)), Does.Not.Contain("Development"));
        }

        [Test]
        public void DescriptorCapturesOwnershipDependenciesAndRequirements()
        {
            TestParticipant participant = new TestParticipant("player.child", new[] { "player.parent" });
            PersistenceParticipantDescriptor descriptor = PersistenceParticipantDescriptor.From(participant);
            Assert.That(descriptor.Scope, Is.EqualTo(PersistenceScope.Player));
            Assert.That(descriptor.OwnerId, Is.EqualTo("local-player"));
            Assert.That(descriptor.RequiredDependencies, Is.EquivalentTo(new[] { "player.parent" }));
            Assert.That(descriptor.SupportsRollback, Is.True);
        }

        [Test]
        public void ReadinessReportsMissingRequiredParticipant()
        {
            PersistenceService service = CreateService();
            PlayerPersistenceContext context = new PlayerPersistenceContext(service);
            PersistenceReadinessReport report = context.BuildReadiness(new[] { "player.identity" });
            Assert.That(report.succeeded, Is.False);
            Assert.That(report.failures, Has.Some.Contains("player.identity"));
        }

        [Test]
        public void RequiredDependencyControlsLoadOrder()
        {
            PersistenceService service = CreateService();
            Assert.That(service.RegisterParticipant(new TestParticipant("player.child", new[] { "player.parent" }), out _), Is.True);
            Assert.That(service.RegisterParticipant(new TestParticipant("player.parent", Array.Empty<string>()), out _), Is.True);
            PersistenceDependencyReport report = service.BuildParticipantDependencyReport();
            Assert.That(report.succeeded, Is.True, report.message);
            Assert.That(Array.IndexOf(report.orderedParticipantKeys, "player.parent"), Is.LessThan(Array.IndexOf(report.orderedParticipantKeys, "player.child")));
        }

        [Test]
        public void MissingRequiredDependencyMakesContextNotReady()
        {
            PersistenceService service = CreateService();
            PlayerPersistenceContext context = new PlayerPersistenceContext(service);
            Assert.That(context.Register(new TestParticipant("player.child", new[] { "player.parent" }), out _), Is.True);
            PersistenceReadinessReport report = context.BuildReadiness(new[] { "player.child" });
            Assert.That(report.succeeded, Is.False);
            Assert.That(report.failures, Has.Some.Contains("player.parent"));
        }

        [Test]
        public void RequiredConsistencyValidatorCanRejectLoadAndRollback()
        {
            PersistenceService service = CreateService();
            TestParticipant participant = new TestParticipant("player.test", Array.Empty<string>()) { Value = 5 };
            Assert.That(service.RegisterParticipant(participant, out _), Is.True);
            Assert.That(service.Save("manual-1").Succeeded, Is.True);
            participant.Value = 9;
            Assert.That(service.RegisterConsistencyValidator(
                new DelegatePersistenceConsistencyValidator("test.invalid", true, () => PersistenceConsistencyAuditReport.Critical("Invalid", "Rejected by test.")),
                out _), Is.True);
            PersistenceLoadResult load = service.Load("manual-1");
            Assert.That(load.Succeeded, Is.False);
            Assert.That(participant.Value, Is.EqualTo(9), "The pre-load value should be restored after audit failure.");
        }

        private PersistenceService CreateService() => new PersistenceService(
            new PersistencePathProvider(root),
            worldId: "local-world",
            playerId: "local-player",
            accountId: "local-account",
            contextKind: PersistenceContextKind.Player);

        [Serializable]
        private sealed class Payload { public int value; }

        private sealed class TestParticipant : TypedPersistenceParticipant<Payload>
        {
            public TestParticipant(string key, string[] dependencies)
                : base(new PersistenceParticipantDescriptor(key, 1, true, PersistenceScope.Player, "local-player", PersistenceLoadPhase.Bootstrap, 0, dependencies, Array.Empty<string>(), true, false, false, false)) { }

            public int Value { get; set; }
            protected override bool TryCapture(out Payload saveData, out string failureReason) { saveData = new Payload { value = Value }; failureReason = string.Empty; return true; }
            protected override bool TryValidate(Payload saveData, out string failureReason) { failureReason = saveData == null ? "Missing payload." : string.Empty; return saveData != null; }
            protected override Payload CaptureRollback() => new Payload { value = Value };
            protected override bool TryRestore(Payload saveData, out string failureReason) { Value = saveData.value; failureReason = string.Empty; return true; }
        }
    }
}
