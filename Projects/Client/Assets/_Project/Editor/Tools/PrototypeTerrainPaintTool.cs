using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityIsekaiGame.Editor
{
    public static class PrototypeTerrainPaintTool
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        private const string LayerRoot = "Assets/ThirdParty/Handpainted_Grass_and_Ground_Textures/Demo/terrain_layers";
        private const string TerrainRoot = "Assets/_Project/Prototype/Environment/Terrain";
        private const string TerrainLayerFolder = TerrainRoot + "/Layers";
        private const string TerrainMaterialFolder = "Assets/_Project/Prototype/Environment/Terrain/Materials";
        private const string TerrainMaterialPath = TerrainMaterialFolder + "/Prototype Terrain URP.mat";
        private const string UrpTerrainTemplatePath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat";

        private enum TerrainTargetScope
        {
            AllPrototypeTerrains,
            SelectedTerrains
        }

        [MenuItem("Tools/Prototype Scene/Paint Prototype Terrain Ground/All Prototype Terrains")]
        public static void PaintPrototypeTerrainGround()
        {
            PaintPrototypeTerrainGround(TerrainTargetScope.AllPrototypeTerrains);
        }

        [MenuItem("Tools/Prototype Scene/Paint Prototype Terrain Ground/Selected Terrains")]
        public static void PaintSelectedPrototypeTerrainGround()
        {
            PaintPrototypeTerrainGround(TerrainTargetScope.SelectedTerrains);
        }

        [MenuItem("Tools/Prototype Scene/Assign Prototype Terrain Layers/All Prototype Terrains")]
        public static void AssignPrototypeTerrainLayers()
        {
            AssignPrototypeTerrainLayers(TerrainTargetScope.AllPrototypeTerrains);
        }

        [MenuItem("Tools/Prototype Scene/Assign Prototype Terrain Layers/Selected Terrains")]
        public static void AssignSelectedPrototypeTerrainLayers()
        {
            AssignPrototypeTerrainLayers(TerrainTargetScope.SelectedTerrains);
        }

        private static void AssignPrototypeTerrainLayers(TerrainTargetScope scope)
        {
            var scene = EnsurePrototypeSceneLoaded(scope);
            var terrains = FindTargetTerrains(scope);
            var layers = LoadGroundLayers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var terrainMaterial = LoadOrCreateTerrainMaterial();

            foreach (var terrain in terrains)
            {
                UseTerrainMaterial(terrain, terrainMaterial);
                AssignTerrainLayers(terrain, layers);
                EditorUtility.SetDirty(terrain);
                EditorUtility.SetDirty(terrain.terrainData);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"Assigned {layers.Length} prototype terrain layer(s) to {terrains.Length} terrain tile(s): {string.Join(", ", layers.Select(layer => layer.name))}.");
        }

        private static void PaintPrototypeTerrainGround(TerrainTargetScope scope)
        {
            var scene = EnsurePrototypeSceneLoaded(scope);
            var terrains = FindTargetTerrains(scope);
            var layers = LoadGroundLayers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var terrainMaterial = LoadOrCreateTerrainMaterial();
            var globalHeightRange = FindHeightRange(terrains);
            Debug.Log($"Prototype terrain global height range: {globalHeightRange.x:0.00} to {globalHeightRange.y:0.00}.");

            var summaries = new List<string>();
            foreach (var terrain in terrains)
            {
                UseTerrainMaterial(terrain, terrainMaterial);
                summaries.Add(PaintTerrain(terrain, layers, globalHeightRange));
                UnityEditor.EditorUtility.SetDirty(terrain.terrainData);
                UnityEditor.EditorUtility.SetDirty(terrain);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"Painted {terrains.Length} prototype terrain tile(s) with height-aware grass, dirt, and rock layers.");
            Debug.Log("Prototype terrain layer averages: " + string.Join(" | ", summaries));
        }

        private static Scene EnsurePrototypeSceneLoaded(TerrainTargetScope scope)
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

        private static Terrain[] FindTargetTerrains(TerrainTargetScope scope)
        {
            return scope == TerrainTargetScope.SelectedTerrains
                ? FindSelectedTerrains()
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

        private static Terrain[] FindSelectedTerrains()
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

        private static TerrainLayer[] LoadGroundLayers()
        {
            return new[]
            {
                LoadLayer("Grass_normal_up.terrainlayer", "Grass"),
                LoadLayer("dirt_normal_up.terrainlayer", "Dirt"),
                LoadLayer("dirt_lighted_up.terrainlayer", "Packed Dirt"),
                LoadLayer("dirt_desatured_rocks_up.terrainlayer", "Rock")
            };
        }

        private static TerrainLayer LoadLayer(string fileName, string label)
        {
            var path = $"{LayerRoot}/{fileName}";
            var sourceLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (sourceLayer != null)
            {
                return LoadOrCreateProjectLayer(sourceLayer, label);
            }

            var fallbackGuid = AssetDatabase.FindAssets($"{label} t:TerrainLayer", new[] { LayerRoot })
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fallbackGuid))
            {
                sourceLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(fallbackGuid));
                if (sourceLayer != null)
                {
                    return LoadOrCreateProjectLayer(sourceLayer, label);
                }
            }

            throw new InvalidOperationException($"Could not find a {label} TerrainLayer under {LayerRoot}.");
        }

        private static TerrainLayer LoadOrCreateProjectLayer(TerrainLayer sourceLayer, string label)
        {
            EnsureFolder("Assets/_Project/Prototype");
            EnsureFolder("Assets/_Project/Prototype/Environment");
            EnsureFolder(TerrainRoot);
            EnsureFolder(TerrainLayerFolder);

            var targetPath = $"{TerrainLayerFolder}/Prototype {label}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(targetPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, targetPath);
            }

            EditorUtility.CopySerialized(sourceLayer, layer);
            layer.name = $"Prototype {label}";
            PrepareLayer(layer, label);
            UnityEditor.EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(targetPath);
            if (layer == null)
            {
                throw new InvalidOperationException($"Could not load project-owned TerrainLayer '{targetPath}' after creation.");
            }

            PrepareLayer(layer, label);
            return layer;
        }

        private static void PrepareLayer(TerrainLayer layer, string label)
        {
            if (layer.diffuseTexture == null)
            {
                throw new InvalidOperationException($"{label} TerrainLayer '{AssetDatabase.GetAssetPath(layer)}' has no diffuse texture.");
            }

            layer.smoothness = 0f;
            layer.metallic = 0f;
        }

        private static Material LoadOrCreateTerrainMaterial()
        {
            EnsureFolder("Assets/_Project/Prototype");
            EnsureFolder("Assets/_Project/Prototype/Environment");
            EnsureFolder(TerrainRoot);
            EnsureFolder(TerrainMaterialFolder);

            var material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (material == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(UrpTerrainTemplatePath);
                if (template != null && AssetDatabase.CopyAsset(UrpTerrainTemplatePath, TerrainMaterialPath))
                {
                    material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
                }

                if (material == null)
                {
                    material = new Material(FindTerrainShader());
                    AssetDatabase.CreateAsset(material, TerrainMaterialPath);
                }
            }

            var terrainShader = FindTerrainShader();
            var sourceTemplate = AssetDatabase.LoadAssetAtPath<Material>(UrpTerrainTemplatePath);
            if (sourceTemplate != null)
            {
                EditorUtility.CopySerialized(sourceTemplate, material);
            }
            else if (material.shader == null || !string.Equals(material.shader.name, terrainShader.name, StringComparison.Ordinal))
            {
                material.shader = terrainShader;
            }

            material.name = "Prototype Terrain URP";
            material.shader = terrainShader;
            SetFloatIfPresent(material, "_Smoothness", 0f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness0", 0f);
            SetFloatIfPresent(material, "_Smoothness1", 0f);
            SetFloatIfPresent(material, "_Smoothness2", 0f);
            SetFloatIfPresent(material, "_Smoothness3", 0f);
            SetFloatIfPresent(material, "_Metallic0", 0f);
            SetFloatIfPresent(material, "_Metallic1", 0f);
            SetFloatIfPresent(material, "_Metallic2", 0f);
            SetFloatIfPresent(material, "_Metallic3", 0f);
            SetFloatIfPresent(material, "_EnableInstancedPerPixelNormal", 1f);
            material.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static Shader FindTerrainShader()
        {
            string[] shaderNames =
            {
                "Universal Render Pipeline/Terrain/Lit",
                "Nature/Terrain/Standard",
                "Hidden/TerrainEngine/Splatmap/Standard-Base"
            };

            foreach (string shaderName in shaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    return shader;
                }
            }

            throw new InvalidOperationException("Could not find a supported terrain shader. Expected Universal Render Pipeline/Terrain/Lit.");
        }

        private static void UseTerrainMaterial(Terrain terrain, Material terrainMaterial)
        {
            terrain.drawInstanced = true;
            if (terrain.materialTemplate != terrainMaterial)
            {
                terrain.materialTemplate = terrainMaterial;
            }

            EditorUtility.SetDirty(terrain);
        }

        private static string PaintTerrain(Terrain terrain, TerrainLayer[] layers, Vector2 globalHeightRange)
        {
            var data = terrain.terrainData;
            AssignTerrainLayers(terrain, layers);

            var width = data.alphamapWidth;
            var height = data.alphamapHeight;
            var assignedLayers = data.terrainLayers;
            var map = new float[height, width, assignedLayers.Length];
            var origin = terrain.transform.position;
            var size = data.size;
            var totals = new float[assignedLayers.Length];
            var totalNormalizedHeight = 0f;
            var totalSlope = 0f;
            var maxSlope = 0f;

            for (var y = 0; y < height; y++)
            {
                var normalizedZ = height <= 1 ? 0f : y / (float)(height - 1);
                for (var x = 0; x < width; x++)
                {
                    var normalizedX = width <= 1 ? 0f : x / (float)(width - 1);
                    var worldX = origin.x + normalizedX * size.x;
                    var worldZ = origin.z + normalizedZ * size.z;

                    var slope = data.GetSteepness(normalizedX, normalizedZ);
                    var worldHeight = origin.y + data.GetInterpolatedHeight(normalizedX, normalizedZ);
                    var normalizedHeight = NormalizeHeight(worldHeight, globalHeightRange);
                    totalNormalizedHeight += normalizedHeight;
                    totalSlope += slope;
                    maxSlope = Mathf.Max(maxSlope, slope);
                    var noiseA = ValueNoise(worldX * 0.037f, worldZ * 0.037f);
                    var noiseB = ValueNoise((worldX + 47.2f) * 0.013f, (worldZ - 19.4f) * 0.013f);

                    var dirtFlecks = Mathf.SmoothStep(0.52f, 0.9f, noiseA)
                        * Mathf.SmoothStep(0.2f, 0.82f, noiseB);

                    var grassWeight = 0f;
                    var dirtWeight = 0f;
                    var packedWeight = 0f;
                    var rockWeight = 0f;

                    if (normalizedHeight < 0.56f && slope < 26f)
                    {
                        grassWeight = 5.6f - normalizedHeight * 2.2f;
                        dirtWeight = 0.28f + dirtFlecks * 0.9f + normalizedHeight * 0.42f;
                        rockWeight = 0.02f + Mathf.SmoothStep(20f, 26f, slope) * 0.08f;
                    }
                    else if (normalizedHeight < 0.76f && slope < 36f)
                    {
                        grassWeight = 1.15f * (1f - normalizedHeight);
                        dirtWeight = 1.7f + dirtFlecks * 0.65f;
                        rockWeight = 0.26f + Mathf.SmoothStep(0.56f, 0.76f, normalizedHeight) * 0.48f;
                    }
                    else
                    {
                        grassWeight = 0.08f * (1f - Mathf.Clamp01(normalizedHeight));
                        dirtWeight = 1.15f + dirtFlecks * 0.35f;
                        rockWeight = 1.2f + Mathf.SmoothStep(0.72f, 1f, normalizedHeight) * 1.15f + Mathf.SmoothStep(32f, 58f, slope) * 1.1f;
                    }

                    Normalize(map, y, x, grassWeight, dirtWeight, packedWeight, rockWeight);
                    for (var layerIndex = 0; layerIndex < assignedLayers.Length; layerIndex++)
                    {
                        totals[layerIndex] += map[y, x, layerIndex];
                    }
                }
            }

            data.SetAlphamaps(0, 0, map);
            var count = Mathf.Max(1, width * height);
            return $"{terrain.name}=height:{totalNormalizedHeight / count:0.00}, slope:{totalSlope / count:0.0}/{maxSlope:0.0}, grass:{totals[0] / count:0.00}, dirt:{totals[1] / count:0.00}, packed:{totals[2] / count:0.00}, rock:{totals[3] / count:0.00}";
        }

        private static void AssignTerrainLayers(Terrain terrain, TerrainLayer[] layers)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                throw new InvalidOperationException("Cannot assign prototype terrain layers to a missing Terrain or TerrainData.");
            }

            var validLayers = layers
                .Where(layer => layer != null)
                .ToArray();

            if (validLayers.Length != layers.Length || validLayers.Length == 0)
            {
                throw new InvalidOperationException("Prototype terrain layer assignment requires a complete non-empty TerrainLayer set.");
            }

            var data = terrain.terrainData;
            Undo.RecordObject(data, "Assign Prototype Terrain Layers");
            data.terrainLayers = validLayers;

            var assignedLayers = data.terrainLayers;
            if (assignedLayers == null || assignedLayers.Length != validLayers.Length || assignedLayers.Any(layer => layer == null))
            {
                throw new InvalidOperationException($"Terrain '{terrain.name}' did not retain the assigned prototype terrain layer set.");
            }

            EditorUtility.SetDirty(data);
            Debug.Log($"Terrain '{terrain.name}' layers: {string.Join(", ", assignedLayers.Select(layer => $"{layer.name} ({AssetDatabase.GetAssetPath(layer)})"))}.");
        }

        private static Vector2 FindHeightRange(IReadOnlyCollection<Terrain> terrains)
        {
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;

            foreach (var terrain in terrains)
            {
                var data = terrain.terrainData;
                var step = Mathf.Max(1, data.heightmapResolution / 64);

                for (var y = 0; y < data.heightmapResolution; y += step)
                {
                    var normalizedY = y / (float)(data.heightmapResolution - 1);
                    for (var x = 0; x < data.heightmapResolution; x += step)
                    {
                        var normalizedX = x / (float)(data.heightmapResolution - 1);
                        var height = terrain.transform.position.y + data.GetInterpolatedHeight(normalizedX, normalizedY);
                        min = Mathf.Min(min, height);
                        max = Mathf.Max(max, height);
                    }
                }
            }

            if (float.IsNaN(min) || float.IsNaN(max) || float.IsInfinity(min) || float.IsInfinity(max) || max - min < 0.001f)
            {
                var fallbackHeight = terrains
                    .Where(terrain => terrain != null && terrain.terrainData != null)
                    .Select(terrain => Mathf.Max(1f, terrain.terrainData.size.y))
                    .DefaultIfEmpty(1f)
                    .Max();

                return new Vector2(0f, fallbackHeight);
            }

            return new Vector2(min, max);
        }

        private static float NormalizeHeight(float height, Vector2 range)
        {
            return Mathf.Clamp01((height - range.x) / Mathf.Max(0.001f, range.y - range.x));
        }

        private static void Normalize(float[,,] map, int y, int x, float grass, float dirt, float packed, float rock)
        {
            grass = Mathf.Max(0f, grass);
            dirt = Mathf.Max(0f, dirt);
            packed = Mathf.Max(0f, packed);
            rock = Mathf.Max(0f, rock);

            var total = grass + dirt + packed + rock;
            if (total <= 0.0001f)
            {
                map[y, x, 0] = 1f;
                return;
            }

            map[y, x, 0] = grass / total;
            map[y, x, 1] = dirt / total;
            map[y, x, 2] = packed / total;
            map[y, x, 3] = rock / total;
        }

        private static float ValueNoise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y);
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
    }
}
