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
        private void OnSceneGUI(SceneView view)
        {
            if (!overlayVisible || view != owningSceneView || zoneLayer == null) return;
            DrawLayerOutlines();
            if (paintMode == SceneZonePaintMode.SelectZone)
            {
                HandleSelectZoneTool(view);
                return;
            }
            if (zone == null) return;
            DrawCenterPoint();
            if (zone.Shape == SceneZoneShape.Circle)
            {
                DrawCircleFill(zone.CircleCenter, zone.CircleRadius, zone.FillColor);
                Color circleColor = pendingNewZone || definingCircleRadius ? zone.LineColor : selectedZoneColor;
                DrawZoneCircle(zone.CircleCenter, zone.CircleRadius, circleColor, false, pendingNewZone ? 3f : 4f);
                DrawZoneName(zone);
            }
            if (paintMode == SceneZonePaintMode.CenterPoint)
            {
                HandleCenterPointTool(view);
                return;
            }
            IReadOnlyList<Vector3> displayed = zone.Shape == SceneZoneShape.Polygon && drawing
                ? draftPoints
                : movingPointIndex >= 0 && movingPointDraft.Length > 0 ? movingPointDraft : zone.PolygonPoints;
            if (zone.Shape == SceneZoneShape.Polygon) DrawPolygonFill(displayed, zone.FillColor);
            DrawOutline(displayed, close: !drawing);
            if (zone.Shape == SceneZoneShape.Polygon) DrawZoneName(zone);
            DrawOverlapHighlights();
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
            if (YieldToSceneViewNavigation(view)) return;
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
            if (zone.Shape != SceneZoneShape.Polygon || zone.PolygonPoints.Count == 0) return;
            Event current = Event.current;
            if (YieldToSceneViewNavigation(view)) return;
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
                int nearest = FindPointAtMouse(current.mousePosition, zone.PolygonPoints, 14f);
                if (nearest >= 0)
                {
                    insertingPoint = current.shift;
                    if (insertingPoint)
                    {
                        movingPointIndex = nearest + 1;
                        movingPointDraft = InsertPointAfter(zone.PolygonPoints, nearest, DefaultInsertedPoint(zone.PolygonPoints, nearest));
                    }
                    else
                    {
                        movingPointIndex = nearest;
                        movingPointDraft = zone.PolygonPoints.ToArray();
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
                    else if (zone.HasCenterPoint && !SceneZoneOverlap.PolygonContains(movingPointDraft, zone.CenterPoint))
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
                    ShowNotification(new GUIContent("The inserted point would make this zone invalid."));
                movingPointIndex = -1;
                movingPointDraft = Array.Empty<Vector3>();
                insertingPoint = false;
                GUIUtility.hotControl = 0;
                current.Use();
            }

            IReadOnlyList<Vector3> points = movingPointIndex >= 0 && movingPointDraft.Length > 0 ? movingPointDraft : zone.PolygonPoints;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 displayedPoint = ToDisplayPoint(points[i]);
                DrawPointHandle(displayedPoint, i == movingPointIndex ? hoveredZoneColor : selectedZoneColor, 0.13f);
            }
            view.Repaint();
        }

        private void HandleSelectZoneTool(SceneView view)
        {
            Event current = Event.current;
            if (YieldToSceneViewNavigation(view)) return;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);
            bool found = TryScenePlanePoint(current.mousePosition, out Vector3 candidate);
            SceneZoneAsset hovered = found
                ? sceneLayers
                    .SelectMany(layer => layer.Zones)
                    .Where(value => value != null && value.Contains(candidate))
                    .Where(IsZoneVisible)
                    .OrderBy(value => value.ApproximateHorizontalArea())
                    .FirstOrDefault()
                : null;

            if (hovered != null)
            {
                Color previous = Handles.color;
                Handles.color = hoveredZoneColor;
                if (hovered.Shape == SceneZoneShape.Circle)
                    DrawZoneCircle(hovered.CircleCenter, hovered.CircleRadius, hoveredZoneColor, false, 5f);
                else
                {
                    Vector3[] points = hovered.PolygonPoints.Select(ToDisplayPoint).ToArray();
                    for (int i = 1; i < points.Length; i++) Handles.DrawAAPolyLine(5f, points[i - 1], points[i]);
                    if (points.Length >= 3) Handles.DrawAAPolyLine(5f, points[points.Length - 1], points[0]);
                }
                Handles.Label(ToDisplayPoint(candidate), $"Select {hovered.DisplayName}");
                Handles.color = previous;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (hovered != null) SelectZone(hovered);
                current.Use();
            }
            view.Repaint();
        }

        private void HandleCenterPointTool(SceneView view)
        {
            Event current = Event.current;
            if (YieldToSceneViewNavigation(view)) return;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);
            bool found = TryScenePlanePoint(current.mousePosition, out Vector3 candidate);
            if (zone.Shape == SceneZoneShape.Circle)
            {
                // A completed circle is selected data, not an implicit edit target.
                // Circle geometry may only change through a pending new-zone draft
                // or the explicit Replace Selected Outline action.
                if (!pendingNewZone && !replacingCircle) return;
                HandleCircleTool(view, current, control, found, candidate);
                return;
            }
            bool valid = found && (zone.Shape == SceneZoneShape.Circle || SceneZoneOverlap.PolygonContains(zone.PolygonPoints, candidate));
            if (found)
            {
                DrawPointHandle(candidate, valid ? centerToolColor : Color.red, 0.14f);
                Handles.Label(candidate, valid ? "Place center" : "Center must be inside zone");
            }
            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt && found)
            {
                if (!valid)
                {
                    overlapError = "ERROR: The center point must be placed inside its zone.";
                    ShowNotification(new GUIContent("Center point placement is invalid."));
                }
                else SetCenterPoint(candidate);
                current.Use();
            }
            view.Repaint();
        }

        internal static bool ShouldYieldSceneInput(Tool currentTool, bool viewToolActive)
        {
            return currentTool != Tool.None || viewToolActive;
        }

        private bool YieldToSceneViewNavigation(SceneView view)
        {
            if (!ShouldYieldSceneInput(Tools.current, Tools.viewToolActive)) return false;
            ReleaseActiveMouseInteraction();
            hasHover = false;
            hoveringReusablePoint = false;
            view.Repaint();
            return true;
        }

        private void ReleaseActiveMouseInteraction()
        {
            bool ownsMouse = dragging || movingDraftPointIndex >= 0 || movingPendingCircleCenter || movingPointIndex >= 0;
            dragging = false;
            movingDraftPointIndex = -1;
            movingPendingCircleCenter = false;
            movingPointIndex = -1;
            movingPointDraft = Array.Empty<Vector3>();
            insertingPoint = false;
            if (ownsMouse) GUIUtility.hotControl = 0;
        }

        private void HandleCircleTool(SceneView view, Event current, int control, bool found, Vector3 candidate)
        {
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && (definingCircleRadius || replacingCircle))
            {
                definingCircleRadius = false;
                movingPendingCircleCenter = false;
                if (replacingCircle)
                {
                    replacingCircle = false;
                    paintMode = SceneZonePaintMode.SelectZone;
                }
                ClearOverlapError();
                current.Use();
                view.Repaint();
                return;
            }

            if (!definingCircleRadius)
            {
                if (found)
                {
                    DrawPointHandle(candidate, centerToolColor, 0.14f);
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
            DrawPointHandle(displayedCenter, centerToolColor, 0.15f);
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
            Handles.color = conflict == null ? centerToolColor : Color.red;
            Handles.DrawDottedLine(displayedCenter, ToDisplayPoint(candidate), 4f);
            DrawCircleFill(pendingCircleCenter, radius, zone.FillColor);
            DrawZoneCircle(pendingCircleCenter, radius, conflict == null ? centerToolColor : Color.red, true);
            string circleAction = replacingCircle ? "replace" : "create";
            Handles.Label(candidate, conflict == null ? $"Radius {radius:0.##} - click to {circleAction}" : $"Radius overlaps {conflict.DisplayName}");

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

        private void DrawZoneCircle(Vector3 center, float radius, Color color, bool dotted, float width = 3f)
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
                else Handles.DrawAAPolyLine(width, previous, next);
                previous = next;
            }
        }

        private void DrawCircleOverlapFill(Vector3 center, float radius)
        {
            if (radius <= 0.01f || zoneLayer == null) return;
            SceneZoneAsset[] overlapping = layerZones
                .Where(item => item != null && item != zone && SceneZoneOverlap.CircleOverlaps(center, radius, item))
                .ToArray();
            if (overlapping.Length == 0) return;

            const int circleSegments = 128;
            List<Vector2> candidateCircle = CreateCirclePolygon(center, radius, circleSegments);
            foreach (SceneZoneAsset overlap in overlapping)
            {
                if (overlap.Shape == SceneZoneShape.Circle)
                {
                    List<Vector2> existingCircle = CreateCirclePolygon(overlap.CircleCenter, overlap.CircleRadius, circleSegments);
                    DrawSmoothOverlapPolygon(ClipConvexPolygon(candidateCircle, existingCircle));
                    continue;
                }

                IReadOnlyList<Vector3> points = overlap.PolygonPoints;
                foreach (int[] triangle in Triangulate(points))
                {
                    Vector2[] clipTriangle = triangle
                        .Select(index => new Vector2(points[index].x, points[index].z))
                        .ToArray();
                    DrawSmoothOverlapPolygon(ClipConvexPolygon(candidateCircle, clipTriangle));
                }
            }
        }

        private static List<Vector2> CreateCirclePolygon(Vector3 center, float radius, int segments)
        {
            List<Vector2> points = new List<Vector2>(segments);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points.Add(new Vector2(center.x + Mathf.Cos(angle) * radius, center.z + Mathf.Sin(angle) * radius));
            }
            return points;
        }

        internal static List<Vector2> ClipConvexPolygon(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip)
        {
            List<Vector2> output = subject?.ToList() ?? new List<Vector2>();
            if (output.Count < 3 || clip == null || clip.Count < 3) return new List<Vector2>();

            float signedArea = 0f;
            for (int i = 0; i < clip.Count; i++)
            {
                Vector2 a = clip[i];
                Vector2 b = clip[(i + 1) % clip.Count];
                signedArea += a.x * b.y - b.x * a.y;
            }
            bool counterClockwise = signedArea >= 0f;

            for (int edge = 0; edge < clip.Count && output.Count > 0; edge++)
            {
                Vector2 edgeStart = clip[edge];
                Vector2 edgeEnd = clip[(edge + 1) % clip.Count];
                List<Vector2> input = output;
                output = new List<Vector2>(input.Count + 1);
                Vector2 previous = input[input.Count - 1];
                bool previousInside = IsInsideClipEdge(previous, edgeStart, edgeEnd, counterClockwise);
                foreach (Vector2 current in input)
                {
                    bool currentInside = IsInsideClipEdge(current, edgeStart, edgeEnd, counterClockwise);
                    if (currentInside != previousInside)
                        output.Add(IntersectLines(previous, current, edgeStart, edgeEnd));
                    if (currentInside) output.Add(current);
                    previous = current;
                    previousInside = currentInside;
                }
            }
            return output;
        }

        private static bool IsInsideClipEdge(Vector2 point, Vector2 start, Vector2 end, bool counterClockwise)
        {
            float cross = (end.x - start.x) * (point.y - start.y) - (end.y - start.y) * (point.x - start.x);
            return counterClockwise ? cross >= -0.0001f : cross <= 0.0001f;
        }

        private static Vector2 IntersectLines(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
        {
            Vector2 firstDirection = firstEnd - firstStart;
            Vector2 secondDirection = secondEnd - secondStart;
            float denominator = firstDirection.x * secondDirection.y - firstDirection.y * secondDirection.x;
            if (Mathf.Abs(denominator) <= 0.000001f) return firstEnd;
            Vector2 delta = secondStart - firstStart;
            float t = (delta.x * secondDirection.y - delta.y * secondDirection.x) / denominator;
            return firstStart + firstDirection * t;
        }

        private void DrawSmoothOverlapPolygon(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3) return;
            Vector3[] displayed = points
                .Select(point => ToDisplayPoint(new Vector3(point.x, 0f, point.y)) + Vector3.up * 0.03f)
                .ToArray();
            Color previousColor = Handles.color;
            CompareFunction previousDepthTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.color = new Color(1f, 0f, 0f, 0.3f);
            Handles.DrawAAConvexPolygon(displayed);
            Handles.zTest = previousDepthTest;
            Handles.color = previousColor;
        }

        private void DrawCenterPoint()
        {
            if (!zone.HasCenterPoint) return;
            Vector3 center = ToDisplayPoint(zone.CenterPoint);
            DrawPointHandle(center, centerToolColor, 0.15f);
        }

        private void SetCenterPoint(Vector3 point)
        {
            point = ToStoredPoint(point);
            Undo.RecordObject(zone, "Place Zone Center");
            SerializedObject serialized = new SerializedObject(zone);
            serialized.FindProperty("hasCenterPoint").boolValue = true;
            serialized.FindProperty("centerPoint").vector2Value = new Vector2(point.x, point.z);
            if (zone.Shape == SceneZoneShape.Circle) serialized.FindProperty("circleCenter").vector2Value = new Vector2(point.x, point.z);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(zone);
            AssetDatabase.SaveAssetIfDirty(zone);
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
        }

        internal void SetCircle(Vector3 center, float radius)
        {
            center = ToStoredPoint(center);
            if (!pendingNewZone) Undo.RecordObject(zone, replacingCircle ? "Replace Zone Circle" : "Edit Zone Circle");
            SerializedObject serialized = new SerializedObject(zone);
            serialized.FindProperty("hasCenterPoint").boolValue = true;
            serialized.FindProperty("centerPoint").vector2Value = new Vector2(center.x, center.z);
            serialized.FindProperty("circleCenter").vector2Value = new Vector2(center.x, center.z);
            serialized.FindProperty("circleRadius").floatValue = RoundToThreeDecimals(Mathf.Max(0.01f, radius));
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(zone);
            PersistPendingZone();
            AssetDatabase.SaveAssetIfDirty(zone);
            replacingCircle = false;
            definingCircleRadius = false;
            movingPendingCircleCenter = false;
            paintMode = SceneZonePaintMode.SelectZone;
            blockedBy = null;
            ClearOverlapError();
            SceneView.RepaintAll();
            Repaint();
        }

        private void BeginDrawing(bool clearExisting)
        {
            draftPoints.Clear();
            if (!clearExisting) draftPoints.AddRange(zone.PolygonPoints);
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
            if (zone.HasCenterPoint && !SceneZoneOverlap.PolygonContains(draftPoints, zone.CenterPoint))
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
            if (!pendingNewZone) Undo.RecordObject(zone, "Replace Zone Boundary");
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
            else if (zone != null && zone.PolygonPoints.Count > 0)
            {
                Undo.RecordObject(zone, "Remove Zone Point");
                SetPoints(zone.PolygonPoints.Take(zone.PolygonPoints.Count - 1));
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
            bool showPersistentSelection = zone != null && !pendingNewZone && !drawing;
            Handles.color = showPersistentSelection ? selectedZoneColor : zone == null ? polygonToolColor : zone.LineColor;
            float width = showPersistentSelection ? 4f : 3f;
            for (int i = 1; i < displayed.Length; i++) Handles.DrawAAPolyLine(width, displayed[i - 1], displayed[i]);
            if (close && displayed.Length >= 3) Handles.DrawAAPolyLine(width, displayed[displayed.Length - 1], displayed[0]);
            for (int i = 0; i < displayed.Length; i++)
            {
                Color pointColor = showPersistentSelection ? selectedZoneColor : i == 0 ? Color.green : polygonToolColor;
                DrawPointHandle(displayed[i], pointColor, i == 0 ? 0.15f : 0.12f);
            }
            Handles.zTest = previousDepthTest;
            Handles.color = previousColor;
        }

        private void DrawLayerOutlines()
        {
            if (zoneLayer == null) return;
            foreach (SceneZoneLayerAsset visibleLayer in sceneLayers)
            foreach (SceneZoneAsset item in visibleLayer.Zones)
            {
                if (item == null || !item.IsUsable || (item == zone && paintMode != SceneZonePaintMode.SelectZone)) continue;
                if (!IsZoneVisible(item)) continue;
                bool selected = item == zone && !pendingNewZone;
                Color outlineColor = selected ? selectedZoneColor : visibleLayer.OutlineColor;
                float outlineWidth = selected ? 4f : 2f;
                Handles.color = outlineColor;
                if (item.Shape == SceneZoneShape.Circle)
                {
                    DrawCircleFill(item.CircleCenter, item.CircleRadius, item.FillColor);
                    DrawZoneCircle(item.CircleCenter, item.CircleRadius, outlineColor, false, outlineWidth);
                    DrawPointHandle(ToDisplayPoint(item.CircleCenter), selected ? selectedZoneColor : inactivePointColor, selected ? 0.13f : 0.11f);
                    DrawZoneName(item);
                    continue;
                }
                DrawPolygonFill(item.PolygonPoints, item.FillColor);
                Vector3[] points = item.PolygonPoints.Select(ToDisplayPoint).ToArray();
                for (int i = 1; i < points.Length; i++) Handles.DrawAAPolyLine(outlineWidth, points[i - 1], points[i]);
                if (points.Length >= 3) Handles.DrawAAPolyLine(outlineWidth, points[points.Length - 1], points[0]);
                foreach (Vector3 point in points)
                    DrawPointHandle(point, selected ? selectedZoneColor : inactivePointColor, selected ? 0.12f : 0.09f);
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
                DrawPointHandle(displayedHover, polygonToolColor, 0.15f);
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
            Handles.color = nextEdgeConflict == null ? zone.LineColor : Color.red;
            Handles.DrawDottedLine(displayedLast, displayedHover, 4f);
            if (draftPoints.Count >= 2)
            {
                Handles.color = closingEdgeConflict == null ? new Color(zone.LineColor.r, zone.LineColor.g, zone.LineColor.b, 0.55f) : Color.red;
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
            foreach (SceneZoneAsset zone in layerZones)
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


    }
}
