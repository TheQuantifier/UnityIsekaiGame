using System;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
        public void Server_project_resolves_all_shared_terrain_data()
        {
            const string terrainPath = "Packages/com.thequantifier.isekai.content/Content/World/Terrain/ServerCollision";
            string[] terrainGuids = AssetDatabase.FindAssets("t:TerrainData", new[] { terrainPath });

            Assert.That(terrainGuids, Has.Length.EqualTo(4));
            LogAssert.ignoreFailingMessages = true;
            try
            {
                TerrainData[] terrainData = terrainGuids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(path => AssetDatabase.LoadAssetAtPath<TerrainData>(path))
                    .ToArray();
                Assert.That(terrainData, Has.None.Null);
                Assert.That(terrainData.Sum(data => data.treeInstanceCount), Is.GreaterThan(0));
                TreePrototype[] treePrototypes = terrainData.SelectMany(data => data.treePrototypes).ToArray();
                Assert.That(treePrototypes, Is.Not.Empty);
                Assert.That(treePrototypes.All(prototype =>
                    prototype.prefab != null &&
                    prototype.prefab.GetComponentInChildren<Collider>(true) != null &&
                    prototype.prefab.GetComponentInChildren<MeshFilter>(true)?.sharedMesh != null &&
                    prototype.prefab.GetComponentInChildren<Renderer>(true)?.sharedMaterial != null), Is.True);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void Server_physics_and_layer_configuration_matches_the_client_project()
        {
            string serverRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            string projectsRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(serverRoot, ".."));
            string clientRoot = System.IO.Path.Combine(projectsRoot, "Client");
            foreach (string settingsFile in new[] { "DynamicsManager.asset", "TagManager.asset" })
            {
                string serverSettings = System.IO.Path.Combine(serverRoot, "ProjectSettings", settingsFile);
                string clientSettings = System.IO.Path.Combine(clientRoot, "ProjectSettings", settingsFile);
                Assert.That(System.IO.File.Exists(serverSettings), Is.True, serverSettings);
                Assert.That(System.IO.File.Exists(clientSettings), Is.True, clientSettings);
                Assert.That(
                    System.IO.File.ReadAllText(serverSettings),
                    Is.EqualTo(System.IO.File.ReadAllText(clientSettings)),
                    $"Client and server {settingsFile} must remain identical so collider layers behave authoritatively.");
            }
        }

        [Test]
        public void Server_scene_contains_authority_without_client_or_presentation_components()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            try
            {
                // Unity 6 warns that the deliberately invisible server tree marker does not use the legacy
                // Nature/Soft Occlusion shader. That shader is unavailable in this project and is irrelevant
                // to headless physics; the assertions below validate the collider data itself.
                LogAssert.ignoreFailingMessages = true;
                Scene scene = EditorSceneManager.OpenScene(ServerProjectBuildAutomation.ServerScenePath, OpenSceneMode.Single);
                Component[] serializedComponents = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .ToArray();
                Assert.That(serializedComponents, Has.None.Null, "The dedicated-server scene contains a missing script reference.");
                Component[] components = serializedComponents.Where(component => component != null).ToArray();

                Assert.That(components.OfType<NetworkManager>().SingleOrDefault(), Is.Not.Null);
                Assert.That(components.OfType<UnityTransport>().SingleOrDefault(), Is.Not.Null);
                // The build pipeline adds LocalDedicatedServer to a generated staging copy so a
                // build never mutates this tracked extracted source scene.
                Assert.That(components.OfType<LocalDedicatedServer>(), Is.Empty);
                Assert.That(components.OfType<Camera>(), Is.Empty);
                Assert.That(components.OfType<Renderer>(), Is.Empty);
                TerrainCollider[] terrainColliders = components.OfType<TerrainCollider>().ToArray();
                Assert.That(terrainColliders, Has.Length.EqualTo(4), "The authoritative scene must retain all four terrain tiles.");
                Assert.That(
                    terrainColliders.All(collider => collider.terrainData != null),
                    Is.True,
                    "Every authoritative TerrainCollider must resolve its shared TerrainData asset.");
                MeshCollider[] meshColliders = components.OfType<MeshCollider>().ToArray();
                Assert.That(meshColliders, Is.Not.Empty, "Detailed source collision such as stairs must remain mesh-accurate.");
                Assert.That(meshColliders.All(collider =>
                    collider.sharedMesh != null &&
                    AssetDatabase.GetAssetPath(collider.sharedMesh).StartsWith(
                        "Packages/com.thequantifier.isekai.content/Content/World/CollisionMeshes/",
                        StringComparison.Ordinal)), Is.True,
                    "Every server MeshCollider must use a shared collision-only mesh asset rather than client presentation geometry.");
                Collider[] colliders = components.OfType<Collider>().ToArray();
                Assert.That(colliders.Any(collider => collider.enabled && !collider.isTrigger), Is.True);
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
                LogAssert.ignoreFailingMessages = false;
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
            GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.thequantifier.isekai.content/Content/Networking/Prefabs/NetworkWorldItemPickup.prefab");

            Assert.That(list, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            Assert.That(pickup, Is.Not.Null);
            Assert.That(player.GetComponent<NetworkPlayerActor>(), Is.Not.Null);
            Assert.That(combat.GetComponent<NetworkCombatWorldState>(), Is.Not.Null);
            Assert.That(pickup.GetComponent<NetworkWorldItemPickup>(), Is.Not.Null);
            Assert.That(pickup.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(list.Contains(player), Is.True);
            Assert.That(list.Contains(combat), Is.True);
            Assert.That(list.Contains(pickup), Is.True);
        }
    }
}
