using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityIsekaiGame.Editor
{
    public static class ThirdPartyAssetPreparationTool
    {
        private const string AssetsRoot = "Assets";
        private const string ThirdPartyRoot = "Assets/ThirdParty";
        private static bool VerboseLogging;

        private static readonly HashSet<string> ProtectedTopLevelFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets/_Project",
            "Assets/Packages",
            "Assets/Resources",
            "Assets/StreamingAssets",
            "Assets/ThirdParty"
        };

        [MenuItem("Tools/ThirdParty/Prepare ThirdParty Assets/Prepare Selected Folder")]
        public static void PrepareSelectedAssetFolder()
        {
            string folder = GetSelectedAssetFolder();
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new InvalidOperationException("Select an asset folder in the Project window before running asset preparation.");
            }

            string preparedFolder = IsUnder(folder, ThirdPartyRoot) ? folder : MoveAssetFolderToThirdParty(folder);
            PrepareAssetFolder(preparedFolder);
        }

        [MenuItem("Tools/ThirdParty/Prepare ThirdParty Assets/Prepare All Folders")]
        public static void PrepareAllThirdPartyAssetFolders()
        {
            EnsureFolder(ThirdPartyRoot);

            int folderCount = 0;
            ThirdPartyAssetPreparationReport total = new ThirdPartyAssetPreparationReport();
            foreach (string folder in AssetDatabase.GetSubFolders(ThirdPartyRoot))
            {
                ThirdPartyAssetPreparationReport report = PrepareAssetFolder(folder, false);
                total.Add(report);
                folderCount++;
            }

            Debug.Log($"Prepared {folderCount} third-party asset folder(s). {total}");
        }

        public static void PrepareAssetFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string path = CommandLineValue(args, "-assetPath");
            bool moveToThirdParty = HasCommandLineFlag(args, "-moveToThirdParty");
            VerboseLogging = HasCommandLineFlag(args, "-verbose");

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Missing -assetPath argument for ThirdPartyAssetPreparationTool.PrepareAssetFromCommandLine.");
            }

            string preparedPath = moveToThirdParty ? MoveAssetFolderToThirdParty(path) : path;
            PrepareAssetFolder(preparedPath);
        }

        public static string MoveAssetFolderToThirdParty(string selectedAssetPath)
        {
            string folder = NormalizeAssetPath(selectedAssetPath);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                folder = NormalizeAssetPath(Path.GetDirectoryName(folder));
            }

            if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                throw new InvalidOperationException($"Asset folder was not found: {selectedAssetPath}");
            }

            if (IsUnder(folder, ThirdPartyRoot))
            {
                return folder;
            }

            string topLevel = TopLevelAssetFolder(folder);
            if (string.IsNullOrWhiteSpace(topLevel) || !AssetDatabase.IsValidFolder(topLevel))
            {
                throw new InvalidOperationException($"Only folders under {AssetsRoot} can be moved to {ThirdPartyRoot}: {folder}");
            }

            if (ProtectedTopLevelFolders.Contains(topLevel))
            {
                throw new InvalidOperationException($"Refusing to move protected project folder '{topLevel}' to {ThirdPartyRoot}.");
            }

            EnsureFolder(ThirdPartyRoot);
            string destination = AssetDatabase.GenerateUniqueAssetPath($"{ThirdPartyRoot}/{Path.GetFileName(topLevel)}");
            string moveError = AssetDatabase.MoveAsset(topLevel, destination);
            if (!string.IsNullOrEmpty(moveError))
            {
                throw new InvalidOperationException($"Failed to move '{topLevel}' to '{destination}': {moveError}");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Moved imported asset folder '{topLevel}' to '{destination}'.");
            return destination;
        }

        public static ThirdPartyAssetPreparationReport PrepareAssetFolder(string assetFolderPath, bool logSummary = true)
        {
            string root = NormalizeAssetPath(assetFolderPath);
            if (string.IsNullOrWhiteSpace(root) || !AssetDatabase.IsValidFolder(root))
            {
                throw new InvalidOperationException($"Asset folder was not found: {assetFolderPath}");
            }

            List<string> changedAssetPaths = new List<string>();
            ThirdPartyAssetPreparationReport report = new ThirdPartyAssetPreparationReport(root);
            report.MetaPaths = NormalizeMetaAssetPaths(root);
            report.ModelImporters = NormalizeModelImporters(root, changedAssetPaths);
            report.TextureImporters = NormalizeTextureImporters(root, changedAssetPaths);
            report.Materials = NormalizeMaterials(root, changedAssetPaths);
            report.Prefabs = CleanPrefabs(root);
            report.Scenes = CleanDemoScenes(root);

            AssetDatabase.SaveAssets();
            if (changedAssetPaths.Count > 0)
            {
                List<string> existingChangedAssetPaths = ExistingAssetPaths(changedAssetPaths);
                if (existingChangedAssetPaths.Count > 0)
                {
                    AssetDatabase.ForceReserializeAssets(existingChangedAssetPaths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (logSummary)
            {
                Debug.Log(report.ToString());
            }

            return report;
        }

        private static int NormalizeMetaAssetPaths(string root)
        {
            string absoluteRoot = Path.GetFullPath(root);
            if (!Directory.Exists(absoluteRoot))
            {
                return 0;
            }

            int changed = 0;
            foreach (string metaPath in Directory.EnumerateFiles(absoluteRoot, "*.meta", SearchOption.AllDirectories))
            {
                string assetPath = AssetPathForMetaFile(metaPath);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string text = File.ReadAllText(metaPath);
                string updated = Regex.Replace(
                    text,
                    @"(?m)^(\s*assetPath:\s*).*$",
                    match => match.Groups[1].Value + assetPath);

                if (updated == text)
                {
                    continue;
                }

                File.WriteAllText(metaPath, updated);
                changed++;
            }

            return changed;
        }

        private static int NormalizeModelImporters(string root, ICollection<string> changedAssetPaths)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    continue;
                }

                bool dirty = false;
                SerializedObject serialized = new SerializedObject(importer);
                SerializedProperty materialLocation = serialized.FindProperty("m_Materials.m_MaterialLocation");
                if (materialLocation != null && materialLocation.propertyType == SerializedPropertyType.Integer && materialLocation.intValue != 1)
                {
                    materialLocation.intValue = 1;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                }

                if (importer.importCameras)
                {
                    importer.importCameras = false;
                    dirty = true;
                }

                if (importer.importLights)
                {
                    importer.importLights = false;
                    dirty = true;
                }

                if (!importer.generateSecondaryUV)
                {
                    importer.generateSecondaryUV = true;
                    dirty = true;
                }

                if (!dirty)
                {
                    continue;
                }

                changedAssetPaths.Add(path);
                importer.SaveAndReimport();
                changed++;
            }

            return changed;
        }

        private static int NormalizeTextureImporters(string root, ICollection<string> changedAssetPaths)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);
                bool dirty = false;
                TextureImporterType desiredType = LooksLikeNormalMap(name) ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != desiredType)
                {
                    importer.textureType = desiredType;
                    dirty = true;
                }

                if (LooksLikeMaskMap(name) && importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    dirty = true;
                }

                if (!LooksLikeMaskMap(name) && desiredType == TextureImporterType.Default && !importer.sRGBTexture)
                {
                    importer.sRGBTexture = true;
                    dirty = true;
                }

                if (!importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = true;
                    dirty = true;
                }

                if (!dirty)
                {
                    continue;
                }

                changedAssetPaths.Add(path);
                importer.SaveAndReimport();
                changed++;
            }

            return changed;
        }

        private static int NormalizeMaterials(string root, ICollection<string> changedAssetPaths)
        {
            Shader litShader = FindShader("Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Standard");
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetExtension(path), ".mat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                bool dirty = false;
                Texture baseMap = FirstTexture(material, "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo");
                Texture normalMap = FirstTexture(material, "_BumpMap", "_NormalMap", "_Normal_Texture");
                Color baseColor = FirstColor(material, "_BaseColor", "_Color", "_Tint");
                bool needsShaderReplacement = material.shader == null
                    || material.shader.name == "Standard"
                    || material.shader.name == "Hidden/InternalErrorShader";

                if (needsShaderReplacement)
                {
                    material.shader = litShader;
                    ApplyTexture(material, "_BaseMap", baseMap);
                    ApplyTexture(material, "_MainTex", baseMap);
                    ApplyTexture(material, "_BumpMap", normalMap);
                    ApplyColor(material, "_BaseColor", baseColor);
                    ApplyColor(material, "_Color", baseColor);
                    dirty = true;
                }

                if (normalMap != null && material.HasProperty("_BumpMap"))
                {
                    if (!SerializedTexturePropertyAssigned(path, "_BumpMap") && material.GetTexture("_BumpMap") != normalMap)
                    {
                        material.SetTexture("_BumpMap", normalMap);
                        dirty = true;
                    }

                    if (!SerializedAssetContains(path, "- _NORMALMAP") && !material.IsKeywordEnabled("_NORMALMAP"))
                    {
                        material.EnableKeyword("_NORMALMAP");
                        dirty |= material.IsKeywordEnabled("_NORMALMAP");
                    }
                }

                if (material.HasProperty("_Metallic")
                    && !SerializedFloatApproximately(path, "_Metallic", 0f)
                    && !Mathf.Approximately(material.GetFloat("_Metallic"), 0f))
                {
                    material.SetFloat("_Metallic", 0f);
                    dirty = true;
                }

                float smoothness = IsTransparentMaterial(material) ? 0.35f : 0.25f;
                if (material.HasProperty("_Smoothness")
                    && !SerializedFloatAtMost(path, "_Smoothness", smoothness)
                    && material.GetFloat("_Smoothness") > smoothness)
                {
                    material.SetFloat("_Smoothness", smoothness);
                    dirty = true;
                }

                if (IsTransparentMaterial(material))
                {
                    dirty |= ConfigureTransparentSurface(material);
                }

                if (!dirty)
                {
                    continue;
                }

                if (VerboseLogging)
                {
                    float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                    float materialSmoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0f;
                    bool bumpAssigned = material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null;
                    Debug.Log($"Prepared material '{path}'. Shader='{material.shader?.name}', Metallic={metallic}, Smoothness={materialSmoothness}, BumpAssigned={bumpAssigned}, NormalKeyword={material.IsKeywordEnabled("_NORMALMAP")}.");
                }

                EditorUtility.SetDirty(material);
                changedAssetPaths.Add(path);
                changed++;
            }

            return changed;
        }

        private static int CleanPrefabs(string root)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                int prefabChanged = 0;

                try
                {
                    prefabChanged += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefabRoot);
                    prefabChanged += ConfigurePrefabLights(prefabRoot);
                    prefabChanged += RemoveOrphanUniversalAdditionalLightData(prefabRoot);

                    if (prefabChanged > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        changed += prefabChanged;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return changed;
        }

        private static int ConfigurePrefabLights(GameObject root)
        {
            int changed = 0;
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light == null || light.type == LightType.Directional)
                {
                    continue;
                }

                bool dirty = false;
                if (light.shadows != LightShadows.None && light.shadowResolution != LightShadowResolution.Low)
                {
                    light.shadowResolution = LightShadowResolution.Low;
                    dirty = true;
                }

                if (light.renderMode != LightRenderMode.Auto)
                {
                    light.renderMode = LightRenderMode.Auto;
                    dirty = true;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(light);
                    changed++;
                }
            }

            return changed;
        }

        private static int CleanDemoScenes(string root)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { root });
            if (sceneGuids.Length == 0)
            {
                return 0;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string activeScenePath = activeScene.path;
            if (!string.IsNullOrEmpty(activeScenePath) && activeScene.isDirty)
            {
                Debug.LogWarning($"Skipped scene cleanup under '{root}' because the active scene has unsaved changes.");
                return 0;
            }

            int changed = 0;
            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int sceneChanged = 0;

                foreach (GameObject gameObject in scene.GetRootGameObjects())
                {
                    sceneChanged += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                    sceneChanged += ConfigureSceneLights(gameObject);
                    sceneChanged += RemoveOrphanUniversalAdditionalLightData(gameObject);
                }

                if (LightmapSettings.lightmaps is { Length: > 0 })
                {
                    LightmapSettings.lightmaps = Array.Empty<LightmapData>();
                    sceneChanged++;
                }

                if (LightmapSettings.lightProbes != null)
                {
                    LightmapSettings.lightProbes = null;
                    sceneChanged++;
                }

                try
                {
                    Lightmapping.ClearLightingDataAsset();
                }
                catch (MissingMethodException)
                {
                    // Older editor API fallback: saved scene and sidecar cleanup still remove stale baked data references.
                }

                if (sceneChanged > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changed += sceneChanged;
                }

                changed += DisableSceneBakedLighting(scenePath);
                changed += DeleteAssetIfExists(SceneLightingFolderPath(scenePath));
                changed += DeleteAssetIfExists(SceneLightingSettingsPath(scenePath));
            }

            if (!string.IsNullOrEmpty(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }

            return changed;
        }

        private static int ConfigureSceneLights(GameObject root)
        {
            int changed = 0;
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light == null || light.type == LightType.Directional)
                {
                    continue;
                }

                if (light.shadows != LightShadows.None)
                {
                    light.shadowResolution = LightShadowResolution.Low;
                    EditorUtility.SetDirty(light);
                    changed++;
                }
            }

            return changed;
        }

        private static int RemoveOrphanUniversalAdditionalLightData(GameObject root)
        {
            int changed = 0;
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "UniversalAdditionalLightData")
                {
                    continue;
                }

                if (component.GetComponent<Light>() != null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(component);
                changed++;
            }

            return changed;
        }

        private static int DisableSceneBakedLighting(string scenePath)
        {
            string fullPath = Path.GetFullPath(scenePath);
            if (!File.Exists(fullPath))
            {
                return 0;
            }

            string text = File.ReadAllText(fullPath);
            string updated = text
                .Replace("    m_EnableBakedLightmaps: 1", "    m_EnableBakedLightmaps: 0")
                .Replace("    m_EnableRealtimeLightmaps: 1", "    m_EnableRealtimeLightmaps: 0");

            int lightingSettingsIndex = updated.IndexOf("  m_LightingSettings: {fileID:", StringComparison.Ordinal);
            if (lightingSettingsIndex >= 0)
            {
                int lineEnd = updated.IndexOf('\n', lightingSettingsIndex);
                if (lineEnd < 0)
                {
                    lineEnd = updated.Length;
                }

                updated = updated[..lightingSettingsIndex] + "  m_LightingSettings: {fileID: 0}" + updated[lineEnd..];
            }

            if (updated == text)
            {
                return 0;
            }

            File.WriteAllText(fullPath, updated);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            return 1;
        }

        private static int DeleteAssetIfExists(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return 0;
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null && !AssetDatabase.IsValidFolder(assetPath))
            {
                return 0;
            }

            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                throw new InvalidOperationException($"Failed to delete stale third-party preview asset at {assetPath}.");
            }

            return 1;
        }

        private static string GetSelectedAssetFolder()
        {
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    return path;
                }

                string parent = NormalizeAssetPath(Path.GetDirectoryName(path));
                if (AssetDatabase.IsValidFolder(parent))
                {
                    return parent;
                }
            }

            string activePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            if (AssetDatabase.IsValidFolder(activePath))
            {
                return activePath;
            }

            return AssetDatabase.IsValidFolder(NormalizeAssetPath(Path.GetDirectoryName(activePath)))
                ? NormalizeAssetPath(Path.GetDirectoryName(activePath))
                : string.Empty;
        }

        private static string AssetPathForMetaFile(string metaPath)
        {
            string assetFullPath = metaPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                ? metaPath[..^5]
                : metaPath;

            if (!File.Exists(assetFullPath) && !Directory.Exists(assetFullPath))
            {
                return string.Empty;
            }

            return ToUnityAssetPath(assetFullPath);
        }

        private static string TopLevelAssetFolder(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string[] parts = normalized.Split('/');
            return parts.Length >= 2 ? $"Assets/{parts[1]}" : string.Empty;
        }

        private static bool IsUnder(string path, string root)
        {
            string normalizedPath = NormalizeAssetPath(path);
            string normalizedRoot = NormalizeAssetPath(root);
            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        private static string ToUnityAssetPath(string fullPath)
        {
            string projectRoot = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');
            string normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            if (!normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return normalized[(projectRoot.Length + 1)..];
        }

        private static List<string> ExistingAssetPaths(IEnumerable<string> assetPaths)
        {
            List<string> existing = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string assetPath in assetPaths)
            {
                string normalized = NormalizeAssetPath(assetPath);
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(normalized);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    continue;
                }

                existing.Add(normalized);
            }

            return existing;
        }

        private static bool SerializedAssetContains(string assetPath, string value)
        {
            string normalized = NormalizeAssetPath(assetPath);
            string fullPath = Path.GetFullPath(normalized);
            return File.Exists(fullPath)
                && File.ReadAllText(fullPath).IndexOf(value, StringComparison.Ordinal) >= 0;
        }

        private static bool SerializedTexturePropertyAssigned(string assetPath, string propertyName)
        {
            string normalized = NormalizeAssetPath(assetPath);
            string fullPath = Path.GetFullPath(normalized);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            string text = File.ReadAllText(fullPath);
            string pattern = $@"-\s*{Regex.Escape(propertyName)}:\s*\r?\n\s*m_Texture:\s*\{{fileID:\s*(?!0(?:,|\}}))";
            return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
        }

        private static bool SerializedFloatApproximately(string assetPath, string propertyName, float expected)
        {
            return TryReadSerializedFloat(assetPath, propertyName, out float value)
                && Mathf.Approximately(value, expected);
        }

        private static bool SerializedFloatAtMost(string assetPath, string propertyName, float maximum)
        {
            return TryReadSerializedFloat(assetPath, propertyName, out float value)
                && value <= maximum;
        }

        private static bool TryReadSerializedFloat(string assetPath, string propertyName, out float value)
        {
            value = 0f;
            string normalized = NormalizeAssetPath(assetPath);
            string fullPath = Path.GetFullPath(normalized);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            string text = File.ReadAllText(fullPath);
            Match match = Regex.Match(
                text,
                $@"-\s*{Regex.Escape(propertyName)}:\s*(?<value>[-+]?\d+(?:\.\d+)?)",
                RegexOptions.CultureInvariant);
            return match.Success
                && float.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void EnsureFolder(string folder)
        {
            string normalized = NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string parent = NormalizeAssetPath(Path.GetDirectoryName(normalized));
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            string createError = AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
            if (!string.IsNullOrEmpty(createError))
            {
                throw new InvalidOperationException($"Failed to create folder '{normalized}': {createError}");
            }
        }

        private static string CommandLineValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static bool HasCommandLineFlag(string[] args, string name)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeNormalMap(string name)
        {
            return ContainsToken(name, "normal")
                || ContainsToken(name, "norm")
                || ContainsToken(name, "n");
        }

        private static bool LooksLikeMaskMap(string name)
        {
            return ContainsToken(name, "orm")
                || ContainsToken(name, "mask")
                || ContainsToken(name, "metallic")
                || ContainsToken(name, "roughness")
                || ContainsToken(name, "ao");
        }

        private static bool ContainsToken(string name, string token)
        {
            return Regex.IsMatch(name, $@"(^|[_\-\s]){Regex.Escape(token)}($|[_\-\s])", RegexOptions.IgnoreCase);
        }

        private static bool IsTransparentMaterial(Material material)
        {
            string name = material.name;
            return name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Shader FindShader(params string[] names)
        {
            foreach (string name in names)
            {
                Shader shader = Shader.Find(name);
                if (shader != null)
                {
                    return shader;
                }
            }

            throw new InvalidOperationException("No supported material shader was found for asset preparation.");
        }

        private static Texture FirstTexture(Material material, params string[] names)
        {
            foreach (string name in names)
            {
                if (!material.HasProperty(name))
                {
                    continue;
                }

                Texture texture = material.GetTexture(name);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private static Color FirstColor(Material material, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    return material.GetColor(name);
                }
            }

            return Color.white;
        }

        private static bool ApplyTexture(Material material, string propertyName, Texture texture)
        {
            if (!material.HasProperty(propertyName) || material.GetTexture(propertyName) == texture)
            {
                return false;
            }

            material.SetTexture(propertyName, texture);
            return true;
        }

        private static bool ApplyColor(Material material, string propertyName, Color value)
        {
            if (!material.HasProperty(propertyName) || material.GetColor(propertyName) == value)
            {
                return false;
            }

            material.SetColor(propertyName, value);
            return true;
        }

        private static bool ApplyFloat(Material material, string propertyName, float value)
        {
            if (!material.HasProperty(propertyName) || Mathf.Approximately(material.GetFloat(propertyName), value))
            {
                return false;
            }

            material.SetFloat(propertyName, value);
            return true;
        }

        private static bool ConfigureTransparentSurface(Material material)
        {
            bool dirty = false;
            dirty |= ApplyFloat(material, "_Surface", 1f);
            dirty |= ApplyFloat(material, "_Blend", 0f);
            dirty |= ApplyFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            dirty |= ApplyFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            dirty |= ApplyFloat(material, "_ZWrite", 0f);

            if (material.GetTag("RenderType", false, string.Empty) != "Transparent")
            {
                material.SetOverrideTag("RenderType", "Transparent");
                dirty |= material.GetTag("RenderType", false, string.Empty) == "Transparent";
            }

            if (material.renderQueue != (int)RenderQueue.Transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                dirty = true;
            }

            if (!material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                dirty |= material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            }

            return dirty;
        }

        private static string SceneLightingFolderPath(string scenePath)
        {
            string directory = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(scenePath);
            return string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name) ? string.Empty : $"{directory}/{name}";
        }

        private static string SceneLightingSettingsPath(string scenePath)
        {
            string directory = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(scenePath);
            return string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name) ? string.Empty : $"{directory}/{name}Settings.lighting";
        }

        public sealed class ThirdPartyAssetPreparationReport
        {
            public ThirdPartyAssetPreparationReport()
            {
            }

            public ThirdPartyAssetPreparationReport(string root)
            {
                Root = root;
            }

            public string Root { get; }
            public int MetaPaths { get; set; }
            public int ModelImporters { get; set; }
            public int TextureImporters { get; set; }
            public int Materials { get; set; }
            public int Prefabs { get; set; }
            public int Scenes { get; set; }

            public void Add(ThirdPartyAssetPreparationReport report)
            {
                if (report == null)
                {
                    return;
                }

                MetaPaths += report.MetaPaths;
                ModelImporters += report.ModelImporters;
                TextureImporters += report.TextureImporters;
                Materials += report.Materials;
                Prefabs += report.Prefabs;
                Scenes += report.Scenes;
            }

            public override string ToString()
            {
                string prefix = string.IsNullOrEmpty(Root) ? "Prepared third-party assets." : $"Prepared asset folder '{Root}'.";
                return $"{prefix} Paths={MetaPaths}, Importers={ModelImporters}, Textures={TextureImporters}, Materials={Materials}, PrefabFixes={Prefabs}, SceneFixes={Scenes}.";
            }
        }
    }
}
