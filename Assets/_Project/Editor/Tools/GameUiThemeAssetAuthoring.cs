using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityIsekaiGame.Editor
{
    public static class GameUiThemeAssetAuthoring
    {
        private const string PresentationRoot = "Assets/_Project/Presentation";
        private const string UiRoot = PresentationRoot + "/UI";
        private const string ResourcesRoot = UiRoot + "/Resources";
        private const string ThemeRoot = ResourcesRoot + "/Theme";
        private const string RoundedSurfacePath = ThemeRoot + "/GameUiRoundedSurface.png";

        [MenuItem("Tools/Unity Isekai Game/UI/Generate Production Theme Assets")]
        public static void GenerateProductionThemeAssets()
        {
            EnsureFolder(PresentationRoot, "UI");
            EnsureFolder(UiRoot, "Resources");
            EnsureFolder(ResourcesRoot, "Theme");
            WriteRoundedSurface();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated the production UI theme assets under '{ThemeRoot}'.");
        }

        private static void WriteRoundedSurface()
        {
            const int size = 64;
            const float radius = 14f;
            const int samplesPerAxis = 4;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try
            {
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int coveredSamples = 0;
                        for (int sampleY = 0; sampleY < samplesPerAxis; sampleY++)
                        {
                            for (int sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                            {
                                float pointX = x + (sampleX + 0.5f) / samplesPerAxis;
                                float pointY = y + (sampleY + 0.5f) / samplesPerAxis;
                                if (IsInsideRoundedRectangle(pointX, pointY, size, radius))
                                {
                                    coveredSamples++;
                                }
                            }
                        }

                        float alpha = coveredSamples / (float)(samplesPerAxis * samplesPerAxis);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(RoundedSurfacePath, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(RoundedSurfacePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(RoundedSurfacePath) as TextureImporter;
            if (importer == null)
            {
                throw new System.InvalidOperationException($"Unable to configure the generated UI sprite at '{RoundedSurfacePath}'.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(16f, 16f, 16f, 16f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static bool IsInsideRoundedRectangle(float x, float y, float size, float radius)
        {
            float nearestX = Mathf.Clamp(x, radius, size - radius);
            float nearestY = Mathf.Clamp(y, radius, size - radius);
            float deltaX = x - nearestX;
            float deltaY = y - nearestY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
