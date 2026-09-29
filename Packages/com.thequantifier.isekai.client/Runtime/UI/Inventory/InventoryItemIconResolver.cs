using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.Inventory;

namespace UnityIsekaiGame.UI.Inventory
{
    /// <summary>
    /// Uses authored item art when it exists and supplies a deterministic,
    /// item-shaped fallback for prototype content that has no icon yet.
    /// </summary>
    public static class InventoryItemIconResolver
    {
        private const int Size = 96;
        private static readonly Dictionary<string, Sprite> Generated = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (Sprite sprite in Generated.Values)
            {
                if (sprite == null) continue;
                Texture2D texture = sprite.texture;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(sprite);
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(sprite);
                    if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Generated.Clear();
        }

        public static Sprite Resolve(ItemDefinition item)
        {
            if (item == null) return null;
            if (item.Icon != null) return item.Icon;

            string key = string.IsNullOrWhiteSpace(item.ItemId) ? item.name : item.ItemId;
            key = string.IsNullOrWhiteSpace(key) ? "item" : key.Trim().ToLowerInvariant();
            if (Generated.TryGetValue(key, out Sprite existing) && existing != null) return existing;

            Sprite generated = Generate(key, string.IsNullOrWhiteSpace(item.DisplayName) ? item.name : item.DisplayName);
            Generated[key] = generated;
            return generated;
        }

        private static Sprite Generate(string key, string displayName)
        {
            Color32[] pixels = new Color32[Size * Size];
            Color accent = AccentFor(key);
            DrawSoftDisc(pixels, Size / 2, Size / 2, 43, new Color(0.16f, 0.095f, 0.045f, 0.98f));
            DrawRing(pixels, Size / 2, Size / 2, 42, 2, Color.Lerp(accent, Color.white, 0.2f));

            if (key.Contains("potion")) DrawPotion(pixels, accent);
            else if (key.Contains("sword")) DrawSword(pixels, accent);
            else if (key.Contains("shield")) DrawShield(pixels, accent);
            else if (key.Contains("helmet")) DrawHelmet(pixels, accent);
            else if (key.Contains("bow")) DrawBow(pixels, accent);
            else if (key.Contains("arrow")) DrawArrow(pixels, accent);
            else if (key.Contains("ore")) DrawOre(pixels, accent);
            else if (key.Contains("wood") || key.Contains("log")) DrawLogs(pixels, accent);
            else if (key.Contains("leather")) DrawLeather(pixels, accent);
            else if (key.Contains("parcel") || key.Contains("package")) DrawParcel(pixels, accent);
            else DrawFallback(pixels, accent, displayName);

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = $"Generated Icon - {displayName}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            return sprite;
        }

        private static Color AccentFor(string key)
        {
            if (key.Contains("health")) return new Color(0.82f, 0.2f, 0.2f, 1f);
            if (key.Contains("mana") || key.Contains("arcane")) return new Color(0.24f, 0.48f, 0.95f, 1f);
            if (key.Contains("stamina")) return new Color(0.2f, 0.72f, 0.34f, 1f);
            if (key.Contains("wood") || key.Contains("leather")) return new Color(0.62f, 0.36f, 0.16f, 1f);
            if (key.Contains("iron") || key.Contains("helmet") || key.Contains("sword") || key.Contains("shield")) return new Color(0.62f, 0.72f, 0.78f, 1f);
            if (key.Contains("parcel")) return new Color(0.78f, 0.58f, 0.25f, 1f);
            return new Color(0.88f, 0.62f, 0.18f, 1f);
        }

        private static void DrawPotion(Color32[] p, Color accent)
        {
            DrawRect(p, 40, 66, 56, 76, new Color(0.55f, 0.34f, 0.15f, 1f));
            DrawRect(p, 42, 57, 54, 67, new Color(0.78f, 0.86f, 0.9f, 1f));
            DrawSoftDisc(p, 48, 39, 22, new Color(0.75f, 0.88f, 0.94f, 0.92f));
            DrawRect(p, 29, 24, 67, 43, new Color(accent.r, accent.g, accent.b, 0.95f));
            DrawRing(p, 48, 39, 22, 3, Color.white);
        }

