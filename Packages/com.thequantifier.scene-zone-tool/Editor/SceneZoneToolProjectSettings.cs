using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneZoneTool.Editor
{
    [FilePath("ProjectSettings/SceneZoneToolSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SceneZoneToolProjectSettings : ScriptableSingleton<SceneZoneToolProjectSettings>
    {
        [Serializable]
        private sealed class SceneLayerSelection
        {
            public string sceneKey;
            public string sceneGuid;
            public string scenePath;
            public string sceneName;
            public string layerAssetGuid;
        }

        [SerializeField] private List<SceneLayerSelection> sceneLayers = new List<SceneLayerSelection>();
        [SerializeField] private string defaultSaveFolder = "Assets/SceneZones";
        [SerializeField] private float defaultDisplayHeight;
        [SerializeField] private bool allowDragDrawing = true;
        [SerializeField, Min(0.25f)] private float dragPointSpacing = 3f;

        internal string DefaultSaveFolder => NormalizeAssetFolder(defaultSaveFolder);
        internal float DefaultDisplayHeight => defaultDisplayHeight;
        internal bool AllowDragDrawing => allowDragDrawing;
        internal float DragPointSpacing => Mathf.Max(0.25f, dragPointSpacing);

        internal SceneZoneLayerAsset LoadLayerForActiveScene()
        {
            string key = ActiveSceneKey();
            SceneLayerSelection selection = sceneLayers.FirstOrDefault(value => value != null && string.Equals(value.sceneKey, key, StringComparison.Ordinal));
            if (selection == null || string.IsNullOrWhiteSpace(selection.layerAssetGuid)) return null;
            string layerPath = AssetDatabase.GUIDToAssetPath(selection.layerAssetGuid);
            SceneZoneLayerAsset layer = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(layerPath);
            if (layer != null) return layer;
            sceneLayers.Remove(selection);
            Save(true);
            return null;
        }

        internal SceneZoneLayerAsset LoadOrDiscoverLayerForActiveScene()
        {
            SceneZoneLayerAsset preferred = LoadLayerForActiveScene();
            if (preferred != null) return preferred;

            Scene scene = SceneManager.GetActiveScene();
            string scenePath = scene.path ?? string.Empty;
            string sceneGuid = string.IsNullOrWhiteSpace(scenePath) ? string.Empty : AssetDatabase.AssetPathToGUID(scenePath);
            SceneZoneLayerAsset[] matches = AssetDatabase.FindAssets("t:SceneZoneLayerAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>)
                .Where(layer => LayerMatchesScene(layer, sceneGuid, scenePath, scene.name))
                .Take(2)
                .ToArray();
            if (matches.Length != 1) return null;

            SetLayerForActiveScene(matches[0]);
            return matches[0];
        }

        internal void SetLayerForActiveScene(SceneZoneLayerAsset layer)
        {
            if (layer == null) return;
            string layerPath = AssetDatabase.GetAssetPath(layer);
            if (string.IsNullOrWhiteSpace(layerPath)) return;
            string layerGuid = AssetDatabase.AssetPathToGUID(layerPath);
            if (string.IsNullOrWhiteSpace(layerGuid)) return;
            Scene scene = SceneManager.GetActiveScene();
            string scenePath = scene.path ?? string.Empty;
            string sceneGuid = string.IsNullOrWhiteSpace(scenePath) ? string.Empty : AssetDatabase.AssetPathToGUID(scenePath);
            string key = ActiveSceneKey();
            SceneLayerSelection selection = sceneLayers.FirstOrDefault(value => value != null && string.Equals(value.sceneKey, key, StringComparison.Ordinal));
            if (selection == null)
            {
                selection = new SceneLayerSelection();
                sceneLayers.Add(selection);
            }
            selection.sceneKey = key;
            selection.sceneGuid = sceneGuid;
            selection.scenePath = scenePath;
            selection.sceneName = scene.name ?? string.Empty;
            selection.layerAssetGuid = layerGuid;
            Save(true);
        }

        internal void RemoveLayer(string layerAssetGuid)
        {
            if (string.IsNullOrWhiteSpace(layerAssetGuid)) return;
            int removed = sceneLayers.RemoveAll(value => value != null && string.Equals(value.layerAssetGuid, layerAssetGuid, StringComparison.Ordinal));
            if (removed > 0) Save(true);
        }

        internal void SetAuthoringDefaults(string folder, float height, bool allowDrag, float spacing)
        {
            defaultSaveFolder = NormalizeAssetFolder(folder);
            defaultDisplayHeight = height;
            allowDragDrawing = allowDrag;
            dragPointSpacing = Mathf.Max(0.25f, spacing);
            Save(true);
        }

        internal void RepairMissingReferences()
        {
            int removed = sceneLayers.RemoveAll(value => value == null
                || string.IsNullOrWhiteSpace(value.layerAssetGuid)
                || string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(value.layerAssetGuid)));
            if (removed > 0) Save(true);
        }

        private static string NormalizeAssetFolder(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "Assets/SceneZones" : value.Trim().Replace('\\', '/').TrimEnd('/');
            return normalized == "Assets" || normalized.StartsWith("Assets/", StringComparison.Ordinal)
                ? normalized
                : "Assets/SceneZones";
        }

        private static string ActiveSceneKey()
        {
            Scene scene = SceneManager.GetActiveScene();
            string path = scene.path ?? string.Empty;
            string guid = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrWhiteSpace(guid)) return $"guid:{guid}";
            if (!string.IsNullOrWhiteSpace(path)) return $"path:{path}";
            return $"unsaved:{scene.name}";
        }

        private static bool LayerMatchesScene(SceneZoneLayerAsset layer, string sceneGuid, string scenePath, string sceneName)
        {
            if (layer == null || !layer.HasSceneBinding) return false;
            if (!string.IsNullOrWhiteSpace(layer.SceneGuid) && !string.IsNullOrWhiteSpace(sceneGuid))
                return string.Equals(layer.SceneGuid, sceneGuid, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(layer.ScenePath) && !string.IsNullOrWhiteSpace(scenePath))
                return string.Equals(layer.ScenePath, scenePath, StringComparison.Ordinal);
            return string.Equals(layer.SceneName, sceneName ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
