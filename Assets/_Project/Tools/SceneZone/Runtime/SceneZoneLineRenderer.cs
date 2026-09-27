using System.Collections.Generic;
using UnityEngine;

namespace SceneZoneTool
{
    /// <summary>Optional standalone world-space visualization for a two-dimensional zone.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SceneZoneLineRenderer : MonoBehaviour
    {
        [SerializeField] private SceneZoneAsset boundary;
        [SerializeField, Min(8)] private int circleSegments = 64;
        [SerializeField] private float displayPlaneHeight;
        [SerializeField] private float verticalOffset = 0.08f;
        [SerializeField] private Material materialOverride;

        private LineRenderer line;

        public SceneZoneAsset Boundary => boundary;

        public void Configure(SceneZoneAsset source, Material material = null, float yOffset = 0.08f, float planeHeight = 0f)
        {
            boundary = source;
            materialOverride = material;
            verticalOffset = yOffset;
            displayPlaneHeight = planeHeight;
            Refresh();
        }

        public void Refresh()
        {
            line ??= GetComponent<LineRenderer>();
            if (line == null || boundary == null)
            {
                if (line != null) line.positionCount = 0;
                return;
            }

            List<Vector3> points = new List<Vector3>();
            if (boundary.Shape == SceneZoneShape.Circle)
            {
                int count = Mathf.Max(8, circleSegments);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    Vector3 point = boundary.CircleCenter + new Vector3(Mathf.Cos(angle) * boundary.CircleRadius, 0f, Mathf.Sin(angle) * boundary.CircleRadius);
                    points.Add(ToDisplayPoint(point));
                }
            }
            else
            {
                foreach (Vector3 point in boundary.PolygonPoints) points.Add(ToDisplayPoint(point));
            }

            line.useWorldSpace = true;
            line.loop = points.Count >= 3;
            line.positionCount = points.Count;
            if (points.Count > 0) line.SetPositions(points.ToArray());
            line.startColor = boundary.LineColor;
            line.endColor = boundary.LineColor;
            line.startWidth = boundary.LineWidth;
            line.endWidth = boundary.LineWidth;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            if (materialOverride != null) line.sharedMaterial = materialOverride;
        }

        private Vector3 ToDisplayPoint(Vector3 mapPoint)
        {
            return new Vector3(mapPoint.x, displayPlaneHeight + verticalOffset, mapPoint.z);
        }

        private void OnEnable()
        {
            SceneZoneAsset.Changed += OnZoneChanged;
            Refresh();
        }

        private void OnDisable() => SceneZoneAsset.Changed -= OnZoneChanged;

        private void OnZoneChanged(SceneZoneAsset changed)
        {
            if (changed == boundary) Refresh();
        }

        private void OnValidate()
        {
            circleSegments = Mathf.Max(8, circleSegments);
            Refresh();
        }
    }
}