        private static void DrawSword(Color32[] p, Color accent)
        {
            DrawLine(p, 27, 25, 69, 69, 9, new Color(0.78f, 0.88f, 0.95f, 1f));
            DrawLine(p, 25, 27, 67, 69, 3, Color.white);
            DrawLine(p, 25, 36, 36, 25, 6, accent);
            DrawLine(p, 20, 20, 31, 31, 7, new Color(0.45f, 0.24f, 0.12f, 1f));
            FillTriangle(p, new Vector2Int(69, 69), new Vector2Int(58, 65), new Vector2Int(65, 58), Color.white);
        }

        private static void DrawShield(Color32[] p, Color accent)
        {
            Vector2Int[] shape = { new Vector2Int(25, 66), new Vector2Int(48, 78), new Vector2Int(71, 66), new Vector2Int(66, 32), new Vector2Int(48, 17), new Vector2Int(30, 32) };
            FillPolygon(p, shape, Color.Lerp(accent, Color.black, 0.12f));
            DrawLine(p, 48, 72, 48, 24, 5, Color.Lerp(accent, Color.white, 0.55f));
            DrawPolyline(p, shape, 3, Color.white, true);
        }

        private static void DrawHelmet(Color32[] p, Color accent)
        {
            DrawSoftDisc(p, 48, 49, 28, accent);
            DrawRect(p, 20, 20, 76, 47, new Color(0f, 0f, 0f, 0f));
            DrawRect(p, 24, 41, 72, 49, Color.Lerp(accent, Color.white, 0.4f));
            DrawRect(p, 45, 21, 52, 48, Color.Lerp(accent, Color.white, 0.55f));
            DrawRing(p, 48, 49, 28, 3, Color.white);
        }

        private static void DrawBow(Color32[] p, Color accent)
        {
            for (int y = 18; y <= 78; y++)
            {
                float normalized = (y - 48f) / 30f;
                int x = Mathf.RoundToInt(62f + 18f * (1f - normalized * normalized));
                DrawSoftDisc(p, x, y, 3, accent);
            }
            DrawLine(p, 63, 18, 63, 78, 2, Color.white);
            DrawLine(p, 26, 48, 73, 48, 3, Color.Lerp(accent, Color.white, 0.55f));
            FillTriangle(p, new Vector2Int(78, 48), new Vector2Int(67, 42), new Vector2Int(67, 54), Color.white);
        }

        private static void DrawArrow(Color32[] p, Color accent)
        {
            DrawLine(p, 22, 28, 70, 67, 5, Color.Lerp(accent, Color.white, 0.35f));
            FillTriangle(p, new Vector2Int(76, 73), new Vector2Int(61, 68), new Vector2Int(70, 58), Color.white);
            DrawLine(p, 20, 26, 18, 39, 4, accent);
            DrawLine(p, 20, 26, 33, 24, 4, accent);
        }

        private static void DrawOre(Color32[] p, Color accent)
        {
            DrawSoftDisc(p, 38, 43, 19, Color.Lerp(accent, Color.black, 0.25f));
            DrawSoftDisc(p, 58, 42, 18, accent);
            DrawSoftDisc(p, 49, 59, 17, Color.Lerp(accent, Color.white, 0.2f));
            DrawSoftDisc(p, 34, 50, 4, Color.white);
            DrawSoftDisc(p, 58, 50, 3, Color.white);
        }

        private static void DrawLogs(Color32[] p, Color accent)
        {
            DrawLine(p, 25, 30, 68, 62, 15, accent);
            DrawLine(p, 27, 62, 69, 31, 15, Color.Lerp(accent, Color.white, 0.12f));
            DrawSoftDisc(p, 24, 29, 8, new Color(0.83f, 0.58f, 0.28f, 1f));
            DrawSoftDisc(p, 27, 63, 8, new Color(0.83f, 0.58f, 0.28f, 1f));
            DrawRing(p, 24, 29, 5, 1, new Color(0.35f, 0.18f, 0.08f, 1f));
        }

