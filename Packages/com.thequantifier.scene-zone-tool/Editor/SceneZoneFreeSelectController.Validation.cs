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
    {        private SceneZoneAsset FindSegmentConflict(Vector3 start, Vector3 end)
        {
            if (zoneLayer == null || !zoneLayer.PreventOverlap) return null;
            return layerZones.FirstOrDefault(item => item != null && item != zone && SceneZoneOverlap.SegmentEnters(start, end, item));
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
            if (zoneLayer == null || !zoneLayer.PreventOverlap || points == null || points.Count < 2) return null;
            foreach (SceneZoneAsset item in layerZones.Where(item => item != null && item != zone))
            {
                for (int i = 1; i < points.Count; i++) if (SceneZoneOverlap.SegmentEnters(points[i - 1], points[i], item)) return item;
                if (includeClosingSegment && points.Count >= 3 && SceneZoneOverlap.SegmentEnters(points[points.Count - 1], points[0], item)) return item;
                if (points.Count >= 3 && SceneZoneOverlap.PolygonOverlaps(points, item)) return item;
            }
            return null;
        }

        private SceneZoneAsset FindCircleConflict(Vector3 center, float radius)
        {
            if (zoneLayer == null || !zoneLayer.PreventOverlap) return null;
            return layerZones.FirstOrDefault(item => item != null && item != zone && SceneZoneOverlap.CircleOverlaps(center, radius, item));
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
            overlapError = $"ERROR: Overlap detected\nCircle {zone.DisplayName} overlaps {conflict.DisplayName}.";
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


    }
}
