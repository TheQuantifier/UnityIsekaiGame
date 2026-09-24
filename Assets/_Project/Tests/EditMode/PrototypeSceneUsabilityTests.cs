using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityIsekaiGame.Configuration;

namespace UnityIsekaiGame.Tests
{
    public sealed class PrototypeSceneUsabilityTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string PrototypeTerrainRoot = "Assets/_Project/Prototype/Environment/Terrain";
        private const string PrototypeVegetationPrefabRoot = "Assets/_Project/Prototype/Environment/Vegetation/Prefabs";
        private const string PrototypeMedievalHousePrefabPath = "Assets/_Project/Prototype/Prefabs/Buildings/PrototypeMedievalHouse/Prototype Medieval House.prefab";
        private const string PrototypeMovementSettingsPath = "Assets/_Project/Prototype/Content/Configuration/PrototypePlayerMovementSettings.asset";
        private const string InputActionsPath = "Assets/_Project/Configuration/Input/InputSystem_Actions.inputactions";

        [Test]
        public void PrototypeSceneKeepsCleanPlayableTestingShell()
        {
            string scene = File.ReadAllText(ScenePath);

            AssertSceneContains(scene, "EventSystem");
            AssertSceneContains(scene, "HUD Canvas");
            AssertSceneContains(scene, "Inventory Canvas");
            AssertSceneContains(scene, "Interaction Prompt Canvas");
            AssertSceneContains(scene, "PrototypeScene");
            AssertSceneContains(scene, "Environment");
            AssertSceneContains(scene, "Ground");
            AssertSceneContains(scene, "Boundaries");
            AssertSceneContains(scene, "Lighting");
            AssertSceneContains(scene, "Landmarks");
            AssertSceneContains(scene, "Buildings");
            AssertSceneContains(scene, "Player");
            AssertSceneContains(scene, "Prototype Player");
            AssertSceneContains(scene, "Spawn Points");
            AssertSceneContains(scene, "Prototype Player Spawn");
            AssertSceneContains(scene, "Gameplay");
            AssertSceneContains(scene, "Items");
            AssertSceneContains(scene, "Combat");
            AssertSceneContains(scene, "NPCs");
            AssertSceneContains(scene, "Quests");
            AssertSceneContains(scene, "Knowledge");
            AssertSceneContains(scene, "Biology");
            AssertSceneContains(scene, "UI");
            AssertSceneContains(scene, "Test Infrastructure");
            AssertSceneContains(scene, "Game Persistence Coordinator");

            AssertSceneDoesNotContain(scene, "Prototype Ground");
            AssertSceneDoesNotContain(scene, "Ground - Main Prototype");
            AssertSceneDoesNotContain(scene, "Prototype Systems World");
            AssertSceneDoesNotContain(scene, "Systems World Safety Floor");
            AssertSceneDoesNotContain(scene, "Hub - Systems World");
            AssertSceneDoesNotContain(scene, "Zone - ");
            AssertSceneDoesNotContain(scene, "Prototype Enemy");
            AssertSceneDoesNotContain(scene, "Prototype Damage Dummy");
            AssertSceneDoesNotContain(scene, "Prototype Dialogue NPC");
            AssertSceneDoesNotContain(scene, "Prototype Quest Investigation Area");
            AssertSceneDoesNotContain(scene, "Prototype Contract Board");
            AssertSceneDoesNotContain(scene, "Prototype Delivery Crate");
            AssertSceneDoesNotContain(scene, "Status Applicator - ");
            AssertSceneDoesNotContain(scene, "Sign - ");
        }

        [Test]
        public void PrototypeSceneTestPointIdsAreUniqueAndComplete()
        {
            string scene = File.ReadAllText(ScenePath);
            MatchCollection matches = Regex.Matches(scene, @"testPointId:\s*(test-point\.[^\r\n]+)");
            HashSet<string> ids = new HashSet<string>();

            foreach (Match match in matches)
            {
                Assert.That(ids.Add(match.Groups[1].Value.Trim()), Is.True, $"Duplicate test point ID: {match.Groups[1].Value}");
            }

            Assert.That(ids, Does.Contain("test-point.spawn"));
            Assert.That(ids, Does.Contain("test-point.items"));
            Assert.That(ids, Does.Contain("test-point.equipment"));
            Assert.That(ids, Does.Contain("test-point.combat"));
            Assert.That(ids, Does.Contain("test-point.magic-status"));
            Assert.That(ids, Does.Contain("test-point.npc-quest"));
            Assert.That(ids, Does.Contain("test-point.contract-board"));
            Assert.That(ids, Does.Contain("test-point.investigation-area"));
        }

