using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityIsekaiGame.Editor
{
    public static class PrototypeTerrainTreePaletteTool
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string TerrainDataRoot = "Assets/_Project/Prototype/Environment/Terrain/Data";
        private const string GeneratedVegetationRoot = "Assets/_Project/Prototype/Environment/Vegetation";
        private const string GeneratedMaterialRoot = GeneratedVegetationRoot + "/Materials";
        private const string GeneratedPrefabRoot = GeneratedVegetationRoot + "/Prefabs";
        private const float DefaultBendFactor = 0.35f;
        private const int TreeScaleRandomSalt = 0x278D13;
        private const float TrunkFootprintSlice = 0.1f;
        private const float MaximumTrunkFootprintSlice = 0.35f;
        private const int MinimumTrunkFootprintVertexCount = 8;

        private enum TerrainTargetScope
        {
            AllPrototypeTerrains,
            SelectedTerrains
        }

        private static readonly PrototypeTreePaletteEntry[] TreePalette =
        {
            new("Broadleaf Tree", "prototype-tree-broadleaf", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/trees/bigLeavesTreeL.prefab", PrototypeTreePaletteStyle.GreenTree, 0.25f, 7.3f, 0.85f, 1.25f),
            new("Simple Tree", "prototype-tree-simple", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/trees/simpleTreeL.prefab", PrototypeTreePaletteStyle.GreenTree, 0.3f, 7.6f, 0.85f, 1.25f),
            new("Classic Tree", "prototype-tree-classic", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/trees/tree1L.prefab", PrototypeTreePaletteStyle.GreenTree, 0.35f, 8.0f, 0.85f, 1.25f),
            new("Fallen Branch Tree", "prototype-tree-fallen-branch", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/trees/fallenBranchesTreeL.prefab", PrototypeTreePaletteStyle.GreenTree, 0.3f, 7.1f, 0.85f, 1.2f),
            new("Purple Accent Tree", "prototype-tree-purple-accent", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/trees/purpleTreeL.prefab", PrototypeTreePaletteStyle.PurpleTree, 0.2f, 6.8f, 0.85f, 1.18f),
            new("Dry Broadleaf Tree", "prototype-tree-dry-broadleaf", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/dryTree/dryBigLeavesTreeL.prefab", PrototypeTreePaletteStyle.DryTree, 0.2f, 6.6f, 0.8f, 1.18f),
            new("Dry Simple Tree", "prototype-tree-dry-simple", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/dryTree/drySimpleTreeL.prefab", PrototypeTreePaletteStyle.DryTree, 0.25f, 7.0f, 0.8f, 1.2f),
            new("Dry Tree", "prototype-tree-dry", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/dryTree/dryTreeL.prefab", PrototypeTreePaletteStyle.DryTree, 0.25f, 7.2f, 0.8f, 1.2f),
            new("Dead Tree", "prototype-tree-dead", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/dryBranchL/deadTreeL.prefab", PrototypeTreePaletteStyle.DeadTree, 0.15f, 6.3f, 0.8f, 1.12f),
            new("Big Leaf Shrub", "prototype-shrub-big-leaf", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/shurbs/bigLeavesShrubsL.prefab", PrototypeTreePaletteStyle.GreenShrub, 0.45f, 2.75f, 0.8f, 1.2f),
            new("Small Leaf Shrub", "prototype-shrub-small-leaf", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/shurbs/littleLeavesShrubL.prefab", PrototypeTreePaletteStyle.GreenShrub, 0.45f, 2.4f, 0.8f, 1.18f),
            new("Shrub", "prototype-shrub", "Assets/ThirdParty/polygonTrees/polygonTrees/prefabs/prefabLODs/shurbs/shrubL.prefab", PrototypeTreePaletteStyle.GreenShrub, 0.4f, 2.55f, 0.8f, 1.2f)
        };

        [MenuItem("Tools/Prototype Scene/Configure Prototype Terrain Tree Palette")]
        public static void ConfigurePrototypeTerrainTreePalette()
        {
            EnsurePrototypeTreeAssets();

            var scene = OpenPrototypeSceneIfNeeded(TerrainTargetScope.AllPrototypeTerrains);
            EnsurePrototypeTerrainDataAssetsAreProjectOwned();
            var terrains = FindPrototypeTerrains();

            var prototypes = LoadTreePrototypes();
            foreach (var terrain in terrains)
            {
                terrain.terrainData.treePrototypes = prototypes;
                terrain.treeDistance = 650f;
                terrain.treeBillboardDistance = 85f;
                terrain.treeCrossFadeLength = 12f;
                terrain.treeMaximumFullLODCount = 120;
                terrain.drawTreesAndFoliage = true;

                UnityEditor.EditorUtility.SetDirty(terrain.terrainData);
                UnityEditor.EditorUtility.SetDirty(terrain);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"Configured {prototypes.Length} prototype terrain tree/shrub paint entries across {terrains.Length} terrain tile(s): {string.Join(", ", TreePalette.Select(entry => entry.Name))}.");
        }

        [MenuItem("Tools/Prototype Scene/Rebuild Prototype Vegetation Prefabs")]
        public static void RebuildPrototypeVegetationPrefabs()
        {
            EnsurePrototypeTreeAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt {TreePalette.Length} project-owned prototype vegetation prefab(s) in {GeneratedPrefabRoot}.");
        }

        [MenuItem("Tools/Prototype Scene/Randomize Painted Prototype Tree Heights/All Prototype Terrains")]
        public static void RandomizeAllPaintedPrototypeTreeHeights()
        {
            RandomizePaintedPrototypeTreeHeights(TerrainTargetScope.AllPrototypeTerrains);
        }

        [MenuItem("Tools/Prototype Scene/Randomize Painted Prototype Tree Heights/Selected Terrains")]
        public static void RandomizeSelectedPaintedPrototypeTreeHeights()
        {
            RandomizePaintedPrototypeTreeHeights(TerrainTargetScope.SelectedTerrains);
        }

        private static void RandomizePaintedPrototypeTreeHeights(TerrainTargetScope scope)
        {
            var scene = OpenPrototypeSceneIfNeeded(scope);
            var terrains = FindPrototypeTerrains(scope);
            var changedInstances = 0;
            var minimumHeight = float.PositiveInfinity;
            var maximumHeight = float.NegativeInfinity;
            var minimumWidth = float.PositiveInfinity;
            var maximumWidth = float.NegativeInfinity;

            foreach (var terrain in terrains)
            {
                var instances = terrain.terrainData.treeInstances;
                for (var i = 0; i < instances.Length; i++)
                {
                    var instance = instances[i];
                    if (instance.prototypeIndex < 0 || instance.prototypeIndex >= TreePalette.Length)
                    {
                        continue;
                    }

                    var entry = TreePalette[instance.prototypeIndex];
                    var height = Mathf.Lerp(entry.MinimumPaintedHeightScale, entry.MaximumPaintedHeightScale, Deterministic01(instance, TreeScaleRandomSalt));
                    var width = height * Mathf.Lerp(0.82f, 1.16f, Deterministic01(instance, TreeScaleRandomSalt + 97));

                    instance.heightScale = height;
                    instance.widthScale = width;
                    instances[i] = instance;
                    changedInstances++;
                    minimumHeight = Mathf.Min(minimumHeight, height);
                    maximumHeight = Mathf.Max(maximumHeight, height);
                    minimumWidth = Mathf.Min(minimumWidth, width);
                    maximumWidth = Mathf.Max(maximumWidth, width);
                }

                terrain.terrainData.treeInstances = instances;
                EditorUtility.SetDirty(terrain.terrainData);
                EditorUtility.SetDirty(terrain);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (changedInstances == 0)
            {
                Debug.Log($"No prototype terrain tree instances were found to randomize across {terrains.Length} terrain tile(s).");
                return;
            }

            Debug.Log($"Randomized prototype terrain tree scale for {changedInstances} painted tree instance(s) across {terrains.Length} terrain tile(s). HeightScale={minimumHeight:0.00}-{maximumHeight:0.00}, WidthScale={minimumWidth:0.00}-{maximumWidth:0.00}.");
        }

        private static void EnsurePrototypeTreeAssets()
        {
            EnsureFolder("Assets/_Project/Prototype");
            EnsureFolder("Assets/_Project/Prototype/Environment");
            EnsureFolder(GeneratedVegetationRoot);
            EnsureFolder(GeneratedMaterialRoot);
            EnsureFolder(GeneratedPrefabRoot);

            var materials = PrototypeTreeMaterials.LoadOrCreate(GeneratedMaterialRoot);
            foreach (var entry in TreePalette)
            {
                CreateOrUpdateGeneratedPrefab(entry, materials);
            }
        }

        private static void EnsurePrototypeTerrainDataAssetsAreProjectOwned()
        {
            EnsureFolder("Assets/_Project/Prototype");
            EnsureFolder("Assets/_Project/Prototype/Environment");
            EnsureFolder("Assets/_Project/Prototype/Environment/Terrain");
            EnsureFolder(TerrainDataRoot);

            foreach (var terrain in FindPrototypeTerrains(TerrainTargetScope.AllPrototypeTerrains))
            {
                var data = terrain == null ? null : terrain.terrainData;
                if (data == null)
                {
                    continue;
                }

                var currentPath = AssetDatabase.GetAssetPath(data);
                if (string.IsNullOrWhiteSpace(currentPath))
                {
                    continue;
                }

                currentPath = currentPath.Replace('\\', '/');
                if (currentPath.StartsWith(TerrainDataRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetPath = AssetDatabase.GenerateUniqueAssetPath($"{TerrainDataRoot}/{SanitizeAssetName(terrain.name)}.asset");
                var moveFailure = AssetDatabase.MoveAsset(currentPath, targetPath);
                if (!string.IsNullOrWhiteSpace(moveFailure))
                {
                    throw new InvalidOperationException($"Failed to move prototype TerrainData '{currentPath}' to '{targetPath}': {moveFailure}");
                }

                var movedData = AssetDatabase.LoadAssetAtPath<TerrainData>(targetPath);
                if (movedData == null)
                {
                    throw new InvalidOperationException($"Moved prototype TerrainData could not be loaded: {targetPath}");
                }

                terrain.terrainData = movedData;
                var collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null)
                {
                    collider.terrainData = movedData;
                    EditorUtility.SetDirty(collider);
                }

                EditorUtility.SetDirty(terrain);
                EditorUtility.SetDirty(movedData);
            }
        }

        private static void CreateOrUpdateGeneratedPrefab(PrototypeTreePaletteEntry entry, PrototypeTreeMaterials materials)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.SourcePrefabPath);
            if (source == null)
            {
                throw new InvalidOperationException($"Missing source vegetation prefab for {entry.Name}: {entry.SourcePrefabPath}");
            }

            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(source);
            }

            try
            {
                instance.name = entry.Name;
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.transform.localScale = Vector3.one;
                AssignProjectMaterials(instance, entry.Style, materials);
                RemoveImportedRuntimeColliders(instance);
                instance.transform.localScale = Vector3.one * CalculateRootScaleForTargetHeight(instance, entry);
                AddGeneratedTrunkCollider(instance, entry);
                ValidateGeneratedPrefab(instance, entry);

                PrefabUtility.SaveAsPrefabAsset(instance, entry.GeneratedPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssignProjectMaterials(GameObject instance, PrototypeTreePaletteStyle style, PrototypeTreeMaterials materials)
        {
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                var sourceMaterials = renderer.sharedMaterials;
                var replacementMaterials = new Material[sourceMaterials.Length];
                for (var i = 0; i < sourceMaterials.Length; i++)
                {
                    replacementMaterials[i] = materials.Resolve(style, sourceMaterials[i], i, sourceMaterials.Length);
                }

                renderer.sharedMaterials = replacementMaterials;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void RemoveImportedRuntimeColliders(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static float CalculateRootScaleForTargetHeight(GameObject instance, PrototypeTreePaletteEntry entry)
        {
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer != null)
                .ToArray();

            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"Cannot scale '{entry.Name}' because it has no renderable mesh.");
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y <= 0.001f)
            {
                throw new InvalidOperationException($"Cannot scale '{entry.Name}' because its visual height is zero.");
            }

            return Mathf.Clamp(entry.TargetVisualHeight / bounds.size.y, 0.01f, 100f);
        }


        private static void AddGeneratedTrunkCollider(GameObject instance, PrototypeTreePaletteEntry entry)
        {
            if (!entry.HasTrunkCollider)
            {
                return;
            }

            var localVertices = CollectLocalMeshVertices(instance);
            if (localVertices.Count == 0)
            {
                throw new InvalidOperationException($"Cannot generate trunk collider for '{entry.Name}' because it has no mesh vertices.");
            }

            var localBounds = CalculateBounds(localVertices);
            var trunkFootprint = CalculateBottomFootprintBounds(localVertices, localBounds, TrunkFootprintSlice);

            var collider = instance.AddComponent<CapsuleCollider>();
            collider.isTrigger = false;
            collider.direction = 1;
            collider.center = new Vector3(trunkFootprint.center.x, localBounds.min.y + localBounds.size.y * entry.TrunkColliderCenterHeight, trunkFootprint.center.z);
            collider.height = Mathf.Max(localBounds.size.y * entry.TrunkColliderHeight, 0.08f);
            collider.radius = Mathf.Clamp(Mathf.Min(trunkFootprint.size.x, trunkFootprint.size.z) * entry.TrunkColliderRadiusScale, 0.035f, entry.MaximumTrunkColliderRadius);
        }

        private static List<Vector3> CollectLocalMeshVertices(GameObject instance)
        {
            var vertices = new List<Vector3>();
            var meshFilters = instance.GetComponentsInChildren<MeshFilter>(true);

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                foreach (var vertex in meshFilter.sharedMesh.vertices)
                {
                    vertices.Add(instance.transform.InverseTransformPoint(meshFilter.transform.TransformPoint(vertex)));
                }
            }

            return vertices;
        }

        private static Bounds CalculateBounds(IReadOnlyList<Vector3> vertices)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = new Bounds(vertices[0], Vector3.zero);
            for (var i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }

            return bounds;
        }

        private static Bounds CalculateBottomFootprintBounds(IReadOnlyList<Vector3> vertices, Bounds localBounds, float startingSlice)
        {
            var slice = Mathf.Clamp01(startingSlice);
            while (slice <= MaximumTrunkFootprintSlice)
            {
                var footprint = CollectBottomFootprint(vertices, localBounds, slice);
                if (footprint.VertexCount >= MinimumTrunkFootprintVertexCount && footprint.Bounds.size.x > 0f && footprint.Bounds.size.z > 0f)
                {
                    return footprint.Bounds;
                }

                slice += 0.05f;
            }

            return CollectBottomFootprint(vertices, localBounds, MaximumTrunkFootprintSlice).Bounds;
        }

        private static TrunkFootprint CollectBottomFootprint(IReadOnlyList<Vector3> vertices, Bounds localBounds, float slice)
        {
            var maxY = localBounds.min.y + localBounds.size.y * Mathf.Clamp01(slice);
            var hasBounds = false;
            var bounds = new Bounds();
            var count = 0;

            foreach (var vertex in vertices)
            {
                if (vertex.y > maxY)
                {
                    continue;
                }

                var footprintPoint = new Vector3(vertex.x, 0f, vertex.z);
                if (!hasBounds)
                {
                    bounds = new Bounds(footprintPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(footprintPoint);
                }

                count++;
            }

            if (!hasBounds)
            {
                bounds = new Bounds(new Vector3(localBounds.center.x, 0f, localBounds.center.z), new Vector3(0.07f, 0f, 0.07f));
            }

            return new TrunkFootprint(bounds, count);
        }

        private static void ValidateGeneratedPrefab(GameObject instance, PrototypeTreePaletteEntry entry)
        {
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"Generated vegetation prefab '{entry.Name}' has no MeshRenderer components.");
            }

            var meshFilters = instance.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length == 0 || meshFilters.All(filter => filter == null || filter.sharedMesh == null))
            {
                throw new InvalidOperationException($"Generated vegetation prefab '{entry.Name}' has no assigned mesh.");
            }

            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null || material.shader == null))
                {
                    throw new InvalidOperationException($"Generated vegetation prefab '{entry.Name}' has an invalid material assignment.");
                }
            }

            var colliders = instance.GetComponentsInChildren<Collider>(true);
            if (entry.HasTrunkCollider)
            {
                if (colliders.OfType<CapsuleCollider>().All(collider => collider == null || collider.isTrigger || collider.radius <= 0f || collider.height <= collider.radius))
                {
                    throw new InvalidOperationException($"Generated tree prefab '{entry.Name}' has no valid trunk CapsuleCollider.");
                }
            }
            else if (colliders.Any(collider => collider != null && !collider.isTrigger))
            {
                throw new InvalidOperationException($"Generated non-tree vegetation prefab '{entry.Name}' should not have blocking colliders.");
            }
        }

        private static TreePrototype[] LoadTreePrototypes()
        {
            var prototypes = new List<TreePrototype>(TreePalette.Length);
            var missing = new List<string>();

            foreach (var entry in TreePalette)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.GeneratedPrefabPath);
                if (prefab == null)
                {
                    missing.Add($"{entry.Name} ({entry.GeneratedPrefabPath})");
                    continue;
                }

                prototypes.Add(new TreePrototype
                {
                    prefab = prefab,
                    bendFactor = entry.BendFactor <= 0f ? DefaultBendFactor : entry.BendFactor
                });
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException("Missing prototype tree palette prefab(s): " + string.Join("; ", missing));
            }

            return prototypes.ToArray();
        }

        private static Scene OpenPrototypeSceneIfNeeded(TerrainTargetScope scope = TerrainTargetScope.AllPrototypeTerrains)
        {
            var scene = SceneManager.GetActiveScene();
            if (scope == TerrainTargetScope.SelectedTerrains)
            {
                if (!string.Equals(scene.path, PrototypeScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Selected terrain operations must be run while PrototypeScene is open.");
                }

                return scene;
            }

            if (!string.Equals(scene.path, PrototypeScenePath, StringComparison.OrdinalIgnoreCase))
            {
                scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            }

            return scene;
        }

        private static Terrain[] FindPrototypeTerrains(TerrainTargetScope scope = TerrainTargetScope.AllPrototypeTerrains)
        {
            return scope == TerrainTargetScope.SelectedTerrains
                ? FindSelectedPrototypeTerrains()
                : FindAllPrototypeTerrains();
        }

        private static Terrain[] FindAllPrototypeTerrains()
        {
            var ground = FindScenePath("PrototypeScene/Environment/Ground");
            if (ground == null)
            {
                throw new InvalidOperationException("PrototypeScene/Environment/Ground was not found.");
            }

            var terrains = ground.GetComponentsInChildren<Terrain>(true)
                .Where(terrain => terrain != null && terrain.terrainData != null)
                .OrderBy(terrain => terrain.transform.position.z)
                .ThenBy(terrain => terrain.transform.position.x)
                .ToArray();

            if (terrains.Length == 0)
            {
                throw new InvalidOperationException("No Terrain components were found under PrototypeScene/Environment/Ground.");
            }

            return terrains;
        }

        private static Terrain[] FindSelectedPrototypeTerrains()
        {
            var terrains = new List<Terrain>();

            foreach (var selected in Selection.gameObjects)
            {
                if (selected == null || !selected.scene.IsValid())
                {
                    continue;
                }

                foreach (var terrain in selected.GetComponentsInChildren<Terrain>(true))
                {
                    if (terrain == null || terrain.terrainData == null)
                    {
                        continue;
                    }

                    if (!terrains.Any(existing => existing == terrain))
                    {
                        terrains.Add(terrain);
                    }
                }
            }

            var ordered = terrains
                .OrderBy(terrain => terrain.transform.position.z)
                .ThenBy(terrain => terrain.transform.position.x)
                .ToArray();

            if (ordered.Length == 0)
            {
                throw new InvalidOperationException("Select one or more Terrain GameObjects, or a parent containing Terrain children, before running this selected terrain command.");
            }

            return ordered;
        }

        private static float Deterministic01(TreeInstance instance, int salt)
        {
            unchecked
            {
                var hash = 2166136261u;
                Mix(ref hash, instance.prototypeIndex);
                Mix(ref hash, Mathf.RoundToInt(instance.position.x * 100000f));
                Mix(ref hash, Mathf.RoundToInt(instance.position.y * 100000f));
                Mix(ref hash, Mathf.RoundToInt(instance.position.z * 100000f));
                Mix(ref hash, salt);
                return (hash & 0x00FFFFFF) / 16777216f;
            }
        }

        private static void Mix(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619u;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = System.IO.Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                throw new InvalidOperationException($"Cannot create invalid asset folder path '{path}'.");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "PrototypeTerrainData";
            }

            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '-' || character == '_' ? character : '_');
            }

            var sanitized = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? "PrototypeTerrainData" : sanitized;
        }

        private static GameObject FindScenePath(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0)
            {
                return null;
            }

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != parts[0])
                {
                    continue;
                }

                var current = root.transform;
                for (var i = 1; i < parts.Length; i++)
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

        private readonly struct PrototypeTreePaletteEntry
        {
            public PrototypeTreePaletteEntry(
                string name,
                string assetName,
                string sourcePrefabPath,
                PrototypeTreePaletteStyle style,
                float bendFactor,
                float targetVisualHeight,
                float minimumPaintedHeightScale,
                float maximumPaintedHeightScale)
            {
                Name = name;
                AssetName = assetName;
                SourcePrefabPath = sourcePrefabPath;
                Style = style;
                BendFactor = bendFactor;
                TargetVisualHeight = targetVisualHeight;
                MinimumPaintedHeightScale = minimumPaintedHeightScale;
                MaximumPaintedHeightScale = maximumPaintedHeightScale;
            }

            public string Name { get; }

            public string AssetName { get; }

            public string SourcePrefabPath { get; }

            public PrototypeTreePaletteStyle Style { get; }

            public float BendFactor { get; }

            public float TargetVisualHeight { get; }

            public float MinimumPaintedHeightScale { get; }

            public float MaximumPaintedHeightScale { get; }

            public bool HasTrunkCollider => Style != PrototypeTreePaletteStyle.GreenShrub;

            public float TrunkColliderCenterHeight
            {
                get
                {
                    return Style == PrototypeTreePaletteStyle.DeadTree ? 0.38f : 0.34f;
                }
            }

            public float TrunkColliderHeight
            {
                get
                {
                    return Style == PrototypeTreePaletteStyle.DeadTree ? 0.76f : 0.68f;
                }
            }

            public float TrunkColliderRadiusScale
            {
                get
                {
                    return Style == PrototypeTreePaletteStyle.DeadTree ? 0.45f : 0.5f;
                }
            }

            public float MaximumTrunkColliderRadius
            {
                get
                {
                    return Style == PrototypeTreePaletteStyle.DeadTree ? 0.11f : 0.12f;
                }
            }

            public string GeneratedPrefabPath => $"{GeneratedPrefabRoot}/{AssetName}.prefab";
        }

        private readonly struct TrunkFootprint
        {
            public TrunkFootprint(Bounds bounds, int vertexCount)
            {
                Bounds = bounds;
                VertexCount = vertexCount;
            }

            public Bounds Bounds { get; }

            public int VertexCount { get; }
        }

        private enum PrototypeTreePaletteStyle
        {
            GreenTree,
            PurpleTree,
            DryTree,
            DeadTree,
            GreenShrub
        }

        private sealed class PrototypeTreeMaterials
        {
            private PrototypeTreeMaterials(Material bark, Material leaves, Material purpleLeaves, Material dryBark, Material dryLeaves, Material deadWood, Material shrubStem, Material shrubLeaves)
            {
                Bark = bark;
                Leaves = leaves;
                PurpleLeaves = purpleLeaves;
                DryBark = dryBark;
                DryLeaves = dryLeaves;
                DeadWood = deadWood;
                ShrubStem = shrubStem;
                ShrubLeaves = shrubLeaves;
            }

            private Material Bark { get; }

            private Material Leaves { get; }

            private Material PurpleLeaves { get; }

            private Material DryBark { get; }

            private Material DryLeaves { get; }

            private Material DeadWood { get; }

            private Material ShrubStem { get; }

            private Material ShrubLeaves { get; }

            public static PrototypeTreeMaterials LoadOrCreate(string root)
            {
                return new PrototypeTreeMaterials(
                    LoadOrCreateMaterial(root, "Prototype Tree Bark", new Color(0.34f, 0.22f, 0.12f, 1f), 0.22f),
                    LoadOrCreateMaterial(root, "Prototype Tree Leaves", new Color(0.22f, 0.50f, 0.18f, 1f), 0.18f),
                    LoadOrCreateMaterial(root, "Prototype Purple Leaves", new Color(0.43f, 0.26f, 0.52f, 1f), 0.2f),
                    LoadOrCreateMaterial(root, "Prototype Dry Bark", new Color(0.42f, 0.31f, 0.19f, 1f), 0.25f),
                    LoadOrCreateMaterial(root, "Prototype Dry Leaves", new Color(0.56f, 0.45f, 0.25f, 1f), 0.18f),
                    LoadOrCreateMaterial(root, "Prototype Dead Wood", new Color(0.36f, 0.32f, 0.27f, 1f), 0.3f),
                    LoadOrCreateMaterial(root, "Prototype Shrub Stem", new Color(0.31f, 0.25f, 0.14f, 1f), 0.24f),
                    LoadOrCreateMaterial(root, "Prototype Shrub Leaves", new Color(0.18f, 0.44f, 0.14f, 1f), 0.18f));
            }

            public Material Resolve(PrototypeTreePaletteStyle style, Material source, int materialIndex, int materialCount)
            {
                var sourceName = source == null ? string.Empty : source.name.ToLowerInvariant();
                var looksLikeLeaves = sourceName.Contains("leave") || sourceName.Contains("grass") || (materialCount > 1 && materialIndex > 0);

                switch (style)
                {
                    case PrototypeTreePaletteStyle.PurpleTree:
                        return looksLikeLeaves ? PurpleLeaves : Bark;
                    case PrototypeTreePaletteStyle.DryTree:
                        return looksLikeLeaves ? DryLeaves : DryBark;
                    case PrototypeTreePaletteStyle.DeadTree:
                        return DeadWood;
                    case PrototypeTreePaletteStyle.GreenShrub:
                        return looksLikeLeaves ? ShrubLeaves : ShrubStem;
                    default:
                        return looksLikeLeaves ? Leaves : Bark;
                }
            }

            private static Material LoadOrCreateMaterial(string root, string name, Color color, float smoothness)
            {
                var path = $"{root}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(ResolveLitShader())
                    {
                        name = name
                    };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = ResolveLitShader();
                SetMaterialColor(material, color);
                SetMaterialFloat(material, "_Smoothness", smoothness);
                SetMaterialFloat(material, "_Metallic", 0f);
                EditorUtility.SetDirty(material);
                return material;
            }

            private static Shader ResolveLitShader()
            {
                return Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Diffuse");
            }

            private static void SetMaterialColor(Material material, Color color)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
            }

            private static void SetMaterialFloat(Material material, string property, float value)
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, value);
                }
            }
        }
    }
}
