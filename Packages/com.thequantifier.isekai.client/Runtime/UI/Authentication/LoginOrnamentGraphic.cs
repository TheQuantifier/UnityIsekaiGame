using UnityEngine;
using UnityEngine.UI;

namespace UnityIsekaiGame.UI.Authentication
{
    /// <summary>
    /// Small, resolution-independent ornaments used by the runtime-built login screen.
    /// Keeping these elements as UI geometry avoids stretching a single baked mockup and
    /// preserves crisp borders at every supported resolution.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoginOrnamentGraphic : MaskableGraphic
    {
        public enum OrnamentStyle
        {
            Leather,
            Frame,
            Input,
            PrimaryButton,
            SecondaryButton,
            Separator,
            UserIcon,
            LockIcon,
            GearIcon,
            ControllerIcon
        }

        private static readonly Color DeepLeather = new Color(0.105f, 0.043f, 0.016f, 0.985f);
        private static readonly Color RaisedLeather = new Color(0.205f, 0.100f, 0.039f, 0.985f);
        private static readonly Color InsetLeather = new Color(0.075f, 0.027f, 0.009f, 0.98f);
        private static readonly Color AntiqueGold = new Color(0.70f, 0.43f, 0.12f, 1f);
        private static readonly Color BrightGold = new Color(0.96f, 0.72f, 0.26f, 1f);
        private static readonly Color DarkGold = new Color(0.37f, 0.19f, 0.045f, 1f);

        [SerializeField] private OrnamentStyle style;

        public OrnamentStyle Style
        {
            get => style;
            set
            {
                if (style == value)
                {
                    return;
                }

                style = value;
                SetVerticesDirty();
            }
        }

        public void Configure(OrnamentStyle ornamentStyle, bool receivesRaycasts = false)
        {
            style = ornamentStyle;
            raycastTarget = receivesRaycasts;
            color = Color.white;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            switch (style)
            {
                case OrnamentStyle.Leather:
                    AddGradientQuad(vertexHelper, rect, RaisedLeather, DeepLeather);
                    break;
                case OrnamentStyle.Frame:
                    DrawFrame(vertexHelper, rect);
                    break;
                case OrnamentStyle.Input:
                    DrawNotchedSurface(vertexHelper, rect, InsetLeather, DeepLeather, AntiqueGold, 1.25f, 9f);
                    break;
                case OrnamentStyle.PrimaryButton:
                    DrawNotchedSurface(vertexHelper, rect, BrightGold, new Color(0.60f, 0.32f, 0.07f, 1f), new Color(1f, 0.78f, 0.32f, 1f), 2f, 12f);
                    DrawInnerHighlight(vertexHelper, rect, 5f, new Color(1f, 0.88f, 0.52f, 0.68f));
                    break;
                case OrnamentStyle.SecondaryButton:
                    DrawNotchedSurface(vertexHelper, rect, RaisedLeather, DeepLeather, AntiqueGold, 1.6f, 10f);
                    break;
                case OrnamentStyle.Separator:
                    DrawSeparator(vertexHelper, rect);
                    break;
                case OrnamentStyle.UserIcon:
                    DrawUserIcon(vertexHelper, rect);
                    break;
                case OrnamentStyle.LockIcon:
                    DrawLockIcon(vertexHelper, rect);
                    break;
                case OrnamentStyle.GearIcon:
                    DrawGearIcon(vertexHelper, rect);
                    break;
                case OrnamentStyle.ControllerIcon:
                    DrawControllerIcon(vertexHelper, rect);
                    break;
            }
        }

        private static void DrawFrame(VertexHelper vh, Rect rect)
        {
            const float outerInset = 1.5f;
            const float innerInset = 7f;
            const float notch = 13f;
            Rect outer = Inset(rect, outerInset);
            Rect inner = Inset(rect, innerInset);
            AddNotchedOutline(vh, outer, notch, AntiqueGold, 1.8f);
            AddNotchedOutline(vh, inner, notch - 3f, new Color(AntiqueGold.r, AntiqueGold.g, AntiqueGold.b, 0.34f), 0.9f);

            float flourish = Mathf.Min(34f, rect.width * 0.065f);
            AddCornerFlourish(vh, new Vector2(outer.xMin + notch, outer.yMax - notch), new Vector2(1f, -1f), flourish);
            AddCornerFlourish(vh, new Vector2(outer.xMax - notch, outer.yMax - notch), new Vector2(-1f, -1f), flourish);
            AddCornerFlourish(vh, new Vector2(outer.xMin + notch, outer.yMin + notch), new Vector2(1f, 1f), flourish);
            AddCornerFlourish(vh, new Vector2(outer.xMax - notch, outer.yMin + notch), new Vector2(-1f, 1f), flourish);
        }

        private static void AddCornerFlourish(VertexHelper vh, Vector2 origin, Vector2 direction, float size)
        {
            Color softGold = new Color(AntiqueGold.r, AntiqueGold.g, AntiqueGold.b, 0.78f);
            Vector2 horizontal = new Vector2(direction.x, 0f);
            Vector2 vertical = new Vector2(0f, direction.y);
            AddThickLine(vh, origin, origin + horizontal * size, 1.2f, softGold);
            AddThickLine(vh, origin, origin + vertical * size, 1.2f, softGold);
            AddThickLine(vh, origin + horizontal * size * 0.28f, origin + horizontal * size * 0.52f + vertical * size * 0.22f, 1.2f, softGold);
            AddThickLine(vh, origin + vertical * size * 0.28f, origin + vertical * size * 0.52f + horizontal * size * 0.22f, 1.2f, softGold);
            AddDiamond(vh, origin + (horizontal + vertical) * size * 0.34f, 3.4f, softGold);
        }

        private static void DrawSeparator(VertexHelper vh, Rect rect)
        {
            float middle = rect.center.x;
            float y = rect.center.y;
            float gap = Mathf.Min(30f, rect.width * 0.09f);
            Color line = new Color(AntiqueGold.r, AntiqueGold.g, AntiqueGold.b, 0.82f);
            AddThickLine(vh, new Vector2(rect.xMin, y), new Vector2(middle - gap, y), 1.15f, line);
            AddThickLine(vh, new Vector2(middle + gap, y), new Vector2(rect.xMax, y), 1.15f, line);
            AddDiamondOutline(vh, new Vector2(middle, y), 7f, line, 1.2f);
            AddDiamond(vh, new Vector2(middle, y), 2.2f, BrightGold);
        }

        private static void DrawUserIcon(VertexHelper vh, Rect rect)
        {
            float scale = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center + Vector2.up * scale * 0.13f;
            AddCircle(vh, center, scale * 0.145f, BrightGold, 20);
            Vector2 leftShoulder = rect.center + new Vector2(-scale * 0.25f, -scale * 0.28f);
            Vector2 rightShoulder = rect.center + new Vector2(scale * 0.25f, -scale * 0.28f);
            Vector2 rightNeck = rect.center + new Vector2(scale * 0.11f, -scale * 0.02f);
            Vector2 leftNeck = rect.center + new Vector2(-scale * 0.11f, -scale * 0.02f);
            AddPolygon(vh, new[] { leftShoulder, rightShoulder, rightNeck, leftNeck }, BrightGold, BrightGold);
        }

        private static void DrawLockIcon(VertexHelper vh, Rect rect)
        {
            float scale = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            Color gold = BrightGold;
            float left = center.x - scale * 0.19f;
            float right = center.x + scale * 0.19f;
            float shackleBottom = center.y + scale * 0.02f;
            float shackleTop = center.y + scale * 0.27f;
            AddThickLine(vh, new Vector2(left, shackleBottom), new Vector2(left, shackleTop - scale * 0.07f), scale * 0.065f, gold);
            AddThickLine(vh, new Vector2(right, shackleBottom), new Vector2(right, shackleTop - scale * 0.07f), scale * 0.065f, gold);
            AddThickLine(vh, new Vector2(left, shackleTop - scale * 0.07f), new Vector2(center.x - scale * 0.10f, shackleTop), scale * 0.065f, gold);
            AddThickLine(vh, new Vector2(center.x - scale * 0.10f, shackleTop), new Vector2(center.x + scale * 0.10f, shackleTop), scale * 0.065f, gold);
            AddThickLine(vh, new Vector2(center.x + scale * 0.10f, shackleTop), new Vector2(right, shackleTop - scale * 0.07f), scale * 0.065f, gold);
            Rect body = new Rect(center.x - scale * 0.28f, center.y - scale * 0.28f, scale * 0.56f, scale * 0.36f);
            AddGradientQuad(vh, body, BrightGold, AntiqueGold);
            AddCircle(vh, center + Vector2.down * scale * 0.08f, scale * 0.045f, DarkGold, 12);
            AddThickLine(vh, center + Vector2.down * scale * 0.11f, center + Vector2.down * scale * 0.20f, scale * 0.035f, DarkGold);
        }

        private static void DrawGearIcon(VertexHelper vh, Rect rect)
        {
            float scale = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            float toothInner = scale * 0.30f;
            float toothOuter = scale * 0.43f;
            float halfWidth = scale * 0.09f;
            for (int index = 0; index < 10; index++)
            {
                float angle = index * Mathf.PI * 2f / 10f;
                Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 side = new Vector2(-outward.y, outward.x) * halfWidth;
                AddPolygon(vh, new[]
                {
                    center + outward * toothInner - side,
                    center + outward * toothInner + side,
                    center + outward * toothOuter + side * 0.72f,
                    center + outward * toothOuter - side * 0.72f
                }, BrightGold, AntiqueGold);
            }

            AddCircle(vh, center, scale * 0.32f, BrightGold, 32);
            AddCircle(vh, center, scale * 0.21f, AntiqueGold, 28);
            AddCircle(vh, center, scale * 0.095f, DeepLeather, 24);
        }

        private static void DrawControllerIcon(VertexHelper vh, Rect rect)
        {
            float scale = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            Vector2[] shell =
            {
                center + new Vector2(-0.34f, 0.20f) * scale,
                center + new Vector2(-0.22f, 0.31f) * scale,
                center + new Vector2(0.22f, 0.31f) * scale,
                center + new Vector2(0.34f, 0.20f) * scale,
                center + new Vector2(0.43f, -0.23f) * scale,
                center + new Vector2(0.31f, -0.34f) * scale,
                center + new Vector2(0.10f, -0.17f) * scale,
                center + new Vector2(-0.10f, -0.17f) * scale,
                center + new Vector2(-0.31f, -0.34f) * scale,
                center + new Vector2(-0.43f, -0.23f) * scale
            };
            AddPolygon(vh, shell, BrightGold, BrightGold);

            // Use the leather as negative space so every visible part of the icon remains
            // a single antique-gold color at any resolution.
            Vector2[] inset =
            {
                center + new Vector2(-0.28f, 0.15f) * scale,
                center + new Vector2(-0.18f, 0.23f) * scale,
                center + new Vector2(0.18f, 0.23f) * scale,
                center + new Vector2(0.28f, 0.15f) * scale,
                center + new Vector2(0.34f, -0.17f) * scale,
                center + new Vector2(0.28f, -0.22f) * scale,
                center + new Vector2(0.08f, -0.09f) * scale,
                center + new Vector2(-0.08f, -0.09f) * scale,
                center + new Vector2(-0.28f, -0.22f) * scale,
                center + new Vector2(-0.34f, -0.17f) * scale
            };
            AddPolygon(vh, inset, DeepLeather, DeepLeather);

            Color gold = BrightGold;
            Vector2 dpad = center + new Vector2(-0.17f, 0.015f) * scale;
            AddThickLine(vh, dpad + Vector2.left * scale * 0.10f, dpad + Vector2.right * scale * 0.10f, scale * 0.065f, gold);
            AddThickLine(vh, dpad + Vector2.down * scale * 0.10f, dpad + Vector2.up * scale * 0.10f, scale * 0.065f, gold);
            AddCircle(vh, center + new Vector2(0.15f, 0.07f) * scale, scale * 0.045f, gold, 14);
            AddCircle(vh, center + new Vector2(0.24f, -0.02f) * scale, scale * 0.045f, gold, 14);
        }

        private static void DrawNotchedSurface(VertexHelper vh, Rect rect, Color top, Color bottom, Color border, float borderWidth, float notch)
        {
            Vector2[] points = NotchedPoints(rect, notch);
            AddPolygon(vh, points, top, bottom);
            AddOutline(vh, points, border, borderWidth);
        }

        private static void DrawInnerHighlight(VertexHelper vh, Rect rect, float inset, Color color)
        {
            Rect inner = Inset(rect, inset);
            AddNotchedOutline(vh, inner, 8f, color, 0.8f);
        }

        private static void AddNotchedOutline(VertexHelper vh, Rect rect, float notch, Color color, float width)
        {
            AddOutline(vh, NotchedPoints(rect, notch), color, width);
        }

        private static Vector2[] NotchedPoints(Rect rect, float notch)
        {
            float amount = Mathf.Clamp(notch, 0f, Mathf.Min(rect.width, rect.height) * 0.25f);
            return new[]
            {
                new Vector2(rect.xMin + amount, rect.yMin),
                new Vector2(rect.xMax - amount, rect.yMin),
                new Vector2(rect.xMax, rect.yMin + amount),
                new Vector2(rect.xMax, rect.yMax - amount),
                new Vector2(rect.xMax - amount, rect.yMax),
                new Vector2(rect.xMin + amount, rect.yMax),
                new Vector2(rect.xMin, rect.yMax - amount),
                new Vector2(rect.xMin, rect.yMin + amount)
            };
        }

        private static void AddPolygon(VertexHelper vh, Vector2[] points, Color top, Color bottom)
        {
            if (points == null || points.Length < 3)
            {
                return;
            }

            float minY = points[0].y;
            float maxY = points[0].y;
            Vector2 center = Vector2.zero;
            for (int i = 0; i < points.Length; i++)
            {
                center += points[i];
                minY = Mathf.Min(minY, points[i].y);
                maxY = Mathf.Max(maxY, points[i].y);
            }
            center /= points.Length;

            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, Color.Lerp(bottom, top, 0.5f), Vector2.zero);
            for (int i = 0; i < points.Length; i++)
            {
                float blend = Mathf.InverseLerp(minY, maxY, points[i].y);
                vh.AddVert(points[i], Color.Lerp(bottom, top, blend), Vector2.zero);
            }

            for (int i = 0; i < points.Length; i++)
            {
                vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + ((i + 1) % points.Length));
            }
        }

        private static void AddOutline(VertexHelper vh, Vector2[] points, Color color, float width)
        {
            for (int i = 0; i < points.Length; i++)
            {
                AddThickLine(vh, points[i], points[(i + 1) % points.Length], width, color);
            }
        }

        private static void AddGradientQuad(VertexHelper vh, Rect rect, Color top, Color bottom)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(rect.xMin, rect.yMin), bottom, Vector2.zero);
            vh.AddVert(new Vector2(rect.xMin, rect.yMax), top, Vector2.up);
            vh.AddVert(new Vector2(rect.xMax, rect.yMax), top, Vector2.one);
            vh.AddVert(new Vector2(rect.xMax, rect.yMin), bottom, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddThickLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero);
            vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero);
            vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddCircle(VertexHelper vh, Vector2 center, float radius, Color color, int segments)
        {
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
            {
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }

        private static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            AddPolygon(vh, new[]
            {
                center + Vector2.up * radius,
                center + Vector2.right * radius,
                center + Vector2.down * radius,
                center + Vector2.left * radius
            }, color, color);
        }

        private static void AddDiamondOutline(VertexHelper vh, Vector2 center, float radius, Color color, float width)
        {
            AddOutline(vh, new[]
            {
                center + Vector2.up * radius,
                center + Vector2.right * radius,
                center + Vector2.down * radius,
                center + Vector2.left * radius
            }, color, width);
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, Mathf.Max(0f, rect.width - amount * 2f), Mathf.Max(0f, rect.height - amount * 2f));
        }
    }
}