        [Test]
        public void PrototypeSceneHasSingleEventSystem()
        {
            string scene = File.ReadAllText(ScenePath);
            MatchCollection matches = Regex.Matches(scene, @"m_Name:\s*EventSystem\b");

            Assert.That(matches.Count, Is.EqualTo(1));
        }

        [Test]
        public void PrototypeMovementSettingsUseBoundedSprintMultiplier()
        {
            PlayerMovementSettings settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(PrototypeMovementSettingsPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SprintSpeedMultiplier, Is.InRange(1.5f, 2f));
            Assert.That(settings.SprintSpeed, Is.EqualTo(settings.WalkSpeed * settings.SprintSpeedMultiplier).Within(0.001f));
            Assert.That(settings.Acceleration, Is.GreaterThan(0f));
            Assert.That(settings.Deceleration, Is.GreaterThan(0f));
        }

        [Test]
        public void PrototypeInputBindsSprintOnlyToLeftShift()
        {
            string inputActions = File.ReadAllText(InputActionsPath);

            Assert.That(Regex.Matches(inputActions, "\"action\": \"Sprint\"").Count, Is.EqualTo(1));
            Assert.That(inputActions, Does.Contain("\"path\": \"<Keyboard>/leftShift\""));
            Assert.That(HasSprintBinding(inputActions, "<Keyboard>/rightShift"), Is.False);
            Assert.That(HasSprintBinding(inputActions, "<Gamepad>/leftStickPress"), Is.False);
            Assert.That(HasSprintBinding(inputActions, "<XRController>/trigger"), Is.False);
        }

