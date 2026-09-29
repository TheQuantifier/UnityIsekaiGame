using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Networking;
using UnityIsekaiGame.Networking.Client;
using UnityIsekaiGame.Networking.Server;

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
            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (prefabList == null)
            {
                throw new InvalidOperationException($"The generated network prefab list is missing at '{DefaultNetworkPrefabsPath}'.");
            }

            GameObject playerActorPrefab = EnsurePlayerActorPrefab();
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

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(client);
            EditorUtility.SetDirty(server);
            EditorUtility.SetDirty(prefabList);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save the local network foundation in '{PrototypeScenePath}'.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Baked the local network foundation into '{PrototypeScenePath}'.");
        }

        private static GameObject EnsurePlayerActorPrefab()
        {
            EnsureFolder("Assets/_Project/Content/Networking");
            EnsureFolder("Assets/_Project/Content/Networking/Prefabs");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerActorPrefabPath);
            if (prefab != null)
            {
                if (prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<NetworkPlayerActor>() == null)
                {
                    throw new InvalidOperationException($"The player actor prefab at '{PlayerActorPrefabPath}' is missing its networking components.");
                }

                NormalizeNetworkPrefab(prefab);
                return prefab;
            }

            GameObject source = new GameObject("Network Player Actor");
            try
            {
                source.AddComponent<NetworkObject>();
                source.AddComponent<NetworkPlayerActor>();
                prefab = PrefabUtility.SaveAsPrefabAsset(source, PlayerActorPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            if (prefab != null)
            {
                NormalizeNetworkPrefab(prefab);
            }

            return prefab == null
                ? throw new InvalidOperationException($"Failed to create the player actor prefab at '{PlayerActorPrefabPath}'.")
                : prefab;
        }

        private static void NormalizeNetworkPrefab(GameObject prefab)
        {
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
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
