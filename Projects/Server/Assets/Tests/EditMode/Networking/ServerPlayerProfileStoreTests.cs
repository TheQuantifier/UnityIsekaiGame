using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Server;

namespace UnityIsekaiGame.Tests
{
    public sealed class ServerPlayerProfileStoreTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "UnityIsekaiGame-ServerProfileTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void Profile_round_trip_preserves_authoritative_identity_and_gameplay_state()
        {
            PlayerSessionSnapshot session = OpenSession("Persistent.Player");
            ServerPlayerProfileData original = CreateProfile(session);
            ServerPlayerProfileStore store = new ServerPlayerProfileStore(root);

            Assert.That(store.TrySave(original, out string saveMessage), Is.True, saveMessage);
            Assert.That(store.TryLoad(session, out ServerPlayerProfileData loaded, out string loadMessage), Is.True, loadMessage);

            Assert.That(loaded.playerId, Is.EqualTo(session.PlayerId));
            Assert.That(loaded.personId, Is.EqualTo(session.PersonId));
            Assert.That(loaded.actorId, Is.EqualTo(session.ActorId));
            Assert.That(loaded.Position, Is.EqualTo(new Vector3(12.5f, 3f, -7.25f)));
            Assert.That(loaded.yawDegrees, Is.EqualTo(275f));
            Assert.That(loaded.vitals.Health, Is.EqualTo(73f));
            Assert.That(loaded.vitals.MaximumStamina, Is.EqualTo(240f));
            Assert.That(loaded.inventory.slotCapacity, Is.EqualTo(24));
            Assert.That(loaded.equipment, Is.Not.Null);
        }

        [Test]
        public void Corrupt_primary_profile_recovers_previous_valid_backup()
        {
            PlayerSessionSnapshot session = OpenSession("backup-player");
            ServerPlayerProfileData profile = CreateProfile(session);
            ServerPlayerProfileStore store = new ServerPlayerProfileStore(root);
            Assert.That(store.TrySave(profile, out string firstMessage), Is.True, firstMessage);
            profile.revision = 2L;
            profile.positionX = 99f;
            Assert.That(store.TrySave(profile, out string secondMessage), Is.True, secondMessage);

            string primary = Directory.GetFiles(root, "server-profile.json", SearchOption.AllDirectories)[0];
            File.WriteAllText(primary, "{ corrupt");

            Assert.That(store.TryLoad(session, out ServerPlayerProfileData recovered, out string loadMessage), Is.True, loadMessage);
            Assert.That(recovered.revision, Is.EqualTo(1L));
            Assert.That(recovered.positionX, Is.EqualTo(12.5f));
            Assert.That(loadMessage, Does.Contain("backup"));
        }

        [Test]
        public void Profile_load_rejects_a_different_player_identity()
        {
            PlayerSessionSnapshot first = OpenSession("first-player");
            PlayerSessionSnapshot second = OpenSession("second-player");
            ServerPlayerProfileStore store = new ServerPlayerProfileStore(root);
            Assert.That(store.TrySave(CreateProfile(first), out string saveMessage), Is.True, saveMessage);

            string firstDirectory = Directory.GetDirectories(Path.Combine(root, "Players"))[0];
            string secondDirectory = Path.Combine(root, "Players", second.PlayerId.ToLowerInvariant());
            Directory.CreateDirectory(secondDirectory);
            File.Copy(Path.Combine(firstDirectory, "server-profile.json"), Path.Combine(secondDirectory, "server-profile.json"));

            Assert.That(store.TryLoad(second, out _, out string failure), Is.False);
            Assert.That(failure, Does.Contain("identity"));
        }

        [Test]
        public void Profile_save_rejects_non_finite_authoritative_state()
        {
            PlayerSessionSnapshot session = OpenSession("invalid-numeric-player");
            ServerPlayerProfileData profile = CreateProfile(session);
            ServerPlayerProfileStore store = new ServerPlayerProfileStore(root);
            profile.positionX = float.NaN;

            Assert.That(store.TrySave(profile, out string transformFailure), Is.False);
            Assert.That(transformFailure, Does.Contain("non-finite"));

            profile.positionX = 12.5f;
            NetworkVitalsState invalidVitals = profile.vitals;
            invalidVitals.Mana = float.PositiveInfinity;
            profile.vitals = invalidVitals;
            Assert.That(store.TrySave(profile, out string vitalsFailure), Is.False);
            Assert.That(vitalsFailure, Does.Contain("vitals"));
        }

