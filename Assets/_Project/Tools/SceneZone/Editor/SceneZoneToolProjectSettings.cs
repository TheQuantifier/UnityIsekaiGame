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

        private static string ActiveSceneKey()
        {
            Scene scene = SceneManager.GetActiveScene();
            string path = scene.path ?? string.Empty;
            string guid = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrWhiteSpace(guid)) return $"guid:{guid}";
            if (!string.IsNullOrWhiteSpace(path)) return $"path:{path}";
            return $"unsaved:{scene.name}";
        }
    }
}
