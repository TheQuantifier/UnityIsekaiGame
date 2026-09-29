using NUnit.Framework;
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
            ConnectionRequestPayload request = Request("client.instance.one", "Player.One");

            Assert.That(registry.TryOpen(42UL, request, out PlayerSessionSnapshot active, out string failure), Is.True, failure);
            Assert.That(active.Phase, Is.EqualTo(PlayerSessionPhase.Active));
            Assert.That(active.PlayerId, Is.EqualTo("Player.One"));
            Assert.That(active.ActorId, Is.EqualTo("actor.player.player.one"));
            Assert.That(active.SessionId, Is.EqualTo("session.42.1"));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGetByClientId(42UL, out PlayerSessionSnapshot byClient), Is.True);
            Assert.That(byClient, Is.EqualTo(active));
            Assert.That(registry.TryGetByPlayerId("player.one", out PlayerSessionSnapshot byPlayer), Is.True);
            Assert.That(byPlayer, Is.EqualTo(active));

            Assert.That(registry.TryClose(42UL, out PlayerSessionSnapshot closed), Is.True);
            Assert.That(closed.Phase, Is.EqualTo(PlayerSessionPhase.Disconnected));
            Assert.That(closed.SessionId, Is.EqualTo(active.SessionId));
            Assert.That(closed.EndedAtUnixMilliseconds, Is.GreaterThanOrEqualTo(closed.StartedAtUnixMilliseconds));
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.TryGetByPlayerId("player.one", out _), Is.False);
        }

        [Test]
        public void Session_registry_rejects_duplicate_client_and_player_ownership()
        {
            PlayerSessionRegistry registry = new PlayerSessionRegistry();
            Assert.That(registry.TryOpen(1UL, Request("client.one", "player.one"), out _, out string firstFailure), Is.True, firstFailure);

            Assert.That(registry.TryOpen(1UL, Request("client.two", "player.two"), out _, out string clientFailure), Is.False);
            Assert.That(clientFailure, Does.Contain("Client 1"));
            Assert.That(registry.TryOpen(2UL, Request("client.two", "PLAYER.ONE"), out _, out string playerFailure), Is.False);
            Assert.That(playerFailure, Does.Contain("already owns"));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Session_registry_allows_player_to_reconnect_after_disconnect()
        {
            PlayerSessionRegistry registry = new PlayerSessionRegistry();
            Assert.That(registry.TryOpen(1UL, Request("client.one", "player.one"), out PlayerSessionSnapshot first, out string firstFailure), Is.True, firstFailure);
            Assert.That(registry.TryClose(1UL, out _), Is.True);
            Assert.That(registry.TryOpen(7UL, Request("client.reconnected", "player.one"), out PlayerSessionSnapshot second, out string secondFailure), Is.True, secondFailure);

            Assert.That(second.ActorId, Is.EqualTo(first.ActorId), "A player's actor identity must remain stable across sessions.");
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
                Assert.That(registry.TryOpen(5UL, Request("client.five", "player.five"), out PlayerSessionSnapshot session, out string failure), Is.True, failure);

                actor.ConfigureServer(session);

                Assert.That(actor.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(actor.ClientInstanceId, Is.EqualTo(session.ClientInstanceId));
                Assert.That(actor.PlayerId, Is.EqualTo(session.PlayerId));
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
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(prefabs, Is.Not.Null);
            Assert.That(prefabs.Contains(prefab), Is.True);
        }

        private static ConnectionRequestPayload Request(string instanceId, string playerId) =>
            new ConnectionRequestPayload(instanceId, playerId, "0.1.0");
    }
}