        [Test]
        public void Background_write_queue_flushes_the_latest_profile_revision()
        {
            PlayerSessionSnapshot session = OpenSession("queued-player");
            ServerPlayerProfileData profile = CreateProfile(session);
            ServerPlayerProfileStore store = new ServerPlayerProfileStore(root);
            using (var queue = new ServerPlayerProfileWriteQueue(store))
            {
                Assert.That(queue.TryEnqueue(profile, out string firstMessage), Is.True, firstMessage);
                profile.revision = 2L;
                profile.positionX = 42f;
                Assert.That(queue.TryEnqueue(profile, out string secondMessage), Is.True, secondMessage);
                Assert.That(queue.Flush(TimeSpan.FromSeconds(5)), Is.True);
            }

            Assert.That(store.TryLoad(session, out ServerPlayerProfileData loaded, out string loadMessage), Is.True, loadMessage);
            Assert.That(loaded.revision, Is.EqualTo(2L));
            Assert.That(loaded.positionX, Is.EqualTo(42f));
        }

        [Test]
        public void World_checkpoint_queue_writes_prepared_snapshot_without_blocking_runtime_capture()
        {
            string worldRoot = Path.Combine(root, "World");
            PersistenceService service = new PersistenceService(
                new PersistencePathProvider(worldRoot),
                worldId: PersistenceService.LocalWorldId,
                accountId: PersistenceService.LocalAccountId,
                contextKind: PersistenceContextKind.World);
            WorldTestParticipant participant = new WorldTestParticipant { Value = 12 };
            Assert.That(service.RegisterParticipant(participant, out string registrationFailure), Is.True, registrationFailure);
            Assert.That(
                service.TryPrepareSave("world-current", "World Checkpoint", out PreparedPersistenceSave prepared, out PersistenceSaveResult prepareFailure),
                Is.True,
                prepareFailure?.Message);

            using (var queue = new ServerWorldCheckpointWriteQueue(service))
            {
                Assert.That(queue.TryEnqueue(prepared, out string queueMessage), Is.True, queueMessage);
                Assert.That(queue.Flush(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(queue.TryDequeueResult(out ServerWorldCheckpointWriteResult completed), Is.True);
                Assert.That(completed.Prepared, Is.SameAs(prepared));
                Assert.That(service.CompletePreparedSave(completed.Prepared, completed.Result).Succeeded, Is.True, completed.Result.Message);
            }

            participant.Value = 99;
            Assert.That(service.Load("world-current").Succeeded, Is.True);
            Assert.That(participant.Value, Is.EqualTo(12));
        }

        private static PlayerSessionSnapshot OpenSession(string playerId)
        {
            PlayerSessionRegistry registry = new PlayerSessionRegistry();
            using SHA256 sha = SHA256.Create();
            string secureUserId = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(playerId))).Replace("-", string.Empty).ToLowerInvariant();
            Assert.That(registry.TryOpen(
                1UL,
                "test-client",
                secureUserId,
                out PlayerSessionSnapshot session,
                out string failure), Is.True, failure);
            return session;
        }

