using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneZoneTool.Editor
{
    [CustomEditor(typeof(SceneZoneAsset))]
    public sealed class SceneZoneAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("zoneId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
                EditorGUILayout.ObjectField("Zone Layer", ((SceneZoneAsset)target).Layer, typeof(SceneZoneLayerAsset), false);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shape"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("hasCenterPoint"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("centerPoint"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("circleCenter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("circleRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("polygonPoints"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dataVersion"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("notes"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lineColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lineWidth"));
            serializedObject.ApplyModifiedProperties();

            SceneZoneAsset zone = (SceneZoneAsset)target;
            if (!zone.Validate(out string failure)) EditorGUILayout.HelpBox(failure, MessageType.Error);
            EditorGUILayout.Space();
            if (GUILayout.Button("Use In Scene Zone Overlay")) SceneZoneOverlay.Open(zone.Layer);
        }
    }

    [CustomEditor(typeof(SceneZoneLayerAsset))]
    public sealed class SceneZoneLayerAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("layerId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneGuid"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("scenePath"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneName"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("preventOverlap"));
            serializedObject.ApplyModifiedProperties();

            SceneZoneLayerAsset layer = (SceneZoneLayerAsset)target;
            if (!layer.Validate(out string failure)) EditorGUILayout.HelpBox(failure, MessageType.Error);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Zones ({layer.Zones.Count})", EditorStyles.boldLabel);
            foreach (SceneZoneAsset zone in layer.Zones.Where(value => value != null))
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(zone.DisplayName, zone, typeof(SceneZoneAsset), false);

            string assetPath = AssetDatabase.GetAssetPath(layer);
            SceneZoneAsset[] embeddedZones = string.IsNullOrWhiteSpace(assetPath)
                ? Array.Empty<SceneZoneAsset>()
                : AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<SceneZoneAsset>()
                    .Where(zone => zone != null && zone.Layer == layer)
                    .OrderBy(zone => zone.ZoneId, StringComparer.Ordinal)
                    .ToArray();
            bool listNeedsRepair = embeddedZones.Length != layer.Zones.Count || embeddedZones.Any(zone => !layer.Zones.Contains(zone));
            if (listNeedsRepair)
            {
                EditorGUILayout.HelpBox("The layer's zone list does not match its embedded zone sub-assets.", MessageType.Warning);
                if (GUILayout.Button("Repair Embedded Zone List")) Repair(layer, embeddedZones);
            }
            if (GUILayout.Button("Use In Scene Zone Overlay")) SceneZoneOverlay.Open(layer);
        }

        private static void Repair(SceneZoneLayerAsset layer, SceneZoneAsset[] embeddedZones)
        {
            Undo.RecordObject(layer, "Repair Embedded Zone List");
            SerializedObject layerObject = new SerializedObject(layer);
            SerializedProperty zonesProperty = layerObject.FindProperty("zones");
            zonesProperty.arraySize = embeddedZones.Length;
            for (int i = 0; i < embeddedZones.Length; i++)
                zonesProperty.GetArrayElementAtIndex(i).objectReferenceValue = embeddedZones[i];
            layerObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssetIfDirty(layer);
        }
    }
}
