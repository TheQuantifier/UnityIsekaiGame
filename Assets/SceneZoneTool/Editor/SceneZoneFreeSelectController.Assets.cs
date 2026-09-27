using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SceneZoneTool.Editor
{
    internal sealed partial class SceneZoneFreeSelectController
    {
        internal void BeginNewZone(SceneZoneShape shape)
        {
            if (zoneLayer == null) { ShowNotification(new GUIContent("Create or select a zone layer first.")); return; }
            if (!LayerMatchesActiveScene(zoneLayer)) { ShowNotification(new GUIContent("The selected layer belongs to another scene.")); return; }
            Tools.current = Tool.None;
            DiscardIncompleteActiveZone();
            int ordinal = 1;
            HashSet<string> names = layerZones.Where(value => value != null).Select(value => value.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string name;
            do name = $"Zone {ordinal++}"; while (names.Contains(name));
            string layerSuffix = Sanitize(zoneLayer.LayerId.Replace("zone-layer.", string.Empty, StringComparison.Ordinal));
            SceneZoneAsset created = CreateInstance<SceneZoneAsset>();
            created.name = name;
            created.hideFlags = HideFlags.HideAndDontSave;
            created.Configure($"zone.{layerSuffix}.{Sanitize(name)}", name, shape, radius: shape == SceneZoneShape.Circle ? 0f : 25f, authoredLayer: zoneLayer);
            created.AssignFillColor(CreateUniqueFillColor());
            zone = created;
            pendingNewZone = true;
            replacingCircle = false;
            blockedBy = null;
            ClearOverlapError();
            if (shape == SceneZoneShape.Polygon)
            {
                paintMode = SceneZonePaintMode.PolygonPoints;
                BeginDrawing(clearExisting: true);
            }
            else
            {
                paintMode = SceneZonePaintMode.CenterPoint;
            }
        }

        internal void SelectZone(SceneZoneAsset selected)
        {
            if (drawing) CancelDrawing();
            else if (pendingNewZone) CancelPendingZone();
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            replacingCircle = false;
            zone = selected;
            bool layerChanged = selected != null && selected.Layer != zoneLayer;
            if (selected != null) zoneLayer = selected.Layer;
            if (selected != null) hiddenZones.Remove(selected);
            blockedBy = null;
            ClearOverlapError();
            if (layerChanged) RefreshLayerZones();
            Repaint();
            SceneView.RepaintAll();
        }

        private void SelectLayer(SceneZoneLayerAsset selected)
        {
            if (drawing) CancelDrawing();
            else if (pendingNewZone) CancelPendingZone();
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            replacingCircle = false;
            zoneLayer = selected;
            blockedBy = null;
            ClearOverlapError();
            RefreshLayerZones();
            zone = layerZones.FirstOrDefault();
            if (zone != null) hiddenZones.Remove(zone);
            if (selected != null)
            {
                BindLayerToActiveScene(selected, force: false);
                if (LayerMatchesActiveScene(selected)) SceneZoneToolProjectSettings.instance.SetLayerForActiveScene(selected);
            }
            Repaint();
            SceneView.RepaintAll();
        }

        internal void UseLayer(SceneZoneLayerAsset selected) => SelectLayer(selected);

        internal void SelectPaintMode(SceneZonePaintMode selected)
        {
            Tools.current = Tool.None;
            if (paintMode == selected)
            {
                ReleaseActiveMouseInteraction();
                SceneView.RepaintAll();
                return;
            }
            if (drawing) CancelDrawing();
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            replacingCircle = false;
            paintMode = selected;
            blockedBy = null;
            ClearOverlapError();
            Repaint();
            SceneView.RepaintAll();
        }

        private void CreateLayer() => PromptCreateLayer();

        internal bool PromptCreateLayer()
        {
            string defaultFolder = SceneZoneToolProjectSettings.instance.DefaultSaveFolder;
            EnsureFolder(defaultFolder);
            string path = EditorUtility.SaveFilePanelInProject("Create Zone Layer", "zones", "asset", "Choose where to save the reusable zone layer.", defaultFolder);
            if (string.IsNullOrWhiteSpace(path)) return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            SceneZoneLayerAsset created = CreateInstance<SceneZoneLayerAsset>();
            created.Configure($"zone-layer.{Sanitize(name)}", name, new Color(1f, 0.7f, 0.15f, 0.8f));
            AssetDatabase.CreateAsset(created, path);
            BindLayerToActiveScene(created, force: true);
            AssetDatabase.SaveAssets();
            SelectLayer(created);
            return true;
        }

        internal bool PromptSelectExistingLayer()
        {
            string absolutePath = EditorUtility.OpenFilePanel("Select Zone Layer Data File", Application.dataPath, "asset");
            if (string.IsNullOrWhiteSpace(absolutePath)) return false;

            string assetsRoot = System.IO.Path.GetFullPath(Application.dataPath).TrimEnd('\\', '/');
            string selectedPath = System.IO.Path.GetFullPath(absolutePath);
            string requiredPrefix = assetsRoot + System.IO.Path.DirectorySeparatorChar;
            if (!selectedPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid Zone Data File", "Choose a SceneZoneLayerAsset stored inside this project's Assets folder.", "OK");
                return false;
            }

            string assetPath = ("Assets" + selectedPath.Substring(assetsRoot.Length)).Replace('\\', '/');
            SceneZoneLayerAsset selected = AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>(assetPath);
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Invalid Zone Data File", "The selected .asset file is not a Scene Zone layer.", "OK");
                return false;
            }

            if (selected.HasSceneBinding && !LayerMatchesActiveScene(selected))
            {
                if (!EditorUtility.DisplayDialog(
                        "Zone Layer Belongs To Another Scene",
                        $"'{selected.DisplayName}' is linked to scene '{selected.SceneName}'. Rebind it to the active scene?",
                        "Rebind",
                        "Cancel")) return false;
                BindLayerToActiveScene(selected, force: true);
            }

            SelectLayer(selected);
            return true;
        }

        private void DeleteSelectedZone()
        {
            if (zone == null) return;
            if (pendingNewZone) { CancelPendingZone(); return; }
            string dependentWarning = FormatDependencyWarning(new[] { zone });
            if (!EditorUtility.DisplayDialog("Delete Zone", $"Delete zone '{zone.DisplayName}' from layer '{zoneLayer.DisplayName}'? This can be undone until Unity closes.{dependentWarning}", "Delete", "Cancel")) return;
            DeleteZoneInternal(zone);
        }

        private void DeleteSelectedLayer()
        {
            if (zoneLayer == null) return;
            string path = AssetDatabase.GetAssetPath(zoneLayer);
            if (string.IsNullOrWhiteSpace(path)) return;
            string layerGuid = AssetDatabase.AssetPathToGUID(path);
            string dependentWarning = FormatDependencyWarning(new UnityEngine.Object[] { zoneLayer }.Concat(layerZones));
            if (!EditorUtility.DisplayDialog(
                    "Delete Zone Layer",
                    $"Move layer '{zoneLayer.DisplayName}' and all {layerZones.Count} zone(s) to the operating system's Recycle Bin?{dependentWarning}",
                    "Move To Recycle Bin",
                    "Cancel")) return;

            if (!AssetDatabase.MoveAssetToTrash(path))
            {
                EditorUtility.DisplayDialog("Could Not Delete Layer", $"Unity could not move '{path}' to the Recycle Bin.", "OK");
                return;
            }

            drawing = false;
            definingCircleRadius = false;
            replacingCircle = false;
            draftPoints.Clear();
            zone = null;
            zoneLayer = null;
            layerZones.Clear();
            ClearOverlapError();
            SceneZoneToolProjectSettings.instance.RemoveLayer(layerGuid);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Repaint();
        }

        private void DiscardIncompleteActiveZone()
        {
            if (pendingNewZone) CancelPendingZone();
        }

        private void PersistPendingZone()
        {
            if (!pendingNewZone || zone == null || zoneLayer == null) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Create {zone.Shape} Zone");
            zone.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(zone, zoneLayer);
            Undo.RegisterCreatedObjectUndo(zone, $"Create {zone.Shape} Zone");
            pendingNewZone = false;
            SetLayerZones(layerZones.Append(zone));
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            RefreshLayerZones();
        }

        private void CancelPendingZone()
        {
            if (!pendingNewZone) return;
            SceneZoneAsset pending = zone;
            pendingNewZone = false;
            drawing = false;
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            replacingCircle = false;
            draftPoints.Clear();
            zone = layerZones.FirstOrDefault();
            if (pending != null) DestroyImmediate(pending);
            ClearOverlapError();
        }

        internal void DeleteZoneInternal(SceneZoneAsset deletedZone)
        {
            if (deletedZone == null) return;
            if (pendingNewZone && deletedZone == zone) { CancelPendingZone(); return; }
            SceneZoneLayerAsset owner = deletedZone.Layer ?? zoneLayer;
            if (owner != null)
            {
                SceneZoneAsset[] remaining = owner.Zones.Where(value => value != null && value != deletedZone).ToArray();
                SceneZoneLayerAsset previousLayer = zoneLayer;
                zoneLayer = owner;
                SetLayerZones(remaining);
                zoneLayer = previousLayer;
            }
            zoneRenameDrafts.Remove(deletedZone);
            Undo.DestroyObjectImmediate(deletedZone);
            if (owner != null) EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();
            zoneLayer = owner;
            RefreshLayerZones();
            zone = layerZones.FirstOrDefault();
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
        }

        private static string FormatDependencyWarning(IEnumerable<UnityEngine.Object> targets)
        {
            HashSet<(string Guid, long LocalId)> identities = new HashSet<(string Guid, long LocalId)>();
            HashSet<string> targetPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityEngine.Object target in targets ?? Array.Empty<UnityEngine.Object>())
            {
                if (target == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long localId)) continue;
                identities.Add((guid, localId));
                string path = AssetDatabase.GetAssetPath(target);
                if (!string.IsNullOrWhiteSpace(path)) targetPaths.Add(path);
            }
            if (identities.Count == 0) return string.Empty;

            // Read serialized YAML as plain text instead of asking AssetDatabase.GetDependencies
            // about every asset. The latter imports unrelated content and can emit warnings merely
            // because an old third-party asset was inspected during a delete confirmation.
            string[] searchableExtensions =
            {
                ".asset", ".prefab", ".unity", ".mat", ".controller", ".overridecontroller",
                ".anim", ".playable", ".preset", ".rendertexture"
            };
            List<string> dependents = new List<string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || targetPaths.Contains(path)) continue;
                if (!searchableExtensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) continue;
                if (!SerializedTextReferences(path, identities)) continue;
                dependents.Add(path);
                if (dependents.Count >= 6) break;
            }

            if (dependents.Count == 0) return "\n\nNo serialized asset references were found.";
            string listed = string.Join("\n", dependents.Take(5).Select(path => $"- {path}"));
            string remainder = dependents.Count > 5 ? "\n- Additional references exist." : string.Empty;
            return $"\n\nThe following assets reference this selection and will need to be reassigned:\n{listed}{remainder}";
        }

        private static bool SerializedTextReferences(string assetPath, IReadOnlyCollection<(string Guid, long LocalId)> identities)
        {
            try
            {
                using System.IO.StreamReader reader = new System.IO.StreamReader(assetPath);
                while (reader.ReadLine() is { } line)
                {
                    foreach ((string guid, long localId) in identities)
                    {
                        if (line.IndexOf($"guid: {guid}", StringComparison.Ordinal) < 0) continue;
                        if (line.IndexOf($"fileID: {localId}", StringComparison.Ordinal) >= 0) return true;
                    }
                }
            }
            catch (System.IO.IOException)
            {
                // A locked or non-text asset is simply omitted from the advisory list. Deletion
                // still requires explicit confirmation and never depends on this best-effort scan.
            }
            catch (UnauthorizedAccessException)
            {
            }
            return false;
        }

        private static void BindLayerToActiveScene(SceneZoneLayerAsset layer, bool force)
        {
            if (layer == null || (layer.HasSceneBinding && !force)) return;
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string path = scene.path ?? string.Empty;
            string guid = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            layer.BindToScene(guid, path, scene.name);
            EditorUtility.SetDirty(layer);
            if (AssetDatabase.Contains(layer))
            {
                AssetDatabase.SaveAssetIfDirty(layer);
                SceneZoneToolProjectSettings.instance.SetLayerForActiveScene(layer);
            }
        }

        private static bool LayerMatchesActiveScene(SceneZoneLayerAsset layer)
        {
            if (layer == null || !layer.HasSceneBinding) return true;
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string path = scene.path ?? string.Empty;
            string guid = string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrWhiteSpace(layer.SceneGuid) && !string.IsNullOrWhiteSpace(guid))
                return string.Equals(layer.SceneGuid, guid, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(layer.ScenePath) && !string.IsNullOrWhiteSpace(path))
                return string.Equals(layer.ScenePath, path, StringComparison.Ordinal);
            return string.Equals(layer.SceneName, scene.name, StringComparison.Ordinal);
        }

        private void RefreshLayerZones()
        {
            layerZones.Clear();
            if (zoneLayer != null)
            {
                layerZones.AddRange(zoneLayer.Zones.Where(value => value != null && value.Layer == zoneLayer));
                layerZones.Sort((left, right) => string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal));
                WarnAboutIncompleteZones();
                NormalizeUniqueFillColors();
            }
            RefreshSceneLayers();
        }

        private void OnUndoRedoPerformed()
        {
            RefreshLayerZones();
            if (zone == null || !layerZones.Contains(zone)) zone = layerZones.FirstOrDefault();
            if (zone != null) hiddenZones.Remove(zone);
            pendingNewZone = false;
            drawing = false;
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            replacingCircle = false;
            draftPoints.Clear();
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
        }

        private void WarnAboutIncompleteZones()
        {
            if (zoneLayer == null) return;
            string layerPath = AssetDatabase.GetAssetPath(zoneLayer);
            foreach (SceneZoneAsset incompleteZone in layerZones.Where(candidate => candidate != null && candidate.IsIncomplete))
            {
                string warningKey;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(incompleteZone, out string guid, out long localId))
                    warningKey = $"{guid}:{localId}";
                else
                    warningKey = $"instance:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(incompleteZone)}";
                if (!warnedIncompleteZones.Add(warningKey)) continue;

                Debug.LogWarning(
                    $"Scene Zone Tool: incomplete zone '{incompleteZone.DisplayName}' found in layer '{zoneLayer.DisplayName}' at '{layerPath}'. " +
                    "Run Tools > Scene Zone Tools > Validate Project to remove incomplete zone assets.",
                    incompleteZone);
            }
        }

        private void RefreshSceneLayers()
        {
            sceneLayers.Clear();
            SceneZoneLayerAsset[] discovered = AssetDatabase.FindAssets("t:SceneZoneLayerAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneZoneLayerAsset>)
                .Where(layer => layer != null && (layer == zoneLayer || (layer.HasSceneBinding && LayerMatchesActiveScene(layer))))
                .Distinct()
                .OrderBy(layer => layer.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            sceneLayers.AddRange(discovered);
            if (zoneLayer != null && !sceneLayers.Contains(zoneLayer)) sceneLayers.Insert(0, zoneLayer);

            HashSet<SceneZoneAsset> available = sceneLayers
                .SelectMany(layer => layer.Zones)
                .Where(candidate => candidate != null)
                .ToHashSet();
            hiddenZones.RemoveWhere(candidate => candidate == null || !available.Contains(candidate));
            if (zone != null) hiddenZones.Remove(zone);
        }

        private Color CreateUniqueFillColor()
        {
            HashSet<Color32> used = layerZones.Where(zone => zone != null).Select(zone => (Color32)zone.FillColor).ToHashSet();
            System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
            float hue = (float)random.NextDouble();
            for (int attempt = 0; attempt < 64; attempt++)
            {
                hue = Mathf.Repeat(hue + 0.61803398875f, 1f);
                Color candidate = Color.HSVToRGB(hue, 0.58f + (attempt % 3) * 0.1f, 0.9f);
                candidate.a = 0.16f;
                if (!used.Contains((Color32)candidate)) return candidate;
            }
            Color fallback = Color.HSVToRGB(Mathf.Repeat(layerZones.Count * 0.137f, 1f), 0.8f, 0.95f);
            fallback.a = 0.16f;
            return fallback;
        }

        private void NormalizeUniqueFillColors()
        {
            if (zoneLayer == null || layerZones.Count == 0) return;
            HashSet<Color32> used = new HashSet<Color32>();
            bool changed = false;
            foreach (SceneZoneAsset zone in layerZones)
            {
                Color color = zone.FillColor;
                if (!used.Add((Color32)color))
                {
                    color = CreateUniqueFillColor();
                    while (!used.Add((Color32)color)) color = CreateUniqueFillColor();
                    zone.AssignFillColor(color);
                    EditorUtility.SetDirty(zone);
                    changed = true;
                }
            }
            if (changed && AssetDatabase.Contains(zoneLayer)) AssetDatabase.SaveAssetIfDirty(zoneLayer);
        }

        private void SetLayerZones(IEnumerable<SceneZoneAsset> values)
        {
            if (zoneLayer == null) return;
            SceneZoneAsset[] zones = (values ?? Array.Empty<SceneZoneAsset>()).Where(value => value != null).Distinct().ToArray();
            Undo.RecordObject(zoneLayer, "Update Zone Layer");
            SerializedObject serialized = new SerializedObject(zoneLayer);
            SerializedProperty property = serialized.FindProperty("zones");
            property.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(zoneLayer);
        }


    }
}
