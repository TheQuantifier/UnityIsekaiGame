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
    public enum SceneZonePaintMode
    {
        PolygonPoints = 0,
        CenterPoint = 1,
        MovePoints = 2,
        NameZone = 3,
        SelectZone = 4
    }

    /// <summary>Standalone Scene View free-select controller. No project-specific types are used here.</summary>
    internal sealed class SceneZoneFreeSelectController : ScriptableObject
    {
        private static string DefaultFolder => AssetDatabase.IsValidFolder("Assets/_Project")
            ? "Assets/_Project/Content/World/Zones"
            : "Assets/Zones";
        private const float CloseHandlePixels = 14f;
        private static readonly Color PolygonToolColor = new Color(0.25f, 0.65f, 1f, 1f);
        private static readonly Color CenterToolColor = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color InactivePointColor = new Color(0.65f, 0.9f, 1f, 1f);

        [SerializeField] private SceneZoneAsset boundary;
        [SerializeField] private SceneZoneLayerAsset boundaryLayer;
        [SerializeField] private SceneZonePaintMode paintMode = SceneZonePaintMode.PolygonPoints;
        [SerializeField] private float displayHeight;
        [SerializeField] private bool showAdvancedOptions;
        [SerializeField] private bool drawing;
        [SerializeField] private bool pendingNewZone;
        [SerializeField] private bool dragToDraw = true;
        [SerializeField] private bool showLayerOutlines = true;
        [SerializeField, Min(0.25f)] private float freehandSpacing = 3f;

        private readonly List<Vector3> draftPoints = new List<Vector3>();
        private readonly List<SceneZoneAsset> layerBoundaries = new List<SceneZoneAsset>();
        private Vector3 hoverPoint;
        private bool hasHover;
        private bool hoveringReusablePoint;
        private bool dragging;
        private int movingDraftPointIndex = -1;
        private bool movingPendingCircleCenter;
        private bool definingCircleRadius;
        private Vector3 pendingCircleCenter;
        private SceneZoneAsset blockedBy;
        private string overlapError;
        private readonly List<Vector3> overlapHighlightSegments = new List<Vector3>();
        private readonly Dictionary<SceneZoneAsset, string> zoneRenameDrafts = new Dictionary<SceneZoneAsset, string>();
        private int movingPointIndex = -1;
        private Vector3[] movingPointDraft = Array.Empty<Vector3>();
        private bool insertingPoint;
        private static Texture2D selectHandIcon;
        private static Texture2D yellowCircleOutlineIcon;
        private static Texture2D bluePolygonOutlineIcon;
        private static Texture2D textCursorIcon;
        private static GUIStyle zoneNameStyle;

        internal static SceneZoneFreeSelectController CreateOverlayController()
        {
            SceneZoneFreeSelectController controller = CreateInstance<SceneZoneFreeSelectController>();
            controller.hideFlags = HideFlags.HideAndDontSave;
            return controller;
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.projectChanged += OnProjectChange;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            if (boundaryLayer == null) RestorePreferredLayer();
            RefreshLayerBoundaries();
            if (boundary == null) boundary = layerBoundaries.FirstOrDefault();
        }

        private void OnProjectChange()
        {
            if (boundaryLayer != null && !AssetDatabase.Contains(boundaryLayer)) boundaryLayer = null;
            RefreshLayerBoundaries();
            if (!pendingNewZone && boundary != null && !layerBoundaries.Contains(boundary)) boundary = layerBoundaries.FirstOrDefault();
            SceneView.RepaintAll();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            CancelPendingZone();
            boundaryLayer = SceneZoneToolProjectSettings.instance.LoadLayerForActiveScene();
            RefreshLayerBoundaries();
            boundary = layerBoundaries.FirstOrDefault();
            ClearOverlapError();
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.projectChanged -= OnProjectChange;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            CancelPendingZone();
        }

        private static void Repaint() => SceneView.RepaintAll();

        private static void ShowNotification(GUIContent content)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null) sceneView.ShowNotification(content);
            else Debug.LogWarning(content?.text ?? "Scene Zone Tool notification");
        }

        internal void DrawOverlayToolbar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(235f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("+", "Create a new zone layer file"), EditorStyles.miniButton, GUILayout.Width(24f))) CreateLayer();
                    using (new EditorGUI.DisabledScope(boundaryLayer == null))
                    {
                        if (GUILayout.Button(new GUIContent("-", "Delete the selected layer and all zones stored in it"), EditorStyles.miniButton, GUILayout.Width(24f))) DeleteSelectedLayer();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    SceneZoneLayerAsset selectedLayer = (SceneZoneLayerAsset)EditorGUILayout.ObjectField(
                        boundaryLayer, typeof(SceneZoneLayerAsset), false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck()) SelectLayer(selectedLayer);
                }

                DrawSectionSeparator();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Zone", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(boundary == null))
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
                    EditorGUILayout.LabelField(boundary == null ? "New Zone" : $"{boundary.DisplayName} (draft)");
                }
                else
                {
                    using (new EditorGUI.DisabledScope(layerBoundaries.Count == 0))
                    {
                        int currentIndex = Mathf.Max(0, layerBoundaries.IndexOf(boundary));
                        int selectedIndex = EditorGUILayout.Popup(currentIndex, layerBoundaries.Select(value => value.DisplayName).ToArray());
                        if (selectedIndex >= 0 && selectedIndex < layerBoundaries.Count && layerBoundaries[selectedIndex] != boundary)
                            SelectBoundary(layerBoundaries[selectedIndex]);
                    }
                }

                bool layerMatchesActiveScene = boundaryLayer == null || LayerMatchesActiveScene(boundaryLayer);
                if (boundaryLayer != null && !layerMatchesActiveScene)
                    EditorGUILayout.HelpBox($"This layer belongs to scene '{boundaryLayer.SceneName}'. Open that scene or rebind it in Options.", MessageType.Warning);

                DrawSectionSeparator();
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(boundaryLayer == null || !layerMatchesActiveScene))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToolButton(new GUIContent(string.Empty, GetSelectHandIcon(), "Select Zone: click inside an existing zone to select it"), paintMode == SceneZonePaintMode.SelectZone))
                        SelectPaintMode(SceneZonePaintMode.SelectZone);
                    if (ToolButton(new GUIContent(string.Empty, GetCircleOutlineIcon(), "Circle Zone: click its center, then click its radius"), paintMode == SceneZonePaintMode.CenterPoint && boundary != null && boundary.Shape == SceneZoneShape.Circle))
                        BeginNewZone(SceneZoneShape.Circle);
                    if (ToolButton(new GUIContent(string.Empty, GetPolygonOutlineIcon(), "Polygon Zone: place unrestricted world X/Z points"), paintMode == SceneZonePaintMode.PolygonPoints && drawing))
                        BeginNewZone(SceneZoneShape.Polygon);
                    if (ToolButton(EditorGUIUtility.IconContent("MoveTool", "Move Points|Drag existing polygon points"), paintMode == SceneZonePaintMode.MovePoints))
                        SelectPaintMode(SceneZonePaintMode.MovePoints);
                    if (ToolButton(new GUIContent(string.Empty, GetTextCursorIcon(), "Name Zone: view and edit zone labels"), paintMode == SceneZonePaintMode.NameZone))
                        SelectPaintMode(SceneZonePaintMode.NameZone);
                    using (new EditorGUI.DisabledScope(
                               boundary == null || pendingNewZone || drawing || boundary.Shape != SceneZoneShape.Polygon))
                    {
                        if (ToolButton(EditorGUIUtility.IconContent("d_editicon.sml", "Redraw Selected Outline|Replace this zone's polygon while preserving its ID, name, and external references."), false))
                            BeginRedrawingSelectedPolygon();
                    }
                }

                if (boundaryLayer == null)
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
                else if (paintMode == SceneZonePaintMode.CenterPoint && boundary != null && boundary.Shape == SceneZoneShape.Circle)
                {
                    EditorGUILayout.LabelField(definingCircleRadius ? "Click to set radius" : "Click to set circle center", EditorStyles.miniLabel);
                }

                DrawSectionSeparator();
                showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Options", true);
                if (showAdvancedOptions)
                {
                    displayHeight = EditorGUILayout.FloatField("Display Height", displayHeight);
                    showLayerOutlines = EditorGUILayout.ToggleLeft("Show layer outlines", showLayerOutlines);
                    dragToDraw = EditorGUILayout.ToggleLeft("Allow drag drawing", dragToDraw);
                    if (dragToDraw)
                        freehandSpacing = Mathf.Max(0.25f, EditorGUILayout.FloatField("Drag Point Spacing", freehandSpacing));
                    EditorGUILayout.LabelField("Scene", string.IsNullOrWhiteSpace(boundaryLayer.SceneName) ? "Unbound" : boundaryLayer.SceneName);
                    if (!layerMatchesActiveScene && GUILayout.Button("Rebind Layer To Active Scene", EditorStyles.miniButton)) BindLayerToActiveScene(boundaryLayer, force: true);
                    EditorGUILayout.HelpBox("Zone placement has no canvas bounds. Only world X/Z coordinates are saved.", MessageType.None);
                }

                if (!string.IsNullOrWhiteSpace(overlapError)) EditorGUILayout.HelpBox(overlapError, MessageType.Error);
            }
        }

        internal void HandleOverlayVisibilityChanged(bool anyOverlayVisible)
        {
            if (anyOverlayVisible) return;
            dragging = false;
            movingDraftPointIndex = -1;
            movingPendingCircleCenter = false;
            GUIUtility.hotControl = 0;
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

        private static Texture2D GetSelectHandIcon()
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

        private static Texture2D GetCircleOutlineIcon()
        {
            if (yellowCircleOutlineIcon != null) return yellowCircleOutlineIcon;
            Vector2 center = new Vector2(9.5f, 9.5f);
            yellowCircleOutlineIcon = CreateIcon((x, y) => Mathf.Abs(Vector2.Distance(new Vector2(x, y), center) - 6.6f) <= 1.1f,
                new Color(1f, 0.72f, 0.05f, 1f));
            return yellowCircleOutlineIcon;
        }

        private static Texture2D GetPolygonOutlineIcon()
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

        private static Texture2D GetTextCursorIcon()
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
            if (boundary == null || pendingNewZone || !layerBoundaries.Contains(boundary))
            {
                EditorGUILayout.HelpBox("Select a saved zone from the Zone dropdown to rename it.", MessageType.None);
                return;
            }

            SceneZoneAsset selectedZone = boundary;
            if (!zoneRenameDrafts.TryGetValue(selectedZone, out string draft)) draft = selectedZone.DisplayName;
            using (new EditorGUILayout.HorizontalScope())
            {
                draft = EditorGUILayout.TextField(draft);
                zoneRenameDrafts[selectedZone] = draft;
                using (new EditorGUI.DisabledScope(
                           string.IsNullOrWhiteSpace(draft) ||
                           string.Equals(draft.Trim(), selectedZone.DisplayName, StringComparison.Ordinal)))
                {
                    if (GUILayout.Button("Rename", GUILayout.Width(64f))) RenameZone(selectedZone, draft);
                }
            }
        }

        internal void RenameZone(SceneZoneAsset zone, string requestedName)
        {
            if (zone == null || string.IsNullOrWhiteSpace(requestedName)) return;
            string normalized = requestedName.Trim();
            if (layerBoundaries.Any(value => value != null && value != zone && string.Equals(value.DisplayName, normalized, StringComparison.OrdinalIgnoreCase)))
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

        private void OnSceneGUI(SceneView view)
        {
            if (!SceneZoneOverlayController.IsActiveFor(view) || boundaryLayer == null) return;
            DrawLayerOutlines();
            DrawOverlapHighlights();
            if (paintMode == SceneZonePaintMode.SelectZone)
            {
                HandleSelectZoneTool(view);
                return;
            }
            if (boundary == null) return;
            DrawCenterPoint();
            if (boundary.Shape == SceneZoneShape.Circle)
            {
                DrawCircleFill(boundary.CircleCenter, boundary.CircleRadius, boundary.FillColor);
                DrawZoneCircle(boundary.CircleCenter, boundary.CircleRadius, boundary.LineColor, false);
                DrawZoneName(boundary);
            }
            if (paintMode == SceneZonePaintMode.CenterPoint)
            {
                HandleCenterPointTool(view);
                return;
            }
            IReadOnlyList<Vector3> displayed = boundary.Shape == SceneZoneShape.Polygon && drawing
                ? draftPoints
                : movingPointIndex >= 0 && movingPointDraft.Length > 0 ? movingPointDraft : boundary.PolygonPoints;
            if (boundary.Shape == SceneZoneShape.Polygon) DrawPolygonFill(displayed, boundary.FillColor);
            DrawOutline(displayed, close: !drawing);
            if (boundary.Shape == SceneZoneShape.Polygon) DrawZoneName(boundary);
            if (paintMode == SceneZonePaintMode.MovePoints)
            {
                HandleMovePointsTool(view);
                return;
            }
            if (paintMode == SceneZonePaintMode.NameZone) return;
            if (!drawing)
            {
                return;
            }

            Event current = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);
            hasHover = TryScenePlanePoint(current.mousePosition, out hoverPoint);
            Vector3 reusablePoint = default;
            hoveringReusablePoint = hasHover && TryFindReusablePointAtMouse(current.mousePosition, out reusablePoint);
            if (hoveringReusablePoint) hoverPoint = reusablePoint;
            DrawLivePreview();

            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode is KeyCode.Return or KeyCode.KeypadEnter) { CommitDraft(); current.Use(); }
                else if (current.keyCode == KeyCode.Backspace) { RemoveLast(); current.Use(); }
                else if (current.keyCode == KeyCode.Escape) { CancelDrawing(); current.Use(); }
            }
            if (current.type == EventType.MouseDown && current.button == 1 && draftPoints.Count > 0)
            {
                RemoveLast();
                current.Use();
            }
            else if (current.button == 0 && current.shift && !current.alt && current.type == EventType.MouseDown)
            {
                movingDraftPointIndex = FindPointAtMouse(current.mousePosition, draftPoints, 14f);
                if (movingDraftPointIndex >= 0)
                {
                    dragging = false;
                    GUIUtility.hotControl = control;
                    current.Use();
                }
            }
            else if (current.button == 0 && current.type == EventType.MouseDrag && movingDraftPointIndex >= 0)
            {
                if (TryScenePlanePoint(current.mousePosition, out Vector3 movedPoint))
                {
                    movedPoint = ToStoredPoint(movedPoint);
                    Vector3 previous = draftPoints[movingDraftPointIndex];
                    draftPoints[movingDraftPointIndex] = movedPoint;
                    if (draftPoints.Count >= 3 && !SceneZoneOverlap.ValidateSimplePolygon(draftPoints, out string geometryFailure))
                    {
                        draftPoints[movingDraftPointIndex] = previous;
                        overlapError = $"ERROR: {geometryFailure}";
                    }
                    else
                    {
                        SceneZoneAsset conflict = FindPolygonConflict(draftPoints, includeClosingSegment: false);
                        if (conflict != null)
                        {
                            draftPoints[movingDraftPointIndex] = previous;
                            blockedBy = conflict;
                            SetPolygonOverlapError(draftPoints, conflict);
                        }
                        else
                        {
                            blockedBy = null;
                            ClearOverlapError();
                        }
                    }
                }
                current.Use();
            }
            else if (current.button == 0 && current.type == EventType.MouseUp && movingDraftPointIndex >= 0)
            {
                movingDraftPointIndex = -1;
                GUIUtility.hotControl = 0;
                current.Use();
            }
            else if (current.button == 0 && !current.shift && !current.alt && current.type == EventType.MouseDown && hasHover)
            {
                if (ShouldClose(current)) CommitDraft(); else AddDraft(hoverPoint);
                dragging = dragToDraw;
                GUIUtility.hotControl = control;
                current.Use();
            }
            else if (current.button == 0 && !current.shift && current.type == EventType.MouseDrag && dragging && hasHover)
            {
                if (draftPoints.Count == 0 || HorizontalDistance(draftPoints[draftPoints.Count - 1], hoverPoint) >= freehandSpacing) AddDraft(hoverPoint);
                current.Use();
            }
            else if (current.button == 0 && current.type == EventType.MouseUp && dragging)
            {
                dragging = false;
                GUIUtility.hotControl = 0;
                current.Use();
            }
            view.Repaint();
        }

        private void HandleMovePointsTool(SceneView view)
        {
            if (boundary.Shape != SceneZoneShape.Polygon || boundary.PolygonPoints.Count == 0) return;
            Event current = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && movingPointIndex >= 0)
            {
                movingPointIndex = -1;
                movingPointDraft = Array.Empty<Vector3>();
                insertingPoint = false;
                ClearOverlapError();
                GUIUtility.hotControl = 0;
                current.Use();
            }
            if (current.type == EventType.MouseDown && current.button == 1 && movingPointIndex >= 0 && !insertingPoint)
            {
                int sourceIndex = movingPointIndex;
                movingPointDraft = InsertPointAfter(movingPointDraft, sourceIndex, DefaultInsertedPoint(movingPointDraft, sourceIndex));
                movingPointIndex = sourceIndex + 1;
                insertingPoint = true;
                current.Use();
            }
            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                int nearest = FindPointAtMouse(current.mousePosition, boundary.PolygonPoints, 14f);
                if (nearest >= 0)
                {
                    insertingPoint = current.shift;
                    if (insertingPoint)
                    {
                        movingPointIndex = nearest + 1;
                        movingPointDraft = InsertPointAfter(boundary.PolygonPoints, nearest, DefaultInsertedPoint(boundary.PolygonPoints, nearest));
                    }
                    else
                    {
                        movingPointIndex = nearest;
                        movingPointDraft = boundary.PolygonPoints.ToArray();
                    }
                    GUIUtility.hotControl = control;
                    current.Use();
                }
            }
            else if (current.type == EventType.MouseDrag && current.button == 0 && movingPointIndex >= 0)
            {
                if (TryScenePlanePoint(current.mousePosition, out Vector3 candidate))
                {
                    candidate = ToStoredPoint(candidate);
                    Vector3 previous = movingPointDraft[movingPointIndex];
                    movingPointDraft[movingPointIndex] = candidate;
                    if (!SceneZoneOverlap.ValidateSimplePolygon(movingPointDraft, out string geometryFailure))
                    {
                        movingPointDraft[movingPointIndex] = previous;
                        overlapError = $"ERROR: {geometryFailure}";
                    }
                    else if (boundary.HasCenterPoint && !SceneZoneOverlap.PolygonContains(movingPointDraft, boundary.CenterPoint))
                    {
                        movingPointDraft[movingPointIndex] = previous;
                        overlapError = "ERROR: Moving this point would exclude the orange center point.";
                    }
                    else
                    {
                        SceneZoneAsset conflict = FindPolygonConflict(movingPointDraft, includeClosingSegment: true);
                        if (conflict != null)
                        {
                            blockedBy = conflict;
                            SetPolygonOverlapError(movingPointDraft, conflict);
                            movingPointDraft[movingPointIndex] = previous;
                        }
                        else
                        {
                            blockedBy = null;
                            ClearOverlapError();
                        }
                    }
                }
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0 && movingPointIndex >= 0)
            {
                bool applied = TryApplyPolygonPoints(movingPointDraft);
                if (insertingPoint && !applied)
                    ShowNotification(new GUIContent("The inserted point would make this boundary invalid."));
                movingPointIndex = -1;
                movingPointDraft = Array.Empty<Vector3>();
                insertingPoint = false;
                GUIUtility.hotControl = 0;
                current.Use();
            }

            IReadOnlyList<Vector3> points = movingPointIndex >= 0 && movingPointDraft.Length > 0 ? movingPointDraft : boundary.PolygonPoints;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 displayedPoint = ToDisplayPoint(points[i]);
                DrawPointHandle(displayedPoint, i == movingPointIndex ? Color.yellow : PolygonToolColor, 0.13f);
            }
            view.Repaint();
        }

        private void HandleSelectZoneTool(SceneView view)
        {
            Event current = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);
            bool found = TryScenePlanePoint(current.mousePosition, out Vector3 candidate);
            SceneZoneAsset hovered = found
                ? layerBoundaries
                    .Where(value => value != null && value.Contains(candidate))
                    .OrderBy(value => value.ApproximateHorizontalArea())
                    .FirstOrDefault()
                : null;

            if (hovered != null)
            {
                Color previous = Handles.color;
                Handles.color = Color.yellow;
                if (hovered.Shape == SceneZoneShape.Circle)
                    DrawZoneCircle(hovered.CircleCenter, hovered.CircleRadius, Color.yellow, false);
                else
                {
                    Vector3[] points = hovered.PolygonPoints.Select(ToDisplayPoint).ToArray();
                    for (int i = 1; i < points.Length; i++) Handles.DrawAAPolyLine(4f, points[i - 1], points[i]);
                    if (points.Length >= 3) Handles.DrawAAPolyLine(4f, points[points.Length - 1], points[0]);
                }
                Handles.Label(ToDisplayPoint(candidate), $"Select {hovered.DisplayName}");
                Handles.color = previous;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (hovered != null) SelectBoundary(hovered);
                current.Use();
            }
            view.Repaint();
        }

        private void HandleCenterPointTool(SceneView view)
        {
            Event current = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);
            bool found = TryScenePlanePoint(current.mousePosition, out Vector3 candidate);
            if (boundary.Shape == SceneZoneShape.Circle)
            {
                HandleCircleTool(view, current, control, found, candidate);
                return;
            }
            bool valid = found && (boundary.Shape == SceneZoneShape.Circle || SceneZoneOverlap.PolygonContains(boundary.PolygonPoints, candidate));
            if (found)
            {
                DrawPointHandle(candidate, valid ? CenterToolColor : Color.red, 0.14f);
                Handles.Label(candidate, valid ? "Place center" : "Center must be inside boundary");
            }
            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt && found)
            {
                if (!valid)
                {
                    overlapError = "ERROR: The center point must be placed inside its boundary.";
                    ShowNotification(new GUIContent("Center point placement is invalid."));
                }
                else SetCenterPoint(candidate);
                current.Use();
            }
            view.Repaint();
        }

        private void HandleCircleTool(SceneView view, Event current, int control, bool found, Vector3 candidate)
        {
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && definingCircleRadius)
            {
                definingCircleRadius = false;
                movingPendingCircleCenter = false;
                ClearOverlapError();
                current.Use();
            }

            if (!definingCircleRadius)
            {
                if (found)
                {
                    DrawPointHandle(candidate, CenterToolColor, 0.14f);
                    Handles.Label(candidate, "Click circle center");
                }
                if (current.type == EventType.MouseDown && current.button == 0 && !current.alt && found)
                {
                    pendingCircleCenter = ToStoredPoint(candidate);
                    definingCircleRadius = true;
                    movingPendingCircleCenter = false;
                    blockedBy = null;
                    ClearOverlapError();
                    current.Use();
                }
                view.Repaint();
                return;
            }

            Vector3 displayedCenter = ToDisplayPoint(pendingCircleCenter);
            DrawPointHandle(displayedCenter, CenterToolColor, 0.15f);
            if (current.type == EventType.MouseDown && current.button == 1)
            {
                definingCircleRadius = false;
                movingPendingCircleCenter = false;
                blockedBy = null;
                ClearOverlapError();
                current.Use();
                view.Repaint();
                return;
            }
            if (current.type == EventType.MouseDown && current.button == 0 && current.shift && !current.alt
                && Vector2.Distance(current.mousePosition, HandleUtility.WorldToGUIPoint(displayedCenter)) <= 14f)
            {
                movingPendingCircleCenter = true;
                GUIUtility.hotControl = control;
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDrag && current.button == 0 && movingPendingCircleCenter)
            {
                if (found) pendingCircleCenter = ToStoredPoint(candidate);
                blockedBy = null;
                ClearOverlapError();
                current.Use();
                view.Repaint();
                return;
            }
            if (current.type == EventType.MouseUp && current.button == 0 && movingPendingCircleCenter)
            {
                movingPendingCircleCenter = false;
                GUIUtility.hotControl = 0;
                current.Use();
                view.Repaint();
                return;
            }
            if (!found) { view.Repaint(); return; }
            float radius = HorizontalDistance(pendingCircleCenter, candidate);
            SceneZoneAsset conflict = radius > 0.01f ? FindCircleConflict(pendingCircleCenter, radius) : null;
            if (conflict != blockedBy)
            {
                blockedBy = conflict;
                if (conflict == null) ClearOverlapError(); else SetCircleOverlapError(conflict);
                Repaint();
            }
            if (conflict != null) DrawCircleOverlapFill(pendingCircleCenter, radius);
            Handles.color = conflict == null ? CenterToolColor : Color.red;
            Handles.DrawDottedLine(displayedCenter, ToDisplayPoint(candidate), 4f);
            DrawCircleFill(pendingCircleCenter, radius, boundary.FillColor);
            DrawZoneCircle(pendingCircleCenter, radius, conflict == null ? CenterToolColor : Color.red, true);
            Handles.Label(candidate, conflict == null ? $"Radius {radius:0.##} - click to create" : $"Radius overlaps {conflict.DisplayName}");

            if (current.type == EventType.MouseDown && current.button == 0 && !current.shift && !current.alt)
            {
                if (radius <= 0.01f)
                {
                    overlapError = "ERROR: Circle radius must be greater than zero.";
                    ShowNotification(new GUIContent("Choose a larger circle radius."));
                }
                else if (conflict != null)
                {
                    blockedBy = conflict;
                    SetCircleOverlapError(conflict);
                    ShowNotification(new GUIContent($"Circle overlaps {conflict.DisplayName}."));
                }
                else
                {
                    SetCircle(pendingCircleCenter, radius);
                    definingCircleRadius = false;
                }
                current.Use();
            }
            view.Repaint();
        }

        private void DrawZoneCircle(Vector3 center, float radius, Color color, bool dotted)
        {
            if (radius <= 0.01f) return;
            const int segments = 64;
            Handles.color = color;
            Vector3 previous = ToDisplayPoint(center + Vector3.right * radius);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = ToDisplayPoint(center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                if (dotted) Handles.DrawDottedLine(previous, next, 3f);
                else Handles.DrawAAPolyLine(3f, previous, next);
                previous = next;
            }
        }

        private void DrawCircleOverlapFill(Vector3 center, float radius)
        {
            if (radius <= 0.01f || boundaryLayer == null) return;
            SceneZoneAsset[] overlapping = layerBoundaries
                .Where(item => item != null && item != boundary && SceneZoneOverlap.CircleOverlaps(center, radius, item))
                .ToArray();
            if (overlapping.Length == 0) return;

            const int angularSegments = 72;
            const int radialSegments = 12;
            Handles.color = new Color(1f, 0f, 0f, 0.32f);
            for (int radial = 0; radial < radialSegments; radial++)
            {
                float innerRadius = radius * radial / radialSegments;
                float outerRadius = radius * (radial + 1) / radialSegments;
                float sampleRadius = (innerRadius + outerRadius) * 0.5f;
                for (int angular = 0; angular < angularSegments; angular++)
                {
                    float startAngle = angular * Mathf.PI * 2f / angularSegments;
                    float endAngle = (angular + 1) * Mathf.PI * 2f / angularSegments;
                    float middleAngle = (startAngle + endAngle) * 0.5f;
                    Vector3 sample = center + new Vector3(Mathf.Cos(middleAngle) * sampleRadius, 0f, Mathf.Sin(middleAngle) * sampleRadius);
                    if (!overlapping.Any(item => SceneZoneOverlap.ContainsHorizontal(item, sample))) continue;

                    Vector3 outerStart = ToDisplayPoint(center + new Vector3(Mathf.Cos(startAngle) * outerRadius, 0f, Mathf.Sin(startAngle) * outerRadius)) + Vector3.up * 0.03f;
                    Vector3 outerEnd = ToDisplayPoint(center + new Vector3(Mathf.Cos(endAngle) * outerRadius, 0f, Mathf.Sin(endAngle) * outerRadius)) + Vector3.up * 0.03f;
                    if (radial == 0)
                    {
                        Handles.DrawAAConvexPolygon(ToDisplayPoint(center) + Vector3.up * 0.03f, outerStart, outerEnd);
                    }
                    else
                    {
                        Vector3 innerStart = ToDisplayPoint(center + new Vector3(Mathf.Cos(startAngle) * innerRadius, 0f, Mathf.Sin(startAngle) * innerRadius)) + Vector3.up * 0.03f;
                        Vector3 innerEnd = ToDisplayPoint(center + new Vector3(Mathf.Cos(endAngle) * innerRadius, 0f, Mathf.Sin(endAngle) * innerRadius)) + Vector3.up * 0.03f;
                        Handles.DrawAAConvexPolygon(innerStart, outerStart, outerEnd, innerEnd);
                    }
                }
            }
        }

        private void DrawCenterPoint()
        {
            if (!boundary.HasCenterPoint) return;
            Vector3 center = ToDisplayPoint(boundary.CenterPoint);
            DrawPointHandle(center, CenterToolColor, 0.15f);
            Handles.Label(center, "Center");
        }

        private void SetCenterPoint(Vector3 point)
        {
            point = ToStoredPoint(point);
            Undo.RecordObject(boundary, "Place Zone Center");
            SerializedObject serialized = new SerializedObject(boundary);
            serialized.FindProperty("hasCenterPoint").boolValue = true;
            serialized.FindProperty("centerPoint").vector2Value = new Vector2(point.x, point.z);
            if (boundary.Shape == SceneZoneShape.Circle) serialized.FindProperty("circleCenter").vector2Value = new Vector2(point.x, point.z);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(boundary);
            AssetDatabase.SaveAssetIfDirty(boundary);
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
        }

        internal void SetCircle(Vector3 center, float radius)
        {
            center = ToStoredPoint(center);
            Undo.RecordObject(boundary, "Create Zone Circle");
            SerializedObject serialized = new SerializedObject(boundary);
            serialized.FindProperty("hasCenterPoint").boolValue = true;
            serialized.FindProperty("centerPoint").vector2Value = new Vector2(center.x, center.z);
            serialized.FindProperty("circleCenter").vector2Value = new Vector2(center.x, center.z);
            serialized.FindProperty("circleRadius").floatValue = RoundToThreeDecimals(Mathf.Max(0.01f, radius));
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(boundary);
            PersistPendingZone();
            AssetDatabase.SaveAssetIfDirty(boundary);
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
        }

        private void BeginDrawing(bool clearExisting)
        {
            draftPoints.Clear();
            if (!clearExisting) draftPoints.AddRange(boundary.PolygonPoints);
            blockedBy = null;
            movingDraftPointIndex = -1;
            ClearOverlapError();
            drawing = true;
            SceneView.RepaintAll();
            Repaint();
        }

        internal void CommitDraft()
        {
            if (draftPoints.Count < 3) { ShowNotification(new GUIContent("At least three points are required.")); return; }
            if (!SceneZoneOverlap.ValidateSimplePolygon(draftPoints, out string geometryFailure))
            {
                overlapError = $"ERROR: {geometryFailure}";
                ShowNotification(new GUIContent(geometryFailure));
                return;
            }
            if (boundary.HasCenterPoint && !SceneZoneOverlap.PolygonContains(draftPoints, boundary.CenterPoint))
            {
                overlapError = "ERROR: The edited polygon would exclude its orange center point.";
                ShowNotification(new GUIContent("Polygon must contain its center point."));
                return;
            }
            SceneZoneAsset conflict = FindPolygonConflict(draftPoints, includeClosingSegment: true);
            if (conflict != null)
            {
                blockedBy = conflict;
                SetPolygonOverlapError(draftPoints, conflict);
                ShowNotification(new GUIContent($"Overlaps {conflict.DisplayName} on the same layer."));
                SceneView.RepaintAll();
                Repaint();
                return;
            }
            Undo.RecordObject(boundary, "Close Zone Boundary");
            SetPoints(draftPoints);
            PersistPendingZone();
            drawing = false;
            dragging = false;
            blockedBy = null;
            ClearOverlapError();
            draftPoints.Clear();
            Save();
            SceneView.RepaintAll();
            Repaint();
        }

        internal void CancelDrawing()
        {
            drawing = false;
            dragging = false;
            blockedBy = null;
            movingDraftPointIndex = -1;
            ClearOverlapError();
            draftPoints.Clear();
            if (pendingNewZone) CancelPendingZone();
            SceneView.RepaintAll();
            Repaint();
        }

        internal void AddDraft(Vector3 value)
        {
            value = ToStoredPoint(value);
            if (draftPoints.Count == 0)
            {
                blockedBy = null;
                ClearOverlapError();
                draftPoints.Add(value);
                Repaint();
                return;
            }

            SceneZoneAsset conflict = FindSegmentConflict(draftPoints[draftPoints.Count - 1], value);
            if (conflict != null)
            {
                blockedBy = conflict;
                SetSegmentOverlapError(draftPoints[draftPoints.Count - 1], value, conflict);
                Repaint();
                return;
            }
            blockedBy = null;
            ClearOverlapError();
            draftPoints.Add(value);
            Repaint();
        }

        internal void RemoveLast()
        {
            if (drawing)
            {
                if (draftPoints.Count > 0) draftPoints.RemoveAt(draftPoints.Count - 1);
                movingDraftPointIndex = -1;
                blockedBy = null;
                ClearOverlapError();
            }
            else if (boundary != null && boundary.PolygonPoints.Count > 0)
            {
                Undo.RecordObject(boundary, "Remove Zone Point");
                SetPoints(boundary.PolygonPoints.Take(boundary.PolygonPoints.Count - 1));
            }
            SceneView.RepaintAll();
            Repaint();
        }

        private static void DrawPointHandle(Vector3 position, Color color, float screenScale)
        {
            Color previousColor = Handles.color;
            CompareFunction previousDepthTest = Handles.zTest;
            Handles.color = color;
            Handles.zTest = CompareFunction.Always;
            float size = HandleUtility.GetHandleSize(position) * Mathf.Max(0.05f, screenScale);
            Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
            Handles.zTest = previousDepthTest;
            Handles.color = previousColor;
        }

        private void DrawOutline(IReadOnlyList<Vector3> points, bool close)
        {
            if (points == null || points.Count == 0) return;
            Vector3[] displayed = points.Select(ToDisplayPoint).ToArray();
            Color previousColor = Handles.color;
            CompareFunction previousDepthTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.color = boundary == null ? PolygonToolColor : boundary.LineColor;
            for (int i = 1; i < displayed.Length; i++) Handles.DrawAAPolyLine(3f, displayed[i - 1], displayed[i]);
            if (close && displayed.Length >= 3) Handles.DrawAAPolyLine(3f, displayed[displayed.Length - 1], displayed[0]);
            for (int i = 0; i < displayed.Length; i++)
            {
                DrawPointHandle(displayed[i], i == 0 ? Color.green : PolygonToolColor, i == 0 ? 0.15f : 0.12f);
            }
            Handles.zTest = previousDepthTest;
            Handles.color = previousColor;
        }

        private void DrawLayerOutlines()
        {
            if (!showLayerOutlines || boundaryLayer == null) return;
            foreach (SceneZoneAsset item in layerBoundaries)
            {
                if (item == null || (item == boundary && paintMode != SceneZonePaintMode.SelectZone)) continue;
                Handles.color = boundaryLayer.OutlineColor;
                if (item.Shape == SceneZoneShape.Circle)
                {
                    DrawCircleFill(item.CircleCenter, item.CircleRadius, item.FillColor);
                    DrawZoneCircle(item.CircleCenter, item.CircleRadius, boundaryLayer.OutlineColor, false);
                    DrawPointHandle(ToDisplayPoint(item.CircleCenter), InactivePointColor, 0.11f);
                    DrawZoneName(item);
                    continue;
                }
                DrawPolygonFill(item.PolygonPoints, item.FillColor);
                Vector3[] points = item.PolygonPoints.Select(ToDisplayPoint).ToArray();
                for (int i = 1; i < points.Length; i++) Handles.DrawAAPolyLine(2f, points[i - 1], points[i]);
                if (points.Length >= 3) Handles.DrawAAPolyLine(2f, points[points.Length - 1], points[0]);
                foreach (Vector3 point in points) DrawPointHandle(point, InactivePointColor, 0.09f);
                DrawZoneName(item);
            }
        }

        private void DrawCircleFill(Vector3 center, float radius, Color color)
        {
            if (radius <= 0.01f) return;
            const int segments = 64;
            Vector3[] points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = ToDisplayPoint(center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            DrawTranslucentConvexFill(points, color);
        }

        private void DrawPolygonFill(IReadOnlyList<Vector3> points, Color color)
        {
            if (points == null || points.Count < 3 || !SceneZoneOverlap.ValidateSimplePolygon(points, out _)) return;
            foreach (int[] triangle in Triangulate(points))
            {
                Vector3[] displayed = triangle.Select(index => ToDisplayPoint(points[index])).ToArray();
                DrawTranslucentConvexFill(displayed, color);
            }
        }

        private static void DrawTranslucentConvexFill(Vector3[] points, Color color)
        {
            if (points == null || points.Length < 3) return;
            Color previousColor = Handles.color;
            CompareFunction previousDepthTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.color = new Color(color.r, color.g, color.b, Mathf.Clamp(color.a, 0.04f, 0.22f));
            Handles.DrawAAConvexPolygon(points);
            Handles.zTest = previousDepthTest;
            Handles.color = previousColor;
        }

        private void DrawZoneName(SceneZoneAsset zone)
        {
            if (zone == null || !zone.IsUsable) return;
            zoneNameStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red },
                fontSize = 12
            };
            Handles.Label(ToDisplayPoint(zone.CalculatedCenter), zone.DisplayName, zoneNameStyle);
        }

        private static IEnumerable<int[]> Triangulate(IReadOnlyList<Vector3> points)
        {
            List<int[]> triangles = new List<int[]>();
            if (points == null || points.Count < 3) return triangles;
            List<int> remaining = Enumerable.Range(0, points.Count).ToList();
            float signedArea = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                signedArea += a.x * b.z - b.x * a.z;
            }
            bool counterClockwise = signedArea > 0f;
            int guard = points.Count * points.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int cursor = 0; cursor < remaining.Count; cursor++)
                {
                    int previous = remaining[(cursor - 1 + remaining.Count) % remaining.Count];
                    int current = remaining[cursor];
                    int next = remaining[(cursor + 1) % remaining.Count];
                    float turn = CrossXZ(points[previous], points[current], points[next]);
                    if (counterClockwise ? turn <= 0.0001f : turn >= -0.0001f) continue;
                    bool containsPoint = remaining.Any(index => index != previous && index != current && index != next
                        && PointInTriangleXZ(points[index], points[previous], points[current], points[next]));
                    if (containsPoint) continue;
                    triangles.Add(new[] { previous, current, next });
                    remaining.RemoveAt(cursor);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (remaining.Count == 3) triangles.Add(remaining.ToArray());
            return triangles;
        }

        private static bool PointInTriangleXZ(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            float first = CrossXZ(a, b, point);
            float second = CrossXZ(b, c, point);
            float third = CrossXZ(c, a, point);
            bool hasNegative = first < -0.0001f || second < -0.0001f || third < -0.0001f;
            bool hasPositive = first > 0.0001f || second > 0.0001f || third > 0.0001f;
            return !(hasNegative && hasPositive);
        }

        private static float CrossXZ(Vector3 a, Vector3 b, Vector3 point)
        {
            return (b.x - a.x) * (point.z - a.z) - (b.z - a.z) * (point.x - a.x);
        }

        private void DrawOverlapHighlights()
        {
            if (overlapHighlightSegments.Count < 2) return;
            Handles.color = Color.red;
            for (int i = 0; i + 1 < overlapHighlightSegments.Count; i += 2)
                Handles.DrawAAPolyLine(7f, ToDisplayPoint(overlapHighlightSegments[i]), ToDisplayPoint(overlapHighlightSegments[i + 1]));
        }

        private void DrawLivePreview()
        {
            if (!hasHover) return;
            Vector3 displayedHover = ToDisplayPoint(hoverPoint);
            if (hoveringReusablePoint)
            {
                DrawPointHandle(displayedHover, Color.white, 0.21f);
                DrawPointHandle(displayedHover, new Color(0.35f, 0.85f, 1f, 1f), 0.15f);
            }
            else
            {
                DrawPointHandle(displayedHover, PolygonToolColor, 0.15f);
            }
            if (draftPoints.Count == 0)
            {
                Handles.Label(displayedHover, hoveringReusablePoint ? "Attach to existing point" : "Place first point");
                return;
            }
            SceneZoneAsset nextEdgeConflict = FindSegmentConflict(draftPoints[draftPoints.Count - 1], hoverPoint);
            SceneZoneAsset closingEdgeConflict = draftPoints.Count >= 2 ? FindSegmentConflict(hoverPoint, draftPoints[0]) : null;
            Vector3 displayedLast = ToDisplayPoint(draftPoints[draftPoints.Count - 1]);
            CompareFunction previousDepthTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.color = nextEdgeConflict == null ? boundary.LineColor : Color.red;
            Handles.DrawDottedLine(displayedLast, displayedHover, 4f);
            if (draftPoints.Count >= 2)
            {
                Handles.color = closingEdgeConflict == null ? new Color(boundary.LineColor.r, boundary.LineColor.g, boundary.LineColor.b, 0.55f) : Color.red;
                Handles.DrawDottedLine(displayedHover, ToDisplayPoint(draftPoints[0]), 4f);
            }
            Handles.zTest = previousDepthTest;
            string label = hoveringReusablePoint && nextEdgeConflict == null && closingEdgeConflict == null
                ? "Attach to existing point"
                : nextEdgeConflict != null
                ? $"Next edge blocked by {nextEdgeConflict.DisplayName}"
                : closingEdgeConflict != null
                    ? $"Closing preview crosses {closingEdgeConflict.DisplayName}; add another point"
                    : draftPoints.Count >= 3 ? "Add point or press Enter to close" : "Click or drag";
            Handles.Label(displayedHover, label);
        }

        private bool TryFindReusablePointAtMouse(Vector2 mousePosition, out Vector3 point)
        {
            point = default;
            float nearestDistance = CloseHandlePixels;
            bool found = false;
            foreach (SceneZoneAsset zone in layerBoundaries)
            {
                if (zone == null || zone.Shape != SceneZoneShape.Polygon) continue;
                foreach (Vector3 candidate in zone.PolygonPoints)
                {
                    float distance = Vector2.Distance(mousePosition, HandleUtility.WorldToGUIPoint(ToDisplayPoint(candidate)));
                    if (distance > nearestDistance) continue;
                    nearestDistance = distance;
                    point = candidate;
                    found = true;
                }
            }
            return found;
        }

        private bool ShouldClose(Event current)
        {
            return draftPoints.Count >= 3 && (Vector2.Distance(HandleUtility.WorldToGUIPoint(ToDisplayPoint(draftPoints[0])), current.mousePosition) <= CloseHandlePixels || current.clickCount >= 2);
        }

        private bool TryScenePlanePoint(Vector2 guiPosition, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, displayHeight, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                point.y = displayHeight;
                return true;
            }
            point = default;
            return false;
        }

        private SceneZoneAsset FindSegmentConflict(Vector3 start, Vector3 end)
        {
            if (boundaryLayer == null || !boundaryLayer.PreventOverlap) return null;
            return layerBoundaries.FirstOrDefault(item => item != null && item != boundary && SceneZoneOverlap.SegmentEnters(start, end, item));
        }

        private int FindPointAtMouse(Vector2 mousePosition, IReadOnlyList<Vector3> points, float maximumPixels)
        {
            int nearestIndex = -1;
            float nearestDistance = maximumPixels;
            for (int i = 0; i < points.Count; i++)
            {
                float distance = Vector2.Distance(mousePosition, HandleUtility.WorldToGUIPoint(ToDisplayPoint(points[i])));
                if (distance > nearestDistance) continue;
                nearestDistance = distance;
                nearestIndex = i;
            }
            return nearestIndex;
        }

        private SceneZoneAsset FindPolygonConflict(IReadOnlyList<Vector3> points, bool includeClosingSegment)
        {
            if (boundaryLayer == null || !boundaryLayer.PreventOverlap || points == null || points.Count < 2) return null;
            foreach (SceneZoneAsset item in layerBoundaries.Where(item => item != null && item != boundary))
            {
                for (int i = 1; i < points.Count; i++) if (SceneZoneOverlap.SegmentEnters(points[i - 1], points[i], item)) return item;
                if (includeClosingSegment && points.Count >= 3 && SceneZoneOverlap.SegmentEnters(points[points.Count - 1], points[0], item)) return item;
                if (points.Count >= 3 && SceneZoneOverlap.PolygonOverlaps(points, item)) return item;
            }
            return null;
        }

        private SceneZoneAsset FindCircleConflict(Vector3 center, float radius)
        {
            if (boundaryLayer == null || !boundaryLayer.PreventOverlap) return null;
            return layerBoundaries.FirstOrDefault(item => item != null && item != boundary && SceneZoneOverlap.CircleOverlaps(center, radius, item));
        }

        private void SetPolygonOverlapError(IReadOnlyList<Vector3> candidate, SceneZoneAsset conflict)
        {
            ClearOverlapError();
            IReadOnlyList<SceneZoneSegmentConflict> details = SceneZoneOverlap.FindPolygonConflicts(candidate, conflict);
            List<string> messages = new List<string>();
            foreach (SceneZoneSegmentConflict detail in details)
            {
                string candidateSegment = detail.CandidateSegmentIndex >= 0 ? SegmentCoordinates(candidate, detail.CandidateSegmentIndex) : string.Empty;
                string existingSegment = detail.ExistingSegmentIndex >= 0 ? SegmentCoordinates(conflict.PolygonPoints, detail.ExistingSegmentIndex) : string.Empty;
                switch (detail.Kind)
                {
                    case SceneZoneOverlapKind.CrossingSegments:
                    case SceneZoneOverlapKind.CollinearSegments:
                        messages.Add($"Draft {candidateSegment} overlaps {conflict.DisplayName} {existingSegment}.");
                        Highlight(candidate, detail.CandidateSegmentIndex);
                        Highlight(conflict.PolygonPoints, detail.ExistingSegmentIndex);
                        break;
                    case SceneZoneOverlapKind.CandidateSegmentInsideExisting:
                        messages.Add($"Draft {candidateSegment} lies inside {conflict.DisplayName}.");
                        Highlight(candidate, detail.CandidateSegmentIndex);
                        break;
                    case SceneZoneOverlapKind.ExistingSegmentInsideCandidate:
                        messages.Add($"{conflict.DisplayName} {existingSegment} lies inside the draft area.");
                        Highlight(conflict.PolygonPoints, detail.ExistingSegmentIndex);
                        break;
                    case SceneZoneOverlapKind.CandidateSegmentIntersectsCircle:
                        messages.Add($"Draft {candidateSegment} overlaps circle {conflict.DisplayName}.");
                        Highlight(candidate, detail.CandidateSegmentIndex);
                        break;
                    case SceneZoneOverlapKind.CircleInsideCandidate:
                        messages.Add($"Circle {conflict.DisplayName} lies inside the draft area.");
                        break;
                }
            }
            overlapError = FormatOverlapError(messages, conflict);
        }

        private void SetSegmentOverlapError(Vector3 start, Vector3 end, SceneZoneAsset conflict)
        {
            ClearOverlapError();
            IReadOnlyList<SceneZoneSegmentConflict> details = SceneZoneOverlap.FindSegmentConflicts(start, end, conflict);
            List<string> messages = new List<string>();
            string draftSegment = SceneZoneOverlap.FormatSegmentCoordinates(start, end);
            overlapHighlightSegments.Add(start);
            overlapHighlightSegments.Add(end);
            foreach (SceneZoneSegmentConflict detail in details)
            {
                if (detail.ExistingSegmentIndex >= 0)
                {
                    string existingSegment = SegmentCoordinates(conflict.PolygonPoints, detail.ExistingSegmentIndex);
                    messages.Add($"Draft {draftSegment} overlaps {conflict.DisplayName} {existingSegment}.");
                    Highlight(conflict.PolygonPoints, detail.ExistingSegmentIndex);
                }
                else if (conflict.Shape == SceneZoneShape.Circle)
                    messages.Add($"Draft {draftSegment} overlaps circle {conflict.DisplayName}.");
                else
                    messages.Add($"Draft {draftSegment} lies inside {conflict.DisplayName}.");
            }
            overlapError = FormatOverlapError(messages, conflict);
        }

        private void SetCircleOverlapError(SceneZoneAsset conflict)
        {
            ClearOverlapError();
            overlapError = $"ERROR: Overlap detected\nCircle {boundary.DisplayName} overlaps {conflict.DisplayName}.";
        }

        private void ClearOverlapError()
        {
            overlapError = string.Empty;
            overlapHighlightSegments.Clear();
        }

        private void Highlight(IReadOnlyList<Vector3> points, int segmentIndex)
        {
            if (points == null || points.Count == 0 || segmentIndex < 0) return;
            overlapHighlightSegments.Add(points[segmentIndex % points.Count]);
            overlapHighlightSegments.Add(points[(segmentIndex + 1) % points.Count]);
        }

        private static string FormatOverlapError(IEnumerable<string> messages, SceneZoneAsset conflict)
        {
            string[] distinct = (messages ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            return distinct.Length == 0
                ? $"ERROR: Overlap detected with {conflict.DisplayName}."
                : $"ERROR: Overlapping segments:\n{string.Join("\n", distinct.Select(value => $"- {value}"))}";
        }

        private static string SegmentCoordinates(IReadOnlyList<Vector3> points, int segmentIndex)
        {
            if (points == null || points.Count == 0 || segmentIndex < 0) return "segment [?]";
            return SceneZoneOverlap.FormatSegmentCoordinates(
                points[segmentIndex % points.Count],
                points[(segmentIndex + 1) % points.Count]);
        }

        private void SetPoints(IEnumerable<Vector3> values)
        {
            Vector3[] points = (values ?? Array.Empty<Vector3>()).Select(ToStoredPoint).ToArray();
            SerializedObject serialized = new SerializedObject(boundary);
            SerializedProperty property = serialized.FindProperty("polygonPoints");
            property.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) property.GetArrayElementAtIndex(i).vector2Value = new Vector2(points[i].x, points[i].z);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(boundary);
        }

        private void Save()
        {
            if (boundary == null || pendingNewZone || !AssetDatabase.Contains(boundary)) return;
            EditorUtility.SetDirty(boundary);
            AssetDatabase.SaveAssetIfDirty(boundary);
        }

        internal bool HasPendingZone => pendingNewZone;
        internal SceneZoneAsset PendingZone => pendingNewZone ? boundary : null;
        internal SceneZoneAsset ActiveZone => boundary;
        internal SceneZonePaintMode PaintMode => paintMode;
        internal int DraftPointCount => draftPoints.Count;
        internal IReadOnlyList<Vector3> DraftPoints => draftPoints;

        internal bool BeginRedrawingSelectedPolygon()
        {
            if (boundary == null || pendingNewZone || boundary.Shape != SceneZoneShape.Polygon) return false;
            paintMode = SceneZonePaintMode.PolygonPoints;
            BeginDrawing(clearExisting: true);
            return true;
        }

        internal bool TryApplyPolygonPoints(IEnumerable<Vector3> values)
        {
            if (boundary == null || boundary.Shape != SceneZoneShape.Polygon) return false;
            Vector3[] points = (values ?? Array.Empty<Vector3>()).Select(ToStoredPoint).ToArray();
            if (!SceneZoneOverlap.ValidateSimplePolygon(points, out string geometryFailure))
            {
                overlapError = $"ERROR: {geometryFailure}";
                return false;
            }
            if (boundary.HasCenterPoint && !SceneZoneOverlap.PolygonContains(points, boundary.CenterPoint))
            {
                overlapError = "ERROR: Moving these points would exclude the orange center point.";
                return false;
            }
            SceneZoneAsset conflict = FindPolygonConflict(points, includeClosingSegment: true);
            if (conflict != null)
            {
                blockedBy = conflict;
                SetPolygonOverlapError(points, conflict);
                return false;
            }

            Undo.RecordObject(boundary, "Move Zone Points");
            SetPoints(points);
            Save();
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
            return true;
        }

        internal bool TryInsertPolygonPointAfter(int pointIndex, Vector3 value)
        {
            if (boundary == null || boundary.Shape != SceneZoneShape.Polygon) return false;
            if (pointIndex < 0 || pointIndex >= boundary.PolygonPoints.Count) return false;
            return TryApplyPolygonPoints(InsertPointAfter(boundary.PolygonPoints, pointIndex, ToStoredPoint(value)));
        }

        private static Vector3[] InsertPointAfter(IReadOnlyList<Vector3> points, int pointIndex, Vector3 value)
        {
            List<Vector3> inserted = new List<Vector3>((points?.Count ?? 0) + 1);
            if (points == null) return inserted.ToArray();
            for (int i = 0; i < points.Count; i++)
            {
                inserted.Add(points[i]);
                if (i == pointIndex) inserted.Add(value);
            }
            return inserted.ToArray();
        }

        private static Vector3 DefaultInsertedPoint(IReadOnlyList<Vector3> points, int pointIndex)
        {
            if (points == null || points.Count == 0 || pointIndex < 0 || pointIndex >= points.Count) return Vector3.zero;
            Vector3 current = points[pointIndex];
            Vector3 next = points[(pointIndex + 1) % points.Count];
            return ToStoredPoint((current + next) * 0.5f);
        }

        internal void BeginNewZone(SceneZoneShape shape)
        {
            if (boundaryLayer == null) { ShowNotification(new GUIContent("Create or select a zone layer first.")); return; }
            if (!LayerMatchesActiveScene(boundaryLayer)) { ShowNotification(new GUIContent("The selected layer belongs to another scene.")); return; }
            DiscardIncompleteActiveZone();
            int ordinal = 1;
            HashSet<string> names = layerBoundaries.Where(value => value != null).Select(value => value.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string name;
            do name = $"Zone {ordinal++}"; while (names.Contains(name));
            string layerSuffix = Sanitize(boundaryLayer.LayerId.Replace("zone-layer.", string.Empty, StringComparison.Ordinal));
            SceneZoneAsset created = CreateInstance<SceneZoneAsset>();
            created.name = name;
            created.hideFlags = HideFlags.HideAndDontSave;
            created.Configure($"zone.{layerSuffix}.{Sanitize(name)}", name, shape, radius: shape == SceneZoneShape.Circle ? 0f : 25f, authoredLayer: boundaryLayer);
            created.AssignFillColor(CreateUniqueFillColor());
            boundary = created;
            pendingNewZone = true;
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

        internal void SelectBoundary(SceneZoneAsset selected)
        {
            if (drawing) CancelDrawing();
            else if (pendingNewZone) CancelPendingZone();
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            boundary = selected;
            bool layerChanged = selected != null && selected.BoundaryLayer != boundaryLayer;
            if (selected != null) boundaryLayer = selected.BoundaryLayer;
            blockedBy = null;
            ClearOverlapError();
            if (layerChanged) RefreshLayerBoundaries();
            Repaint();
            SceneView.RepaintAll();
        }

        private void SelectLayer(SceneZoneLayerAsset selected)
        {
            if (drawing) CancelDrawing();
            else if (pendingNewZone) CancelPendingZone();
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            boundaryLayer = selected;
            blockedBy = null;
            ClearOverlapError();
            RefreshLayerBoundaries();
            boundary = layerBoundaries.FirstOrDefault();
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
            if (paintMode == selected) return;
            if (drawing) CancelDrawing();
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            paintMode = selected;
            blockedBy = null;
            ClearOverlapError();
            Repaint();
            SceneView.RepaintAll();
        }

        private void CreateLayer() => PromptCreateLayer();

        internal bool PromptCreateLayer()
        {
            EnsureFolder(DefaultFolder);
            string path = EditorUtility.SaveFilePanelInProject("Create Zone Layer", "zones", "asset", "Choose where to save the reusable zone layer.", DefaultFolder);
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
            if (boundary == null) return;
            if (pendingNewZone) { CancelPendingZone(); return; }
            string dependentWarning = FormatDependencyWarning(new[] { boundary });
            if (!EditorUtility.DisplayDialog("Delete Zone", $"Delete zone '{boundary.DisplayName}' from layer '{boundaryLayer.DisplayName}'? This can be undone until Unity closes.{dependentWarning}", "Delete", "Cancel")) return;
            DeleteZoneInternal(boundary);
        }

        private void DeleteSelectedLayer()
        {
            if (boundaryLayer == null) return;
            string path = AssetDatabase.GetAssetPath(boundaryLayer);
            if (string.IsNullOrWhiteSpace(path)) return;
            string layerGuid = AssetDatabase.AssetPathToGUID(path);
            string dependentWarning = FormatDependencyWarning(new UnityEngine.Object[] { boundaryLayer }.Concat(layerBoundaries));
            if (!EditorUtility.DisplayDialog(
                    "Delete Zone Layer",
                    $"Move layer '{boundaryLayer.DisplayName}' and all {layerBoundaries.Count} zone(s) to the operating system's Recycle Bin?{dependentWarning}",
                    "Move To Recycle Bin",
                    "Cancel")) return;

            if (!AssetDatabase.MoveAssetToTrash(path))
            {
                EditorUtility.DisplayDialog("Could Not Delete Layer", $"Unity could not move '{path}' to the Recycle Bin.", "OK");
                return;
            }

            drawing = false;
            definingCircleRadius = false;
            draftPoints.Clear();
            boundary = null;
            boundaryLayer = null;
            layerBoundaries.Clear();
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
            if (!pendingNewZone || boundary == null || boundaryLayer == null) return;
            boundary.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(boundary, boundaryLayer);
            pendingNewZone = false;
            SetLayerZones(layerBoundaries.Append(boundary));
            EditorUtility.SetDirty(boundary);
            AssetDatabase.SaveAssets();
            RefreshLayerBoundaries();
        }

        private void CancelPendingZone()
        {
            if (!pendingNewZone) return;
            SceneZoneAsset pending = boundary;
            pendingNewZone = false;
            drawing = false;
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            draftPoints.Clear();
            boundary = layerBoundaries.FirstOrDefault();
            if (pending != null) DestroyImmediate(pending);
            ClearOverlapError();
        }

        internal void DeleteZoneInternal(SceneZoneAsset zone)
        {
            if (zone == null) return;
            if (pendingNewZone && zone == boundary) { CancelPendingZone(); return; }
            SceneZoneLayerAsset owner = zone.BoundaryLayer ?? boundaryLayer;
            if (owner != null)
            {
                SceneZoneAsset[] remaining = owner.Zones.Where(value => value != null && value != zone).ToArray();
                SceneZoneLayerAsset previousLayer = boundaryLayer;
                boundaryLayer = owner;
                SetLayerZones(remaining);
                boundaryLayer = previousLayer;
            }
            zoneRenameDrafts.Remove(zone);
            Undo.DestroyObjectImmediate(zone);
            if (owner != null) EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();
            boundaryLayer = owner;
            RefreshLayerBoundaries();
            boundary = layerBoundaries.FirstOrDefault();
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

        private void RefreshLayerBoundaries()
        {
            layerBoundaries.Clear();
            if (boundaryLayer == null) return;
            layerBoundaries.AddRange(boundaryLayer.Zones.Where(value => value != null && value.BoundaryLayer == boundaryLayer));
            layerBoundaries.Sort((left, right) => string.Compare(left.BoundaryId, right.BoundaryId, StringComparison.Ordinal));
            NormalizeUniqueFillColors();
        }

        private Color CreateUniqueFillColor()
        {
            HashSet<Color32> used = layerBoundaries.Where(zone => zone != null).Select(zone => (Color32)zone.FillColor).ToHashSet();
            System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
            float hue = (float)random.NextDouble();
            for (int attempt = 0; attempt < 64; attempt++)
            {
                hue = Mathf.Repeat(hue + 0.61803398875f, 1f);
                Color candidate = Color.HSVToRGB(hue, 0.58f + (attempt % 3) * 0.1f, 0.9f);
                candidate.a = 0.16f;
                if (!used.Contains((Color32)candidate)) return candidate;
            }
            Color fallback = Color.HSVToRGB(Mathf.Repeat(layerBoundaries.Count * 0.137f, 1f), 0.8f, 0.95f);
            fallback.a = 0.16f;
            return fallback;
        }

        private void NormalizeUniqueFillColors()
        {
            if (boundaryLayer == null || layerBoundaries.Count == 0) return;
            HashSet<Color32> used = new HashSet<Color32>();
            bool changed = false;
            foreach (SceneZoneAsset zone in layerBoundaries)
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
            if (changed && AssetDatabase.Contains(boundaryLayer)) AssetDatabase.SaveAssetIfDirty(boundaryLayer);
        }

        private void SetLayerZones(IEnumerable<SceneZoneAsset> values)
        {
            if (boundaryLayer == null) return;
            SceneZoneAsset[] zones = (values ?? Array.Empty<SceneZoneAsset>()).Where(value => value != null).Distinct().ToArray();
            Undo.RecordObject(boundaryLayer, "Update Zone Layer");
            SerializedObject serialized = new SerializedObject(boundaryLayer);
            SerializedProperty property = serialized.FindProperty("zones");
            property.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(boundaryLayer);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // Assets intentionally store only world X/Z. The editor plane height is visual authoring
        // state and never becomes part of zone identity, persistence, or containment tests.
        private static Vector3 ToStoredPoint(Vector3 point) => new Vector3(RoundToThreeDecimals(point.x), 0f, RoundToThreeDecimals(point.z));

        private static float RoundToThreeDecimals(float value) => Mathf.Round(value * 1000f) / 1000f;

        private Vector3 ToDisplayPoint(Vector3 storedPoint)
        {
            return new Vector3(storedPoint.x, displayHeight, storedPoint.z);
        }

        private void RestorePreferredLayer()
        {
            boundaryLayer = SceneZoneToolProjectSettings.instance.LoadLayerForActiveScene();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string cursor = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cursor}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cursor, parts[i]);
                cursor = next;
            }
        }

        private static string Sanitize(string value) => new string((value ?? "boundary").ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
    }

    [CustomEditor(typeof(SceneZoneAsset))]
    public sealed class SceneZoneAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("boundaryId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
                EditorGUILayout.ObjectField("Zone Layer", ((SceneZoneAsset)target).BoundaryLayer, typeof(SceneZoneLayerAsset), false);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shape"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("hasCenterPoint"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("centerPoint"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("circleCenter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("circleRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("polygonPoints"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("version"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("notes"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lineColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lineWidth"));
            serializedObject.ApplyModifiedProperties();
            SceneZoneAsset zone = (SceneZoneAsset)target;
            if (!zone.Validate(out string failure)) EditorGUILayout.HelpBox(failure, MessageType.Error);
            EditorGUILayout.Space();
            if (GUILayout.Button("Use In Scene Zone Overlay")) SceneZoneOverlay.Open(zone.BoundaryLayer);
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
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(zone.DisplayName, zone, typeof(SceneZoneAsset), false);
            }
            string assetPath = AssetDatabase.GetAssetPath(layer);
            SceneZoneAsset[] embeddedZones = string.IsNullOrWhiteSpace(assetPath)
                ? Array.Empty<SceneZoneAsset>()
                : AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<SceneZoneAsset>()
                    .Where(zone => zone != null && zone.BoundaryLayer == layer)
                    .OrderBy(zone => zone.BoundaryId, StringComparer.Ordinal)
                    .ToArray();
            bool listNeedsRepair = embeddedZones.Length != layer.Zones.Count
                || embeddedZones.Any(zone => !layer.Zones.Contains(zone));
            if (listNeedsRepair)
            {
                EditorGUILayout.HelpBox("The layer's zone list does not match its embedded zone sub-assets.", MessageType.Warning);
                if (GUILayout.Button("Repair Embedded Zone List"))
                {
                    Undo.RecordObject(layer, "Repair Embedded Zone List");
                    SerializedObject layerObject = new SerializedObject(layer);
                    SerializedProperty zonesProperty = layerObject.FindProperty("zones");
                    zonesProperty.arraySize = embeddedZones.Length;
                    for (int i = 0; i < embeddedZones.Length; i++) zonesProperty.GetArrayElementAtIndex(i).objectReferenceValue = embeddedZones[i];
                    layerObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(layer);
                    AssetDatabase.SaveAssetIfDirty(layer);
                }
            }
            if (GUILayout.Button("Use In Scene Zone Overlay")) SceneZoneOverlay.Open(layer);
        }
    }
}