        [Test]
        public void PrototypeVegetationPaintPrefabsHaveRenderableMeshesAndMaterials()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrototypeVegetationPrefabRoot });

            Assert.That(prefabGuids.Length, Is.GreaterThanOrEqualTo(12));

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(prefab, Is.Not.Null, path);

                MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
                MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

                Assert.That(meshFilters.Any(meshFilter => meshFilter != null && meshFilter.sharedMesh != null), Is.True, path);
                Assert.That(renderers.Length, Is.GreaterThan(0), path);

                foreach (MeshRenderer renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterials.Length, Is.GreaterThan(0), path);
                    Assert.That(renderer.sharedMaterials.All(material => material != null && material.shader != null), Is.True, path);
                }

                Bounds bounds = CalculateMeshRendererBounds(prefab);
                bool isTree = prefab.name.StartsWith("prototype-tree-", System.StringComparison.Ordinal);
                if (isTree)
                {
                    Assert.That(bounds.size.y, Is.InRange(6.0f, 9.9f), path);
                    Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.LessThanOrEqualTo(13.1f), path);
                }
                else
                {
                    Assert.That(bounds.size.y, Is.InRange(1.9f, 3.4f), path);
                    Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.LessThanOrEqualTo(3.9f), path);
                }
            }
        }

        [Test]
        public void PrototypeTreePaintPrefabsUseSimpleTrunkColliders()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrototypeVegetationPrefabRoot });

            Assert.That(prefabGuids.Length, Is.GreaterThanOrEqualTo(12));

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(prefab, Is.Not.Null, path);

                bool isTree = prefab.name.StartsWith("prototype-tree-", System.StringComparison.Ordinal);
                Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);

                if (isTree)
                {
                    CapsuleCollider trunk = colliders.OfType<CapsuleCollider>().SingleOrDefault(collider => collider != null && !collider.isTrigger);

                    Assert.That(trunk, Is.Not.Null, path);
                    Assert.That(trunk.direction, Is.EqualTo(1), path);
                    Assert.That(trunk.radius, Is.GreaterThan(0f), path);
                    Assert.That(trunk.radius, Is.LessThanOrEqualTo(0.12f), path);
                    Assert.That(trunk.height, Is.GreaterThan(trunk.radius), path);
                    Assert.That(trunk.center.y, Is.GreaterThan(0f), path);

                    Bounds bottomFootprint = CalculateBottomMeshFootprint(prefab);
                    Assert.That(trunk.center.x, Is.InRange(bottomFootprint.min.x, bottomFootprint.max.x), path);
                    Assert.That(trunk.center.z, Is.InRange(bottomFootprint.min.z, bottomFootprint.max.z), path);
                }
                else
                {
                    Assert.That(colliders.Any(collider => collider != null && !collider.isTrigger), Is.False, path);
                }
            }
        }

        [Test]
        public void PrototypeTerrainsUseGeneratedVegetationPaintPalette()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Terrain[] terrains = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .ToArray();

            Assert.That(terrains.Length, Is.GreaterThanOrEqualTo(1));

            foreach (Terrain terrain in terrains)
            {
                Assert.That(terrain.drawTreesAndFoliage, Is.True, terrain.name);
                Assert.That(terrain.materialTemplate, Is.Not.Null, $"{terrain.name} should use the project-owned prototype terrain material.");
                Assert.That(AssetDatabase.GetAssetPath(terrain.materialTemplate), Does.StartWith("Assets/_Project/Prototype/Environment/Terrain/Materials/"), terrain.name);
                Assert.That(terrain.materialTemplate.shader, Is.Not.Null, terrain.name);
                Assert.That(terrain.materialTemplate.shader.name, Does.Contain("Terrain"), terrain.name);
                Assert.That(terrain.materialTemplate.shader.name, Does.Not.Contain("Error"), terrain.name);
                Assert.That(terrain.materialTemplate.shader.isSupported, Is.True, terrain.name);
                Assert.That(terrain.terrainData, Is.Not.Null, terrain.name);
                Assert.That(terrain.terrainData.terrainLayers.Length, Is.GreaterThanOrEqualTo(4), terrain.name);
                Assert.That(terrain.terrainData.treePrototypes.Length, Is.GreaterThanOrEqualTo(12), terrain.name);

                foreach (TerrainLayer terrainLayer in terrain.terrainData.terrainLayers)
                {
                    Assert.That(terrainLayer, Is.Not.Null, terrain.name);
                    Assert.That(IsApprovedTerrainLayerPath(AssetDatabase.GetAssetPath(terrainLayer)), Is.True, $"{terrain.name}:{terrainLayer.name}");
                    Assert.That(terrainLayer.diffuseTexture, Is.Not.Null, $"{terrain.name}:{terrainLayer.name}");
                    Assert.That(terrainLayer.smoothness, Is.EqualTo(0f), $"{terrain.name}:{terrainLayer.name}");
                }

                foreach (TreePrototype treePrototype in terrain.terrainData.treePrototypes)
                {
                    Assert.That(treePrototype.prefab, Is.Not.Null, terrain.name);

                    string prefabPath = AssetDatabase.GetAssetPath(treePrototype.prefab);
                    Assert.That(prefabPath, Does.StartWith(PrototypeVegetationPrefabRoot), terrain.name);
                }
            }
        }

        [Test]
        public void PrototypeMedievalHouseIsPlacedAsReusablePrototypeBuilding()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeMedievalHousePrefabPath);
            Assert.That(prefab, Is.Not.Null, PrototypeMedievalHousePrefabPath);

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject house = FindScenePath(scene, "PrototypeScene/Environment/Landmarks/Buildings/Prototype Medieval House");

            Assert.That(house, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(house), Is.EqualTo(PrototypeMedievalHousePrefabPath));
            Assert.That(house.transform.position.y, Is.GreaterThanOrEqualTo(-0.1f));
            Assert.That(float.IsNaN(house.transform.position.y), Is.False);
            Bounds houseBounds = CalculateMeshRendererBounds(house);
            Assert.That(houseBounds.size.x, Is.InRange(8f, 13f));
            Assert.That(houseBounds.size.y, Is.InRange(7.5f, 10f));
            Assert.That(houseBounds.size.z, Is.InRange(7f, 12f));

            MeshRenderer[] renderers = house.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Any(renderer => renderer != null), Is.True);
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterials.Length, Is.GreaterThan(0), renderer.name);
                Assert.That(renderer.sharedMaterials.All(material => material != null && material.shader != null), Is.True, renderer.name);
            }

            Collider[] colliders = house.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders.Any(collider => collider != null && !collider.isTrigger), Is.True);

            Light[] lights = house.GetComponentsInChildren<Light>(true);
            foreach (Light light in lights)
            {
                if (light.type != LightType.Directional)
                {
                    Assert.That(light.shadows, Is.Not.EqualTo(LightShadows.None), light.name);
                    Assert.That(light.shadowResolution, Is.EqualTo(UnityEngine.Rendering.LightShadowResolution.Low), light.name);
                }
            }

            foreach (MonoBehaviour behaviour in house.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(behaviour);
                SerializedProperty tierProperty = serialized.FindProperty("m_AdditionalLightsShadowResolutionTier");
                if (tierProperty != null && tierProperty.propertyType == SerializedPropertyType.Integer)
                {
                    Assert.That(tierProperty.intValue, Is.EqualTo(0), behaviour.name);
                }
            }
        }

        private static bool IsApprovedTerrainLayerPath(string assetPath)
        {
            return assetPath.StartsWith(PrototypeTerrainRoot + "/Layers/", System.StringComparison.Ordinal) ||
                assetPath.StartsWith("Assets/ThirdParty/", System.StringComparison.Ordinal);
        }

        private static bool HasSprintBinding(string inputActions, string bindingPath)
        {
            string escapedPath = Regex.Escape($"\"path\": \"{bindingPath}\"");
            return Regex.IsMatch(
                inputActions,
                "\\{[^{}]*" + escapedPath + "[^{}]*\"action\": \"Sprint\"[^{}]*\\}",
                RegexOptions.Singleline);
        }

        private static void AssertSceneContains(string scene, string expectedName)
        {
            Assert.That(scene, Does.Contain($"m_Name: {expectedName}"), expectedName);
        }

        private static void AssertSceneDoesNotContain(string scene, string removedName)
        {
            Assert.That(scene, Does.Not.Contain($"m_Name: {removedName}"), removedName);
        }

        private static GameObject FindScenePath(UnityEngine.SceneManagement.Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!string.Equals(root.name, parts[0], System.StringComparison.Ordinal))
                {
                    continue;
                }

                Transform current = root.transform;
                for (int i = 1; i < parts.Length; i++)
                {
                    current = current.Find(parts[i]);
                    if (current == null)
                    {
                        return null;
                    }
                }

                return current.gameObject;
            }

            return null;
        }

        private static Bounds CalculateBottomMeshFootprint(GameObject root)
        {
            List<Vector3> vertices = new List<Vector3>();
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
                {
                    vertices.Add(root.transform.InverseTransformPoint(meshFilter.transform.TransformPoint(vertex)));
                }
            }

            Assert.That(vertices.Count, Is.GreaterThan(0), root.name);

            Bounds visualBounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                visualBounds.Encapsulate(vertices[i]);
            }

            float maxY = visualBounds.min.y + visualBounds.size.y * 0.1f;
            Bounds footprint = new Bounds(new Vector3(visualBounds.center.x, 0f, visualBounds.center.z), Vector3.zero);
            bool hasFootprint = false;

            foreach (Vector3 vertex in vertices)
            {
                if (vertex.y > maxY)
                {
                    continue;
                }

                Vector3 footprintPoint = new Vector3(vertex.x, 0f, vertex.z);
                if (!hasFootprint)
                {
                    footprint = new Bounds(footprintPoint, Vector3.zero);
                    hasFootprint = true;
                }
                else
                {
                    footprint.Encapsulate(footprintPoint);
                }
            }

            Assert.That(hasFootprint, Is.True, root.name);
            return footprint;
        }

        private static Bounds CalculateMeshRendererBounds(GameObject root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer != null)
                .ToArray();

            Assert.That(renderers.Length, Is.GreaterThan(0), root.name);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
