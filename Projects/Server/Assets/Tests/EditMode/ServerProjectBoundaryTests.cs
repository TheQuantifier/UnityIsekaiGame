using System;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Server;
using UnityIsekaiGame.ServerProject.Editor;

namespace UnityIsekaiGame.ServerProject.Tests
{
    public sealed class ServerProjectBoundaryTests
    {
        [Test]
        public void Server_project_registers_server_packages_but_not_client_package()
        {
            string[] packageNames = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .Select(package => package.name)
                .ToArray();
            Assert.That(packageNames, Does.Contain("com.thequantifier.isekai.server"));
            Assert.That(packageNames, Does.Contain("com.thequantifier.isekai.networking"));
            Assert.That(packageNames, Does.Contain("com.thequantifier.isekai.simulation"));
            Assert.That(packageNames, Does.Contain("com.thequantifier.isekai.content"));
            Assert.That(packageNames, Does.Not.Contain("com.thequantifier.isekai.client"));
        }

        [Test]
        public void Server_build_output_resolves_inside_the_repository()
        {
            string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            string actual = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, ServerProjectBuildAutomation.DefaultOutputPath));
            string repositoryRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, "..", ".."));
            string expected = System.IO.Path.Combine(repositoryRoot, "Builds", "LocalServer", "UnityIsekaiServer.exe");

            Assert.That(actual, Is.EqualTo(expected).IgnoreCase);
        }

        [Test]
        public void Server_scene_contains_authority_without_client_or_presentation_components()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ServerProjectBuildAutomation.ServerScenePath, OpenSceneMode.Single);
                Component[] serializedComponents = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .ToArray();
                Assert.That(serializedComponents, Has.None.Null, "The dedicated-server scene contains a missing script reference.");
                Component[] components = serializedComponents.Where(component => component != null).ToArray();

                Assert.That(components.OfType<NetworkManager>().SingleOrDefault(), Is.Not.Null);
                Assert.That(components.OfType<UnityTransport>().SingleOrDefault(), Is.Not.Null);
                Assert.That(components.OfType<LocalDedicatedServer>().SingleOrDefault(), Is.Not.Null);
                Assert.That(components.OfType<Camera>(), Is.Empty);
                Assert.That(components.OfType<Renderer>(), Is.Empty);
                Assert.That(components.Any(component => string.Equals(
                    component.GetType().Assembly.GetName().Name,
                    "UnityIsekaiGame.Networking.Client",
                    StringComparison.Ordinal)), Is.False);
                Assert.That(components.Any(component => string.Equals(
                    component.GetType().Assembly.GetName().Name,
                    "UnityIsekaiGame.UI",
                    StringComparison.Ordinal)), Is.False);
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

        [Test]
        public void Server_network_prefabs_are_registered_and_authoritative()
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.thequantifier.isekai.content/Content/Networking/Prefabs/NetworkPlayerActor.prefab");
            GameObject combat = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.thequantifier.isekai.content/Content/Networking/Prefabs/NetworkCombatWorldState.prefab");

            Assert.That(list, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            Assert.That(player.GetComponent<NetworkPlayerActor>(), Is.Not.Null);
            Assert.That(combat.GetComponent<NetworkCombatWorldState>(), Is.Not.Null);
            Assert.That(list.Contains(player), Is.True);
            Assert.That(list.Contains(combat), Is.True);
        }
    }
}
