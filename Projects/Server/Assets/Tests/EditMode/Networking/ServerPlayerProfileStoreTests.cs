using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Equipment;
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

        private static PlayerSessionSnapshot OpenSession(string playerId)
        {
            PlayerSessionRegistry registry = new PlayerSessionRegistry();
            Assert.That(registry.TryOpen(1UL, new ConnectionRequestPayload("test-client", playerId, "test"), out PlayerSessionSnapshot session, out string failure), Is.True, failure);
            return session;
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
    }
}
