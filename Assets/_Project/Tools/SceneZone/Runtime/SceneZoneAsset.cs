using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneZoneTool
{
    public enum SceneZoneShape
    {
        Circle = 0,
        Polygon = 1
    }

    /// <summary>
    /// Standalone Unity scene-zone boundary data. It deliberately knows nothing about this game's
    /// Places, governments, catalogs, or persistence and can be copied into another Unity project.
    /// Coordinates are stored as world X/Z pairs. Scene height is deliberately excluded so a
    /// zone is a two-dimensional map region independent of scene geometry and elevation.
    /// </summary>
    public sealed class SceneZoneAsset : ScriptableObject
    {
        public static event Action<SceneZoneAsset> Changed;

        [SerializeField] private string boundaryId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string notes;
        [SerializeField] private SceneZoneLayerAsset boundaryLayer;
        [SerializeField] private SceneZoneShape shape = SceneZoneShape.Polygon;
        [SerializeField] private bool hasCenterPoint;
        [SerializeField] private Vector2 centerPoint;
        [SerializeField] private Vector2 circleCenter;
        [SerializeField, Min(0f)] private float circleRadius = 25f;
        [SerializeField] private Vector2[] polygonPoints = Array.Empty<Vector2>();
        [SerializeField] private Color lineColor = new Color(0.2f, 0.75f, 1f, 0.85f);
        [SerializeField] private Color fillColor;
        [SerializeField, Min(0.01f)] private float lineWidth = 0.2f;
        [SerializeField] private int version = 1;

        [NonSerialized] private IReadOnlyList<Vector2> polygonPoints2DView;
        [NonSerialized] private IReadOnlyList<Vector3> polygonPointsWorldView;
        [NonSerialized] private bool polygonValidationCached;
        [NonSerialized] private bool polygonValidationResult;
        [NonSerialized] private float? horizontalAreaCache;

        public string BoundaryId => boundaryId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Notes => notes ?? string.Empty;
        public SceneZoneLayerAsset BoundaryLayer => boundaryLayer;
        public SceneZoneShape Shape => shape;
        public bool HasCenterPoint => hasCenterPoint;
        public Vector2 CenterPoint2D => hasCenterPoint ? centerPoint : CalculatedCenter2D;
        public Vector3 CenterPoint => ToWorld(CenterPoint2D);
        public Vector2 CalculatedCenter2D
        {
            get
            {
                if (shape == SceneZoneShape.Circle) return circleCenter;
                if (polygonPoints == null || polygonPoints.Length == 0) return Vector2.zero;
                return CalculatePolygonCenter(polygonPoints);
            }
        }
        public Vector3 CalculatedCenter
        {
            get => ToWorld(CalculatedCenter2D);
        }
        public Vector2 CircleCenter2D => circleCenter;
        public Vector3 CircleCenter => ToWorld(circleCenter);
        public float CircleRadius => circleRadius;
        public IReadOnlyList<Vector2> PolygonPoints2D
        {
            get
            {
                EnsurePointViews();
                return polygonPoints2DView;
            }
        }
        public IReadOnlyList<Vector3> PolygonPoints
        {
            get
            {
                EnsurePointViews();
                return polygonPointsWorldView;
            }
        }
        public Color LineColor => lineColor;
        public Color FillColor => fillColor.a > 0f ? fillColor : CreateStableFillColor(boundaryId);
        public float LineWidth => lineWidth;
        public int Version => version;
        public bool IsUsable
        {
            get
            {
                if (shape == SceneZoneShape.Circle)
                    return IsFinite(circleCenter) && IsFinite(circleRadius) && circleRadius > 0.01f;
                if (!polygonValidationCached)
                {
                    polygonValidationResult = SceneZoneOverlap.ValidateSimplePolygon(polygonPoints, out _);
                    polygonValidationCached = true;
                }
                return polygonValidationResult;
            }
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (shape == SceneZoneShape.Circle)
            {
                float x = worldPosition.x - circleCenter.x;
                float z = worldPosition.z - circleCenter.y;
                return x * x + z * z <= circleRadius * circleRadius;
            }
            return ContainsPolygon(worldPosition);
        }

        public float ApproximateHorizontalArea()
        {
            if (shape == SceneZoneShape.Circle) return Mathf.PI * circleRadius * circleRadius;
            if (horizontalAreaCache.HasValue) return horizontalAreaCache.Value;
            if (polygonPoints == null || polygonPoints.Length < 3) return 0f;
            double twiceArea = 0d;
            for (int i = 0; i < polygonPoints.Length; i++)
            {
                Vector2 a = polygonPoints[i];
                Vector2 b = polygonPoints[(i + 1) % polygonPoints.Length];
                twiceArea += (double)a.x * b.y - (double)b.x * a.y;
            }
            horizontalAreaCache = (float)Math.Abs(twiceArea * 0.5d);
            return horizontalAreaCache.Value;
        }

        public void Configure(
            string id,
            string authoredDisplayName,
            SceneZoneShape boundaryShape,
            IEnumerable<Vector3> points = null,
            Vector3 center = default,
            float radius = 25f,
            SceneZoneLayerAsset authoredLayer = null,
            bool useAuthoredCenter = false,
            Vector3 authoredCenter = default)
        {
            boundaryId = N(id);
            displayName = string.IsNullOrWhiteSpace(authoredDisplayName) ? boundaryId : authoredDisplayName.Trim();
            boundaryLayer = authoredLayer;
            shape = boundaryShape;
            hasCenterPoint = useAuthoredCenter;
            centerPoint = useAuthoredCenter ? ToMap(authoredCenter) : centerPoint;
            polygonPoints = (points ?? Array.Empty<Vector3>()).Select(ToMap).ToArray();
            circleCenter = ToMap(center);
            circleRadius = Mathf.Max(0f, radius);
            version = Math.Max(1, version);
            if (fillColor.a <= 0f) fillColor = CreateStableFillColor(boundaryId);
            InvalidatePointViews();
            Changed?.Invoke(this);
        }

        public void AssignFillColor(Color color)
        {
            fillColor = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp(color.a, 0.04f, 0.35f));
            Changed?.Invoke(this);
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(boundaryId)) { failure = "Boundary ID is required."; return false; }
            if (shape == SceneZoneShape.Circle && (!IsFinite(circleCenter) || !IsFinite(circleRadius))) { failure = "Circle center and radius must contain finite coordinates."; return false; }
            if (hasCenterPoint && !IsFinite(centerPoint)) { failure = "The authored center point must contain finite coordinates."; return false; }
            if (shape == SceneZoneShape.Polygon && (polygonPoints == null || polygonPoints.Length < 3)) { failure = "A polygon boundary needs at least three points."; return false; }
            if (shape == SceneZoneShape.Polygon && !SceneZoneOverlap.ValidateSimplePolygon(polygonPoints, out failure)) return false;
            if (!IsUsable) { failure = "Boundary has no usable horizontal area."; return false; }
            if (hasCenterPoint && !Contains(ToWorld(centerPoint))) { failure = "The authored center point must be inside the boundary."; return false; }
            failure = string.Empty;
            return true;
        }

        private bool ContainsPolygon(Vector3 point)
        {
            if (polygonPoints == null || polygonPoints.Length < 3) return false;
            bool inside = false;
            for (int i = 0, previous = polygonPoints.Length - 1; i < polygonPoints.Length; previous = i++)
            {
                Vector3 a = ToWorld(polygonPoints[previous]);
                Vector3 b = ToWorld(polygonPoints[i]);
                if (OnSegmentXZ(point, a, b)) return true;
                bool straddles = (a.z > point.z) != (b.z > point.z);
                if (straddles && point.x < (b.x - a.x) * (point.z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }

        private static bool OnSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            double cross = ((double)point.z - a.z) * (b.x - a.x) - ((double)point.x - a.x) * (b.z - a.z);
            if (Math.Abs(cross) > 0.0001d) return false;
            double dot = ((double)point.x - a.x) * (b.x - a.x) + ((double)point.z - a.z) * (b.z - a.z);
            if (dot < 0d) return false;
            double squaredLength = ((double)b.x - a.x) * (b.x - a.x) + ((double)b.z - a.z) * (b.z - a.z);
            return dot <= squaredLength;
        }

        private void OnValidate()
        {
            boundaryId = N(boundaryId);
            displayName = displayName?.Trim();
            circleRadius = Mathf.Max(0f, circleRadius);
            lineWidth = Mathf.Max(0.01f, lineWidth);
            version = Math.Max(1, version);
            if (fillColor.a <= 0f) fillColor = CreateStableFillColor(boundaryId);
            InvalidatePointViews();
            Changed?.Invoke(this);
        }

        private void EnsurePointViews()
        {
            if (polygonPoints2DView != null && polygonPointsWorldView != null) return;
            Vector2[] source = polygonPoints ?? Array.Empty<Vector2>();
            Vector3[] world = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++) world[i] = ToWorld(source[i]);
            polygonPoints2DView = Array.AsReadOnly(source);
            polygonPointsWorldView = Array.AsReadOnly(world);
        }

        private void InvalidatePointViews()
        {
            polygonPoints2DView = null;
            polygonPointsWorldView = null;
            polygonValidationCached = false;
            horizontalAreaCache = null;
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static Vector2 ToMap(Vector3 point) => new Vector2(point.x, point.z);
        private static Vector3 ToWorld(Vector2 point) => new Vector3(point.x, 0f, point.y);
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static Color CreateStableFillColor(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                float hue = (hash % 3600u) / 3600f;
                Color color = Color.HSVToRGB(hue, 0.68f, 0.95f);
                color.a = 0.16f;
                return color;
            }
        }

        private static Vector2 CalculatePolygonCenter(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0) return Vector2.zero;
            if (points.Count < 3) return points.Aggregate(Vector2.zero, (sum, point) => sum + point) / points.Count;

            double crossSum = 0d;
            double xSum = 0d;
            double ySum = 0d;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                double cross = (double)a.x * b.y - (double)b.x * a.y;
                crossSum += cross;
                xSum += (a.x + b.x) * cross;
                ySum += (a.y + b.y) * cross;
            }

            if (Math.Abs(crossSum) > 0.000001d)
            {
                Vector2 centroid = new Vector2((float)(xSum / (3d * crossSum)), (float)(ySum / (3d * crossSum)));
                if (ContainsPolygon2D(points, centroid)) return centroid;
            }

            // An area centroid can lie outside a concave polygon. An ear triangle's centroid is
            // guaranteed to be inside a valid simple polygon, so use the first available ear.
            float orientation = Mathf.Sign((float)crossSum);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 previous = points[(i - 1 + points.Count) % points.Count];
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                float turn = Cross2D(previous, current, next);
                if (Mathf.Abs(turn) <= 0.0001f || Mathf.Sign(turn) != orientation) continue;
                bool containsOtherVertex = false;
                for (int j = 0; j < points.Count; j++)
                {
                    if (j == i || j == (i - 1 + points.Count) % points.Count || j == (i + 1) % points.Count) continue;
                    if (PointInTriangle(points[j], previous, current, next)) { containsOtherVertex = true; break; }
                }
                if (!containsOtherVertex) return (previous + current + next) / 3f;
            }

            return points[0];
        }

        private static bool ContainsPolygon2D(IReadOnlyList<Vector2> points, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, previous = points.Count - 1; i < points.Count; previous = i++)
            {
                Vector2 a = points[previous];
                Vector2 b = points[i];
                bool straddles = (a.y > point.y) != (b.y > point.y);
                if (straddles && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float first = Cross2D(a, b, point);
            float second = Cross2D(b, c, point);
            float third = Cross2D(c, a, point);
            bool hasNegative = first < -0.0001f || second < -0.0001f || third < -0.0001f;
            bool hasPositive = first > 0.0001f || second > 0.0001f || third > 0.0001f;
            return !(hasNegative && hasPositive);
        }

        private static float Cross2D(Vector2 a, Vector2 b, Vector2 point)
        {
            return (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
        }
    }
}
