using NUnit.Framework;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.Networking;

namespace UnityIsekaiGame.Tests
{
    public sealed class PlayerSessionAuthorityTests
    {
        [Test]
        public void Session_registry_opens_queries_and_closes_authoritative_session()
        {
            PlayerSessionRegistry registry = new PlayerSessionRegistry();
            string playerId = Id('a');

            Assert.That(registry.TryOpen(42UL, "client.instance.one", playerId, out PlayerSessionSnapshot active, out string failure), Is.True, failure);
            Assert.That(active.Phase, Is.EqualTo(PlayerSessionPhase.Active));
            Assert.That(active.PlayerId, Is.EqualTo(playerId));
            Assert.That(active.PersonId, Is.EqualTo("person.player." + playerId));
            Assert.That(active.ActorId, Is.EqualTo("actor.player." + playerId));
            Assert.That(active.SessionId, Is.EqualTo("session.42.1"));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGetByClientId(42UL, out PlayerSessionSnapshot byClient), Is.True);
            Assert.That(byClient, Is.EqualTo(active));
            Assert.That(registry.TryGetByPlayerId(playerId, out PlayerSessionSnapshot byPlayer), Is.True);
            Assert.That(byPlayer, Is.EqualTo(active));

            Assert.That(registry.TryClose(42UL, out PlayerSessionSnapshot closed), Is.True);
            Assert.That(closed.Phase, Is.EqualTo(PlayerSessionPhase.Disconnected));
            Assert.That(closed.SessionId, Is.EqualTo(active.SessionId));
            Assert.That(closed.EndedAtUnixMilliseconds, Is.GreaterThanOrEqualTo(closed.StartedAtUnixMilliseconds));
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.TryGetByPlayerId(playerId, out _), Is.False);
        }

        [Test]
        public void Session_registry_rejects_duplicate_client_and_player_ownership()
        {
            PlayerSessionRegistry registry = new PlayerSessionRegistry();
            Assert.That(registry.TryOpen(1UL, "client.one", Id('a'), out _, out string firstFailure), Is.True, firstFailure);

            Assert.That(registry.TryOpen(1UL, "client.two", Id('b'), out _, out string clientFailure), Is.False);
            Assert.That(clientFailure, Does.Contain("Client 1"));
            Assert.That(registry.TryOpen(2UL, "client.two", Id('a'), out _, out string playerFailure), Is.False);
            Assert.That(playerFailure, Does.Contain("already owns"));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Session_registry_allows_player_to_reconnect_after_disconnect()
        {
            PlayerSessionRegistry registry = new PlayerSessionRegistry();
            Assert.That(registry.TryOpen(1UL, "client.one", Id('a'), out PlayerSessionSnapshot first, out string firstFailure), Is.True, firstFailure);
            Assert.That(registry.TryClose(1UL, out _), Is.True);
            Assert.That(registry.TryOpen(7UL, "client.reconnected", Id('a'), out PlayerSessionSnapshot second, out string secondFailure), Is.True, secondFailure);

            Assert.That(second.ActorId, Is.EqualTo(first.ActorId), "A player's actor identity must remain stable across sessions.");
            Assert.That(second.PersonId, Is.EqualTo(first.PersonId), "A player's narrative identity must remain stable across sessions.");
            Assert.That(second.SessionId, Is.Not.EqualTo(first.SessionId));
            Assert.That(second.ClientId, Is.EqualTo(7UL));
        }

        [Test]
        public void Network_player_actor_accepts_server_identity_before_spawn()
        {
            GameObject root = new GameObject("Network Player Actor Test");
            try
            {
                root.AddComponent<NetworkObject>();
                NetworkPlayerActor actor = root.AddComponent<NetworkPlayerActor>();
                PlayerSessionRegistry registry = new PlayerSessionRegistry();
                Assert.That(registry.TryOpen(5UL, "client.five", Id('a'), out PlayerSessionSnapshot session, out string failure), Is.True, failure);

                actor.ConfigureServer(session);

                Assert.That(actor.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(actor.ClientInstanceId, Is.EqualTo(session.ClientInstanceId));
                Assert.That(actor.PlayerId, Is.EqualTo(session.PlayerId));
                Assert.That(actor.PersonId, Is.EqualTo(session.PersonId));
                Assert.That(actor.ActorId, Is.EqualTo(session.ActorId));
                Assert.That(actor.SessionRevision, Is.EqualTo(session.Revision));
                Assert.That(actor.HasIdentity, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Private_session_identity_is_owner_only_but_world_identity_and_pause_state_are_observable()
        {
            AssertReadPermission("sessionId", NetworkVariableReadPermission.Owner);
            AssertReadPermission("clientInstanceId", NetworkVariableReadPermission.Owner);
            AssertReadPermission("playerId", NetworkVariableReadPermission.Owner);
            AssertReadPermission("sessionRevision", NetworkVariableReadPermission.Owner);
            AssertReadPermission("personId", NetworkVariableReadPermission.Everyone);
            AssertReadPermission("actorId", NetworkVariableReadPermission.Everyone);
            AssertReadPermission("worldParticipationState", NetworkVariableReadPermission.Everyone);
        }

        [Test]
        public void Authored_player_actor_prefab_is_registered_for_network_spawning()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnityIsekaiGame.Editor.LocalNetworkFoundationAuthoring.PlayerActorPrefabPath);
            NetworkPrefabsList prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(UnityIsekaiGame.Editor.LocalNetworkFoundationAuthoring.DefaultNetworkPrefabsPath);

            Assert.That(prefab, Is.Not.Null);
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(networkObject.InScenePlaced, Is.False);
            Assert.That(prefab.GetComponent<NetworkPlayerActor>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkPlayerMovement>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkPlayerInventory>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(prefabs, Is.Not.Null);
            Assert.That(prefabs.Contains(prefab), Is.True);
        }

        private static string Id(char value) => new string(value, AccountAuthenticationProtocol.SecureUserIdLength);

        private static void AssertReadPermission(string fieldName, NetworkVariableReadPermission expected)
        {
            FieldInfo field = typeof(NetworkPlayerActor).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing NetworkPlayerActor field '{fieldName}'.");

            GameObject root = new GameObject($"Permission Test {fieldName}", typeof(NetworkObject));
            try
            {
                NetworkPlayerActor actor = root.AddComponent<NetworkPlayerActor>();
                var variable = field.GetValue(actor) as NetworkVariableBase;
                Assert.That(variable, Is.Not.Null);
                Assert.That(variable.ReadPerm, Is.EqualTo(expected), fieldName);
                Assert.That(variable.WritePerm, Is.EqualTo(NetworkVariableWritePermission.Server), fieldName);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