        private static void DrawLeather(Color32[] p, Color accent)
        {
            Vector2Int[] shape = { new Vector2Int(25, 68), new Vector2Int(42, 76), new Vector2Int(58, 70), new Vector2Int(72, 55), new Vector2Int(65, 35), new Vector2Int(51, 20), new Vector2Int(34, 27), new Vector2Int(20, 45) };
            FillPolygon(p, shape, accent);
            DrawPolyline(p, shape, 3, Color.Lerp(accent, Color.white, 0.45f), true);
            DrawLine(p, 35, 58, 59, 38, 2, new Color(0.35f, 0.18f, 0.08f, 1f));
        }

        private static void DrawParcel(Color32[] p, Color accent)
        {
            DrawRect(p, 23, 25, 73, 70, accent);
            DrawRect(p, 45, 25, 52, 70, new Color(0.42f, 0.22f, 0.09f, 1f));
            DrawRect(p, 23, 45, 73, 52, new Color(0.42f, 0.22f, 0.09f, 1f));
            DrawLine(p, 23, 25, 23, 70, 3, Color.white);
            DrawLine(p, 73, 25, 73, 70, 3, Color.white);
            DrawLine(p, 23, 70, 73, 70, 3, Color.white);
        }

        private static void DrawFallback(Color32[] p, Color accent, string displayName)
        {
            DrawSoftDisc(p, 48, 48, 26, accent);
            DrawRing(p, 48, 48, 26, 3, Color.white);
            int hash = 17;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                for (int i = 0; i < displayName.Length; i++)
                {
                    hash = unchecked(hash * 31 + displayName[i]);
                }
            }
            DrawLine(p, 34, 34, 62, 62, 4, hash % 2 == 0 ? Color.white : Color.Lerp(accent, Color.white, 0.6f));
            DrawLine(p, 34, 62, 62, 34, 4, Color.white);
        }

        private static void DrawRect(Color32[] p, int x0, int y0, int x1, int y1, Color color)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Size - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Size - 1, x1); x++) Set(p, x, y, color);
        }

        private static void DrawSoftDisc(Color32[] p, int cx, int cy, int radius, Color color)
        {
            int radiusSquared = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radiusSquared) Set(p, x, y, color);
        }

        private static void DrawRing(Color32[] p, int cx, int cy, int radius, int thickness, Color color)
        {
            int outer = radius * radius;
            int inner = (radius - thickness) * (radius - thickness);
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int distance = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (distance <= outer && distance >= inner) Set(p, x, y, color);
                }
        }

        private static void DrawLine(Color32[] p, int x0, int y0, int x1, int y1, int thickness, Color color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps == 0) { DrawSoftDisc(p, x0, y0, Mathf.Max(1, thickness / 2), color); return; }
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                DrawSoftDisc(p, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), Mathf.Max(1, thickness / 2), color);
            }
        }

        private static void DrawPolyline(Color32[] p, IReadOnlyList<Vector2Int> points, int thickness, Color color, bool closed)
        {
            for (int i = 0; i < points.Count - 1; i++) DrawLine(p, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, thickness, color);
            if (closed && points.Count > 1) DrawLine(p, points[points.Count - 1].x, points[points.Count - 1].y, points[0].x, points[0].y, thickness, color);
        }

        private static void FillTriangle(Color32[] p, Vector2Int a, Vector2Int b, Vector2Int c, Color color)
        {
            FillPolygon(p, new[] { a, b, c }, color);
        }

        private static void FillPolygon(Color32[] p, IReadOnlyList<Vector2Int> points, Color color)
        {
            int minY = points[0].y;
            int maxY = points[0].y;
            for (int i = 1; i < points.Count; i++) { minY = Mathf.Min(minY, points[i].y); maxY = Mathf.Max(maxY, points[i].y); }
            for (int y = minY; y <= maxY; y++)
            {
                List<int> intersections = new List<int>();
                for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                {
                    Vector2Int pi = points[i];
                    Vector2Int pj = points[j];
                    if ((pi.y > y) == (pj.y > y)) continue;
                    intersections.Add(Mathf.RoundToInt(pj.x + (y - pj.y) * (pi.x - pj.x) / (float)(pi.y - pj.y)));
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2) DrawRect(p, intersections[i], y, intersections[i + 1], y, color);
            }
        }

        private static void Set(Color32[] p, int x, int y, Color color)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size) return;
            if (color.a <= 0f) { p[y * Size + x] = new Color32(0, 0, 0, 0); return; }
            p[y * Size + x] = color;
        }
    }
}
