using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneZoneTool
{
    public enum SceneZoneOverlapKind
    {
        CrossingSegments = 0,
        CollinearSegments = 1,
        CandidateSegmentInsideExisting = 2,
        ExistingSegmentInsideCandidate = 3,
        CandidateSegmentIntersectsCircle = 4,
        CircleInsideCandidate = 5
    }

    public readonly struct SceneZoneSegmentConflict
    {
        public SceneZoneSegmentConflict(int candidateSegmentIndex, int existingSegmentIndex, SceneZoneOverlapKind kind)
        {
            CandidateSegmentIndex = candidateSegmentIndex;
            ExistingSegmentIndex = existingSegmentIndex;
            Kind = kind;
        }

        public int CandidateSegmentIndex { get; }
        public int ExistingSegmentIndex { get; }
        public SceneZoneOverlapKind Kind { get; }
    }

    /// <summary>Horizontal overlap checks used by both editor tooling and runtime validation.</summary>
    public static class SceneZoneOverlap
    {
        private const float Epsilon = 0.0001f;

        public static bool ValidateSimplePolygon(IReadOnlyList<Vector2> polygon, out string failure)
        {
            if (polygon == null || polygon.Count < 3)
            {
                failure = "A polygon needs at least three points.";
                return false;
            }

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 point = polygon[i];
                if (!IsFinite(point.x) || !IsFinite(point.y))
                {
                    failure = $"Polygon point {i + 1} contains a non-finite coordinate.";
                    return false;
                }
                if ((point - polygon[(i + 1) % polygon.Count]).sqrMagnitude <= Epsilon * Epsilon)
                {
                    failure = $"Polygon {FormatSegmentCoordinates(point, polygon[(i + 1) % polygon.Count])} has duplicate or effectively identical endpoints.";
                    return false;
                }
            }

            for (int first = 0; first < polygon.Count; first++)
            {
                int firstEnd = (first + 1) % polygon.Count;
                for (int second = first + 1; second < polygon.Count; second++)
                {
                    int secondEnd = (second + 1) % polygon.Count;
                    bool adjacent = first == second || firstEnd == second || secondEnd == first;
                    if (adjacent) continue;
                    if (SegmentsIntersectOrTouch(polygon[first], polygon[firstEnd], polygon[second], polygon[secondEnd]))
                    {
                        failure = $"Polygon intersects itself: {FormatSegmentCoordinates(polygon[first], polygon[firstEnd])} overlaps {FormatSegmentCoordinates(polygon[second], polygon[secondEnd])}.";
                        return false;
                    }
                }
            }

            double twiceArea = 0d;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                twiceArea += (double)a.x * b.y - (double)b.x * a.y;
            }
            if (Math.Abs(twiceArea) <= Epsilon)
            {
                failure = "Polygon has no usable horizontal area.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static string FormatSegmentCoordinates(Vector3 start, Vector3 end)
        {
            return FormatSegmentCoordinates(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
        }

        public static string FormatSegmentCoordinates(Vector2 start, Vector2 end)
        {
            return $"segment [<{Mathf.Round(start.x):0},{Mathf.Round(start.y):0}>,<{Mathf.Round(end.x):0},{Mathf.Round(end.y):0}>]";
        }

        public static bool ValidateSimplePolygon(IReadOnlyList<Vector3> polygon, out string failure)
        {
            if (polygon == null)
            {
                failure = "A polygon needs at least three points.";
                return false;
            }
            Vector2[] points = new Vector2[polygon.Count];
            for (int i = 0; i < polygon.Count; i++) points[i] = new Vector2(polygon[i].x, polygon[i].z);
            return ValidateSimplePolygon(points, out failure);
        }

        public static bool Overlaps(SceneZoneAsset first, SceneZoneAsset second)
        {
            if (first == null || second == null || !first.IsUsable || !second.IsUsable) return false;
            if (!BoundsOverlap(GetBounds(first), GetBounds(second))) return false;
            if (first.Shape == SceneZoneShape.Circle && second.Shape == SceneZoneShape.Circle)
            {
                float combined = first.CircleRadius + second.CircleRadius;
                return HorizontalSquaredDistance(first.CircleCenter, second.CircleCenter) < combined * combined - Epsilon;
            }
            if (first.Shape == SceneZoneShape.Polygon && second.Shape == SceneZoneShape.Polygon)
                return PolygonsOverlap(first.PolygonPoints, second.PolygonPoints);
            return first.Shape == SceneZoneShape.Polygon
                ? PolygonCircleOverlap(first.PolygonPoints, second.CircleCenter, second.CircleRadius)
                : PolygonCircleOverlap(second.PolygonPoints, first.CircleCenter, first.CircleRadius);
        }

        public static bool PolygonOverlaps(IReadOnlyList<Vector3> polygon, SceneZoneAsset other)
        {
            if (polygon == null || polygon.Count < 3 || other == null || !other.IsUsable) return false;
            if (!BoundsOverlap(GetBounds(polygon), GetBounds(other))) return false;
            return other.Shape == SceneZoneShape.Circle
                ? PolygonCircleOverlap(polygon, other.CircleCenter, other.CircleRadius)
                : PolygonsOverlap(polygon, other.PolygonPoints);
        }

        public static bool CircleOverlaps(Vector3 center, float radius, SceneZoneAsset other)
        {
            if (radius <= 0f || other == null || !other.IsUsable) return false;
            if (!BoundsOverlap(GetCircleBounds(center, radius), GetBounds(other))) return false;
            if (other.Shape == SceneZoneShape.Circle)
            {
                float combined = radius + other.CircleRadius;
                return HorizontalSquaredDistance(center, other.CircleCenter) < combined * combined - Epsilon;
            }
            return PolygonCircleOverlap(other.PolygonPoints, center, radius);
        }

        public static bool PolygonContains(IReadOnlyList<Vector3> polygon, Vector3 point, bool includeBoundary = true)
        {
            if (polygon == null || polygon.Count < 3) return false;
            if (includeBoundary)
            {
                for (int i = 0; i < polygon.Count; i++) if (PointOnSegment(point, polygon[i], polygon[(i + 1) % polygon.Count])) return true;
            }
            return PointStrictlyInsidePolygon(point, polygon);
        }

        public static bool ContainsHorizontal(SceneZoneAsset boundary, Vector3 point)
        {
            if (boundary == null || !boundary.IsUsable) return false;
            if (boundary.Shape == SceneZoneShape.Circle)
                return HorizontalSquaredDistance(point, boundary.CircleCenter) <= boundary.CircleRadius * boundary.CircleRadius + Epsilon;
            return PolygonContains(boundary.PolygonPoints, point);
        }

        public static bool SegmentEnters(Vector3 start, Vector3 end, SceneZoneAsset other)
        {
            if (other == null || !other.IsUsable) return false;
            if (!BoundsOverlap(GetSegmentBounds(start, end), GetBounds(other))) return false;
            if (other.Shape == SceneZoneShape.Circle)
            {
                float radiusSquared = other.CircleRadius * other.CircleRadius;
                return HorizontalSquaredDistance(start, other.CircleCenter) < radiusSquared - Epsilon
                    || HorizontalSquaredDistance(end, other.CircleCenter) < radiusSquared - Epsilon
                    || SegmentSquaredDistance(other.CircleCenter, start, end) < radiusSquared - Epsilon;
            }

            IReadOnlyList<Vector3> polygon = other.PolygonPoints;
            if (PointStrictlyInsidePolygon(start, polygon) || PointStrictlyInsidePolygon(end, polygon)) return true;
            for (int i = 0; i < polygon.Count; i++)
            {
                if (SegmentsProperlyIntersect(start, end, polygon[i], polygon[(i + 1) % polygon.Count])) return true;
            }
            return false;
        }

        public static IReadOnlyList<SceneZoneSegmentConflict> FindPolygonConflicts(IReadOnlyList<Vector3> candidate, SceneZoneAsset other)
        {
            List<SceneZoneSegmentConflict> conflicts = new List<SceneZoneSegmentConflict>();
            if (candidate == null || candidate.Count < 3 || other == null || !other.IsUsable || !PolygonOverlaps(candidate, other)) return conflicts;
            if (other.Shape == SceneZoneShape.Circle)
            {
                float radiusSquared = other.CircleRadius * other.CircleRadius;
                for (int i = 0; i < candidate.Count; i++)
                {
                    if (SegmentSquaredDistance(other.CircleCenter, candidate[i], candidate[(i + 1) % candidate.Count]) < radiusSquared - Epsilon)
                        conflicts.Add(new SceneZoneSegmentConflict(i, -1, SceneZoneOverlapKind.CandidateSegmentIntersectsCircle));
                }
                if (conflicts.Count == 0 && PointStrictlyInsidePolygon(other.CircleCenter, candidate))
                    conflicts.Add(new SceneZoneSegmentConflict(-1, -1, SceneZoneOverlapKind.CircleInsideCandidate));
                return conflicts;
            }

            IReadOnlyList<Vector3> existing = other.PolygonPoints;
            for (int candidateIndex = 0; candidateIndex < candidate.Count; candidateIndex++)
            {
                Vector3 candidateStart = candidate[candidateIndex];
                Vector3 candidateEnd = candidate[(candidateIndex + 1) % candidate.Count];
                for (int existingIndex = 0; existingIndex < existing.Count; existingIndex++)
                {
                    Vector3 existingStart = existing[existingIndex];
                    Vector3 existingEnd = existing[(existingIndex + 1) % existing.Count];
                    if (SegmentsProperlyIntersect(candidateStart, candidateEnd, existingStart, existingEnd))
                        conflicts.Add(new SceneZoneSegmentConflict(candidateIndex, existingIndex, SceneZoneOverlapKind.CrossingSegments));
                    else if (SegmentsCollinearlyOverlap(candidateStart, candidateEnd, existingStart, existingEnd))
                        conflicts.Add(new SceneZoneSegmentConflict(candidateIndex, existingIndex, SceneZoneOverlapKind.CollinearSegments));
                }
                Vector3 candidateMidpoint = (candidateStart + candidateEnd) * 0.5f;
                if (PointStrictlyInsidePolygon(candidateMidpoint, existing))
                    conflicts.Add(new SceneZoneSegmentConflict(candidateIndex, -1, SceneZoneOverlapKind.CandidateSegmentInsideExisting));
            }
            for (int existingIndex = 0; existingIndex < existing.Count; existingIndex++)
            {
                Vector3 midpoint = (existing[existingIndex] + existing[(existingIndex + 1) % existing.Count]) * 0.5f;
                if (PointStrictlyInsidePolygon(midpoint, candidate))
                    conflicts.Add(new SceneZoneSegmentConflict(-1, existingIndex, SceneZoneOverlapKind.ExistingSegmentInsideCandidate));
            }
            return conflicts;
        }

        public static IReadOnlyList<SceneZoneSegmentConflict> FindSegmentConflicts(Vector3 start, Vector3 end, SceneZoneAsset other)
        {
            List<SceneZoneSegmentConflict> conflicts = new List<SceneZoneSegmentConflict>();
            if (other == null || !other.IsUsable || !SegmentEnters(start, end, other)) return conflicts;
            if (other.Shape == SceneZoneShape.Circle)
            {
                conflicts.Add(new SceneZoneSegmentConflict(0, -1, SceneZoneOverlapKind.CandidateSegmentIntersectsCircle));
                return conflicts;
            }
            IReadOnlyList<Vector3> existing = other.PolygonPoints;
            for (int i = 0; i < existing.Count; i++)
            {
                if (SegmentsProperlyIntersect(start, end, existing[i], existing[(i + 1) % existing.Count]))
                    conflicts.Add(new SceneZoneSegmentConflict(0, i, SceneZoneOverlapKind.CrossingSegments));
            }
            if (conflicts.Count == 0)
                conflicts.Add(new SceneZoneSegmentConflict(0, -1, SceneZoneOverlapKind.CandidateSegmentInsideExisting));
            return conflicts;
        }

        private static bool PolygonsOverlap(IReadOnlyList<Vector3> first, IReadOnlyList<Vector3> second)
        {
            if (first == null || second == null || first.Count < 3 || second.Count < 3) return false;
            if (!BoundsOverlap(GetBounds(first), GetBounds(second))) return false;
            for (int a = 0; a < first.Count; a++)
            {
                Vector3 a1 = first[a];
                Vector3 a2 = first[(a + 1) % first.Count];
                for (int b = 0; b < second.Count; b++)
                {
                    if (SegmentsProperlyIntersect(a1, a2, second[b], second[(b + 1) % second.Count])) return true;
                }
            }
            for (int i = 0; i < first.Count; i++) if (PointStrictlyInsidePolygon(first[i], second)) return true;
            for (int i = 0; i < second.Count; i++) if (PointStrictlyInsidePolygon(second[i], first)) return true;

            // Detect positive-area overlap along coincident edges. The probe sits just inside its
            // source polygon, so a merely shared border remains valid while stacked area does not.
            for (int i = 0; i < first.Count; i++) if (PointStrictlyInsidePolygon(InteriorEdgeProbe(first, i), second)) return true;
            for (int i = 0; i < second.Count; i++) if (PointStrictlyInsidePolygon(InteriorEdgeProbe(second, i), first)) return true;
            return false;
        }

        private static bool PolygonCircleOverlap(IReadOnlyList<Vector3> polygon, Vector3 center, float radius)
        {
            if (polygon == null || polygon.Count < 3 || radius <= 0f) return false;
            if (!BoundsOverlap(GetBounds(polygon), GetCircleBounds(center, radius))) return false;
            float radiusSquared = radius * radius;
            if (PointStrictlyInsidePolygon(center, polygon)) return true;
            for (int i = 0; i < polygon.Count; i++)
            {
                if (HorizontalSquaredDistance(polygon[i], center) < radiusSquared - Epsilon) return true;
                if (SegmentSquaredDistance(center, polygon[i], polygon[(i + 1) % polygon.Count]) < radiusSquared - Epsilon) return true;
            }
            return false;
        }

        private static bool PointStrictlyInsidePolygon(Vector3 point, IReadOnlyList<Vector3> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;
            bool inside = false;
            for (int i = 0, previous = polygon.Count - 1; i < polygon.Count; previous = i++)
            {
                Vector3 a = polygon[previous];
                Vector3 b = polygon[i];
                if (PointOnSegment(point, a, b)) return false;
                bool straddles = (a.z > point.z) != (b.z > point.z);
                if (straddles && point.x < (b.x - a.x) * (point.z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }

        private static bool SegmentsProperlyIntersect(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            double first = Cross(a, b, c);
            double second = Cross(a, b, d);
            double third = Cross(c, d, a);
            double fourth = Cross(c, d, b);
            return ((first > Epsilon && second < -Epsilon) || (first < -Epsilon && second > Epsilon))
                && ((third > Epsilon && fourth < -Epsilon) || (third < -Epsilon && fourth > Epsilon));
        }

        private static bool SegmentsCollinearlyOverlap(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (Math.Abs(Cross(a, b, c)) > Epsilon || Math.Abs(Cross(a, b, d)) > Epsilon) return false;
            Vector2 direction = new Vector2(b.x - a.x, b.z - a.z);
            float lengthSquared = direction.sqrMagnitude;
            if (lengthSquared <= Epsilon) return false;
            float first = ((c.x - a.x) * direction.x + (c.z - a.z) * direction.y) / lengthSquared;
            float second = ((d.x - a.x) * direction.x + (d.z - a.z) * direction.y) / lengthSquared;
            float overlapStart = Mathf.Max(0f, Mathf.Min(first, second));
            float overlapEnd = Mathf.Min(1f, Mathf.Max(first, second));
            return overlapEnd - overlapStart > Epsilon;
        }

        private static bool PointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            if (Math.Abs(Cross(a, b, point)) > Epsilon) return false;
            float dot = (point.x - a.x) * (b.x - a.x) + (point.z - a.z) * (b.z - a.z);
            float lengthSquared = HorizontalSquaredDistance(a, b);
            return dot >= -Epsilon && dot <= lengthSquared + Epsilon;
        }

        private static float SegmentSquaredDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            float x = b.x - a.x;
            float z = b.z - a.z;
            float lengthSquared = x * x + z * z;
            if (lengthSquared <= Epsilon) return HorizontalSquaredDistance(point, a);
            float amount = Mathf.Clamp01(((point.x - a.x) * x + (point.z - a.z) * z) / lengthSquared);
            float closestX = a.x + x * amount;
            float closestZ = a.z + z * amount;
            float dx = point.x - closestX;
            float dz = point.z - closestZ;
            return dx * dx + dz * dz;
        }

        private static float HorizontalSquaredDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }

        private static double Cross(Vector3 a, Vector3 b, Vector3 point) => ((double)b.x - a.x) * (point.z - a.z) - ((double)b.z - a.z) * (point.x - a.x);

        private static bool SegmentsIntersectOrTouch(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double first = Cross(a, b, c);
            double second = Cross(a, b, d);
            double third = Cross(c, d, a);
            double fourth = Cross(c, d, b);
            if (((first > Epsilon && second < -Epsilon) || (first < -Epsilon && second > Epsilon))
                && ((third > Epsilon && fourth < -Epsilon) || (third < -Epsilon && fourth > Epsilon))) return true;
            return Math.Abs(first) <= Epsilon && PointOnSegment(c, a, b)
                || Math.Abs(second) <= Epsilon && PointOnSegment(d, a, b)
                || Math.Abs(third) <= Epsilon && PointOnSegment(a, c, d)
                || Math.Abs(fourth) <= Epsilon && PointOnSegment(b, c, d);
        }

        private static bool PointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            return point.x >= Mathf.Min(a.x, b.x) - Epsilon && point.x <= Mathf.Max(a.x, b.x) + Epsilon
                && point.y >= Mathf.Min(a.y, b.y) - Epsilon && point.y <= Mathf.Max(a.y, b.y) + Epsilon;
        }

        private static double Cross(Vector2 a, Vector2 b, Vector2 point)
        {
            return ((double)b.x - a.x) * (point.y - a.y) - ((double)b.y - a.y) * (point.x - a.x);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private readonly struct HorizontalBounds
        {
            public HorizontalBounds(float minimumX, float maximumX, float minimumZ, float maximumZ)
            {
                MinimumX = minimumX;
                MaximumX = maximumX;
                MinimumZ = minimumZ;
                MaximumZ = maximumZ;
            }

            public float MinimumX { get; }
            public float MaximumX { get; }
            public float MinimumZ { get; }
            public float MaximumZ { get; }
        }

        private static HorizontalBounds GetBounds(SceneZoneAsset zone)
        {
            return zone.Shape == SceneZoneShape.Circle
                ? GetCircleBounds(zone.CircleCenter, zone.CircleRadius)
                : GetBounds(zone.PolygonPoints);
        }

        private static HorizontalBounds GetBounds(IReadOnlyList<Vector3> polygon)
        {
            if (polygon == null || polygon.Count == 0) return new HorizontalBounds(0f, 0f, 0f, 0f);
            float minimumX = polygon[0].x;
            float maximumX = minimumX;
            float minimumZ = polygon[0].z;
            float maximumZ = minimumZ;
            for (int i = 1; i < polygon.Count; i++)
            {
                Vector3 point = polygon[i];
                minimumX = Mathf.Min(minimumX, point.x);
                maximumX = Mathf.Max(maximumX, point.x);
                minimumZ = Mathf.Min(minimumZ, point.z);
                maximumZ = Mathf.Max(maximumZ, point.z);
            }
            return new HorizontalBounds(minimumX, maximumX, minimumZ, maximumZ);
        }

        private static HorizontalBounds GetCircleBounds(Vector3 center, float radius)
        {
            return new HorizontalBounds(center.x - radius, center.x + radius, center.z - radius, center.z + radius);
        }

        private static HorizontalBounds GetSegmentBounds(Vector3 start, Vector3 end)
        {
            return new HorizontalBounds(Mathf.Min(start.x, end.x), Mathf.Max(start.x, end.x), Mathf.Min(start.z, end.z), Mathf.Max(start.z, end.z));
        }

        private static bool BoundsOverlap(HorizontalBounds first, HorizontalBounds second)
        {
            return first.MaximumX >= second.MinimumX - Epsilon
                && second.MaximumX >= first.MinimumX - Epsilon
                && first.MaximumZ >= second.MinimumZ - Epsilon
                && second.MaximumZ >= first.MinimumZ - Epsilon;
        }

        private static Vector3 InteriorEdgeProbe(IReadOnlyList<Vector3> polygon, int edgeIndex)
        {
            Vector3 start = polygon[edgeIndex];
            Vector3 end = polygon[(edgeIndex + 1) % polygon.Count];
            Vector3 midpoint = (start + end) * 0.5f;
            float signedArea = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 a = polygon[i];
                Vector3 b = polygon[(i + 1) % polygon.Count];
                signedArea += a.x * b.z - b.x * a.z;
            }
            Vector2 edge = new Vector2(end.x - start.x, end.z - start.z);
            if (edge.sqrMagnitude <= Epsilon) return midpoint;
            Vector2 inward = signedArea >= 0f ? new Vector2(-edge.y, edge.x) : new Vector2(edge.y, -edge.x);
            inward.Normalize();
            float offset = Mathf.Max(0.001f, edge.magnitude * 0.00001f);
            return midpoint + new Vector3(inward.x * offset, 0f, inward.y * offset);
        }
    }
}
