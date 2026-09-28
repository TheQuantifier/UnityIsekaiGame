using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.Editor
{
    public static class PrototypeInventoryUiAuthoring
    {
        private const string PrototypeScenePath = "Assets/_Project/Scenes/Prototype/PrototypeScene.unity";

        [MenuItem("Tools/Prototype/UI/Bake Inventory Menu Layout")]
        public static void BakePrototypeSceneMenu()
        {
            BakePrototypeScene();
        }

        public static void BakePrototypeScene()
        {
            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            InventoryScreenView[] views = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<InventoryScreenView>(true))
                .ToArray();

            if (views.Length == 0)
            {
                throw new System.InvalidOperationException($"No {nameof(InventoryScreenView)} was found in '{PrototypeScenePath}'.");
            }

            foreach (InventoryScreenView view in views)
            {
                view.BakeAuthoringLayout();
                EditorUtility.SetDirty(view);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new System.InvalidOperationException($"Failed to save the baked inventory UI in '{PrototypeScenePath}'.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Baked {views.Length} inventory menu layout(s) into '{PrototypeScenePath}'.");
        }
    }
}
