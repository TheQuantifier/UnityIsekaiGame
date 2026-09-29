using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;
using UnityIsekaiGame.Networking.Server;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Player;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Equipment;

namespace UnityIsekaiGame.Editor
{
    public static class LocalNetworkFoundationAuthoring
    {
        public const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        public const string NetworkRootName = "Local Network Runtime";
        public const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        public const string PlayerActorPrefabPath = "Assets/_Project/Content/Networking/Prefabs/NetworkPlayerActor.prefab";

        [MenuItem("Tools/Unity Isekai Game/Networking/Bake Local Network Foundation")]
        public static void BakePrototypeSceneMenu() => BakePrototypeScene();

        public static void BakePrototypeScene()
        {
            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(value => value.name == NetworkRootName);
            if (root == null)
            {
                root = new GameObject(NetworkRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            NetworkManager manager = GetOrAdd<NetworkManager>(root);
            UnityTransport transport = GetOrAdd<UnityTransport>(root);
            LocalGameClient client = GetOrAdd<LocalGameClient>(root);
            LocalDedicatedServer server = GetOrAdd<LocalDedicatedServer>(root);
            LocalPlayerMovementBridge movementBridge = GetOrAdd<LocalPlayerMovementBridge>(root);
            LocalPlayerVitalsBridge vitalsBridge = GetOrAdd<LocalPlayerVitalsBridge>(root);
            LocalPlayerInventoryBridge inventoryBridge = GetOrAdd<LocalPlayerInventoryBridge>(root);
            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (prefabList == null)
            {
                throw new InvalidOperationException($"The generated network prefab list is missing at '{DefaultNetworkPrefabsPath}'.");
            }

            PlayerInputReader playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerInputReader>();
            FirstPersonCharacterMotor playerMotor = UnityEngine.Object.FindAnyObjectByType<FirstPersonCharacterMotor>();
            CharacterResourceCollection playerResources = playerMotor == null ? null : playerMotor.GetComponent<CharacterResourceCollection>();
            PlayerStamina playerStamina = playerMotor == null ? null : playerMotor.GetComponent<PlayerStamina>();
            PlayerInventory playerInventory = playerMotor == null ? null : playerMotor.GetComponent<PlayerInventory>();
            PlayerEquipment playerEquipment = playerMotor == null ? null : playerMotor.GetComponent<PlayerEquipment>();
            PrototypePersistenceServiceBehaviour persistence = UnityEngine.Object.FindAnyObjectByType<PrototypePersistenceServiceBehaviour>();
            if (playerInput == null || playerMotor == null || playerMotor.MovementSettings == null || playerResources == null || playerStamina == null
                || playerInventory == null || playerEquipment == null || persistence == null || persistence.DefinitionCatalog == null)
            {
                throw new InvalidOperationException("The Prototype Scene requires player input, movement, vitals, inventory, equipment, persistence, and a definition catalog for the network bridges.");
            }

            GameObject playerActorPrefab = EnsurePlayerActorPrefab(playerMotor.MovementSettings, playerStamina);
            foreach (NetworkPrefab entry in prefabList.PrefabList
                         .Where(entry => entry == null || entry.Prefab == null || entry.Prefab == playerActorPrefab)
                         .ToArray())
            {
                prefabList.Remove(entry);
            }

            prefabList.Add(new NetworkPrefab
            {
                Override = NetworkPrefabOverride.None,
                Prefab = playerActorPrefab
            });

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionData = Array.Empty<byte>();
            client.Configure(manager);
            server.Configure(manager);
            server.ConfigurePlayerActorPrefab(playerActorPrefab);
            server.ConfigurePlayerSpawn(playerMotor.transform.position, playerMotor.transform.eulerAngles.y);
            server.ConfigurePrototypePlayerMovement(playerMotor.GetComponent<CharacterController>(), playerMotor);
            server.ConfigurePrototypePlayerVitals(playerResources);
            server.ConfigurePrototypePlayerInventory(playerInventory, playerEquipment, persistence.DefinitionCatalog);
            movementBridge.Configure(client, playerInput, playerMotor, playerMotor.transform);
            vitalsBridge.Configure(client, playerResources, playerInput);
            inventoryBridge.Configure(client, playerInventory, playerEquipment, persistence.DefinitionCatalog);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(client);
            EditorUtility.SetDirty(server);
            EditorUtility.SetDirty(movementBridge);
            EditorUtility.SetDirty(vitalsBridge);
            EditorUtility.SetDirty(inventoryBridge);
            EditorUtility.SetDirty(prefabList);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save the local network foundation in '{PrototypeScenePath}'.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Baked the local network foundation into '{PrototypeScenePath}'.");
        }

        private static GameObject EnsurePlayerActorPrefab(UnityIsekaiGame.Configuration.PlayerMovementSettings settings, PlayerStamina playerStamina)
        {
            EnsureFolder("Assets/_Project/Content/Networking");
            EnsureFolder("Assets/_Project/Content/Networking/Prefabs");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerActorPrefabPath);
            if (prefab == null)
            {
                GameObject source = new GameObject("Network Player Actor");
                try
                {
                    source.AddComponent<NetworkObject>();
                    PrefabUtility.SaveAsPrefabAsset(source, PlayerActorPrefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerActorPrefabPath);
            try
            {
                NetworkObject networkObject = GetOrAdd<NetworkObject>(contents);
                GetOrAdd<NetworkPlayerActor>(contents);
                NetworkPlayerMovement movement = GetOrAdd<NetworkPlayerMovement>(contents);
                NetworkPlayerVitals vitals = GetOrAdd<NetworkPlayerVitals>(contents);
                GetOrAdd<NetworkPlayerInventory>(contents);
                NetworkTransform networkTransform = GetOrAdd<NetworkTransform>(contents);
                CharacterController controller = GetOrAdd<CharacterController>(contents);
                controller.height = 2f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0f, 1f, 0f);
                controller.slopeLimit = 50f;
                controller.stepOffset = 0.35f;
                controller.skinWidth = 0.08f;
                networkTransform.Interpolate = true;
                networkTransform.InLocalSpace = false;
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;
                movement.ConfigureTuning(
                    settings.WalkSpeed,
                    settings.SprintSpeedMultiplier,
                    settings.Acceleration,
                    settings.Deceleration,
                    settings.JumpHeight,
                    settings.Gravity,
                    settings.GroundedStickForce);
                ResourceDefinition healthDefinition = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/_Project/Content/Characters/Resources/HealthResource.asset");
                ResourceDefinition staminaDefinition = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/_Project/Content/Characters/Resources/StaminaResource.asset");
                ResourceDefinition manaDefinition = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/_Project/Content/Characters/Resources/ManaResource.asset");
                if (healthDefinition == null || staminaDefinition == null || manaDefinition == null)
                {
                    throw new InvalidOperationException("Health, Stamina, and Mana resource definitions are required to bake authoritative network vitals.");
                }

                vitals.ConfigureFallbackTuning(
                    healthDefinition.RegenerationPerSecond,
                    playerStamina.SprintDrainPerSecond,
                    playerStamina.SprintRestartThreshold,
                    staminaDefinition.RegenerationPerSecond,
                    manaDefinition.RegenerationPerSecond,
                    staminaDefinition.RegenerationDelayAfterSpend,
                    manaDefinition.RegenerationDelayAfterSpend);
                NormalizeNetworkPrefab(networkObject);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerActorPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerActorPrefabPath);
            return prefab == null ? throw new InvalidOperationException($"Failed to create the player actor prefab at '{PlayerActorPrefabPath}'.") : prefab;
        }

        private static void NormalizeNetworkPrefab(NetworkObject networkObject)
        {
            SerializedObject serializedNetworkObject = new SerializedObject(networkObject);
            SerializedProperty inScenePlaced = serializedNetworkObject.FindProperty("m_InScenePlaced");
            if (inScenePlaced != null && inScenePlaced.boolValue)
            {
                inScenePlaced.boolValue = false;
                serializedNetworkObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(networkObject);
            }
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

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component == null ? target.AddComponent<T>() : component;
        }
    }
}
