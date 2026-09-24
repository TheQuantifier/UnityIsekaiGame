using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests.EditMode
{
    public sealed class SaveSlotsAutosaveTests
    {
        private string root;
        private GameObject owner;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "UnityIsekaiGame", "SaveSlots", Guid.NewGuid().ToString("N"));
            owner = new GameObject("Save slot test helpers");
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void CatalogCreatesOnlyCurrentManualAndAutosaveSlots()
        {
            Assert.That(PrototypeSaveSlotCatalog.ManualSlotId(0), Is.EqualTo("manual-1"));
            Assert.That(PrototypeSaveSlotCatalog.AutosaveSlotId(0), Is.EqualTo("autosave-0"));
            Assert.That(PrototypeSaveSlotCatalog.CurrentWorldCheckpointSlotId, Is.EqualTo("world-current"));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrototypeSaveSlotCatalog.ManualSlotId(-1));
        }

        [Test]
        public void AutosaveRotationPromotesStagingAndPreservesNewestFirst()
        {
            PersistenceService service = CreateService();
            TestParticipant participant = new TestParticipant();
            Assert.That(service.RegisterParticipant(participant, out _), Is.True);
            participant.Value = 1;
            Assert.That(service.Save(PrototypeSaveSlotCatalog.AutosaveStagingSlotId).Succeeded, Is.True);
            Assert.That(service.RotateAutosaveSlots(PrototypeSaveSlotCatalog.AutosaveStagingSlotId, PrototypeSaveSlotCatalog.BuildAutosaveSlotIds(3)).Succeeded, Is.True);
            participant.Value = 2;
            Assert.That(service.Save(PrototypeSaveSlotCatalog.AutosaveStagingSlotId).Succeeded, Is.True);
            Assert.That(service.RotateAutosaveSlots(PrototypeSaveSlotCatalog.AutosaveStagingSlotId, PrototypeSaveSlotCatalog.BuildAutosaveSlotIds(3)).Succeeded, Is.True);

            participant.Value = 0;
            Assert.That(service.Load("autosave-0").Succeeded, Is.True);
            Assert.That(participant.Value, Is.EqualTo(2));
            Assert.That(service.Load("autosave-1").Succeeded, Is.True);
            Assert.That(participant.Value, Is.EqualTo(1));
        }

        [Test]
        public void DirtyTrackerOnlyChangesWhenStateActuallyChanges()
        {
            GameSaveDirtyTracker tracker = owner.AddComponent<GameSaveDirtyTracker>();
            int notifications = 0;
            tracker.DirtyStateChanged += (_, _) => notifications++;
            tracker.MarkDirty("Inventory changed.");
            tracker.MarkDirty("Inventory changed.");
            tracker.MarkClean("Saved.");
            Assert.That(tracker.IsDirty, Is.False);
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void PlayTimeCanExcludeOpenMenuTime()
        {
            PlayTimeTracker tracker = owner.AddComponent<PlayTimeTracker>();
            tracker.Restore(12d);
            tracker.SetMenuOpen(true);
            Assert.That(tracker.CumulativeSeconds, Is.GreaterThanOrEqualTo(12d));
            tracker.SetMenuOpen(false);
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
            public TestParticipant() : base(new PersistenceParticipantDescriptor(
                "player.autosave-test", 1, true, PersistenceScope.Player, "local-player",
                PersistenceLoadPhase.Bootstrap, 0, Array.Empty<string>(), Array.Empty<string>(), true, false, false, false)) { }

            public int Value { get; set; }
            protected override bool TryCapture(out Payload saveData, out string failureReason) { saveData = new Payload { value = Value }; failureReason = string.Empty; return true; }
            protected override bool TryValidate(Payload saveData, out string failureReason) { failureReason = saveData == null ? "Missing payload." : string.Empty; return saveData != null; }
            protected override Payload CaptureRollback() => new Payload { value = Value };
            protected override bool TryRestore(Payload saveData, out string failureReason) { Value = saveData.value; failureReason = string.Empty; return true; }
        }
    }
}
