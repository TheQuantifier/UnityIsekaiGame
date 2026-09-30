using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Server;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.ServerProject.Editor
{
    public static class ServerProjectBuildAutomation
    {
        public const string ServerScenePath = "Assets/Scenes/ServerPrototypeScene.unity";
        public const string ServerSceneManifestPath = "Assets/Scenes/ServerPrototypeScene.inputs.sha256";
        private const string StagedServerScenePath = "Assets/Generated/Build/ServerPrototypeScene.unity";
        public const string DefaultOutputPath = "../../Builds/LocalServer/UnityIsekaiServer.exe";

        [MenuItem("Tools/Unity Isekai Game/Build Windows Dedicated Server")]
        public static void BuildWindowsDedicatedServer() => Build(DefaultOutputPath);

        public static void BuildWindowsDedicatedServerCommandLine() => BuildWindowsDedicatedServer();

        [MenuItem("Tools/Unity Isekai Game/Prepare Authoritative Server Scene")]
        public static void PrepareAuthoritativeServerScene() => PrepareAuthoritativeServerSceneInternal(ServerScenePath);

        public static void PrepareAuthoritativeServerSceneCommandLine() => PrepareAuthoritativeServerSceneInternal(ServerScenePath);

        private static void Build(string relativeOutputPath)
        {
            if (!File.Exists(ServerScenePath))
            {
                throw new BuildFailedException($"The authoritative server scene is missing at '{ServerScenePath}'.");
            }

            ValidateSourceFingerprint();
            EnsureAssetFolder("Assets/Generated/Build");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StagedServerScenePath) != null)
                AssetDatabase.DeleteAsset(StagedServerScenePath);
            if (!AssetDatabase.CopyAsset(ServerScenePath, StagedServerScenePath))
                throw new BuildFailedException($"Could not create staged server scene '{StagedServerScenePath}'.");

            try
            {
                PrepareAuthoritativeServerSceneInternal(StagedServerScenePath);
                ValidateAuthoritativeCollision(StagedServerScenePath);
                string projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("The server project root could not be resolved.");
                string outputPath = Path.GetFullPath(Path.Combine(projectRoot, relativeOutputPath));
                using var output = new BuildOutputTransaction(outputPath);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { StagedServerScenePath },
                    locationPathName = output.StagingExecutablePath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    options = BuildOptions.None
                });

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"Dedicated-server build failed with {report.summary.totalErrors} error(s). See the Editor log.");
                }

                output.ValidateManagedAssemblies(new[]
                {
                    "UnityIsekaiGame.Networking.Client.dll",
                    "UnityIsekaiGame.UI.dll",
                    "UnityIsekaiGame.Development.dll",
                    "Unity.InputSystem.dll",
                    "Unity.InputSystem.ForUI.dll",
                    "UnityEngine.UI.dll"
                });
                output.Commit();
                Debug.Log($"Built dedicated server at '{outputPath}' ({report.summary.totalSize:N0} bytes).");
            }
            finally
            {
                AssetDatabase.DeleteAsset(StagedServerScenePath);
            }
        }

        private static void PrepareAuthoritativeServerSceneInternal(string scenePath)
        {
            Scene serverScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Component[] components = serverScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component != null)
                .ToArray();
            NetworkManager networkManager = components.OfType<NetworkManager>().SingleOrDefault()
                ?? throw new BuildFailedException("The synchronized server scene must contain exactly one NetworkManager.");
            LocalDedicatedServer server = networkManager.GetComponent<LocalDedicatedServer>();
            if (server == null)
            {
                server = networkManager.gameObject.AddComponent<LocalDedicatedServer>();
            }

            PlayerInventory inventory = components.OfType<PlayerInventory>().SingleOrDefault()
                ?? throw new BuildFailedException("The synchronized server scene must contain exactly one prototype PlayerInventory.");
            PlayerEquipment equipment = inventory.GetComponent<PlayerEquipment>()
                ?? components.OfType<PlayerEquipment>().SingleOrDefault()
                ?? throw new BuildFailedException("The synchronized server scene must contain prototype PlayerEquipment state.");
            CharacterController playerController = inventory.GetComponent<CharacterController>()
                ?? inventory.GetComponentInParent<CharacterController>()
                ?? throw new BuildFailedException("The prototype player inventory must resolve its CharacterController.");
            CharacterResourceCollection resources = inventory.GetComponent<CharacterResourceCollection>()
                ?? inventory.GetComponentInParent<CharacterResourceCollection>()
                ?? components.OfType<CharacterResourceCollection>().FirstOrDefault()
                ?? throw new BuildFailedException("The synchronized server scene must contain prototype player resources.");
            PlayerMeleeCombat meleeCombat = inventory.GetComponent<PlayerMeleeCombat>()
                ?? inventory.GetComponentInParent<PlayerMeleeCombat>()
                ?? components.OfType<PlayerMeleeCombat>().FirstOrDefault()
                ?? throw new BuildFailedException("The synchronized server scene must contain prototype melee combat state.");
            PlayerSpellLoadout spellLoadout = inventory.GetComponent<PlayerSpellLoadout>()
                ?? inventory.GetComponentInParent<PlayerSpellLoadout>()
                ?? components.OfType<PlayerSpellLoadout>().FirstOrDefault()
                ?? throw new BuildFailedException("The synchronized server scene must contain a prototype spell loadout.");
            PrototypePersistenceServiceBehaviour persistence = components.OfType<PrototypePersistenceServiceBehaviour>().SingleOrDefault()
                ?? throw new BuildFailedException("The synchronized server scene must contain prototype persistence state.");
            DefinitionCatalog catalog = AssetDatabase.FindAssets("t:DefinitionCatalog")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DefinitionCatalog>)
                .FirstOrDefault(value => value != null)
                ?? throw new BuildFailedException("The server project could not resolve its DefinitionCatalog.");

            server.Configure(networkManager, "0.0.0.0", 7777, 8);
            server.ConfigurePlayerActorPrefab(FindRegisteredPrefab<NetworkPlayerActor>(networkManager));
            server.ConfigureCombatWorldStatePrefab(FindRegisteredPrefab<NetworkCombatWorldState>(networkManager));
            server.ConfigureWorldItemPickupPrefab(FindRegisteredPrefab<NetworkWorldItemPickup>(networkManager));
            server.ConfigurePrototypePlayerMovement(playerController, null);
            server.ConfigurePrototypePlayerVitals(resources);
            server.ConfigurePrototypePlayerInventory(inventory, equipment, catalog);
            server.ConfigurePrototypeCombat(meleeCombat, spellLoadout);
            server.ConfigurePrototypeNarrative(persistence);

            EditorUtility.SetDirty(server);
            EditorSceneManager.MarkSceneDirty(serverScene);
            if (!EditorSceneManager.SaveScene(serverScene, scenePath, false))
            {
                throw new BuildFailedException($"Could not save the prepared server scene '{scenePath}'.");
            }

            NormalizeLocalDedicatedServerYaml(scenePath);
        }

        private static void NormalizeLocalDedicatedServerYaml(string scenePath)
        {
            const string classMarker =
                "  m_EditorClassIdentifier: UnityIsekaiGame.Networking.Server::UnityIsekaiGame.Networking.Server.LocalDedicatedServer";
            string yaml = File.ReadAllText(scenePath);
            int classIndex = yaml.IndexOf(classMarker, StringComparison.Ordinal);
            if (classIndex < 0) return;

            int nameIndex = yaml.LastIndexOf("  m_Name: ", classIndex, StringComparison.Ordinal);
            if (nameIndex < 0) return;

            int nameValueEnd = nameIndex + "  m_Name:".Length;
            yaml = yaml.Remove(nameValueEnd, 1);
            File.WriteAllText(scenePath, yaml);
        }

        private static GameObject FindRegisteredPrefab<T>(NetworkManager networkManager) where T : Component
        {
            foreach (NetworkPrefabsList list in networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists)
            {
                if (list == null) continue;
                NetworkPrefab entry = list.PrefabList.FirstOrDefault(value =>
                    value != null && value.Prefab != null && value.Prefab.GetComponent<T>() != null);
                if (entry?.Prefab != null)
                {
                    return entry.Prefab;
                }
            }

            throw new BuildFailedException($"The server NetworkManager does not register a prefab containing {typeof(T).Name}.");
        }

        public static void ValidateAuthoritativeCollision() => ValidateAuthoritativeCollision(ServerScenePath);

        private static void ValidateAuthoritativeCollision(string scenePath)
        {
            Scene previousScene = SceneManager.GetActiveScene();
            string previousScenePath = previousScene.path;
            try
            {
                Scene serverScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                TerrainCollider[] terrainColliders = serverScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TerrainCollider>(true))
                    .ToArray();
                if (terrainColliders.Length == 0)
                {
                    throw new BuildFailedException(
                        $"The authoritative server scene '{scenePath}' has no terrain colliders. " +
                        "Players would fall through the world.");
                }

                TerrainCollider[] missingTerrain = terrainColliders
                    .Where(collider => collider.terrainData == null)
                    .ToArray();
                if (missingTerrain.Length > 0)
                {
                    string names = string.Join(", ", missingTerrain.Select(collider => collider.name));
                    throw new BuildFailedException(
                        $"The authoritative server scene has {missingTerrain.Length} terrain collider(s) without TerrainData: {names}. " +
                        "Server collision cannot be built until every terrain dependency is available.");
                }

                MeshCollider[] meshColliders = serverScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MeshCollider>(true))
                    .ToArray();
                MeshCollider[] invalidMeshColliders = meshColliders
                    .Where(collider => collider.sharedMesh == null ||
                        !AssetDatabase.GetAssetPath(collider.sharedMesh).StartsWith(
                            "Packages/com.thequantifier.isekai.content/Content/World/CollisionMeshes/",
                            StringComparison.Ordinal))
                    .ToArray();
                if (invalidMeshColliders.Length > 0)
                {
                    string names = string.Join(", ", invalidMeshColliders.Select(collider => collider.name));
                    throw new BuildFailedException(
                        $"The authoritative server scene contains {invalidMeshColliders.Length} mesh collider(s) without collision-only shared meshes: {names}. " +
                        "Regenerate the scene with the dedicated-server extractor.");
                }

                Collider[] colliders = serverScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Collider>(true))
                    .ToArray();
                if (!colliders.Any(collider => collider.enabled && !collider.isTrigger))
                {
                    throw new BuildFailedException(
                        $"The authoritative server scene '{scenePath}' has no enabled solid colliders.");
                }

                Collider[] invalidColliders = colliders.Where(collider => !HasValidShape(collider)).ToArray();
                if (invalidColliders.Length > 0)
                {
                    string names = string.Join(", ", invalidColliders.Select(collider => collider.name));
                    throw new BuildFailedException(
                        $"The authoritative server scene contains {invalidColliders.Length} collider(s) with invalid dimensions or data: {names}.");
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousScenePath) &&
                    !string.Equals(previousScenePath, scenePath, StringComparison.Ordinal))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static bool HasValidShape(Collider collider)
        {
            switch (collider)
            {
                case TerrainCollider terrainCollider:
                    return terrainCollider.terrainData != null;
                case BoxCollider boxCollider:
                    return IsFinitePositive(boxCollider.size.x) &&
                        IsFinitePositive(boxCollider.size.y) &&
                        IsFinitePositive(boxCollider.size.z);
                case SphereCollider sphereCollider:
                    return IsFinitePositive(sphereCollider.radius);
                case CapsuleCollider capsuleCollider:
                    return IsFinitePositive(capsuleCollider.radius) &&
                        IsFinitePositive(capsuleCollider.height);
                case CharacterController characterController:
                    return IsFinitePositive(characterController.radius) &&
                        IsFinitePositive(characterController.height);
                case MeshCollider meshCollider:
                    return meshCollider.sharedMesh != null && meshCollider.sharedMesh.vertexCount >= 3;
                default:
                    return true;
            }
        }

        private static void ValidateSourceFingerprint()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath)
                ?? throw new BuildFailedException("The server project root could not be resolved.");
            string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
            string manifestPath = Path.GetFullPath(Path.Combine(projectRoot, ServerSceneManifestPath));
            if (!File.Exists(manifestPath))
            {
                throw new BuildFailedException(
                    $"The server scene source manifest is missing at '{ServerSceneManifestPath}'. " +
                    "Run the client project's dedicated-server scene extractor before building.");
            }

            string[] lines = File.ReadAllLines(manifestPath);
            if (lines.Length < 2 || !string.Equals(lines[0], "version=1", StringComparison.Ordinal))
                throw new BuildFailedException("The server scene source manifest is malformed or unsupported.");
            string rootPrefix = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            for (int i = 1; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('|');
                if (separator != 64) throw new BuildFailedException($"Malformed source manifest entry at line {i + 1}.");
                string expectedHash = lines[i].Substring(0, separator);
                string relativePath = lines[i].Substring(separator + 1).Replace('/', Path.DirectorySeparatorChar);
                string physicalPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
                if (!physicalPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(physicalPath)
                    || !string.Equals(expectedHash, ComputeSha256(physicalPath), StringComparison.OrdinalIgnoreCase))
                {
                    throw new BuildFailedException(
                        $"The synchronized server scene is stale because '{relativePath}' changed or is missing. " +
                        "Run the client project's dedicated-server scene extractor before building.");
                }
            }
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static void EnsureAssetFolder(string path)
        {
            string normalized = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;
            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/')
                ?? throw new BuildFailedException($"Cannot resolve parent folder for '{path}'.");
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
