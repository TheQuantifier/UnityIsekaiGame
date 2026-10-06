using System.IO;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityIsekaiGame.Networking;

namespace UnityIsekaiGame.Tests
{
    public sealed class PausedPlayerAuthorityTests
    {
        [Test]
        public void World_participation_state_is_explicit_and_rejects_unknown_values()
        {
            Assert.That(NetworkPlayerActor.IsKnownWorldParticipationState(NetworkPlayerWorldParticipationState.Active), Is.True);
            Assert.That(NetworkPlayerActor.IsKnownWorldParticipationState(NetworkPlayerWorldParticipationState.PausedProtected), Is.True);
            Assert.That(NetworkPlayerActor.IsKnownWorldParticipationState((NetworkPlayerWorldParticipationState)byte.MaxValue), Is.False);

            Assert.That(InventoryAuthorityFailure.PlayerPaused, Is.Not.EqualTo(InventoryAuthorityFailure.None));
            Assert.That(CombatAuthorityFailure.PlayerPaused, Is.Not.EqualTo(CombatAuthorityFailure.None));
            Assert.That(NarrativeAuthorityFailure.PlayerPaused, Is.Not.EqualTo(NarrativeAuthorityFailure.None));
        }

        [Test]
        public void Unspawned_player_actor_defaults_to_active_world_participation()
        {
            GameObject root = new GameObject("Paused Player State Test", typeof(NetworkObject));
            try
            {
                NetworkPlayerActor actor = root.AddComponent<NetworkPlayerActor>();

                Assert.That(actor.WorldParticipationState, Is.EqualTo(NetworkPlayerWorldParticipationState.Active));
                Assert.That(actor.IsWorldParticipationActive, Is.True);
                Assert.That(actor.IsPausedProtected, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Paused_state_is_enforced_at_all_authoritative_world_boundaries()
        {
            AssertSourceContains(
                "Packages/com.thequantifier.isekai.networking/Runtime/Shared/Networking/Replication/NetworkPlayerMovement.cs",
                "actor?.IsPausedProtected == true",
                "controller.enabled = active");
            AssertSourceContains(
                "Packages/com.thequantifier.isekai.networking/Runtime/Shared/Networking/Replication/NetworkPlayerVitals.cs",
                "actor?.IsPausedProtected == true",
                "IsAuthoritativeHealthAvailable");
            AssertSourceContains(
                "Packages/com.thequantifier.isekai.server/Runtime/Networking/ServerPlayerInventoryAuthority.cs",
                "InventoryAuthorityFailure.PlayerPaused",
                "World pickups are unavailable while the player is paused and protected.");
            AssertSourceContains(
                "Packages/com.thequantifier.isekai.server/Runtime/Networking/ServerPlayerCombatAuthority.cs",
                "CombatAuthorityFailure.PlayerPaused");
            AssertSourceContains(
                "Packages/com.thequantifier.isekai.server/Runtime/Networking/ServerPlayerNarrativeAuthority.cs",
                "NarrativeAuthorityFailure.PlayerPaused");
            AssertSourceContains(
                "Packages/com.thequantifier.isekai.server/Runtime/Networking/ServerCombatWorldAuthority.cs",
                "!actor.IsWorldParticipationActive");
            AssertSourceContains(
                "Packages/com.thequantifier.isekai.client/Runtime/UI/PauseMenuController.cs",
                "RequestPausedProtected(open)");
        }

        private static void AssertSourceContains(string repositoryRelativePath, params string[] expected)
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string source = File.ReadAllText(Path.Combine(repositoryRoot, repositoryRelativePath));
            for (int i = 0; i < expected.Length; i++)
            {
                StringAssert.Contains(expected[i], source, repositoryRelativePath);
            }
        }
    }
}
