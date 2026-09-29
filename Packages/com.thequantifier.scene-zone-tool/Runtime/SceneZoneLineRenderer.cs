using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SceneZoneTool
{
    /// <summary>Optional standalone world-space visualization for a two-dimensional zone.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SceneZoneLineRenderer : MonoBehaviour
    {
        [FormerlySerializedAs("boundary")]
        [SerializeField] private SceneZoneAsset zone;
        [SerializeField, Min(8)] private int circleSegments = 64;
        [SerializeField] private float displayPlaneHeight;
        [SerializeField] private float verticalOffset = 0.08f;
        [SerializeField] private Material materialOverride;

        private LineRenderer line;
        private Material generatedMaterial;
        private SceneZoneAsset subscribedZone;

        /// <summary>Zone currently rendered by this component.</summary>
        public SceneZoneAsset Zone => zone;

        /// <summary>Assigns a zone and optional material and refreshes the generated line.</summary>
        public void Configure(SceneZoneAsset source, Material material = null, float yOffset = 0.08f, float planeHeight = 0f)
        {
            zone = source;
            materialOverride = material;
            verticalOffset = yOffset;
            displayPlaneHeight = planeHeight;
            UpdateZoneSubscription();
            Refresh();
        }

        /// <summary>Rebuilds the attached LineRenderer from current zone data.</summary>
        public void Refresh()
        {
            line ??= GetComponent<LineRenderer>();
            if (line == null || zone == null)
            {
                if (line != null) line.positionCount = 0;
                return;
            }

            List<Vector3> points = new List<Vector3>();
            if (zone.Shape == SceneZoneShape.Circle)
            {
                int count = Mathf.Max(8, circleSegments);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    Vector3 point = zone.CircleCenter + new Vector3(Mathf.Cos(angle) * zone.CircleRadius, 0f, Mathf.Sin(angle) * zone.CircleRadius);
                    points.Add(ToDisplayPoint(point));
                }
            }
            else
            {
                foreach (Vector3 point in zone.PolygonPoints) points.Add(ToDisplayPoint(point));
            }

            line.useWorldSpace = true;
            line.loop = points.Count >= 3;
            line.positionCount = points.Count;
            if (points.Count > 0) line.SetPositions(points.ToArray());
            line.startColor = zone.LineColor;
            line.endColor = zone.LineColor;
            line.startWidth = zone.LineWidth;
            line.endWidth = zone.LineWidth;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            ApplyMaterial();
        }

        private Vector3 ToDisplayPoint(Vector3 mapPoint)
        {
            return new Vector3(mapPoint.x, displayPlaneHeight + verticalOffset, mapPoint.z);
        }

        private void OnEnable()
        {
            UpdateZoneSubscription();
            Refresh();
        }

        private void OnDisable()
        {
            if (subscribedZone != null) subscribedZone.Changed -= OnZoneChanged;
            subscribedZone = null;
            ReleaseGeneratedMaterial();
        }

        private void OnZoneChanged()
        {
            Refresh();
        }

        private void OnValidate()
        {
            circleSegments = Mathf.Max(8, circleSegments);
            UpdateZoneSubscription();
            Refresh();
        }

        private void UpdateZoneSubscription()
        {
            SceneZoneAsset target = isActiveAndEnabled ? zone : null;
            if (subscribedZone == target) return;
            if (subscribedZone != null) subscribedZone.Changed -= OnZoneChanged;
            subscribedZone = target;
            if (subscribedZone != null) subscribedZone.Changed += OnZoneChanged;
        }

        private void ApplyMaterial()
        {
            if (materialOverride != null)
            {
                ReleaseGeneratedMaterial();
                line.sharedMaterial = materialOverride;
                return;
            }

            if (generatedMaterial == null)
                generatedMaterial = SceneZoneMaterialFactory.CreateUnlitMaterial(Color.white);
            if (generatedMaterial != null) line.sharedMaterial = generatedMaterial;
        }

        private void ReleaseGeneratedMaterial()
        {
            if (generatedMaterial == null) return;
            if (line != null && line.sharedMaterial == generatedMaterial) line.sharedMaterial = null;
            if (Application.isPlaying) Destroy(generatedMaterial);
            else DestroyImmediate(generatedMaterial);
            generatedMaterial = null;
        }
    }
}
