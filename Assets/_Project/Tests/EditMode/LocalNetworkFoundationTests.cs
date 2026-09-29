using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Editor;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;
using UnityIsekaiGame.Networking.Server;

namespace UnityIsekaiGame.Tests
{
    public sealed class LocalNetworkFoundationTests
    {
        [Test]
        public void Prototype_scene_contains_an_inert_local_network_runtime()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(LocalNetworkFoundationAuthoring.PrototypeScenePath, OpenSceneMode.Single);
                GameObject root = scene.GetRootGameObjects().Single(value => value.name == LocalNetworkFoundationAuthoring.NetworkRootName);
                NetworkManager manager = root.GetComponent<NetworkManager>();
                UnityTransport transport = root.GetComponent<UnityTransport>();

                Assert.That(manager, Is.Not.Null);
                Assert.That(transport, Is.Not.Null);
                Assert.That(root.GetComponent<LocalGameClient>(), Is.Not.Null);
                Assert.That(root.GetComponent<LocalPlayerMovementBridge>(), Is.Not.Null);
                LocalDedicatedServer server = root.GetComponent<LocalDedicatedServer>();
                Assert.That(server, Is.Not.Null);
                Assert.That(manager.NetworkConfig.NetworkTransport, Is.SameAs(transport));
                NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(LocalNetworkFoundationAuthoring.DefaultNetworkPrefabsPath);
                Assert.That(prefabList, Is.Not.Null);
                Assert.That(manager.NetworkConfig.Prefabs.NetworkPrefabsLists, Has.Count.EqualTo(1));
                Assert.That(manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Single(), Is.SameAs(prefabList));
                Assert.That(manager.NetworkConfig.ConnectionApproval, Is.True);
                Assert.That(manager.NetworkConfig.EnableSceneManagement, Is.False);
                Assert.That(manager.IsListening, Is.False);
                Assert.That(server.PlayerActorPrefab, Is.Not.Null);
                Assert.That(server.PlayerActorPrefab.GetComponent<NetworkObject>(), Is.Not.Null);
                Assert.That(server.PlayerActorPrefab.GetComponent<NetworkPlayerActor>(), Is.Not.Null);
                Assert.That(server.PlayerActorPrefab.GetComponent<NetworkPlayerMovement>(), Is.Not.Null);
                Assert.That(prefabList.Contains(server.PlayerActorPrefab), Is.True);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousPath))
                {
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }
    }
}
