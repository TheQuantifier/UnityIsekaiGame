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
        private const string UseEquipIconPath = "Assets/_Project/Art/UI/InventoryActions/action-use-equip.png";
        private const string UnequipIconPath = "Assets/_Project/Art/UI/InventoryActions/action-unequip.png";
        private const string DropIconPath = "Assets/_Project/Art/UI/InventoryActions/action-drop.png";
        private const string ConsumeFoodIconPath = "Assets/_Project/Art/UI/InventoryActions/action-consume-food.png";
        private const string ConsumePotionIconPath = "Assets/_Project/Art/UI/InventoryActions/action-consume-potion.png";

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

            Sprite useEquipIcon = LoadActionIcon(UseEquipIconPath);
            Sprite unequipIcon = LoadActionIcon(UnequipIconPath);
            Sprite dropIcon = LoadActionIcon(DropIconPath);
            Sprite consumeFoodIcon = LoadActionIcon(ConsumeFoodIconPath);
            Sprite consumePotionIcon = LoadActionIcon(ConsumePotionIconPath);
            foreach (InventoryScreenView view in views)
            {
                view.ConfigureInventoryActionIcons(useEquipIcon, unequipIcon, dropIcon, consumeFoodIcon, consumePotionIcon);
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

        private static Sprite LoadActionIcon(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new System.InvalidOperationException($"Inventory action icon is missing or is not a texture: '{assetPath}'.");
            }

            bool requiresReimport = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.alphaIsTransparency
                || importer.mipmapEnabled
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.maxTextureSize != 256;
            if (requiresReimport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new System.InvalidOperationException($"Inventory action icon could not be loaded as a Sprite: '{assetPath}'.");
            }

            return sprite;
        }
    }
}
