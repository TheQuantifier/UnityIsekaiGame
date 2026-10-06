using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityIsekaiGame.UI.Authentication
{
    /// <summary>
    /// Builds crisp, resolution-independent gilded lettering from the existing UI Text mesh.
    /// The effect preserves the authored font size and layout while adding an outline,
    /// down-right extrusion, raised highlight edge, and subtly graduated gold face.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class LoginEmbossedTextEffect : BaseMeshEffect
    {
        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(-1f, -1f),
            new Vector2(0f, -1f),
            new Vector2(1f, -1f),
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        private readonly List<UIVertex> sourceVertices = new List<UIVertex>();
        private readonly List<UIVertex> outputVertices = new List<UIVertex>();

        [SerializeField, Min(0f)] private float extrusionDepth = 4f;
        [SerializeField, Min(0f)] private float outlineWidth = 1f;
        [SerializeField, Range(1, 8)] private int extrusionSteps = 4;
        [SerializeField, Range(0.02f, 0.16f)] private float middleSeamWidth = 0.07f;

        public float ExtrusionDepth => extrusionDepth;
        public float OutlineWidth => outlineWidth;
        public float MiddleSeamWidth => middleSeamWidth;

        public void Configure(float depth, float configuredOutlineWidth, int steps = 4, float configuredMiddleSeamWidth = 0.07f)
        {
            extrusionDepth = Mathf.Max(0f, depth);
            outlineWidth = Mathf.Max(0f, configuredOutlineWidth);
            extrusionSteps = Mathf.Clamp(steps, 1, 8);
            middleSeamWidth = Mathf.Clamp(configuredMiddleSeamWidth, 0.02f, 0.16f);
            graphic?.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper == null || vertexHelper.currentVertCount == 0)
            {
                return;
            }

            sourceVertices.Clear();
            vertexHelper.GetUIVertexStream(sourceVertices);
            if (sourceVertices.Count == 0)
            {
                return;
            }

            float lowestY = float.PositiveInfinity;
            float highestY = float.NegativeInfinity;
            for (int i = 0; i < sourceVertices.Count; i++)
            {
                float y = sourceVertices[i].position.y;
                lowestY = Mathf.Min(lowestY, y);
                highestY = Mathf.Max(highestY, y);
            }

            outputVertices.Clear();
            int layerCount = OutlineDirections.Length + extrusionSteps + 4;
            if (outputVertices.Capacity < sourceVertices.Count * layerCount)
            {
                outputVertices.Capacity = sourceVertices.Count * layerCount;
            }

            // Two offset silhouettes provide a compact cast shadow behind the extrusion.
            // Keeping this directional (instead of a broad glow) reinforces the same raised,
            // forged-metal lighting language used by the crest and its sword.
            AppendSolidLayer(
                sourceVertices,
                new Vector2(extrusionDepth * 0.78f, -extrusionDepth * 1.42f),
                0.035f,
                0.012f,
                0.004f,
                0.48f);
            AppendSolidLayer(
                sourceVertices,
                new Vector2(extrusionDepth * 0.58f, -extrusionDepth * 1.16f),
                0.075f,
                0.026f,
                0.006f,
                0.76f);

            // A slim dark rim keeps the gold face legible on both the leather banner and
            // the brighter portions of the painted login background.
            for (int i = 0; i < OutlineDirections.Length; i++)
            {
                AppendSolidLayer(
                    sourceVertices,
                    OutlineDirections[i] * outlineWidth,
                    0.22f,
                    0.10f,
                    0.025f,
                    0.94f);
            }

            // Draw the deepest side first, then build toward the face. The slight horizontal
            // component reads as beveled metal rather than a conventional flat drop shadow.
            for (int step = extrusionSteps; step >= 1; step--)
            {
                float amount = step / (float)extrusionSteps;
                float brightness = Mathf.Lerp(0.50f, 0.24f, amount);
                AppendTintedLayer(
                    sourceVertices,
                    new Vector2(extrusionDepth * amount * 0.42f, -extrusionDepth * amount),
                    brightness,
                    brightness * 0.82f,
                    brightness * 0.48f,
                    1f);
            }

            // A fractionally offset bright copy peeks out along the upper-left edge and
            // supplies the specular lip of the embossed lettering.
            AppendTintedLayer(
                sourceVertices,
                new Vector2(-0.45f, 0.65f),
                1.32f,
                1.24f,
                1.04f,
                0.92f);

            AppendFacetedFace(sourceVertices, lowestY, highestY);
            vertexHelper.Clear();
            vertexHelper.AddUIVertexTriangleStream(outputVertices);
        }

        private void AppendFacetedFace(IReadOnlyList<UIVertex> vertices, float lowestY, float highestY)
        {
            // Legacy UI Text supplies one six-vertex triangle stream per glyph. Splitting each
            // glyph independently makes the ridge follow every letter instead of producing one
            // broad lighting divide across the complete title.
            if (vertices.Count % 6 != 0)
            {
                AppendGraduatedFallback(vertices, lowestY, highestY);
                return;
            }

            float height = Mathf.Max(0.001f, highestY - lowestY);
            float seamLeft = 0.5f - middleSeamWidth * 0.5f;
            float seamRight = 0.5f + middleSeamWidth * 0.5f;
            for (int glyphStart = 0; glyphStart < vertices.Count; glyphStart += 6)
            {
                FindGlyphCorners(
                    vertices,
                    glyphStart,
                    out UIVertex bottomLeft,
                    out UIVertex topLeft,
                    out UIVertex topRight,
                    out UIVertex bottomRight);

                UIVertex leftRidgeBottom = LerpVertex(bottomLeft, bottomRight, seamLeft);
                UIVertex leftRidgeTop = LerpVertex(topLeft, topRight, seamLeft);
                UIVertex rightRidgeBottom = LerpVertex(bottomLeft, bottomRight, seamRight);
                UIVertex rightRidgeTop = LerpVertex(topLeft, topRight, seamRight);

                // The left plane rises into a bright ridge. A sharp light-to-dark transition
                // across the narrow centre strip reads as the flat central edge of a forged
                // sword, while the right plane gently rolls back toward the normal gold face.
                AppendFaceQuad(bottomLeft, topLeft, leftRidgeTop, leftRidgeBottom,
                    0.88f, 1.14f, lowestY, height);
                AppendFaceQuad(leftRidgeBottom, leftRidgeTop, rightRidgeTop, rightRidgeBottom,
                    1.14f, 0.76f, lowestY, height);
                AppendFaceQuad(rightRidgeBottom, rightRidgeTop, topRight, bottomRight,
                    0.82f, 1.00f, lowestY, height);
            }
        }

        private void AppendFaceQuad(
            UIVertex bottomLeft,
            UIVertex topLeft,
            UIVertex topRight,
            UIVertex bottomRight,
            float leftBrightness,
            float rightBrightness,
            float lowestY,
            float textHeight)
        {
            bottomLeft = ShadeFaceVertex(bottomLeft, leftBrightness, lowestY, textHeight);
            topLeft = ShadeFaceVertex(topLeft, leftBrightness, lowestY, textHeight);
            topRight = ShadeFaceVertex(topRight, rightBrightness, lowestY, textHeight);
            bottomRight = ShadeFaceVertex(bottomRight, rightBrightness, lowestY, textHeight);

            outputVertices.Add(bottomLeft);
            outputVertices.Add(topLeft);
            outputVertices.Add(topRight);
            outputVertices.Add(topRight);
            outputVertices.Add(bottomRight);
            outputVertices.Add(bottomLeft);
        }

        private static UIVertex ShadeFaceVertex(UIVertex vertex, float facetBrightness, float lowestY, float textHeight)
        {
            float normalizedHeight = Mathf.Clamp01((vertex.position.y - lowestY) / textHeight);
            float verticalLight = Mathf.Lerp(0.86f, 1.10f, normalizedHeight);
            float brightness = facetBrightness * verticalLight;
            vertex.color = Tint(vertex.color, brightness, brightness * 0.98f, brightness * 0.90f, 1f);
            return vertex;
        }

        private void AppendGraduatedFallback(IReadOnlyList<UIVertex> vertices, float lowestY, float highestY)
        {
            float height = Mathf.Max(0.001f, highestY - lowestY);
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = ShadeFaceVertex(vertices[i], 1f, lowestY, height);
                outputVertices.Add(vertex);
            }
        }

        private static void FindGlyphCorners(
            IReadOnlyList<UIVertex> vertices,
            int start,
            out UIVertex bottomLeft,
            out UIVertex topLeft,
            out UIVertex topRight,
            out UIVertex bottomRight)
        {
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            for (int i = start; i < start + 6; i++)
            {
                Vector3 position = vertices[i].position;
                minimumX = Mathf.Min(minimumX, position.x);
                maximumX = Mathf.Max(maximumX, position.x);
                minimumY = Mathf.Min(minimumY, position.y);
                maximumY = Mathf.Max(maximumY, position.y);
            }

            bottomLeft = FindClosestVertex(vertices, start, new Vector2(minimumX, minimumY));
            topLeft = FindClosestVertex(vertices, start, new Vector2(minimumX, maximumY));
            topRight = FindClosestVertex(vertices, start, new Vector2(maximumX, maximumY));
            bottomRight = FindClosestVertex(vertices, start, new Vector2(maximumX, minimumY));
        }

        private static UIVertex FindClosestVertex(IReadOnlyList<UIVertex> vertices, int start, Vector2 target)
        {
            UIVertex closest = vertices[start];
            float closestDistance = float.PositiveInfinity;
            for (int i = start; i < start + 6; i++)
            {
                Vector3 position = vertices[i].position;
                float distance = (new Vector2(position.x, position.y) - target).sqrMagnitude;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closest = vertices[i];
            }

            return closest;
        }

        private static UIVertex LerpVertex(UIVertex from, UIVertex to, float amount)
        {
            from.position = Vector3.LerpUnclamped(from.position, to.position, amount);
            from.normal = Vector3.LerpUnclamped(from.normal, to.normal, amount);
            from.tangent = Vector4.LerpUnclamped(from.tangent, to.tangent, amount);
            from.uv0 = Vector4.LerpUnclamped(from.uv0, to.uv0, amount);
            from.uv1 = Vector4.LerpUnclamped(from.uv1, to.uv1, amount);
            from.uv2 = Vector4.LerpUnclamped(from.uv2, to.uv2, amount);
            from.uv3 = Vector4.LerpUnclamped(from.uv3, to.uv3, amount);
            return from;
        }

        private void AppendTintedLayer(
            IReadOnlyList<UIVertex> vertices,
            Vector2 offset,
            float red,
            float green,
            float blue,
            float alpha)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = vertices[i];
                vertex.position += new Vector3(offset.x, offset.y, 0f);
                vertex.color = Tint(vertex.color, red, green, blue, alpha);
                outputVertices.Add(vertex);
            }
        }

        private void AppendSolidLayer(
            IReadOnlyList<UIVertex> vertices,
            Vector2 offset,
            float red,
            float green,
            float blue,
            float alpha)
        {
            var color = new Color(red, green, blue, alpha);
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = vertices[i];
                vertex.position += new Vector3(offset.x, offset.y, 0f);
                color.a = alpha * (vertex.color.a / 255f);
                vertex.color = color;
                outputVertices.Add(vertex);
            }
        }

        private static Color32 Tint(Color32 source, float red, float green, float blue, float alpha)
        {
            var color = (Color)source;
            color.r = Mathf.Clamp01(color.r * red);
            color.g = Mathf.Clamp01(color.g * green);
            color.b = Mathf.Clamp01(color.b * blue);
            color.a = Mathf.Clamp01(color.a * alpha);
            return color;
        }
    }
}
