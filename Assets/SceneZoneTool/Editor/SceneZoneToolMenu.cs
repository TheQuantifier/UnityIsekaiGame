using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneZoneTool.Editor
{
    internal static class SceneZoneToolMenu
    {
        [MenuItem("Tools/Scene Zone Tools/Validate Project", false, 220)]
        internal static void ValidateProject()
        {
            string[] guids = AssetDatabase.FindAssets("t:SceneZoneLayerAsset");
            int invalid = 0;
            int repaired = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SceneZoneLayerAsset layer = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(path);
                repaired += RepairMissingOrDuplicateIds(layer, path);
                string failure = "Asset could not be loaded.";
                if (layer != null && layer.Validate(out failure)) continue;
                invalid++;
                Debug.LogError($"Scene Zone Tool: invalid layer at '{path}': {failure}", layer);
            }

            AssetDatabase.SaveAssets();
            string repairSummary = repaired > 0 ? $" Repaired {repaired} missing or duplicate identity field(s)." : string.Empty;
            if (invalid == 0)
                EditorUtility.DisplayDialog("Scene Zone Tool Validation", $"Validated {guids.Length} layer asset(s).{repairSummary} No remaining problems were found.", "OK");
            else
                EditorUtility.DisplayDialog("Scene Zone Tool Validation", $"{repairSummary} Found {invalid} layer asset(s) with remaining problems. See the Console for details.", "OK");
        }

        [MenuItem("Tools/Scene Zone Tools/Project Settings", false, 221)]
        internal static void OpenSettings() => SettingsService.OpenProjectSettings("Project/Scene Zone Tool");

        [MenuItem("Tools/Scene Zone Tools/Documentation", false, 240)]
        internal static void OpenDocumentation()
        {
            string guid = AssetDatabase.FindAssets("README t:TextAsset", new[] { "Assets/SceneZoneTool/Documentation" }).FirstOrDefault();
            UnityEngine.Object document = string.IsNullOrWhiteSpace(guid) ? null : AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (document != null) AssetDatabase.OpenAsset(document);
            else EditorUtility.DisplayDialog("Scene Zone Tool", "README.md was not found under Assets/SceneZoneTool/Documentation.", "OK");
        }

        [MenuItem("Tools/Scene Zone Tools/About", false, 241)]
        private static void About() => SceneZoneAboutWindow.ShowWindow();

        internal static int RepairMissingOrDuplicateIds(SceneZoneLayerAsset layer, string assetPath)
        {
            if (layer == null) return 0;
            int repaired = 0;
            string layerId = layer.LayerId;
            if (string.IsNullOrWhiteSpace(layerId))
            {
                layerId = $"zone-layer.{ToIdSegment(System.IO.Path.GetFileNameWithoutExtension(assetPath))}";
                SerializedObject serializedLayer = new SerializedObject(layer);
                serializedLayer.FindProperty("layerId").stringValue = layerId;
                serializedLayer.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(layer);
                repaired++;
            }

            string layerSegment = ToIdSegment(layerId.Replace("zone-layer.", string.Empty, StringComparison.OrdinalIgnoreCase));
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SceneZoneAsset zone in layer.Zones.Where(candidate => candidate != null))
            {
                string currentName = zone.DisplayName?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(currentName) && usedNames.Add(currentName)) continue;

                string baseName = string.IsNullOrWhiteSpace(currentName) ? "Zone" : currentName;
                string candidateName = baseName;
                int suffix = 2;
                while (!usedNames.Add(candidateName)) candidateName = $"{baseName} ({suffix++})";
                SerializedObject serializedZone = new SerializedObject(zone);
                serializedZone.FindProperty("displayName").stringValue = candidateName;
                serializedZone.ApplyModifiedPropertiesWithoutUndo();
                zone.name = candidateName;
                EditorUtility.SetDirty(zone);
                repaired++;
            }

            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            foreach (SceneZoneAsset zone in layer.Zones.Where(candidate => candidate != null))
            {
                string current = zone.ZoneId?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(current) && used.Add(current)) continue;

                string baseId = $"zone.{layerSegment}.{ToIdSegment(zone.DisplayName)}";
                string candidateId = baseId;
                int suffix = 2;
                while (!used.Add(candidateId)) candidateId = $"{baseId}.{suffix++}";
                SerializedObject serializedZone = new SerializedObject(zone);
                serializedZone.FindProperty("zoneId").stringValue = candidateId;
                serializedZone.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(zone);
                repaired++;
            }

            if (repaired > 0)
                Debug.Log($"Scene Zone Tool: repaired {repaired} missing or duplicate identity field(s) in '{assetPath}'.", layer);
            return repaired;
        }

        private static string ToIdSegment(string value)
        {
            string normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '.')
                .ToArray());
            while (normalized.Contains("..", StringComparison.Ordinal)) normalized = normalized.Replace("..", ".", StringComparison.Ordinal);
            normalized = normalized.Trim('.');
            return string.IsNullOrWhiteSpace(normalized) ? "unnamed" : normalized;
        }
    }
}
