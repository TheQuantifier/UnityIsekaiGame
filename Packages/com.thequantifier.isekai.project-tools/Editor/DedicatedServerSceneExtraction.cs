using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Player;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Editor
{
    /// <summary>
    /// Produces the asset-light authoritative scene used by the physically separate server project.
    /// It keeps simulation state, interaction bindings, authoritative collision, and server networking,
    /// while removing every client adapter and presentation component.
    /// </summary>
    public static class DedicatedServerSceneExtraction
    {
        public const string SourceScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        public const string OutputScenePath = "Assets/_Project/Scenes/Server/ServerPrototypeScene.unity";
        public const string OutputManifestPath = "Assets/_Project/Scenes/Server/ServerPrototypeScene.inputs.sha256";
        private const string ServerTerrainFolder = "Packages/com.thequantifier.isekai.content/Content/World/Terrain/ServerCollision";
        private const string ServerCollisionMeshFolder = "Packages/com.thequantifier.isekai.content/Content/World/CollisionMeshes";
        private const string ServerPrototypeSeedPersonId = "person.server.prototype-seed";

        public static void ExtractCommandLine()
        {
            Extract();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        [MenuItem("Tools/Unity Isekai Game/Networking/Extract Dedicated Server Scene")]
        public static void Extract()
        {
            EnsureFolder("Assets/_Project/Scenes/Server");
            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, OutputScenePath, false))
            {
                throw new InvalidOperationException($"Could not create '{OutputScenePath}'.");
            }

            int unpackedPrefabs = 0;
            bool unpackedAny;
            do
            {
                unpackedAny = false;
                GameObject[] instanceRoots = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject))
                    .Where(root => root != null)
                    .Distinct()
                    .ToArray();
                foreach (GameObject instanceRoot in instanceRoots)
                {
                    PrefabUtility.UnpackPrefabInstance(
                        instanceRoot,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    unpackedPrefabs++;
                    unpackedAny = true;
                }
            }
            while (unpackedAny);

            int stabilizedSeedIdentities = StabilizeServerPrototypeSeedIdentities(scene);
            TerrainCollisionResult terrainCollision = CreateServerTerrainCollisionData(scene);
            Collider[] allSourceColliders = GetSceneColliders(scene);
            ColliderSnapshot[] sourceColliders = allSourceColliders
                .Where(IsOperationalCollider)
                .Select(ColliderSnapshot.Capture)
                .ToArray();
            int sourceTerrainColliderCount = sourceColliders.Count(collider => collider.ExpectedType == typeof(TerrainCollider));
            ColliderAdaptationResult colliderAdaptation = AdaptMeshColliders(scene);

            int removedComponents = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true)
                    .Reverse()
                    .OrderBy(component => component is EventSystem ? 1 : 0))
                {
                    if (component != null && ShouldRemove(component))
                    {
                        UnityEngine.Object.DestroyImmediate(component);
                        removedComponents++;
                    }
                }
            }

            ValidateColliderPreservation(
                scene,
                sourceColliders,
                sourceTerrainColliderCount);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OutputScenePath, false))
            {
                throw new InvalidOperationException($"Could not save the stripped server scene '{OutputScenePath}'.");
            }

            AssetDatabase.SaveAssets();
            WriteSourceFingerprintManifest();
            string[] projectDependencies = AssetDatabase.GetDependencies(OutputScenePath, true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !string.Equals(path, OutputScenePath, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string physicalServerScenePath = SynchronizePhysicalServerScene();
            Debug.Log(
                $"Extracted dedicated-server scene '{OutputScenePath}'. Unpacked {unpackedPrefabs} prefab instance(s) and " +
                $"redirected {colliderAdaptation.AdaptedMeshColliders} mesh collider(s) to {colliderAdaptation.CollisionMeshAssets} deduplicated collision-only mesh asset(s), " +
                $"removed {colliderAdaptation.RemovedInvalidMeshColliders} inert mesh collider(s) without meshes, and " +
                $"removed {removedComponents} presentation component(s). " +
                $"Stabilized {stabilizedSeedIdentities} server prototype seed identity component(s). " +
                $"Preserved all {sourceColliders.Length} operational collider(s), including {sourceTerrainColliderCount} terrain collider(s). " +
                $"Generated {terrainCollision.TerrainDataAssets} collision-only terrain asset(s) and " +
                $"{terrainCollision.TreeProxyPrefabs} server-safe tree collider proxy prefab(s), while pruning " +
                $"{terrainCollision.PrunedNonCollidingTreeInstances} terrain tree/shrub instance(s) that had no source colliders. " +
                $"Synchronized '{physicalServerScenePath}'. " +
                $"Remaining project-owned dependencies ({projectDependencies.Length}):\n{string.Join("\n", projectDependencies)}");
        }

        private static int StabilizeServerPrototypeSeedIdentities(Scene scene)
        {
            PlayerIdentityProgression[] identities = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerIdentityProgression>(true))
                .ToArray();
            foreach (PlayerIdentityProgression identity in identities)
            {
                identity.ConfigureIdentity(identity.AccountId, identity.PlayerId, ServerPrototypeSeedPersonId);
                EditorUtility.SetDirty(identity);
            }

            return identities.Length;
        }

        private static string SynchronizePhysicalServerScene()
        {
            string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputScenePath));
            string targetPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "Server",
                "Assets",
                "Scenes",
                "ServerPrototypeScene.unity"));
            string targetDirectory = Path.GetDirectoryName(targetPath)
                ?? throw new InvalidOperationException("The physical server scene directory could not be resolved.");
            Directory.CreateDirectory(targetDirectory);
            File.Copy(sourcePath, targetPath, true);
            NormalizeLocalDedicatedServerYaml(targetPath);
            string sourceManifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputManifestPath));
            string targetManifestPath = Path.ChangeExtension(targetPath, ".inputs.sha256");
            File.Copy(sourceManifestPath, targetManifestPath, true);
            return targetPath;
        }

        private static void WriteSourceFingerprintManifest()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
            var dependencies = new HashSet<string>(AssetDatabase.GetDependencies(SourceScenePath, true), StringComparer.Ordinal)
            {
                SourceScenePath,
                "Packages/com.thequantifier.isekai.project-tools/Editor/DedicatedServerSceneExtraction.cs"
            };
            var lines = new List<string> { "version=1" };
            foreach (string assetPath in dependencies.OrderBy(value => value, StringComparer.Ordinal))
            {
                bool alwaysInclude = string.Equals(assetPath, SourceScenePath, StringComparison.Ordinal)
                    || string.Equals(
                        assetPath,
                        "Packages/com.thequantifier.isekai.project-tools/Editor/DedicatedServerSceneExtraction.cs",
                        StringComparison.Ordinal);
                string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                if (!alwaysInclude && extension != ".asset" && extension != ".prefab"
                    && extension != ".fbx" && extension != ".obj" && extension != ".dae" && extension != ".blend")
                    continue;
                string physicalPath = ResolvePhysicalAssetPath(projectRoot, assetPath);
                if (string.IsNullOrWhiteSpace(physicalPath) || !File.Exists(physicalPath)) continue;
                string normalizedPhysicalPath = physicalPath.Replace('\\', '/');
                if (normalizedPhysicalPath.IndexOf("/Library/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                AddManifestEntry(lines, repositoryRoot, physicalPath);
                string metaPath = physicalPath + ".meta";
                if (File.Exists(metaPath)) AddManifestEntry(lines, repositoryRoot, metaPath);
            }

            string manifestPath = Path.GetFullPath(Path.Combine(projectRoot, OutputManifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)
                ?? throw new InvalidOperationException("The server scene manifest directory could not be resolved."));
            File.WriteAllLines(manifestPath, lines, new UTF8Encoding(false));
        }

        private static void AddManifestEntry(ICollection<string> lines, string repositoryRoot, string physicalPath)
        {
            string relative = Path.GetRelativePath(repositoryRoot, physicalPath).Replace('\\', '/');
            if (relative.StartsWith("../", StringComparison.Ordinal))
            {
                    throw new InvalidOperationException($"Server extraction dependency escaped the repository: '{physicalPath}'.");
            }

            lines.Add($"{ComputeSha256(physicalPath)}|{relative}");
        }

        private static string ResolvePhysicalAssetPath(string projectRoot, string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (!assetPath.StartsWith("Packages/", StringComparison.Ordinal)) return string.Empty;
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath)) return string.Empty;
            string packagePrefix = $"Packages/{package.name}";
            string packageRelative = assetPath.Length == packagePrefix.Length
                ? string.Empty
                : assetPath.Substring(packagePrefix.Length + 1);
            return Path.GetFullPath(Path.Combine(package.resolvedPath, packageRelative));
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(stream);
            return string.Concat(hash.Select(value => value.ToString("x2")));
        }

        private static void NormalizeLocalDedicatedServerYaml(string scenePath)
        {
            // Unity serializes an empty MonoBehaviour name with a trailing space. Because this
            // component is introduced by extraction, that otherwise leaves every synchronized
            // server scene with a new diff-check violation. Limit normalization to the extracted
            // server host component so existing authored YAML is not mechanically rewritten.
            const string classMarker =
                "  m_EditorClassIdentifier: UnityIsekaiGame.Networking.Server::UnityIsekaiGame.Networking.Server.LocalDedicatedServer";
            string yaml = File.ReadAllText(scenePath);
            int classIndex = yaml.IndexOf(classMarker, StringComparison.Ordinal);
            if (classIndex < 0)
            {
                return;
            }

            int nameIndex = yaml.LastIndexOf("  m_Name: ", classIndex, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return;
            }

            int nameValueEnd = nameIndex + "  m_Name:".Length;
            yaml = yaml.Remove(nameValueEnd, 1);
            File.WriteAllText(scenePath, yaml);
        }

        private static Collider[] GetSceneColliders(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Collider>(true))
            .ToArray();

        private static TerrainCollisionResult CreateServerTerrainCollisionData(Scene scene)
        {
            EnsureFolder(ServerTerrainFolder);
            var treeProxyCache = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            TerrainCollider[] terrainColliders = GetSceneColliders(scene).OfType<TerrainCollider>().ToArray();
            int prunedNonCollidingTreeInstances = 0;
            foreach (TerrainCollider terrainCollider in terrainColliders)
            {
                TerrainData source = terrainCollider.terrainData;
                if (source == null)
                {
                    throw new InvalidOperationException(
                        $"TerrainCollider '{GetHierarchyPath(terrainCollider.transform)}' has no TerrainData.");
                }

                string sourcePath = AssetDatabase.GetAssetPath(source);
                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                string collisionPath = $"{ServerTerrainFolder}/{SanitizeFileName(source.name)}-{sourceGuid}-ServerCollision.asset";
                TerrainData collision = AssetDatabase.LoadAssetAtPath<TerrainData>(collisionPath);
                if (collision == null)
                {
                    collision = new TerrainData();
                    AssetDatabase.CreateAsset(collision, collisionPath);
                }

                EditorUtility.CopySerialized(source, collision);
                collision.name = $"{source.name} Server Collision";
                TreeInstance[] treeInstances = source.treeInstances;
                TreePrototype[] sourcePrototypes = source.treePrototypes;
                var prototypeRemap = Enumerable.Repeat(-1, sourcePrototypes.Length).ToArray();
                var collisionPrototypes = new List<TreePrototype>();
                for (int i = 0; i < sourcePrototypes.Length; i++)
                {
                    TreePrototype sourcePrototype = sourcePrototypes[i];
                    GameObject proxy = CreateOrUpdateTreeCollisionProxy(sourcePrototype.prefab, treeProxyCache);
                    if (proxy.GetComponentInChildren<Collider>(true) == null)
                    {
                        continue;
                    }

                    prototypeRemap[i] = collisionPrototypes.Count;
                    collisionPrototypes.Add(new TreePrototype
                    {
                        prefab = proxy,
                        bendFactor = sourcePrototype.bendFactor,
                        navMeshLod = sourcePrototype.navMeshLod
                    });
                }

                var collisionTreeInstances = new List<TreeInstance>();
                foreach (TreeInstance sourceTree in treeInstances)
                {
                    if (sourceTree.prototypeIndex < 0 || sourceTree.prototypeIndex >= prototypeRemap.Length)
                    {
                        throw new InvalidOperationException(
                            $"TerrainData '{sourcePath}' contains invalid tree prototype index {sourceTree.prototypeIndex}.");
                    }

                    int collisionPrototypeIndex = prototypeRemap[sourceTree.prototypeIndex];
                    if (collisionPrototypeIndex < 0)
                    {
                        prunedNonCollidingTreeInstances++;
                        continue;
                    }

                    TreeInstance collisionTree = sourceTree;
                    collisionTree.prototypeIndex = collisionPrototypeIndex;
                    collisionTreeInstances.Add(collisionTree);
                }

                collision.terrainLayers = Array.Empty<TerrainLayer>();
                collision.detailPrototypes = Array.Empty<DetailPrototype>();
                collision.treePrototypes = collisionPrototypes.ToArray();
                collision.treeInstances = collisionTreeInstances.ToArray();
                EditorUtility.SetDirty(collision);
                terrainCollider.terrainData = collision;
                EditorUtility.SetDirty(terrainCollider);
            }

            AssetDatabase.SaveAssets();
            return new TerrainCollisionResult(
                terrainColliders.Length,
                treeProxyCache.Count,
                prunedNonCollidingTreeInstances);
        }

        private static GameObject CreateOrUpdateTreeCollisionProxy(
            GameObject sourcePrefab,
            IDictionary<string, GameObject> cache)
        {
            if (sourcePrefab == null)
            {
                throw new InvalidOperationException("A TerrainData tree prototype has no prefab and cannot be represented on the server.");
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (cache.TryGetValue(sourceGuid, out GameObject cached))
            {
                return cached;
            }

            string proxyPath = $"{ServerTerrainFolder}/Tree-{SanitizeFileName(sourcePrefab.name)}-{sourceGuid}-ServerCollision.prefab";
            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate tree prototype '{sourcePath}'.");
            }

            try
            {
                UnpackAllPrefabInstances(instance);
                instance.name = $"{sourcePrefab.name} Server Collision";
                EnsureFolder(ServerCollisionMeshFolder);
                var collisionMeshCache = new Dictionary<string, Mesh>(StringComparer.Ordinal);
                foreach (MeshCollider meshCollider in instance.GetComponentsInChildren<MeshCollider>(true).ToArray())
                {
                    AdaptMeshCollider(meshCollider, collisionMeshCache);
                }

                foreach (Component component in instance.GetComponentsInChildren<Component>(true).Reverse())
                {
                    if (component != null && !(component is Transform) && !(component is Collider))
                    {
                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }

                Mesh markerMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (markerMesh == null)
                {
                    throw new InvalidOperationException("Unity's built-in Cube mesh is unavailable for the server terrain-tree proxy.");
                }

                MeshFilter markerFilter = instance.AddComponent<MeshFilter>();
                markerFilter.sharedMesh = markerMesh;
                MeshRenderer markerRenderer = instance.AddComponent<MeshRenderer>();
                markerRenderer.sharedMaterial = GetOrCreateTerrainTreeMarkerMaterial();
                LODGroup markerLodGroup = instance.AddComponent<LODGroup>();
                markerLodGroup.SetLODs(new[]
                {
                    new LOD(0.01f, new Renderer[] { markerRenderer })
                });
                markerLodGroup.RecalculateBounds();

                GameObject proxy = PrefabUtility.SaveAsPrefabAsset(instance, proxyPath);
                if (proxy == null)
                {
                    throw new InvalidOperationException($"Could not save server tree collision proxy '{proxyPath}'.");
                }

                cache[sourceGuid] = proxy;
                return proxy;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void UnpackAllPrefabInstances(GameObject root)
        {
            while (true)
            {
                GameObject[] instanceRoots = root.GetComponentsInChildren<Transform>(true)
                    .Select(transform => PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject))
                    .Where(instanceRoot => instanceRoot != null)
                    .Distinct()
                    .ToArray();
                if (instanceRoots.Length == 0)
                {
                    return;
                }

                foreach (GameObject instanceRoot in instanceRoots)
                {
                    PrefabUtility.UnpackPrefabInstance(
                        instanceRoot,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }
            }
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        }

        private static Material GetOrCreateTerrainTreeMarkerMaterial()
        {
            string materialPath = $"{ServerTerrainFolder}/ServerTerrainTreeMarker.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>($"{ServerTerrainFolder}/ServerTerrainTreeMarker.shader") ??
                Shader.Find("Nature/Soft Occlusion Leaves") ??
                Shader.Find("Nature/Soft Occlusion Bark") ??
                Shader.Find("Standard") ??
                Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
            {
                throw new InvalidOperationException("No built-in shader is available for the server terrain-tree marker material.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "Server Terrain Tree Marker" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.color = Color.clear;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ValidateColliderPreservation(
            Scene scene,
            ColliderSnapshot[] expectedColliders,
            int expectedTerrainColliderCount)
        {
            Collider[] colliders = GetSceneColliders(scene);
            if (colliders.Length != expectedColliders.Length)
            {
                throw new InvalidOperationException(
                    $"Dedicated-server extraction changed the source scene's collider count. " +
                    $"Expected {expectedColliders.Length}, got {colliders.Length}.");
            }

            var unmatched = colliders.ToList();
            foreach (ColliderSnapshot expected in expectedColliders)
            {
                Collider match = unmatched.FirstOrDefault(collider =>
                    GetHierarchyPath(collider.transform) == expected.HierarchyPath &&
                    collider.GetType() == expected.ExpectedType &&
                    collider.enabled == expected.Enabled &&
                    collider.isTrigger == expected.IsTrigger);
                if (match == null)
                {
                    throw new InvalidOperationException(
                        $"Dedicated-server extraction did not preserve collider '{expected.HierarchyPath}' as " +
                        $"{expected.ExpectedType.Name} (enabled={expected.Enabled}, trigger={expected.IsTrigger}).");
                }

                unmatched.Remove(match);
            }

            if (unmatched.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Dedicated-server extraction created {unmatched.Count} unexpected collider(s): " +
                    string.Join(", ", unmatched.Select(collider => GetHierarchyPath(collider.transform))));
            }

            TerrainCollider[] terrainColliders = colliders.OfType<TerrainCollider>().ToArray();
            if (terrainColliders.Length != expectedTerrainColliderCount || terrainColliders.Any(collider => collider.terrainData == null))
            {
                throw new InvalidOperationException(
                    $"Dedicated-server extraction must preserve all {expectedTerrainColliderCount} terrain collider(s) with resolved TerrainData.");
            }

            MeshCollider[] meshColliders = colliders.OfType<MeshCollider>().ToArray();
            MeshCollider[] invalidMeshColliders = meshColliders
                .Where(collider => collider.sharedMesh == null ||
                    !AssetDatabase.GetAssetPath(collider.sharedMesh).StartsWith(ServerCollisionMeshFolder + "/", StringComparison.Ordinal))
                .ToArray();
            if (invalidMeshColliders.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Dedicated-server extraction left {invalidMeshColliders.Length} MeshCollider component(s) without shared collision-only meshes.");
            }
        }

        private static ColliderAdaptationResult AdaptMeshColliders(Scene scene)
        {
            EnsureFolder(ServerCollisionMeshFolder);
            MeshCollider[] meshColliders = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MeshCollider>(true))
                .ToArray();
            var collisionMeshCache = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            int adaptedCount = 0;
            int invalidCount = 0;
            foreach (MeshCollider meshCollider in meshColliders)
            {
                if (AdaptMeshCollider(meshCollider, collisionMeshCache)) adaptedCount++;
                else invalidCount++;
            }

            AssetDatabase.SaveAssets();
            return new ColliderAdaptationResult(adaptedCount, invalidCount, collisionMeshCache.Count);
        }

        private static bool IsOperationalCollider(Collider collider) =>
            !(collider is MeshCollider meshCollider) || meshCollider.sharedMesh != null;

        private static bool AdaptMeshCollider(MeshCollider meshCollider, IDictionary<string, Mesh> collisionMeshCache)
        {
            Mesh mesh = meshCollider.sharedMesh;
            if (mesh == null)
            {
                Debug.LogWarning(
                    $"Removing inert MeshCollider '{GetHierarchyPath(meshCollider.transform)}' because it has no shared mesh and " +
                    "therefore provides no collision in the source scene.");
                UnityEngine.Object.DestroyImmediate(meshCollider);
                return false;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string sourceGuid, out long sourceLocalId))
            {
                throw new InvalidOperationException(
                    $"Could not resolve the source asset identity for MeshCollider '{GetHierarchyPath(meshCollider.transform)}'.");
            }

            string cacheKey = $"{sourceGuid}-{sourceLocalId}";
            if (!collisionMeshCache.TryGetValue(cacheKey, out Mesh collisionMesh))
            {
                string collisionPath = $"{ServerCollisionMeshFolder}/{SanitizeFileName(mesh.name)}-{sourceGuid}-{sourceLocalId}-ServerCollision.asset";
                collisionMesh = AssetDatabase.LoadAssetAtPath<Mesh>(collisionPath);
                if (collisionMesh == null)
                {
                    collisionMesh = UnityEngine.Object.Instantiate(mesh);
                    collisionMesh.name = $"{mesh.name} Server Collision";
                    AssetDatabase.CreateAsset(collisionMesh, collisionPath);
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, collisionMesh);
                    collisionMesh.name = $"{mesh.name} Server Collision";
                    EditorUtility.SetDirty(collisionMesh);
                }

                collisionMeshCache.Add(cacheKey, collisionMesh);
            }

            meshCollider.sharedMesh = collisionMesh;
            EditorUtility.SetDirty(meshCollider);
            return true;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = $"{transform.name}[{transform.GetSiblingIndex()}]";
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}[{transform.GetSiblingIndex()}]/{path}";
            }

            return path;
        }

        private readonly struct ColliderSnapshot
        {
            private ColliderSnapshot(string hierarchyPath, Type expectedType, bool enabled, bool isTrigger)
            {
                HierarchyPath = hierarchyPath;
                ExpectedType = expectedType;
                Enabled = enabled;
                IsTrigger = isTrigger;
            }

            public string HierarchyPath { get; }
            public Type ExpectedType { get; }
            public bool Enabled { get; }
            public bool IsTrigger { get; }

            public static ColliderSnapshot Capture(Collider collider) => new ColliderSnapshot(
                GetHierarchyPath(collider.transform),
                collider.GetType(),
                collider.enabled,
                collider.isTrigger);
        }

        private readonly struct ColliderAdaptationResult
        {
            public ColliderAdaptationResult(int adaptedMeshColliders, int removedInvalidMeshColliders, int collisionMeshAssets)
            {
                AdaptedMeshColliders = adaptedMeshColliders;
                RemovedInvalidMeshColliders = removedInvalidMeshColliders;
                CollisionMeshAssets = collisionMeshAssets;
            }

            public int AdaptedMeshColliders { get; }
            public int RemovedInvalidMeshColliders { get; }
            public int CollisionMeshAssets { get; }
        }

        private readonly struct TerrainCollisionResult
        {
            public TerrainCollisionResult(
                int terrainDataAssets,
                int treeProxyPrefabs,
                int prunedNonCollidingTreeInstances)
            {
                TerrainDataAssets = terrainDataAssets;
                TreeProxyPrefabs = treeProxyPrefabs;
                PrunedNonCollidingTreeInstances = prunedNonCollidingTreeInstances;
            }

            public int TerrainDataAssets { get; }
            public int TreeProxyPrefabs { get; }
            public int PrunedNonCollidingTreeInstances { get; }
        }

        private static bool ShouldRemove(Component component)
        {
            if (component is Transform)
            {
                return false;
            }

            Type type = component.GetType();
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            string namespaceName = type.Namespace ?? string.Empty;
            if (assemblyName.Equals("UnityIsekaiGame.Networking.Client", StringComparison.Ordinal) ||
                assemblyName.Equals("UnityIsekaiGame.UI", StringComparison.Ordinal) ||
                assemblyName.Equals("UnityIsekaiGame.Development", StringComparison.Ordinal) ||
                assemblyName.Equals("Assembly-CSharp", StringComparison.Ordinal) ||
                namespaceName.StartsWith("UnityEngine.UI", StringComparison.Ordinal) ||
                namespaceName.StartsWith("UnityEngine.EventSystems", StringComparison.Ordinal) ||
                namespaceName.StartsWith("TMPro", StringComparison.Ordinal) ||
                namespaceName.StartsWith("UnityEngine.Rendering", StringComparison.Ordinal))
            {
                return true;
            }

            return component is Renderer ||
                component is MeshFilter ||
                component is Terrain ||
                component is Camera ||
                component is Light ||
                component is AudioSource ||
                component is AudioListener ||
                component is Animator ||
                component is Animation ||
                component is ParticleSystem ||
                component is Canvas ||
                component is CanvasRenderer ||
                component is EventSystem ||
                component is BaseInputModule ||
                component is Selectable ||
                component.GetType().FullName == "UnityIsekaiGame.Interaction.InteractionPromptView" ||
                component.GetType().FullName == "UnityIsekaiGame.Interaction.InteractionPromptPresenter" ||
                component is PlayerInputReader ||
                component is FirstPersonCharacterMotor ||
                HasImmediateModeGuiCallback(component);
        }

        private static bool HasImmediateModeGuiCallback(Component component)
        {
            if (!(component is MonoBehaviour))
            {
                return false;
            }

            return component.GetType().GetMethod(
                "OnGUI",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string name = path.Substring(path.LastIndexOf('/') + 1);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
