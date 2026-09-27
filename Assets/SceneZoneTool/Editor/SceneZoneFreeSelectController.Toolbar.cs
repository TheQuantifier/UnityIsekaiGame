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
    {        internal void DrawOverlayToolbar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(235f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("+", "Create a new zone layer file"), EditorStyles.miniButton, GUILayout.Width(24f))) CreateLayer();
                    using (new EditorGUI.DisabledScope(zoneLayer == null))
                    {
                        if (GUILayout.Button(new GUIContent("-", "Delete the selected layer and all zones stored in it"), EditorStyles.miniButton, GUILayout.Width(24f))) DeleteSelectedLayer();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    SceneZoneLayerAsset selectedLayer = (SceneZoneLayerAsset)EditorGUILayout.ObjectField(
                        zoneLayer, typeof(SceneZoneLayerAsset), false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck()) SelectLayer(selectedLayer);
                }

                DrawSectionSeparator();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Zone", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(zone == null))
                    {
                        string tooltip = pendingNewZone ? "Discard the unfinished zone" : "Delete the selected zone";
                        if (GUILayout.Button(new GUIContent("-", tooltip), EditorStyles.miniButton, GUILayout.Width(24f)))
                        {
                            if (pendingNewZone) CancelPendingZone();
                            else DeleteSelectedZone();
                        }
                    }
                }

                if (pendingNewZone)
                {
                    EditorGUILayout.LabelField(zone == null ? "New Zone" : $"{zone.DisplayName} (draft)");
                }
                else
                {
                    using (new EditorGUI.DisabledScope(layerZones.Count == 0))
                    {
                        int currentIndex = Mathf.Max(0, layerZones.IndexOf(zone));
                        int selectedIndex = EditorGUILayout.Popup(currentIndex, layerZones.Select(value => value.DisplayName).ToArray());
                        if (selectedIndex >= 0 && selectedIndex < layerZones.Count && layerZones[selectedIndex] != zone)
                            SelectZone(layerZones[selectedIndex]);
                    }
                }

                bool layerMatchesActiveScene = zoneLayer == null || LayerMatchesActiveScene(zoneLayer);
                if (zoneLayer != null && !layerMatchesActiveScene)
                    EditorGUILayout.HelpBox($"This layer belongs to scene '{zoneLayer.SceneName}'. Open that scene or rebind it in Options.", MessageType.Warning);

                DrawSectionSeparator();
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(zoneLayer == null || !layerMatchesActiveScene))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToolButton(new GUIContent(string.Empty, GetSelectHandIcon(), "Select Zone: click inside an existing zone to select it"), paintMode == SceneZonePaintMode.SelectZone))
                        SelectPaintMode(SceneZonePaintMode.SelectZone);
                    if (ToolButton(new GUIContent(string.Empty, GetCircleOutlineIcon(), "Circle Zone: click its center, then click its radius"), paintMode == SceneZonePaintMode.CenterPoint && zone != null && zone.Shape == SceneZoneShape.Circle))
                        BeginNewZone(SceneZoneShape.Circle);
                    if (ToolButton(new GUIContent(string.Empty, GetPolygonOutlineIcon(), "Polygon Zone: place unrestricted world X/Z points"), paintMode == SceneZonePaintMode.PolygonPoints && drawing))
                        BeginNewZone(SceneZoneShape.Polygon);
                    if (ToolButton(EditorGUIUtility.IconContent("MoveTool", "Move Points|Drag existing polygon points"), paintMode == SceneZonePaintMode.MovePoints))
                        SelectPaintMode(SceneZonePaintMode.MovePoints);
                    if (ToolButton(new GUIContent(string.Empty, GetTextCursorIcon(), "Name Zone: view and edit zone labels"), paintMode == SceneZonePaintMode.NameZone))
                        SelectPaintMode(SceneZonePaintMode.NameZone);
                    using (new EditorGUI.DisabledScope(
                               zone == null || pendingNewZone || drawing))
                    {
                        if (ToolButton(EditorGUIUtility.IconContent("d_editicon.sml", "Replace Selected Outline|Replace this zone's circle or polygon while preserving its ID, name, and external references."), false))
                            BeginReplacingSelectedZone();
                    }
                }

                if (zoneLayer == null)
                {
                    EditorGUILayout.HelpBox("Select a layer or press + to create zones.", MessageType.Info);
                    return;
                }

                if (paintMode == SceneZonePaintMode.NameZone) DrawSelectedZoneRename();

                if (drawing)
                {
                    EditorGUILayout.LabelField($"Polygon draft: {draftPoints.Count} point(s)", EditorStyles.miniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Close", EditorStyles.miniButton)) CommitDraft();
                        if (GUILayout.Button("Undo Point", EditorStyles.miniButton)) RemoveLast();
                        if (GUILayout.Button("Cancel", EditorStyles.miniButton)) CancelDrawing();
                    }
                }
                else if (paintMode == SceneZonePaintMode.CenterPoint && zone != null && zone.Shape == SceneZoneShape.Circle)
                {
                    string operation = replacingCircle ? "replacement" : "circle";
                    EditorGUILayout.LabelField(definingCircleRadius ? $"Click to set {operation} radius" : $"Click to set {operation} center", EditorStyles.miniLabel);
                }

                DrawSectionSeparator();
                DrawVisibleZoneControls();

                DrawSectionSeparator();
                showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Options", true);
                if (showAdvancedOptions)
                {
                    EditorGUI.BeginChangeCheck();
                    displayHeight = EditorGUILayout.FloatField("Display Height", displayHeight);
                    dragToDraw = EditorGUILayout.ToggleLeft("Allow drag drawing", dragToDraw);
                    if (dragToDraw)
                        freehandSpacing = Mathf.Max(0.25f, EditorGUILayout.FloatField("Drag Point Spacing", freehandSpacing));
                    if (EditorGUI.EndChangeCheck())
                        SceneZoneToolProjectSettings.instance.SetAuthoringDefaults(
                            SceneZoneToolProjectSettings.instance.DefaultSaveFolder,
                            displayHeight, dragToDraw, freehandSpacing);
                    EditorGUILayout.LabelField("Scene", string.IsNullOrWhiteSpace(zoneLayer.SceneName) ? "Unbound" : zoneLayer.SceneName);
                    if (!layerMatchesActiveScene && GUILayout.Button("Rebind Layer To Active Scene", EditorStyles.miniButton)) BindLayerToActiveScene(zoneLayer, force: true);
                    EditorGUILayout.HelpBox("Zone placement has no canvas bounds. Only world X/Z coordinates are saved.", MessageType.None);
                }

                if (!string.IsNullOrWhiteSpace(overlapError)) EditorGUILayout.HelpBox(overlapError, MessageType.Error);
            }
        }

        internal void HandleOverlayVisibilityChanged(bool anyOverlayVisible)
        {
            if (anyOverlayVisible) return;
            ReleaseActiveMouseInteraction();
        }

        private static void DrawSectionSeparator()
        {
            Rect line = EditorGUILayout.GetControlRect(false, 5f);
            line.y += 2f;
            line.height = 1f;
            EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.32f));
        }

        private static bool ToolButton(GUIContent content, bool active)
        {
            Color previous = GUI.backgroundColor;
            if (active) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f, 1f);
            bool clicked = GUILayout.Button(content, GUILayout.Width(32f), GUILayout.Height(30f));
            GUI.backgroundColor = previous;
            return clicked;
        }

        private Texture2D GetSelectHandIcon()
        {
            if (selectHandIcon != null) return selectHandIcon;
            selectHandIcon = CreateIcon((x, y) =>
                (x >= 8 && x <= 18 && y >= 10 && y <= 12) ||
                (x >= 5 && x <= 11 && y >= 6 && y <= 13) ||
                (x >= 3 && x <= 7 && y >= 7 && y <= 10) ||
                (x >= 7 && x <= 13 && y >= 4 && y <= 8),
                new Color(0.88f, 0.88f, 0.88f, 1f));
            return selectHandIcon;
        }

        private Texture2D GetCircleOutlineIcon()
        {
            if (yellowCircleOutlineIcon != null) return yellowCircleOutlineIcon;
            Vector2 center = new Vector2(9.5f, 9.5f);
            yellowCircleOutlineIcon = CreateIcon((x, y) => Mathf.Abs(Vector2.Distance(new Vector2(x, y), center) - 6.6f) <= 1.1f,
                new Color(1f, 0.72f, 0.05f, 1f));
            return yellowCircleOutlineIcon;
        }

        private Texture2D GetPolygonOutlineIcon()
        {
            if (bluePolygonOutlineIcon != null) return bluePolygonOutlineIcon;
            Vector2[] vertices =
            {
                new Vector2(3f, 7f), new Vector2(6f, 16f), new Vector2(14f, 15f),
                new Vector2(17f, 9f), new Vector2(12f, 3f), new Vector2(5f, 4f)
            };
            bluePolygonOutlineIcon = CreateIcon((x, y) =>
            {
                Vector2 point = new Vector2(x, y);
                for (int i = 0; i < vertices.Length; i++)
                    if (DistanceToSegment(point, vertices[i], vertices[(i + 1) % vertices.Length]) <= 1.05f) return true;
                return false;
            }, new Color(0.2f, 0.65f, 1f, 1f));
            return bluePolygonOutlineIcon;
        }

        private Texture2D GetTextCursorIcon()
        {
            if (textCursorIcon != null) return textCursorIcon;
            textCursorIcon = CreateIcon((x, y) =>
                (x >= 9 && x <= 10 && y >= 3 && y <= 16) ||
                (x >= 5 && x <= 14 && (y == 3 || y == 4 || y == 15 || y == 16)),
                new Color(0.88f, 0.88f, 0.88f, 1f));
            return textCursorIcon;
        }

        private static Texture2D CreateIcon(Func<int, int, bool> contains, Color color)
        {
            const int size = 20;
            Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, contains(x, y) ? color : clear);
            icon.Apply(false, true);
            return icon;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;
            float lengthSquared = direction.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, direction) / lengthSquared);
            return Vector2.Distance(point, start + direction * t);
        }

        private void DrawSelectedZoneRename()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rename Selected Zone", EditorStyles.boldLabel);
            if (zone == null || pendingNewZone || !layerZones.Contains(zone))
            {
                EditorGUILayout.HelpBox("Select a saved zone from the Zone dropdown to rename it.", MessageType.None);
                return;
            }

            SceneZoneAsset selectedZone = zone;
            if (!zoneRenameDrafts.TryGetValue(selectedZone, out string draft)) draft = selectedZone.DisplayName;
            using (new EditorGUILayout.HorizontalScope())
            {
                const string renameControl = "SceneZoneTool.RenameField";
                GUI.SetNextControlName(renameControl);
                draft = EditorGUILayout.TextField(draft);
                zoneRenameDrafts[selectedZone] = draft;
                bool canRename = !string.IsNullOrWhiteSpace(draft)
                    && !string.Equals(draft.Trim(), selectedZone.DisplayName, StringComparison.Ordinal);
                bool submitWithEnter = canRename
                    && GUI.GetNameOfFocusedControl() == renameControl
                    && Event.current.type == EventType.KeyDown
                    && Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter;
                using (new EditorGUI.DisabledScope(
                           !canRename))
                {
                    if (GUILayout.Button("Rename", GUILayout.Width(64f)) || submitWithEnter)
                    {
                        RenameZone(selectedZone, draft);
                        if (submitWithEnter)
                        {
                            Event.current.Use();
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
        }

        private void DrawVisibleZoneControls()
        {
            showVisibleZones = EditorGUILayout.Foldout(showVisibleZones, "Visible Layers & Zones", true);
            if (!showVisibleZones) return;

            SceneZoneAsset[] available = sceneLayers
                .Where(layer => layer != null)
                .SelectMany(layer => layer.Zones)
                .Where(candidate => candidate != null)
                .Distinct()
                .ToArray();
            if (available.Length == 0)
            {
                EditorGUILayout.HelpBox("No zones are bound to the active scene.", MessageType.None);
                return;
            }

            bool allVisible = available.All(IsZoneVisible);
            bool allHiddenExceptActive = available.Where(candidate => candidate != zone).All(candidate => !IsZoneVisible(candidate));
            bool activeLayerVisible = zoneLayer != null && zoneLayer.Zones
                .Where(candidate => candidate != null)
                .All(IsZoneVisible);
            bool otherLayersHidden = sceneLayers
                .Where(layer => layer != null && layer != zoneLayer)
                .SelectMany(layer => layer.Zones)
                .Where(candidate => candidate != null)
                .All(candidate => !IsZoneVisible(candidate));
            bool showingActiveLayer = activeLayerVisible && otherLayersHidden;
            bool showAll = EditorGUILayout.ToggleLeft("Show All", allVisible);
            if (showAll != allVisible)
            {
                if (showAll) ShowAllZones();
                else HideAllZones();
            }
            bool hideAll = EditorGUILayout.ToggleLeft("Hide All", allHiddenExceptActive);
            if (hideAll != allHiddenExceptActive)
            {
                if (hideAll) HideAllZones();
                else ShowAllZones();
            }
            bool showActiveLayer = EditorGUILayout.ToggleLeft("Show Active Layer", showingActiveLayer);
            if (showActiveLayer != showingActiveLayer)
            {
                if (showActiveLayer) ShowActiveLayer();
                else ShowAllZones();
            }

            EditorGUILayout.Space(2f);
            visibleZonesScroll = EditorGUILayout.BeginScrollView(visibleZonesScroll, GUILayout.MaxHeight(220f));
            foreach (SceneZoneLayerAsset layer in sceneLayers.Where(candidate => candidate != null))
            {
                SceneZoneAsset[] zones = layer.Zones.Where(candidate => candidate != null).ToArray();
                if (zones.Length == 0) continue;
                bool layerAllVisible = zones.All(IsZoneVisible);
                bool layerAnyVisible = zones.Any(IsZoneVisible);
                EditorGUI.showMixedValue = layerAnyVisible && !layerAllVisible;
                bool layerVisible = EditorGUILayout.ToggleLeft($"{layer.DisplayName} (Layer)", layerAllVisible, EditorStyles.boldLabel);
                EditorGUI.showMixedValue = false;
                if (layerVisible != layerAllVisible)
                    foreach (SceneZoneAsset candidate in zones) SetZoneVisible(candidate, layerVisible);

                EditorGUI.indentLevel++;
                foreach (SceneZoneAsset candidate in zones)
                {
                    bool active = candidate == zone;
                    using (new EditorGUI.DisabledScope(active))
                    {
                        bool visible = EditorGUILayout.ToggleLeft(active ? $"{candidate.DisplayName} (active)" : candidate.DisplayName, IsZoneVisible(candidate));
                        if (!active && visible != IsZoneVisible(candidate)) SetZoneVisible(candidate, visible);
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("The active zone is always visible. Visibility affects Scene view authoring only; it does not change saved zone data.", MessageType.None);
        }

        internal void RenameZone(SceneZoneAsset zone, string requestedName)
        {
            if (zone == null || string.IsNullOrWhiteSpace(requestedName)) return;
            string normalized = requestedName.Trim();
            if (layerZones.Any(value => value != null && value != zone && string.Equals(value.DisplayName, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                ShowNotification(new GUIContent("Zone names must be unique within a layer."));
                return;
            }
            Undo.RecordObject(zone, "Rename Zone");
            SerializedObject serialized = new SerializedObject(zone);
            serialized.FindProperty("displayName").stringValue = normalized;
            serialized.ApplyModifiedProperties();
            zone.name = normalized;
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssetIfDirty(zone);
            zoneRenameDrafts[zone] = normalized;
            Repaint();
        }


    }
}
