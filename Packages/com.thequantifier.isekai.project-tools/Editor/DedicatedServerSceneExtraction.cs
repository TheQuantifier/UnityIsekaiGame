using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Player;

namespace UnityIsekaiGame.Editor
{
    /// <summary>
    /// Produces the asset-light authoritative scene used by the physically separate server project.
    /// It keeps simulation state, interaction bindings, primitive collision, and server networking,
    /// while removing every client adapter and presentation component.
    /// </summary>
    public static class DedicatedServerSceneExtraction
    {
        public const string SourceScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";
        public const string OutputScenePath = "Assets/_Project/Scenes/Server/ServerPrototypeScene.unity";

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

            int removedComponents = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true).Reverse())
                {
                    if (component != null && ShouldRemove(component))
                    {
                        UnityEngine.Object.DestroyImmediate(component);
                        removedComponents++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OutputScenePath, false))
            {
                throw new InvalidOperationException($"Could not save the stripped server scene '{OutputScenePath}'.");
            }

            AssetDatabase.SaveAssets();
            string[] projectDependencies = AssetDatabase.GetDependencies(OutputScenePath, true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !string.Equals(path, OutputScenePath, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Debug.Log(
                $"Extracted dedicated-server scene '{OutputScenePath}'. Unpacked {unpackedPrefabs} prefab instance(s) and " +
                $"removed {removedComponents} presentation component(s). " +
                $"Remaining project-owned dependencies ({projectDependencies.Length}):\n{string.Join("\n", projectDependencies)}");
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
                component is TerrainCollider ||
                component is MeshCollider ||
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
                component is Selectable ||
                component is PlayerInputReader ||
                component is FirstPersonCharacterMotor;
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
