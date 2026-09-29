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

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionData = Array.Empty<byte>();
            client.Configure(manager);
            server.Configure(manager);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(client);
            EditorUtility.SetDirty(server);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Failed to save the local network foundation in '{PrototypeScenePath}'.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Baked the local network foundation into '{PrototypeScenePath}'.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component == null ? target.AddComponent<T>() : component;
        }
    }
}