        [Test]
        public void Persistence_capture_metrics_preserve_raw_frames_and_report_distribution()
        {
            var samples = new[]
            {
                new ServerPersistenceCaptureSample(1, "world.first", 0.04d, 10L),
                new ServerPersistenceCaptureSample(2, "world.second", 0.12d, 20L),
                new ServerPersistenceCaptureSample(3, "world.third", 0.14d, 30L),
                new ServerPersistenceCaptureSample(4, "world.slowest", 2.0d, 40L)
            };

            ServerPersistenceCaptureSummary summary = ServerPersistenceCaptureMetrics.Summarize(samples);

            Assert.That(summary.Count, Is.EqualTo(4));
            Assert.That(summary.TotalMilliseconds, Is.EqualTo(2.3d).Within(0.0001d));
            Assert.That(summary.MeanMilliseconds, Is.EqualTo(0.575d).Within(0.0001d));
            Assert.That(summary.MedianMilliseconds, Is.EqualTo(0.13d).Within(0.0001d));
            Assert.That(summary.ModeBucketMilliseconds, Is.EqualTo(0.1d).Within(0.0001d));
            Assert.That(summary.MaximumMilliseconds, Is.EqualTo(2d));
            Assert.That(summary.MaximumParticipantKey, Is.EqualTo("world.slowest"));
            Assert.That(summary.TotalAllocatedBytes, Is.EqualTo(100L));
            Assert.That(summary.MaximumAllocatedBytes, Is.EqualTo(40L));
            Assert.That(summary.MaximumAllocationParticipantKey, Is.EqualTo("world.slowest"));
            Assert.That(
                ServerPersistenceCaptureMetrics.FormatSamples(samples),
                Is.EqualTo("1|world.first|0.040|10;2|world.second|0.120|20;3|world.third|0.140|30;4|world.slowest|2.000|40"));
        }

        [Test]
        public void Profile_snapshot_comparison_ignores_save_metadata_but_detects_gameplay_changes()
        {
            PlayerSessionSnapshot session = OpenSession("profile-dirty-check");
            ServerPlayerProfileData profile = CreateProfile(session);
            ServerPlayerProfileData snapshot = profile.Clone();

            profile.revision += 10L;
            profile.savedAtUnixMilliseconds += 1000L;
            Assert.That(profile.HasSamePersistentState(snapshot), Is.True);

            profile.positionX += 0.25f;
            Assert.That(profile.HasSamePersistentState(snapshot), Is.False);
            profile.positionX = snapshot.positionX;
            profile.inventory.entries.Add(new InventoryEntrySaveData
            {
                mode = InventoryEntrySaveMode.DefinitionStack,
                definitionId = "item.test",
                quantity = 2
            });
            Assert.That(profile.HasSamePersistentState(snapshot), Is.False);
            Assert.That(snapshot.inventory.entries, Is.Empty, "The queued snapshot must not share mutable inventory lists.");
        }

        private static ServerPlayerProfileData CreateProfile(PlayerSessionSnapshot session)
        {
            return ServerPlayerProfileData.Create(
                session,
                new Vector3(12.5f, 3f, -7.25f),
                275f,
                new NetworkVitalsState(73f, 150f, 180f, 240f, 91f, 120f, NetworkActorLifeState.Active, 8u),
                new InventorySaveData { slotCapacity = 24 },
                new EquipmentSaveData());
        }

        [Serializable]
        private sealed class WorldTestPayload
        {
            public int value;
        }

        private sealed class WorldTestParticipant : TypedPersistenceParticipant<WorldTestPayload>
        {
            public WorldTestParticipant()
                : base(new PersistenceParticipantDescriptor(
                    "world.test",
                    1,
                    true,
                    PersistenceScope.SharedWorld,
                    PersistenceService.LocalWorldId,
                    PersistenceLoadPhase.Bootstrap,
                    0,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    true,
                    false,
                    false,
                    false))
            {
            }

            public int Value { get; set; }

            protected override bool TryCapture(out WorldTestPayload saveData, out string failureReason)
            {
                saveData = new WorldTestPayload { value = Value };
                failureReason = string.Empty;
                return true;
            }

            protected override bool TryValidate(WorldTestPayload saveData, out string failureReason)
            {
                failureReason = saveData == null ? "Payload is missing." : string.Empty;
                return saveData != null;
            }

            protected override WorldTestPayload CaptureRollback() => new WorldTestPayload { value = Value };

            protected override bool TryRestore(WorldTestPayload saveData, out string failureReason)
            {
                Value = saveData.value;
                failureReason = string.Empty;
                return true;
            }
        }
    }
}
